'use client';

import { useEffect, useRef, useState } from 'react';
import { useTranslations } from 'next-intl';
import { Search } from 'lucide-react';
import { Input } from '@/components/ui/input';
import { Select } from '@/components/ui/select';
import { useDebounce } from '@/hooks/use-debounce';
import type { AttendanceDayStatus } from '@/lib/api/types';

interface AttendanceDayFiltersProps {
  employeeSearch: string;
  dateFrom: string;
  dateTo: string;
  status: AttendanceDayStatus | '';
  onEmployeeSearchChange: (v: string) => void;
  onDateFromChange: (v: string) => void;
  onDateToChange: (v: string) => void;
  onStatusChange: (v: AttendanceDayStatus | '') => void;
}

export function AttendanceDayFilters({
  employeeSearch,
  dateFrom,
  dateTo,
  status,
  onEmployeeSearchChange,
  onDateFromChange,
  onDateToChange,
  onStatusChange,
}: AttendanceDayFiltersProps) {
  const t = useTranslations('attendance');

  const [localSearch, setLocalSearch] = useState(employeeSearch);
  const debouncedSearch = useDebounce(localSearch, 500);

  // onEmployeeSearchChange gets a new identity on every parent render (inline callback),
  // which would otherwise re-fire this effect — and the parent's handler resets pagination
  // unconditionally. Guard on the VALUE, not the effect firing, so an identity-only churn
  // is a no-op instead of a spurious page reset.
  const lastEmitted = useRef(employeeSearch);
  useEffect(() => {
    if (lastEmitted.current === debouncedSearch) return;
    lastEmitted.current = debouncedSearch;
    onEmployeeSearchChange(debouncedSearch);
  }, [debouncedSearch, onEmployeeSearchChange]);

  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setLocalSearch(employeeSearch);
  }, [employeeSearch]);

  return (
    <div className="flex flex-col gap-3 md:flex-row md:flex-wrap md:items-end">
      <div className="min-w-0 flex-1">
        <div className="relative">
          <Search className="pointer-events-none absolute left-2.5 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
          <Input
            value={localSearch}
            onChange={(e) => setLocalSearch(e.target.value)}
            placeholder={t('searchPlaceholder')}
            className="pl-8"
          />
        </div>
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
      <div className="w-full md:w-36">
        <Select
          value={status}
          onChange={(e) => onStatusChange(e.target.value as AttendanceDayStatus | '')}
        >
          <option value="">{t('filters.allStatuses')}</option>
          <option value="Complete">{t('status.Complete')}</option>
          <option value="Incomplete">{t('status.Incomplete')}</option>
        </Select>
      </div>
    </div>
  );
}
