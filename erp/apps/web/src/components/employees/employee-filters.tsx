'use client';

import { useTranslations } from 'next-intl';
import { Search } from 'lucide-react';
import { Input } from '@/components/ui/input';
import { Select } from '@/components/ui/select';
import { EMPLOYEE_ROLES, EMPLOYEE_STATUSES } from '@/lib/constants';
import type { EmployeeRole, EmployeeStatus } from '@/lib/api/types';

interface EmployeeFiltersProps {
  search: string;
  role: EmployeeRole | '';
  status: EmployeeStatus | '';
  onSearchChange: (value: string) => void;
  onRoleChange: (value: EmployeeRole | '') => void;
  onStatusChange: (value: EmployeeStatus | '') => void;
}

export function EmployeeFilters({
  search,
  role,
  status,
  onSearchChange,
  onRoleChange,
  onStatusChange,
}: EmployeeFiltersProps) {
  const t = useTranslations('employees');
  const tForm = useTranslations('employees.form');

  return (
    <div className="flex flex-col gap-3 md:flex-row md:items-end">
      <div className="flex-1">
        <div className="relative">
          <Search className="pointer-events-none absolute left-2.5 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
          <Input
            value={search}
            onChange={(e) => onSearchChange(e.target.value)}
            placeholder={t('searchPlaceholder')}
            className="pl-8"
          />
        </div>
      </div>
      <div className="w-full md:w-44">
        <Select
          value={role}
          onChange={(e) => onRoleChange(e.target.value as EmployeeRole | '')}
        >
          <option value="">{t('filters.allRoles')}</option>
          {EMPLOYEE_ROLES.map((r) => (
            <option key={r} value={r}>
              {tForm(`roleOptions.${r}`)}
            </option>
          ))}
        </Select>
      </div>
      <div className="w-full md:w-44">
        <Select
          value={status}
          onChange={(e) => onStatusChange(e.target.value as EmployeeStatus | '')}
        >
          <option value="">{t('filters.allStatuses')}</option>
          {EMPLOYEE_STATUSES.map((s) => (
            <option key={s} value={s}>
              {tForm(`statusOptions.${s}`)}
            </option>
          ))}
        </Select>
      </div>
    </div>
  );
}
