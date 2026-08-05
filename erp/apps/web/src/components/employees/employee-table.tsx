'use client';

import Link from 'next/link';
import type { Route } from 'next';
import { useTranslations } from 'next-intl';
import { KeyRound, Pencil, Trash2 } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button, buttonVariants } from '@/components/ui/button';
import { cn } from '@/lib/utils';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { useHasRole } from '@/lib/auth/store';
import type { Employee, EmployeeStatus } from '@/lib/api/types';

interface EmployeeTableProps {
  employees: Employee[];
  onDelete: (employee: Employee) => void;
}

const STATUS_VARIANT: Record<EmployeeStatus, 'success' | 'warning' | 'destructive'> = {
  Active: 'success',
  OnLeave: 'warning',
  Terminated: 'destructive',
};

function formatIdr(value: number, currency: string): string {
  try {
    return new Intl.NumberFormat('id-ID', {
      style: 'currency',
      currency,
      maximumFractionDigits: 0,
    }).format(value);
  } catch {
    return `${currency} ${value.toLocaleString('id-ID')}`;
  }
}

export function EmployeeTable({ employees, onDelete }: EmployeeTableProps) {
  const t = useTranslations('employees');
  const tForm = useTranslations('employees.form');
  const tCommon = useTranslations('common');
  // Matches the backend: only Owner reads pay or terminates; Manager may still edit.
  const isOwner = useHasRole('Owner');
  const canEdit = useHasRole('Owner', 'Manager');

  if (employees.length === 0) {
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
            <TableHead>{tForm('fullName')}</TableHead>
            <TableHead>{tForm('nik')}</TableHead>
            <TableHead>{tForm('role')}</TableHead>
            <TableHead>{tForm('status')}</TableHead>
            {isOwner && <TableHead className="text-right">{tForm('monthlyWage')}</TableHead>}
            <TableHead className="text-right">{tCommon('actions')}</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {employees.map((employee) => (
            <TableRow key={employee.id}>
              <TableCell className="font-medium">{employee.fullName}</TableCell>
              <TableCell className="font-mono text-xs">{employee.nik}</TableCell>
              <TableCell>
                <Badge variant="outline">{tForm(`roleOptions.${employee.role}`)}</Badge>
              </TableCell>
              <TableCell>
                <Badge variant={STATUS_VARIANT[employee.status]}>
                  {tForm(`statusOptions.${employee.status}`)}
                </Badge>
              </TableCell>
              {isOwner && (
                <TableCell className="text-right tabular-nums">
                  {employee.monthlyWageAmount === null
                    ? '—'
                    : formatIdr(employee.monthlyWageAmount, employee.monthlyWageCurrency ?? 'IDR')}
                </TableCell>
              )}
              <TableCell className="text-right">
                <div className="flex justify-end gap-1">
                  <Link
                    href={`/accounts?employeeId=${employee.id}` as Route}
                    className={cn(buttonVariants({ variant: 'ghost', size: 'icon' }))}
                    aria-label={t('createAccount')}
                    title={t('createAccount')}
                  >
                    <KeyRound className="h-4 w-4" />
                  </Link>
                  {canEdit && (
                    <Link
                      href={`/employees/${employee.id}` as Route}
                      className={cn(buttonVariants({ variant: 'ghost', size: 'icon' }))}
                      aria-label="Edit"
                    >
                      <Pencil className="h-4 w-4" />
                    </Link>
                  )}
                  {isOwner && (
                    <Button
                      variant="ghost"
                      size="icon"
                      onClick={() => onDelete(employee)}
                      disabled={employee.status === 'Terminated'}
                      aria-label="Delete"
                    >
                      <Trash2 className="h-4 w-4" />
                    </Button>
                  )}
                </div>
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </div>
  );
}
