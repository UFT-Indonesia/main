'use client';

import { useState } from 'react';
import { useTranslations } from 'next-intl';
import { Check, Copy } from 'lucide-react';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';

interface TempPasswordDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  username: string;
  tempPassword: string;
}

/** Shows a freshly generated temp password exactly once, with a copy button. */
export function TempPasswordDialog({
  open,
  onOpenChange,
  username,
  tempPassword,
}: TempPasswordDialogProps) {
  const t = useTranslations('accounts.tempPassword');
  const tCommon = useTranslations('common');
  const [copied, setCopied] = useState(false);

  const copy = async () => {
    await navigator.clipboard.writeText(tempPassword);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogHeader>
        <DialogTitle>{t('title')}</DialogTitle>
        <DialogDescription>{t('description', { username })}</DialogDescription>
      </DialogHeader>
      <div className="mt-4 flex items-center gap-2">
        <code className="flex-1 rounded-md border border-border bg-muted px-3 py-2 font-mono text-sm">
          {tempPassword}
        </code>
        <Button variant="outline" size="icon" onClick={copy} aria-label={t('copy')}>
          {copied ? <Check className="h-4 w-4" /> : <Copy className="h-4 w-4" />}
        </Button>
      </div>
      <p className="mt-2 text-xs text-muted-foreground">{t('shownOnce')}</p>
      <DialogFooter>
        <Button onClick={() => onOpenChange(false)}>{tCommon('close')}</Button>
      </DialogFooter>
    </Dialog>
  );
}
