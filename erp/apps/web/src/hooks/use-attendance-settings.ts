'use client';

import { useMemo } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  getAttendancePolicy,
  listHolidays,
  removeHoliday,
  saveHoliday,
  updateAttendancePolicy,
} from '@/lib/api/attendance-settings';
import type { Holiday, SaveHolidayBody, UpdateAttendancePolicyBody } from '@/lib/api/types';

const attendanceSettingsKeys = {
  all: ['attendance-settings'] as const,
  policy: () => [...attendanceSettingsKeys.all, 'policy'] as const,
  holidays: (from: string, to: string) => [...attendanceSettingsKeys.all, 'holidays', from, to] as const,
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

export function useHolidays(from: string, to: string) {
  return useQuery({
    queryKey: attendanceSettingsKeys.holidays(from, to),
    queryFn: () => listHolidays(from, to),
    enabled: !!from && !!to,
  });
}

export interface HolidayCalendar {
  /** "YYYY-MM-DD" → the holiday on that date. */
  byDate: ReadonlyMap<string, Holiday>;
  /** Same dates, for `countWorkdays` and the pickers. */
  dates: ReadonlySet<string>;
}

/**
 * Every holiday from 1 Jan last year to 31 Dec next year, as one cached query — what the leave
 * form and the attendance table need to agree with the server's workday rule.
 * ponytail: a leave dated outside that window previews without holidays; the server still
 * counts them correctly. Widen the window if old leave ever gets edited routinely.
 */
export function useHolidayCalendar(): HolidayCalendar {
  const year = new Date().getFullYear();
  const { data } = useHolidays(`${year - 1}-01-01`, `${year + 1}-12-31`);

  return useMemo(() => {
    const byDate = new Map((data ?? []).map((holiday) => [holiday.date, holiday]));
    return { byDate, dates: new Set(byDate.keys()) };
  }, [data]);
}

/**
 * A holiday moves leave counts and who reads Absent, so those caches go too. The server's
 * recount runs just after the write commits; a refetch that beats it catches up on the next.
 */
function useInvalidateHolidayDependents() {
  const qc = useQueryClient();
  return () => {
    qc.invalidateQueries({ queryKey: attendanceSettingsKeys.all });
    qc.invalidateQueries({ queryKey: ['attendance'] });
    qc.invalidateQueries({ queryKey: ['leave'] });
  };
}

export function useSaveHoliday() {
  const invalidate = useInvalidateHolidayDependents();
  return useMutation({
    mutationFn: ({ date, body }: { date: string; body: SaveHolidayBody }) => saveHoliday(date, body),
    onSuccess: invalidate,
  });
}

export function useRemoveHoliday() {
  const invalidate = useInvalidateHolidayDependents();
  return useMutation({
    mutationFn: (date: string) => removeHoliday(date),
    onSuccess: invalidate,
  });
}
