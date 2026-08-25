'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  createProbationExtension,
  decideProbationExtension,
  listProbationExtensions,
} from '@/lib/api/probation';
import type {
  CreateProbationExtensionBody,
  ListProbationExtensionsParams,
} from '@/lib/api/types';

const probationKeys = {
  all: ['probation'] as const,
  list: (params: ListProbationExtensionsParams) => [...probationKeys.all, 'list', params] as const,
};

export function useProbationExtensions(params: ListProbationExtensionsParams) {
  return useQuery({
    queryKey: probationKeys.list(params),
    queryFn: () => listProbationExtensions(params),
    placeholderData: (prev) => prev,
  });
}

export function useCreateProbationExtension() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: CreateProbationExtensionBody) => createProbationExtension(body),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: probationKeys.all });
    },
  });
}

export function useDecideProbationExtension() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, action, note }: {
      id: string;
      action: 'approve' | 'deny' | 'cancel';
      note?: string | null;
    }) => decideProbationExtension(id, action, note),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: probationKeys.all });
      // Approval moves the employee's probation end, which changes their leave entitlement.
      qc.invalidateQueries({ queryKey: ['employees'] });
      qc.invalidateQueries({ queryKey: ['leave'] });
    },
  });
}
