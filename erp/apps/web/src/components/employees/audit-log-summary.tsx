import { useTranslations } from 'next-intl';
import type { EmployeeAuditLogEntry } from '@/lib/api/types';

interface CreatedValue {
  fullName: string;
  role: string;
  monthlyWageAmount: number;
  monthlyWageCurrency: string;
  parentName: string | null;
}

interface BasicInfoValue {
  fullName: string;
  npwp: string | null;
}

interface SalaryValue {
  monthlyWageAmount: number;
  monthlyWageCurrency: string;
}

interface ParentValue {
  parentName: string | null;
}

interface RoleValue {
  role: string;
}

interface TerminatedValue {
  terminationDate: string;
}

interface HireDateValue {
  hireDate: string | null;
}

interface ProbationEndValue {
  probationEndsOn: string | null;
}

interface LeaveQuotaValue {
  leaveType: string;
  entitledDays: number | null;
}

function formatMoney(amount: number, currency: string): string {
  return new Intl.NumberFormat('id-ID', { style: 'currency', currency, maximumFractionDigits: 0 }).format(amount);
}

function parse<T>(json: string | null): T | null {
  return json ? (JSON.parse(json) as T) : null;
}

/** One-line human summary of an audit row's old/new JSON, per event type. */
export function AuditLogSummary({ entry }: { entry: EmployeeAuditLogEntry }) {
  const t = useTranslations('employeeAuditLog.summary');
  const none = t('none');

  switch (entry.eventType) {
    case 'employee.created': {
      const v = parse<CreatedValue>(entry.newValueJson);
      if (!v) return null;
      return (
        <span>
          {t('created', {
            role: v.role,
            wage: formatMoney(v.monthlyWageAmount, v.monthlyWageCurrency),
            parent: v.parentName ?? none,
          })}
        </span>
      );
    }
    case 'employee.basic_info_changed': {
      const oldValue = parse<BasicInfoValue>(entry.oldValueJson);
      const newValue = parse<BasicInfoValue>(entry.newValueJson);
      if (!oldValue || !newValue) return null;
      const parts: string[] = [];
      if (oldValue.fullName !== newValue.fullName) {
        parts.push(t('name', { from: oldValue.fullName, to: newValue.fullName }));
      }
      if (oldValue.npwp !== newValue.npwp) {
        parts.push(t('npwp', { from: oldValue.npwp ?? none, to: newValue.npwp ?? none }));
      }
      return <span>{parts.join('; ') || t('basicInfoUpdated')}</span>;
    }
    case 'employee.salary_changed': {
      const oldValue = parse<SalaryValue>(entry.oldValueJson);
      const newValue = parse<SalaryValue>(entry.newValueJson);
      if (!oldValue || !newValue) return null;
      return (
        <span>
          {t('wage', {
            from: formatMoney(oldValue.monthlyWageAmount, oldValue.monthlyWageCurrency),
            to: formatMoney(newValue.monthlyWageAmount, newValue.monthlyWageCurrency),
          })}
        </span>
      );
    }
    case 'employee.parent_changed': {
      const oldValue = parse<ParentValue>(entry.oldValueJson);
      const newValue = parse<ParentValue>(entry.newValueJson);
      if (!oldValue || !newValue) return null;
      return <span>{t('parent', { from: oldValue.parentName ?? none, to: newValue.parentName ?? none })}</span>;
    }
    case 'employee.role_changed': {
      const oldValue = parse<RoleValue>(entry.oldValueJson);
      const newValue = parse<RoleValue>(entry.newValueJson);
      if (!oldValue || !newValue) return null;
      return <span>{t('role', { from: oldValue.role, to: newValue.role })}</span>;
    }
    case 'employee.terminated': {
      const v = parse<TerminatedValue>(entry.newValueJson);
      if (!v) return null;
      return <span>{t('terminated', { date: v.terminationDate })}</span>;
    }
    case 'employee.hire_date_changed': {
      const oldValue = parse<HireDateValue>(entry.oldValueJson);
      const newValue = parse<HireDateValue>(entry.newValueJson);
      if (!oldValue || !newValue) return null;
      return <span>{t('hireDate', { from: oldValue.hireDate ?? none, to: newValue.hireDate ?? none })}</span>;
    }
    case 'employee.probation_end_changed': {
      const oldValue = parse<ProbationEndValue>(entry.oldValueJson);
      const newValue = parse<ProbationEndValue>(entry.newValueJson);
      if (!oldValue || !newValue) return null;
      return (
        <span>
          {t('probationEnd', {
            from: oldValue.probationEndsOn ?? none,
            to: newValue.probationEndsOn ?? none,
          })}
        </span>
      );
    }
    case 'employee.leave_quota_changed': {
      const oldValue = parse<LeaveQuotaValue>(entry.oldValueJson);
      const newValue = parse<LeaveQuotaValue>(entry.newValueJson);
      if (!oldValue || !newValue) return null;
      // Null days is not zero — it means no override at all, so the default applies again.
      return (
        <span>
          {t('leaveQuota', {
            type: newValue.leaveType,
            from: oldValue.entitledDays ?? t('quotaDefault'),
            to: newValue.entitledDays ?? t('quotaDefault'),
          })}
        </span>
      );
    }
    default:
      return <span>{entry.eventType}</span>;
  }
}
