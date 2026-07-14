'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { getAttendancePolicy, updateAttendancePolicy } from '@/lib/api/attendance-settings';
import type { UpdateAttendancePolicyBody } from '@/lib/api/types';

const attendanceSettingsKeys = {
  all: ['attendance-settings'] as const,
  policy: () => [...attendanceSettingsKeys.all, 'policy'] as const,
};

export function useAttendancePolicy() {
  return useQuery({
    queryKey: attendanceSettingsKeys.policy(),
    queryFn: () => getAttendancePolicy(),
  });
}

export function useUpdateAttendancePolicy() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: UpdateAttendancePolicyBody) => updateAttendancePolicy(body),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: attendanceSettingsKeys.all });
    },
  });
}
