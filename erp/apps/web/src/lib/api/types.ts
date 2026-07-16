export interface AuthUser {
  id: string;
  email: string;
  fullName: string;
  employeeId: string | null;
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
  monthlyWageAmount: number;
  monthlyWageCurrency: string;
  effectiveSalaryFrom: string;
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

export type UpdateEmployeeBody = CreateEmployeeBody;

export interface DeleteEmployeeBody {
  terminationDate?: string | null;
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
  approvedWorkdaysThisYear: number;
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
