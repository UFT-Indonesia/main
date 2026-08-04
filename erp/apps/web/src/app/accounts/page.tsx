'use client';

import { Suspense, useState } from 'react';
import { useSearchParams } from 'next/navigation';
import { useTranslations } from 'next-intl';
import { Plus } from 'lucide-react';
import { AppShell } from '@/components/layout/app-shell';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { AccountTable } from '@/components/accounts/account-table';
import { CreateAccountDialog } from '@/components/accounts/create-account-dialog';
import { TempPasswordDialog } from '@/components/accounts/temp-password-dialog';
import { useAccounts, useResetAccountPassword, useSetAccountEnabled } from '@/hooks/use-accounts';
import { useAuthStore } from '@/lib/auth/store';
import { extractApiError } from '@/lib/api/client';
import { useToast } from '@/hooks/use-toast';
import type { Account } from '@/lib/api/types';

function AccountsPageContent() {
  const t = useTranslations('accounts');
  const toast = useToast();
  const searchParams = useSearchParams();
  const currentUser = useAuthStore((s) => s.user);

  const presetEmployeeId = searchParams.get('employeeId') ?? undefined;
  const [createOpen, setCreateOpen] = useState(!!presetEmployeeId);
  const [tempPassword, setTempPassword] = useState<{ username: string; password: string } | null>(
    null,
  );

  const { data, isLoading, error } = useAccounts();
  const setEnabledMutation = useSetAccountEnabled();
  const resetMutation = useResetAccountPassword();

  const handleToggleEnabled = async (account: Account) => {
    try {
      await setEnabledMutation.mutateAsync({ id: account.id, enabled: !account.isEnabled });
      toast.success(
        account.isEnabled ? t('toggle.disabledTitle') : t('toggle.enabledTitle'),
        account.username,
      );
    } catch (err) {
      toast.error(t('toggle.errorTitle'), extractApiError(err).message);
    }
  };

  const handleResetPassword = async (account: Account) => {
    try {
      const response = await resetMutation.mutateAsync(account.id);
      setTempPassword({ username: account.username, password: response.tempPassword });
    } catch (err) {
      toast.error(t('reset.errorTitle'), extractApiError(err).message);
    }
  };

  return (
    <div className="space-y-4">
      <header className="flex items-start justify-between gap-3">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">{t('title')}</h1>
          <p className="text-sm text-muted-foreground">{t('subtitle')}</p>
        </div>
        <Button onClick={() => setCreateOpen(true)}>
          <Plus className="h-4 w-4" />
          {t('addNew')}
        </Button>
      </header>

      {error ? (
        <div className="rounded-lg border border-destructive/40 bg-destructive/10 p-4 text-sm text-destructive">
          {extractApiError(error).message}
        </div>
      ) : isLoading ? (
        <div className="space-y-2">
          {Array.from({ length: 5 }).map((_, i) => (
            <Skeleton key={i} className="h-12 w-full" />
          ))}
        </div>
      ) : (
        <AccountTable
          accounts={data?.items ?? []}
          currentUserId={currentUser?.id}
          onToggleEnabled={handleToggleEnabled}
          onResetPassword={handleResetPassword}
        />
      )}

      <CreateAccountDialog
        open={createOpen}
        onOpenChange={setCreateOpen}
        presetEmployeeId={presetEmployeeId}
        onCreated={(response) =>
          setTempPassword({
            username: response.account.username,
            password: response.tempPassword,
          })
        }
      />

      <TempPasswordDialog
        open={!!tempPassword}
        onOpenChange={(open) => !open && setTempPassword(null)}
        username={tempPassword?.username ?? ''}
        tempPassword={tempPassword?.password ?? ''}
      />
    </div>
  );
}

export default function AccountsPage() {
  return (
    <AppShell>
      <Suspense>
        <AccountsPageContent />
      </Suspense>
    </AppShell>
  );
}
