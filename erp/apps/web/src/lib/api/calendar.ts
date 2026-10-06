import { apiClient } from './client';
import type { DayMarker } from './types';

/**
 * Leave and OT dates of one employee inside a window — kinds only, never type or reason, so
 * anyone signed in may ask (same openness as the leave calendar).
 */
export async function getDayMarkers(employeeId: string, from: string, to: string): Promise<DayMarker[]> {
  const { data } = await apiClient.get<{ markers: DayMarker[] }>('/api/calendar/markers', {
    params: { employeeId, from, to },
  });
  return data.markers;
}
