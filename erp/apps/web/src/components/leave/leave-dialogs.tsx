'use client';

import { useEffect, useState } from 'react';
import { useTranslations } from 'next-intl';
import { Download } from 'lucide-react';
import {
  Dialog,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select } from '@/components/ui/select';
import { Badge } from '@/components/ui/badge';
import { EmployeePicker } from '@/components/employees/employee-picker';
import { DateRangePickerField } from '@/components/ui/date-picker';
import { FileDropzone } from '@/components/ui/file-dropzone';
import { Switch } from '@/components/ui/switch';
import { useBlockedLeaveDates, useLeaveBalance } from '@/hooks/use-leave';
import { useAttendancePolicy } from '@/hooks/use-attendance-settings';
import { useAuthStore, useHasRole } from '@/lib/auth/store';
import { useToast } from '@/hooks/use-toast';
import { downloadLeaveAttachment } from '@/lib/api/leave';
import { extractApiError } from '@/lib/api/client';
import type { HalfDayPeriod, LeaveQuota, LeaveRequest, LeaveType } from '@/lib/api/types';

export const LEAVE_TYPES: LeaveType[] = ['Annual', 'Sick', 'Permission', 'Unpaid'];

// Annual and Unpaid are mutually exclusive: on probation you get Unpaid, once confirmed you get
// Annual. Enforced server-side in CreateLeaveRequestHandler — this only keeps the picker honest.
const PROBATION_TYPES: LeaveType[] = ['Permission', 'Sick', 'Unpaid'];
const CONFIRMED_TYPES: LeaveType[] = ['Permission', 'Sick', 'Annual'];

// Legal in both sets, so resetting to it can never land on a type the employee cannot file.
const NEUTRAL_TYPE: LeaveType = 'Permission';

export const LEAVE_STATUS_VARIANT = {
  Pending: 'warning',
  Approved: 'success',
  Denied: 'destructive',
  Cancelled: 'secondary',
} as const;

// ponytail: mirrors LeaveRequest.ReasonMinLength; the server rejects anything shorter.
export const REASON_MIN_LENGTH = 2;

/** Mirrors LeaveRequest.AllowedAttachmentContentTypes and AttachmentMaxBytes on the server. */
export const ATTACHMENT_ACCEPT = ['application/pdf', 'image/jpeg', 'image/png'] as const;
export const ATTACHMENT_MAX_BYTES = 10 * 1024 * 1024;

// ponytail: mirrors LeaveRequest.AllowedHourlyBoundaries — 12:00 is excluded, and a range must
// stay on one side of it. Update both together if the lunch hour ever moves.
export const HOURLY_BOUNDARIES = [9, 10, 11, 13, 14, 15, 16, 17, 18] as const;

export function formatHour(hour: number): string {
  return `${String(hour).padStart(2, '0')}:00`;
}

/** "HH:mm" → net minutes from midnight, so shift math stays a plain subtraction. */
function minutesOfDay(hhmm: string): number {
  const [h = 0, m = 0] = hhmm.split(':').map(Number);
  return h * 60 + m;
}

// ponytail: mirrors the backend's hardcoded Mon–Fri workday rule (LeaveRequest.CountWorkdays);
// update both together if weekends ever become configurable.
export function countWorkdays(start: string, end: string): number {
  if (!start || !end) return 0;
  const from = new Date(`${start}T00:00:00Z`);
  const to = new Date(`${end}T00:00:00Z`);
  if (Number.isNaN(from.getTime()) || Number.isNaN(to.getTime()) || from > to) return 0;
  let count = 0;
  for (const d = new Date(from); d <= to; d.setUTCDate(d.getUTCDate() + 1)) {
    const dow = d.getUTCDay();
    if (dow !== 0 && dow !== 6) count += 1;
  }
  return count;
}

const dateFormatter = new Intl.DateTimeFormat('id-ID', { dateStyle: 'medium', timeZone: 'UTC' });

export function formatLeaveDate(ymd: string): string {
  return dateFormatter.format(new Date(`${ymd}T00:00:00Z`));
}

const dateTimeFormatter = new Intl.DateTimeFormat('id-ID', {
  dateStyle: 'medium',
  timeStyle: 'short',
});

