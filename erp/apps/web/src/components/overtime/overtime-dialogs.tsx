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
import { EmployeePicker } from '@/components/employees/employee-picker';
import { DatePickerField } from '@/components/ui/date-picker';
import { FileDropzone } from '@/components/ui/file-dropzone';
import { Select } from '@/components/ui/select';
import { ATTACHMENT_ACCEPT, ATTACHMENT_MAX_BYTES, REASON_MIN_LENGTH, useFormatLeaveDate } from '@/components/leave/leave-dialogs';
import { markedDates, useDayMarkers } from '@/hooks/use-day-markers';
import { useHolidayCalendar } from '@/hooks/use-attendance-settings';
import { useOvertimeList } from '@/hooks/use-overtime';
import { useToast } from '@/hooks/use-toast';
import { extractApiError } from '@/lib/api/client';
import type { CreateRapelBody, OvertimeAssignment, OvertimeCorrectionKind, Rapel } from '@/lib/api/types';
import { formatIdr } from '@/lib/utils';

export const OVERTIME_STATUS_VARIANT = {
  Pending: 'warning',
  Approved: 'success',
  Rejected: 'destructive',
  Cancelled: 'secondary',
  Expired: 'secondary',
} as const;

// ponytail: mirror OvertimeAssignment.WeekdayStart / DayBoundary; the server re-checks both.
const WEEKDAY_START = '18:30';
const DAY_BOUNDARY = '05:00';

/** "18:30:00" → "18:30". */
export const hhmm = (time: string) => time.slice(0, 5);

/** A window is valid when it ends later the same day, or runs into the small hours by 05:00. */
function validWindow(start: string, end: string, dayOff: boolean): boolean {
  if (!start || !end || start === end) return false;
  if (dayOff && start < DAY_BOUNDARY) return false;
  return end > start || end <= DAY_BOUNDARY;
}

function isDayOffDate(date: string, holidays: ReadonlySet<string>): boolean {
  const dow = new Date(`${date}T00:00:00Z`).getUTCDay();
  return dow === 0 || dow === 6 || holidays.has(date);
}

/** A proof download, fetched through an endpoint that re-checks who is asking. */
export function ProofLink({ fileName, fetchProof }: { fileName: string; fetchProof: () => Promise<Blob> }) {
  const t = useTranslations('overtime');
  const toast = useToast();
  const [busy, setBusy] = useState(false);

  async function download() {
    setBusy(true);
    try {
      const url = URL.createObjectURL(await fetchProof());
      const link = document.createElement('a');
      link.href = url;
      link.download = fileName;
      link.click();
      URL.revokeObjectURL(url);
    } catch (err) {
      toast.error(t('proofError'), extractApiError(err).message);
    } finally {
      setBusy(false);
    }
  }

  return (
    <button
      type="button"
      onClick={download}
      disabled={busy}
      className="inline-flex items-center gap-1.5 text-primary hover:underline disabled:opacity-50"
    >
      <Download className="h-3.5 w-3.5 shrink-0" />
      <span className="truncate">{fileName}</span>
    </button>
  );
}

interface NoteDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  title: string;
  description: string;
  confirmLabel: string;
  destructive?: boolean;
  /** Shows the optional note field. Off for approvals and for the period close. */
  withNote?: boolean;
  submitting?: boolean;
  onConfirm: (note: string | null) => void | Promise<void>;
}

/** One confirm-with-optional-note dialog for every approve / reject / cancel / close. */
export function NoteDialog({
  open, onOpenChange, title, description, confirmLabel, destructive, withNote, submitting, onConfirm,
}: NoteDialogProps) {
  const t = useTranslations('overtime');
  const tCommon = useTranslations('common');
  const [note, setNote] = useState('');

  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    if (!open) setNote('');
  }, [open]);

  if (!open) return null;

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogHeader>
        <DialogTitle>{title}</DialogTitle>
        <DialogDescription>{description}</DialogDescription>
      </DialogHeader>
      {withNote && (
        <div className="mt-4 flex flex-col gap-1.5">
          <Label>{t('note')}</Label>
          <Input value={note} maxLength={500} onChange={(e) => setNote(e.target.value)} placeholder={t('notePlaceholder')} />
        </div>
      )}
      <DialogFooter>
        <Button variant="outline" onClick={() => onOpenChange(false)} disabled={submitting}>
          {tCommon('cancel')}
        </Button>
        <Button variant={destructive ? 'destructive' : 'default'} onClick={() => onConfirm(note || null)} disabled={submitting}>
          {submitting ? tCommon('loading') : confirmLabel}
        </Button>
      </DialogFooter>
    </Dialog>
  );
}

