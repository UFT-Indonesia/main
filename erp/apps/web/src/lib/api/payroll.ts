import { apiClient } from './client';
import type { LeaveDeductionMonth, PayrollSettings } from './types';

// Potongan Cuti (Owner only). `month` is any date in the month; omitted means the current one.

export async function getLeaveDeductionMonth(month?: string): Promise<LeaveDeductionMonth> {
  const { data } = await apiClient.get<LeaveDeductionMonth>('/api/payroll/leave-deductions', {
    params: { month },
  });
  return data;
}

export async function closeLeaveDeductionMonth(month: string): Promise<LeaveDeductionMonth> {
  const { data } = await apiClient.post<LeaveDeductionMonth>('/api/payroll/leave-deductions/close', { month });
  return data;
}

export async function setPayrollDivisor(divisor: number): Promise<PayrollSettings> {
  const { data } = await apiClient.put<PayrollSettings>('/api/payroll/leave-deductions/divisor', { divisor });
  return data;
}
