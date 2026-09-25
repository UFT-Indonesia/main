'use client';

import { useMemo, useState } from 'react';
import { useTranslations } from 'next-intl';
import { Plus, Download } from 'lucide-react';
import { AppShell } from '@/components/layout/app-shell';
import { Button } from '@/components/ui/button';
import { Checkbox } from '@/components/ui/checkbox';
import { Select } from '@/components/ui/select';
import { Label } from '@/components/ui/label';
import { Skeleton } from '@/components/ui/skeleton';
import { FilterBuilder } from '@/components/ui/filter-builder';
import { todayInZone } from '@/components/ui/date-picker';
import { useFilters } from '@/hooks/use-filters';
import { ATTENDANCE_DAY_FILTER_FIELDS } from '@/lib/filters/fields';
import { useVisibleFilterFields } from '@/lib/filters/use-visible-fields';
import {
  AttendanceCalendarTable,
  hasProblem,
} from '@/components/attendance/attendance-calendar-table';
import {
  AddManualLogDialog,
  type ManualLogPrefill,
} from '@/components/attendance/add-manual-log-dialog';
import { ViewLogDetailsDialog } from '@/components/attendance/view-log-details-dialog';
import { useAttendanceDays, useRecordManualLog } from '@/hooks/use-attendance';
import { useToast } from '@/hooks/use-toast';
import { extractApiError } from '@/lib/api/client';
import { exportAttendanceDays } from '@/lib/api/attendance';
import { useHasRole } from '@/lib/auth/store';
import { APP_TIME_ZONE } from '@/lib/constants';
import { datedFilename, downloadBlob } from '@/lib/csv';
import type { AttendanceDayListItem, PunchType } from '@/lib/api/types';

/** Last day of the month containing a "YYYY-MM-DD" date. */
function endOfMonth(ymd: string): string {
  return new Date(Date.UTC(Number(ymd.slice(0, 4)), Number(ymd.slice(5, 7)), 0)).toISOString().slice(0, 10);
}

// ponytail: fixed 24-month window, widen if older periods are needed.
const MONTHS_BACK = 24;

/** "YYYY-MM" for the current month and the MONTHS_BACK before it, newest first. */
function recentMonths(today: string): string[] {
  const year = Number(today.slice(0, 4));
  const month = Number(today.slice(5, 7)) - 1;
  return Array.from({ length: MONTHS_BACK }, (_, i) =>
    new Date(Date.UTC(year, month - i, 1)).toISOString().slice(0, 7));
}

/** "2026-09" → "September 2026". */
function formatMonth(ym: string): string {
  return new Intl.DateTimeFormat('id-ID', { month: 'long', year: 'numeric', timeZone: 'UTC' })
    .format(new Date(`${ym}-01T00:00:00Z`));
}

/** Month to date: today is the first row, and no blank future dates sit above it. */
function defaultPeriod(timeZone: string): { from: string; to: string } {
  const to = todayInZone(timeZone);
  return { from: `${to.slice(0, 7)}-01`, to };
}

