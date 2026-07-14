'use client';

import { useEffect } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useTranslations } from 'next-intl';
import { AppShell } from '@/components/layout/app-shell';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Skeleton } from '@/components/ui/skeleton';
import { useAttendancePolicy, useUpdateAttendancePolicy } from '@/hooks/use-attendance-settings';
import { useToast } from '@/hooks/use-toast';
import { extractApiError } from '@/lib/api/client';
import { useAuthStore } from '@/lib/auth/store';

const formSchema = z
  .object({
    shiftStart: z.string().regex(/^\d{2}:\d{2}$/, 'Invalid time.'),
    shiftEnd: z.string().regex(/^\d{2}:\d{2}$/, 'Invalid time.'),
    clockInGraceMinutes: z.coerce.number().int().min(0, 'Must be zero or positive.'),
    clockOutGraceMinutes: z.coerce.number().int().min(0, 'Must be zero or positive.'),
    timeZoneId: z.string().min(1, 'Time zone is required.'),
  })
  .refine((v) => v.shiftStart < v.shiftEnd, {
    message: 'Shift start must be before shift end.',
    path: ['shiftEnd'],
  });

type FormValues = z.infer<typeof formSchema>;

export default function AttendanceSettingsPage() {
  const t = useTranslations('attendanceSettings');
  const tCommon = useTranslations('common');
  const toast = useToast();

  const user = useAuthStore((s) => s.user);
  const canEdit = !!user?.roles.some((role) => role === 'Owner' || role === 'Manager');

  const { data, isLoading, error } = useAttendancePolicy();
  const updateMutation = useUpdateAttendancePolicy();

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors },
  } = useForm<FormValues>({
    resolver: zodResolver(formSchema),
    defaultValues: {
      shiftStart: '09:00',
      shiftEnd: '18:00',
      clockInGraceMinutes: 5,
      clockOutGraceMinutes: 5,
      timeZoneId: 'Asia/Jakarta',
    },
  });

  useEffect(() => {
    if (data) {
      reset({
        shiftStart: data.shiftStart,
        shiftEnd: data.shiftEnd,
        clockInGraceMinutes: data.clockInGraceMinutes,
        clockOutGraceMinutes: data.clockOutGraceMinutes,
        timeZoneId: data.timeZoneId,
      });
    }
  }, [data, reset]);

  const onSubmit = async (values: FormValues) => {
    try {
      await updateMutation.mutateAsync(values);
      toast.success(t('successTitle'), t('successDescription'));
    } catch (err) {
      toast.error(t('errorTitle'), extractApiError(err).message);
    }
  };

  if (!canEdit) {
    return (
      <AppShell>
        <div className="rounded-lg border border-border bg-card p-4 text-sm text-muted-foreground">
          {t('accessDenied')}
        </div>
      </AppShell>
    );
  }

  return (
    <AppShell>
      <div className="space-y-4">
        <header>
          <h1 className="text-2xl font-semibold tracking-tight">{t('title')}</h1>
          <p className="text-sm text-muted-foreground">{t('subtitle')}</p>
        </header>

        {error ? (
          <div className="rounded-lg border border-destructive/40 bg-destructive/10 p-4 text-sm text-destructive">
            {extractApiError(error).message}
          </div>
        ) : isLoading ? (
          <div className="space-y-2">
            {Array.from({ length: 4 }).map((_, i) => (
              <Skeleton key={i} className="h-12 w-full" />
            ))}
          </div>
        ) : (
          <form
            onSubmit={handleSubmit(onSubmit)}
            className="max-w-lg space-y-4 rounded-lg border border-border bg-card p-4"
          >
            <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
              <Field label={t('form.shiftStart')} error={errors.shiftStart?.message}>
                <Input type="time" {...register('shiftStart')} />
              </Field>

              <Field label={t('form.shiftEnd')} error={errors.shiftEnd?.message}>
                <Input type="time" {...register('shiftEnd')} />
              </Field>

              <Field label={t('form.clockInGraceMinutes')} error={errors.clockInGraceMinutes?.message}>
                <Input type="number" min="0" step="1" {...register('clockInGraceMinutes')} />
              </Field>

              <Field label={t('form.clockOutGraceMinutes')} error={errors.clockOutGraceMinutes?.message}>
                <Input type="number" min="0" step="1" {...register('clockOutGraceMinutes')} />
              </Field>

              <Field label={t('form.timeZoneId')} error={errors.timeZoneId?.message}>
                <Input placeholder={t('form.timeZoneIdPlaceholder')} {...register('timeZoneId')} />
              </Field>
            </div>

            <div className="flex justify-end">
              <Button type="submit" disabled={updateMutation.isPending}>
                {updateMutation.isPending ? tCommon('loading') : t('form.save')}
              </Button>
            </div>
          </form>
        )}
      </div>
    </AppShell>
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