interface CreateOvertimeDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  submitting?: boolean;
  onConfirm: (body: { employeeId: string; date: string; startTime: string | null; endTime: string }) => void | Promise<void>;
}

export function CreateOvertimeDialog({ open, onOpenChange, submitting, onConfirm }: CreateOvertimeDialogProps) {
  const t = useTranslations('overtime');
  const tCommon = useTranslations('common');
  const holidays = useHolidayCalendar();

  const [employeeId, setEmployeeId] = useState('');
  const [date, setDate] = useState('');
  const [start, setStart] = useState('');
  const [end, setEnd] = useState('');

  useEffect(() => {
    if (open) return;
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setEmployeeId(''); setDate(''); setStart(''); setEnd('');
  }, [open]);

  // Leave days stay pickable (OT wins them); a date already holding OT is refused — one per employee per date.
  const markers = useDayMarkers(employeeId || null);

  // Weekday or day off is decided here from the calendar for the form, and again — authoritatively,
  // and then frozen onto the OT assignment — by the server.
  const dayOff = !!date && isDayOffDate(date, holidays.dates);
  const effectiveStart = dayOff ? start : WEEKDAY_START;
  const canSubmit = !!employeeId && !!date && validWindow(effectiveStart, end, dayOff);

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogHeader>
        <DialogTitle>{t('create.title')}</DialogTitle>
        <DialogDescription>{t('create.description')}</DialogDescription>
      </DialogHeader>

      <div className="mt-4 space-y-4">
        <div className="flex flex-col gap-1.5">
          <Label>{t('create.employee')}</Label>
          <EmployeePicker value={employeeId} onChange={setEmployeeId} placeholder={t('create.employeePlaceholder')} clearable={false} />
        </div>
        <div className="flex flex-col gap-1.5">
          <Label>{t('create.date')}</Label>
          <DatePickerField
            value={date}
            onChange={setDate}
            blockedDates={markedDates(markers, ['Overtime', 'OvertimePending'])}
            markers={markers}
            aria-label={t('create.date')}
          />
          {date && (
            <p className="text-xs text-muted-foreground">{dayOff ? t('create.dayOffHint') : t('create.weekdayHint')}</p>
          )}
        </div>
        <div className="grid grid-cols-2 gap-3">
          <div className="flex flex-col gap-1.5">
            <Label>{t('create.start')}</Label>
            {dayOff ? (
              <Input type="time" min={DAY_BOUNDARY} value={start} onChange={(e) => setStart(e.target.value)} />
            ) : (
              <Input type="time" value={WEEKDAY_START} disabled />
            )}
          </div>
          <div className="flex flex-col gap-1.5">
            <Label>{t('create.end')}</Label>
            <Input type="time" value={end} onChange={(e) => setEnd(e.target.value)} />
          </div>
        </div>
        <p className="text-xs text-muted-foreground">{t('create.windowHint')}</p>
      </div>

      <DialogFooter>
        <Button variant="outline" onClick={() => onOpenChange(false)} disabled={submitting}>
          {tCommon('cancel')}
        </Button>
        <Button
          onClick={() => onConfirm({ employeeId, date, startTime: dayOff ? start : null, endTime: end })}
          disabled={!canSubmit || submitting}
        >
          {submitting ? tCommon('loading') : t('create.confirm')}
        </Button>
      </DialogFooter>
    </Dialog>
  );
}

interface EditOvertimeEndDialogProps {
  assignment: OvertimeAssignment | null;
  onOpenChange: (open: boolean) => void;
  submitting?: boolean;
  onConfirm: (endTime: string) => void | Promise<void>;
}

