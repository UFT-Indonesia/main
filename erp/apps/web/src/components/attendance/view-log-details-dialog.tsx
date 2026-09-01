'use client';

import { useState, type ReactNode } from 'react';
import { useTranslations } from 'next-intl';
import { parseAbsolute, toCalendarDate, today } from '@internationalized/date';
import { MessageSquare, Pencil, Plus, X } from 'lucide-react';
import {
  Dialog,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Badge, type BadgeProps } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Skeleton } from '@/components/ui/skeleton';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import {
  useAddAttendanceLogNote,
  useAttendanceDayLogs,
  useDeleteAttendanceLogNote,
  useUpdateAttendanceLog,
} from '@/hooks/use-attendance';
import { DateTimePickerField } from '@/components/ui/date-picker';
import { formatLeaveDate } from '@/components/leave/leave-dialogs';
import { useAttendancePolicy } from '@/hooks/use-attendance-settings';
import { useBlockedLeaveDates } from '@/hooks/use-leave';
import { useToast } from '@/hooks/use-toast';
import { extractApiError } from '@/lib/api/client';
import { APP_TIME_ZONE } from '@/lib/constants';
import { cn } from '@/lib/utils';
import type {
  AttendanceDayListItem,
  AttendanceLogListItem,
  AttendanceSource,
  LeaveType,
  PunchType,
} from '@/lib/api/types';

const SOURCE_VARIANT: Record<AttendanceSource, 'outline' | 'secondary'> = {
  Device: 'outline',
  Manual: 'secondary',
};

function formatPunchedAt(iso: string, timeZoneId: string | undefined): string {
  return new Intl.DateTimeFormat('id-ID', {
    dateStyle: 'medium',
    timeStyle: 'short',
    timeZone: timeZoneId,
  }).format(new Date(iso));
}

interface FormState {
  /** UTC ISO instant, straight through — the picker owns the zone conversion. */
  punchedAtUtc: string;
  punchType: PunchType;
}

interface ViewLogDetailsDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  day: AttendanceDayListItem | null;
  canEdit: boolean;
}

