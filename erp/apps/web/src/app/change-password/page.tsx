'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useTranslations } from 'next-intl';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { useAuthStore } from '@/lib/auth/store';
import { useRequireAuth } from '@/lib/auth/use-auth';
import { changePassword } from '@/lib/api/auth';
import { extractApiError } from '@/lib/api/client';
import { useToast } from '@/hooks/use-toast';
import { APP_NAME } from '@/lib/constants';

const schema = z
  .object({
    currentPassword: z.string().min(1, 'Password saat ini wajib diisi.'),
    newPassword: z.string().min(8, 'Password baru minimal 8 karakter.'),
    confirmPassword: z.string().min(1, 'Konfirmasi password wajib diisi.'),
  })
  .refine((values) => values.newPassword === values.confirmPassword, {
    path: ['confirmPassword'],
    message: 'Konfirmasi password tidak cocok.',
  });

type ChangePasswordValues = z.infer<typeof schema>;

export default function ChangePasswordPage() {
  useRequireAuth();
  const router = useRouter();
  const setSession = useAuthStore((s) => s.setSession);
  const toast = useToast();
  const t = useTranslations('changePassword');
  const tCommon = useTranslations('common');
  const [submitting, setSubmitting] = useState(false);

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<ChangePasswordValues>({
    resolver: zodResolver(schema),
    defaultValues: { currentPassword: '', newPassword: '', confirmPassword: '' },
  });

  const onSubmit = async (values: ChangePasswordValues) => {
    setSubmitting(true);
    try {
      const response = await changePassword(values.currentPassword, values.newPassword);
      setSession(response.accessToken, response.user, response.expiresAtUtc);
      toast.success(t('successTitle'), t('successDescription'));
      router.replace('/');
    } catch (error) {
      toast.error(t('errorTitle'), extractApiError(error).message);
    } finally {
      setSubmitting(false);
    }
  };

  const fields = [
    { name: 'currentPassword', label: t('currentPassword'), autoComplete: 'current-password' },
    { name: 'newPassword', label: t('newPassword'), autoComplete: 'new-password' },
    { name: 'confirmPassword', label: t('confirmPassword'), autoComplete: 'new-password' },
  ] as const;

  return (
    <main className="flex min-h-screen items-center justify-center bg-muted/40 p-4">
      <Card className="w-full max-w-sm">
        <CardHeader className="text-center">
          <CardTitle>{APP_NAME}</CardTitle>
          <CardDescription>{t('subtitle')}</CardDescription>
        </CardHeader>
        <CardContent>
          <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
            {fields.map((field) => (
              <div key={field.name} className="space-y-1.5">
                <Label htmlFor={field.name}>{field.label}</Label>
                <Input
                  id={field.name}
                  type="password"
                  autoComplete={field.autoComplete}
                  {...register(field.name)}
                  aria-invalid={!!errors[field.name]}
                />
                {errors[field.name] && (
                  <p className="text-xs text-destructive">{errors[field.name]?.message}</p>
                )}
              </div>
            ))}

            <Button type="submit" className="w-full" disabled={submitting}>
              {submitting ? tCommon('loading') : t('submit')}
            </Button>
          </form>
        </CardContent>
      </Card>
    </main>
  );
}
