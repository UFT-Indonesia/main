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
import { Input } from '@/components/ui/input';
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
  const today = new Date().toISOString().slice(0, 10);
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
        <Input
          id="terminationDate"
          type="date"
          value={date}
          onChange={(e) => setDate(e.target.value)}
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
