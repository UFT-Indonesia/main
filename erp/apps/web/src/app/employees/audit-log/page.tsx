'use client';

import { useState } from 'react';
import { useTranslations } from 'next-intl';
import { ChevronLeft, ChevronRight, Download } from 'lucide-react';
import { AppShell } from '@/components/layout/app-shell';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { AuditLogFilters } from '@/components/employees/audit-log-filters';
import { isKnownEventType } from '@/components/employees/audit-log-event-types';
import { AuditLogSummary } from '@/components/employees/audit-log-summary';
import { useEmployeeAuditLog } from '@/hooks/use-employee-audit-log';
import { useToast } from '@/hooks/use-toast';
import { exportEmployeeAuditLog } from '@/lib/api/employee-audit-log';
import { extractApiError } from '@/lib/api/client';
import { datedFilename, downloadBlob } from '@/lib/csv';
import { useHasRole } from '@/lib/auth/store';

const PAGE_SIZE = 20;

// Pinned to Jakarta so the timestamps match the day the date filters select on the server.
const dateTimeFormatter = new Intl.DateTimeFormat('id-ID', {
  dateStyle: 'medium',
  timeStyle: 'short',
  timeZone: 'Asia/Jakarta',
});

export default function EmployeeAuditLogPage() {
  const t = useTranslations('employeeAuditLog');
  const tCommon = useTranslations('common');
  const toast = useToast();

  // A change history exposes every salary/reporting-line change ever made — Owner-only.
  const isOwner = useHasRole('Owner');

  const [employeeId, setEmployeeId] = useState('');
  const [dateFrom, setDateFrom] = useState('');
  const [dateTo, setDateTo] = useState('');
  const [eventType, setEventType] = useState('');
  const [page, setPage] = useState(1);
  const [exporting, setExporting] = useState(false);

  const params = { page, pageSize: PAGE_SIZE, employeeId, dateFrom, dateTo, eventType };
  const { data, isLoading, isFetching, error } = useEmployeeAuditLog(params, isOwner);

  const totalPages = data ? Math.max(1, Math.ceil(data.totalCount / data.pageSize)) : 1;

  const handleExport = async () => {
    setExporting(true);
    try {
      const blob = await exportEmployeeAuditLog({ employeeId, dateFrom, dateTo, eventType });
      downloadBlob(blob, datedFilename('employee-audit-log', 'csv'));
    } catch (err) {
      toast.error(t('export.errorTitle'), extractApiError(err).message);
    } finally {
      setExporting(false);
    }
  };

  if (!isOwner) {
    return (
      <AppShell>
        <div className="rounded-lg border border-dashed border-border p-8 text-center text-sm text-muted-foreground">
          {t('ownerOnly')}
        </div>
      </AppShell>
    );
  }

  return (
    <AppShell>
      <div className="space-y-4">
        <header className="flex items-start justify-between gap-3">
          <div>
            <h1 className="text-2xl font-semibold tracking-tight">{t('title')}</h1>
            <p className="text-sm text-muted-foreground">{t('subtitle')}</p>
          </div>
          <Button size="sm" onClick={handleExport} disabled={exporting}>
            <Download className="h-4 w-4" />
            {exporting ? tCommon('loading') : t('export.button')}
          </Button>
        </header>

        <AuditLogFilters
          employeeId={employeeId}
          dateFrom={dateFrom}
          dateTo={dateTo}
          eventType={eventType}
          onEmployeeIdChange={(v) => { setEmployeeId(v); setPage(1); }}
          onDateFromChange={(v) => { setDateFrom(v); setPage(1); }}
          onDateToChange={(v) => { setDateTo(v); setPage(1); }}
          onEventTypeChange={(v) => { setEventType(v); setPage(1); }}
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
        ) : (data?.items.length ?? 0) === 0 ? (
          <div className="rounded-lg border border-dashed border-border p-8 text-center text-sm text-muted-foreground">
            {t('empty')}
          </div>
        ) : (
          <div className="rounded-lg border border-border bg-card">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>{t('table.occurredAt')}</TableHead>
                  <TableHead>{t('table.employee')}</TableHead>
                  <TableHead>{t('table.eventType')}</TableHead>
                  <TableHead>{t('table.actor')}</TableHead>
                  <TableHead>{t('table.summary')}</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {data!.items.map((entry) => (
                  <TableRow key={entry.id}>
                    <TableCell className="whitespace-nowrap text-sm text-muted-foreground">
                      {dateTimeFormatter.format(new Date(entry.occurredAtUtc))}
                    </TableCell>
                    <TableCell className="font-medium">{entry.employeeFullName}</TableCell>
                    <TableCell>
                      <Badge variant="secondary">
                        {isKnownEventType(entry.eventType)
                          ? t(`eventType.${entry.eventType}`)
                          : entry.eventType}
                      </Badge>
                    </TableCell>
                    <TableCell className="text-sm text-muted-foreground">
                      {entry.actorName ?? t('table.systemActor')}
                    </TableCell>
                    <TableCell className="text-sm">
                      <AuditLogSummary entry={entry} />
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </div>
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
    </AppShell>
  );
}
