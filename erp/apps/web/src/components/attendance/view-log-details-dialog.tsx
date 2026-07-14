'use client';

import { useState } from 'react';
import { useTranslations } from 'next-intl';
import { ChevronLeft, Pencil } from 'lucide-react';
import {
  Dialog,
  DialogDescription,
  DialogFooter,
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
import { useAttendanceDayLogs, useUpdateAttendanceLog } from '@/hooks/use-attendance';
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
  note: string;
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
  const [form, setForm] = useState<FormState>({ punchedAt: '', punchType: 'In', note: '' });

  const { data, isLoading, error } = useAttendanceDayLogs(
    day?.employeeId ?? '',
    day?.date ?? '',
    open,
  );
  const updateMutation = useUpdateAttendanceLog();
  const { data: policy } = useAttendancePolicy();

  function startEditing(log: AttendanceLogListItem) {
    setEditingId(log.id);
    setForm({
      punchedAt: isoToLocalInput(log.punchedAtUtc),
      punchType: log.punchType,
      note: log.note ?? '',
    });
  }

  function handleOpenChange(o: boolean) {
    if (!o) setEditingId(null);
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
          note: form.note || null,
        },
      });
      toast.success(t('edit.successTitle'), t('edit.successDescription'));
      setEditingId(null);
    } catch (err) {
      toast.error(t('edit.errorTitle'), extractApiError(err).message);
    }
  }

  const editing = editingId !== null;

  return (
    <Dialog open={open} onOpenChange={handleOpenChange} className="sm:max-w-2xl">
      <DialogHeader>
        <DialogTitle>{editing ? t('edit.title') : t('details.title')}</DialogTitle>
        <DialogDescription>
          {day ? `${day.employeeFullName} — ${day.date}` : t('details.description')}
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
        ) : editing ? (
          <div className="space-y-3">
            <Button variant="ghost" size="sm" onClick={() => setEditingId(null)}>
              <ChevronLeft className="h-4 w-4" />
              {tCommon('back')}
            </Button>
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
            <div className="flex flex-col gap-1.5">
              <Label>{t('columns.note')}</Label>
              <Input
                value={form.note}
                onChange={(e) => setForm((s) => ({ ...s, note: e.target.value }))}
                placeholder={t('manualLog.notePlaceholder')}
              />
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
                {canEdit && <TableHead className="w-10" />}
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
                  <TableCell className="text-muted-foreground">{log.note ?? '—'}</TableCell>
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

      <DialogFooter>
        <Button variant="outline" onClick={() => handleOpenChange(false)}>
          {tCommon('back')}
        </Button>
      </DialogFooter>
    </Dialog>
  );
}
