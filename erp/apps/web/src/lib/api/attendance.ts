import { apiClient } from './client';
import type {
  AttendanceLogResponse,
  ListAttendanceLogsParams,
  ListAttendanceLogsResponse,
  RecordManualLogBody,
} from './types';

export async function listAttendanceLogs(
  params: ListAttendanceLogsParams,
): Promise<ListAttendanceLogsResponse> {
  const { data } = await apiClient.get<ListAttendanceLogsResponse>('/api/attendance', {
    params: {
      page: params.page,
      pageSize: params.pageSize,
      employeeSearch: params.employeeSearch || undefined,
      dateFrom: params.dateFrom || undefined,
      dateTo: params.dateTo || undefined,
      punchType: params.punchType || undefined,
      source: params.source || undefined,
    },
  });
  return data;
}

export async function recordManualLog(body: RecordManualLogBody): Promise<AttendanceLogResponse> {
  const { data } = await apiClient.post<AttendanceLogResponse>('/api/attendance/manual-logs', body);
  return data;
}
