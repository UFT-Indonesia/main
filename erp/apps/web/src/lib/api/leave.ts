import { apiClient } from './client';
import type {
  BlockedLeaveDatesResponse,
  CreateLeaveRequestBody,
  LeaveBalance,
  LeaveRequest,
  ListLeaveRequestsParams,
  ListLeaveRequestsResponse,
} from './types';

export async function listLeaveRequests(
  params: ListLeaveRequestsParams,
): Promise<ListLeaveRequestsResponse> {
  const { data } = await apiClient.get<ListLeaveRequestsResponse>('/api/leave', {
    params: {
      page: params.page,
      pageSize: params.pageSize,
      status: params.status || undefined,
      employeeId: params.employeeId || undefined,
    },
  });
  return data;
}

export async function createLeaveRequest(body: CreateLeaveRequestBody): Promise<LeaveRequest> {
  const { data } = await apiClient.post<LeaveRequest>('/api/leave', body);
  return data;
}

export async function decideLeaveRequest(
  id: string,
  action: 'approve' | 'deny' | 'cancel',
  note?: string | null,
): Promise<LeaveRequest> {
  const { data } = await apiClient.post<LeaveRequest>(`/api/leave/${id}/${action}`, {
    note: note || null,
  });
  return data;
}

/** Entitlement, usage and remaining days across all four types. Year omitted means the current one. */
export async function getLeaveBalance(employeeId: string, year?: number): Promise<LeaveBalance> {
  const { data } = await apiClient.get<LeaveBalance>('/api/leave/balance', {
    params: { employeeId, year },
  });
  return data;
}

/**
 * Approved leave spans for one employee inside a window, for the date pickers to grey out.
 * The window is required by the API — it is what keeps the query bounded as tenure grows.
 */
export async function getBlockedLeaveDates(
  employeeId: string,
  from: string,
  to: string,
): Promise<BlockedLeaveDatesResponse> {
  const { data } = await apiClient.get<BlockedLeaveDatesResponse>('/api/leave/blocked-dates', {
    params: { employeeId, from, to },
  });
  return data;
}
