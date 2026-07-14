'use client';

import { useTranslations } from 'next-intl';
import { Eye } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Checkbox } from '@/components/ui/checkbox';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { useAttendancePolicy } from '@/hooks/use-attendance-settings';
import type { AttendanceDayListItem, AttendanceDayStatus } from '@/lib/api/types';

export function attendanceDayKey(item: Pick<AttendanceDayListItem, 'employeeId' | 'date'>): string {
  return `${item.employeeId}|${item.date}`;
}

interface AttendanceDayTableProps {
  items: AttendanceDayListItem[];
  selected: Set<string>;
  onToggle: (key: string) => void;
  onToggleAll: (keys: string[], checked: boolean) => void;
  onViewDetails: (item: AttendanceDayListItem) => void;
}

const STATUS_VARIANT: Record<AttendanceDayStatus, 'success' | 'destructive'> = {
  Complete: 'success',
  Incomplete: 'destructive',
};

// ymd is a calendar date only (no time), already derived server-side under the policy's
// zone — format it as-is, with no zone conversion, so it can't drift from that date.
const dateFormatter = new Intl.DateTimeFormat('id-ID', { dateStyle: 'medium', timeZone: 'UTC' });

function formatDate(ymd: string): string {
  return dateFormatter.format(new Date(`${ymd}T00:00:00Z`));
}

function formatTime(iso: string | null, timeZoneId: string | undefined): string {
  return iso
    ? new Intl.DateTimeFormat('id-ID', { timeStyle: 'short', timeZone: timeZoneId }).format(new Date(iso))
    : '–';
}

export function AttendanceDayTable({
  items,
  selected,
  onToggle,
  onToggleAll,
  onViewDetails,
}: AttendanceDayTableProps) {
  const t = useTranslations('attendance');
  const { data: policy } = useAttendancePolicy();

  if (items.length === 0) {
    return (
      <div className="rounded-lg border border-dashed border-border p-8 text-center text-sm text-muted-foreground">
        {t('empty')}
      </div>
    );
  }

  const pageKeys = items.map(attendanceDayKey);
  const selectedOnPage = pageKeys.filter((key) => selected.has(key)).length;
  const allSelected = selectedOnPage === pageKeys.length;
  const someSelected = selectedOnPage > 0 && !allSelected;

  return (
    <div className="rounded-lg border border-border bg-card">
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead className="w-10">
              <Checkbox
                checked={allSelected}
                indeterminate={someSelected}
                onChange={(e) => onToggleAll(pageKeys, e.target.checked)}
                aria-label={t('selection.selectAll')}
              />
            </TableHead>
            <TableHead>{t('columns.employee')}</TableHead>
            <TableHead>{t('columns.date')}</TableHead>
            <TableHead>{t('columns.tapIn')}</TableHead>
            <TableHead>{t('columns.tapOut')}</TableHead>
            <TableHead>{t('columns.status')}</TableHead>
            <TableHead className="text-right">{t('columns.action')}</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {items.map((item) => {
            const key = attendanceDayKey(item);
            return (
              <TableRow key={key}>
                <TableCell>
                  <Checkbox
                    checked={selected.has(key)}
                    onChange={() => onToggle(key)}
                    aria-label={t('selection.selectRow')}
                  />
                </TableCell>
                <TableCell className="font-medium">{item.employeeFullName}</TableCell>
                <TableCell className="tabular-nums">{formatDate(item.date)}</TableCell>
                <TableCell className="tabular-nums">{formatTime(item.tapInUtc, policy?.timeZoneId)}</TableCell>
                <TableCell className="tabular-nums">{formatTime(item.tapOutUtc, policy?.timeZoneId)}</TableCell>
                <TableCell>
                  <Badge variant={STATUS_VARIANT[item.status]}>
                    {t(`status.${item.status}`)}
                  </Badge>
                </TableCell>
                <TableCell className="text-right">
                  <div className="flex justify-end gap-1">
                    <Button
                      variant="ghost"
                      size="icon"
                      onClick={() => onViewDetails(item)}
                      aria-label={t('actions.viewLogDetails')}
                      title={t('actions.viewLogDetails')}
                    >
                      <Eye className="h-4 w-4" />
                    </Button>
                  </div>
                </TableCell>
              </TableRow>
            );
          })}
        </TableBody>
      </Table>
    </div>
  );
}