interface CreateLeaveDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onConfirm: (
    employeeId: string,
    type: LeaveType,
    startDate: string,
    endDate: string,
    reason: string,
    attachment: File | null,
    halfDay: boolean,
    halfDayPeriod: HalfDayPeriod | null,
    startHour: number | null,
    endHour: number | null,
  ) => void | Promise<void>;
  submitting?: boolean;
  /** Set by the caller when the server rejected the attachment specifically — shown as its own
   * row under the uploader instead of a generic toast, since it names exactly what's wrong. */
  attachmentError?: string | null;
  /** Called when the user picks a new file, so a stale rejection isn't left showing. */
  onAttachmentErrorClear?: () => void;
}

const EMPTY_FORM = {
  employeeId: '',
  // Empty until an employee is picked — which set of types applies depends on their probation.
  type: '' as LeaveType | '',
  startDate: '',
  endDate: '',
  reason: '',
  halfDay: false,
  halfDayPeriod: 'Morning' as HalfDayPeriod,
  hourly: false,
  startHour: '' as number | '',
  endHour: '' as number | '',
};

export function CreateLeaveDialog({
  open, onOpenChange, onConfirm, submitting, attachmentError, onAttachmentErrorClear,
}: CreateLeaveDialogProps) {
  const t = useTranslations('leave');
  const tCommon = useTranslations('common');

  // Staff file only for themselves, so they get no picker (and cannot read the employee list).
  const canPickEmployee = useHasRole('Owner', 'Manager');
  const self = useAuthStore((s) => s.user);

  const [form, setForm] = useState(EMPTY_FORM);
  // Kept outside `form` because a File is not part of the serialisable shape the rest of the
  // form is, and it is cleared by its own rules — see the effect below.
  const [attachment, setAttachment] = useState<File | null>(null);

  // Only Sick takes a doctor's note, and it will not submit without one; the server rejects a
  // file on any other type outright.
  const needsAttachment = form.type === 'Sick';
  // Annual's own toggle and Izin's own toggle — mutually exclusive by type, never both shown.
  const canHalfDay = form.type === 'Annual';
  const canHourly = form.type === 'Permission';

  const policy = useAttendancePolicy();
  // No cap yet known means don't block on it client-side — the server enforces it regardless.
  const maxIzinHours = policy.data?.maxIzinHours ?? Infinity;

  const hourlyValid =
    !form.hourly
    || (form.startHour !== '' && form.endHour !== '' && form.startHour < form.endHour
      && form.endHour - form.startHour <= maxIzinHours);

  const workdays = countWorkdays(form.startDate, form.endDate);
  const canSubmit =
    !!form.employeeId
    && !!form.type
    && workdays > 0
    && form.reason.trim().length >= REASON_MIN_LENGTH
    && (!needsAttachment || !!attachment)
    && hourlyValid;

  // Disabled until an employee is picked. The server enforces the quota either way — this is
  // only so the request is not filed blind and rejected a second later.
  const balance = useLeaveBalance(form.employeeId || null);

  // Net working hours: shift length minus the 1-hour lunch LeaveRequest assumes at 12:00–13:00.
  // A full day of hours taken off is a full day charged — lunch was never work time to begin with.
  const netWorkingHours = policy.data
    ? (minutesOfDay(policy.data.shiftEnd) - minutesOfDay(policy.data.shiftStart) - 60) / 60
    : 8;

  const chargePerWorkday = form.halfDay
    ? 0.5
    : form.hourly && form.startHour !== '' && form.endHour !== '' && form.startHour < form.endHour
      ? (form.endHour - form.startHour) / netWorkingHours
      : 1;
  const chargedDays = workdays * chargePerWorkday;

  // Already-approved leave only conflicts when its hours actually overlap what's being built
  // here — the candidate window recomputes as half-day/hourly fields change, so the picker's
  // highlight tracks exactly what the server would accept.
  const blocked = useBlockedLeaveDates(form.employeeId || null, {
    halfDay: form.halfDay,
    halfDayPeriod: form.halfDay ? form.halfDayPeriod : null,
    startHour: form.hourly && form.startHour !== '' ? form.startHour : null,
    endHour: form.hourly && form.endHour !== '' ? form.endHour : null,
  });
  const quota = balance.data?.quotas.find((q) => q.type === form.type);

  const typesReady = !!form.employeeId && !!balance.data;
  const types = balance.data?.onProbation ? PROBATION_TYPES : CONFIRMED_TYPES;

  // Reset on every open and close, regardless of whether it closed via the dialog's own
  // affordances or a parent driving `open` to false after a successful submit.
  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setForm({ ...EMPTY_FORM, employeeId: canPickEmployee ? '' : (self?.employeeId ?? '') });
    setAttachment(null);
  }, [open, canPickEmployee, self?.employeeId]);

  // Switching away from Sick drops the file: sending one on any other type is rejected, and a
  // file left hanging on a hidden field is a file the user cannot see to remove.
  useEffect(() => {
    if (needsAttachment) return;
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setAttachment(null);
  }, [needsAttachment]);

  // Switching away from Annual/Permission drops the toggle each one owns: sending either on
  // the wrong type is rejected outright, and a value left hanging on a hidden field is a value
  // the user cannot see to clear.
  useEffect(() => {
    if (canHalfDay) return;
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setForm((s) => (s.halfDay ? { ...s, halfDay: false } : s));
  }, [canHalfDay]);

  useEffect(() => {
    if (canHourly) return;
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setForm((s) => (s.hourly ? { ...s, hourly: false, startHour: '', endHour: '' } : s));
  }, [canHourly]);

  // Switching employee can invalidate the chosen type (Annual for a probationer, Unpaid for a
  // confirmed employee), so land on the one type both sets share rather than leaving a value
  // the <select> no longer lists.
  useEffect(() => {
    if (!typesReady) return;
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setForm((prev) => (types.includes(prev.type as LeaveType) ? prev : { ...prev, type: NEUTRAL_TYPE }));
  }, [typesReady, types]);

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogHeader>
        <DialogTitle>{t('create.title')}</DialogTitle>
        <DialogDescription>{t('create.description')}</DialogDescription>
      </DialogHeader>

      <div className="mt-4 space-y-3">
        <div className="flex flex-col gap-1.5">
          <Label>{t('create.employee')}</Label>
          {canPickEmployee ? (
            <EmployeePicker
              value={form.employeeId}
              onChange={(v) => setForm((s) => ({ ...s, employeeId: v }))}
              placeholder={t('create.employeePlaceholder')}
            />
          ) : (
            <p className="rounded-lg border border-border bg-muted/40 px-3 py-2 text-sm">
              {self?.fullName ?? '—'}
            </p>
          )}
        </div>

        <div className="flex flex-col gap-1.5">
          <Label>{t('columns.type')}</Label>
          <Select
            value={form.type}
            disabled={!typesReady}
            onChange={(e) => setForm((s) => ({ ...s, type: e.target.value as LeaveType }))}
          >
            {typesReady ? (
              types.map((type) => (
                <option key={type} value={type}>{t(`type.${type}`)}</option>
              ))
            ) : (
              <option value="">-</option>
            )}
          </Select>
          {quota && <QuotaHint quota={quota} />}
        </div>

        {needsAttachment && (
          <div className="flex flex-col gap-1.5">
            <Label>{t('create.attachment')}</Label>
            <FileDropzone
              value={attachment}
              onChange={(file) => { onAttachmentErrorClear?.(); setAttachment(file); }}
              accept={ATTACHMENT_ACCEPT}
              maxBytes={ATTACHMENT_MAX_BYTES}
              disabled={submitting}
              hint={t('create.attachmentHint')}
            />
            {attachmentError && (
              <p className="text-xs text-destructive">{attachmentError}</p>
            )}
          </div>
        )}

        <div className="flex items-end gap-3">
          <div className="flex flex-1 flex-col gap-1.5">
            <Label>{t('create.dateRange')}</Label>
            <DateRangePickerField
              start={form.startDate}
              end={form.endDate}
              onChange={(startDate, endDate) => setForm((s) => ({ ...s, startDate, endDate }))}
              blockedDates={blocked.data?.blockedDates}
              partialDates={blocked.data?.partialDates}
              isDisabled={!form.employeeId}
            />
          </div>

          {canHalfDay && (
            <div className="flex flex-col gap-1.5">
              <Label htmlFor="half-day-toggle" className="cursor-pointer font-normal">
                {t('create.halfDayLabel')}
              </Label>
              <div className="flex h-9 items-center">
                <Switch
                  id="half-day-toggle"
                  checked={form.halfDay}
                  onCheckedChange={(halfDay) => setForm((s) => ({ ...s, halfDay }))}
                />
              </div>
            </div>
          )}

          {canHourly && (
            <div className="flex flex-col gap-1.5">
              <Label htmlFor="hourly-toggle" className="cursor-pointer font-normal">
                {t('create.hourlyLabel')}
              </Label>
              <div className="flex h-9 items-center">
                <Switch
                  id="hourly-toggle"
                  checked={form.hourly}
                  onCheckedChange={(hourly) => setForm((s) => ({ ...s, hourly, startHour: '', endHour: '' }))}
                />
              </div>
            </div>
          )}
        </div>

        {form.halfDay && (
          <div className="flex gap-2">
            {(['Morning', 'Afternoon'] as const).map((period) => (
              <Button
                key={period}
                type="button"
                variant={form.halfDayPeriod === period ? 'default' : 'outline'}
                className="flex-1"
                onClick={() => setForm((s) => ({ ...s, halfDayPeriod: period }))}
              >
                {t(`create.period${period}`)}
              </Button>
            ))}
          </div>
        )}

        {form.hourly && (
          <div className="grid grid-cols-2 gap-3">
            <div className="flex flex-col gap-1.5">
              <Label>{t('create.startHour')}</Label>
              <Select
                value={form.startHour}
                onChange={(e) => {
                  const startHour = Number(e.target.value);
                  setForm((s) => ({
                    ...s,
                    startHour,
                    // A start picked after the current end, or one that now puts the span over
                    // the configured max, leaves the end hour invalid — clear it rather than
                    // submit something that was never actually chosen for this start.
                    endHour: s.endHour !== '' && s.endHour > startHour && s.endHour - startHour <= maxIzinHours
                      ? s.endHour
                      : '',
                  }));
                }}
              >
                <option value="">-</option>
                {HOURLY_BOUNDARIES.map((hour) => (
                  <option key={hour} value={hour}>{formatHour(hour)}</option>
                ))}
              </Select>
            </div>
            <div className="flex flex-col gap-1.5">
              <Label>{t('create.endHour')}</Label>
              <Select
                value={form.endHour}
                disabled={form.startHour === ''}
                onChange={(e) => setForm((s) => ({ ...s, endHour: Number(e.target.value) }))}
              >
                <option value="">-</option>
                {/* Only hours after Start, and within the configured max span — "should not be
                    possible to Izin a whole day" is enforced here, not just server-side. */}
                {HOURLY_BOUNDARIES.filter((hour) =>
                  form.startHour !== '' && hour > form.startHour && hour - form.startHour <= maxIzinHours,
                ).map((hour) => (
                  <option key={hour} value={hour}>{formatHour(hour)}</option>
                ))}
              </Select>
            </div>
          </div>
        )}

        <div className="flex flex-col gap-1.5">
          <Label>{t('create.reason')}</Label>
          <Input
            value={form.reason}
            minLength={REASON_MIN_LENGTH}
            maxLength={500}
            onChange={(e) => setForm((s) => ({ ...s, reason: e.target.value }))}
            placeholder={t('create.reasonPlaceholder')}
          />
        </div>

        {/* A plain request charges one day per workday, so the count says everything. A half
            day or hourly Izin charges less than that, and showing the two numbers side by side
            reads as a contradiction — so spell out the per-day rate that connects them. */}
        <p className="text-sm text-muted-foreground">
          {workdays > 0 && form.halfDay
            ? t('create.chargePreviewHalfDay', {
                workdays,
                days: Number(chargedDays.toFixed(2)),
              })
            : workdays > 0 && form.hourly && form.startHour !== '' && form.endHour !== ''
              ? t('create.chargePreviewHourly', {
                  workdays,
                  hours: form.endHour - form.startHour,
                  days: Number(chargedDays.toFixed(2)),
                })
              : t('create.workdayPreview', { count: workdays })}
        </p>
      </div>

      <DialogFooter>
        <Button variant="outline" onClick={() => onOpenChange(false)} disabled={submitting}>
          {tCommon('cancel')}
        </Button>
        <Button
          onClick={() => {
            // canSubmit already guarantees this; the guard is what narrows '' out of the type.
            if (!form.type) return;
            onConfirm(
              form.employeeId,
              form.type,
              form.startDate,
              form.endDate,
              form.reason.trim(),
              attachment,
              form.halfDay,
              form.halfDay ? form.halfDayPeriod : null,
              form.hourly && form.startHour !== '' ? form.startHour : null,
              form.hourly && form.endHour !== '' ? form.endHour : null,
            );
          }}
          disabled={submitting || !canSubmit}
        >
          {submitting ? tCommon('loading') : t('create.confirm')}
        </Button>
      </DialogFooter>
    </Dialog>
  );
}

