'use client';

import { use, useState } from 'react';
import { useRouter } from 'next/navigation';
import Link from 'next/link';
import type { Route } from 'next';
import { useTranslations } from 'next-intl';
import { ArrowLeft, Trash2 } from 'lucide-react';
import { AppShell } from '@/components/layout/app-shell';
import { Button, buttonVariants } from '@/components/ui/button';
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import { Badge } from '@/components/ui/badge';
import { EmployeeForm, type EmployeeFormValues } from '@/components/employees/employee-form';
import { DeleteEmployeeDialog } from '@/components/employees/delete-employee-dialog';
import {
  useDeleteEmployee,
  useEmployee,
  useUpdateEmployee,
} from '@/hooks/use-employees';
import { useToast } from '@/hooks/use-toast';
import { extractApiError } from '@/lib/api/client';
import { cn } from '@/lib/utils';

interface PageProps {
  params: Promise<{ id: string }>;
}

export default function EmployeeDetailPage({ params }: PageProps) {
  const { id } = use(params);
  const router = useRouter();
  const t = useTranslations('employees');
  const tForm = useTranslations('employees.form');
  const tDetail = useTranslations('employees.detail');
  const tCommon = useTranslations('common');
  const toast = useToast();
  const [deleteOpen, setDeleteOpen] = useState(false);

  const { data, isLoading, error } = useEmployee(id);
  const updateMutation = useUpdateEmployee(id);
  const deleteMutation = useDeleteEmployee();

  const onSubmit = async (values: EmployeeFormValues) => {
    try {
      await updateMutation.mutateAsync({
        fullName: values.fullName,
        nik: values.nik,
        npwp: values.npwp ? values.npwp : null,
        monthlyWageAmount: values.monthlyWageAmount,
        effectiveSalaryFrom: values.effectiveSalaryFrom,
        role: values.role,
        parentId: values.parentId ? values.parentId : null,
      });
      toast.success(tDetail('updateSuccessTitle'), tDetail('updateSuccessDescription'));
    } catch (err) {
      const apiErr = extractApiError(err);
      toast.error(tDetail('updateErrorTitle'), apiErr.message);
    }
  };

  const onConfirmDelete = async (terminationDate: string | null) => {
    try {
      await deleteMutation.mutateAsync({ id, body: { terminationDate } });
      toast.success(t('delete.successTitle'), t('delete.successDescription'));
      setDeleteOpen(false);
      router.replace('/employees');
    } catch (err) {
      const apiErr = extractApiError(err);
      toast.error(t('delete.errorTitle'), apiErr.message);
    }
  };

  return (
    <AppShell>
      <div className="mx-auto max-w-3xl space-y-4">
        <header className="flex items-start justify-between gap-3">
          <div className="flex items-start gap-3">
            <Link
              href={'/employees' as Route}
              className={cn(buttonVariants({ variant: 'ghost', size: 'icon' }))}
              aria-label={tCommon('back')}
            >
              <ArrowLeft className="h-4 w-4" />
            </Link>
            <div>
              <h1 className="text-2xl font-semibold tracking-tight">
                {data?.fullName ?? tDetail('title')}
              </h1>
              <div className="mt-1 flex items-center gap-2">
                {data && (
                  <>
                    <Badge variant="outline">{tForm(`roleOptions.${data.role}`)}</Badge>
                    <Badge
                      variant={
                        data.status === 'Active'
                          ? 'success'
                          : data.status === 'OnLeave'
                            ? 'warning'
                            : 'destructive'
                      }
                    >
                      {tForm(`statusOptions.${data.status}`)}
                    </Badge>
                  </>
                )}
              </div>
            </div>
          </div>
          {data && data.status !== 'Terminated' && (
            <Button variant="destructive" size="sm" onClick={() => setDeleteOpen(true)}>
              <Trash2 className="h-4 w-4" />
              {tDetail('terminate')}
            </Button>
          )}
        </header>

        {error ? (
          <Card>
            <CardContent className="p-6 text-sm text-destructive">
              {extractApiError(error).message}
            </CardContent>
          </Card>
        ) : isLoading || !data ? (
          <Card>
            <CardContent className="space-y-2 p-6">
              <Skeleton className="h-8 w-1/3" />
              <Skeleton className="h-32 w-full" />
            </CardContent>
          </Card>
        ) : (
          <Card>
            <CardHeader>
              <CardTitle>{t('details')}</CardTitle>
              <CardDescription>{tDetail('hint')}</CardDescription>
            </CardHeader>
            <CardContent>
              <EmployeeForm
                mode="edit"
                initial={data}
                onSubmit={onSubmit}
                onCancel={() => router.replace('/employees')}
                submitting={updateMutation.isPending}
              />
            </CardContent>
          </Card>
        )}
      </div>

      <DeleteEmployeeDialog
        employee={data ?? null}
        open={deleteOpen}
        onOpenChange={setDeleteOpen}
        onConfirm={onConfirmDelete}
        submitting={deleteMutation.isPending}
      />
    </AppShell>
  );
}
