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

export interface AttendanceLogListItem {
  id: string;
  employeeId: string;
  employeeFullName: string;
  punchedAtUtc: string;
  source: AttendanceSource;
  punchType: PunchType;
  deviceId: string | null;
  recordedByUserId: string | null;
  note: string | null;
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
  note: string | null;
}
