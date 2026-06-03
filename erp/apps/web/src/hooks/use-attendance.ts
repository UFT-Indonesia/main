'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { listAttendanceLogs, recordManualLog } from '@/lib/api/attendance';
import type { ListAttendanceLogsParams, RecordManualLogBody } from '@/lib/api/types';

const attendanceKeys = {
  all: ['attendance'] as const,
  lists: () => [...attendanceKeys.all, 'list'] as const,
  list: (params: ListAttendanceLogsParams) => [...attendanceKeys.lists(), params] as const,
};

export function useAttendanceLogs(params: ListAttendanceLogsParams) {
  return useQuery({
    queryKey: attendanceKeys.list(params),
    queryFn: () => listAttendanceLogs(params),
    placeholderData: (prev) => prev,
  });
}

export function useRecordManualLog() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: RecordManualLogBody) => recordManualLog(body),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: attendanceKeys.lists() });
    },
  });
}
