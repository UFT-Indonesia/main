'use client';

import { useEffect, useState } from 'react';
import Link from 'next/link';
import type { Route } from 'next';
import { useTranslations } from 'next-intl';
import { Check, History, Lock, Plus, Trash2, X } from 'lucide-react';
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
import { EmployeePicker } from '@/components/employees/employee-picker';
import { DecideLeaveDialog, LEAVE_TYPES, useFormatLeaveDate, type LeaveDecision } from '@/components/leave/leave-dialogs';
import { useFormatMonth } from '@/components/leave/payroll-correction';
import {
  useAddLeaveDeductionAdjustment,
  useCloseLeaveDeductionMonth,
  useDeleteLeaveDeductionAdjustment,
  useDivisorHistory,
  useLeaveDeductionMonth,
  useSetPayrollDivisor,
} from '@/hooks/use-payroll';
import { useDecideLeaveRequest, useLeaveRequest } from '@/hooks/use-leave';
import { useToast } from '@/hooks/use-toast';
import { useDateLocale } from '@/hooks/use-date-locale';
import { extractApiError } from '@/lib/api/client';
import { APP_TIME_ZONE } from '@/lib/constants';
import { formatIdr } from '@/lib/utils';
import type {
  CloseBlocker,
  LeaveDeductionDay,
  LeaveDeductionEmployeeRow,
  LeaveDeductionMonth,
} from '@/lib/api/types';

const MONTHS_BACK = 12;
/** A month has at most 31 days; the server refuses anything above. */
const MAX_DIVISOR = 31;

/** "YYYY-MM-01" of the month `count` months before the one holding `ymd`. */
function monthBefore(ymd: string, count: number): string {
  const [year = 0, month = 1] = ymd.split('-').map(Number);
  const index = year * 12 + (month - 1) - count;
  return `${Math.floor(index / 12)}-${String((index % 12) + 1).padStart(2, '0')}-01`;
}

/** A row's net: a deduction as "Rp X", a refund (negative) as "Refund Rp X" — never a bare minus (Q14). */
function NetAmount({ amount, className }: { amount: number; className?: string }) {
  const t = useTranslations('potonganCuti');
  if (amount < 0) {
    return <span className={`text-success ${className ?? ''}`}>{t('refund', { amount: formatIdr(-amount) })}</span>;
  }
  return <span className={className}>{formatIdr(amount)}</span>;
}

