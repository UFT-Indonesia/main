'use client';

import { useState } from 'react';
import { useLocale, useTranslations } from 'next-intl';
import { Plus, Check, X, Ban, Pencil, FilePlus2, ChevronLeft, ChevronRight } from 'lucide-react';
import { AppShell } from '@/components/layout/app-shell';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Input } from '@/components/ui/input';
import { Skeleton } from '@/components/ui/skeleton';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import {
  CorrectionDialog,
  CreateOvertimeDialog,
  EditOvertimeEndDialog,
  NoteDialog,
  OVERTIME_STATUS_VARIANT,
  ProofLink,
  RapelDialog,
  hhmm,
} from '@/components/overtime/overtime-dialogs';
import { monthName, monthRange, periodLabel } from '@/components/overtime/period';
import { useFormatLeaveDate } from '@/components/leave/leave-dialogs';
import {
  useCreateOvertime,
  useCreateOvertimeCorrection,
  useCreateRapel,
  useDecideOvertime,
  useDecideOvertimeCorrection,
  useEditOvertimeEnd,
  useMyGajiPremi,
  useOvertimeCorrections,
  useOvertimeList,
} from '@/hooks/use-overtime';
import { useAttendancePolicy } from '@/hooks/use-attendance-settings';
import { useToast } from '@/hooks/use-toast';
import { extractApiError } from '@/lib/api/client';
import { downloadOvertimeCorrectionProof, downloadRapelProof } from '@/lib/api/overtime';
import { useAuthStore, useHasRole } from '@/lib/auth/store';
import { formatIdr } from '@/lib/utils';
import type { MyGajiPremiRow, OvertimeAssignment, OvertimeCorrection } from '@/lib/api/types';

const PAGE_SIZE = 20;

type Tab = 'assignments' | 'corrections' | 'mine';
type Decision = { item: OvertimeAssignment; action: 'approve' | 'reject' | 'cancel' };
type CorrectionDecision = { item: OvertimeCorrection; action: 'approve' | 'reject' };

const currentMonth = () => new Date().toISOString().slice(0, 7);

const isAttachmentError = (code?: string) => !!code && /attachment/.test(code);

export default function OvertimePage() {
  const t = useTranslations('overtime');
  const [tab, setTab] = useState<Tab>('assignments');

  const isOwner = useHasRole('Owner');
  const isManager = useHasRole('Manager');
  // "Gaji Premi Saya" is the employee's own pay: an Owner reads anyone's on the payroll page, and
  // a Manager sees neither pay nor this tab.
  const showMine = !isOwner && !isManager;

  const tabs: { key: Tab; label: string }[] = [
    { key: 'assignments', label: t('tabs.assignments') },
    { key: 'corrections', label: t('tabs.corrections') },
    ...(showMine ? [{ key: 'mine' as const, label: t('tabs.mine') }] : []),
  ];

  return (
    <AppShell>
      <div className="space-y-4">
        <header>
          <h1 className="text-2xl font-semibold tracking-tight">{t('title')}</h1>
          <p className="text-sm text-muted-foreground">{t('subtitle')}</p>
        </header>

        <div role="tablist" className="flex gap-1 border-b border-border">
          {tabs.map(({ key, label }) => (
            <button
              key={key}
              role="tab"
              aria-selected={tab === key}
              onClick={() => setTab(key)}
              className={`-mb-px border-b-2 px-3 py-2 text-sm font-medium transition-colors ${
                tab === key
                  ? 'border-primary text-foreground'
                  : 'border-transparent text-muted-foreground hover:text-foreground'
              }`}
            >
              {label}
            </button>
          ))}
        </div>

        {tab === 'assignments' && <AssignmentsTab canAssign={isOwner || isManager} showPay={!isManager || isOwner} />}
        {tab === 'corrections' && <CorrectionsTab />}
        {tab === 'mine' && showMine && <MineTab />}
      </div>
    </AppShell>
  );
}

