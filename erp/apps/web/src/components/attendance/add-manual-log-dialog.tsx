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
import { useEmployees } from '@/hooks/use-employees';
import type { PunchType } from '@/lib/api/types';

interface ManualLogFormState {
  employeeId: string;
  punchedAt: string;
  punchType: PunchType;
  note: string;
}

function defaultState(): ManualLogFormState {
  const now = new Date();
  const local = new Date(now.getTime() - now.getTimezoneOffset() * 60_000)
    .toISOString()
    .slice(0, 16);
  return { employeeId: '', punchedAt: local, punchType: 'In', note: '' };
}

interface AddManualLogDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onConfirm: (
    employeeId: string,
    punchedAtUtc: string,
    punchType: PunchType,
    note: string | null,
  ) => void | Promise<void>;
  submitting?: boolean;
}

export function AddManualLogDialog({
  open,
  onOpenChange,
  onConfirm,
  submitting,
}: AddManualLogDialogProps) {
  const t = useTranslations('attendance');
  const tCommon = useTranslations('common');

  const [form, setForm] = useState<ManualLogFormState>(defaultState);
  const [empSearch, setEmpSearch] = useState('');

  const allEmployeesQuery = useEmployees({ status: 'Active', search: empSearch, pageSize: 50 });
  const empOptions = (allEmployeesQuery.data?.items ?? []).map((e) => ({
    value: e.id,
    label: e.fullName,
    meta: e.role,
  }));

  const canSubmit = !!form.employeeId && !!form.punchedAt;

  function handleConfirm() {
    if (!canSubmit) return;
    const utc = new Date(form.punchedAt).toISOString();
    onConfirm(form.employeeId, utc, form.punchType, form.note || null);
  }

  function handleOpenChange(o: boolean) {
    if (!o) setForm(defaultState());
    onOpenChange(o);
  }

  return (
    <Dialog open={open} onOpenChange={handleOpenChange}>
      <DialogHeader>
        <DialogTitle>{t('manualLog.title')}</DialogTitle>
        <DialogDescription>{t('manualLog.description')}</DialogDescription>
      </DialogHeader>

      <div className="mt-4 space-y-3">
        <div className="flex flex-col gap-1.5">
          <Label>{t('manualLog.employee')}</Label>
          <Combobox
            value={form.employeeId}
            onChange={(v) => setForm((s) => ({ ...s, employeeId: v }))}
            options={empOptions}
            placeholder={t('manualLog.employeePlaceholder')}
            searchPlaceholder={tCommon('search')}
            onSearchChange={setEmpSearch}
            loading={allEmployeesQuery.isLoading}
            clearable
          />
        </div>

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
          <Select
            value={form.punchType}
            onChange={(e) => setForm((s) => ({ ...s, punchType: e.target.value as PunchType }))}
          >
            <option value="In">{t('punchType.In')}</option>
            <option value="Out">{t('punchType.Out')}</option>
          </Select>
        </div>

        <div className="flex flex-col gap-1.5">
          <Label>{t('columns.note')}</Label>
          <Input
            value={form.note}
            onChange={(e) => setForm((s) => ({ ...s, note: e.target.value }))}
            placeholder={t('manualLog.notePlaceholder')}
          />
        </div>
      </div>

      <DialogFooter>
        <Button variant="outline" onClick={() => handleOpenChange(false)} disabled={submitting}>
          {tCommon('cancel')}
        </Button>
        <Button onClick={handleConfirm} disabled={submitting || !canSubmit}>
          {submitting ? tCommon('loading') : t('manualLog.confirm')}
        </Button>
      </DialogFooter>
    </Dialog>
  );
}
