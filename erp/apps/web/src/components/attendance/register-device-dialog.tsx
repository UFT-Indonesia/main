'use client';

import { useEffect, useState } from 'react';
import { useTranslations } from 'next-intl';
import {
  Dialog,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';

interface RegisterDeviceDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onConfirm: (deviceKey: string, name: string) => void | Promise<void>;
  submitting?: boolean;
}

const EMPTY_FORM = { deviceKey: '', name: '' };

export function RegisterDeviceDialog({
  open,
  onOpenChange,
  onConfirm,
  submitting,
}: RegisterDeviceDialogProps) {
  const t = useTranslations('attendanceDevices.register');
  const tCommon = useTranslations('common');
  const [form, setForm] = useState(EMPTY_FORM);

  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setForm(EMPTY_FORM);
  }, [open]);

  const canSubmit = !!form.deviceKey.trim() && !!form.name.trim();

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogHeader>
        <DialogTitle>{t('title')}</DialogTitle>
        <DialogDescription>{t('description')}</DialogDescription>
      </DialogHeader>

      <div className="mt-4 space-y-3">
        <div className="flex flex-col gap-1.5">
          <Label>{t('deviceKey')}</Label>
          <Input
            value={form.deviceKey}
            maxLength={100}
            onChange={(e) => setForm((s) => ({ ...s, deviceKey: e.target.value }))}
            placeholder={t('deviceKeyPlaceholder')}
          />
          <p className="text-xs text-muted-foreground">{t('deviceKeyHint')}</p>
        </div>

        <div className="flex flex-col gap-1.5">
          <Label>{t('name')}</Label>
          <Input
            value={form.name}
            maxLength={200}
            onChange={(e) => setForm((s) => ({ ...s, name: e.target.value }))}
            placeholder={t('namePlaceholder')}
          />
        </div>
      </div>

      <DialogFooter>
        <Button variant="outline" onClick={() => onOpenChange(false)} disabled={submitting}>
          {tCommon('cancel')}
        </Button>
        <Button
          onClick={() => onConfirm(form.deviceKey.trim(), form.name.trim())}
          disabled={submitting || !canSubmit}
        >
          {submitting ? tCommon('loading') : t('confirm')}
        </Button>
      </DialogFooter>
    </Dialog>
  );
}
