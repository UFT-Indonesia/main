'use client';

import { useEffect, useState } from 'react';
import { useLocale, useTranslations } from 'next-intl';
import { Lock } from 'lucide-react';
import { AppShell } from '@/components/layout/app-shell';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select } from '@/components/ui/select';
import { Skeleton } from '@/components/ui/skeleton';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Dialog, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { todayInZone } from '@/components/ui/date-picker';
import { NoteDialog } from '@/components/overtime/overtime-dialogs';
import { LEAVE_TYPES, useFormatLeaveDate } from '@/components/leave/leave-dialogs';
import {
  useCloseLeaveDeductionMonth,
  useLeaveDeductionMonth,
  useSetPayrollDivisor,
} from '@/hooks/use-payroll';
import { useToast } from '@/hooks/use-toast';
import { extractApiError } from '@/lib/api/client';
import { APP_TIME_ZONE } from '@/lib/constants';
import { formatIdr } from '@/lib/utils';
import type { LeaveDeductionEmployeeRow, LeaveDeductionMonth } from '@/lib/api/types';

const MONTHS_BACK = 12;
/** A month has at most 31 days; the server refuses anything above. */
const MAX_DIVISOR = 31;

/** "YYYY-MM-01" of the month `count` months before the one holding `ymd`. */
function monthBefore(ymd: string, count: number): string {
  const [year = 0, month = 1] = ymd.split('-').map(Number);
  const index = year * 12 + (month - 1) - count;
  return `${Math.floor(index / 12)}-${String((index % 12) + 1).padStart(2, '0')}-01`;
}

function monthLabel(ymd: string, locale: string): string {
  return new Intl.DateTimeFormat(locale, { month: 'long', year: 'numeric', timeZone: 'UTC' })
    .format(new Date(`${ymd}T00:00:00Z`));
}

