'use client';

import { LogOut } from 'lucide-react';
import { useRouter } from 'next/navigation';
import { useTranslations } from 'next-intl';
import { Button } from '@/components/ui/button';
import { useAuth } from '@/lib/auth/use-auth';

export function Topbar() {
  const router = useRouter();
  const { user, clear } = useAuth();
  const t = useTranslations('common');

  const handleLogout = () => {
    clear();
    router.replace('/login');
  };

  return (
    <header className="flex h-14 items-center justify-between border-b border-border bg-card px-6">
      <div className="text-sm text-muted-foreground">
        {user ? (
          <span>
            {user.fullName} <span className="text-xs">({user.email})</span>
          </span>
        ) : null}
      </div>
      <Button variant="ghost" size="sm" onClick={handleLogout}>
        <LogOut className="h-4 w-4" />
        {t('logout')}
      </Button>
    </header>
  );
}