/** Only the end moves — the date never does (cancel and assign again), and the start is fixed or already chosen. */
export function EditOvertimeEndDialog({ assignment, onOpenChange, submitting, onConfirm }: EditOvertimeEndDialogProps) {
  const t = useTranslations('overtime');
  const tCommon = useTranslations('common');
  const [end, setEnd] = useState('');

  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setEnd(assignment ? hhmm(assignment.endTime) : '');
  }, [assignment]);

  if (!assignment) return null;
  const valid = validWindow(hhmm(assignment.startTime), end, assignment.isDayOff);

  return (
    <Dialog open onOpenChange={onOpenChange}>
      <DialogHeader>
        <DialogTitle>{t('edit.title')}</DialogTitle>
        <DialogDescription>
          {t('edit.description', { employee: assignment.employeeFullName, start: hhmm(assignment.startTime) })}
        </DialogDescription>
      </DialogHeader>
      <div className="mt-4 flex flex-col gap-1.5">
        <Label>{t('create.end')}</Label>
        <Input type="time" value={end} onChange={(e) => setEnd(e.target.value)} />
        <p className="text-xs text-muted-foreground">{t('edit.hint')}</p>
      </div>
      <DialogFooter>
        <Button variant="outline" onClick={() => onOpenChange(false)} disabled={submitting}>
          {tCommon('cancel')}
        </Button>
        <Button onClick={() => onConfirm(end)} disabled={!valid || submitting}>
          {submitting ? tCommon('loading') : t('edit.confirm')}
        </Button>
      </DialogFooter>
    </Dialog>
  );
}

interface CorrectionDialogProps {
  assignment: OvertimeAssignment | null;
  onOpenChange: (open: boolean) => void;
  submitting?: boolean;
  /** The server's own message when it refused the proof, shown under the uploader. */
  attachmentError?: string | null;
  onConfirm: (body: { kind: OvertimeCorrectionKind; time: string; reason: string; attachment: File }) => void | Promise<void>;
}

/** The employee asks for the OT tap-in or tap-out they missed. The system never invents a punch. */
export function CorrectionDialog({ assignment, onOpenChange, submitting, attachmentError, onConfirm }: CorrectionDialogProps) {
  const t = useTranslations('overtime');
  const tCommon = useTranslations('common');
  const [time, setTime] = useState('');
  const [reason, setReason] = useState('');
  const [attachment, setAttachment] = useState<File | null>(null);

  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setTime(''); setReason(''); setAttachment(null);
  }, [assignment]);

  if (!assignment) return null;
  // No tap-in yet means that is the missing one; otherwise it is the tap-out.
  const kind: OvertimeCorrectionKind = assignment.tapInUtc ? 'TapOut' : 'TapIn';
  const canSubmit = !!time && reason.trim().length >= REASON_MIN_LENGTH && !!attachment;

  return (
    <Dialog open onOpenChange={onOpenChange}>
      <DialogHeader>
        <DialogTitle>{t('correction.title')}</DialogTitle>
        <DialogDescription>
          {t('correction.description', {
            kind: t(`correction.kind.${kind}`),
            start: hhmm(assignment.startTime),
            end: hhmm(assignment.endTime),
          })}
        </DialogDescription>
      </DialogHeader>
      <div className="mt-4 space-y-4">
        <div className="flex flex-col gap-1.5">
          <Label>{t('correction.time', { kind: t(`correction.kind.${kind}`) })}</Label>
          <Input type="time" value={time} onChange={(e) => setTime(e.target.value)} />
          <p className="text-xs text-muted-foreground">{t('correction.timeHint', { end: hhmm(assignment.endTime) })}</p>
        </div>
        <div className="flex flex-col gap-1.5">
          <Label>{t('correction.reason')}</Label>
          <Input value={reason} maxLength={500} onChange={(e) => setReason(e.target.value)} placeholder={t('correction.reasonPlaceholder')} />
        </div>
        <div className="flex flex-col gap-1.5">
          <Label>{t('correction.proof')}</Label>
          <FileDropzone value={attachment} onChange={setAttachment} accept={ATTACHMENT_ACCEPT} maxBytes={ATTACHMENT_MAX_BYTES} hint={t('proofHint')} />
          {attachmentError && <p className="text-xs text-destructive">{attachmentError}</p>}
        </div>
      </div>
      <DialogFooter>
        <Button variant="outline" onClick={() => onOpenChange(false)} disabled={submitting}>
          {tCommon('cancel')}
        </Button>
        <Button onClick={() => attachment && onConfirm({ kind, time, reason: reason.trim(), attachment })} disabled={!canSubmit || submitting}>
          {submitting ? tCommon('loading') : t('correction.confirm')}
        </Button>
      </DialogFooter>
    </Dialog>
  );
}

