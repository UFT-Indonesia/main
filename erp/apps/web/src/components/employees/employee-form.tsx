'use client';

import { useState } from 'react';
import { useForm, Controller, useWatch } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useTranslations } from 'next-intl';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select } from '@/components/ui/select';
import { Combobox } from '@/components/ui/combobox';
import { useParentCandidates } from '@/hooks/use-employees';
import { EMPLOYEE_ROLES } from '@/lib/constants';
import type { Employee, EmployeeRole } from '@/lib/api/types';

const formSchema = z
  .object({
    fullName: z.string().min(1, 'Full name is required.').max(200),
    nik: z
      .string()
      .length(16, 'NIK must be 16 digits.')
      .regex(/^\d+$/, 'NIK must contain digits only.'),
    npwp: z.string().optional().or(z.literal('')),
    monthlyWageAmount: z.coerce.number().positive('Wage must be positive.'),
    effectiveSalaryFrom: z.string().min(1, 'Effective date is required.'),
    role: z.enum(['Owner', 'Manager', 'Staff'] as const),
    parentId: z.string().optional().or(z.literal('')),
  })
  .superRefine((value, ctx) => {
    if (value.role === 'Owner' && value.parentId) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        message: 'Owner cannot have a parent.',
        path: ['parentId'],
      });
    }
    if (value.role !== 'Owner' && !value.parentId) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        message: 'Non-owner employee must have a parent.',
        path: ['parentId'],
      });
    }
  });

export type EmployeeFormValues = z.infer<typeof formSchema>;

interface EmployeeFormProps {
  initial?: Employee;
  onSubmit: (values: EmployeeFormValues) => void | Promise<void>;
  onCancel?: () => void;
  submitting?: boolean;
  mode: 'create' | 'edit';
}

function toFormDefaults(initial?: Employee): EmployeeFormValues {
  return {
    fullName: initial?.fullName ?? '',
    nik: initial?.nik ?? '',
    npwp: initial?.npwp ?? '',
    monthlyWageAmount: initial?.monthlyWageAmount ?? 0,
    effectiveSalaryFrom: initial?.effectiveSalaryFrom ?? new Date().toISOString().slice(0, 10),
    role: (initial?.role ?? 'Staff') as EmployeeRole,
    parentId: initial?.parentId ?? '',
  };
}

export function EmployeeForm({ initial, onSubmit, onCancel, submitting, mode }: EmployeeFormProps) {
  const t = useTranslations('employees.form');
  const tCommon = useTranslations('common');
  const [parentSearch, setParentSearch] = useState('');
  const { candidates, isLoading: candidatesLoading } = useParentCandidates(parentSearch, role !== 'Owner');

  const {
    register,
    control,
    handleSubmit,
    formState: { errors },
  } = useForm<EmployeeFormValues>({
    resolver: zodResolver(formSchema),
    defaultValues: toFormDefaults(initial),
  });

  const role = useWatch({ control, name: 'role' });

  const parentOptions = candidates.map((e) => ({
    value: e.id,
    label: e.fullName,
    meta: t(`roleOptions.${e.role}`),
  }));

  return (
    <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
      <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
        <Field label={t('fullName')} error={errors.fullName?.message}>
          <Input {...register('fullName')} autoComplete="off" />
        </Field>

        <Field label={t('nik')} error={errors.nik?.message}>
          <Input {...register('nik')} disabled={mode === 'edit'} inputMode="numeric" maxLength={16} />
        </Field>

        <Field label={t('npwp')} error={errors.npwp?.message}>
          <Input {...register('npwp')} placeholder={t('npwpPlaceholder')} maxLength={16} />
        </Field>

        <Field label={t('monthlyWage')} error={errors.monthlyWageAmount?.message}>
          <Input type="number" step="1" min="0" {...register('monthlyWageAmount')} />
        </Field>

        <Field label={t('effectiveSalaryFrom')} error={errors.effectiveSalaryFrom?.message}>
          <Input type="date" {...register('effectiveSalaryFrom')} />
        </Field>

        <Field label={t('role')} error={errors.role?.message}>
          <Select {...register('role')}>
            {EMPLOYEE_ROLES.map((r) => (
              <option key={r} value={r}>
                {t(`roleOptions.${r}`)}
              </option>
            ))}
          </Select>
        </Field>

        {role !== 'Owner' && (
          <Field label={t('parent')} error={errors.parentId?.message}>
            <Controller
              name="parentId"
              control={control}
              shouldUnregister
              render={({ field }) => (
                <Combobox
                  value={field.value ?? ''}
                  onChange={field.onChange}
                  options={parentOptions}
                  placeholder={t('parentPlaceholder')}
                  searchPlaceholder={t('parentSearchPlaceholder')}
                  onSearchChange={setParentSearch}
                  loading={candidatesLoading}
                  clearable
                  error={!!errors.parentId}
                />
              )}
            />
          </Field>
        )}
      </div>

      <div className="flex justify-end gap-2">
        {onCancel && (
          <Button type="button" variant="outline" onClick={onCancel} disabled={submitting}>
            {tCommon('cancel')}
          </Button>
        )}
        <Button type="submit" disabled={submitting}>
          {submitting ? tCommon('loading') : tCommon('save')}
        </Button>
      </div>
    </form>
  );
}

function Field({
  label,
  error,
  children,
}: {
  label: string;
  error?: string;
  children: React.ReactNode;
}) {
  return (
    <div className="flex flex-col gap-1.5">
      <Label>{label}</Label>
      {children}
      {error && <p className="text-xs text-destructive">{error}</p>}
    </div>
  );
}
