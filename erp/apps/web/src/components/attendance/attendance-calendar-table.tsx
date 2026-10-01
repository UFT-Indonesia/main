'use client';

import { useState } from 'react';
import { useTranslations } from 'next-intl';
import { ChevronDown, ChevronRight } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { useAttendancePolicy, useHolidayCalendar } from '@/hooks/use-attendance-settings';
import { type DateLocale, useDateLocale } from '@/hooks/use-date-locale';
import { cn } from '@/lib/utils';
import type {
  AttendanceCalendarDate,
  AttendanceDayListItem,
  AttendanceDayStatus,
} from '@/lib/api/types';

const STATUS_VARIANT: Record<AttendanceDayStatus, 'success' | 'destructive' | 'warning' | 'yellow' | 'outline' | 'secondary'> = {
  Complete: 'success',
  Incomplete: 'destructive',
  // A no-show is the one thing on this page that needs chasing today.
  Absent: 'destructive',
  // Matches the employee table's OnLeave badge — an absence that was approved is neither
  // a completed day nor a failure to show up.
  OnLeave: 'warning',
  // Today, still unfinished: neither of these is a verdict.
  ClockedIn: 'yellow',
  NotInYet: 'outline',
  Upcoming: 'outline',
  // Reported, not judged: there was no shift to complete.
  WorkedOnDayOff: 'secondary',
};

/** A date is "in trouble" only for these. Approved leave is expected, not a problem. */
export const PROBLEM_STATUSES: readonly AttendanceDayStatus[] = ['Absent', 'Incomplete'];

export function hasProblem(date: AttendanceCalendarDate): boolean {
  return (
    date.isWorkday
    && !date.isFuture
    && !date.isInProgress
    && date.employees.some((employee) => PROBLEM_STATUSES.includes(employee.status))
  );
}

// ymd is a calendar date only (no time), already derived server-side under the policy's
// zone — format it as-is, with no zone conversion, so it can't drift from that date.
const DATE_OPTIONS: Intl.DateTimeFormatOptions = {
  weekday: 'short',
  day: '2-digit',
  month: 'short',
  year: 'numeric',
  timeZone: 'UTC',
};

function formatDate(ymd: string, locale: DateLocale): string {
  return new Intl.DateTimeFormat(locale, DATE_OPTIONS).format(new Date(`${ymd}T00:00:00Z`));
}

function formatTime(iso: string | null, timeZoneId: string | undefined, locale: DateLocale): string {
  return iso
    ? new Intl.DateTimeFormat(locale, { timeStyle: 'short', timeZone: timeZoneId }).format(new Date(iso))
    : '–';
}

function countBy(date: AttendanceCalendarDate, status: AttendanceDayStatus): number {
  return date.employees.filter((employee) => employee.status === status).length;
}

interface AttendanceCalendarTableProps {
  dates: AttendanceCalendarDate[];
  /**
   * A caller scoped to their own record. The summary then shows that one person's day rather
   * than counts, which is the same table rendering the reference timesheet.
   */
  selfView: boolean;
  onViewDetails: (item: AttendanceDayListItem) => void;
}

export function AttendanceCalendarTable({
  dates,
  selfView,
  onViewDetails,
}: AttendanceCalendarTableProps) {
  const t = useTranslations('attendance');
  const dateLocale = useDateLocale();
  const { data: policy } = useAttendancePolicy();
  const holidays = useHolidayCalendar();
  const [expanded, setExpanded] = useState<Set<string>>(new Set());

  if (dates.length === 0) {
    return (
      <div className="rounded-lg border border-dashed border-border p-8 text-center text-sm text-muted-foreground">
        {t('empty')}
      </div>
    );
  }

  const toggle = (date: string) => {
    setExpanded((prev) => {
      const next = new Set(prev);
      if (next.has(date)) {
        next.delete(date);
      } else {
        next.add(date);
      }
      return next;
    });
  };

  return (
    <div className="rounded-lg border border-border bg-card">
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead className="w-10" />
            <TableHead className="text-center">{t('columns.date')}</TableHead>
            <TableHead className="text-center">{t('columns.scheduleTime')}</TableHead>
            <TableHead className="text-center">{t('columns.headcount')}</TableHead>
            <TableHead className="text-center">{t('columns.present')}</TableHead>
            <TableHead className="text-center">{t('columns.absent')}</TableHead>
            <TableHead className="text-center">{t('columns.incomplete')}</TableHead>
            <TableHead className="text-center">{t('columns.onLeave')}</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {dates.map((date) => {
            const isOpen = expanded.has(date.date);
            const holiday = holidays.byDate.get(date.date);
            // A weekend or an untouched future date has nothing to open.
            const canExpand = date.employees.length > 0;
            // Counts only mean something once a day has settled into a verdict for every
            // employee. Self view, future dates, and today-in-progress all need the merged
            // note instead — see DateSummary.
            const settled = !selfView && date.isWorkday && !date.isFuture
              && date.employees.length > 0 && !date.isInProgress;

            return [
              <TableRow
                key={date.date}
                onClick={() => canExpand && toggle(date.date)}
                className={cn(
                  canExpand && 'cursor-pointer',
                  !date.isWorkday && 'text-muted-foreground',
                )}
              >
                <TableCell>
                  {canExpand && (
                    <span aria-label={t('actions.expandDate')}>
                      {isOpen
                        ? <ChevronDown className="h-4 w-4" />
                        : <ChevronRight className="h-4 w-4" />}
                    </span>
                  )}
                </TableCell>
                <TableCell className={cn('text-center tabular-nums', date.isInProgress && 'font-semibold')}>
                  {formatDate(date.date, dateLocale)}
                </TableCell>
                <TableCell className="text-center tabular-nums">
                  {holiday ? (
                    <span className="text-destructive">
                      {t(`holidayKind.${holiday.kind}`)}: {holiday.name}
                    </span>
                  ) : date.isWorkday && policy ? `${policy.shiftStart} – ${policy.shiftEnd}` : '–'}
                </TableCell>
                {settled ? (
                  <>
                    <TableCell className="text-center tabular-nums">{date.employees.length}</TableCell>
                    <TableCell className="text-center tabular-nums">{countBy(date, 'Complete')}</TableCell>
                    <TableCell className="text-center tabular-nums">{countBy(date, 'Absent')}</TableCell>
                    <TableCell className="text-center tabular-nums">{countBy(date, 'Incomplete')}</TableCell>
                    <TableCell className="text-center tabular-nums">{countBy(date, 'OnLeave')}</TableCell>
                  </>
                ) : !date.isWorkday ? (
                  <>
                    <TableCell className="text-center tabular-nums">–</TableCell>
                    <TableCell className="text-center tabular-nums">–</TableCell>
                    <TableCell className="text-center tabular-nums">–</TableCell>
                    <TableCell className="text-center tabular-nums">–</TableCell>
                    <TableCell className="text-center tabular-nums">–</TableCell>
                  </>
                ) : (
                  <TableCell colSpan={5} className="text-center">
                    <DateSummary
                      date={date}
                      selfView={selfView}
                      timeZoneId={policy?.timeZoneId}
                    />
                  </TableCell>
                )}
              </TableRow>,

              isOpen ? (
                <TableRow key={`${date.date}-employees`}>
                  <TableCell colSpan={8} className="bg-muted/30 p-0">
                    <EmployeeTable
                      employees={date.employees}
                      timeZoneId={policy?.timeZoneId}
                      onViewDetails={onViewDetails}
                    />
                  </TableCell>
                </TableRow>
              ) : null,
            ];
          })}
        </TableBody>
      </Table>
    </div>
  );
}

