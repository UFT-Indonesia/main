import { apiClient } from './client';
import type { AttendancePolicy, UpdateAttendancePolicyBody } from './types';

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
