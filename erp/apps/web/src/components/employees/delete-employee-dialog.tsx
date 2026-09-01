'use client';

import { useState } from 'react';
import { useTranslations } from 'next-intl';
import {
  Dialog,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { DatePickerField, todayInZone } from '@/components/ui/date-picker';
import { APP_TIME_ZONE } from '@/lib/constants';
import { Label } from '@/components/ui/label';
import type { Employee } from '@/lib/api/types';

interface DeleteEmployeeDialogProps {
  employee: Employee | null;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onConfirm: (terminationDate: string | null) => void | Promise<void>;
  submitting?: boolean;
}

export function DeleteEmployeeDialog({
  employee,
  open,
  onOpenChange,
  onConfirm,
  submitting,
}: DeleteEmployeeDialogProps) {
  const t = useTranslations('employees.delete');
  const tCommon = useTranslations('common');
  // toISOString() is UTC: before 07:00 WIB it returns yesterday, which used to make the
  // default termination date a day early.
  const today = todayInZone(APP_TIME_ZONE);
  const [date, setDate] = useState<string>(today);

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogHeader>
        <DialogTitle>{t('title')}</DialogTitle>
        <DialogDescription>
          {employee ? t('description', { name: employee.fullName }) : null}
        </DialogDescription>
      </DialogHeader>

      <div className="mt-4 space-y-1.5">
        <Label htmlFor="terminationDate">{t('terminationDate')}</Label>
        <DatePickerField
          value={date}
          onChange={setDate}
          aria-label={t('terminationDate')}
        />
      </div>

      <DialogFooter>
        <Button variant="outline" onClick={() => onOpenChange(false)} disabled={submitting}>
          {tCommon('cancel')}
        </Button>
        <Button
          variant="destructive"
          onClick={() => onConfirm(date || null)}
          disabled={submitting || !employee}
        >
          {submitting ? tCommon('loading') : t('confirm')}
        </Button>
      </DialogFooter>
    </Dialog>
  );
}
