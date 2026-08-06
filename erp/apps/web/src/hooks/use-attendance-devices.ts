'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  listAttendanceDevices,
  registerAttendanceDevice,
  setAttendanceDeviceEnabled,
} from '@/lib/api/attendance-devices';
import type { RegisterAttendanceDeviceBody } from '@/lib/api/types';

const deviceKeys = {
  all: ['attendance-devices'] as const,
};

export function useAttendanceDevices(enabled = true) {
  return useQuery({
    queryKey: deviceKeys.all,
    queryFn: listAttendanceDevices,
    enabled,
  });
}

export function useRegisterAttendanceDevice() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: RegisterAttendanceDeviceBody) => registerAttendanceDevice(body),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: deviceKeys.all });
    },
  });
}

export function useSetAttendanceDeviceEnabled() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, enabled }: { id: string; enabled: boolean }) =>
      setAttendanceDeviceEnabled(id, enabled),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: deviceKeys.all });
    },
  });
}
