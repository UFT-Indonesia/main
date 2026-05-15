'use client';

import { useEffect } from 'react';
import { CheckCircle2, AlertTriangle, Info, X } from 'lucide-react';
import { useToastStore, type Toast } from '@/hooks/use-toast';
import { cn } from '@/lib/utils';

const ICONS = {
  success: CheckCircle2,
  error: AlertTriangle,
  info: Info,
} as const;

const TONES = {
  success: 'border-success/40 bg-success/10 text-success-foreground',
  error: 'border-destructive/40 bg-destructive/10 text-destructive-foreground',
  info: 'border-border bg-card text-card-foreground',
} as const;

const ICON_TONES = {
  success: 'text-success',
  error: 'text-destructive',
  info: 'text-muted-foreground',
} as const;

function ToastItem({ toast }: { toast: Toast }) {
  const dismiss = useToastStore((s) => s.dismiss);
  const Icon = ICONS[toast.variant];

  useEffect(() => {
    const timer = window.setTimeout(() => dismiss(toast.id), toast.duration ?? 4000);
    return () => window.clearTimeout(timer);
  }, [toast.id, toast.duration, dismiss]);

  return (
    <div
      className={cn(
        'pointer-events-auto flex items-start gap-3 rounded-lg border p-3 shadow-lg backdrop-blur',
        TONES[toast.variant],
      )}
      role={toast.variant === 'error' ? 'alert' : 'status'}
    >
      <Icon className={cn('mt-0.5 h-4 w-4 shrink-0', ICON_TONES[toast.variant])} />
      <div className="flex-1">
        <p className="text-sm font-medium text-foreground">{toast.title}</p>
        {toast.description && (
          <p className="mt-0.5 text-xs text-muted-foreground">{toast.description}</p>
        )}
      </div>
      <button
        type="button"
        onClick={() => dismiss(toast.id)}
        className="text-muted-foreground hover:text-foreground"
        aria-label="Dismiss"
      >
        <X className="h-4 w-4" />
      </button>
    </div>
  );
}

export function Toaster() {
  const toasts = useToastStore((s) => s.toasts);
  return (
    <div className="pointer-events-none fixed right-4 top-4 z-50 flex w-80 flex-col gap-2">
      {toasts.map((t) => (
        <ToastItem key={t.id} toast={t} />
      ))}
    </div>
  );
}
