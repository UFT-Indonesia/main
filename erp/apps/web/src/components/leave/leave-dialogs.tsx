'use client';

import { useState } from 'react';
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
import { Label } from '@/components/ui/label';
import { Select } from '@/components/ui/select';
import { Combobox } from '@/components/ui/combobox';
import { Badge } from '@/components/ui/badge';
import { useEmployees } from '@/hooks/use-employees';
import type { LeaveRequest, LeaveType } from '@/lib/api/types';

export const LEAVE_TYPES: LeaveType[] = ['Annual', 'Sick', 'Permission', 'Unpaid'];

export const LEAVE_STATUS_VARIANT = {
  Pending: 'warning',
  Approved: 'success',
  Denied: 'destructive',
  Cancelled: 'secondary',
} as const;

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
    reason: string | null,
  ) => void | Promise<void>;
  submitting?: boolean;
}

const EMPTY_FORM = { employeeId: '', type: 'Annual' as LeaveType, startDate: '', endDate: '', reason: '' };

export function CreateLeaveDialog({ open, onOpenChange, onConfirm, submitting }: CreateLeaveDialogProps) {
  const t = useTranslations('leave');
  const tCommon = useTranslations('common');

  const [form, setForm] = useState(EMPTY_FORM);
  const [empSearch, setEmpSearch] = useState('');

  const employeesQuery = useEmployees({ status: 'Active', search: empSearch, pageSize: 50 });
  const empOptions = (employeesQuery.data?.items ?? []).map((e) => ({
    value: e.id,
    label: e.fullName,
    meta: e.role,
  }));

  const workdays = countWorkdays(form.startDate, form.endDate);
  const canSubmit = !!form.employeeId && workdays > 0;

  function handleOpenChange(o: boolean) {
    if (!o) setForm(EMPTY_FORM);
    onOpenChange(o);
  }

  return (
    <Dialog open={open} onOpenChange={handleOpenChange}>
      <DialogHeader>
        <DialogTitle>{t('create.title')}</DialogTitle>
        <DialogDescription>{t('create.description')}</DialogDescription>
      </DialogHeader>

      <div className="mt-4 space-y-3">
        <div className="flex flex-col gap-1.5">
          <Label>{t('create.employee')}</Label>
          <Combobox
            value={form.employeeId}
            onChange={(v) => setForm((s) => ({ ...s, employeeId: v }))}
            options={empOptions}
            placeholder={t('create.employeePlaceholder')}
            searchPlaceholder={tCommon('search')}
            onSearchChange={setEmpSearch}
            loading={employeesQuery.isLoading}
            clearable
          />
        </div>

        <div className="flex flex-col gap-1.5">
          <Label>{t('columns.type')}</Label>
          <Select
            value={form.type}
            onChange={(e) => setForm((s) => ({ ...s, type: e.target.value as LeaveType }))}
          >
            {LEAVE_TYPES.map((type) => (
              <option key={type} value={type}>{t(`type.${type}`)}</option>
            ))}
          </Select>
        </div>

        <div className="flex gap-3">
          <div className="flex flex-1 flex-col gap-1.5">
            <Label>{t('create.startDate')}</Label>
            <Input
              type="date"
              value={form.startDate}
              onChange={(e) => setForm((s) => ({ ...s, startDate: e.target.value }))}
            />
          </div>
          <div className="flex flex-1 flex-col gap-1.5">
            <Label>{t('create.endDate')}</Label>
            <Input
              type="date"
              value={form.endDate}
              onChange={(e) => setForm((s) => ({ ...s, endDate: e.target.value }))}
            />
          </div>
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

        <p className="text-sm text-muted-foreground">
          {t('create.workdayPreview', { count: workdays })}
        </p>
      </div>

      <DialogFooter>
        <Button variant="outline" onClick={() => handleOpenChange(false)} disabled={submitting}>
          {tCommon('cancel')}
        </Button>
        <Button
          onClick={() => onConfirm(form.employeeId, form.type, form.startDate, form.endDate, form.reason || null)}
          disabled={submitting || !canSubmit}
        >
          {submitting ? tCommon('loading') : t('create.confirm')}
        </Button>
      </DialogFooter>
    </Dialog>
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

  function handleOpenChange(o: boolean) {
    if (!o) setNote('');
    onOpenChange(o);
  }

  if (!open) return null;

  return (
    <Dialog open={open} onOpenChange={handleOpenChange}>
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
        <Button variant="outline" onClick={() => handleOpenChange(false)} disabled={submitting}>
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

  const rows: [string, string][] = [
    [t('columns.employee'), request.employeeFullName],
    [t('columns.type'), t(`type.${request.type}`)],
    [t('columns.dates'), `${formatLeaveDate(request.startDate)} – ${formatLeaveDate(request.endDate)}`],
    [t('columns.workdays'), String(request.workdayCount)],
    [t('columns.approvedThisYear'), String(request.approvedWorkdaysThisYear)],
    [t('details.reason'), request.reason || '–'],
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
      </dl>
    </Dialog>
  );
}
