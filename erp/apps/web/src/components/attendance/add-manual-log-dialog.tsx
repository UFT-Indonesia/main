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
import { DateTimePickerField } from '@/components/ui/date-picker';
import { EmployeePicker } from '@/components/employees/employee-picker';
import { useAttendancePolicy } from '@/hooks/use-attendance-settings';
import { useBlockedLeaveDates } from '@/hooks/use-leave';
import { APP_TIME_ZONE } from '@/lib/constants';
import type { PunchType } from '@/lib/api/types';

interface ManualLogFormState {
  employeeId: string;
  /** UTC ISO instant. The picker owns the zone conversion; nothing here parses a bare string. */
  punchedAtUtc: string;
  punchType: PunchType;
  note: string;
}

function defaultState(): ManualLogFormState {
  return {
    employeeId: '',
    punchedAtUtc: new Date().toISOString(),
    punchType: 'In',
    note: '',
  };
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

  // Wall-clock time is entered in the policy's zone, which is also the zone the server buckets
  // calendar days by. Falling back to the company zone rather than the browser's is the point.
  const { data: policy } = useAttendancePolicy();
  const timeZone = policy?.timeZoneId ?? APP_TIME_ZONE;

  // Approved leave for the selected employee, greyed out in the calendar. The device still
  // records punches on those days; this only stops one being invented by hand.
  const blocked = useBlockedLeaveDates(form.employeeId || null);

  const canSubmit = !!form.employeeId && !!form.punchedAtUtc;

  function handleConfirm() {
    if (!canSubmit) return;
    onConfirm(form.employeeId, form.punchedAtUtc, form.punchType, form.note || null);
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
          <EmployeePicker
            value={form.employeeId}
            onChange={(v) => setForm((s) => ({ ...s, employeeId: v }))}
            placeholder={t('manualLog.employeePlaceholder')}
          />
        </div>

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
