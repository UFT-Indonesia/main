'use client';

import { useState } from 'react';
import { useLocale, useTranslations } from 'next-intl';
import { Check, Lock, Plus, X } from 'lucide-react';
import { AppShell } from '@/components/layout/app-shell';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Select } from '@/components/ui/select';
import { Skeleton } from '@/components/ui/skeleton';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Dialog, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import {
  ApproveRapelDialog,
  NoteDialog,
  OVERTIME_STATUS_VARIANT,
  ProofLink,
  RapelDialog,
  hhmm,
} from '@/components/overtime/overtime-dialogs';
import { periodEnd, periodLabel, periodStartBefore, periodStartOf } from '@/components/overtime/period';
import { useFormatLeaveDate } from '@/components/leave/leave-dialogs';
import {
  useCloseGajiPremiPeriod,
  useCreateRapel,
  useDecideRapel,
  useGajiPremiPeriod,
  useOvertimeList,
} from '@/hooks/use-overtime';
import { useAttendancePolicy } from '@/hooks/use-attendance-settings';
import { useToast } from '@/hooks/use-toast';
import { extractApiError } from '@/lib/api/client';
import { downloadRapelProof } from '@/lib/api/overtime';
import { formatIdr } from '@/lib/utils';
import type { GajiPremiEmployeeRow, GajiPremiPeriod, Rapel } from '@/lib/api/types';

const PERIODS_BACK = 12;