export function ViewLogDetailsDialog({
  open,
  onOpenChange,
  day,
  canEdit,
}: ViewLogDetailsDialogProps) {
  const t = useTranslations('attendance');
  const tCommon = useTranslations('common');
  const toast = useToast();

  const [editingId, setEditingId] = useState<string | null>(null);
  const [notesForId, setNotesForId] = useState<string | null>(null);
  const [form, setForm] = useState<FormState>({ punchedAtUtc: '', punchType: 'In' });
  const [noteText, setNoteText] = useState('');

  const { data, isLoading, error } = useAttendanceDayLogs(
    day?.employeeId ?? '',
    day?.date ?? '',
    open,
  );
  const updateMutation = useUpdateAttendanceLog();
  const addNoteMutation = useAddAttendanceLogNote();
  const deleteNoteMutation = useDeleteAttendanceLogNote();
  const { data: policy } = useAttendancePolicy();
  const timeZone = policy?.timeZoneId ?? APP_TIME_ZONE;
  // Same rule as the create dialog: a punch cannot be moved onto a day the employee is on leave.
  const blocked = useBlockedLeaveDates(day?.employeeId ?? null);

  // Notes are re-read from the (refetched) query data each render so the
  // thread reflects adds/deletes without local copies to keep in sync.
  const notesLog: AttendanceLogListItem | null =
    (notesForId && data?.items.find((log) => log.id === notesForId)) || null;

  function startEditing(log: AttendanceLogListItem) {
    setEditingId(log.id);
    setForm({
      punchedAtUtc: log.punchedAtUtc,
      punchType: log.punchType,
    });
  }

  function openNotes(log: AttendanceLogListItem) {
    setNotesForId(log.id);
    setNoteText('');
  }

  function handleOpenChange(o: boolean) {
    if (!o) {
      setEditingId(null);
      setNotesForId(null);
    }
    onOpenChange(o);
  }

  async function handleSave() {
    if (!editingId || !form.punchedAtUtc) return;
    try {
      await updateMutation.mutateAsync({
        id: editingId,
        body: {
          punchedAtUtc: form.punchedAtUtc,
          punchType: form.punchType,
        },
      });
      toast.success(t('edit.successTitle'), t('edit.successDescription'));
      setEditingId(null);
    } catch (err) {
      toast.error(t('edit.errorTitle'), extractApiError(err).message);
    }
  }

  async function handleAddNote() {
    if (!notesForId || !noteText.trim()) return;
    try {
      await addNoteMutation.mutateAsync({ logId: notesForId, text: noteText.trim() });
      setNoteText('');
    } catch (err) {
      toast.error(t('notes.errorTitle'), extractApiError(err).message);
    }
  }

  async function handleDeleteNote(noteId: string) {
    if (!notesForId) return;
    try {
      await deleteNoteMutation.mutateAsync({ logId: notesForId, noteId });
    } catch (err) {
      toast.error(t('notes.errorTitle'), extractApiError(err).message);
    }
  }

  const editing = editingId !== null;
  const viewingNotes = notesLog !== null;

  // No punches means the logs table would render empty. A day only ever lacks punches because
  // leave materialized it (AttendanceDay.CreateForLeave is the sole punchless path), so show
  // what the day actually is instead of nothing.
  const isLeaveOnly = !!day && !day.tapInUtc && !!day.leaveType;

  return (
    <Dialog open={open} onOpenChange={handleOpenChange} className="sm:max-w-2xl">
      <DialogHeader>
        <DialogTitle>
          {editing
            ? t('edit.title')
            : viewingNotes
              ? t('notes.title')
              : isLeaveOnly
                ? t('leaveDetails.title')
                : t('details.title')}
        </DialogTitle>
        {/* A leave day names the employee and both dates in the panel itself — repeating them
            under the title is noise, so that branch runs without a subtitle. */}
        {!isLeaveOnly && (
          <DialogDescription>
            {viewingNotes && day
              ? `${day.employeeFullName} — ${formatPunchedAt(notesLog.punchedAtUtc, policy?.timeZoneId)} (${t(`punchType.${notesLog.punchType}`)})`
              : day
                ? `${day.employeeFullName} — ${formatLeaveDate(day.date)}`
                : t('details.description')}
          </DialogDescription>
        )}
      </DialogHeader>

      <div className="mt-4 max-h-[70vh] overflow-y-auto">
        {error ? (
          <div className="rounded-lg border border-destructive/40 bg-destructive/10 p-4 text-sm text-destructive">
            {extractApiError(error).message}
          </div>
        ) : isLoading ? (
          <div className="space-y-2">
            {Array.from({ length: 3 }).map((_, i) => (
              <Skeleton key={i} className="h-10 w-full" />
            ))}
          </div>
        ) : viewingNotes ? (
          <div className="space-y-3">
            {notesLog.notes.length === 0 ? (
              <div className="rounded-lg border border-dashed border-border p-6 text-center text-sm text-muted-foreground">
                {t('notes.empty')}
              </div>
            ) : (
              <ul className="space-y-2">
                {notesLog.notes.map((note) => (
                  <li key={note.id} className="rounded-lg border border-border bg-card p-3">
                    <div className="flex items-start justify-between gap-2">
                      <p className="text-xs text-muted-foreground">
                        <span className="font-medium text-foreground">{note.createdByName}</span>
                        {' · '}
                        {formatPunchedAt(note.createdAtUtc, policy?.timeZoneId)}
                      </p>
                      {canEdit && (
                        <Button
                          variant="ghost"
                          size="icon"
                          className="h-6 w-6 shrink-0"
                          onClick={() => handleDeleteNote(note.id)}
                          disabled={deleteNoteMutation.isPending}
                          aria-label={t('notes.deleteLabel')}
                          title={t('notes.deleteLabel')}
                        >
                          <X className="h-3.5 w-3.5" />
                        </Button>
                      )}
                    </div>
                    <p className="mt-1 whitespace-pre-wrap text-sm">{note.text}</p>
                  </li>
                ))}
              </ul>
            )}

            <div className="flex gap-2">
              {canEdit && (
                <Input
                  value={noteText}
                  onChange={(e) => setNoteText(e.target.value)}
                  onKeyDown={(e) => {
                    if (e.key === 'Enter') void handleAddNote();
                  }}
                  placeholder={t('notes.placeholder')}
                  maxLength={500}
                />
              )}
              <Button variant="outline" onClick={() => setNotesForId(null)}>
                {tCommon('back')}
              </Button>
              {canEdit && (
                <Button
                  onClick={handleAddNote}
                  disabled={addNoteMutation.isPending || !noteText.trim()}
                >
                  {addNoteMutation.isPending ? tCommon('loading') : t('notes.add')}
                </Button>
              )}
            </div>
          </div>
        ) : editing ? (
          <div className="space-y-3">
            <div className="flex flex-col gap-1.5">
              <Label>{t('manualLog.punchedAt')}</Label>
              <DateTimePickerField
                value={form.punchedAtUtc}
                onChange={(v) => setForm((s) => ({ ...s, punchedAtUtc: v }))}
                timeZone={timeZone}
                blocked={blocked.data?.ranges}
              />
            </div>
            <div className="flex flex-col gap-1.5">
              <Label>{t('columns.punchType')}</Label>
              <div className="flex gap-2">
                {(['In', 'Out'] as const).map((pt) => (
                  <Button
                    key={pt}
                    type="button"
                    variant={form.punchType === pt ? 'default' : 'outline'}
                    className={cn('flex-1', form.punchType !== pt && 'text-muted-foreground')}
                    onClick={() => setForm((s) => ({ ...s, punchType: pt }))}
                  >
                    {t(`punchType.${pt}`)}
                  </Button>
                ))}
              </div>
            </div>
            <div className="flex justify-end gap-2">
              <Button
                variant="outline"
                onClick={() => setEditingId(null)}
                disabled={updateMutation.isPending}
              >
                {tCommon('cancel')}
              </Button>
              <Button onClick={handleSave} disabled={updateMutation.isPending || !form.punchedAtUtc}>
                {updateMutation.isPending ? tCommon('loading') : tCommon('save')}
              </Button>
            </div>
          </div>
        ) : isLeaveOnly ? (
          <LeaveSummary day={day} timeZoneId={policy?.timeZoneId} />
        ) : (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>{t('columns.punchedAt')}</TableHead>
                <TableHead>{t('columns.punchType')}</TableHead>
                <TableHead>{t('columns.source')}</TableHead>
                <TableHead>{t('columns.note')}</TableHead>
                {canEdit && <TableHead className="w-10 text-right">{t('columns.action')}</TableHead>}
              </TableRow>
            </TableHeader>
            <TableBody>
              {(data?.items ?? []).map((log) => (
                <TableRow key={log.id}>
                  <TableCell className="tabular-nums">
                    {formatPunchedAt(log.punchedAtUtc, policy?.timeZoneId)}
                  </TableCell>
                  <TableCell>
                    <Badge variant={log.punchType === 'In' ? 'success' : 'destructive'}>
                      {t(`punchType.${log.punchType}`)}
                    </Badge>
                  </TableCell>
                  <TableCell>
                    <Badge variant={SOURCE_VARIANT[log.source]}>{t(`source.${log.source}`)}</Badge>
                  </TableCell>
                  <TableCell className="max-w-48">
                    {log.notes.length > 0 ? (
                      <button
                        type="button"
                        onClick={() => openNotes(log)}
                        className="flex w-full items-center gap-1.5 text-left text-sm text-muted-foreground hover:text-foreground"
                        title={t('notes.viewLabel')}
                      >
                        <MessageSquare className="h-3.5 w-3.5 shrink-0" />
                        <span className="shrink-0 tabular-nums">{log.notes.length}</span>
                        <span className="truncate">
                          · {log.notes[log.notes.length - 1]?.text}
                        </span>
                      </button>
                    ) : canEdit ? (
                      <button
                        type="button"
                        onClick={() => openNotes(log)}
                        className="flex items-center justify-center rounded-lg border border-border px-4 py-[5px] text-muted-foreground hover:bg-accent hover:text-accent-foreground"
                        title={t('notes.addLabel')}
                      >
                        <Plus className="h-4 w-4" />
                      </button>
                    ) : (
                      <span className="text-muted-foreground">—</span>
                    )}
                  </TableCell>
                  {canEdit && (
                    <TableCell className="text-right">
                      <Button
                        variant="ghost"
                        size="icon"
                        onClick={() => startEditing(log)}
                        aria-label={t('actions.edit')}
                        title={t('actions.edit')}
                      >
                        <Pencil className="h-4 w-4" />
                      </Button>
                    </TableCell>
                  )}
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}
      </div>
    </Dialog>
  );
}

// Izin/Sakit/Cuti Tahunan get the client's requested colors; Unpaid has none specified, so it
// falls back to a neutral chip rather than inventing a fourth color.
const LEAVE_TYPE_BADGE: Record<LeaveType, BadgeProps['variant']> = {
  Permission: 'warning',
  Sick: 'destructive',
  Annual: 'success',
  Unpaid: 'secondary',
};

function Field({
  label,
  value,
  className,
}: {
  label: string;
  value: ReactNode;
  className?: string;
}) {
  return (
    <div className={className}>
      <dt className="text-xs text-muted-foreground">{label}</dt>
      <dd className="mt-0.5 text-sm font-medium">{value}</dd>
    </div>
  );
}

/** Footer stat — deliberately larger than the Field pairs above it. */
function StatField({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <dt className="text-sm text-muted-foreground">{label}</dt>
      <dd className="mt-1 text-xl font-semibold">{value}</dd>
    </div>
  );
}

/** Adds whole days to a "YYYY-MM-DD" date. UTC-anchored, like every other date-only helper here. */
function addDays(ymd: string, days: number): string {
  const d = new Date(`${ymd}T00:00:00Z`);
  d.setUTCDate(d.getUTCDate() + days);
  return d.toISOString().slice(0, 10);
}

/** Whole days between two date-only values, `to − from`. */
function daysBetween(from: string, to: string): number {
  const ms = new Date(`${to}T00:00:00Z`).getTime() - new Date(`${from}T00:00:00Z`).getTime();
  return Math.round(ms / 86_400_000);
}

/**
 * What a punchless leave day has to say for itself. Everything here rides on the LeaveRequest
 * navigation the day list already includes — no extra fetch. Reason is deliberately absent:
 * it is health data for Sick leave, gated behind LeaveRules.CanReadDetails, which the
 * attendance list does not evaluate per row.
 */
function LeaveSummary({
  day,
  timeZoneId,
}: {
  day: AttendanceDayListItem;
  timeZoneId: string | undefined;
}) {
  const t = useTranslations('attendance');
  const tLeave = useTranslations('leave');
  const zone = timeZoneId ?? APP_TIME_ZONE;
  const none = '–';

  // Inclusive: 1–10 Sep is ten days off, not nine. Deliberately calendar days, not the stored
  // WorkdayCount — it has to agree with the Leave/Return dates shown directly above it.
  const lengthInDays =
    day.leaveStartDate && day.leaveEndDate
      ? daysBetween(day.leaveStartDate, day.leaveEndDate) + 1
      : null;

  // Countdown from today, not from the approval date — recomputed every time the dialog opens
  // so it counts down day by day and flips to "On Leave" once the start date arrives.
  const daysUntilStart = day.leaveStartDate
    ? daysBetween(today(zone).toString(), day.leaveStartDate)
    : null;

  return (
    <div className="space-y-4">
      <dl className="grid grid-cols-2 gap-4">
        <Field label={t('leaveDetails.employeeName')} value={day.employeeFullName} />
        <Field label={t('leaveDetails.requestedTo')} value={day.leaveDecidedByName ?? none} />
        <Field
          label={t('leaveDetails.applicationDate')}
          value={
            day.leaveRequestedAtUtc
              ? formatLeaveDate(toCalendarDate(parseAbsolute(day.leaveRequestedAtUtc, zone)).toString())
              : none
          }
        />
        <Field
          label={t('leaveDetails.type')}
          value={
            day.leaveType ? (
              <Badge variant={LEAVE_TYPE_BADGE[day.leaveType]}>
                {tLeave(`type.${day.leaveType}`)}
              </Badge>
            ) : (
              none
            )
          }
        />
        <Field
          label={t('leaveDetails.leaveDate')}
          value={day.leaveStartDate ? formatLeaveDate(day.leaveStartDate) : none}
        />
        <Field
          label={t('leaveDetails.returnDate')}
          value={day.leaveEndDate ? formatLeaveDate(addDays(day.leaveEndDate, 1)) : none}
        />
      </dl>

      <div>
        <p className="text-xs text-muted-foreground">{t('leaveDetails.reason')}</p>
        <p className="mt-1 min-h-20 whitespace-pre-wrap rounded-lg border-1 border-border-strong bg-card p-3 text-sm">
          {day.leaveReason || none}
        </p>
      </div>

      <dl className="grid grid-cols-2 gap-4">
        <StatField
          label={t('leaveDetails.lengthInDays')}
          value={lengthInDays === null ? none : t('leaveDetails.dayCount', { count: lengthInDays })}
        />
        <StatField
          label={t('leaveDetails.startsIn')}
          value={
            daysUntilStart === null
              ? none
              : daysUntilStart <= 0
                ? t('leaveDetails.alreadyStarted')
                : t('leaveDetails.dayCount', { count: daysUntilStart })
          }
        />
      </dl>
    </div>
  );
}
