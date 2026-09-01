'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  createLeaveRequest,
  decideLeaveRequest,
  getBlockedLeaveDates,
  getLeaveBalance,
  listLeaveRequests,
} from '@/lib/api/leave';
import type { CreateLeaveRequestBody, ListLeaveRequestsParams } from '@/lib/api/types';

const leaveKeys = {
  all: ['leave'] as const,
  list: (params: ListLeaveRequestsParams) => [...leaveKeys.all, 'list', params] as const,
  balance: (employeeId: string, year?: number) =>
    [...leaveKeys.all, 'balance', employeeId, year ?? 'current'] as const,
  blockedDates: (employeeId: string, from: string, to: string) =>
    [...leaveKeys.all, 'blocked-dates', employeeId, from, to] as const,
};

/**
 * A wide window around today, so the pickers do not refetch as the user pages through months.
 * Approved leave more than a year out either way is not something anyone is booking against.
 */
function blockedWindow(): { from: string; to: string } {
  const year = new Date().getUTCFullYear();
  return { from: `${year - 1}-01-01`, to: `${year + 1}-12-31` };
}

/**
 * Disabled until an employee is picked, so the create dialog can call it unconditionally.
 * Invalidated with the rest of the leave keys, so approving a request refreshes the balance.
 */
export function useLeaveBalance(employeeId: string | null | undefined, year?: number) {
  return useQuery({
    queryKey: leaveKeys.balance(employeeId ?? '', year),
    queryFn: () => getLeaveBalance(employeeId!, year),
    enabled: !!employeeId,
  });
}

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

/**
 * Approved leave spans for one employee, for the date pickers. Disabled until an employee is
 * picked, so a dialog can call it unconditionally. Shares the leave keys, so approving or
 * cancelling a request re-greys the calendars.
 */
export function useBlockedLeaveDates(employeeId: string | null | undefined) {
  const { from, to } = blockedWindow();
  return useQuery({
    queryKey: leaveKeys.blockedDates(employeeId ?? '', from, to),
    queryFn: () => getBlockedLeaveDates(employeeId!, from, to),
    enabled: !!employeeId,
  });
}
