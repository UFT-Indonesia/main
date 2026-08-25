/** Audit event types the UI knows how to label and summarise. Mirrors the domain events. */
export const AUDIT_EVENT_TYPES = [
  'employee.created',
  'employee.basic_info_changed',
  'employee.salary_changed',
  'employee.role_changed',
  'employee.parent_changed',
  'employee.terminated',
  'employee.hire_date_changed',
  'employee.probation_end_changed',
  'employee.leave_quota_changed',
] as const;

/**
 * Guards the `eventType.*` message lookup: an event type added server-side before the
 * translations catch up should show its raw name, not throw and blank the whole page.
 */
export function isKnownEventType(eventType: string): boolean {
  return (AUDIT_EVENT_TYPES as readonly string[]).includes(eventType);
}
