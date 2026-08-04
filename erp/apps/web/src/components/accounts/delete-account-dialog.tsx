'use client';

import { useTranslations } from 'next-intl';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';

interface DeleteAccountDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  username: string;
  onConfirm: () => void;
  isPending: boolean;
}

export function DeleteAccountDialog({
  open,
  onOpenChange,
  username,
  onConfirm,
  isPending,
}: DeleteAccountDialogProps) {
  const t = useTranslations('accounts.delete');
  const tCommon = useTranslations('common');

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogHeader>
        <DialogTitle>{t('title')}</DialogTitle>
        <DialogDescription>{t('confirm', { username })}</DialogDescription>
      </DialogHeader>
      <DialogFooter>
        <Button variant="outline" onClick={() => onOpenChange(false)} disabled={isPending}>
          {tCommon('cancel')}
        </Button>
        <Button variant="destructive" onClick={onConfirm} disabled={isPending}>
          {tCommon('confirm')}
        </Button>
      </DialogFooter>
    </Dialog>
  );
}
