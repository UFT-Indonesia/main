'use client';

import { useTranslations } from 'next-intl';
import { Lock } from 'lucide-react';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { useDateLocale } from '@/hooks/use-date-locale';
import { formatIdr } from '@/lib/utils';
import type { LeaveCorrectionPreview, LeaveRequest } from '@/lib/api/types';

/** "March 2026" for a "YYYY-MM-01" month, in the app's locale. */
export function useFormatMonth(): (ymd: string) => string {
  const formatter = new Intl.DateTimeFormat(useDateLocale(), { month: 'long', year: 'numeric', timeZone: 'UTC' });
  return (ymd) => formatter.format(new Date(`${ymd}T00:00:00Z`));
}

/**
 * Why a request's buttons are disabled or behave differently because of a closed payroll month — the
 * server decides, this only says it. Null when payroll doesn't touch the request for this caller.
 */
export function usePayrollLockReason(): (request: LeaveRequest) => string | null {
  const t = useTranslations('leave.payrollLock');
  const formatMonth = useFormatMonth();
  return (request) => {
    if (request.canDecide && request.approveBlockedMonth) {
      return t('approve', { month: formatMonth(request.approveBlockedMonth), employee: request.employeeFullName });
    }
    if (request.payrollClosedMonth && (request.editBlockedByPayroll || request.cancelBlockedByPayroll)) {
      return t('ownerOnly', { month: formatMonth(request.payrollClosedMonth) });
    }
    if (request.payrollClosedMonth && request.isCorrection) {
      return t('correction', { month: formatMonth(request.payrollClosedMonth) });
    }
    return null;
  };
}

/** The one-line reason under a request in the list. */
export function PayrollLockNote({ request }: { request: LeaveRequest }) {
  const reason = usePayrollLockReason()(request);
  if (!reason) return null;
  return (
    <div className="mt-1 flex items-start gap-1 text-xs font-normal text-muted-foreground">
      <Lock className="mt-0.5 h-3 w-3 shrink-0" aria-hidden />
      <span>{reason}</span>
    </div>
  );
}

/** Correction mode's warning: the closed month's paid figures won't change. */
export function CorrectionBanner({ month }: { month: string }) {
  const t = useTranslations('leave.correction');
  const formatMonth = useFormatMonth();
  return (
    <div className="mt-3 flex gap-2 rounded-lg border border-warning/50 bg-warning/10 px-3 py-2 text-sm" role="status">
      <Lock className="mt-0.5 h-4 w-4 shrink-0" aria-hidden />
      <p>{t('banner', { month: formatMonth(month) })}</p>
    </div>
  );
}

/** Required in correction mode; the employee and their manager will read it. */
export function CorrectionReasonField({
  value,
  onChange,
  employee,
  disabled,
}: {
  value: string;
  onChange: (value: string) => void;
  employee: string;
  disabled?: boolean;
}) {
  const t = useTranslations('leave.correction');
  return (
    <div className="flex flex-col gap-1.5">
      <Label htmlFor="correction-reason">{t('reasonLabel', { employee })}</Label>
      <Input
        id="correction-reason"
        value={value}
        maxLength={1000}
        disabled={disabled}
        onChange={(e) => onChange(e.target.value)}
        placeholder={t('reasonPlaceholder')}
        aria-required
      />
    </div>
  );
}

/**
 * What the correction does, before it's saved: over-quota days before → after, and the rupiah that
 * moves to the first open month (Owner only — this panel is only ever shown in correction mode).
 */
export function CorrectionEffect({
  preview,
  loading,
  error,
}: {
  preview: LeaveCorrectionPreview | undefined;
  loading: boolean;
  error: string | null;
}) {
  const t = useTranslations('leave.correction');
  const formatMonth = useFormatMonth();
  // Not useFormatLeaveDate: leave-dialogs imports this file, so this file doesn't import it back.
  const dateFormatter = new Intl.DateTimeFormat(useDateLocale(), { dateStyle: 'medium', timeZone: 'UTC' });
  const formatDate = (ymd: string) => dateFormatter.format(new Date(`${ymd}T00:00:00Z`));

  return (
    <section className="rounded-lg border border-border bg-muted/30 px-3 py-2 text-sm" aria-live="polite">
      <h3 className="mb-1 font-medium">{t('effect')}</h3>
      {error ? (
        <p className="text-destructive">{error}</p>
      ) : !preview ? (
        <p className="text-muted-foreground">{loading ? t('calculating') : t('pickDates')}</p>
      ) : (
        <dl className="space-y-1">
          <div className="flex justify-between gap-4">
            <dt className="text-muted-foreground">{t('overQuota')}</dt>
            <dd className="tabular-nums">{preview.overQuotaDaysBefore} → {preview.overQuotaDaysAfter}</dd>
          </div>
          {preview.targetMonth && preview.netAmount !== 0 ? (
            <>
              <div className="flex justify-between gap-4">
                <dt className="text-muted-foreground">{t('lateCorrection', { month: formatMonth(preview.targetMonth) })}</dt>
                <dd className={preview.netAmount < 0 ? 'font-semibold text-success' : 'font-semibold text-destructive'}>
                  {preview.netAmount < 0
                    ? t('refund', { amount: formatIdr(-preview.netAmount) })
                    : t('charge', { amount: formatIdr(preview.netAmount) })}
                </dd>
              </div>
              <ul className="space-y-0.5 text-xs text-muted-foreground">
                {preview.lines.map((line) => (
                  <li key={`${line.date}-${line.amount}`}>
                    {t(line.amount < 0 ? 'lineRefund' : 'lineCharge', {
                      date: formatDate(line.date),
                      amount: formatIdr(Math.abs(line.amount)),
                      month: formatMonth(line.sourceMonth),
                    })}
                  </li>
                ))}
              </ul>
            </>
          ) : (
            <p className="text-muted-foreground">{t('noMoney')}</p>
          )}
        </dl>
      )}
    </section>
  );
}