export default function PotonganCutiPage() {
  const t = useTranslations('potonganCuti');
  const tLeave = useTranslations('leave');
  const toast = useToast();
  const formatDate = useFormatLeaveDate();
  const formatMonth = useFormatMonth();

  // The server's "today" is Jakarta's; the browser's UTC date is a day behind it until 07:00 WIB.
  const thisMonth = `${todayInZone(APP_TIME_ZONE).slice(0, 7)}-01`;
  const [month, setMonth] = useState(thisMonth);
  const { data, isLoading, error } = useLeaveDeductionMonth(month);

  const [closing, setClosing] = useState(false);
  const [detailId, setDetailId] = useState<string | null>(null);
  const [adding, setAdding] = useState(false);
  const [deciding, setDeciding] = useState<{ id: string; action: LeaveDecision } | null>(null);

  const closeMutation = useCloseLeaveDeductionMonth();
  const decideMutation = useDecideLeaveRequest();
  const decidingRequest = useLeaveRequest(deciding?.id);
  const options = Array.from({ length: MONTHS_BACK + 1 }, (_, i) => monthBefore(thisMonth, i));

  // Follow the live row so the dialog updates after an adjustment is added or removed.
  const detail = data?.rows.find((row) => row.employeeId === detailId) ?? null;

  return (
    <AppShell>
      <div className="space-y-4">
        <header className="flex flex-wrap items-start justify-between gap-3">
          <div>
            <h1 className="text-2xl font-semibold tracking-tight">{t('title')}</h1>
            <p className="text-sm text-muted-foreground">{t('subtitle')}</p>
          </div>
          <div className="flex flex-col items-end gap-1">
            <div className="flex items-center gap-2">
              <Select className="w-52" value={month} onChange={(e) => setMonth(e.target.value)} aria-label={t('month')}>
                {options.map((option) => (
                  <option key={option} value={option}>{formatMonth(option)}</option>
                ))}
              </Select>
              {data && !data.closed && (
                <Button
                  onClick={() => setClosing(true)}
                  disabled={!data.canClose}
                  aria-describedby={data.canClose ? undefined : 'close-blockers'}
                >
                  <Lock className="h-4 w-4" />
                  {t('close.button')}
                </Button>
              )}
            </div>
            {data && !data.closed && data.closeBlockers.length > 0 && (
              <CloseBlockerList id="close-blockers" blockers={data.closeBlockers} month={data.month} />
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
              <span className="ml-auto flex flex-wrap gap-x-4 font-semibold tabular-nums">
                <span>{t('cutsTotal', { amount: formatIdr(data.cutsTotal) })}</span>
                {data.refundsTotal > 0 && (
                  <span className="text-success">{t('refundsTotal', { amount: formatIdr(data.refundsTotal) })}</span>
                )}
              </span>
            </div>

            {data.closed ? (
              <p className="text-sm text-muted-foreground">{t('divisor.closedWith', { divisor: data.divisor })}</p>
            ) : (
              <DivisorField data={data} />
            )}

            {data.month < data.firstMonth && (
              <p className="text-sm text-muted-foreground">{t('beforeLaunch')}</p>
            )}

            {!data.closed && data.month >= data.firstMonth && (
              <div className="flex justify-end">
                <Button variant="outline" size="sm" onClick={() => setAdding(true)}>
                  <Plus className="h-4 w-4" />
                  {t('adjustment.add')}
                </Button>
              </div>
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
                        onClick={() => setDetailId(row.employeeId)}
                        onKeyDown={(e) => {
                          if (e.key === 'Enter' || e.key === ' ') {
                            e.preventDefault();
                            setDetailId(row.employeeId);
                          }
                        }}
                      >
                        <TableCell className="font-medium">
                          {row.fullName}
                          {(row.corrections.length > 0 || row.adjustments.length > 0) && (
                            <div className="text-xs font-normal text-muted-foreground">
                              {[
                                row.corrections.length > 0 && t('row.corrections', { count: row.corrections.length }),
                                row.adjustments.length > 0 && t('row.adjustments', { count: row.adjustments.length }),
                              ].filter(Boolean).join(' · ')}
                            </div>
                          )}
                        </TableCell>
                        {LEAVE_TYPES.map((type) => (
                          <TableCell key={type} className="text-right tabular-nums">{row.cutDaysByType[type] ?? '—'}</TableCell>
                        ))}
                        <TableCell className="text-right tabular-nums">{row.cutDays}</TableCell>
                        <TableCell className="text-right font-semibold tabular-nums">
                          <NetAmount amount={row.total} />
                        </TableCell>
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
                          <TableCell className="text-right">
                            {/* Decide it right here instead of hunting for it on the Leave page (Q7A). */}
                            <div className="flex justify-end gap-1">
                              <Button
                                variant="ghost"
                                size="sm"
                                onClick={() => setDeciding({ id: item.leaveRequestId, action: 'approve' })}
                              >
                                <Check className="h-4 w-4 text-success" />
                                {tLeave('decide.approve.confirm')}
                              </Button>
                              <Button
                                variant="ghost"
                                size="sm"
                                onClick={() => setDeciding({ id: item.leaveRequestId, action: 'deny' })}
                              >
                                <X className="h-4 w-4 text-destructive" />
                                {tLeave('decide.deny.confirm')}
                              </Button>
                            </div>
                          </TableCell>
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
        description={t('close.description', { month: formatMonth(month) })}
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

      <DecideLeaveDialog
        request={deciding ? decidingRequest.data ?? null : null}
        action={deciding?.action ?? null}
        onOpenChange={(o) => { if (!o) setDeciding(null); }}
        submitting={decideMutation.isPending}
        onConfirm={async (note) => {
          if (!deciding) return;
          try {
            await decideMutation.mutateAsync({ id: deciding.id, action: deciding.action, note });
            toast.success(tLeave(`decide.${deciding.action}.successTitle`));
            setDeciding(null);
          } catch (err) {
            toast.error(tLeave(`decide.${deciding.action}.errorTitle`), extractApiError(err).message);
          }
        }}
      />

      {adding && data && <AddAdjustmentDialog month={data.month} onClose={() => setAdding(false)} />}

      {detail && data && (
        <EmployeeDaysDialog
          row={detail}
          month={data}
          onClose={() => setDetailId(null)}
          onViewMonth={(source) => {
            setDetailId(null);
            setMonth(source);
          }}
        />
      )}
    </AppShell>
  );
}

/** Says exactly why Close is disabled, in the order to fix it (Q7C). The server decides; this only renders. */
function CloseBlockerList({ id, blockers, month }: { id: string; blockers: CloseBlocker[]; month: string }) {
  const t = useTranslations('potonganCuti.blockers');
  const formatMonth = useFormatMonth();
  return (
    <ul id={id} className="max-w-sm space-y-0.5 text-right text-xs text-muted-foreground">
      {blockers.map((blocker) => (
        <li key={blocker.code}>
          {t(blocker.code, {
            count: blocker.count ?? 0,
            month: formatMonth(blocker.month ?? month),
          })}
        </li>
      ))}
    </ul>
  );
}

/** Company-wide divisor. Changing it reprices open months; closed ones keep their frozen figures. */
function DivisorField({ data }: { data: LeaveDeductionMonth }) {
  const t = useTranslations('potonganCuti.divisor');
  const toast = useToast();
  const dateLocale = useDateLocale();
  const [value, setValue] = useState(String(data.divisor));
  const [confirming, setConfirming] = useState(false);
  const [historyOpen, setHistoryOpen] = useState(false);
  const mutation = useSetPayrollDivisor();
  const dateFormatter = new Intl.DateTimeFormat(dateLocale, { dateStyle: 'medium' });

  // Follow the server's value after a save or a refetch.
  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setValue(String(data.divisor));
  }, [data.divisor]);

  const parsed = Number(value);
  const valid = Number.isInteger(parsed) && parsed >= 1 && parsed <= MAX_DIVISOR;
  const last = data.lastDivisorChange;

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
      <div className="max-w-md space-y-1 text-xs text-muted-foreground">
        <p>{t('hint')}</p>
        {last && (
          <p>
            {t('lastChange', {
              from: last.oldDivisor,
              to: last.newDivisor,
              name: last.byName,
              at: dateFormatter.format(new Date(last.atUtc)),
            })}{' '}
            <button
              type="button"
              className="inline-flex items-center gap-1 font-medium text-foreground underline-offset-4 hover:underline"
              onClick={() => setHistoryOpen(true)}
            >
              <History className="h-3 w-3" aria-hidden />
              {t('history')}
            </button>
          </p>
        )}
      </div>

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

      {historyOpen && <DivisorHistoryDialog onClose={() => setHistoryOpen(false)} />}
    </div>
  );
}

function DivisorHistoryDialog({ onClose }: { onClose: () => void }) {
  const t = useTranslations('potonganCuti.divisor');
  const tCommon = useTranslations('common');
  const history = useDivisorHistory(true);
  const dateFormatter = new Intl.DateTimeFormat(useDateLocale(), { dateStyle: 'medium', timeStyle: 'short' });

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogHeader>
        <DialogTitle>{t('historyTitle')}</DialogTitle>
        <DialogDescription>{t('historyDescription')}</DialogDescription>
      </DialogHeader>
      <div className="mt-4 max-h-80 overflow-auto rounded-lg border border-border">
        {history.isLoading ? (
          <Skeleton className="h-24 w-full" />
        ) : (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>{t('columns.change')}</TableHead>
                <TableHead>{t('columns.by')}</TableHead>
                <TableHead>{t('columns.at')}</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {(history.data ?? []).map((change) => (
                <TableRow key={change.atUtc}>
                  <TableCell className="tabular-nums">{change.oldDivisor} → {change.newDivisor}</TableCell>
                  <TableCell>{change.byName}</TableCell>
                  <TableCell className="tabular-nums">{dateFormatter.format(new Date(change.atUtc))}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}
      </div>
      <DialogFooter>
        <Button variant="outline" onClick={onClose}>{tCommon('close')}</Button>
      </DialogFooter>
    </Dialog>
  );
}

/** Owner's manual line on an open month (Q12/Q15): refund or extra deduction, with a required reason. */
function AddAdjustmentDialog({ month, onClose }: { month: string; onClose: () => void }) {
  const t = useTranslations('potonganCuti.adjustment');
  const tCommon = useTranslations('common');
  const toast = useToast();
  const formatMonth = useFormatMonth();
  const mutation = useAddLeaveDeductionAdjustment();

  const [employeeId, setEmployeeId] = useState('');
  const [direction, setDirection] = useState<'refund' | 'deduct'>('refund');
  const [amount, setAmount] = useState('');
  const [reason, setReason] = useState('');

  const parsed = Number(amount);
  const valid = employeeId !== '' && Number.isInteger(parsed) && parsed > 0 && reason.trim() !== '';

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogHeader>
        <DialogTitle>{t('title', { month: formatMonth(month) })}</DialogTitle>
        <DialogDescription>{t('description')}</DialogDescription>
      </DialogHeader>
      <div className="mt-4 space-y-3">
        <div className="flex flex-col gap-1.5">
          <Label>{t('employee')}</Label>
          <EmployeePicker value={employeeId} onChange={setEmployeeId} />
        </div>
        <div className="grid grid-cols-2 gap-3">
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="adjustment-direction">{t('direction')}</Label>
            <Select
              id="adjustment-direction"
              value={direction}
              onChange={(e) => setDirection(e.target.value as 'refund' | 'deduct')}
            >
              <option value="refund">{t('refund')}</option>
              <option value="deduct">{t('deduct')}</option>
            </Select>
          </div>
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="adjustment-amount">{t('amount')}</Label>
            <Input
              id="adjustment-amount"
              type="number"
              min="1"
              step="1000"
              value={amount}
              onChange={(e) => setAmount(e.target.value)}
            />
          </div>
        </div>
        <div className="flex flex-col gap-1.5">
          <Label htmlFor="adjustment-reason">{t('reason')}</Label>
          <Input
            id="adjustment-reason"
            value={reason}
            maxLength={1000}
            onChange={(e) => setReason(e.target.value)}
            placeholder={t('reasonPlaceholder')}
          />
        </div>
        <p className="text-xs text-muted-foreground">{t('hint')}</p>
      </div>
      <DialogFooter>
        <Button variant="outline" onClick={onClose} disabled={mutation.isPending}>{tCommon('cancel')}</Button>
        <Button
          disabled={!valid || mutation.isPending}
          onClick={async () => {
            try {
              await mutation.mutateAsync({
                month,
                employeeId,
                amount: direction === 'refund' ? -parsed : parsed,
                reason: reason.trim(),
              });
              toast.success(t('successTitle'));
              onClose();
            } catch (err) {
              toast.error(t('errorTitle'), extractApiError(err).message);
            }
          }}
        >
          {mutation.isPending ? tCommon('loading') : t('save')}
        </Button>
      </DialogFooter>
    </Dialog>
  );
}

/** What happened to a closed-month day after close, if anything (Q17). */
function DayMarker({ day }: { day: LeaveDeductionDay }) {
  const t = useTranslations('potonganCuti.marker');
  const formatMonth = useFormatMonth();
  const dateFormatter = new Intl.DateTimeFormat(useDateLocale(), { dateStyle: 'medium' });

  if (day.supersededAtUtc) {
    return (
      <div className="text-xs text-muted-foreground">
        {t(day.supersededTargetMonth ? 'correctedMoved' : 'corrected', {
          name: day.supersededByName ?? '–',
          at: dateFormatter.format(new Date(day.supersededAtUtc)),
          month: day.supersededTargetMonth ? formatMonth(day.supersededTargetMonth) : '',
        })}
      </div>
    );
  }
  if (!day.paidAtClose) {
    return (
      <div className="text-xs text-muted-foreground">
        {day.lateTargetMonth ? t('addedCharged', { month: formatMonth(day.lateTargetMonth) }) : t('added')}
      </div>
    );
  }
  return null;
}

/** The day-by-day behind one employee's row, plus late corrections and adjustments landing in this month. */
function EmployeeDaysDialog({
  row,
  month,
  onClose,
  onViewMonth,
}: {
  row: LeaveDeductionEmployeeRow;
  month: LeaveDeductionMonth;
  onClose: () => void;
  /** Jumps the page to the closed month a late correction came from. */
  onViewMonth: (month: string) => void;
}) {
  const t = useTranslations('potonganCuti');
  const tLeave = useTranslations('leave');
  const tCommon = useTranslations('common');
  const formatDate = useFormatLeaveDate();
  const formatMonth = useFormatMonth();
  const toast = useToast();
  const dateFormatter = new Intl.DateTimeFormat(useDateLocale(), { dateStyle: 'medium' });
  const remove = useDeleteLeaveDeductionAdjustment();

  // A day that wasn't paid at close, or was corrected since, reads as history, not as this month's money.
  const counted = (day: LeaveDeductionDay) => day.paidAtClose;

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()} className="max-w-4xl">
      <DialogHeader>
        <DialogTitle>{row.fullName}</DialogTitle>
        <DialogDescription>{t('detail.description')}</DialogDescription>
      </DialogHeader>

      <div className="mt-4 max-h-[28rem] space-y-4 overflow-auto">
        {row.days.length > 0 && (
          <div className="rounded-lg border border-border">
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
                  <TableRow key={`${day.leaveRequestId}-${day.date}-${day.supersededAtUtc ?? ''}`}>
                    <TableCell className="tabular-nums">
                      {formatDate(day.date)}
                      <DayMarker day={day} />
                    </TableCell>
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
                    <TableCell className={`text-right tabular-nums ${counted(day) ? '' : 'text-muted-foreground line-through'}`}>
                      {day.cutDays > 0 ? formatIdr(day.amount) : '—'}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </div>
        )}

        {row.corrections.length > 0 && (
          <section className="space-y-2">
            <h3 className="text-sm font-semibold">{t('corrections.title')}</h3>
            <ul className="space-y-2">
              {row.corrections.map((c) => (
                <li key={c.id} className="rounded-lg border border-border px-3 py-2 text-sm">
                  <div className="flex flex-wrap justify-between gap-2">
                    <span>
                      {t('corrections.line', {
                        month: formatMonth(c.sourceMonth),
                        date: formatDate(c.date),
                        type: tLeave(`type.${c.leaveType}`),
                      })}
                    </span>
                    <NetAmount amount={c.amount} className="font-semibold tabular-nums" />
                  </div>
                  <div className="text-xs text-muted-foreground">
                    “{c.reason}” — {t('corrections.by', { name: c.byName, at: dateFormatter.format(new Date(c.atUtc)) })}{' '}
                    <button
                      type="button"
                      className="font-medium text-foreground underline-offset-4 hover:underline"
                      onClick={() => onViewMonth(c.sourceMonth)}
                    >
                      {t('corrections.view', { month: formatMonth(c.sourceMonth) })}
                    </button>
                  </div>
                </li>
              ))}
            </ul>
          </section>
        )}

        {row.adjustments.length > 0 && (
          <section className="space-y-2">
            <h3 className="text-sm font-semibold">{t('adjustment.listTitle')}</h3>
            <ul className="space-y-2">
              {row.adjustments.map((a) => (
                <li key={a.id} className="flex items-start justify-between gap-2 rounded-lg border border-border px-3 py-2 text-sm">
                  <div>
                    <NetAmount amount={a.amount} className="font-semibold tabular-nums" />
                    <div className="text-xs text-muted-foreground">
                      “{a.reason}” — {t('corrections.by', { name: a.byName, at: dateFormatter.format(new Date(a.atUtc)) })}
                    </div>
                  </div>
                  {!month.closed && (
                    <Button
                      variant="ghost"
                      size="icon"
                      aria-label={t('adjustment.delete')}
                      title={t('adjustment.delete')}
                      disabled={remove.isPending}
                      onClick={async () => {
                        try {
                          await remove.mutateAsync(a.id);
                          toast.success(t('adjustment.deletedTitle'));
                        } catch (err) {
                          toast.error(t('adjustment.errorTitle'), extractApiError(err).message);
                        }
                      }}
                    >
                      <Trash2 className="h-4 w-4 text-destructive" />
                    </Button>
                  )}
                </li>
              ))}
            </ul>
          </section>
        )}
      </div>

      <div className="mt-3 flex justify-end text-sm font-semibold">
        {t('detail.total')}:&nbsp;<NetAmount amount={row.total} className="tabular-nums" />
      </div>

      <DialogFooter>
        {row.days.some((d) => d.supersededAtUtc || !d.paidAtClose) && (
          <Link href={'/leave' as Route} className="mr-auto self-center text-xs text-muted-foreground underline-offset-4 hover:underline">
            {t('detail.leaveLink')}
          </Link>
        )}
        <Button variant="outline" onClick={onClose}>{tCommon('close')}</Button>
      </DialogFooter>
    </Dialog>
  );
}
