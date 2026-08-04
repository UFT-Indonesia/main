'use client';

import { useTranslations } from 'next-intl';
import { KeyRound, Lock, LockOpen, Trash2 } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import type { Account } from '@/lib/api/types';

interface AccountTableProps {
  accounts: Account[];
  currentUserId: string | undefined;
  onToggleEnabled: (account: Account) => void;
  onResetPassword: (account: Account) => void;
  onDelete: (account: Account) => void;
}

export function AccountTable({
  accounts,
  currentUserId,
  onToggleEnabled,
  onResetPassword,
  onDelete,
}: AccountTableProps) {
  const t = useTranslations('accounts');
  const tRoles = useTranslations('employees.form.roleOptions');
  const tCommon = useTranslations('common');

  if (accounts.length === 0) {
    return (
      <div className="rounded-lg border border-dashed border-border p-8 text-center text-sm text-muted-foreground">
        {t('empty')}
      </div>
    );
  }

  return (
    <div className="rounded-lg border border-border bg-card">
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead>{t('table.username')}</TableHead>
            <TableHead>{t('table.fullName')}</TableHead>
            <TableHead>{t('table.role')}</TableHead>
            <TableHead>{t('table.status')}</TableHead>
            <TableHead className="text-right">{tCommon('actions')}</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {accounts.map((account) => (
            <TableRow key={account.id}>
              <TableCell className="font-medium">{account.username}</TableCell>
              <TableCell>{account.fullName}</TableCell>
              <TableCell>
                <Badge variant="outline">{tRoles(account.role)}</Badge>
              </TableCell>
              <TableCell>
                <div className="flex gap-1">
                  <Badge variant={account.isEnabled ? 'success' : 'destructive'}>
                    {account.isEnabled ? t('status.enabled') : t('status.disabled')}
                  </Badge>
                  {account.mustChangePassword && (
                    <Badge variant="warning">{t('status.mustChangePassword')}</Badge>
                  )}
                </div>
              </TableCell>
              <TableCell className="text-right">
                <div className="flex justify-end gap-1">
                  <Button
                    variant="ghost"
                    size="icon"
                    onClick={() => onResetPassword(account)}
                    aria-label={t('actions.resetPassword')}
                    title={t('actions.resetPassword')}
                  >
                    <KeyRound className="h-4 w-4" />
                  </Button>
                  <Button
                    variant="ghost"
                    size="icon"
                    onClick={() => onToggleEnabled(account)}
                    disabled={account.id === currentUserId}
                    aria-label={account.isEnabled ? t('actions.disable') : t('actions.enable')}
                    title={account.isEnabled ? t('actions.disable') : t('actions.enable')}
                  >
                    {account.isEnabled ? (
                      <Lock className="h-4 w-4" />
                    ) : (
                      <LockOpen className="h-4 w-4" />
                    )}
                  </Button>
                  <Button
                    variant="ghost"
                    size="icon"
                    onClick={() => onDelete(account)}
                    disabled={account.id === currentUserId}
                    aria-label={t('actions.delete')}
                    title={t('actions.delete')}
                  >
                    <Trash2 className="h-4 w-4" />
                  </Button>
                </div>
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </div>
  );
}
