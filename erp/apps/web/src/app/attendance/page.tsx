'use client';

import { useState } from 'react';
import { useTranslations } from 'next-intl';
import { Plus, ChevronLeft, ChevronRight } from 'lucide-react';
import { AppShell } from '@/components/layout/app-shell';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { AttendanceFilters } from '@/components/attendance/attendance-filters';
import { AttendanceTable } from '@/components/attendance/attendance-table';
import { AddManualLogDialog } from '@/components/attendance/add-manual-log-dialog';
import { useAttendanceLogs, useRecordManualLog } from '@/hooks/use-attendance';
import { useToast } from '@/hooks/use-toast';
import { extractApiError } from '@/lib/api/client';
import type { AttendanceSource, PunchType } from '@/lib/api/types';

const PAGE_SIZE = 20;

export default function AttendancePage() {
  const t = useTranslations('attendance');
  const tCommon = useTranslations('common');
  const toast = useToast();

  const [employeeSearch, setEmployeeSearch] = useState('');
  const [dateFrom, setDateFrom] = useState('');
  const [dateTo, setDateTo] = useState('');
  const [punchType, setPunchType] = useState<PunchType | ''>('');
  const [source, setSource] = useState<AttendanceSource | ''>('');
  const [page, setPage] = useState(1);
  const [dialogOpen, setDialogOpen] = useState(false);

  const toUtcParam = (dateStr: string, endOfDay = false) => {
    if (!dateStr) return undefined;
    const d = new Date(dateStr);
    if (endOfDay) d.setDate(d.getDate() + 1);
    return d.toISOString();
  };

  const params = {
    page,
    pageSize: PAGE_SIZE,
    employeeSearch: employeeSearch || undefined,
    dateFrom: toUtcParam(dateFrom),
    dateTo: toUtcParam(dateTo, true),
    punchType: punchType || undefined,
    source: source || undefined,
  };

  const { data, isLoading, isFetching, error } = useAttendanceLogs(params);
  const recordMutation = useRecordManualLog();

  const totalPages = data ? Math.max(1, Math.ceil(data.totalCount / data.pageSize)) : 1;

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
    } catch (err) {
      const apiErr = extractApiError(err);
      toast.error(t('manualLog.errorTitle'), apiErr.message);
    }
  };

  const resetPage = () => setPage(1);

  return (
    <AppShell>
      <div className="space-y-4">
        <header className="flex items-start justify-between gap-3">
          <div>
            <h1 className="text-2xl font-semibold tracking-tight">{t('title')}</h1>
            <p className="text-sm text-muted-foreground">{t('subtitle')}</p>
          </div>
          <Button onClick={() => setDialogOpen(true)}>
            <Plus className="h-4 w-4" />
            {t('addManual')}
          </Button>
        </header>

        <AttendanceFilters
          employeeSearch={employeeSearch}
          dateFrom={dateFrom}
          dateTo={dateTo}
          punchType={punchType}
          source={source}
          onEmployeeSearchChange={(v) => { setEmployeeSearch(v); resetPage(); }}
          onDateFromChange={(v) => { setDateFrom(v); resetPage(); }}
          onDateToChange={(v) => { setDateTo(v); resetPage(); }}
          onPunchTypeChange={(v) => { setPunchType(v); resetPage(); }}
          onSourceChange={(v) => { setSource(v); resetPage(); }}
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
          <AttendanceTable items={data?.items ?? []} />
        )}

        {data && data.totalCount > 0 && (
          <div className="flex items-center justify-between">
            <p className="text-xs text-muted-foreground">
              {t('pagination.summary', {
                from: (data.page - 1) * data.pageSize + 1,
                to: Math.min(data.page * data.pageSize, data.totalCount),
                total: data.totalCount,
              })}
            </p>
            <div className="flex items-center gap-2">
              <Button
                variant="outline"
                size="sm"
                onClick={() => setPage((p) => Math.max(1, p - 1))}
                disabled={page <= 1 || isFetching}
              >
                <ChevronLeft className="h-4 w-4" />
                {tCommon('previous')}
              </Button>
              <span className="text-xs text-muted-foreground">
                {data.page} / {totalPages}
              </span>
              <Button
                variant="outline"
                size="sm"
                onClick={() => setPage((p) => p + 1)}
                disabled={page >= totalPages || isFetching}
              >
                {tCommon('next')}
                <ChevronRight className="h-4 w-4" />
              </Button>
            </div>
          </div>
        )}
      </div>

      <AddManualLogDialog
        open={dialogOpen}
        onOpenChange={setDialogOpen}
        onConfirm={handleConfirm}
        submitting={recordMutation.isPending}
      />
    </AppShell>
  );
}
