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
import { ConfirmParentChangeDialog } from '@/components/employees/confirm-parent-change-dialog';
import { useParentCandidates } from '@/hooks/use-employees';
import { useHasRole } from '@/lib/auth/store';
import { EMPLOYEE_ROLES } from '@/lib/constants';
import type { Employee, EmployeeRole } from '@/lib/api/types';

const baseSchema = z.object({
  fullName: z.string().min(1, 'Full name is required.').max(200),
  nik: z
    .string()
    .length(16, 'NIK must be 16 digits.')
    .regex(/^\d+$/, 'NIK must contain digits only.'),
  npwp: z.string().optional().or(z.literal('')),
  // Optional at the schema level because Managers never see these fields; required
  // back below whenever the caller can actually edit pay.
  monthlyWageAmount: z.coerce.number().positive('Wage must be positive.').optional(),
  effectiveSalaryFrom: z.string().optional(),
  role: z.enum(['Owner', 'Manager', 'Staff'] as const),
  parentId: z.string().optional().or(z.literal('')),
  // Required on create, Owner-only on edit — see the superRefine below. It anchors probation,
  // and through it the annual leave entitlement.
  hireDate: z.string().optional().or(z.literal('')),
});

function buildSchema(canEditWage: boolean, requireHireDate: boolean) {
  return baseSchema.superRefine((value, ctx) => {
    if (requireHireDate && !value.hireDate) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        message: 'Hire date is required.',
        path: ['hireDate'],
      });
    }
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
    if (!canEditWage) return;
    if (value.monthlyWageAmount === undefined) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        message: 'Wage is required.',
        path: ['monthlyWageAmount'],
      });
    }
    if (!value.effectiveSalaryFrom) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        message: 'Effective date is required.',
        path: ['effectiveSalaryFrom'],
      });
    }
  });
}

export type EmployeeFormValues = z.infer<typeof baseSchema>;

interface EmployeeFormProps {
  initial?: Employee;
  onSubmit: (values: EmployeeFormValues) => void | Promise<void>;
  onCancel?: () => void;
  submitting?: boolean;
  mode: 'create' | 'edit';
}

function toFormDefaults(
  initial: Employee | undefined,
  canEditWage: boolean,
  mode: 'create' | 'edit',
): EmployeeFormValues {
  return {
    fullName: initial?.fullName ?? '',
    nik: initial?.nik ?? '',
    npwp: initial?.npwp ?? '',
    // Left undefined for Managers so the wage is never sent back at all.
    monthlyWageAmount: canEditWage ? (initial?.monthlyWageAmount ?? undefined) : undefined,
    effectiveSalaryFrom: canEditWage
      ? (initial?.effectiveSalaryFrom ?? new Date().toISOString().slice(0, 10))
      : undefined,
    role: canEditWage ? ((initial?.role ?? 'Staff') as EmployeeRole) : 'Staff',
    parentId: initial?.parentId ?? '',
    // Only Owners may write it, so a Manager's form leaves it empty and never sends it back.
    hireDate: canEditWage
      ? (initial?.hireDate ?? (mode === 'create' ? new Date().toISOString().slice(0, 10) : ''))
      : '',
  };
}

export function EmployeeForm({ initial, onSubmit, onCancel, submitting, mode }: EmployeeFormProps) {
  const t = useTranslations('employees.form');
  const tCommon = useTranslations('common');
  const [parentSearch, setParentSearch] = useState('');
  const [pendingValues, setPendingValues] = useState<EmployeeFormValues | null>(null);
  // Owner is the only role that may read or write pay, or assign a role other than Staff.
  const isOwner = useHasRole('Owner');

  const {
    register,
    control,
    handleSubmit,
    formState: { errors },
  } = useForm<EmployeeFormValues>({
    resolver: zodResolver(buildSchema(isOwner, isOwner && mode === 'create')),
    defaultValues: toFormDefaults(initial, isOwner, mode),
  });

  const role = useWatch({ control, name: 'role' });
  const { candidates, isLoading: candidatesLoading } = useParentCandidates(
    parentSearch,
    role !== 'Owner',
  );

  const parentOptions = candidates.map((e) => ({
    value: e.id,
    label: e.fullName,
    meta: t(`roleOptions.${e.role}`),
  }));

  /** Null when there is no parent at all, undefined when the id is not in the loaded candidates. */
  const parentLabel = (id: string | null | undefined) =>
    id ? candidates.find((c) => c.id === id)?.fullName : null;

  // Reparenting restructures the org chart, so hold the submit until it is confirmed.
  const confirmThenSubmit = (values: EmployeeFormValues) => {
    const parentChanged = mode === 'edit' && (values.parentId ?? '') !== (initial?.parentId ?? '');
    if (parentChanged) {
      setPendingValues(values);
      return;
    }
    return onSubmit(values);
  };

  return (
    <>
      <form onSubmit={handleSubmit(confirmThenSubmit)} className="space-y-4">
        <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
          <Field label={t('fullName')} error={errors.fullName?.message}>
            <Input {...register('fullName')} autoComplete="off" />
          </Field>

          <Field label={t('nik')} error={errors.nik?.message}>
            <Input
              {...register('nik')}
              disabled={mode === 'edit'}
              inputMode="numeric"
              maxLength={16}
            />
          </Field>

          <Field label={t('npwp')} error={errors.npwp?.message}>
            <Input {...register('npwp')} placeholder={t('npwpPlaceholder')} maxLength={16} />
          </Field>

          {isOwner && (
            <>
              <Field label={t('monthlyWage')} error={errors.monthlyWageAmount?.message}>
                <Input type="number" step="1" min="0" {...register('monthlyWageAmount')} />
              </Field>

              <Field label={t('effectiveSalaryFrom')} error={errors.effectiveSalaryFrom?.message}>
                <Input type="date" {...register('effectiveSalaryFrom')} />
              </Field>

              <Field
                label={t('hireDate')}
                error={errors.hireDate?.message}
                hint={initial && !initial.hireDate ? t('hireDateLegacyHint') : t('hireDateHint')}
              >
                <Input type="date" {...register('hireDate')} />
              </Field>
            </>
          )}

          <Field label={t('role')} error={errors.role?.message}>
            <Select {...register('role')} disabled={!isOwner}>
              {(isOwner ? EMPLOYEE_ROLES : (['Staff'] as const)).map((r) => (
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

      <ConfirmParentChangeDialog
        open={pendingValues !== null}
        onOpenChange={(open) => !open && setPendingValues(null)}
        employeeName={initial?.fullName ?? ''}
        fromLabel={parentLabel(initial?.parentId)}
        toLabel={parentLabel(pendingValues?.parentId)}
        submitting={submitting}
        onConfirm={() => {
          const values = pendingValues;
          setPendingValues(null);
          if (values) void onSubmit(values);
        }}
      />
    </>
  );
}

function Field({
  label,
  error,
  hint,
  children,
}: {
  label: string;
  error?: string;
  hint?: string;
  children: React.ReactNode;
}) {
  return (
    <div className="flex flex-col gap-1.5">
      <Label>{label}</Label>
      {children}
      {error ? (
        <p className="text-xs text-destructive">{error}</p>
      ) : hint ? (
        <p className="text-xs text-muted-foreground">{hint}</p>
      ) : null}
    </div>
  );
}
