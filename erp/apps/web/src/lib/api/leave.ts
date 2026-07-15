import { apiClient } from './client';
import type {
  CreateLeaveRequestBody,
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
