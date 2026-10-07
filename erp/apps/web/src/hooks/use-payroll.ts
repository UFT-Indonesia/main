'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { closeLeaveDeductionMonth, getLeaveDeductionMonth, setPayrollDivisor } from '@/lib/api/payroll';

const payrollKeys = {
  month: (month?: string) => ['payroll', 'leave-deductions', month ?? 'current'] as const,
};

/** Potongan Cuti for one month (Owner only). `month` is "YYYY-MM-01"; omitted means the current month. */
export function useLeaveDeductionMonth(month?: string) {
  return useQuery({
    queryKey: payrollKeys.month(month),
    queryFn: () => getLeaveDeductionMonth(month),
    placeholderData: (prev) => prev,
  });
}

/** Closing freezes the month, and the next one becomes closable, so every month's figures go stale. */
export function useCloseLeaveDeductionMonth() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: closeLeaveDeductionMonth,
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['payroll'] });
      // A closed month locks leave and holidays inside it.
      qc.invalidateQueries({ queryKey: ['leave'] });
    },
  });
}

export function useSetPayrollDivisor() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: setPayrollDivisor,
    onSuccess: () => qc.invalidateQueries({ queryKey: ['payroll'] }),
  });
}
