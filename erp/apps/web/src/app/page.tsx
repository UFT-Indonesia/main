'use client';

import { useEffect } from 'react';
import { useRouter } from 'next/navigation';
import { useTranslations } from 'next-intl';
import { useAuthStore } from '@/lib/auth/store';
import { AppShell } from '@/components/layout/app-shell';
import { Card, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';

export default function HomePage() {
  const router = useRouter();
  const hydrated = useAuthStore((s) => s.hydrated);
  const token = useAuthStore((s) => s.token);

  useEffect(() => {
    if (hydrated && !token) {
      router.replace('/login');
    }
  }, [hydrated, token, router]);

  const tHome = useTranslations('home');
  const tNav = useTranslations('nav');

  return (
    <AppShell>
      <div className="space-y-4">
        <header>
          <h1 className="text-2xl font-semibold tracking-tight">{tNav('dashboard')}</h1>
          <p className="text-sm text-muted-foreground">{tHome('subtitle')}</p>
        </header>

        <Card>
          <CardHeader>
            <CardTitle>{tHome('title')}</CardTitle>
            <CardDescription>{tHome('subtitle')}</CardDescription>
          </CardHeader>
        </Card>
      </div>
    </AppShell>
  );
}
