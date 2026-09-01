'use client';

import { useTranslations } from 'next-intl';
import { DateRangePickerField } from '@/components/ui/date-picker';
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
      <div className="w-full md:w-72">
        <DateRangePickerField
          start={dateFrom}
          end={dateTo}
          onChange={(from, to) => {
            onDateFromChange(from);
            onDateToChange(to);
          }}
          aria-label={t('filters.dateRange')}
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
