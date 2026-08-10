'use client';

import { useQuery } from '@tanstack/react-query';
import { listEmployeeAuditLog } from '@/lib/api/employee-audit-log';
import type { ListEmployeeAuditLogParams } from '@/lib/api/types';

const auditLogKeys = {
  all: ['employee-audit-log'] as const,
  list: (params: ListEmployeeAuditLogParams) => [...auditLogKeys.all, 'list', params] as const,
};

/** `enabled` is false for callers (non-Owner) the endpoint would reject anyway. */
export function useEmployeeAuditLog(params: ListEmployeeAuditLogParams, enabled = true) {
  return useQuery({
    queryKey: auditLogKeys.list(params),
    queryFn: () => listEmployeeAuditLog(params),
    placeholderData: (previous) => previous,
    enabled,
  });
}
