'use client';

import { useEffect, useState } from 'react';
import { useTranslations } from 'next-intl';
import { Search } from 'lucide-react';
import { Input } from '@/components/ui/input';
import { Select } from '@/components/ui/select';
import { useDebounce } from '@/hooks/use-debounce';
import type { AttendanceSource, PunchType } from '@/lib/api/types';

interface AttendanceFiltersProps {
  employeeSearch: string;
  dateFrom: string;
  dateTo: string;
  punchType: PunchType | '';
  source: AttendanceSource | '';
  onEmployeeSearchChange: (v: string) => void;
  onDateFromChange: (v: string) => void;
  onDateToChange: (v: string) => void;
  onPunchTypeChange: (v: PunchType | '') => void;
  onSourceChange: (v: AttendanceSource | '') => void;
}

export function AttendanceFilters({
  employeeSearch,
  dateFrom,
  dateTo,
  punchType,
  source,
  onEmployeeSearchChange,
  onDateFromChange,
  onDateToChange,
  onPunchTypeChange,
  onSourceChange,
}: AttendanceFiltersProps) {
  const t = useTranslations('attendance');

  const [localSearch, setLocalSearch] = useState(employeeSearch);
  const debouncedSearch = useDebounce(localSearch, 500);

  useEffect(() => {
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
          value={punchType}
          onChange={(e) => onPunchTypeChange(e.target.value as PunchType | '')}
        >
          <option value="">{t('filters.allPunchTypes')}</option>
          <option value="In">{t('punchType.In')}</option>
          <option value="Out">{t('punchType.Out')}</option>
        </Select>
      </div>
      <div className="w-full md:w-36">
        <Select
          value={source}
          onChange={(e) => onSourceChange(e.target.value as AttendanceSource | '')}
        >
          <option value="">{t('filters.allSources')}</option>
          <option value="Device">{t('source.Device')}</option>
          <option value="Manual">{t('source.Manual')}</option>
        </Select>
      </div>
    </div>
  );
}
