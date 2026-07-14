'use client';

import { useTranslations } from 'next-intl';
import { Badge } from '@/components/ui/badge';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import type { AttendanceLogListItem, AttendanceSource } from '@/lib/api/types';

interface AttendanceTableProps {
  items: AttendanceLogListItem[];
}

const SOURCE_VARIANT: Record<AttendanceSource, 'outline' | 'secondary'> = {
  Device: 'outline',
  Manual: 'secondary',
};

const dateTimeFormatter = new Intl.DateTimeFormat('id-ID', {
  dateStyle: 'medium',
  timeStyle: 'short',
  timeZone: 'Asia/Jakarta',
});

function formatDateTime(iso: string): string {
  return dateTimeFormatter.format(new Date(iso));
}

export function AttendanceTable({ items }: AttendanceTableProps) {
  const t = useTranslations('attendance');

  if (items.length === 0) {
    return (
      <div className="rounded-lg border border-dashed border-border p-8 text-center text-sm text-muted-foreground">
        {t('empty')}
      </div>
    );
  }

  return (
    <div className="rounded-lg border border-border bg-card">
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead>{t('columns.employee')}</TableHead>
            <TableHead>{t('columns.punchedAt')}</TableHead>
            <TableHead>{t('columns.punchType')}</TableHead>
            <TableHead>{t('columns.source')}</TableHead>
            <TableHead>{t('columns.note')}</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {items.map((item) => (
            <TableRow key={item.id}>
              <TableCell className="font-medium">{item.employeeFullName}</TableCell>
              <TableCell className="tabular-nums">{formatDateTime(item.punchedAtUtc)}</TableCell>
              <TableCell>
                <Badge variant={item.punchType === 'In' ? 'success' : 'destructive'}>
                  {t(`punchType.${item.punchType}`)}
                </Badge>
              </TableCell>
              <TableCell>
                <Badge variant={SOURCE_VARIANT[item.source]}>{t(`source.${item.source}`)}</Badge>
              </TableCell>
              <TableCell className="max-w-48 text-muted-foreground">
                {item.notes.length > 0 ? (
                  <span className="block truncate" title={item.notes.map((n) => n.text).join('\n')}>
                    {item.notes.length > 1 ? `(${item.notes.length}) ` : ''}
                    {item.notes[item.notes.length - 1]?.text}
                  </span>
                ) : (
                  '—'
                )}
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </div>
  );
}
