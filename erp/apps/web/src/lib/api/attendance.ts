import { apiClient } from './client';
import type {
  AttendanceDayKey,
  AttendanceLogResponse,
  GetAttendanceDayLogsResponse,
  ListAttendanceDaysParams,
  ListAttendanceDaysResponse,
  ListAttendanceLogsParams,
  ListAttendanceLogsResponse,
  RecordManualLogBody,
  UpdateAttendanceLogBody,
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

export async function listAttendanceDays(
  params: ListAttendanceDaysParams,
): Promise<ListAttendanceDaysResponse> {
  const { data } = await apiClient.get<ListAttendanceDaysResponse>('/api/attendance/days', {
    params: {
      page: params.page,
      pageSize: params.pageSize,
      employeeSearch: params.employeeSearch || undefined,
      dateFrom: params.dateFrom || undefined,
      dateTo: params.dateTo || undefined,
      status: params.status || undefined,
    },
  });
  return data;
}

export async function getAttendanceDayLogs(
  employeeId: string,
  date: string,
): Promise<GetAttendanceDayLogsResponse> {
  const { data } = await apiClient.get<GetAttendanceDayLogsResponse>(
    `/api/attendance/days/${employeeId}/${date}/logs`,
  );
  return data;
}

export async function updateAttendanceLog(
  id: string,
  body: UpdateAttendanceLogBody,
): Promise<AttendanceLogResponse> {
  const { data } = await apiClient.patch<AttendanceLogResponse>(
    `/api/attendance/logs/${id}`,
    body,
  );
  return data;
}

export async function exportAttendanceDays(dayKeys: AttendanceDayKey[]): Promise<Blob> {
  const { data } = await apiClient.post<Blob>(
    '/api/attendance/days/export',
    { items: dayKeys },
    { responseType: 'blob' },
  );
  return data;
}
