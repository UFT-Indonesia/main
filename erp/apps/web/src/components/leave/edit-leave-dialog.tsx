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
import { Label } from '@/components/ui/label';
import { Select } from '@/components/ui/select';
import { Switch } from '@/components/ui/switch';
import { DateRangePickerField } from '@/components/ui/date-picker';
import {
  HOURLY_BOUNDARIES,
  countWorkdays,
  formatHour,
  formatLeaveDate,
} from '@/components/leave/leave-dialogs';
import { useAttendancePolicy } from '@/hooks/use-attendance-settings';
import { useBlockedLeaveDates } from '@/hooks/use-leave';
import type { EditLeaveRequestBody, HalfDayPeriod, LeaveRequest } from '@/lib/api/types';

interface EditLeaveDialogProps {
  /** Null closes the dialog; the request drives every default. */
  request: LeaveRequest | null;
  onOpenChange: (open: boolean) => void;
  onConfirm: (body: EditLeaveRequestBody) => void | Promise<void>;
  submitting?: boolean;
}

/**
 * Moves an existing request's dates and half-day/hourly shape. Deliberately narrower than the
 * create dialog: no employee, type, reason or attachment — changing those makes it a different
 * absence, which is what cancel-and-refile is for.
 */
export function EditLeaveDialog({
  request,
  onOpenChange,
  onConfirm,
  submitting,
}: EditLeaveDialogProps) {
  const t = useTranslations('leave');
  const tCommon = useTranslations('common');

  const [startDate, setStartDate] = useState('');
  const [endDate, setEndDate] = useState('');
  const [halfDay, setHalfDay] = useState(false);
  const [halfDayPeriod, setHalfDayPeriod] = useState<HalfDayPeriod>('Morning');
  const [hourly, setHourly] = useState(false);
  const [startHour, setStartHour] = useState<number | ''>('');
  const [endHour, setEndHour] = useState<number | ''>('');

  const policy = useAttendancePolicy();
  const maxIzinHours = policy.data?.maxIzinHours ?? Infinity;

  // Only Annual carries a half day, only Izin carries hours — same rule the server enforces.
  const canHalfDay = request?.type === 'Annual';
  const canHourly = request?.type === 'Permission';

  // Excludes this request from its own conflict check, matching the server: an approved
  // request must not be found overlapping itself while its dates are being moved.
  const blocked = useBlockedLeaveDates(request?.employeeId ?? null, {
    halfDay,
    halfDayPeriod: halfDay ? halfDayPeriod : null,
    startHour: hourly && startHour !== '' ? startHour : null,
    endHour: hourly && endHour !== '' ? endHour : null,
  });

  // Re-seed from the request every time a different one is opened.
  useEffect(() => {
    if (!request) return;
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setStartDate(request.startDate);
    setEndDate(request.endDate);
    setHalfDay(request.halfDay);
    setHalfDayPeriod(request.halfDayPeriod ?? 'Morning');
    setHourly(request.startHour !== null);
    setStartHour(request.startHour ?? '');
    setEndHour(request.endHour ?? '');
  }, [request]);

  if (!request) return null;

  const workdays = countWorkdays(startDate, endDate);
  const hourlyValid =
    !hourly
    || (startHour !== '' && endHour !== '' && startHour < endHour
      && endHour - startHour <= maxIzinHours);
  const unchanged =
    startDate === request.startDate
    && endDate === request.endDate
    && halfDay === request.halfDay
    && (!halfDay || halfDayPeriod === request.halfDayPeriod)
    && (hourly ? startHour === request.startHour && endHour === request.endHour
      : request.startHour === null);

  const canSubmit = workdays > 0 && hourlyValid && !unchanged;

  return (
    <Dialog open onOpenChange={onOpenChange}>
      <DialogHeader>
        <DialogTitle>{t('edit.title')}</DialogTitle>
        <DialogDescription>
          {t('edit.description', {
            employee: request.employeeFullName,
            from: formatLeaveDate(request.startDate),
            to: formatLeaveDate(request.endDate),
          })}
        </DialogDescription>
      </DialogHeader>

      <div className="mt-4 space-y-3">
        <div className="flex items-end gap-3">
          <div className="flex flex-1 flex-col gap-1.5">
            <Label>{t('create.dateRange')}</Label>
            <DateRangePickerField
              start={startDate}
              end={endDate}
              onChange={(s, e) => { setStartDate(s); setEndDate(e); }}
              blockedDates={blocked.data?.blockedDates}
              partialDates={blocked.data?.partialDates}
            />
          </div>

          {canHalfDay && (
            <div className="flex flex-col gap-1.5">
              <Label htmlFor="edit-half-day-toggle" className="cursor-pointer font-normal">
                {t('create.halfDayLabel')}
              </Label>
              <div className="flex h-9 items-center">
                <Switch
                  id="edit-half-day-toggle"
                  checked={halfDay}
                  onCheckedChange={setHalfDay}
                />
              </div>
            </div>
          )}

          {canHourly && (
            <div className="flex flex-col gap-1.5">
              <Label htmlFor="edit-hourly-toggle" className="cursor-pointer font-normal">
                {t('create.hourlyLabel')}
              </Label>
              <div className="flex h-9 items-center">
                <Switch
                  id="edit-hourly-toggle"
                  checked={hourly}
                  onCheckedChange={(next) => {
                    setHourly(next);
                    setStartHour('');
                    setEndHour('');
                  }}
                />
              </div>
            </div>
          )}
        </div>

        {halfDay && (
          <div className="flex gap-2">
            {(['Morning', 'Afternoon'] as const).map((period) => (
              <Button
                key={period}
                type="button"
                variant={halfDayPeriod === period ? 'default' : 'outline'}
                className="flex-1"
                onClick={() => setHalfDayPeriod(period)}
              >
                {t(`create.period${period}`)}
              </Button>
            ))}
          </div>
        )}

        {hourly && (
          <div className="grid grid-cols-2 gap-3">
            <div className="flex flex-col gap-1.5">
              <Label>{t('create.startHour')}</Label>
              <Select
                value={startHour}
                onChange={(e) => {
                  const next = Number(e.target.value);
                  setStartHour(next);
                  // Clear an end hour the new start makes illegal, rather than submit a span
                  // that was never chosen for it.
                  setEndHour((prev) =>
                    prev !== '' && prev > next && prev - next <= maxIzinHours ? prev : '');
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
                value={endHour}
                disabled={startHour === ''}
                onChange={(e) => setEndHour(Number(e.target.value))}
              >
                <option value="">-</option>
                {HOURLY_BOUNDARIES.filter((hour) =>
                  startHour !== '' && hour > startHour && hour - startHour <= maxIzinHours,
                ).map((hour) => (
                  <option key={hour} value={hour}>{formatHour(hour)}</option>
                ))}
              </Select>
            </div>
          </div>
        )}

        <p className="text-sm text-muted-foreground">
          {t('create.workdayPreview', { count: workdays })}
        </p>
      </div>

      <DialogFooter>
        <Button variant="outline" onClick={() => onOpenChange(false)} disabled={submitting}>
          {tCommon('cancel')}
        </Button>
        <Button
          onClick={() =>
            onConfirm({
              startDate,
              endDate,
              halfDay,
              halfDayPeriod: halfDay ? halfDayPeriod : null,
              startHour: hourly && startHour !== '' ? startHour : null,
              endHour: hourly && endHour !== '' ? endHour : null,
            })
          }
          disabled={submitting || !canSubmit}
        >
          {submitting ? tCommon('loading') : t('edit.confirm')}
        </Button>
      </DialogFooter>
    </Dialog>
  );
}
