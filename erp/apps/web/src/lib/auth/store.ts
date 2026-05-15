import { create } from 'zustand';
import { persist, createJSONStorage } from 'zustand/middleware';
import type { AuthUser } from '@/lib/api/types';

interface AuthState {
  token: string | null;
  user: AuthUser | null;
  expiresAtUtc: string | null;
  hydrated: boolean;
  setSession: (token: string, user: AuthUser, expiresAtUtc: string) => void;
  setUser: (user: AuthUser) => void;
  clear: () => void;
}

export const useAuthStore = create<AuthState>()(
  persist(
    (set) => ({
      token: null,
      user: null,
      expiresAtUtc: null,
      hydrated: false,
      setSession: (token, user, expiresAtUtc) => set({ token, user, expiresAtUtc }),
      setUser: (user) => set({ user }),
      clear: () => set({ token: null, user: null, expiresAtUtc: null }),
    }),
    {
      name: 'erp-auth',
      storage: createJSONStorage(() => localStorage),
      partialize: (state) => ({
        token: state.token,
        user: state.user,
        expiresAtUtc: state.expiresAtUtc,
      }),
      onRehydrateStorage: () => (state) => {
        if (state) state.hydrated = true;
      },
    },
  ),
);

export function useAuthHydrated(): boolean {
  return useAuthStore((s) => s.hydrated);
}