interface RapelDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  /** Fixed for an employee's own claim; null lets the Owner pick whose. */
  employeeId: string | null;
  /** The closed period being claimed: only its approved overtime can be picked. */
  period: { start: string; end: string } | null;
  /** True for an Owner adding one directly: they may set the amount, or leave it to the suggestion. */
  withAmount?: boolean;
  submitting?: boolean;
  attachmentError?: string | null;
  onConfirm: (body: CreateRapelBody) => void | Promise<void>;
}

/**
 * A late claim on one approved overtime of a closed period: the times worked, a note and the WA
 * proof. The server suggests the amount from those times; the employee never names one.
 */
export function RapelDialog({
  open, onOpenChange, employeeId, period, withAmount, submitting, attachmentError, onConfirm,
}: RapelDialogProps) {
  const t = useTranslations('overtime');
  const tCommon = useTranslations('common');
  const formatDate = useFormatLeaveDate();
  const [who, setWho] = useState('');
  const [assignmentId, setAssignmentId] = useState('');
  const [from, setFrom] = useState('');
  const [to, setTo] = useState('');
  const [note, setNote] = useState('');
  const [amount, setAmount] = useState('');
  const [attachment, setAttachment] = useState<File | null>(null);

  useEffect(() => {
    if (open) return;
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setWho(''); setAssignmentId(''); setFrom(''); setTo(''); setNote(''); setAmount(''); setAttachment(null);
  }, [open]);

  const target = employeeId ?? who;
  const { data } = useOvertimeList(
    { employeeId: target, from: period?.start, to: period?.end, status: 'Approved', pageSize: 100 },
    open && !!target && !!period,
  );
  const claimable = (data?.items ?? []).filter((a) => a.isFrozen);

  const pick = (id: string) => {
    setAssignmentId(id);
    const chosen = claimable.find((a) => a.id === id);
    // Prefilled with the assigned window; the employee trims it to what they really worked.
    setFrom(chosen ? hhmm(chosen.startTime) : '');
    setTo(chosen ? hhmm(chosen.endTime) : '');
  };

  const amountOk = !amount || Number(amount) > 0;
  const canSubmit = !!assignmentId && !!from && !!to && from !== to
    && note.trim().length >= REASON_MIN_LENGTH && !!attachment && amountOk;

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogHeader>
        <DialogTitle>{withAmount ? t('rapel.addTitle') : t('rapel.requestTitle')}</DialogTitle>
        <DialogDescription>{withAmount ? t('rapel.addDescription') : t('rapel.requestDescription')}</DialogDescription>
      </DialogHeader>
      <div className="mt-4 space-y-4">
        {employeeId === null && (
          <div className="flex flex-col gap-1.5">
            <Label>{t('create.employee')}</Label>
            <EmployeePicker value={who} onChange={(id) => { setWho(id); pick(''); }} placeholder={t('create.employeePlaceholder')} clearable={false} />
          </div>
        )}
        <div className="flex flex-col gap-1.5">
          <Label>{t('rapel.overtime')}</Label>
          <Select value={assignmentId} onChange={(e) => pick(e.target.value)} disabled={!target} aria-label={t('rapel.overtime')}>
            <option value="">{t('rapel.overtimePlaceholder')}</option>
            {claimable.map((a) => (
              <option key={a.id} value={a.id}>
                {formatDate(a.date)} · {hhmm(a.startTime)}–{hhmm(a.endTime)}{a.endsNextDay ? ' (+1)' : ''}
              </option>
            ))}
          </Select>
          {target && data && claimable.length === 0 && <p className="text-xs text-muted-foreground">{t('rapel.noOvertime')}</p>}
        </div>
        <div className="grid grid-cols-2 gap-3">
          <div className="flex flex-col gap-1.5">
            <Label>{t('rapel.from')}</Label>
            <Input type="time" value={from} onChange={(e) => setFrom(e.target.value)} disabled={!assignmentId} />
          </div>
          <div className="flex flex-col gap-1.5">
            <Label>{t('rapel.to')}</Label>
            <Input type="time" value={to} onChange={(e) => setTo(e.target.value)} disabled={!assignmentId} />
          </div>
          <p className="col-span-2 text-xs text-muted-foreground">{t('rapel.timesHint')}</p>
        </div>
        <div className="flex flex-col gap-1.5">
          <Label>{t('rapel.note')}</Label>
          <Input value={note} maxLength={500} onChange={(e) => setNote(e.target.value)} placeholder={t('rapel.notePlaceholder')} />
        </div>
        {withAmount && (
          <div className="flex flex-col gap-1.5">
            <Label>{t('rapel.amount')}</Label>
            <Input type="number" min={0} step={1000} value={amount} onChange={(e) => setAmount(e.target.value)} placeholder={t('rapel.amountSuggestedPlaceholder')} />
          </div>
        )}
        <div className="flex flex-col gap-1.5">
          <Label>{t('rapel.proof')}</Label>
          <FileDropzone value={attachment} onChange={setAttachment} accept={ATTACHMENT_ACCEPT} maxBytes={ATTACHMENT_MAX_BYTES} hint={t('proofHint')} />
          {attachmentError && <p className="text-xs text-destructive">{attachmentError}</p>}
        </div>
      </div>
      <DialogFooter>
        <Button variant="outline" onClick={() => onOpenChange(false)} disabled={submitting}>
          {tCommon('cancel')}
        </Button>
        <Button
          onClick={() => attachment && onConfirm({
            assignmentId, from, to, note: note.trim(), attachment,
            amount: withAmount && amount ? Number(amount) : undefined,
          })}
          disabled={!canSubmit || submitting}
        >
          {submitting ? tCommon('loading') : withAmount ? t('rapel.addConfirm') : t('rapel.requestConfirm')}
        </Button>
      </DialogFooter>
    </Dialog>
  );
}

