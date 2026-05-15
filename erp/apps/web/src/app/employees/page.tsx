'use client';

import { useState } from 'react';
import Link from 'next/link';
import type { Route } from 'next';
import { useTranslations } from 'next-intl';
import { Plus, ChevronLeft, ChevronRight } from 'lucide-react';
import { AppShell } from '@/components/layout/app-shell';
import { Button, buttonVariants } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { EmployeeTable } from '@/components/employees/employee-table';
import { EmployeeFilters } from '@/components/employees/employee-filters';
import { DeleteEmployeeDialog } from '@/components/employees/delete-employee-dialog';
import { useDeleteEmployee, useEmployees } from '@/hooks/use-employees';
import { extractApiError } from '@/lib/api/client';
import { useToast } from '@/hooks/use-toast';
import { cn } from '@/lib/utils';
import type { Employee, EmployeeRole, EmployeeStatus } from '@/lib/api/types';

const PAGE_SIZE = 20;

export default function EmployeesPage() {
  const t = useTranslations('employees');
  const tCommon = useTranslations('common');
  const toast = useToast();

  const [search, setSearch] = useState('');
  const [role, setRole] = useState<EmployeeRole | ''>('');
  const [status, setStatus] = useState<EmployeeStatus | ''>('');
  const [page, setPage] = useState(1);
  const [target, setTarget] = useState<Employee | null>(null);

  const params = { page, pageSize: PAGE_SIZE, search, role, status };
  const { data, isLoading, isFetching, error } = useEmployees(params);
  const deleteMutation = useDeleteEmployee();

  const totalPages = data ? Math.max(1, Math.ceil(data.totalCount / data.pageSize)) : 1;

  const handleConfirmDelete = async (terminationDate: string | null) => {
    if (!target) return;
    try {
      await deleteMutation.mutateAsync({
        id: target.id,
        body: { terminationDate },
      });
      toast.success(t('delete.successTitle'), t('delete.successDescription'));
      setTarget(null);
    } catch (err) {
      const apiErr = extractApiError(err);
      toast.error(t('delete.errorTitle'), apiErr.message);
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
          <Link
            href={'/employees/new' as Route}
            className={cn(buttonVariants({ variant: 'default', size: 'default' }))}
          >
            <Plus className="h-4 w-4" />
            {t('addNew')}
          </Link>
        </header>

        <EmployeeFilters
          search={search}
          role={role}
          status={status}
          onSearchChange={(value) => {
            setSearch(value);
            setPage(1);
          }}
          onRoleChange={(value) => {
            setRole(value);
            setPage(1);
          }}
          onStatusChange={(value) => {
            setStatus(value);
            setPage(1);
          }}
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
          <EmployeeTable employees={data?.items ?? []} onDelete={(e) => setTarget(e)} />
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

      <DeleteEmployeeDialog
        employee={target}
        open={!!target}
        onOpenChange={(open) => !open && setTarget(null)}
        onConfirm={handleConfirmDelete}
        submitting={deleteMutation.isPending}
      />
    </AppShell>
  );
}
