'use client';

import { useRouter } from 'next/navigation';
import { useTranslations } from 'next-intl';
import { AppShell } from '@/components/layout/app-shell';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { EmployeeForm, type EmployeeFormValues } from '@/components/employees/employee-form';
import { useCreateEmployee } from '@/hooks/use-employees';
import { useToast } from '@/hooks/use-toast';
import { extractApiError } from '@/lib/api/client';

export default function NewEmployeePage() {
  const router = useRouter();
  const t = useTranslations('employees');
  const tCreate = useTranslations('employees.create');
  const toast = useToast();
  const mutation = useCreateEmployee();

  const onSubmit = async (values: EmployeeFormValues) => {
    try {
      const employee = await mutation.mutateAsync({
        fullName: values.fullName,
        nik: values.nik,
        npwp: values.npwp ? values.npwp : null,
        monthlyWageAmount: values.monthlyWageAmount,
        effectiveSalaryFrom: values.effectiveSalaryFrom,
        role: values.role,
        parentId: values.parentId ? values.parentId : null,
      });
      toast.success(tCreate('successTitle'), tCreate('successDescription'));
      router.replace(`/employees/${employee.id}`);
    } catch (error) {
      const err = extractApiError(error);
      toast.error(tCreate('errorTitle'), err.message);
    }
  };

  return (
    <AppShell>
      <div className="mx-auto max-w-3xl space-y-4">
        <header>
          <h1 className="text-2xl font-semibold tracking-tight">{tCreate('title')}</h1>
          <p className="text-sm text-muted-foreground">{tCreate('subtitle')}</p>
        </header>

        <Card>
          <CardHeader>
            <CardTitle>{t('details')}</CardTitle>
            <CardDescription>{tCreate('hint')}</CardDescription>
          </CardHeader>
          <CardContent>
            <EmployeeForm
              mode="create"
              onSubmit={onSubmit}
              onCancel={() => router.replace('/employees')}
              submitting={mutation.isPending}
            />
          </CardContent>
        </Card>
      </div>
    </AppShell>
  );
}
