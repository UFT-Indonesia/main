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
}

export type EmployeeRole = 'Owner' | 'Manager' | 'Staff';
export type EmployeeStatus = 'Active' | 'OnLeave' | 'Terminated';

export interface Employee {
  id: string;
  fullName: string;
  nik: string;
  npwp: string | null;
  /** Null for non-Owner callers — pay is redacted server-side. */
  monthlyWageAmount: number | null;
  monthlyWageCurrency: string | null;
  effectiveSalaryFrom: string | null;
  role: EmployeeRole;
  status: EmployeeStatus;
  parentId: string | null;
  terminationDate: string | null;
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
}

export interface UpdateEmployeeBody extends Omit<CreateEmployeeBody, 'monthlyWageAmount' | 'effectiveSalaryFrom'> {
  /** Omit or null to leave pay unchanged. Managers must omit it — only Owner may set pay. */
  monthlyWageAmount?: number | null;
  effectiveSalaryFrom?: string | null;
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

export type AttendanceDayStatus = 'Complete' | 'Incomplete';

export interface AttendanceDayListItem {
  employeeId: string;
  employeeFullName: string;
  /** Calendar date in the attendance policy time zone, "YYYY-MM-DD". */
  date: string;
  tapInUtc: string | null;
  tapOutUtc: string | null;
  status: AttendanceDayStatus;
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

export interface LeaveRequest {
  id: string;
  employeeId: string;
  employeeFullName: string;
  type: LeaveType;
  /** "YYYY-MM-DD" inclusive. */
  startDate: string;
  /** "YYYY-MM-DD" inclusive. */
  endDate: string;
  workdayCount: number;
  reason: string | null;
  status: LeaveRequestStatus;
  requestedAtUtc: string;
  decidedByName: string | null;
  decidedAtUtc: string | null;
  decisionNote: string | null;
  /** Null when the caller may not read this employee's balance. */
  approvedWorkdaysThisYear: number | null;
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
  status?: LeaveRequestStatus | '';
  employeeId?: string;
}

export interface CreateLeaveRequestBody {
  employeeId: string;
  type: LeaveType;
  startDate: string;
  endDate: string;
  reason?: string | null;
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