/** Remaining days for the selected type, or why there are none. */
function QuotaHint({ quota }: { quota: LeaveQuota }) {
  const t = useTranslations('leave');

  if (quota.remainingDays === null) {
    return <p className="text-xs text-muted-foreground">{t('quota.uncapped')}</p>;
  }

  return (
    <p className={quota.remainingDays > 0 ? 'text-xs text-muted-foreground' : 'text-xs text-destructive'}>
      {t('quota.remaining', {
        remaining: quota.remainingDays,
        used: quota.usedDays,
        entitled: quota.entitledDays ?? 0,
      })}
    </p>
  );
}

export type LeaveDecision = 'approve' | 'deny' | 'cancel';

interface DecideLeaveDialogProps {
  request: LeaveRequest | null;
  action: LeaveDecision | null;
  onOpenChange: (open: boolean) => void;
  onConfirm: (note: string | null) => void | Promise<void>;
  submitting?: boolean;
}

export function DecideLeaveDialog({
  request,
  action,
  onOpenChange,
  onConfirm,
  submitting,
}: DecideLeaveDialogProps) {
  const t = useTranslations('leave');
  const tCommon = useTranslations('common');
  const [note, setNote] = useState('');

  const open = !!request && !!action;

  // `open` going false doesn't unmount this component (render just returns null
  // below), so state would otherwise leak into the next request/action shown.
  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    if (!open) setNote('');
  }, [open]);

  if (!open) return null;

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogHeader>
        <DialogTitle>{t(`decide.${action}.title`)}</DialogTitle>
        <DialogDescription>
          {t(`decide.${action}.description`, {
            employee: request.employeeFullName,
            from: formatLeaveDate(request.startDate),
            to: formatLeaveDate(request.endDate),
            count: request.workdayCount,
          })}
        </DialogDescription>
      </DialogHeader>

      {action !== 'approve' && (
        <div className="mt-4 flex flex-col gap-1.5">
          <Label>{t('decide.note')}</Label>
          <Input
            value={note}
            maxLength={500}
            onChange={(e) => setNote(e.target.value)}
            placeholder={t('decide.notePlaceholder')}
          />
        </div>
      )}

      <DialogFooter>
        <Button variant="outline" onClick={() => onOpenChange(false)} disabled={submitting}>
          {tCommon('cancel')}
        </Button>
        <Button
          variant={action === 'approve' ? 'default' : 'destructive'}
          onClick={() => onConfirm(note || null)}
          disabled={submitting}
        >
          {submitting ? tCommon('loading') : t(`decide.${action}.confirm`)}
        </Button>
      </DialogFooter>
    </Dialog>
  );
}

