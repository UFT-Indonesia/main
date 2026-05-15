import { apiClient } from './client';
import type { AuthResponse, AuthUser } from './types';

export async function login(email: string, password: string): Promise<AuthResponse> {
  const { data } = await apiClient.post<AuthResponse>('/api/auth/login', { email, password });
  return data;
}

export async function fetchMe(): Promise<AuthUser> {
  const { data } = await apiClient.get<AuthUser>('/api/auth/me');
  return data;
}
