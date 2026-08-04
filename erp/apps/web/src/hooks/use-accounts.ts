'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  createAccount,
  listAccounts,
  listProvisionCandidates,
  resetAccountPassword,
  setAccountEnabled,
} from '@/lib/api/accounts';
import type { CreateAccountBody } from '@/lib/api/types';

const accountKeys = {
  all: ['accounts'] as const,
};

export function useAccounts() {
  return useQuery({
    queryKey: accountKeys.all,
    queryFn: listAccounts,
  });
}

export function useProvisionCandidates(enabled = true) {
  return useQuery({
    queryKey: [...accountKeys.all, 'provision-candidates'] as const,
    queryFn: listProvisionCandidates,
    enabled,
  });
}

export function useCreateAccount() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: CreateAccountBody) => createAccount(body),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: accountKeys.all });
    },
  });
}

export function useSetAccountEnabled() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, enabled }: { id: string; enabled: boolean }) =>
      setAccountEnabled(id, enabled),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: accountKeys.all });
    },
  });
}

export function useResetAccountPassword() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => resetAccountPassword(id),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: accountKeys.all });
    },
  });
}
