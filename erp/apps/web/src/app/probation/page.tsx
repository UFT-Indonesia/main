'use client';

import { useState } from 'react';
import { useTranslations } from 'next-intl';
import { Plus, ChevronLeft, ChevronRight, Check, X, Ban, Eye } from 'lucide-react';
import { AppShell } from '@/components/layout/app-shell';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Select } from '@/components/ui/select';
import { Skeleton } from '@/components/ui/skeleton';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { EmployeePicker } from '@/components/employees/employee-picker';
import { formatLeaveDate } from '@/components/leave/leave-dialogs';
import {
  CreateProbationExtensionDialog,
  DecideProbationDialog,
  ProbationDetailsDialog,
  PROBATION_STATUS_VARIANT,
  type ProbationDecision,
} from '@/components/probation/probation-dialogs';
import {
  useProbationExtensions,
  useCreateProbationExtension,
  useDecideProbationExtension,
} from '@/hooks/use-probation';
import { useToast } from '@/hooks/use-toast';
import { extractApiError } from '@/lib/api/client';
import { useHasRole } from '@/lib/auth/store';
import type { ProbationExtension, ProbationExtensionStatus } from '@/lib/api/types';

const PAGE_SIZE = 20;
const STATUSES: ProbationExtensionStatus[] = ['Pending', 'Approved', 'Denied', 'Cancelled'];

export default function ProbationPage() {
  const t = useTranslations('probation');
  const tCommon = useTranslations('common');
  const toast = useToast();

  // Only a manager files: an owner already holds the direct edit on the employee record.
  const canFile = useHasRole('Manager');

  // Everyone lands on what still needs a decision; the rest is history.
  const [status, setStatus] = useState<ProbationExtensionStatus | ''>('Pending');
  const [employeeId, setEmployeeId] = useState('');
  const [page, setPage] = useState(1);
  const [createOpen, setCreateOpen] = useState(false);
  const [details, setDetails] = useState<ProbationExtension | null>(null);
  const [decision, setDecision] = useState<
    { request: ProbationExtension; action: ProbationDecision } | null
  >(null);

  const { data, isLoading, isFetching, error } = useProbationExtensions({
    page,
    pageSize: PAGE_SIZE,
    status,
    employeeId: employeeId || undefined,
  });
  const createMutation = useCreateProbationExtension();
  const decideMutation = useDecideProbationExtension();

  const totalPages = data ? Math.max(1, Math.ceil(data.totalCount / data.pageSize)) : 1;

  const handleCreate = async (empId: string, proposedEndsOn: string, reason: string) => {
    try {
      await createMutation.mutateAsync({ employeeId: empId, proposedEndsOn, reason });
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
            <p className="text-sm text-muted-foreground">
              {canFile ? t('subtitleManager') : t('subtitle')}
            </p>
          </div>
          {canFile && (
            <Button onClick={() => setCreateOpen(true)}>
              <Plus className="h-4 w-4" />
              {t('create.button')}
            </Button>
          )}
        </header>

        <div className="flex flex-col gap-3 md:flex-row md:items-end">
          <div className="w-full md:w-64">
            <EmployeePicker
              value={employeeId}
              onChange={(v) => { setEmployeeId(v); setPage(1); }}
              placeholder={t('filters.allEmployees')}
            />
          </div>
          <div className="w-full md:w-40">
            <Select
              value={status}
              onChange={(e) => {
                setStatus(e.target.value as ProbationExtensionStatus | '');
                setPage(1);
              }}
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
                  <TableHead>{t('columns.currentEndsOn')}</TableHead>
                  <TableHead>{t('columns.proposedEndsOn')}</TableHead>
                  <TableHead>{t('columns.reason')}</TableHead>
                  <TableHead>{t('columns.status')}</TableHead>
                  <TableHead className="text-right">{tCommon('actions')}</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {data!.items.map((item) => (
                  <TableRow key={item.id}>
                    <TableCell className="font-medium">{item.employeeFullName}</TableCell>
                    <TableCell className="tabular-nums">{formatLeaveDate(item.currentEndsOn)}</TableCell>
                    <TableCell className="tabular-nums">{formatLeaveDate(item.proposedEndsOn)}</TableCell>
                    <TableCell className="max-w-xs truncate">{item.reason}</TableCell>
                    <TableCell>
                      <Badge variant={PROBATION_STATUS_VARIANT[item.status]}>
                        {t(`status.${item.status}`)}
                      </Badge>
                    </TableCell>
                    <TableCell className="text-right">
                      <div className="flex justify-end gap-1">
                        {/* canDecide/canCancel come from the server, the only side that knows
                            the subject's reporting line and who filed the request. */}
                        {item.canDecide && (
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
                        {item.canCancel && (
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

      <CreateProbationExtensionDialog
        open={createOpen}
        onOpenChange={setCreateOpen}
        onConfirm={handleCreate}
        submitting={createMutation.isPending}
      />

      <DecideProbationDialog
        request={decision?.request ?? null}
        action={decision?.action ?? null}
        onOpenChange={(o) => { if (!o) setDecision(null); }}
        onConfirm={handleDecide}
        submitting={decideMutation.isPending}
      />

      <ProbationDetailsDialog
        request={details}
        onOpenChange={(o) => { if (!o) setDetails(null); }}
      />
    </AppShell>
  );
}
