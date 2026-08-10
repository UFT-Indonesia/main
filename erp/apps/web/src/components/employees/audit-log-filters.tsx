'use client';

import { useTranslations } from 'next-intl';
import { Input } from '@/components/ui/input';
import { Select } from '@/components/ui/select';
import { EmployeePicker } from '@/components/employees/employee-picker';
import { AUDIT_EVENT_TYPES } from '@/components/employees/audit-log-event-types';

interface AuditLogFiltersProps {
  employeeId: string;
  dateFrom: string;
  dateTo: string;
  eventType: string;
  onEmployeeIdChange: (v: string) => void;
  onDateFromChange: (v: string) => void;
  onDateToChange: (v: string) => void;
  onEventTypeChange: (v: string) => void;
}

export function AuditLogFilters({
  employeeId,
  dateFrom,
  dateTo,
  eventType,
  onEmployeeIdChange,
  onDateFromChange,
  onDateToChange,
  onEventTypeChange,
}: AuditLogFiltersProps) {
  const t = useTranslations('employeeAuditLog');

  return (
    <div className="flex flex-col gap-3 md:flex-row md:flex-wrap md:items-end">
      <div className="min-w-0 flex-1 md:min-w-[14rem]">
        <EmployeePicker
          value={employeeId}
          onChange={onEmployeeIdChange}
          placeholder={t('filters.employeePlaceholder')}
        />
      </div>
      <div className="w-full md:w-36">
        <Input
          type="date"
          value={dateFrom}
          onChange={(e) => onDateFromChange(e.target.value)}
          aria-label={t('filters.dateFrom')}
        />
      </div>
      <div className="w-full md:w-36">
        <Input
          type="date"
          value={dateTo}
          onChange={(e) => onDateToChange(e.target.value)}
          aria-label={t('filters.dateTo')}
        />
      </div>
      <div className="w-full md:w-56">
        <Select value={eventType} onChange={(e) => onEventTypeChange(e.target.value)}>
          <option value="">{t('filters.allEventTypes')}</option>
          {AUDIT_EVENT_TYPES.map((type) => (
            <option key={type} value={type}>
              {t(`eventType.${type}`)}
            </option>
          ))}
        </Select>
      </div>
    </div>
  );
}
