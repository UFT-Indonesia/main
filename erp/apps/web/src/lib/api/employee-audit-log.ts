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
      employeeId: params.employeeId || undefined,
      dateFrom: params.dateFrom || undefined,
      dateTo: params.dateTo || undefined,
      eventType: params.eventType || undefined,
    },
  });
  return data;
}

export async function exportEmployeeAuditLog(params: ExportEmployeeAuditLogParams): Promise<Blob> {
  const { data } = await apiClient.get<Blob>('/api/employees/audit-log/export', {
    params: {
      employeeId: params.employeeId || undefined,
      dateFrom: params.dateFrom || undefined,
      dateTo: params.dateTo || undefined,
      eventType: params.eventType || undefined,
    },
    responseType: 'blob',
  });
  return data;
}
