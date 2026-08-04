'use client';

import { useEffect, useMemo, useState } from 'react';
import { useTranslations } from 'next-intl';
import { Button } from '@/components/ui/button';
import { Dialog, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select } from '@/components/ui/select';
import { useCreateAccount, useProvisionCandidates } from '@/hooks/use-accounts';
import { extractApiError } from '@/lib/api/client';
import { useToast } from '@/hooks/use-toast';
import type { CreateAccountResponse } from '@/lib/api/types';

interface CreateAccountDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  /** Pre-selects the employee when opened from the employees page. */
  presetEmployeeId?: string;
  onCreated: (response: CreateAccountResponse) => void;
}

function suggestUsername(fullName: string): string {
  return fullName
    .toLowerCase()
    .normalize('NFD')
    .replace(/[̀-ͯ]/g, '')
    .replace(/[^a-z0-9\s]/g, '')
    .trim()
    .split(/\s+/)
    .slice(0, 2)
    .join('.');
}

export function CreateAccountDialog({
  open,
  onOpenChange,
  presetEmployeeId,
  onCreated,
}: CreateAccountDialogProps) {
  const t = useTranslations('accounts.create');
  const tCommon = useTranslations('common');
  const toast = useToast();

  const { data: candidatesData, isLoading } = useProvisionCandidates(open);
  const createMutation = useCreateAccount();

  const [employeeId, setEmployeeId] = useState('');
  const [username, setUsername] = useState('');
  const [email, setEmail] = useState('');
  const [usernameTouched, setUsernameTouched] = useState(false);

  const candidates = useMemo(() => candidatesData?.items ?? [], [candidatesData]);

  useEffect(() => {
    if (open) {
      // eslint-disable-next-line react-hooks/set-state-in-effect
      setEmployeeId(presetEmployeeId ?? '');
      setUsername('');
      setEmail('');
      setUsernameTouched(false);
    }
  }, [open, presetEmployeeId]);

  useEffect(() => {
    if (usernameTouched) return;
    const candidate = candidates.find((c) => c.employeeId === employeeId);
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setUsername(candidate ? suggestUsername(candidate.fullName) : '');
  }, [employeeId, candidates, usernameTouched]);

  const submit = async () => {
    try {
      const response = await createMutation.mutateAsync({
        employeeId,
        username: username.trim(),
        email: email.trim() || null,
      });
      onOpenChange(false);
      onCreated(response);
    } catch (error) {
      toast.error(t('errorTitle'), extractApiError(error).message);
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogHeader>
        <DialogTitle>{t('title')}</DialogTitle>
      </DialogHeader>
      <div className="mt-4 space-y-4">
        <div className="space-y-1.5">
          <Label htmlFor="account-employee">{t('employee')}</Label>
          <Select
            id="account-employee"
            value={employeeId}
            onChange={(e) => setEmployeeId(e.target.value)}
            disabled={isLoading}
          >
            <option value="">{isLoading ? tCommon('loading') : t('selectEmployee')}</option>
            {candidates.map((candidate) => (
              <option key={candidate.employeeId} value={candidate.employeeId}>
                {candidate.fullName}
              </option>
            ))}
          </Select>
        </div>

        <div className="space-y-1.5">
          <Label htmlFor="account-username">{t('username')}</Label>
          <Input
            id="account-username"
            value={username}
            onChange={(e) => {
              setUsername(e.target.value);
              setUsernameTouched(true);
            }}
            autoComplete="off"
          />
        </div>

        <div className="space-y-1.5">
          <Label htmlFor="account-email">{t('email')}</Label>
          <Input
            id="account-email"
            type="email"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            placeholder={t('emailOptional')}
            autoComplete="off"
          />
        </div>
      </div>
      <DialogFooter>
        <Button variant="outline" onClick={() => onOpenChange(false)}>
          {tCommon('cancel')}
        </Button>
        <Button
          onClick={submit}
          disabled={!employeeId || !username.trim() || createMutation.isPending}
        >
          {createMutation.isPending ? tCommon('loading') : t('submit')}
        </Button>
      </DialogFooter>
    </Dialog>
  );
}
