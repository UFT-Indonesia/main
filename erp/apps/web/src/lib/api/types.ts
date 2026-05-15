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
