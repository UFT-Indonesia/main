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

/**
 * Multipart rather than JSON: Sick leave carries a doctor's note, and the endpoint takes the
 * file alongside the fields in one request rather than staging an upload first.
 */
export async function createLeaveRequest(body: CreateLeaveRequestBody): Promise<LeaveRequest> {
  const form = new FormData();
  form.append('employeeId', body.employeeId);
  form.append('type', body.type);
  form.append('startDate', body.startDate);
  form.append('endDate', body.endDate);
  form.append('reason', body.reason);
  if (body.attachment) {
    form.append('attachment', body.attachment);
  }

  // apiClient defaults every request to Content-Type: application/json, and axios only fills
  // in the multipart boundary itself when no Content-Type is already set — so that default has
  // to be cleared here, or the browser sends the form body under the wrong header and the
  // server rejects it with 415 Unsupported Media Type.
  const { data } = await apiClient.post<LeaveRequest>('/api/leave', form, {
    headers: { 'Content-Type': undefined },
  });
  return data;
}

/**
 * The attachment's bytes, as a blob for the caller to hand to a download. Separate from the
 * request payload because it is served by an endpoint that re-checks who is asking.
 */
export async function downloadLeaveAttachment(id: string): Promise<Blob> {
  const { data } = await apiClient.get<Blob>(`/api/leave/${id}/attachment`, {
    responseType: 'blob',
  });
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
