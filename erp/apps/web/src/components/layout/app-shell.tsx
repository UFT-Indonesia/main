'use client';

import { useEffect, type ReactNode } from 'react';
import { useRouter } from 'next/navigation';
import { Sidebar } from './sidebar';
import { Topbar } from './topbar';
import { useRequireAuth } from '@/lib/auth/use-auth';
import { useAuthStore } from '@/lib/auth/store';
import { Skeleton } from '@/components/ui/skeleton';

export function AppShell({ children }: { children: ReactNode }) {
  const { token, hydrated } = useRequireAuth();
  const router = useRouter();
  const mustChangePassword = useAuthStore((s) => s.user?.mustChangePassword ?? false);

  // The API rejects everything but /api/auth while the temp password is active,
  // so park the user on the change-password screen.
  useEffect(() => {
    if (hydrated && token && mustChangePassword) {
      router.replace('/change-password');
    }
  }, [hydrated, token, mustChangePassword, router]);

  if (!hydrated || !token || mustChangePassword) {
    return (
      <div className="flex min-h-screen items-center justify-center p-8">
        <Skeleton className="h-12 w-48" />
      </div>
    );
  }

  return (
    <div className="flex min-h-screen">
      <Sidebar />
      <div className="flex flex-1 flex-col">
        <Topbar />
        <main className="flex-1 bg-background p-6">{children}</main>
      </div>
    </div>
  );
}
