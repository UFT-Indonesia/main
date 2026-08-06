'use client';

import { useTranslations } from 'next-intl';
import { ArrowRight, Network } from 'lucide-react';
import {
  Dialog,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';

interface ConfirmParentChangeDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  employeeName: string;
  /** Null when the employee genuinely has no parent; undefined when the name could not be resolved. */
  fromLabel: string | null | undefined;
  toLabel: string | null | undefined;
  onConfirm: () => void;
  submitting?: boolean;
}

export function ConfirmParentChangeDialog({
  open,
  onOpenChange,
  employeeName,
  fromLabel,
  toLabel,
  onConfirm,
  submitting,
}: ConfirmParentChangeDialogProps) {
  const t = useTranslations('employees.parentChange');
  const tCommon = useTranslations('common');

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogHeader>
        <DialogTitle>{t('title')}</DialogTitle>
        <DialogDescription>{t('description', { name: employeeName })}</DialogDescription>
      </DialogHeader>

      <div className="mt-4 rounded-lg border border-border bg-muted/40 p-4">
        <div className="flex items-center gap-3">
          <Line label={t('from')} value={fromLabel} />
          <ArrowRight className="mt-5 h-4 w-4 shrink-0 text-muted-foreground" />
          <Line label={t('to')} value={toLabel} highlight />
        </div>
      </div>

      <DialogFooter>
        <Button variant="outline" onClick={() => onOpenChange(false)} disabled={submitting}>
          {tCommon('cancel')}
        </Button>
        <Button onClick={onConfirm} disabled={submitting}>
          {submitting ? tCommon('loading') : t('confirm')}
        </Button>
      </DialogFooter>
    </Dialog>
  );
}

function Line({
  label,
  value,
  highlight,
}: {
  label: string;
  value: string | null | undefined;
  highlight?: boolean;
}) {
  const t = useTranslations('employees.parentChange');
  const resolved = value === null ? t('none') : (value ?? t('unknown'));

  return (
    <div className="min-w-0 flex-1">
      <p className="text-xs text-muted-foreground">{label}</p>
      <p className="mt-1 flex items-center gap-1.5 truncate text-sm font-medium">
        <Network className="h-3.5 w-3.5 shrink-0 text-muted-foreground" />
        <span className={highlight ? 'truncate text-foreground' : 'truncate text-muted-foreground'}>
          {resolved}
        </span>
      </p>
    </div>
  );
}
