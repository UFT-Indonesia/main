import { AUDIT_EVENT_TYPES } from '@/components/employees/audit-log-event-types';
import { EMPLOYEE_ROLES, EMPLOYEE_STATUSES } from '@/lib/constants';
import type { FilterFieldDef } from './types';

/**
 * What each menu's filter builder offers. Data, not components — the one <FilterBuilder /> reads
 * these, so a new filterable field is a line here plus a line in the matching C# registry.
 *
 * `ownerOnly` mirrors a server rule rather than implementing one: the API answers 403 for these
 * fields whoever asks, because filtering a redacted column would leak its value through the row
 * count. Hiding them here only keeps the menu honest.
 */

export const EMPLOYEE_FILTER_FIELDS: readonly FilterFieldDef[] = [
  { key: 'fullName', type: 'text', labelKey: 'employees.filters.fields.fullName' },
  {
    key: 'role',
    type: 'enum',
    labelKey: 'employees.filters.fields.role',
    options: EMPLOYEE_ROLES,
    optionLabelPrefix: 'employees.form.roleOptions',
  },
  {
    key: 'status',
    type: 'enum',
    labelKey: 'employees.filters.fields.status',
    options: EMPLOYEE_STATUSES,
    optionLabelPrefix: 'employees.form.statusOptions',
  },
  { key: 'hireDate', type: 'date', labelKey: 'employees.filters.fields.hireDate', nullable: true },

  // NIK and NPWP are stored as converted value objects, so the server can only compare them
  // whole — no "contains". Equality takes a complete, valid NIK.
  { key: 'nik', type: 'convertedText', labelKey: 'employees.filters.fields.nik', ownerOnly: true },
  {
    key: 'npwp',
    type: 'convertedText',
    labelKey: 'employees.filters.fields.npwp',
    nullable: true,
    ownerOnly: true,
  },
  { key: 'monthlyWage', type: 'number', labelKey: 'employees.filters.fields.monthlyWage', ownerOnly: true },
  {
    key: 'effectiveSalaryFrom',
    type: 'date',
    labelKey: 'employees.filters.fields.effectiveSalaryFrom',
    ownerOnly: true,
  },
  {
    key: 'terminationDate',
    type: 'date',
    labelKey: 'employees.filters.fields.terminationDate',
    nullable: true,
    ownerOnly: true,
  },
];

export const ATTENDANCE_DAY_FILTER_FIELDS: readonly FilterFieldDef[] = [
  { key: 'employeeName', type: 'text', labelKey: 'attendance.filters.fields.employeeName' },
  { key: 'employeeId', type: 'relation', labelKey: 'attendance.filters.fields.employee', relation: 'employee' },
  { key: 'date', type: 'date', labelKey: 'attendance.filters.fields.date' },
  {
    key: 'status',
    type: 'enum',
    labelKey: 'attendance.filters.fields.status',
    options: ['Complete', 'Incomplete', 'OnLeave'],
    optionLabelPrefix: 'attendance.status',
  },
  { key: 'tapIn', type: 'date', labelKey: 'attendance.filters.fields.tapIn', nullable: true },
  { key: 'tapOut', type: 'date', labelKey: 'attendance.filters.fields.tapOut', nullable: true },
];

export const AUDIT_LOG_FILTER_FIELDS: readonly FilterFieldDef[] = [
  {
    key: 'employeeId',
    type: 'relation',
    labelKey: 'employeeAuditLog.filters.fields.employee',
    relation: 'employee',
  },
  {
    key: 'eventType',
    type: 'enum',
    labelKey: 'employeeAuditLog.filters.fields.eventType',
    options: AUDIT_EVENT_TYPES,
    optionLabelPrefix: 'employeeAuditLog.eventType',
  },
  { key: 'occurredAt', type: 'date', labelKey: 'employeeAuditLog.filters.fields.occurredAt' },
  { key: 'actorName', type: 'text', labelKey: 'employeeAuditLog.filters.fields.actorName', nullable: true },
];

export const LEAVE_FILTER_FIELDS: readonly FilterFieldDef[] = [
  { key: 'employeeId', type: 'relation', labelKey: 'leave.filters.fields.employee', relation: 'employee' },
  { key: 'employeeName', type: 'text', labelKey: 'leave.filters.fields.employeeName' },
  {
    key: 'status',
    type: 'enum',
    labelKey: 'leave.filters.fields.status',
    // "Open" is gone: it existed only because there was no OR. Pick both here instead.
    options: ['Pending', 'Approved', 'Denied', 'Cancelled'],
    optionLabelPrefix: 'leave.status',
  },
  { key: 'startDate', type: 'date', labelKey: 'leave.filters.fields.startDate' },
  { key: 'endDate', type: 'date', labelKey: 'leave.filters.fields.endDate' },
  { key: 'workdayCount', type: 'number', labelKey: 'leave.filters.fields.workdayCount' },
  { key: 'requestedAt', type: 'date', labelKey: 'leave.filters.fields.requestedAt' },
  { key: 'decidedByName', type: 'text', labelKey: 'leave.filters.fields.decidedByName', nullable: true },
  { key: 'decidedAt', type: 'date', labelKey: 'leave.filters.fields.decidedAt', nullable: true },

  // Redacted per row unless you may read the request's details, so Owner-only to filter.
  {
    key: 'type',
    type: 'enum',
    labelKey: 'leave.filters.fields.type',
    options: ['Annual', 'Sick', 'Permission', 'Unpaid'],
    optionLabelPrefix: 'leave.type',
    ownerOnly: true,
  },
  { key: 'reason', type: 'text', labelKey: 'leave.filters.fields.reason', ownerOnly: true },
  {
    key: 'decisionNote',
    type: 'text',
    labelKey: 'leave.filters.fields.decisionNote',
    nullable: true,
    ownerOnly: true,
  },
  { key: 'halfDay', type: 'bool', labelKey: 'leave.filters.fields.halfDay', ownerOnly: true },
];

export const PROBATION_FILTER_FIELDS: readonly FilterFieldDef[] = [
  { key: 'employeeId', type: 'relation', labelKey: 'probation.filters.fields.employee', relation: 'employee' },
  { key: 'employeeName', type: 'text', labelKey: 'probation.filters.fields.employeeName' },
  {
    key: 'status',
    type: 'enum',
    labelKey: 'probation.filters.fields.status',
    options: ['Pending', 'Approved', 'Denied', 'Cancelled'],
    optionLabelPrefix: 'probation.status',
  },
  { key: 'currentEndsOn', type: 'date', labelKey: 'probation.filters.fields.currentEndsOn' },
  { key: 'proposedEndsOn', type: 'date', labelKey: 'probation.filters.fields.proposedEndsOn' },
  { key: 'reason', type: 'text', labelKey: 'probation.filters.fields.reason' },
  { key: 'requestedAt', type: 'date', labelKey: 'probation.filters.fields.requestedAt' },
  { key: 'decidedByName', type: 'text', labelKey: 'probation.filters.fields.decidedByName', nullable: true },
  { key: 'decidedAt', type: 'date', labelKey: 'probation.filters.fields.decidedAt', nullable: true },
];
