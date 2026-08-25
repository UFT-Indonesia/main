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

  const saveProbationEnd = async (value: string | null) => {
    try {
      await probationMutation.mutateAsync({ endsOn: value });
      toast.success(t('saveSuccessTitle'));
    } catch (err) {
      toast.error(t('saveErrorTitle'), extractApiError(err).message);
    }
  };

  const saveQuota = async (type: LeaveType, raw: string) => {
    const trimmed = raw.trim();
    // Empty clears the override; 0 is a real setting meaning "none of this type".
    const days = trimmed === '' ? null : Number(trimmed);
    if (days !== null && (!Number.isInteger(days) || days < 0)) return;
    try {
      await quotaMutation.mutateAsync({ type, days });
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
        <dl className="space-y-2 text-sm">
          <Row label={t('hireDate')} value={employee.hireDate ? formatLeaveDate(employee.hireDate) : '–'} />
          <Row
            label={t('probationEndsOn')}
            value={
              employee.probationEndsOn ? (
                <span className="flex items-center justify-end gap-2">
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
          <Row
            label={t('status')}
            value={
              <Badge variant={onProbation ? 'warning' : 'success'}>
                {onProbation ? t('onProbation') : t('confirmed')}
              </Badge>
            }
          />
        </dl>

        {isOwner && (
          <div className="space-y-2 border-t border-border pt-4">
            <Label>{t('overrideLabel')}</Label>
            <div className="flex gap-2">
              <Input type="date" value={endsOn} onChange={(e) => setEndsOn(e.target.value)} />
              <Button
                onClick={() => saveProbationEnd(endsOn || null)}
                disabled={probationMutation.isPending}
              >
                {probationMutation.isPending ? tCommon('loading') : tCommon('save')}
              </Button>
              {employee.probationEndsOnOverride && (
                <Button
                  variant="outline"
                  onClick={() => { setEndsOn(''); void saveProbationEnd(null); }}
                  disabled={probationMutation.isPending}
                >
                  {t('clearOverride')}
                </Button>
              )}
            </div>
            <p className="text-xs text-muted-foreground">{t('overrideHint')}</p>
          </div>
        )}

        {canRequestExtension && onProbation && (
          <div className="border-t border-border pt-4">
            <Button variant="outline" onClick={() => setRequestOpen(true)}>
              {t('requestExtension')}
            </Button>
          </div>
        )}

        {isOwner && (
          <div className="space-y-3 border-t border-border pt-4">
            <div>
              <Label>{t('quotaLabel')}</Label>
              <p className="text-xs text-muted-foreground">{t('quotaHint')}</p>
            </div>
            <div className="grid grid-cols-2 gap-3 md:grid-cols-4">
              {LEAVE_TYPES.map((type) => (
                <QuotaField
                  key={type}
                  label={tLeave(`type.${type}`)}
                  placeholder={t('quotaDefault')}
                  initial={employee.leaveQuotaOverrides?.[type]}
                  disabled={quotaMutation.isPending}
                  onCommit={(raw) => saveQuota(type, raw)}
                />
              ))}
            </div>
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

function Row({ label, value }: { label: string; value: React.ReactNode }) {
  return (
    <div className="flex items-center justify-between gap-4">
      <dt className="shrink-0 text-muted-foreground">{label}</dt>
      <dd className="text-right font-medium">{value}</dd>
    </div>
  );
}

/** Commits on blur rather than on every keystroke — one PUT per edit, not per digit. */
function QuotaField({
  label,
  placeholder,
  initial,
  disabled,
  onCommit,
}: {
  label: string;
  placeholder: string;
  initial: number | undefined;
  disabled?: boolean;
  onCommit: (raw: string) => void;
}) {
  const [value, setValue] = useState(initial === undefined ? '' : String(initial));

  return (
    <div className="flex flex-col gap-1.5">
      <Label className="text-xs">{label}</Label>
      <Input
        type="number"
        min="0"
        step="1"
        value={value}
        placeholder={placeholder}
        disabled={disabled}
        onChange={(e) => setValue(e.target.value)}
        onBlur={() => {
          const current = initial === undefined ? '' : String(initial);
          if (value !== current) onCommit(value);
        }}
      />
    </div>
  );
}