export default function PotonganCutiPage() {
  const t = useTranslations('potonganCuti');
  const tLeave = useTranslations('leave');
  const locale = useLocale();
  const toast = useToast();
  const formatDate = useFormatLeaveDate();

  // The server's "today" is Jakarta's; the browser's UTC date is a day behind it until 07:00 WIB.
  const thisMonth = `${todayInZone(APP_TIME_ZONE).slice(0, 7)}-01`;
  const [month, setMonth] = useState(thisMonth);
  const { data, isLoading, error } = useLeaveDeductionMonth(month);

  const [closing, setClosing] = useState(false);
  const [detail, setDetail] = useState<LeaveDeductionEmployeeRow | null>(null);

  const closeMutation = useCloseLeaveDeductionMonth();
  const options = Array.from({ length: MONTHS_BACK + 1 }, (_, i) => monthBefore(thisMonth, i));

  return (
    <AppShell>
      <div className="space-y-4">
        <header className="flex flex-wrap items-start justify-between gap-3">
          <div>
            <h1 className="text-2xl font-semibold tracking-tight">{t('title')}</h1>
            <p className="text-sm text-muted-foreground">{t('subtitle')}</p>
          </div>
          <div className="flex items-center gap-2">
            <Select className="w-52" value={month} onChange={(e) => setMonth(e.target.value)} aria-label={t('month')}>
              {options.map((option) => (
                <option key={option} value={option}>{monthLabel(option, locale)}</option>
              ))}
            </Select>
            {data && !data.closed && (
              <Button onClick={() => setClosing(true)} disabled={!data.canClose} title={data.canClose ? undefined : t('cannotClose')}>
                <Lock className="h-4 w-4" />
                {t('close.button')}
              </Button>
            )}
          </div>
        </header>

        {error ? (
          <div className="rounded-lg border border-destructive/40 bg-destructive/10 p-4 text-sm text-destructive">
            {extractApiError(error).message}
          </div>
        ) : isLoading || !data ? (
          <div className="space-y-2">
            {Array.from({ length: 4 }).map((_, i) => <Skeleton key={i} className="h-12 w-full" />)}
          </div>
        ) : (
          <>
            <div className="flex flex-wrap items-center gap-3 text-sm">
              <Badge variant={data.closed ? 'secondary' : 'success'}>{data.closed ? t('closed') : t('open')}</Badge>
              {data.closed && data.closedByName && (
                <span className="text-muted-foreground">{t('closedBy', { name: data.closedByName })}</span>
              )}
              {!data.closed && <span className="text-muted-foreground">{t('estimate')}</span>}
              <span className="ml-auto font-semibold tabular-nums">{t('total')}: {formatIdr(data.total)}</span>
            </div>

            <DivisorField data={data} />

            {data.month < data.firstMonth && (
              <p className="text-sm text-muted-foreground">{t('beforeLaunch')}</p>
            )}

            {data.rows.length === 0 ? (
              <div className="rounded-lg border border-dashed border-border p-8 text-center text-sm text-muted-foreground">
                {t('empty')}
              </div>
            ) : (
              <div className="rounded-lg border border-border bg-card">
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>{t('columns.employee')}</TableHead>
                      {LEAVE_TYPES.map((type) => (
                        <TableHead key={type} className="text-right">{tLeave(`type.${type}`)}</TableHead>
                      ))}
                      <TableHead className="text-right">{t('columns.cutDays')}</TableHead>
                      <TableHead className="text-right">{t('columns.total')}</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {data.rows.map((row) => (
                      <TableRow
                        key={row.employeeId}
                        className="cursor-pointer"
                        tabIndex={0}
                        role="button"
                        aria-label={t('detail.open', { name: row.fullName })}
                        onClick={() => setDetail(row)}
                        onKeyDown={(e) => {
                          if (e.key === 'Enter' || e.key === ' ') {
                            e.preventDefault();
                            setDetail(row);
                          }
                        }}
                      >
                        <TableCell className="font-medium">{row.fullName}</TableCell>
                        {LEAVE_TYPES.map((type) => (
                          <TableCell key={type} className="text-right tabular-nums">{row.cutDaysByType[type] ?? '—'}</TableCell>
                        ))}
                        <TableCell className="text-right tabular-nums">{row.cutDays}</TableCell>
                        <TableCell className="text-right font-semibold tabular-nums">{formatIdr(row.total)}</TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </div>
            )}

            {data.pending.length > 0 && (
              <section className="space-y-2">
                <div>
                  <h2 className="text-lg font-semibold">{t('pending.title')}</h2>
                  <p className="text-sm text-muted-foreground">{t('pending.description')}</p>
                </div>
                <div className="rounded-lg border border-border bg-card">
                  <Table>
                    <TableBody>
                      {data.pending.map((item) => (
                        <TableRow key={item.leaveRequestId}>
                          <TableCell className="font-medium">{item.fullName}</TableCell>
                          <TableCell>{tLeave(`type.${item.leaveType}`)}</TableCell>
                          <TableCell className="tabular-nums">{formatDate(item.start)} – {formatDate(item.end)}</TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                </div>
              </section>
            )}
          </>
        )}
      </div>

      <NoteDialog
        open={closing}
        onOpenChange={setClosing}
        title={t('close.title')}
        description={t('close.description', { month: monthLabel(month, locale) })}
        confirmLabel={t('close.confirm')}
        destructive
        submitting={closeMutation.isPending}
        onConfirm={async () => {
          try {
            await closeMutation.mutateAsync(month);
            toast.success(t('close.successTitle'), t('close.successDescription'));
            setClosing(false);
          } catch (err) {
            toast.error(t('close.errorTitle'), extractApiError(err).message);
          }
        }}
      />

      {detail && <EmployeeDaysDialog row={detail} onClose={() => setDetail(null)} />}
    </AppShell>
  );
}

/** Company-wide divisor. Changing it reprices open months; closed ones keep their frozen figures. */
function DivisorField({ data }: { data: LeaveDeductionMonth }) {
  const t = useTranslations('potonganCuti.divisor');
  const toast = useToast();
  const [value, setValue] = useState(String(data.divisor));
  const [confirming, setConfirming] = useState(false);
  const mutation = useSetPayrollDivisor();

  // Follow the server's value after a save or a refetch.
  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setValue(String(data.divisor));
  }, [data.divisor]);

  const parsed = Number(value);
  const valid = Number.isInteger(parsed) && parsed >= 1 && parsed <= MAX_DIVISOR;

  return (
    <div className="flex flex-wrap items-end gap-3 rounded-lg border border-border bg-card p-3">
      <div className="flex flex-col gap-1.5">
        <Label htmlFor="payroll-divisor">{t('label')}</Label>
        <Input
          id="payroll-divisor"
          type="number"
          min="1"
          max={MAX_DIVISOR}
          step="1"
          className="w-24"
          value={value}
          disabled={mutation.isPending}
          onChange={(e) => setValue(e.target.value)}
        />
      </div>
      <Button
        variant="outline"
        disabled={!valid || parsed === data.divisor || mutation.isPending}
        onClick={() => setConfirming(true)}
      >
        {t('save')}
      </Button>
      <p className="max-w-md text-xs text-muted-foreground">{t('hint')}</p>

      {/* One click reprices every open month for everyone, so it asks first. */}
      <NoteDialog
        open={confirming}
        onOpenChange={setConfirming}
        title={t('confirmTitle')}
        description={t('confirmDescription', { from: data.divisor, to: parsed })}
        confirmLabel={t('save')}
        submitting={mutation.isPending}
        onConfirm={async () => {
          try {
            await mutation.mutateAsync(parsed);
            toast.success(t('successTitle'));
            setConfirming(false);
          } catch (err) {
            toast.error(t('errorTitle'), extractApiError(err).message);
          }
        }}
      />
    </div>
  );
}

/** The day-by-day behind one employee's row: free or cut, the salary used, the rate, the amount. */
function EmployeeDaysDialog({ row, onClose }: { row: LeaveDeductionEmployeeRow; onClose: () => void }) {
  const t = useTranslations('potonganCuti');
  const tLeave = useTranslations('leave');
  const tCommon = useTranslations('common');
  const formatDate = useFormatLeaveDate();

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()} className="max-w-4xl">
      <DialogHeader>
        <DialogTitle>{row.fullName}</DialogTitle>
        <DialogDescription>{t('detail.description')}</DialogDescription>
      </DialogHeader>
      <div className="mt-4 max-h-96 overflow-auto rounded-lg border border-border">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>{t('columns.date')}</TableHead>
              <TableHead>{t('columns.type')}</TableHead>
              <TableHead>{t('columns.request')}</TableHead>
              <TableHead>{t('columns.status')}</TableHead>
              <TableHead className="text-right">{t('columns.salary')}</TableHead>
              <TableHead className="text-right">{t('columns.rate')}</TableHead>
              <TableHead className="text-right">{t('columns.amount')}</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {row.days.map((day) => (
              <TableRow key={`${day.leaveRequestId}-${day.date}`}>
                <TableCell className="tabular-nums">{formatDate(day.date)}</TableCell>
                <TableCell>{tLeave(`type.${day.leaveType}`)}</TableCell>
                <TableCell className="tabular-nums">{formatDate(day.requestStart)} – {formatDate(day.requestEnd)}</TableCell>
                <TableCell>
                  {day.cutDays === 0 ? (
                    <Badge variant="success">{t('free')}</Badge>
                  ) : day.freeDays === 0 ? (
                    <Badge variant="warning">{t('cut')}</Badge>
                  ) : (
                    <Badge variant="yellow">{t('partial', { free: day.freeDays, cut: day.cutDays })}</Badge>
                  )}
                </TableCell>
                <TableCell className="text-right tabular-nums">{day.cutDays > 0 ? formatIdr(day.salary) : '—'}</TableCell>
                <TableCell className="text-right tabular-nums">{day.cutDays > 0 ? formatIdr(day.dailyRate) : '—'}</TableCell>
                <TableCell className="text-right tabular-nums">{day.cutDays > 0 ? formatIdr(day.amount) : '—'}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </div>
      <DialogFooter>
        <Button variant="outline" onClick={onClose}>{tCommon('close')}</Button>
      </DialogFooter>
    </Dialog>
  );
}
