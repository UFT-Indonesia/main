'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  createLeaveRequest,
  decideLeaveRequest,
  getBlockedLeaveDates,
  getLeaveBalance,
  listLeaveRequests,
} from '@/lib/api/leave';
import type {
  BlockedLeaveDatesParams,
  CreateLeaveRequestBody,
  ListLeaveRequestsParams,
} from '@/lib/api/types';

const leaveKeys = {
  all: ['leave'] as const,
  list: (params: ListLeaveRequestsParams) => [...leaveKeys.all, 'list', params] as const,
  balance: (employeeId: string, year?: number) =>
    [...leaveKeys.all, 'balance', employeeId, year ?? 'current'] as const,
  blockedDates: (
    employeeId: string, from: string, to: string, candidate: BlockedLeaveDatesParams | undefined,
  ) => [...leaveKeys.all, 'blocked-dates', employeeId, from, to, candidate ?? {}] as const,
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
 * Which dates are blocked for one employee, for the date pickers. Disabled until an employee is
 * picked, so a dialog can call it unconditionally. Shares the leave keys, so approving or
 * cancelling a request re-greys the calendars.
 *
 * `candidate` is the in-progress request's own shape (half-day/hourly) — omit it for a picker
 * that isn't filing leave itself (e.g. the manual punch picker), which should keep treating any
 * approved leave as a full-day block. Passing it live as the leave form's fields change lets a
 * date that only partially conflicts show up as merely a hint instead of fully blocked.
 */
export function useBlockedLeaveDates(
  employeeId: string | null | undefined,
  candidate?: BlockedLeaveDatesParams,
) {
  const { from, to } = blockedWindow();
  return useQuery({
    queryKey: leaveKeys.blockedDates(employeeId ?? '', from, to, candidate),
    queryFn: () => getBlockedLeaveDates(employeeId!, from, to, candidate),
    enabled: !!employeeId,
  });
}