function ErrorBox({ error }: { error: unknown }) {
  return (
    <div className="rounded-lg border border-destructive/40 bg-destructive/10 p-4 text-sm text-destructive">
      {extractApiError(error).message}
    </div>
  );
}

function Loading() {
  return (
    <div className="space-y-2">
      {Array.from({ length: 5 }).map((_, i) => (
        <Skeleton key={i} className="h-12 w-full" />
      ))}
    </div>
  );
}

function Empty({ text }: { text: string }) {
  return <div className="rounded-lg border border-dashed border-border p-8 text-center text-sm text-muted-foreground">{text}</div>;
}

/** Wall-clock HH:mm of an instant in the office's zone. */
function useFormatClock(): (iso: string | null) => string {
  const zone = useAttendancePolicy().data?.timeZoneId ?? 'Asia/Jakarta';
  const formatter = new Intl.DateTimeFormat('en-GB', { hour: '2-digit', minute: '2-digit', hour12: false, timeZone: zone });
  return (iso) => (iso ? formatter.format(new Date(iso)) : '—');
}

function AssignmentsTab({ canAssign, showPay }: { canAssign: boolean; showPay: boolean }) {
  const t = useTranslations('overtime');
  const tCommon = useTranslations('common');
  const toast = useToast();
  const formatDate = useFormatLeaveDate();
  const formatClock = useFormatClock();

  const [page, setPage] = useState(1);
  const [month, setMonth] = useState(currentMonth());
  const range = month ? monthRange(month) : {};
  const { data, isLoading, isFetching, error } = useOvertimeList({ page, pageSize: PAGE_SIZE, ...range });

  const [createOpen, setCreateOpen] = useState(false);
  const [decision, setDecision] = useState<Decision | null>(null);
  const [editing, setEditing] = useState<OvertimeAssignment | null>(null);
  const [correcting, setCorrecting] = useState<OvertimeAssignment | null>(null);
  const [attachmentError, setAttachmentError] = useState<string | null>(null);

  const createMutation = useCreateOvertime();
  const decideMutation = useDecideOvertime();
  const editMutation = useEditOvertimeEnd();
  const correctionMutation = useCreateOvertimeCorrection();

  const totalPages = data ? Math.max(1, Math.ceil(data.totalCount / data.pageSize)) : 1;

  return (
    <div className="space-y-4">
      <div className="flex items-end justify-between gap-3">
        <div className="flex flex-col gap-1.5">
          <label className="text-xs text-muted-foreground">{t('month')}</label>
          <div className="flex items-center gap-2">
            <Input
              type="month"
              className="w-44"
              value={month}
              onChange={(e) => { setMonth(e.target.value); setPage(1); }}
            />
            {month && (
              <Button variant="ghost" size="sm" onClick={() => { setMonth(''); setPage(1); }}>
                {t('allMonths')}
              </Button>
            )}
          </div>
        </div>
        {canAssign && (
          <Button onClick={() => setCreateOpen(true)}>
            <Plus className="h-4 w-4" />
            {t('create.button')}
          </Button>
        )}
      </div>

      {error ? (
        <ErrorBox error={error} />
      ) : isLoading ? (
        <Loading />
      ) : (data?.items.length ?? 0) === 0 ? (
        <Empty text={t('empty')} />
      ) : (
        <div className="rounded-lg border border-border bg-card">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>{t('columns.employee')}</TableHead>
                <TableHead>{t('columns.date')}</TableHead>
                <TableHead>{t('columns.window')}</TableHead>
                <TableHead>{t('columns.punches')}</TableHead>
                <TableHead className="text-right">{t('columns.hours')}</TableHead>
                {showPay && <TableHead className="text-right">{t('columns.pay')}</TableHead>}
                <TableHead>{t('columns.status')}</TableHead>
                <TableHead className="text-right">{tCommon('actions')}</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {data!.items.map((item) => (
                <TableRow key={item.id}>
                  <TableCell className="font-medium">{item.employeeFullName}</TableCell>
                  <TableCell className="tabular-nums">
                    {formatDate(item.date)}
                    <div className="text-xs text-muted-foreground">{item.isDayOff ? t('dayOff') : t('weekday')}</div>
                  </TableCell>
                  <TableCell className="tabular-nums">
                    {hhmm(item.startTime)} – {hhmm(item.endTime)}
                    {item.endsNextDay && <span className="text-xs text-muted-foreground"> (+1)</span>}
                  </TableCell>
                  <TableCell className="tabular-nums">
                    {formatClock(item.tapInUtc)} – {formatClock(item.tapOutUtc)}
                    {item.status === 'Approved' && item.incomplete && (
                      <div className="text-xs text-muted-foreground">
                        {t('assignedUntil', { end: hhmm(item.endTime) })}
                      </div>
                    )}
                  </TableCell>
                  <TableCell className="text-right tabular-nums">{item.hours}</TableCell>
                  {showPay && (
                    <TableCell className="text-right tabular-nums">
                      {item.amount == null ? '—' : formatIdr(item.amount)}
                    </TableCell>
                  )}
                  <TableCell>
                    <div className="flex flex-wrap items-center gap-1">
                      <Badge variant={OVERTIME_STATUS_VARIANT[item.status]}>{t(`status.${item.status}`)}</Badge>
                      {item.status === 'Approved' && item.incomplete && <Badge variant="warning">{t('flags.incomplete')}</Badge>}
                      {item.late && <Badge variant="yellow">{t('flags.late')}</Badge>}
                      {item.leftEarly && <Badge variant="yellow">{t('flags.leftEarly')}</Badge>}
                    </div>
                    {item.decisionNote && <div className="mt-1 text-xs text-muted-foreground">{item.decisionNote}</div>}
                  </TableCell>
                  <TableCell className="text-right">
                    <div className="flex justify-end gap-1">
                      {item.canDecide && (
                        <>
                          <Button variant="ghost" size="icon" title={t('decide.approve.title')} aria-label={t('decide.approve.title')}
                            onClick={() => setDecision({ item, action: 'approve' })}>
                            <Check className="h-4 w-4 text-success" />
                          </Button>
                          <Button variant="ghost" size="icon" title={t('decide.reject.title')} aria-label={t('decide.reject.title')}
                            onClick={() => setDecision({ item, action: 'reject' })}>
                            <X className="h-4 w-4 text-destructive" />
                          </Button>
                        </>
                      )}
                      {item.canManage && (
                        <>
                          <Button variant="ghost" size="icon" title={t('edit.title')} aria-label={t('edit.title')} onClick={() => setEditing(item)}>
                            <Pencil className="h-4 w-4 text-muted-foreground" />
                          </Button>
                          {/* Once it has punches the server refuses a cancel; the button simply fails with its message. */}
                          <Button variant="ghost" size="icon" title={t('decide.cancel.title')} aria-label={t('decide.cancel.title')}
                            onClick={() => setDecision({ item, action: 'cancel' })}>
                            <Ban className="h-4 w-4 text-muted-foreground" />
                          </Button>
                        </>
                      )}
                      {item.canFileCorrection && (
                        <Button variant="ghost" size="icon" title={t('correction.button')} aria-label={t('correction.button')}
                          onClick={() => { setAttachmentError(null); setCorrecting(item); }}>
                          <FilePlus2 className="h-4 w-4 text-primary" />
                        </Button>
                      )}
                    </div>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </div>
      )}

      {data && data.totalCount > 0 && (
        <div className="flex items-center justify-between">
          <p className="text-xs text-muted-foreground">
            {t('pagination', {
              from: (data.page - 1) * data.pageSize + 1,
              to: Math.min(data.page * data.pageSize, data.totalCount),
              total: data.totalCount,
            })}
          </p>
          <div className="flex items-center gap-2">
            <Button variant="outline" size="sm" onClick={() => setPage((p) => Math.max(1, p - 1))} disabled={page <= 1 || isFetching}>
              <ChevronLeft className="h-4 w-4" />
              {tCommon('previous')}
            </Button>
            <span className="text-xs text-muted-foreground">{data.page} / {totalPages}</span>
            <Button variant="outline" size="sm" onClick={() => setPage((p) => p + 1)} disabled={page >= totalPages || isFetching}>
              {tCommon('next')}
              <ChevronRight className="h-4 w-4" />
            </Button>
          </div>
        </div>
      )}

      <CreateOvertimeDialog
        open={createOpen}
        onOpenChange={setCreateOpen}
        submitting={createMutation.isPending}
        onConfirm={async (body) => {
          try {
            const created = await createMutation.mutateAsync(body);
            toast.success(
              t('create.successTitle'),
              created.status === 'Pending' ? t('create.successPending') : t('create.successApproved'),
            );
            setCreateOpen(false);
          } catch (err) {
            toast.error(t('create.errorTitle'), extractApiError(err).message);
          }
        }}
      />

      <NoteDialog
        open={!!decision}
        onOpenChange={(open) => !open && setDecision(null)}
        title={decision ? t(`decide.${decision.action}.title`) : ''}
        description={decision ? t(`decide.${decision.action}.description`, {
          employee: decision.item.employeeFullName,
          date: formatDate(decision.item.date),
        }) : ''}
        confirmLabel={decision ? t(`decide.${decision.action}.confirm`) : ''}
        destructive={decision?.action !== 'approve'}
        withNote={decision?.action !== 'approve'}
        submitting={decideMutation.isPending}
        onConfirm={async (note) => {
          if (!decision) return;
          try {
            await decideMutation.mutateAsync({ id: decision.item.id, action: decision.action, note });
            toast.success(t(`decide.${decision.action}.successTitle`));
            setDecision(null);
          } catch (err) {
            toast.error(t(`decide.${decision.action}.errorTitle`), extractApiError(err).message);
          }
        }}
      />

      <EditOvertimeEndDialog
        assignment={editing}
        onOpenChange={(open) => !open && setEditing(null)}
        submitting={editMutation.isPending}
        onConfirm={async (endTime) => {
          if (!editing) return;
          try {
            const updated = await editMutation.mutateAsync({ id: editing.id, endTime });
            toast.success(t('edit.successTitle'), updated.status === 'Pending' ? t('edit.successPending') : undefined);
            setEditing(null);
          } catch (err) {
            toast.error(t('edit.errorTitle'), extractApiError(err).message);
          }
        }}
      />

      <CorrectionDialog
        assignment={correcting}
        onOpenChange={(open) => !open && setCorrecting(null)}
        submitting={correctionMutation.isPending}
        attachmentError={attachmentError}
        onConfirm={async (body) => {
          if (!correcting) return;
          setAttachmentError(null);
          try {
            await correctionMutation.mutateAsync({ assignmentId: correcting.id, ...body });
            toast.success(t('correction.successTitle'), t('correction.successDescription'));
            setCorrecting(null);
          } catch (err) {
            const apiError = extractApiError(err);
            if (isAttachmentError(apiError.code)) {
              setAttachmentError(apiError.message);
              return;
            }
            toast.error(t('correction.errorTitle'), apiError.message);
          }
        }}
      />
    </div>
  );
}

function CorrectionsTab() {
  const t = useTranslations('overtime');
  const tCommon = useTranslations('common');
  const toast = useToast();
  const formatDate = useFormatLeaveDate();
  const formatClock = useFormatClock();
  const { data, isLoading, error } = useOvertimeCorrections();
  const decideMutation = useDecideOvertimeCorrection();
  const [decision, setDecision] = useState<CorrectionDecision | null>(null);

  return (
    <div className="space-y-4">
      {error ? (
        <ErrorBox error={error} />
      ) : isLoading ? (
        <Loading />
      ) : (data?.length ?? 0) === 0 ? (
        <Empty text={t('corrections.empty')} />
      ) : (
        <div className="rounded-lg border border-border bg-card">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>{t('columns.employee')}</TableHead>
                <TableHead>{t('columns.date')}</TableHead>
                <TableHead>{t('corrections.columns.punch')}</TableHead>
                <TableHead>{t('corrections.columns.reason')}</TableHead>
                <TableHead>{t('corrections.columns.proof')}</TableHead>
                <TableHead>{t('columns.status')}</TableHead>
                <TableHead className="text-right">{tCommon('actions')}</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {data!.map((item) => (
                <TableRow key={item.id}>
                  <TableCell className="font-medium">{item.employeeFullName}</TableCell>
                  <TableCell className="tabular-nums">{formatDate(item.workDate)}</TableCell>
                  <TableCell className="tabular-nums">
                    {t(`correction.kind.${item.kind}`)} · {formatClock(item.punchedAtUtc)}
                  </TableCell>
                  <TableCell className="max-w-xs truncate" title={item.reason}>{item.reason}</TableCell>
                  <TableCell>
                    <ProofLink fileName={item.attachmentFileName} fetchProof={() => downloadOvertimeCorrectionProof(item.id)} />
                  </TableCell>
                  <TableCell>
                    <Badge variant={OVERTIME_STATUS_VARIANT[item.status]}>
                      {t(`status.${item.status}`)}
                    </Badge>
                    {item.decisionNote && <div className="mt-1 text-xs text-muted-foreground">{item.decisionNote}</div>}
                  </TableCell>
                  <TableCell className="text-right">
                    {item.canDecide && (
                      <div className="flex justify-end gap-1">
                        <Button variant="ghost" size="icon" title={t('decide.approve.title')} aria-label={t('decide.approve.title')}
                          onClick={() => setDecision({ item, action: 'approve' })}>
                          <Check className="h-4 w-4 text-success" />
                        </Button>
                        <Button variant="ghost" size="icon" title={t('decide.reject.title')} aria-label={t('decide.reject.title')}
                          onClick={() => setDecision({ item, action: 'reject' })}>
                          <X className="h-4 w-4 text-destructive" />
                        </Button>
                      </div>
                    )}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </div>
      )}

      <NoteDialog
        open={!!decision}
        onOpenChange={(open) => !open && setDecision(null)}
        title={decision ? t(`corrections.decide.${decision.action}.title`) : ''}
        description={decision ? t(`corrections.decide.${decision.action}.description`, {
          employee: decision.item.employeeFullName,
          kind: t(`correction.kind.${decision.item.kind}`),
          time: formatClock(decision.item.punchedAtUtc),
        }) : ''}
        confirmLabel={decision ? t(`corrections.decide.${decision.action}.confirm`) : ''}
        destructive={decision?.action === 'reject'}
        withNote={decision?.action === 'reject'}
        submitting={decideMutation.isPending}
        onConfirm={async (note) => {
          if (!decision) return;
          try {
            await decideMutation.mutateAsync({ id: decision.item.id, action: decision.action, note });
            toast.success(t(`corrections.decide.${decision.action}.successTitle`));
            setDecision(null);
          } catch (err) {
            toast.error(t(`corrections.decide.${decision.action}.errorTitle`), extractApiError(err).message);
          }
        }}
      />
    </div>
  );
}

/** "Gaji Premi Saya": the employee's own OT disbursements, one row per period, with Request Rapel on closed ones. */
function MineTab() {
  const t = useTranslations('overtime');
  const locale = useLocale();
  const toast = useToast();
  const formatDate = useFormatLeaveDate();
  const self = useAuthStore((s) => s.user);
  const { data, isLoading, error } = useMyGajiPremi(true);
  const rapelMutation = useCreateRapel();
  const [rapelFor, setRapelFor] = useState<MyGajiPremiRow | null>(null);
  const [attachmentError, setAttachmentError] = useState<string | null>(null);

  return (
    <div className="space-y-4">
      {error ? (
        <ErrorBox error={error} />
      ) : isLoading ? (
        <Loading />
      ) : (data?.length ?? 0) === 0 ? (
        <Empty text={t('mine.empty')} />
      ) : (
        <div className="rounded-lg border border-border bg-card">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>{t('mine.columns.period')}</TableHead>
                <TableHead>{t('mine.columns.payout')}</TableHead>
                <TableHead>{t('mine.columns.hours')}</TableHead>
                <TableHead>{t('mine.columns.rapel')}</TableHead>
                <TableHead className="text-right">{t('mine.columns.total')}</TableHead>
                <TableHead />
              </TableRow>
            </TableHeader>
            <TableBody>
              {data!.map((row) => (
                <TableRow key={row.start}>
                  <TableCell className="font-medium">
                    {periodLabel(row.start, row.end, locale)}
                    {row.estimate && <div className="text-xs text-muted-foreground">{t('mine.estimate')}</div>}
                  </TableCell>
                  <TableCell className="tabular-nums">{formatDate(row.payoutDate)}</TableCell>
                  <TableCell className="tabular-nums">
                    <div>{monthName(row.start, locale)}: {row.hours1} {t('mine.hoursUnit')} · {row.days1} {t('mine.daysUnit')}</div>
                    <div>{monthName(row.end, locale)}: {row.hours2} {t('mine.hoursUnit')} · {row.days2} {t('mine.daysUnit')}</div>
                  </TableCell>
                  <TableCell className="text-sm">
                    {row.rapelPaid.map((r) => (
                      <div key={r.id} className="tabular-nums">
                        {t('mine.rapelLine', { date: formatDate(r.workDate) })} · {formatIdr(r.amount ?? 0)}
                      </div>
                    ))}
                    {row.rapelClaims.map((r) => (
                      <div key={r.id} className="flex flex-wrap items-center gap-1 text-xs text-muted-foreground">
                        {t('mine.rapelClaim', { date: formatDate(r.workDate) })}
                        <Badge variant={OVERTIME_STATUS_VARIANT[r.status]}>{t(`status.${r.status}`)}</Badge>
                        {r.status === 'Rejected' && r.decisionNote && <span>{r.decisionNote}</span>}
                        <ProofLink fileName={r.attachmentFileName} fetchProof={() => downloadRapelProof(r.id)} />
                      </div>
                    ))}
                    {row.rapelPaid.length === 0 && row.rapelClaims.length === 0 && '—'}
                  </TableCell>
                  <TableCell className="text-right tabular-nums">{formatIdr(row.total)}</TableCell>
                  <TableCell className="text-right">
                    {/* Past the deadline the button stays, disabled: later claims go to the Owner directly. */}
                    {row.closed && (
                      <Button
                        variant="outline"
                        size="sm"
                        disabled={!row.canRequestRapel}
                        title={row.canRequestRapel ? undefined : t('rapel.deadlinePassed')}
                        onClick={() => { setAttachmentError(null); setRapelFor(row); }}
                      >
                        {t('rapel.requestButton')}
                      </Button>
                    )}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </div>
      )}

      <RapelDialog
        open={!!rapelFor}
        onOpenChange={(open) => !open && setRapelFor(null)}
        employeeId={self?.employeeId ?? ''}
        period={rapelFor ? { start: rapelFor.start, end: rapelFor.end } : null}
        submitting={rapelMutation.isPending}
        attachmentError={attachmentError}
        onConfirm={async (body) => {
          setAttachmentError(null);
          try {
            await rapelMutation.mutateAsync(body);
            toast.success(t('rapel.successTitle'), t('rapel.successDescription'));
            setRapelFor(null);
          } catch (err) {
            const apiError = extractApiError(err);
            if (isAttachmentError(apiError.code)) {
              setAttachmentError(apiError.message);
              return;
            }
            toast.error(t('rapel.errorTitle'), apiError.message);
          }
        }}
      />
    </div>
  );
}
