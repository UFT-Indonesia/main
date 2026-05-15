'use client';

import { type ReactNode } from 'react';
import { Sidebar } from './sidebar';
import { Topbar } from './topbar';
import { useRequireAuth } from '@/lib/auth/use-auth';
import { Skeleton } from '@/components/ui/skeleton';

export function AppShell({ children }: { children: ReactNode }) {
  const { token, hydrated } = useRequireAuth();

  if (!hydrated || !token) {
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
