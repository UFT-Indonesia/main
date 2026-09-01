import type { EmployeeRole, EmployeeStatus } from '@/lib/api/types';

export const EMPLOYEE_ROLES: readonly EmployeeRole[] = ['Owner', 'Manager', 'Staff'] as const;
export const EMPLOYEE_STATUSES: readonly EmployeeStatus[] = [
  'Active',
  'OnLeave',
  'Terminated',
] as const;

export const APP_NAME = 'ERP UFT';

/**
 * The company's zone. Attendance surfaces prefer AttendancePolicy.timeZoneId — that value is
 * Owner-editable and is what the server buckets calendar days by — and fall back to this. HR
 * fields (hire, salary-effective, termination) use it directly rather than pulling in an
 * attendance API call just to know what day it is.
 *
 * Never use the browser's zone for either: a laptop set to another zone silently shifts the
 * instant a punch is recorded at, and `toISOString().slice(0, 10)` (UTC) returns yesterday
 * before 07:00 WIB.
 */
export const APP_TIME_ZONE = 'Asia/Jakarta';
