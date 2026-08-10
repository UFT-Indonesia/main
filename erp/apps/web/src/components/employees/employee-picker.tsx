'use client';

import { useState } from 'react';
import { useTranslations } from 'next-intl';
import { Combobox } from '@/components/ui/combobox';
import { useEmployees } from '@/hooks/use-employees';

interface EmployeePickerProps {
  value: string;
  onChange: (value: string) => void;
  enabled?: boolean;
  placeholder?: string;
  clearable?: boolean;
}

/** Active-employee search picker shared by every "assign to an employee" form. */
export function EmployeePicker({ value, onChange, enabled = true, placeholder, clearable = true }: EmployeePickerProps) {
  const tCommon = useTranslations('common');
  const [search, setSearch] = useState('');

  const employeesQuery = useEmployees({ status: 'Active', search, pageSize: 50 }, enabled);
  const options = (employeesQuery.data?.items ?? []).map((e) => ({
    value: e.id,
    label: e.fullName,
    meta: e.role,
  }));

  return (
    <Combobox
      value={value}
      onChange={onChange}
      options={options}
      placeholder={placeholder}
      searchPlaceholder={tCommon('search')}
      onSearchChange={setSearch}
      loading={employeesQuery.isLoading}
      disabled={!enabled}
      clearable={clearable}
    />
  );
}
