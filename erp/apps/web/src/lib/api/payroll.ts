import { apiClient } from './client';
import type {
  AddLeaveDeductionAdjustmentBody,
  DivisorChange,
  LeaveDeductionAdjustment,
  LeaveDeductionMonth,
  PayrollSettings,
} from './types';

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

export async function getDivisorHistory(): Promise<DivisorChange[]> {
  const { data } = await apiClient.get<DivisorChange[]>('/api/payroll/leave-deductions/divisor-history');
  return data;
}

export async function addLeaveDeductionAdjustment(
  body: AddLeaveDeductionAdjustmentBody,
): Promise<LeaveDeductionAdjustment> {
  const { data } = await apiClient.post<LeaveDeductionAdjustment>('/api/payroll/leave-deductions/adjustments', body);
  return data;
}

export async function deleteLeaveDeductionAdjustment(id: string): Promise<void> {
  await apiClient.delete(`/api/payroll/leave-deductions/adjustments/${id}`);
}
