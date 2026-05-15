'use client';

import { useEffect } from 'react';
import { useRouter } from 'next/navigation';
import { useAuthStore } from './store';

export function useAuth() {
  return useAuthStore((s) => ({
    token: s.token,
    user: s.user,
    isAuthenticated: !!s.token,
    hydrated: s.hydrated,
    clear: s.clear,
  }));
}

export function useRequireAuth(redirectTo: string = '/login') {
  const router = useRouter();
  const token = useAuthStore((s) => s.token);
  const hydrated = useAuthStore((s) => s.hydrated);

  useEffect(() => {
    if (hydrated && !token) {
      router.replace(redirectTo);
    }
  }, [hydrated, token, redirectTo, router]);

  return { token, hydrated };
}

export function useRedirectIfAuthenticated(redirectTo: string = '/employees') {
  const router = useRouter();
  const token = useAuthStore((s) => s.token);
  const hydrated = useAuthStore((s) => s.hydrated);

  useEffect(() => {
    if (hydrated && token) {
      router.replace(redirectTo);
    }
  }, [hydrated, token, redirectTo, router]);

  return { token, hydrated };
}
