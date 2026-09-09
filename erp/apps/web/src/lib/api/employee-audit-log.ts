import { apiClient } from './client';
import type {
  ExportEmployeeAuditLogParams,
  ListEmployeeAuditLogParams,
  ListEmployeeAuditLogResponse,
} from './types';

export async function listEmployeeAuditLog(
  params: ListEmployeeAuditLogParams,
): Promise<ListEmployeeAuditLogResponse> {
  const { data } = await apiClient.get<ListEmployeeAuditLogResponse>('/api/employees/audit-log', {
    params: {
      page: params.page,
      pageSize: params.pageSize,
      filter: params.filter || undefined,
    },
  });
  return data;
}

export async function exportEmployeeAuditLog(params: ExportEmployeeAuditLogParams): Promise<Blob> {
  const { data } = await apiClient.get<Blob>('/api/employees/audit-log/export', {
    params: {
      filter: params.filter || undefined,
    },
    responseType: 'blob',
  });
  return data;
}
