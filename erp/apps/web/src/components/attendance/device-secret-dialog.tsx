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

interface DeviceSecretDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  deviceName: string;
  secret: string;
}

/** Shows a freshly generated device secret exactly once, with a copy button. */
export function DeviceSecretDialog({
  open,
  onOpenChange,
  deviceName,
  secret,
}: DeviceSecretDialogProps) {
  const t = useTranslations('attendanceDevices.secret');
  const tCommon = useTranslations('common');
  const [copied, setCopied] = useState(false);

  const copy = async () => {
    await navigator.clipboard.writeText(secret);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogHeader>
        <DialogTitle>{t('title')}</DialogTitle>
        <DialogDescription>{t('description', { name: deviceName })}</DialogDescription>
      </DialogHeader>
      <div className="mt-4 flex items-center gap-2">
        <code className="flex-1 break-all rounded-md border border-border bg-muted px-3 py-2 font-mono text-sm">
          {secret}
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
