'use client';

import { useState } from 'react';
import { useTranslations } from 'next-intl';
import { CalendarClock } from 'lucide-react';
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Input } from '@/components/ui/input';
import { DatePickerField } from '@/components/ui/date-picker';
import { Label } from '@/components/ui/label';
import { LEAVE_TYPES, formatLeaveDate } from '@/components/leave/leave-dialogs';
import { CreateProbationExtensionDialog } from '@/components/probation/probation-dialogs';
import { useSetLeaveQuota, useSetProbationEnd } from '@/hooks/use-employees';
import { useCreateProbationExtension } from '@/hooks/use-probation';
import { useLeaveBalance } from '@/hooks/use-leave';
import { useToast } from '@/hooks/use-toast';
import { extractApiError } from '@/lib/api/client';
import { useAuthStore, useHasRole } from '@/lib/auth/store';
import type { Employee, LeaveType } from '@/lib/api/types';

interface ProbationQuotaCardProps {
  employee: Employee;
}

/**
 * Probation and per-type leave entitlement for one employee. An owner edits both directly; the
 * employee's own manager can only ask for more probation time, which an owner then approves.
 */
export function ProbationQuotaCard({ employee }: ProbationQuotaCardProps) {
  const t = useTranslations('employees.probation');
  const tLeave = useTranslations('leave');
  const tCommon = useTranslations('common');
  const toast = useToast();

  const isOwner = useHasRole('Owner');
  const self = useAuthStore((s) => s.user);
  // Only the employee's own manager may file, and only while probation is actually running.
  const canRequestExtension =
    useHasRole('Manager')
    && employee.parentId === self?.employeeId
    && employee.role === 'Staff';

  const [endsOn, setEndsOn] = useState(employee.probationEndsOnOverride ?? '');
  const [requestOpen, setRequestOpen] = useState(false);

  const balance = useLeaveBalance(employee.id);
  const probationMutation = useSetProbationEnd(employee.id);
  const quotaMutation = useSetLeaveQuota(employee.id);
  const extensionMutation = useCreateProbationExtension();

  const onProbation = balance.data?.onProbation ?? false;

  // Each field starts at the number actually in force — the employee's own override if they
  // have one, otherwise the company default the server computed. An empty box would have meant
  // "default", which reads as "no quota" to everyone who is not the person who wrote it.
  const effectiveQuota = (type: LeaveType) =>
    employee.leaveQuotaOverrides?.[type]
    ?? balance.data?.quotas.find((q) => q.type === type)?.entitledDays
    ?? null;

  const [quotas, setQuotas] = useState<Partial<Record<LeaveType, string>>>({});
  const quotaValue = (type: LeaveType) =>
    quotas[type] ?? (effectiveQuota(type)?.toString() ?? '');

  const saving = probationMutation.isPending || quotaMutation.isPending;

  /**
   * One button, several writes: probation end and each changed quota have their own endpoint.
   * Sequential and only for what actually changed, so an unrelated field cannot be re-sent and
   * a failure stops rather than half-applying the rest.
   */
  const saveAll = async () => {
    try {
      const target = employee.probationEndsOnOverride ?? '';
      if (endsOn !== target) {
        await probationMutation.mutateAsync({ endsOn: endsOn || null });
      }

      for (const type of LEAVE_TYPES) {
        const raw = quotas[type];
        if (raw === undefined) continue;

        const trimmed = raw.trim();
        // Empty clears the override, falling back to the company default; 0 is a real setting
        // meaning "none of this type".
        const days = trimmed === '' ? null : Number(trimmed);
        if (days !== null && (!Number.isInteger(days) || days < 0)) continue;
        if (days === (employee.leaveQuotaOverrides?.[type] ?? null)) continue;

        await quotaMutation.mutateAsync({ type, days });
      }

      setQuotas({});
      toast.success(t('saveSuccessTitle'));
    } catch (err) {
      toast.error(t('saveErrorTitle'), extractApiError(err).message);
    }
  };

  const clearOverride = async () => {
    setEndsOn('');
    try {
      await probationMutation.mutateAsync({ endsOn: null });
      toast.success(t('saveSuccessTitle'));
    } catch (err) {
      toast.error(t('saveErrorTitle'), extractApiError(err).message);
    }
  };

  const requestExtension = async (_employeeId: string, proposedEndsOn: string, reason: string) => {
    try {
      await extensionMutation.mutateAsync({ employeeId: employee.id, proposedEndsOn, reason });
      toast.success(t('requestSuccessTitle'), t('requestSuccessDescription'));
      setRequestOpen(false);
    } catch (err) {
      toast.error(t('requestErrorTitle'), extractApiError(err).message);
    }
  };

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <CalendarClock className="h-4 w-4" />
          {t('title')}
        </CardTitle>
        <CardDescription>{t('hint')}</CardDescription>
      </CardHeader>
      <CardContent className="space-y-5">
        <dl className="grid grid-cols-2 gap-4 sm:grid-cols-3">
          <Field
            label={t('hireDate')}
            value={employee.hireDate ? formatLeaveDate(employee.hireDate) : '–'}
          />
          <Field
            label={t('probationEndsOn')}
            value={
              employee.probationEndsOn ? (
                <span className="flex flex-wrap items-center gap-2">
                  {formatLeaveDate(employee.probationEndsOn)}
                  <Badge variant={employee.probationEndsOnOverride ? 'warning' : 'secondary'}>
                    {employee.probationEndsOnOverride ? t('setByOwner') : t('default')}
                  </Badge>
                </span>
              ) : (
                t('none')
              )
            }
          />
          <Field
            label={t('status')}
            value={
              <Badge variant={onProbation ? 'warning' : 'success'}>
                {onProbation ? t('onProbation') : t('confirmed')}
              </Badge>
            }
          />
        </dl>

        {isOwner && (
          <div>
            <Label>{t('overrideLabel')}</Label>
            <div className="mt-3">
              <DatePickerField value={endsOn} onChange={setEndsOn} />
            </div>
            <p className="mt-2 text-xs text-muted-foreground">{t('overrideHint')}</p>
          </div>
        )}

        {isOwner && (
          <div className="space-y-3">
            <div>
              <Label>{t('quotaLabel')}</Label>
              <p className="text-xs text-muted-foreground">{t('quotaHint')}</p>
            </div>
            <div className="grid grid-cols-2 gap-3">
              {LEAVE_TYPES.map((type) => (
                <div key={type} className="flex flex-col gap-1.5">
                  <Label className="text-xs">{tLeave(`type.${type}`)}</Label>
                  <Input
                    type="number"
                    min="0"
                    step="1"
                    value={quotaValue(type)}
                    disabled={saving}
                    onChange={(e) => setQuotas((s) => ({ ...s, [type]: e.target.value }))}
                  />
                </div>
              ))}
            </div>
          </div>
        )}

        {/* Every write in this card commits from here. The other two are not saves — one resets
            a field to its default, the other opens a request dialog — so they sit alongside it
            rather than inside it. */}
        {(isOwner || (canRequestExtension && onProbation)) && (
          <div className="flex flex-col gap-2 sm:flex-row">
            {isOwner && (
              <Button className="w-full sm:flex-1" onClick={saveAll} disabled={saving}>
                {saving ? tCommon('loading') : tCommon('save')}
              </Button>
            )}
            {isOwner && employee.probationEndsOnOverride && (
              <Button variant="outline" onClick={clearOverride} disabled={saving}>
                {t('clearOverride')}
              </Button>
            )}
            {canRequestExtension && onProbation && (
              <Button variant="outline" onClick={() => setRequestOpen(true)}>
                {t('requestExtension')}
              </Button>
            )}
          </div>
        )}
      </CardContent>

      <CreateProbationExtensionDialog
        open={requestOpen}
        onOpenChange={setRequestOpen}
        onConfirm={requestExtension}
        submitting={extensionMutation.isPending}
        fixedEmployeeId={employee.id}
        fixedEmployeeName={employee.fullName}
      />
    </Card>
  );
}

/** Label above, value below and bolded — the same shape every read-only pair in the app uses. */
function Field({ label, value }: { label: string; value: React.ReactNode }) {
  return (
    <div>
      <dt className="text-xs text-muted-foreground">{label}</dt>
      <dd className="mt-0.5 text-sm font-medium">{value}</dd>
    </div>
  );
}
