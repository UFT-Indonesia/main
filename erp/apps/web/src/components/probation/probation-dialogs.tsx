'use client';

import { useEffect, useState } from 'react';
import { useTranslations } from 'next-intl';
import {
  Dialog,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { DatePickerField } from '@/components/ui/date-picker';
import { Label } from '@/components/ui/label';
import { Badge } from '@/components/ui/badge';
import { EmployeePicker } from '@/components/employees/employee-picker';
import { formatLeaveDate } from '@/components/leave/leave-dialogs';
import type { ProbationExtension } from '@/lib/api/types';

export const PROBATION_STATUS_VARIANT = {
  Pending: 'warning',
  Approved: 'success',
  Denied: 'destructive',
  Cancelled: 'secondary',
} as const;

const dateTimeFormatter = new Intl.DateTimeFormat('id-ID', {
  dateStyle: 'medium',
  timeStyle: 'short',
});

interface CreateProbationExtensionDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onConfirm: (employeeId: string, proposedEndsOn: string, reason: string) => void | Promise<void>;
  submitting?: boolean;
  /** Preselected subject, for the button on an employee's own detail page. */
  fixedEmployeeId?: string;
  fixedEmployeeName?: string;
}

const EMPTY_FORM = { employeeId: '', proposedEndsOn: '', reason: '' };

export function CreateProbationExtensionDialog({
  open,
  onOpenChange,
  onConfirm,
  submitting,
  fixedEmployeeId,
  fixedEmployeeName,
}: CreateProbationExtensionDialogProps) {
  const t = useTranslations('probation');
  const tCommon = useTranslations('common');

  const [form, setForm] = useState(EMPTY_FORM);

  // A reason is required — extending someone's probation is not a decision to leave unexplained.
  const canSubmit = !!form.employeeId && !!form.proposedEndsOn && form.reason.trim().length > 0;

  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setForm({ ...EMPTY_FORM, employeeId: fixedEmployeeId ?? '' });
  }, [open, fixedEmployeeId]);

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogHeader>
        <DialogTitle>{t('create.title')}</DialogTitle>
        <DialogDescription>{t('create.description')}</DialogDescription>
      </DialogHeader>

      <div className="mt-4 space-y-3">
        <div className="flex flex-col gap-1.5">
          <Label>{t('columns.employee')}</Label>
          {fixedEmployeeId ? (
            <p className="rounded-lg border border-border bg-muted/40 px-3 py-2 text-sm">
              {fixedEmployeeName ?? '—'}
            </p>
          ) : (
            <EmployeePicker
              value={form.employeeId}
              onChange={(v) => setForm((s) => ({ ...s, employeeId: v }))}
              placeholder={t('create.employeePlaceholder')}
            />
          )}
        </div>

        <div className="flex flex-col gap-1.5">
          <Label>{t('create.proposedEndsOn')}</Label>
          <DatePickerField
            value={form.proposedEndsOn}
            onChange={(v) => setForm((s) => ({ ...s, proposedEndsOn: v }))}
            aria-label={t('create.proposedEndsOn')}
          />
          <p className="text-xs text-muted-foreground">{t('create.proposedHint')}</p>
        </div>

        <div className="flex flex-col gap-1.5">
          <Label>{t('create.reason')}</Label>
          <Input
            value={form.reason}
            maxLength={500}
            onChange={(e) => setForm((s) => ({ ...s, reason: e.target.value }))}
            placeholder={t('create.reasonPlaceholder')}
          />
        </div>
      </div>

      <DialogFooter>
        <Button variant="outline" onClick={() => onOpenChange(false)} disabled={submitting}>
          {tCommon('cancel')}
        </Button>
        <Button
          onClick={() => onConfirm(form.employeeId, form.proposedEndsOn, form.reason.trim())}
          disabled={submitting || !canSubmit}
        >
          {submitting ? tCommon('loading') : t('create.confirm')}
        </Button>
      </DialogFooter>
    </Dialog>
  );
}

export type ProbationDecision = 'approve' | 'deny' | 'cancel';

interface DecideProbationDialogProps {
  request: ProbationExtension | null;
  action: ProbationDecision | null;
  onOpenChange: (open: boolean) => void;
  onConfirm: (note: string | null) => void | Promise<void>;
  submitting?: boolean;
}

export function DecideProbationDialog({
  request,
  action,
  onOpenChange,
  onConfirm,
  submitting,
}: DecideProbationDialogProps) {
  const t = useTranslations('probation');
  const tCommon = useTranslations('common');
  const [note, setNote] = useState('');

  const open = !!request && !!action;

  // Closing does not unmount this component, so the note would otherwise leak into the next one.
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
            from: formatLeaveDate(request.currentEndsOn),
            to: formatLeaveDate(request.proposedEndsOn),
          })}
        </DialogDescription>
      </DialogHeader>

      <div className="mt-4 flex flex-col gap-1.5">
        <Label>{t('decide.note')}</Label>
        <Input
          value={note}
          maxLength={500}
          onChange={(e) => setNote(e.target.value)}
          placeholder={t('decide.notePlaceholder')}
        />
      </div>

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

interface ProbationDetailsDialogProps {
  request: ProbationExtension | null;
  onOpenChange: (open: boolean) => void;
}

export function ProbationDetailsDialog({ request, onOpenChange }: ProbationDetailsDialogProps) {
  const t = useTranslations('probation');

  if (!request) return null;

  const rows: [string, string][] = [
    [t('columns.employee'), request.employeeFullName],
    [t('columns.currentEndsOn'), formatLeaveDate(request.currentEndsOn)],
    [t('columns.proposedEndsOn'), formatLeaveDate(request.proposedEndsOn)],
    [t('details.reason'), request.reason],
    [t('details.requestedAt'), dateTimeFormatter.format(new Date(request.requestedAtUtc))],
    [t('details.decidedBy'), request.decidedByName || '–'],
    [
      t('details.decidedAt'),
      request.decidedAtUtc ? dateTimeFormatter.format(new Date(request.decidedAtUtc)) : '–',
    ],
    [t('details.decisionNote'), request.decisionNote || '–'],
  ];

  return (
    <Dialog open onOpenChange={onOpenChange}>
      <DialogHeader>
        <DialogTitle>{t('details.title')}</DialogTitle>
        <DialogDescription>
          <Badge variant={PROBATION_STATUS_VARIANT[request.status]}>
            {t(`status.${request.status}`)}
          </Badge>
        </DialogDescription>
      </DialogHeader>

      <dl className="mt-4 space-y-2 text-sm">
        {rows.map(([label, value]) => (
          <div key={label} className="flex justify-between gap-4">
            <dt className="shrink-0 text-muted-foreground">{label}</dt>
            <dd className="text-right font-medium">{value}</dd>
          </div>
        ))}
      </dl>
    </Dialog>
  );
}
