import { apiClient } from './client';
import type { AuthResponse, AuthUser } from './types';

export async function login(username: string, password: string): Promise<AuthResponse> {
  const { data } = await apiClient.post<AuthResponse>('/api/auth/login', { username, password });
  return data;
}

export async function changePassword(currentPassword: string, newPassword: string): Promise<AuthResponse> {
  const { data } = await apiClient.post<AuthResponse>('/api/auth/change-password', { currentPassword, newPassword });
  return data;
}

/**
 * Revokes the refresh-token family server-side and clears the HttpOnly cookie. Signing out
 * without this leaves the cookie live, and anything that then hits /api/auth/refresh signs the
 * browser straight back in as the user who just left.
 */
export async function logout(): Promise<void> {
  await apiClient.post('/api/auth/logout', {});
}

export async function refreshSession(): Promise<AuthResponse> {
  const { data } = await apiClient.post<AuthResponse>('/api/auth/refresh', {});
  return data;
}

export async function fetchMe(): Promise<AuthUser> {
  const { data } = await apiClient.get<AuthUser>('/api/auth/me');
  return data;
}
