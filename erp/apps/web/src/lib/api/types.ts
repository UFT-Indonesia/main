export interface AuthUser {
  id: string;
  username: string;
  email: string;
  fullName: string;
  employeeId: string | null;
  mustChangePassword: boolean;
  roles: string[];
}

export interface AuthResponse {
  accessToken: string;
  tokenType: string;
  expiresAtUtc: string;
  user: AuthUser;
}

export interface ApiError {
  code?: string;
  message: string;
  fieldErrors?: Record<string, string[]>;
}

export type EmployeeRole = 'Owner' | 'Manager' | 'Staff';
export type EmployeeStatus = 'Active' | 'OnLeave' | 'Terminated';

export interface Employee {
  id: string;
  fullName: string;
  /** National ID. Null unless the caller may read this employee's details. */
  nik: string | null;
  npwp: string | null;
  /** Null for non-Owner callers — pay is redacted server-side. */
  monthlyWageAmount: number | null;
  monthlyWageCurrency: string | null;
  effectiveSalaryFrom: string | null;
  role: EmployeeRole;
  status: EmployeeStatus;
  parentId: string | null;
  terminationDate: string | null;
  /** "YYYY-MM-DD". Null for employees hired before the field existed, or a caller without standing. */
  hireDate: string | null;
  /** Effective probation end — the override if set, otherwise three months from the hire date. */
  probationEndsOn: string | null;
  /** Set only when an owner pinned the date by hand rather than taking the default. */
  probationEndsOnOverride: string | null;
  /** Leave type to overridden entitlement; types left on the default are absent. */
  leaveQuotaOverrides: Partial<Record<LeaveType, number>> | null;
}

