import type { EmployeeRole, EmployeeStatus } from '@/lib/api/types';

export const EMPLOYEE_ROLES: readonly EmployeeRole[] = ['Owner', 'Manager', 'Staff'] as const;
export const EMPLOYEE_STATUSES: readonly EmployeeStatus[] = [
  'Active',
  'OnLeave',
  'Terminated',
] as const;

export const APP_NAME = 'ERP UFT Davis';