interface LeaveDetailsDialogProps {
  request: LeaveRequest | null;
  onOpenChange: (open: boolean) => void;
}

export function LeaveDetailsDialog({ request, onOpenChange }: LeaveDetailsDialogProps) {
  const t = useTranslations('leave');

  if (!request) return null;

  // The server strips type, reason and note together, so a null type is the tell that this
  // row's details are withheld. "Not shown" and "none given" are different claims — a blank
  // reason on a colleague's row must not read as "they gave no reason".
  const detailsHidden = request.type === null;
  const withheld = t('details.hidden');

  const rows: [string, string][] = [
    [t('columns.employee'), request.employeeFullName],
    [t('columns.type'), t(`type.${request.type ?? 'Undisclosed'}`)],
    [t('columns.dates'), `${formatLeaveDate(request.startDate)} – ${formatLeaveDate(request.endDate)}`],
    [t('columns.workdays'), String(request.workdayCount)],
    // Only shown when this request is a fraction of a day — a plain request's charge is
    // already exactly its workday count, so repeating that number here would be noise.
    ...(request.halfDay
      ? ([[t('details.chargedDays'), `${t('create.halfDayLabel')} (${t(`create.period${request.halfDayPeriod}`)})`]] as [string, string][])
      : request.startHour !== null && request.endHour !== null
        ? ([[t('details.chargedDays'), `${formatHour(request.startHour)} – ${formatHour(request.endHour)}`]] as [string, string][])
        : []),
    [t('columns.approvedThisYear'), request.approvedWorkdaysThisYear?.toString() ?? withheld],
    // The quota block is for this row's own type, unlike the all-types tally above it.
    ...(request.quota
      ? ([[
          t('details.quota'),
          request.quota.remainingDays === null
            ? t('quota.uncapped')
            : t('quota.remaining', {
                remaining: request.quota.remainingDays,
                used: request.quota.usedDays,
                entitled: request.quota.entitledDays ?? 0,
              }),
        ]] as [string, string][])
      : []),
    [t('details.reason'), detailsHidden ? withheld : request.reason || '–'],
    [t('details.requestedAt'), dateTimeFormatter.format(new Date(request.requestedAtUtc))],
    [t('details.decidedBy'), request.decidedByName || '–'],
    [
      t('details.decidedAt'),
      request.decidedAtUtc ? dateTimeFormatter.format(new Date(request.decidedAtUtc)) : '–',
    ],
    [t('details.decisionNote'), detailsHidden ? withheld : request.decisionNote || '–'],
    // Not gated behind detailsHidden — "who moved my leave, and from when" is the employee's
    // own business, and the dates themselves are already visible to every colleague.
    ...(request.editedAtUtc && request.previousStartDate && request.previousEndDate
      ? ([[
          t('details.editedBy'),
          t('details.editedValue', {
            name: request.editedByName ?? '–',
            at: dateTimeFormatter.format(new Date(request.editedAtUtc)),
            from: formatLeaveDate(request.previousStartDate),
            to: formatLeaveDate(request.previousEndDate),
          }),
        ]] as [string, string][])
      : []),
    // Not gated behind detailsHidden — no more sensitive than the Cancelled status itself.
    ...(request.status === 'Cancelled' && request.cancellationReason
      ? ([[t('details.cancellationReason'), t(`cancellationReason.${request.cancellationReason}`)]] as [string, string][])
      : []),
  ];

  return (
    <Dialog open onOpenChange={onOpenChange}>
      <DialogHeader>
        <DialogTitle>{t('details.title')}</DialogTitle>
        <DialogDescription>
          <Badge variant={LEAVE_STATUS_VARIANT[request.status]}>{t(`status.${request.status}`)}</Badge>
        </DialogDescription>
      </DialogHeader>

      <dl className="mt-4 space-y-2 text-sm">
        {rows.map(([label, value]) => (
          <div key={label} className="flex justify-between gap-4">
            <dt className="shrink-0 text-muted-foreground">{label}</dt>
            <dd className="text-right font-medium">{value}</dd>
          </div>
        ))}
        {request.attachment && (
          <div className="flex justify-between gap-4">
            <dt className="shrink-0 text-muted-foreground">{t('details.attachment')}</dt>
            <dd className="text-right font-medium">
              <AttachmentLink requestId={request.id} fileName={request.attachment.fileName} />
            </dd>
          </div>
        )}
      </dl>
    </Dialog>
  );
}

/**
 * Downloads the doctor's note. Fetched through the API client rather than linked directly:
 * the endpoint is authenticated, so a bare href would arrive without the bearer token.
 */
function AttachmentLink({ requestId, fileName }: { requestId: string; fileName: string }) {
  const t = useTranslations('leave');
  const toast = useToast();
  const [downloading, setDownloading] = useState(false);

  async function download() {
    setDownloading(true);
    try {
      const blob = await downloadLeaveAttachment(requestId);
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = fileName;
      link.click();
      URL.revokeObjectURL(url);
    } catch (err) {
      toast.error(t('details.attachmentError'), extractApiError(err).message);
    } finally {
      setDownloading(false);
    }
  }

  return (
    <button
      type="button"
      onClick={download}
      disabled={downloading}
      className="inline-flex items-center gap-1.5 text-primary hover:underline disabled:opacity-50"
    >
      <Download className="h-3.5 w-3.5 shrink-0" />
      <span className="truncate">{fileName}</span>
    </button>
  );
}
