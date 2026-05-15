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
import { useRedirectIfAuthenticated } from '@/lib/auth/use-auth';
import { login as loginApi } from '@/lib/api/auth';
import { extractApiError } from '@/lib/api/client';
import { useToast } from '@/hooks/use-toast';
import { APP_NAME } from '@/lib/constants';

const schema = z.object({
  email: z.string().email('Email tidak valid.'),
  password: z.string().min(1, 'Password wajib diisi.'),
});

type LoginValues = z.infer<typeof schema>;

export default function LoginPage() {
  useRedirectIfAuthenticated('/');
  const router = useRouter();
  const setSession = useAuthStore((s) => s.setSession);
  const toast = useToast();
  const t = useTranslations('login');
  const tCommon = useTranslations('common');
  const [submitting, setSubmitting] = useState(false);

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<LoginValues>({
    resolver: zodResolver(schema),
    defaultValues: { email: '', password: '' },
  });

  const onSubmit = async (values: LoginValues) => {
    setSubmitting(true);
    try {
      const response = await loginApi(values.email, values.password);
      setSession(response.accessToken, response.user, response.expiresAtUtc);
      toast.success(t('successTitle'), t('successDescription'));
      router.replace('/');
    } catch (error) {
      const err = extractApiError(error);
      toast.error(t('errorTitle'), err.message);
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <main className="flex min-h-screen items-center justify-center bg-muted/40 p-4">
      <Card className="w-full max-w-sm">
        <CardHeader className="text-center">
          <CardTitle>{APP_NAME}</CardTitle>
          <CardDescription>{t('subtitle')}</CardDescription>
        </CardHeader>
        <CardContent>
          <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
            <div className="space-y-1.5">
              <Label htmlFor="email">{t('email')}</Label>
              <Input
                id="email"
                type="email"
                autoComplete="email"
                {...register('email')}
                aria-invalid={!!errors.email}
              />
              {errors.email && (
                <p className="text-xs text-destructive">{errors.email.message}</p>
              )}
            </div>

            <div className="space-y-1.5">
              <Label htmlFor="password">{t('password')}</Label>
              <Input
                id="password"
                type="password"
                autoComplete="current-password"
                {...register('password')}
                aria-invalid={!!errors.password}
              />
              {errors.password && (
                <p className="text-xs text-destructive">{errors.password.message}</p>
              )}
            </div>

            <Button type="submit" className="w-full" disabled={submitting}>
              {submitting ? tCommon('loading') : t('submit')}
            </Button>
          </form>
        </CardContent>
      </Card>
    </main>
  );
}
