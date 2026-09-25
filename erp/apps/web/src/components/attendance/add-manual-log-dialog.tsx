'use client';

import { useState } from 'react';
import { useTranslations } from 'next-intl';
import { parseZonedDateTime } from '@internationalized/date';
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

/**
 * The employee and date are known when this is opened from an Absent row, so the form starts
 * on that day's shift start rather than on right-now — which would be the wrong date entirely.
 */
function prefilledState(
  prefill: ManualLogPrefill,
  timeZone: string,
  shiftStart: string,
): ManualLogFormState {
  return {
    employeeId: prefill.employeeId,
    punchedAtUtc: parseZonedDateTime(`${prefill.date}T${shiftStart}[${timeZone}]`)
      .toDate()
      .toISOString(),
    punchType: 'In',
    note: '',
  };
}

export interface ManualLogPrefill {
  employeeId: string;
  /** "YYYY-MM-DD" calendar date in the policy's zone. */
  date: string;
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
  /** Set when opened from a specific employee-date, e.g. an Absent row. */
  prefill?: ManualLogPrefill | null;
}

export function AddManualLogDialog({
  open,
  onOpenChange,
  onConfirm,
  submitting,
  prefill,
}: AddManualLogDialogProps) {
  const t = useTranslations('attendance');
  const tCommon = useTranslations('common');

  // Wall-clock time is entered in the policy's zone, which is also the zone the server buckets
  // calendar days by. Falling back to the company zone rather than the browser's is the point.
  const { data: policy } = useAttendancePolicy();
  const timeZone = policy?.timeZoneId ?? APP_TIME_ZONE;

  // Seeded once per mount. The parent bumps this dialog's key on every open, so each open
  // (blank or from an Absent row) remounts it rather than needing an effect to re-seed the form.
  const [form, setForm] = useState<ManualLogFormState>(() =>
    // The "Add punch" buttons stay disabled until the policy loads, so a prefill always has one.
    prefill && policy ? prefilledState(prefill, policy.timeZoneId, policy.shiftStart) : defaultState());

  // Approved leave for the selected employee, greyed out in the calendar. The device still
  // records punches on those days; this only stops one being invented by hand.
  const blocked = useBlockedLeaveDates(form.employeeId || null);

  const canSubmit = !!form.employeeId && !!form.punchedAtUtc;

  function handleConfirm() {
    if (!canSubmit) return;
    onConfirm(form.employeeId, form.punchedAtUtc, form.punchType, form.note || null);
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
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
            blockedDates={blocked.data?.blockedDates}
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
        <Button variant="outline" onClick={() => onOpenChange(false)} disabled={submitting}>
          {tCommon('cancel')}
        </Button>
        <Button onClick={handleConfirm} disabled={submitting || !canSubmit}>
          {submitting ? tCommon('loading') : t('manualLog.confirm')}
        </Button>
      </DialogFooter>
    </Dialog>
  );
}
