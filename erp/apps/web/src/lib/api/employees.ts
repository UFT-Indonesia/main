import { apiClient } from './client';
import type {
  CreateEmployeeBody,
  DeleteEmployeeBody,
  Employee,
  ListEmployeesParams,
  ListEmployeesResponse,
  SetLeaveQuotaBody,
  SetProbationEndBody,
  UpdateEmployeeBody,
} from './types';

export async function listEmployees(params: ListEmployeesParams): Promise<ListEmployeesResponse> {
  const { data } = await apiClient.get<ListEmployeesResponse>('/api/employees', {
    params: {
      page: params.page,
      pageSize: params.pageSize,
      search: params.search || undefined,
      role: params.role || undefined,
      status: params.status || undefined,
    },
  });
  return data;
}

export async function getEmployee(id: string): Promise<Employee> {
  const { data } = await apiClient.get<Employee>(`/api/employees/${id}`);
  return data;
}

export async function createEmployee(body: CreateEmployeeBody): Promise<Employee> {
  const { data } = await apiClient.post<Employee>('/api/employees', body);
  return data;
}

export async function updateEmployee(id: string, body: UpdateEmployeeBody): Promise<Employee> {
  const { data } = await apiClient.put<Employee>(`/api/employees/${id}`, body);
  return data;
}

export async function deleteEmployee(id: string, body?: DeleteEmployeeBody): Promise<Employee> {
  const { data } = await apiClient.delete<Employee>(`/api/employees/${id}`, {
    data: body ?? {},
  });
  return data;
}

/** Owner-only. Null endsOn clears the override and restores the three-month default. */
export async function setProbationEnd(id: string, body: SetProbationEndBody): Promise<Employee> {
  const { data } = await apiClient.put<Employee>(`/api/employees/${id}/probation`, body);
  return data;
}

/** Owner-only, one leave type per call. Null days clears the override. */
export async function setLeaveQuota(id: string, body: SetLeaveQuotaBody): Promise<Employee> {
  const { data } = await apiClient.put<Employee>(`/api/employees/${id}/quota`, body);
  return data;
}