/**
 * The merged cell for every date that is not settled — settled dates get the five count columns
 * instead (see `settled` above). Only three cases reach here: nothing to report yet, Staff's own
 * day, and today in progress.
 */
function DateSummary({
  date,
  selfView,
  timeZoneId,
}: {
  date: AttendanceCalendarDate;
  selfView: boolean;
  timeZoneId: string | undefined;
}) {
  const t = useTranslations('attendance');
  const dateLocale = useDateLocale();

  if (date.isFuture || date.employees.length === 0) {
    return <span className="text-sm text-muted-foreground">{t('summary.nothingYet')}</span>;
  }

  // Scoped to one person, a count is always one — so report that person's day instead.
  if (selfView) {
    const own = date.employees[0]!;
    return (
      <div className="flex items-center justify-center gap-2">
        {(own.tapInUtc || own.tapOutUtc) && (
          <span className="text-sm tabular-nums text-muted-foreground">
            {formatTime(own.tapInUtc, timeZoneId, dateLocale)} – {formatTime(own.tapOutUtc, timeZoneId, dateLocale)}
          </span>
        )}
        <Badge variant={STATUS_VARIANT[own.status]}>{t(`status.${own.status}`)}</Badge>
        {date.isInProgress && (
          <span className="text-xs text-muted-foreground">{t('summary.inProgress')}</span>
        )}
      </div>
    );
  }

  // Today is still moving on its own: report arrivals, not verdicts. The only case left, since
  // a settled date never renders this component.
  const notInYet = countBy(date, 'NotInYet');
  return (
    <div className="flex items-center justify-center gap-2 text-sm">
      <span>{t('summary.clockedIn', { count: countBy(date, 'ClockedIn') })}</span>
      {notInYet > 0 && (
        <span className="text-muted-foreground">· {t('summary.notInYet', { count: notInYet })}</span>
      )}
      <span className="text-xs text-muted-foreground">{t('summary.inProgress')}</span>
    </div>
  );
}

/**
 * Already sorted problems-first by the server. Times sit next to the badge because `Incomplete`
 * covers two different failures — arrived late, and never clocked out — and only the times
 * tell them apart.
 */
function EmployeeTable({
  employees,
  timeZoneId,
  onViewDetails,
}: {
  employees: AttendanceDayListItem[];
  timeZoneId: string | undefined;
  onViewDetails: (item: AttendanceDayListItem) => void;
}) {
  const t = useTranslations('attendance');
  const dateLocale = useDateLocale();
  const tLeave = useTranslations('leave');

  return (
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead>{t('columns.employee')}</TableHead>
          <TableHead>{t('columns.tapIn')}</TableHead>
          <TableHead>{t('columns.tapOut')}</TableHead>
          <TableHead>{t('columns.status')}</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        {employees.map((employee) => (
          <TableRow
            key={employee.employeeId}
            onClick={() => onViewDetails(employee)}
            className="cursor-pointer"
          >
            <TableCell className="font-medium">{employee.employeeFullName}</TableCell>
            <TableCell className="tabular-nums">{formatTime(employee.tapInUtc, timeZoneId, dateLocale)}</TableCell>
            <TableCell className="tabular-nums">{formatTime(employee.tapOutUtc, timeZoneId, dateLocale)}</TableCell>
            <TableCell>
              <div className="flex items-center gap-1.5">
                <Badge variant={STATUS_VARIANT[employee.status]}>
                  {t(`status.${employee.status}`)}
                </Badge>
                {employee.leaveType && (
                  // Punches outrank leave for Status, but the day stays attributable to the
                  // leave — this badge is the only place a Complete-during-leave day is
                  // visible outside the export.
                  <Badge variant="outline">{tLeave(`type.${employee.leaveType}`)}</Badge>
                )}
              </div>
            </TableCell>
          </TableRow>
        ))}
      </TableBody>
    </Table>
  );
}
