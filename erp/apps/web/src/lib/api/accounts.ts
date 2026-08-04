import { apiClient } from './client';
import type {
  CreateAccountBody,
  CreateAccountResponse,
  ListAccountsResponse,
  ListProvisionCandidatesResponse,
  ResetAccountPasswordResponse,
} from './types';

export async function listProvisionCandidates(): Promise<ListProvisionCandidatesResponse> {
  const { data } = await apiClient.get<ListProvisionCandidatesResponse>(
    '/api/accounts/provision-candidates',
  );
  return data;
}

export async function listAccounts(): Promise<ListAccountsResponse> {
  const { data } = await apiClient.get<ListAccountsResponse>('/api/accounts');
  return data;
}

export async function createAccount(body: CreateAccountBody): Promise<CreateAccountResponse> {
  const { data } = await apiClient.post<CreateAccountResponse>('/api/accounts', body);
  return data;
}

export async function setAccountEnabled(id: string, enabled: boolean): Promise<void> {
  await apiClient.patch(`/api/accounts/${id}/enabled`, { enabled });
}

export async function resetAccountPassword(id: string): Promise<ResetAccountPasswordResponse> {
  const { data } = await apiClient.post<ResetAccountPasswordResponse>(
    `/api/accounts/${id}/reset-password`,
  );
  return data;
}