export default function AttendancePage() {
  const t = useTranslations('attendance');
  const tCommon = useTranslations('common');
  const toast = useToast();

  // Coarse gate: Staff never write attendance at all. Whether a Manager may write for a
  // *particular* employee is decided per row by the server's canWrite flag.
  const canWriteSomething = useHasRole('Owner', 'Manager');

  // Staff are scoped server-side to their own record, so a per-date count would always be one.
  // The same table then shows their own day in the summary cell instead.
  const selfView = !canWriteSomething;

  const [period, setPeriod] = useState(() => defaultPeriod(APP_TIME_ZONE));
  const [problemsOnly, setProblemsOnly] = useState(false);
  const filterFields = useVisibleFilterFields(ATTENDANCE_DAY_FILTER_FIELDS);
  const filters = useFilters(filterFields);
  const [dialogOpen, setDialogOpen] = useState(false);
  // Bumped on every open so the manual log form remounts fresh — including a blank reopen,
  // which would otherwise keep whatever was typed (or submitted) last time.
  const [dialogKey, setDialogKey] = useState(0);
  const [prefill, setPrefill] = useState<ManualLogPrefill | null>(null);
  const [detailsDay, setDetailsDay] = useState<AttendanceDayListItem | null>(null);
  const [exporting, setExporting] = useState(false);

  const { data, isLoading, error } = useAttendanceDays({
    from: period.from,
    to: period.to,
    filter: filters.filter,
  });

  const recordMutation = useRecordManualLog();

  const dates = useMemo(() => {
    const all = data?.dates ?? [];
    return problemsOnly ? all.filter(hasProblem) : all;
  }, [data, problemsOnly]);

  const handleConfirm = async (
    employeeId: string,
    punchedAtUtc: string,
    pt: PunchType,
    note: string | null,
  ) => {
    try {
      await recordMutation.mutateAsync({ employeeId, punchedAtUtc, punchType: pt, note });
      toast.success(t('manualLog.successTitle'), t('manualLog.successDescription'));
      setDialogOpen(false);
      setPrefill(null);
      setDetailsDay(null);
    } catch (err) {
      const apiErr = extractApiError(err);
      toast.error(t('manualLog.errorTitle'), apiErr.message);
    }
  };

  // The Absent row hands the correction straight to the manual log form, so the list of who is
  // missing doubles as the worklist for fixing it.
  const handleAddPunch = (day: AttendanceDayListItem) => {
    // detailsDay stays set: the details dialog is only hidden while the form is open, so Cancel returns to it.
    setPrefill({ employeeId: day.employeeId, date: day.date });
    setDialogKey((k) => k + 1);
    setDialogOpen(true);
  };

  // Whole months only: a past month spans its 1st to its last day, the current month runs to today.
  const handleMonthChange = (month: string) => {
    if (!month) return;
    const today = todayInZone(APP_TIME_ZONE);
    setPeriod({
      from: `${month}-01`,
      to: month < today.slice(0, 7) ? endOfMonth(`${month}-01`) : today,
    });
  };

  const handleExport = async () => {
    setExporting(true);
    try {
      const blob = await exportAttendanceDays({
        from: period.from,
        to: period.to,
        filter: filters.filter,
        problemsOnly,
      });
      downloadBlob(blob, datedFilename('attendance-days', 'csv'));
    } catch (err) {
      toast.error(t('export.errorTitle'), extractApiError(err).message);
    } finally {
      setExporting(false);
    }
  };

  return (
    <AppShell>
      <div className="space-y-4">
        <header className="flex items-start justify-between gap-3">
          <div>
            <h1 className="text-2xl font-semibold tracking-tight">{t('title')}</h1>
            <p className="text-sm text-muted-foreground">{t('subtitle')}</p>
          </div>
          <div className="flex items-center gap-2">
            <Button variant="outline" onClick={handleExport} disabled={exporting}>
              <Download className="h-4 w-4" />
              {exporting ? tCommon('loading') : t('actions.exportPeriod')}
            </Button>
            {canWriteSomething && (
              <Button
                onClick={() => {
                  setPrefill(null);
                  setDialogKey((k) => k + 1);
                  setDialogOpen(true);
                }}
              >
                <Plus className="h-4 w-4" />
                {t('addManual')}
              </Button>
            )}
          </div>
        </header>

        <div className="flex flex-wrap items-end gap-4">
          <div className="flex flex-col gap-1.5">
            <Label>{t('period.label')}</Label>
            <Select
              value={period.from.slice(0, 7)}
              onChange={(e) => handleMonthChange(e.target.value)}
              aria-label={t('period.label')}
              className="w-48"
            >
              {recentMonths(todayInZone(APP_TIME_ZONE)).map((month) => (
                <option key={month} value={month}>
                  {formatMonth(month)}
                </option>
              ))}
            </Select>
          </div>
          <label className="flex items-center gap-2 pb-2 text-sm">
            <Checkbox
              checked={problemsOnly}
              onChange={(e) => setProblemsOnly(e.target.checked)}
            />
            {t('filters.problemsOnly')}
          </label>
        </div>

        <FilterBuilder
          fields={filterFields}
          rows={filters.rows}
          activeCount={filters.activeCount}
          onAddRow={filters.addRow}
          onRemoveRow={filters.removeRow}
          onClear={filters.clear}
          onFieldChange={filters.setField}
          onOpChange={filters.setOp}
          onValueChange={filters.setValue}
        />

        {error ? (
          <div className="rounded-lg border border-destructive/40 bg-destructive/10 p-4 text-sm text-destructive">
            {extractApiError(error).message}
          </div>
        ) : isLoading ? (
          <div className="space-y-2">
            {Array.from({ length: 5 }).map((_, i) => (
              <Skeleton key={i} className="h-12 w-full" />
            ))}
          </div>
        ) : (
          <AttendanceCalendarTable
            dates={dates}
            selfView={selfView}
            onViewDetails={setDetailsDay}
          />
        )}
      </div>

      <AddManualLogDialog
        // Remounting per open reseeds the form (blank or prefilled) without an effect.
        key={dialogKey}
        open={dialogOpen}
        onOpenChange={(o) => {
          setDialogOpen(o);
          if (!o) setPrefill(null);
        }}
        onConfirm={handleConfirm}
        submitting={recordMutation.isPending}
        prefill={prefill}
      />

      <ViewLogDetailsDialog
        open={detailsDay !== null && !dialogOpen}
        onOpenChange={(o) => { if (!o) setDetailsDay(null); }}
        day={detailsDay}
        canEdit={detailsDay?.canWrite ?? false}
        onAddPunch={handleAddPunch}
      />
    </AppShell>
  );
}
