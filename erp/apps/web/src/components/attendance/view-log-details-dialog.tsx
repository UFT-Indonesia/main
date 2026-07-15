'use client';

import { useState } from 'react';
import { useTranslations } from 'next-intl';
import { ChevronLeft, MessageSquare, Pencil, Plus, X } from 'lucide-react';
import {
  Dialog,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Badge } from '@/components/ui/badge';
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
import { useAttendancePolicy } from '@/hooks/use-attendance-settings';
import { useToast } from '@/hooks/use-toast';
import { extractApiError } from '@/lib/api/client';
import { cn } from '@/lib/utils';
import type {
  AttendanceDayListItem,
  AttendanceLogListItem,
  AttendanceSource,
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

/** ISO UTC → value for <input type="datetime-local"> in the browser's zone. */
function isoToLocalInput(iso: string): string {
  const date = new Date(iso);
  return new Date(date.getTime() - date.getTimezoneOffset() * 60_000).toISOString().slice(0, 16);
}

interface FormState {
  punchedAt: string;
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
  const [form, setForm] = useState<FormState>({ punchedAt: '', punchType: 'In' });
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

  // Notes are re-read from the (refetched) query data each render so the
  // thread reflects adds/deletes without local copies to keep in sync.
  const notesLog: AttendanceLogListItem | null =
    (notesForId && data?.items.find((log) => log.id === notesForId)) || null;

  function startEditing(log: AttendanceLogListItem) {
    setEditingId(log.id);
    setForm({
      punchedAt: isoToLocalInput(log.punchedAtUtc),
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
    if (!editingId || !form.punchedAt) return;
    try {
      await updateMutation.mutateAsync({
        id: editingId,
        body: {
          punchedAtUtc: new Date(form.punchedAt).toISOString(),
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

  return (
    <Dialog open={open} onOpenChange={handleOpenChange} className="sm:max-w-2xl">
      <DialogHeader>
        <DialogTitle>
          {editing ? t('edit.title') : viewingNotes ? t('notes.title') : t('details.title')}
        </DialogTitle>
        <DialogDescription>
          {viewingNotes && day
            ? `${day.employeeFullName} — ${formatPunchedAt(notesLog.punchedAtUtc, policy?.timeZoneId)} (${t(`punchType.${notesLog.punchType}`)})`
            : day
              ? `${day.employeeFullName} — ${day.date}`
              : t('details.description')}
        </DialogDescription>
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
              <Input
                type="datetime-local"
                value={form.punchedAt}
                onChange={(e) => setForm((s) => ({ ...s, punchedAt: e.target.value }))}
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
              <Button onClick={handleSave} disabled={updateMutation.isPending || !form.punchedAt}>
                {updateMutation.isPending ? tCommon('loading') : tCommon('save')}
              </Button>
            </div>
          </div>
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
