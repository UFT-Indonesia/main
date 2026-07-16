'use client';

import { useState } from 'react';
import { useTranslations } from 'next-intl';
import { Plus, ChevronLeft, ChevronRight, Check, X, Ban, Eye } from 'lucide-react';
import { AppShell } from '@/components/layout/app-shell';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Select } from '@/components/ui/select';
import { Combobox } from '@/components/ui/combobox';
import { Skeleton } from '@/components/ui/skeleton';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import {
  CreateLeaveDialog,
  DecideLeaveDialog,
  LeaveDetailsDialog,
  LEAVE_STATUS_VARIANT,
  formatLeaveDate,
  type LeaveDecision,
} from '@/components/leave/leave-dialogs';
import { useLeaveRequests, useCreateLeaveRequest, useDecideLeaveRequest } from '@/hooks/use-leave';
import { useEmployees } from '@/hooks/use-employees';
import { useToast } from '@/hooks/use-toast';
import { extractApiError } from '@/lib/api/client';
import type { LeaveRequest, LeaveRequestStatus, LeaveType } from '@/lib/api/types';

const PAGE_SIZE = 20;
const STATUSES: LeaveRequestStatus[] = ['Pending', 'Approved', 'Denied', 'Cancelled'];

export default function LeavePage() {
  const t = useTranslations('leave');
  const tCommon = useTranslations('common');
  const toast = useToast();

  const [status, setStatus] = useState<LeaveRequestStatus | ''>('Pending');
  const [employeeId, setEmployeeId] = useState('');
  const [empSearch, setEmpSearch] = useState('');
  const [page, setPage] = useState(1);
  const [createOpen, setCreateOpen] = useState(false);
  const [details, setDetails] = useState<LeaveRequest | null>(null);
  const [decision, setDecision] = useState<{ request: LeaveRequest; action: LeaveDecision } | null>(null);

  const { data, isLoading, isFetching, error } = useLeaveRequests({
    page,
    pageSize: PAGE_SIZE,
    status,
    employeeId: employeeId || undefined,
  });
  const createMutation = useCreateLeaveRequest();
  const decideMutation = useDecideLeaveRequest();

  const employeesQuery = useEmployees({ status: 'Active', search: empSearch, pageSize: 50 });
  const empOptions = (employeesQuery.data?.items ?? []).map((e) => ({
    value: e.id,
    label: e.fullName,
    meta: e.role,
  }));

  const totalPages = data ? Math.max(1, Math.ceil(data.totalCount / data.pageSize)) : 1;

  const handleCreate = async (
    empId: string,
    type: LeaveType,
    startDate: string,
    endDate: string,
    reason: string | null,
  ) => {
    try {
      await createMutation.mutateAsync({ employeeId: empId, type, startDate, endDate, reason });
      toast.success(t('create.successTitle'), t('create.successDescription'));
      setCreateOpen(false);
    } catch (err) {
      toast.error(t('create.errorTitle'), extractApiError(err).message);
    }
  };

  const handleDecide = async (note: string | null) => {
    if (!decision) return;
    try {
      await decideMutation.mutateAsync({ id: decision.request.id, action: decision.action, note });
      toast.success(t(`decide.${decision.action}.successTitle`));
      setDecision(null);
    } catch (err) {
      toast.error(t(`decide.${decision.action}.errorTitle`), extractApiError(err).message);
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
          <Button onClick={() => setCreateOpen(true)}>
            <Plus className="h-4 w-4" />
            {t('create.button')}
          </Button>
        </header>

        <div className="flex flex-col gap-3 md:flex-row md:items-end">
          <div className="w-full md:w-64">
            <Combobox
              value={employeeId}
              onChange={(v) => { setEmployeeId(v); setPage(1); }}
              options={empOptions}
              placeholder={t('filters.allEmployees')}
              searchPlaceholder={tCommon('search')}
              onSearchChange={setEmpSearch}
              loading={employeesQuery.isLoading}
              clearable
            />
          </div>
          <div className="w-full md:w-40">
            <Select
              value={status}
              onChange={(e) => { setStatus(e.target.value as LeaveRequestStatus | ''); setPage(1); }}
            >
              <option value="">{t('filters.allStatuses')}</option>
              {STATUSES.map((s) => (
                <option key={s} value={s}>{t(`status.${s}`)}</option>
              ))}
            </Select>
          </div>
        </div>

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
                  <TableHead>{t('columns.employee')}</TableHead>
                  <TableHead>{t('columns.type')}</TableHead>
                  <TableHead>{t('columns.dates')}</TableHead>
                  <TableHead className="text-right">{t('columns.workdays')}</TableHead>
                  <TableHead className="text-right">{t('columns.approvedThisYear')}</TableHead>
                  <TableHead>{t('columns.status')}</TableHead>
                  <TableHead className="text-right">{tCommon('actions')}</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {data!.items.map((item) => (
                  <TableRow key={item.id}>
                    <TableCell className="font-medium">{item.employeeFullName}</TableCell>
                    <TableCell>{t(`type.${item.type}`)}</TableCell>
                    <TableCell className="tabular-nums">
                      {formatLeaveDate(item.startDate)} – {formatLeaveDate(item.endDate)}
                    </TableCell>
                    <TableCell className="text-right tabular-nums">{item.workdayCount}</TableCell>
                    <TableCell className="text-right tabular-nums">{item.approvedWorkdaysThisYear}</TableCell>
                    <TableCell>
                      <Badge variant={LEAVE_STATUS_VARIANT[item.status]}>
                        {t(`status.${item.status}`)}
                      </Badge>
                    </TableCell>
                    <TableCell className="text-right">
                      <div className="flex justify-end gap-1">
                        {item.status === 'Pending' && (
                          <>
                            <Button
                              variant="ghost"
                              size="icon"
                              onClick={() => setDecision({ request: item, action: 'approve' })}
                              aria-label={t('decide.approve.title')}
                              title={t('decide.approve.title')}
                            >
                              <Check className="h-4 w-4 text-success" />
                            </Button>
                            <Button
                              variant="ghost"
                              size="icon"
                              onClick={() => setDecision({ request: item, action: 'deny' })}
                              aria-label={t('decide.deny.title')}
                              title={t('decide.deny.title')}
                            >
                              <X className="h-4 w-4 text-destructive" />
                            </Button>
                          </>
                        )}
                        {(item.status === 'Pending' || item.status === 'Approved') && (
                          <Button
                            variant="ghost"
                            size="icon"
                            onClick={() => setDecision({ request: item, action: 'cancel' })}
                            aria-label={t('decide.cancel.title')}
                            title={t('decide.cancel.title')}
                          >
                            <Ban className="h-4 w-4 text-muted-foreground" />
                          </Button>
                        )}
                        <Button
                          variant="ghost"
                          size="icon"
                          onClick={() => setDetails(item)}
                          aria-label={t('details.title')}
                          title={t('details.title')}
                        >
                          <Eye className="h-4 w-4" />
                        </Button>
                      </div>
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

      <CreateLeaveDialog
        open={createOpen}
        onOpenChange={setCreateOpen}
        onConfirm={handleCreate}
        submitting={createMutation.isPending}
      />

      <DecideLeaveDialog
        request={decision?.request ?? null}
        action={decision?.action ?? null}
        onOpenChange={(o) => { if (!o) setDecision(null); }}
        onConfirm={handleDecide}
        submitting={decideMutation.isPending}
      />

      <LeaveDetailsDialog
        request={details}
        onOpenChange={(o) => { if (!o) setDetails(null); }}
      />
    </AppShell>
  );
}
