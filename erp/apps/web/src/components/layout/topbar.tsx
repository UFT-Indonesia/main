'use client';

import { LogOut } from 'lucide-react';
import { useRouter } from 'next/navigation';
import { useTranslations } from 'next-intl';
import { Button } from '@/components/ui/button';
import { logout as logoutApi } from '@/lib/api/auth';
import { useAuth } from '@/lib/auth/use-auth';

export function Topbar() {
  const router = useRouter();
  const { user, hydrated, clear } = useAuth();
  const t = useTranslations('common');

  const handleLogout = async () => {
    // Server first, so the refresh cookie is revoked rather than left live. The local session is
    // cleared either way — a network failure must not leave someone still signed in on screen.
    try {
      await logoutApi();
    } finally {
      clear();
      router.replace('/login');
    }
  };

  return (
    <header className="flex h-14 items-center justify-between border-b border-border bg-card px-6">
      <div className="text-sm text-muted-foreground">
        {hydrated && user ? (
          <span>
            {user.fullName} <span className="text-xs">({user.email})</span>
          </span>
        ) : null}
      </div>
      <Button variant="ghost" size="sm" onClick={() => void handleLogout()}>
        <LogOut className="h-4 w-4" />
        {t('logout')}
      </Button>
    </header>
  );
}
