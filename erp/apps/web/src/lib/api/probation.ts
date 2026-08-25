import { apiClient } from './client';
import type {
  CreateProbationExtensionBody,
  ListProbationExtensionsParams,
  ListProbationExtensionsResponse,
  ProbationExtension,
} from './types';

export async function listProbationExtensions(
  params: ListProbationExtensionsParams,
): Promise<ListProbationExtensionsResponse> {
  const { data } = await apiClient.get<ListProbationExtensionsResponse>('/api/probation', {
    params: {
      page: params.page,
      pageSize: params.pageSize,
      status: params.status || undefined,
      employeeId: params.employeeId || undefined,
    },
  });
  return data;
}

export async function createProbationExtension(
  body: CreateProbationExtensionBody,
): Promise<ProbationExtension> {
  const { data } = await apiClient.post<ProbationExtension>('/api/probation', body);
  return data;
}

export async function decideProbationExtension(
  id: string,
  action: 'approve' | 'deny' | 'cancel',
  note?: string | null,
): Promise<ProbationExtension> {
  const { data } = await apiClient.post<ProbationExtension>(`/api/probation/${id}/${action}`, {
    note: note || null,
  });
  return data;
}
