'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { createLeaveRequest, decideLeaveRequest, listLeaveRequests } from '@/lib/api/leave';
import type { CreateLeaveRequestBody, ListLeaveRequestsParams } from '@/lib/api/types';

const leaveKeys = {
  all: ['leave'] as const,
  list: (params: ListLeaveRequestsParams) => [...leaveKeys.all, 'list', params] as const,
};

export function useLeaveRequests(params: ListLeaveRequestsParams) {
  return useQuery({
    queryKey: leaveKeys.list(params),
    queryFn: () => listLeaveRequests(params),
    placeholderData: (prev) => prev,
  });
}

export function useCreateLeaveRequest() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: CreateLeaveRequestBody) => createLeaveRequest(body),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: leaveKeys.all });
    },
  });
}

export function useDecideLeaveRequest() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, action, note }: {
      id: string;
      action: 'approve' | 'deny' | 'cancel';
      note?: string | null;
    }) => decideLeaveRequest(id, action, note),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: leaveKeys.all });
    },
  });
}
