'use client';

import { create } from 'zustand';

export type ToastVariant = 'success' | 'error' | 'info';

export interface Toast {
  id: string;
  title: string;
  description?: string;
  variant: ToastVariant;
  duration?: number;
}

interface ToastState {
  toasts: Toast[];
  show: (toast: Omit<Toast, 'id'>) => string;
  dismiss: (id: string) => void;
  clear: () => void;
}

export const useToastStore = create<ToastState>((set) => ({
  toasts: [],
  show: (toast) => {
    const id = `${Date.now()}-${Math.random().toString(36).slice(2, 8)}`;
    set((state) => ({ toasts: [...state.toasts, { ...toast, id }] }));
    return id;
  },
  dismiss: (id) => set((state) => ({ toasts: state.toasts.filter((t) => t.id !== id) })),
  clear: () => set({ toasts: [] }),
}));

export function useToast() {
  const show = useToastStore((s) => s.show);
  return {
    toast: (toast: Omit<Toast, 'id'>) => show(toast),
    success: (title: string, description?: string) =>
      show({ title, description, variant: 'success' }),
    error: (title: string, description?: string) =>
      show({ title, description, variant: 'error' }),
    info: (title: string, description?: string) => show({ title, description, variant: 'info' }),
  };
}
