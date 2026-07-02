'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  createEmployee,
  deleteEmployee,
  getEmployee,
  listEmployees,
  updateEmployee,
} from '@/lib/api/employees';
import type {
  CreateEmployeeBody,
  DeleteEmployeeBody,
  ListEmployeesParams,
  UpdateEmployeeBody,
} from '@/lib/api/types';

const employeeKeys = {
  all: ['employees'] as const,
  lists: () => [...employeeKeys.all, 'list'] as const,
  list: (params: ListEmployeesParams) => [...employeeKeys.lists(), params] as const,
  details: () => [...employeeKeys.all, 'detail'] as const,
  detail: (id: string) => [...employeeKeys.details(), id] as const,
};

export function useEmployees(params: ListEmployeesParams) {
  return useQuery({
    queryKey: employeeKeys.list(params),
    queryFn: () => listEmployees(params),
    placeholderData: (previous) => previous,
  });
}

export function useEmployee(id: string | undefined) {
  return useQuery({
    queryKey: id ? employeeKeys.detail(id) : ['employees', 'detail', 'none'],
    queryFn: () => getEmployee(id as string),
    enabled: !!id,
  });
}

export function useCreateEmployee() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: CreateEmployeeBody) => createEmployee(body),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: employeeKeys.lists() });
    },
  });
}

export function useUpdateEmployee(id: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: UpdateEmployeeBody) => updateEmployee(id, body),
    onSuccess: (data) => {
      qc.invalidateQueries({ queryKey: employeeKeys.lists() });
      qc.setQueryData(employeeKeys.detail(id), data);
    },
  });
}

export function useDeleteEmployee() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, body }: { id: string; body?: DeleteEmployeeBody }) =>
      deleteEmployee(id, body),
    onSuccess: (data) => {
      qc.invalidateQueries({ queryKey: employeeKeys.lists() });
      qc.setQueryData(employeeKeys.detail(data.id), data);
    },
  });
}

export function useParentCandidates(search: string, enabled = true) {
  const ownerQuery = useQuery({
    queryKey: [...employeeKeys.all, 'parent-candidates', 'Owner', search] as const,
    queryFn: () => listEmployees({ role: 'Owner', status: 'Active', search, pageSize: 50 }),
    placeholderData: (prev) => prev,
    enabled,
  });

  const managerQuery = useQuery({
    queryKey: [...employeeKeys.all, 'parent-candidates', 'Manager', search] as const,
    queryFn: () => listEmployees({ role: 'Manager', status: 'Active', search, pageSize: 50 }),
    placeholderData: (prev) => prev,
    enabled,
  });

  const owners = ownerQuery.data?.items ?? [];
  const managers = managerQuery.data?.items ?? [];
  const combined = [...owners, ...managers].sort((a, b) => a.fullName.localeCompare(b.fullName));

  return {
    candidates: combined,
    isLoading: ownerQuery.isLoading || managerQuery.isLoading,
  };
}