export interface ListEmployeesResponse {
  items: Employee[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface ListEmployeesParams {
  page?: number;
  pageSize?: number;
  search?: string;
  role?: EmployeeRole | '';
  status?: EmployeeStatus | '';
}

export interface CreateEmployeeBody {
  fullName: string;
  nik: string;
  npwp?: string | null;
  monthlyWageAmount: number;
  effectiveSalaryFrom: string;
  role: EmployeeRole;
  parentId?: string | null;
  /** Required. Anchors probation, and through it the annual leave entitlement. */
  hireDate: string;
}

export interface UpdateEmployeeBody
  extends Omit<CreateEmployeeBody, 'monthlyWageAmount' | 'effectiveSalaryFrom' | 'hireDate'> {
  /** Omit or null to leave pay unchanged. Managers must omit it — only Owner may set pay. */
  monthlyWageAmount?: number | null;
  effectiveSalaryFrom?: string | null;
  /** Omit or null to leave the hire date unchanged. Owner-only, like pay; it cannot be cleared. */
  hireDate?: string | null;
}

export interface SetProbationEndBody {
  /** Null clears the owner's override, restoring the three-month default. */
  endsOn: string | null;
}

export interface SetLeaveQuotaBody {
  type: LeaveType;
  /** Null clears the override. Zero is a real setting and means "none of this type". */
  days: number | null;
}

export interface DeleteEmployeeBody {
  terminationDate?: string | null;
}

export interface Account {
  id: string;
  username: string;
  email: string | null;
  fullName: string;
  employeeId: string | null;
  role: EmployeeRole;
  isEnabled: boolean;
  mustChangePassword: boolean;
}

export interface ListAccountsResponse {
  items: Account[];
}

export interface EmployeeAuditLogEntry {
  id: string;
  employeeId: string;
  employeeFullName: string;
  eventType: string;
  occurredAtUtc: string;
  oldValueJson: string | null;
  newValueJson: string | null;
  /** Null for changes with no interactive caller (seeding, background jobs). */
  actorUserId: string | null;
  actorName: string | null;
}

export interface ListEmployeeAuditLogResponse {
  items: EmployeeAuditLogEntry[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface ListEmployeeAuditLogParams {
  page?: number;
  pageSize?: number;
  employeeId?: string;
  dateFrom?: string;
  dateTo?: string;
  eventType?: string;
}

export interface ExportEmployeeAuditLogParams {
  employeeId?: string;
  dateFrom?: string;
  dateTo?: string;
  eventType?: string;
}

export interface CreateAccountBody {
  employeeId: string;
  username: string;
  email?: string | null;
}

export interface CreateAccountResponse {
  account: Account;
  /** Shown exactly once; not retrievable afterwards. */
  tempPassword: string;
}

export interface ResetAccountPasswordResponse {
  tempPassword: string;
}

export interface ProvisionCandidate {
  employeeId: string;
  fullName: string;
  role: EmployeeRole;
}

export interface ListProvisionCandidatesResponse {
  items: ProvisionCandidate[];
}

export type PunchType = 'In' | 'Out';
export type AttendanceSource = 'Device' | 'Manual';

export interface AttendanceLogNote {
  id: string;
  text: string;
  createdByUserId: string;
  createdByName: string;
  createdAtUtc: string;
}

export interface AttendanceLogListItem {
  id: string;
  employeeId: string;
  employeeFullName: string;
  punchedAtUtc: string;
  source: AttendanceSource;
  punchType: PunchType;
  deviceId: string | null;
  recordedByUserId: string | null;
  notes: AttendanceLogNote[];
  /** Server-computed: whether the caller may alter this employee's records. */
  canWrite: boolean;
}

export interface ListAttendanceLogsResponse {
  items: AttendanceLogListItem[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface ListAttendanceLogsParams {
  page?: number;
  pageSize?: number;
  employeeSearch?: string;
  dateFrom?: string;
  dateTo?: string;
  punchType?: PunchType | '';
  source?: AttendanceSource | '';
}

export type AttendanceDayStatus = 'Complete' | 'Incomplete' | 'OnLeave';

export interface AttendanceDayListItem {
  employeeId: string;
  employeeFullName: string;
  /** Calendar date in the attendance policy time zone, "YYYY-MM-DD". */
  date: string;
  tapInUtc: string | null;
  tapOutUtc: string | null;
  status: AttendanceDayStatus;
  /** The kind of leave covering this day (Annual/Sick/…), empty when none does. */
  leaveType: LeaveType | '';
  /** Detail of that leave, all null when no leave covers the day. */
  leaveStartDate: string | null;
  leaveEndDate: string | null;
  leaveWorkdayCount: number | null;
  leaveReason: string | null;
  leaveRequestedAtUtc: string | null;
  leaveDecidedByName: string | null;
  leaveDecidedAtUtc: string | null;
  /** Server-computed: whether the caller may alter this employee's records. */
  canWrite: boolean;
}

export interface ListAttendanceDaysResponse {
  items: AttendanceDayListItem[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface ListAttendanceDaysParams {
  page?: number;
  pageSize?: number;
  employeeSearch?: string;
  /** "YYYY-MM-DD" calendar date (inclusive). */
  dateFrom?: string;
  /** "YYYY-MM-DD" calendar date (inclusive). */
  dateTo?: string;
  status?: AttendanceDayStatus | '';
}

export interface GetAttendanceDayLogsResponse {
  items: AttendanceLogListItem[];
}

export interface UpdateAttendanceLogBody {
  punchedAtUtc: string;
  punchType: PunchType;
}

export interface AttendanceDayKey {
  employeeId: string;
  /** "YYYY-MM-DD" calendar date. */
  date: string;
}

export interface RecordManualLogBody {
  employeeId: string;
  punchedAtUtc: string;
  punchType: PunchType;
  note?: string | null;
}

export interface AttendanceLogResponse {
  id: string;
  employeeId: string;
  punchedAtUtc: string;
  source: AttendanceSource;
  punchType: PunchType;
  deviceId: string | null;
  recordedByUserId: string | null;
  notes: AttendanceLogNote[];
}

export interface AttendanceDevice {
  id: string;
  /** The identifier the physical reader sends on every punch. */
  deviceKey: string;
  name: string;
  enabled: boolean;
  createdAtUtc: string;
}

export interface ListAttendanceDevicesResponse {
  items: AttendanceDevice[];
}

export interface RegisterAttendanceDeviceBody {
  deviceKey: string;
  name: string;
}

export interface RegisterAttendanceDeviceResponse {
  device: AttendanceDevice;
  /** Shown exactly once; not retrievable afterwards. */
  secret: string;
}

export type LeaveType = 'Annual' | 'Sick' | 'Permission' | 'Unpaid';
export type LeaveRequestStatus = 'Pending' | 'Approved' | 'Denied' | 'Cancelled';
/** Server-side pseudo-status: Pending or Approved, i.e. everything still standing. */
export type LeaveStatusFilter = LeaveRequestStatus | 'Open';
export type LeaveCancellationReason = 'WithdrawnByEmployee' | 'RecalledForWork';

export interface LeaveRequest {
  id: string;
  employeeId: string;
  employeeFullName: string;
  /** Null when the caller may not read this request's details — Sick is health data. */
  type: LeaveType | null;
  /** "YYYY-MM-DD" inclusive. */
  startDate: string;
  /** "YYYY-MM-DD" inclusive. */
  endDate: string;
  workdayCount: number;
  reason: string | null;
  /** The doctor's note on a Sick request. Null when there is none, or it is not readable. */
  attachment: LeaveAttachment | null;
  status: LeaveRequestStatus;
  requestedAtUtc: string;
  decidedByName: string | null;
  decidedAtUtc: string | null;
  decisionNote: string | null;
  /** Set only once status is Cancelled. */
  cancellationReason: LeaveCancellationReason | null;
  /** Total approved workdays away this year, all types. Null when the caller may not read the balance. */
  approvedWorkdaysThisYear: number | null;
  /**
   * What is actually enforced for this request's own type. Null when the caller may not read
   * the balance, or may not read the type the block would name.
   */
  quota: LeaveQuota | null;
  /** Server-computed: the rules depend on the subject's role and reporting line, which the client cannot see. */
  canDecide: boolean;
  canCancel: boolean;
}

export interface ListLeaveRequestsResponse {
  items: LeaveRequest[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface ListLeaveRequestsParams {
  page?: number;
  pageSize?: number;
  status?: LeaveStatusFilter | '';
  employeeId?: string;
}

/**
 * One leave type's standing for one employee in one year. Null entitled/remaining means
 * uncapped — an owner, or a type with no override. Remaining may be negative when a cap was
 * set after the days were already taken; the server reports it raw rather than clamping.
 */
export interface LeaveQuota {
  type: LeaveType;
  entitledDays: number | null;
  usedDays: number;
  remainingDays: number | null;
}

export interface LeaveBalance {
  employeeId: string;
  employeeFullName: string;
  year: number;
  onProbation: boolean;
  probationEndsOn: string | null;
  quotas: LeaveQuota[];
}

export type ProbationExtensionStatus = 'Pending' | 'Approved' | 'Denied' | 'Cancelled';

export interface ProbationExtension {
  id: string;
  employeeId: string;
  employeeFullName: string;
  /** The probation end at the time the request was filed. */
  currentEndsOn: string;
  /** The date approval will write. */
  proposedEndsOn: string;
  reason: string;
  status: ProbationExtensionStatus;
  requestedAtUtc: string;
  decidedByName: string | null;
  decidedAtUtc: string | null;
  decisionNote: string | null;
  /** Server-computed: approve/deny take an owner, cancel takes the manager who filed it. */
  canDecide: boolean;
  canCancel: boolean;
}

export interface ListProbationExtensionsResponse {
  items: ProbationExtension[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface ListProbationExtensionsParams {
  page?: number;
  pageSize?: number;
  status?: ProbationExtensionStatus | '';
  employeeId?: string;
}

export interface CreateProbationExtensionBody {
  employeeId: string;
  proposedEndsOn: string;
  reason: string;
}

export interface CreateLeaveRequestBody {
  employeeId: string;
  type: LeaveType;
  startDate: string;
  endDate: string;
  reason: string;
  /** Required for Sick, rejected for every other type. Sent as multipart, not JSON. */
  attachment?: File | null;
}

/** What the API says about a stored attachment. The bytes come from a separate download call. */
export interface LeaveAttachment {
  fileName: string;
  contentType: string;
  sizeBytes: number;
}

/** One approved leave span, inclusive both ends. Dates only — no type, no reason. */
export interface BlockedLeaveRange {
  startDate: string;
  endDate: string;
}

export interface BlockedLeaveDatesResponse {
  ranges: BlockedLeaveRange[];
}

export interface AttendancePolicy {
  /** "HH:mm" formatted shift start. */
  shiftStart: string;
  /** "HH:mm" formatted shift end. */
  shiftEnd: string;
  clockInGraceMinutes: number;
  clockOutGraceMinutes: number;
  /** IANA time zone id (e.g. "Asia/Jakarta"). */
  timeZoneId: string;
  updatedByUserId: string;
  updatedAtUtc: string;
}

export interface UpdateAttendancePolicyBody {
  shiftStart: string;
  shiftEnd: string;
  clockInGraceMinutes: number;
  clockOutGraceMinutes: number;
  timeZoneId: string;
}
