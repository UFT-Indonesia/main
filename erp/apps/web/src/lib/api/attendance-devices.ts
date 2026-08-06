import { apiClient } from './client';
import type {
  ListAttendanceDevicesResponse,
  RegisterAttendanceDeviceBody,
  RegisterAttendanceDeviceResponse,
} from './types';

export async function listAttendanceDevices(): Promise<ListAttendanceDevicesResponse> {
  const { data } = await apiClient.get<ListAttendanceDevicesResponse>('/api/attendance/devices');
  return data;
}

export async function registerAttendanceDevice(
  body: RegisterAttendanceDeviceBody,
): Promise<RegisterAttendanceDeviceResponse> {
  const { data } = await apiClient.post<RegisterAttendanceDeviceResponse>(
    '/api/attendance/devices',
    body,
  );
  return data;
}

export async function setAttendanceDeviceEnabled(id: string, enabled: boolean): Promise<void> {
  await apiClient.patch(`/api/attendance/devices/${id}/enabled`, { enabled });
}
