import { apiClient } from './client';
import type {
  CreateOvertimeBody,
  CreateOvertimeCorrectionBody,
  CreateRapelBody,
  GajiPremiPeriod,
  ListOvertimeParams,
  ListOvertimeResponse,
  MyGajiPremiRow,
  OvertimeAssignment,
  OvertimeCorrection,
  OvertimeRequestStatus,
  Rapel,
} from './types';

/** The API's TimeOnly wants seconds; the browser's time input gives "HH:mm". */
const withSeconds = (time: string) => (time.length === 5 ? `${time}:00` : time);

export async function listOvertime(params: ListOvertimeParams): Promise<ListOvertimeResponse> {
  const { data } = await apiClient.get<ListOvertimeResponse>('/api/overtime', { params });
  return data;
}

export async function createOvertime(body: CreateOvertimeBody): Promise<OvertimeAssignment> {
  const { data } = await apiClient.post<OvertimeAssignment>('/api/overtime', {
    ...body,
    startTime: body.startTime ? withSeconds(body.startTime) : null,
    endTime: withSeconds(body.endTime),
  });
  return data;
}

export async function decideOvertime(
  id: string,
  action: 'approve' | 'reject' | 'cancel',
  note?: string | null,
): Promise<OvertimeAssignment> {
  const { data } = await apiClient.post<OvertimeAssignment>(`/api/overtime/${id}/${action}`, {
    note: note || null,
  });
  return data;
}

export async function editOvertimeEnd(id: string, endTime: string): Promise<OvertimeAssignment> {
  const { data } = await apiClient.post<OvertimeAssignment>(`/api/overtime/${id}/edit`, {
    endTime: withSeconds(endTime),
  });
  return data;
}

export async function listOvertimeCorrections(
  status?: OvertimeRequestStatus,
): Promise<OvertimeCorrection[]> {
  const { data } = await apiClient.get<OvertimeCorrection[]>('/api/overtime/corrections', {
    params: { status },
  });
  return data;
}

// Multipart: the proof travels with the fields. apiClient defaults to JSON, and axios only adds
// the multipart boundary itself when no Content-Type is set — see createLeaveRequest.
export async function createOvertimeCorrection(
  body: CreateOvertimeCorrectionBody,
): Promise<OvertimeCorrection> {
  const form = new FormData();
  form.append('assignmentId', body.assignmentId);
  form.append('kind', body.kind);
  form.append('time', body.time);
  form.append('reason', body.reason);
  form.append('attachment', body.attachment);
  const { data } = await apiClient.post<OvertimeCorrection>('/api/overtime/corrections', form, {
    headers: { 'Content-Type': undefined },
  });
  return data;
}

export async function decideOvertimeCorrection(
  id: string,
  action: 'approve' | 'reject',
  note?: string | null,
): Promise<OvertimeCorrection> {
  const { data } = await apiClient.post<OvertimeCorrection>(
    `/api/overtime/corrections/${id}/${action}`,
    { note: note || null },
  );
  return data;
}

export async function downloadOvertimeCorrectionProof(id: string): Promise<Blob> {
  const { data } = await apiClient.get<Blob>(`/api/overtime/corrections/${id}/attachment`, {
    responseType: 'blob',
  });
  return data;
}

export async function getMyGajiPremi(): Promise<MyGajiPremiRow[]> {
  const { data } = await apiClient.get<MyGajiPremiRow[]>('/api/overtime/gaji-premi/me');
  return data;
}

/** An employee files a claim (no amount); an Owner adds one directly and sets the amount. */
export async function createRapel(body: CreateRapelBody): Promise<Rapel> {
  const form = new FormData();
  form.append('employeeId', body.employeeId);
  form.append('workDate', body.workDate);
  form.append('note', body.note);
  form.append('attachment', body.attachment);
  if (body.amount != null) form.append('amount', String(body.amount));
  const { data } = await apiClient.post<Rapel>('/api/overtime/rapel', form, {
    headers: { 'Content-Type': undefined },
  });
  return data;
}

export async function downloadRapelProof(id: string): Promise<Blob> {
  const { data } = await apiClient.get<Blob>(`/api/overtime/rapel/${id}/attachment`, {
    responseType: 'blob',
  });
  return data;
}

// Payroll (Owner only) ------------------------------------------------------

export async function getGajiPremiPeriod(periodStart?: string): Promise<GajiPremiPeriod> {
  const { data } = await apiClient.get<GajiPremiPeriod>('/api/payroll/gaji-premi', {
    params: { periodStart },
  });
  return data;
}

export async function closeGajiPremiPeriod(periodStart: string): Promise<GajiPremiPeriod> {
  const { data } = await apiClient.post<GajiPremiPeriod>('/api/payroll/gaji-premi/close', {
    periodStart,
  });
  return data;
}

export async function decideRapel(
  id: string,
  body: { action: 'approve'; amount: number } | { action: 'reject'; note?: string | null },
): Promise<Rapel> {
  const { data } = await apiClient.post<Rapel>(
    `/api/payroll/rapel/${id}/${body.action}`,
    body.action === 'approve' ? { amount: body.amount } : { note: body.note || null },
  );
  return data;
}
