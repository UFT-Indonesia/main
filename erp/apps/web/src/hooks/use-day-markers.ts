'use client';

import { useMemo } from 'react';
import { useQuery } from '@tanstack/react-query';
import { getDayMarkers } from '@/lib/api/calendar';
import type { DayMarkerKind } from '@/lib/api/types';

/** date ("YYYY-MM-DD") → the kinds marked on it. */
export type DayMarkers = ReadonlyMap<string, readonly DayMarkerKind[]>;

/**
 * Same wide window as the blocked-leave-dates query, so pickers do not refetch while the user
 * pages through months. Under the 'leave' key on purpose: leave and OT mutations already
 * invalidate it, so a new request re-marks every open calendar.
 */
function window(): { from: string; to: string } {
  const year = new Date().getUTCFullYear();
  return { from: `${year - 1}-01-01`, to: `${year + 1}-12-31` };
}

/** Disabled until an employee is picked, so any picker's dialog can call it unconditionally. */
export function useDayMarkers(employeeId: string | null | undefined): DayMarkers | undefined {
  const { from, to } = window();
  const { data } = useQuery({
    queryKey: ['leave', 'day-markers', employeeId ?? '', from, to],
    queryFn: () => getDayMarkers(employeeId!, from, to),
    enabled: !!employeeId,
  });

  return useMemo(() => {
    if (!data) return undefined;
    const map = new Map<string, DayMarkerKind[]>();
    for (const { date, kind } of data) map.set(date, [...(map.get(date) ?? []), kind]);
    return map;
  }, [data]);
}

/** Dates carrying any of `kinds` — e.g. the OT dates a leave picker must refuse. */
export function markedDates(markers: DayMarkers | undefined, kinds: readonly DayMarkerKind[]): string[] {
  if (!markers) return [];
  return [...markers].filter(([, found]) => found.some((k) => kinds.includes(k))).map(([date]) => date);
}
