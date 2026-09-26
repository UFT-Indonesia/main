import { apiClient } from './client';
import type { AttendancePolicy, Holiday, SaveHolidayBody, UpdateAttendancePolicyBody } from './types';

export async function getAttendancePolicy(): Promise<AttendancePolicy> {
  const { data } = await apiClient.get<AttendancePolicy>('/api/attendance/policy');
  return data;
}

export async function updateAttendancePolicy(
  body: UpdateAttendancePolicyBody,
): Promise<AttendancePolicy> {
  const { data } = await apiClient.put<AttendancePolicy>('/api/attendance/policy', body);
  return data;
}

/** Holidays with `from` ≤ date ≤ `to`, both "YYYY-MM-DD", oldest first. */
export async function listHolidays(from: string, to: string): Promise<Holiday[]> {
  const { data } = await apiClient.get<Holiday[]>('/api/attendance/holidays', { params: { from, to } });
  return data;
}

/** Declares a holiday on `date`, or renames the one already there. */
export async function saveHoliday(date: string, body: SaveHolidayBody): Promise<Holiday> {
  const { data } = await apiClient.put<Holiday>(`/api/attendance/holidays/${date}`, body);
  return data;
}

export async function removeHoliday(date: string): Promise<void> {
  await apiClient.delete(`/api/attendance/holidays/${date}`);
}