export default function GajiPremiPage() {
  const t = useTranslations('gajiPremi');
  const tOvertime = useTranslations('overtime');
  const locale = useLocale();
  const toast = useToast();
  const formatDate = useFormatLeaveDate();

  const todayStart = periodStartOf(new Date().toISOString().slice(0, 10));
  const [start, setStart] = useState(todayStart);
  const { data, isLoading, error } = useGajiPremiPeriod(start);

  const [closing, setClosing] = useState(false);
  const [adding, setAdding] = useState(false);
  const [attachmentError, setAttachmentError] = useState<string | null>(null);
  const [approving, setApproving] = useState<Rapel | null>(null);
  const [rejecting, setRejecting] = useState<Rapel | null>(null);
  const [detail, setDetail] = useState<GajiPremiEmployeeRow | null>(null);

  const closeMutation = useCloseGajiPremiPeriod();
  const rapelMutation = useCreateRapel();
  const decideRapel = useDecideRapel();

  const options = Array.from({ length: PERIODS_BACK + 1 }, (_, i) => periodStartBefore(todayStart, i));

  return (
    <AppShell>
      <div className="space-y-4">
        <header className="flex flex-wrap items-start justify-between gap-3">
          <div>
            <h1 className="text-2xl font-semibold tracking-tight">{t('title')}</h1>
            <p className="text-sm text-muted-foreground">{t('subtitle')}</p>
          </div>
          <div className="flex items-center gap-2">
            <Select className="w-52" value={start} onChange={(e) => setStart(e.target.value)} aria-label={t('period')}>
              {options.map((option) => (
                <option key={option} value={option}>{periodLabel(option, periodEnd(option), locale)}</option>
              ))}
            </Select>
            {/* A rapel claims a closed period, so there is nothing to add to an open one. */}
            <Button variant="outline" onClick={() => { setAttachmentError(null); setAdding(true); }} disabled={!data?.closed}>
              <Plus className="h-4 w-4" />
              {t('addRapel')}
            </Button>
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
              <span className="text-muted-foreground">{t('payout', { date: formatDate(data.payoutDate) })}</span>
              {data.closed && data.closedByName && (
                <span className="text-muted-foreground">{t('closedBy', { name: data.closedByName })}</span>
              )}
              {!data.closed && <span className="text-muted-foreground">{t('estimate')}</span>}
              <span className="ml-auto font-semibold tabular-nums">{t('total')}: {formatIdr(data.total)}</span>
            </div>

            {data.rows.length === 0 ? (
              <div className="rounded-lg border border-dashed border-border p-8 text-center text-sm text-muted-foreground">
                {t('empty')}
              </div>
            ) : (
              <PeriodTable data={data} locale={locale} onOpen={setDetail} />
            )}

            {data.pendingRapel.length > 0 && (
              <section className="space-y-2">
                <h2 className="text-lg font-semibold">{t('pendingRapel')}</h2>
                <div className="rounded-lg border border-border bg-card">
                  <Table>
                    <TableHeader>
                      <TableRow>
                        <TableHead>{tOvertime('columns.employee')}</TableHead>
                        <TableHead>{tOvertime('rapel.workDate')}</TableHead>
                        <TableHead>{tOvertime('rapel.note')}</TableHead>
                        <TableHead>{tOvertime('rapel.proof')}</TableHead>
                        <TableHead />
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {data.pendingRapel.map((r) => (
                        <TableRow key={r.id}>
                          <TableCell className="font-medium">{r.employeeFullName}</TableCell>
                          <TableCell className="tabular-nums">{formatDate(r.workDate)}</TableCell>
                          <TableCell className="max-w-xs truncate" title={r.note}>{r.note}</TableCell>
                          <TableCell><ProofLink fileName={r.attachmentFileName} fetchProof={() => downloadRapelProof(r.id)} /></TableCell>
                          <TableCell className="text-right">
                            <div className="flex justify-end gap-1">
                              <Button variant="ghost" size="icon" title={tOvertime('rapel.approveTitle')} aria-label={tOvertime('rapel.approveTitle')} onClick={() => setApproving(r)}>
                                <Check className="h-4 w-4 text-success" />
                              </Button>
                              <Button variant="ghost" size="icon" title={tOvertime('rapel.rejectTitle')} aria-label={tOvertime('rapel.rejectTitle')} onClick={() => setRejecting(r)}>
                                <X className="h-4 w-4 text-destructive" />
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
        description={t('close.description', { period: data ? periodLabel(data.start, data.end, locale) : '' })}
        confirmLabel={t('close.confirm')}
        destructive
        submitting={closeMutation.isPending}
        onConfirm={async () => {
          try {
            await closeMutation.mutateAsync(start);
            toast.success(t('close.successTitle'), t('close.successDescription'));
            setClosing(false);
          } catch (err) {
            toast.error(t('close.errorTitle'), extractApiError(err).message);
          }
        }}
      />

      <RapelDialog
        open={adding}
        onOpenChange={setAdding}
        employeeId={null}
        withAmount
        period={data ? { start: data.start, end: data.end } : null}
        submitting={rapelMutation.isPending}
        attachmentError={attachmentError}
        onConfirm={async (body) => {
          setAttachmentError(null);
          try {
            await rapelMutation.mutateAsync(body);
            toast.success(tOvertime('rapel.addedTitle'));
            setAdding(false);
          } catch (err) {
            const apiError = extractApiError(err);
            if (apiError.code && /attachment/.test(apiError.code)) {
              setAttachmentError(apiError.message);
              return;
            }
            toast.error(tOvertime('rapel.errorTitle'), apiError.message);
          }
        }}
      />

      <ApproveRapelDialog
        open={!!approving}
        onOpenChange={(open) => !open && setApproving(null)}
        employeeName={approving?.employeeFullName ?? ''}
        submitting={decideRapel.isPending}
        onConfirm={async (amount) => {
          if (!approving) return;
          try {
            await decideRapel.mutateAsync({ id: approving.id, action: 'approve', amount });
            toast.success(tOvertime('rapel.approvedTitle'));
            setApproving(null);
          } catch (err) {
            toast.error(tOvertime('rapel.errorTitle'), extractApiError(err).message);
          }
        }}
      />

      <NoteDialog
        open={!!rejecting}
        onOpenChange={(open) => !open && setRejecting(null)}
        title={tOvertime('rapel.rejectTitle')}
        description={tOvertime('rapel.rejectDescription', { employee: rejecting?.employeeFullName ?? '' })}
        confirmLabel={tOvertime('rapel.rejectConfirm')}
        destructive
        withNote
        submitting={decideRapel.isPending}
        onConfirm={async (note) => {
          if (!rejecting) return;
          try {
            await decideRapel.mutateAsync({ id: rejecting.id, action: 'reject', note });
            toast.success(tOvertime('rapel.rejectedTitle'));
            setRejecting(null);
          } catch (err) {
            toast.error(tOvertime('rapel.errorTitle'), extractApiError(err).message);
          }
        }}
      />

      {detail && data && <EmployeeDaysDialog row={detail} period={data} onClose={() => setDetail(null)} />}
    </AppShell>
  );
}

function PeriodTable({ data, locale, onOpen }: { data: GajiPremiPeriod; locale: string; onOpen: (row: GajiPremiEmployeeRow) => void }) {
  const t = useTranslations('gajiPremi');
  const month = new Intl.DateTimeFormat(locale, { month: 'short', timeZone: 'UTC' });
  const first = month.format(new Date(`${data.start}T00:00:00Z`));
  const second = month.format(new Date(`${data.end}T00:00:00Z`));

  return (
    <div className="rounded-lg border border-border bg-card">
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead>{t('columns.employee')}</TableHead>
            <TableHead className="text-right">{t('columns.days', { month: first })}</TableHead>
            <TableHead className="text-right">{t('columns.hours', { month: first })}</TableHead>
            <TableHead className="text-right">{t('columns.days', { month: second })}</TableHead>
            <TableHead className="text-right">{t('columns.hours', { month: second })}</TableHead>
            <TableHead className="text-right">{t('columns.rapel')}</TableHead>
            <TableHead className="text-right">{t('columns.total')}</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {data.rows.map((row) => (
            <TableRow key={row.employeeId} className="cursor-pointer" onClick={() => onOpen(row)}>
              <TableCell className="font-medium">{row.fullName}</TableCell>
              <TableCell className="text-right tabular-nums">{row.days1}</TableCell>
              <TableCell className="text-right tabular-nums">{row.hours1}</TableCell>
              <TableCell className="text-right tabular-nums">{row.days2}</TableCell>
              <TableCell className="text-right tabular-nums">{row.hours2}</TableCell>
              <TableCell className="text-right tabular-nums">{row.rapelAmount ? formatIdr(row.rapelAmount) : '—'}</TableCell>
              <TableCell className="text-right font-semibold tabular-nums">{formatIdr(row.total)}</TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </div>
  );
}

/** The day-by-day behind one employee's row: window, punches, counted hours, tier, status. */
function EmployeeDaysDialog({ row, period, onClose }: { row: GajiPremiEmployeeRow; period: GajiPremiPeriod; onClose: () => void }) {
  const t = useTranslations('gajiPremi');
  const tOvertime = useTranslations('overtime');
  const tCommon = useTranslations('common');
  const formatDate = useFormatLeaveDate();
  const { data, isLoading } = useOvertimeList({ from: period.start, to: period.end, employeeId: row.employeeId, pageSize: 100 });
  const zone = useAttendancePolicy().data?.timeZoneId ?? 'Asia/Jakarta';
  const clock = new Intl.DateTimeFormat('en-GB', { hour: '2-digit', minute: '2-digit', hour12: false, timeZone: zone });
  const at = (iso: string | null) => (iso ? clock.format(new Date(iso)) : '—');

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()} className="max-w-3xl">
      <DialogHeader>
        <DialogTitle>{row.fullName}</DialogTitle>
        <DialogDescription>{t('detail.description')}</DialogDescription>
      </DialogHeader>
      <div className="mt-4 max-h-96 overflow-auto rounded-lg border border-border">
        {isLoading ? (
          <Skeleton className="h-24 w-full" />
        ) : (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>{tOvertime('columns.date')}</TableHead>
                <TableHead>{tOvertime('columns.window')}</TableHead>
                <TableHead>{tOvertime('columns.punches')}</TableHead>
                <TableHead className="text-right">{tOvertime('columns.hours')}</TableHead>
                <TableHead className="text-right">{tOvertime('columns.pay')}</TableHead>
                <TableHead>{tOvertime('columns.status')}</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {data?.items.map((item) => (
                <TableRow key={item.id}>
                  <TableCell className="tabular-nums">{formatDate(item.date)}</TableCell>
                  <TableCell className="tabular-nums">{hhmm(item.startTime)} – {hhmm(item.endTime)}{item.endsNextDay ? ' (+1)' : ''}</TableCell>
                  <TableCell className="tabular-nums">{at(item.tapInUtc)} – {at(item.tapOutUtc)}</TableCell>
                  <TableCell className="text-right tabular-nums">{item.hours}</TableCell>
                  <TableCell className="text-right tabular-nums">{formatIdr(item.amount ?? 0)}</TableCell>
                  <TableCell>
                    <div className="flex flex-wrap gap-1">
                      <Badge variant={OVERTIME_STATUS_VARIANT[item.status]}>{tOvertime(`status.${item.status}`)}</Badge>
                      {item.incomplete && item.status === 'Approved' && <Badge variant="warning">{tOvertime('flags.incomplete')}</Badge>}
                      {item.late && <Badge variant="yellow">{tOvertime('flags.late')}</Badge>}
                      {item.leftEarly && <Badge variant="yellow">{tOvertime('flags.leftEarly')}</Badge>}
                    </div>
                  </TableCell>
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
