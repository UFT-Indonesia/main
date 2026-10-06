'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  closeGajiPremiPeriod,
  createOvertime,
  createOvertimeCorrection,
  createRapel,
  decideOvertime,
  decideOvertimeCorrection,
  decideRapel,
  editOvertimeEnd,
  getGajiPremiPeriod,
  getMyGajiPremi,
  listOvertime,
  listOvertimeCorrections,
} from '@/lib/api/overtime';
import type { ListOvertimeParams, OvertimeRequestStatus } from '@/lib/api/types';

const overtimeKeys = {
  all: ['overtime'] as const,
  list: (params: ListOvertimeParams) => [...overtimeKeys.all, 'list', params] as const,
  corrections: (status?: OvertimeRequestStatus) => [...overtimeKeys.all, 'corrections', status] as const,
  mine: () => [...overtimeKeys.all, 'gaji-premi-me'] as const,
  period: (start?: string) => ['payroll', 'gaji-premi', start ?? 'current'] as const,
};

export function useOvertimeList(params: ListOvertimeParams, enabled = true) {
  return useQuery({
    queryKey: overtimeKeys.list(params),
    queryFn: () => listOvertime(params),
    placeholderData: (prev) => prev,
    enabled,
  });
}

export function useOvertimeCorrections(status?: OvertimeRequestStatus) {
  return useQuery({
    queryKey: overtimeKeys.corrections(status),
    queryFn: () => listOvertimeCorrections(status),
  });
}

export function useMyGajiPremi(enabled: boolean) {
  return useQuery({ queryKey: overtimeKeys.mine(), queryFn: getMyGajiPremi, enabled });
}

export function useGajiPremiPeriod(periodStart?: string) {
  return useQuery({
    queryKey: overtimeKeys.period(periodStart),
    queryFn: () => getGajiPremiPeriod(periodStart),
    placeholderData: (prev) => prev,
  });
}

/**
 * Everything overtime touches goes stale together: an OT assignment owns punches, so the
 * attendance calendar moves with it, and every pay figure is derived from it.
 */
function useInvalidateOvertime() {
  const qc = useQueryClient();
  return () => {
    qc.invalidateQueries({ queryKey: overtimeKeys.all });
    qc.invalidateQueries({ queryKey: ['payroll'] });
    qc.invalidateQueries({ queryKey: ['attendance'] });
    qc.invalidateQueries({ queryKey: ['leave'] });
  };
}

export function useCreateOvertime() {
  const invalidate = useInvalidateOvertime();
  return useMutation({ mutationFn: createOvertime, onSuccess: invalidate });
}

export function useDecideOvertime() {
  const invalidate = useInvalidateOvertime();
  return useMutation({
    mutationFn: ({ id, action, note }: { id: string; action: 'approve' | 'reject' | 'cancel'; note?: string | null }) =>
      decideOvertime(id, action, note),
    onSuccess: invalidate,
  });
}

export function useEditOvertimeEnd() {
  const invalidate = useInvalidateOvertime();
  return useMutation({
    mutationFn: ({ id, endTime }: { id: string; endTime: string }) => editOvertimeEnd(id, endTime),
    onSuccess: invalidate,
  });
}

export function useCreateOvertimeCorrection() {
  const invalidate = useInvalidateOvertime();
  return useMutation({ mutationFn: createOvertimeCorrection, onSuccess: invalidate });
}

export function useDecideOvertimeCorrection() {
  const invalidate = useInvalidateOvertime();
  return useMutation({
    mutationFn: ({ id, action, note }: { id: string; action: 'approve' | 'reject'; note?: string | null }) =>
      decideOvertimeCorrection(id, action, note),
    onSuccess: invalidate,
  });
}

export function useCreateRapel() {
  const invalidate = useInvalidateOvertime();
  return useMutation({ mutationFn: createRapel, onSuccess: invalidate });
}

export function useDecideRapel() {
  const invalidate = useInvalidateOvertime();
  return useMutation({
    mutationFn: ({ id, ...body }: { id: string } & Parameters<typeof decideRapel>[1]) => decideRapel(id, body),
    onSuccess: invalidate,
  });
}

export function useCloseGajiPremiPeriod() {
  const invalidate = useInvalidateOvertime();
  return useMutation({ mutationFn: closeGajiPremiPeriod, onSuccess: invalidate });
}