interface ApproveRapelDialogProps {
  rapel: Rapel | null;
  onOpenChange: (open: boolean) => void;
  submitting?: boolean;
  onConfirm: (amount: number) => void | Promise<void>;
}

/** The Owner decides the figure: the suggestion is prefilled, and they may change it. */
export function ApproveRapelDialog({ rapel, onOpenChange, submitting, onConfirm }: ApproveRapelDialogProps) {
  const t = useTranslations('overtime');
  const tCommon = useTranslations('common');
  const formatDate = useFormatLeaveDate();
  const [amount, setAmount] = useState('');

  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setAmount(rapel ? String(rapel.suggestedAmount) : '');
  }, [rapel]);

  if (!rapel) return null;

  return (
    <Dialog open onOpenChange={onOpenChange}>
      <DialogHeader>
        <DialogTitle>{t('rapel.approveTitle')}</DialogTitle>
        <DialogDescription>{t('rapel.approveDescription', { employee: rapel.employeeFullName })}</DialogDescription>
      </DialogHeader>
      <div className="mt-4 space-y-4">
        <p className="text-sm">
          {t('rapel.claimSummary', {
            date: formatDate(rapel.workDate),
            from: hhmm(rapel.claimedStart),
            to: hhmm(rapel.claimedEnd),
            hours: rapel.claimedHours,
            suggested: formatIdr(rapel.suggestedAmount),
          })}
        </p>
        <div className="flex flex-col gap-1.5">
          <Label>{t('rapel.amount')}</Label>
          <Input type="number" min={0} step={1000} value={amount} onChange={(e) => setAmount(e.target.value)} />
        </div>
      </div>
      <DialogFooter>
        <Button variant="outline" onClick={() => onOpenChange(false)} disabled={submitting}>
          {tCommon('cancel')}
        </Button>
        <Button onClick={() => onConfirm(Number(amount))} disabled={!(Number(amount) > 0) || submitting}>
          {submitting ? tCommon('loading') : t('rapel.approveConfirm')}
        </Button>
      </DialogFooter>
    </Dialog>
  );
}
