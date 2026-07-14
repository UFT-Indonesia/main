'use client';

import { useState } from 'react';
import { useTranslations } from 'next-intl';
import { Plus, ChevronLeft, ChevronRight, Download } from 'lucide-react';
import { AppShell } from '@/components/layout/app-shell';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { AttendanceDayFilters } from '@/components/attendance/attendance-day-filters';
import { AttendanceDayTable } from '@/components/attendance/attendance-day-table';
import { AddManualLogDialog } from '@/components/attendance/add-manual-log-dialog';
import { ViewLogDetailsDialog } from '@/components/attendance/view-log-details-dialog';
import { useAttendanceDays, useRecordManualLog } from '@/hooks/use-attendance';
import { useToast } from '@/hooks/use-toast';
import { extractApiError } from '@/lib/api/client';
import { exportAttendanceDays } from '@/lib/api/attendance';
import { useAuthStore } from '@/lib/auth/store';
import { downloadBlob } from '@/lib/csv';
import type { AttendanceDayListItem, AttendanceDayStatus, PunchType } from '@/lib/api/types';

const PAGE_SIZE = 20;

export default function AttendancePage() {
  const t = useTranslations('attendance');
  const tCommon = useTranslations('common');
  const toast = useToast();

  const user = useAuthStore((s) => s.user);
  const canEdit = !!user?.roles.some((role) => role === 'Owner' || role === 'Manager');

  const [employeeSearch, setEmployeeSearch] = useState('');
  const [dateFrom, setDateFrom] = useState('');
  const [dateTo, setDateTo] = useState('');
  const [status, setStatus] = useState<AttendanceDayStatus | ''>('');
  const [page, setPage] = useState(1);
  const [dialogOpen, setDialogOpen] = useState(false);
  const [selected, setSelected] = useState<Set<string>>(new Set());
  const [detailsDay, setDetailsDay] = useState<AttendanceDayListItem | null>(null);
  const [exporting, setExporting] = useState(false);

  const params = {
    page,
    pageSize: PAGE_SIZE,
    employeeSearch: employeeSearch || undefined,
    dateFrom: dateFrom || undefined,
    dateTo: dateTo || undefined,
    status: status || undefined,
  };

  const { data, isLoading, isFetching, error } = useAttendanceDays(params);
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

  const toggleSelected = (key: string) => {
    setSelected((prev) => {
      const next = new Set(prev);
      if (next.has(key)) {
        next.delete(key);
      } else {
        next.add(key);
      }
      return next;
    });
  };

  const toggleAllSelected = (keys: string[], checked: boolean) => {
    setSelected((prev) => {
      const next = new Set(prev);
      keys.forEach((key) => {
        if (checked) {
          next.add(key);
        } else {
          next.delete(key);
        }
      });
      return next;
    });
  };

  const handleExport = async () => {
    if (selected.size === 0) return;
    setExporting(true);
    try {
      const dayKeys = Array.from(selected).map((key) => {
        const separatorIndex = key.indexOf('|');
        return {
          employeeId: key.slice(0, separatorIndex),
          date: key.slice(separatorIndex + 1),
        };
      });
      const blob = await exportAttendanceDays(dayKeys);
      downloadBlob(blob, 'attendance-days.csv');
      setSelected(new Set());
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
          <Button onClick={() => setDialogOpen(true)}>
            <Plus className="h-4 w-4" />
            {t('addManual')}
          </Button>
        </header>

        <AttendanceDayFilters
          employeeSearch={employeeSearch}
          dateFrom={dateFrom}
          dateTo={dateTo}
          status={status}
          onEmployeeSearchChange={(v) => { setEmployeeSearch(v); resetPage(); }}
          onDateFromChange={(v) => { setDateFrom(v); resetPage(); }}
          onDateToChange={(v) => { setDateTo(v); resetPage(); }}
          onStatusChange={(v) => { setStatus(v); resetPage(); }}
        />

        {selected.size > 0 && (
          <div className="flex items-center justify-between rounded-lg border border-border bg-card px-4 py-2">
            <p className="text-sm text-muted-foreground">
              {t('selection.nSelected', { count: selected.size })}
            </p>
            <Button size="sm" onClick={handleExport} disabled={exporting}>
              <Download className="h-4 w-4" />
              {exporting ? tCommon('loading') : t('actions.exportSelected')}
            </Button>
          </div>
        )}

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
          <AttendanceDayTable
            items={data?.items ?? []}
            selected={selected}
            onToggle={toggleSelected}
            onToggleAll={toggleAllSelected}
            onViewDetails={setDetailsDay}
          />
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

      <ViewLogDetailsDialog
        open={detailsDay !== null}
        onOpenChange={(o) => { if (!o) setDetailsDay(null); }}
        day={detailsDay}
        canEdit={canEdit}
      />
    </AppShell>
  );
}
