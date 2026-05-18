'use client';

import { useEffect } from 'react';
import { useRouter } from 'next/navigation';
import { useAuthStore } from './store';

export function useAuth() {
  const token = useAuthStore((s) => s.token);
  const user = useAuthStore((s) => s.user);
  const hydrated = useAuthStore((s) => s.hydrated);
  const clear = useAuthStore((s) => s.clear);

  return {
    token,
    user,
    isAuthenticated: !!token,
    hydrated,
    clear,
  };
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
