'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  getAttendanceDayLogs,
  listAttendanceDays,
  listAttendanceLogs,
  recordManualLog,
  updateAttendanceLog,
} from '@/lib/api/attendance';
import type {
  ListAttendanceDaysParams,
  ListAttendanceLogsParams,
  RecordManualLogBody,
  UpdateAttendanceLogBody,
} from '@/lib/api/types';

const attendanceKeys = {
  all: ['attendance'] as const,
  lists: () => [...attendanceKeys.all, 'list'] as const,
  list: (params: ListAttendanceLogsParams) => [...attendanceKeys.lists(), params] as const,
  days: () => [...attendanceKeys.all, 'days'] as const,
  day: (params: ListAttendanceDaysParams) => [...attendanceKeys.days(), params] as const,
  dayLogs: (employeeId: string, date: string) =>
    [...attendanceKeys.all, 'day-logs', employeeId, date] as const,
};

export function useAttendanceLogs(params: ListAttendanceLogsParams) {
  return useQuery({
    queryKey: attendanceKeys.list(params),
    queryFn: () => listAttendanceLogs(params),
    placeholderData: (prev) => prev,
  });
}

export function useAttendanceDays(params: ListAttendanceDaysParams) {
  return useQuery({
    queryKey: attendanceKeys.day(params),
    queryFn: () => listAttendanceDays(params),
    placeholderData: (prev) => prev,
  });
}

export function useAttendanceDayLogs(employeeId: string, date: string, enabled = true) {
  return useQuery({
    queryKey: attendanceKeys.dayLogs(employeeId, date),
    queryFn: () => getAttendanceDayLogs(employeeId, date),
    enabled: enabled && !!employeeId && !!date,
  });
}

export function useRecordManualLog() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: RecordManualLogBody) => recordManualLog(body),
    onSuccess: () => {
      // A new punch changes both the raw log list and the derived day view.
      qc.invalidateQueries({ queryKey: attendanceKeys.all });
    },
  });
}

export function useUpdateAttendanceLog() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, body }: { id: string; body: UpdateAttendanceLogBody }) =>
      updateAttendanceLog(id, body),
    onSuccess: () => {
      // Editing a punch changes the derived day (Tap-In/Tap-Out/Status).
      qc.invalidateQueries({ queryKey: attendanceKeys.all });
    },
  });
}
