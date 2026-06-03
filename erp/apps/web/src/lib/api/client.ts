import axios, { AxiosError, type InternalAxiosRequestConfig } from 'axios';
import { useAuthStore } from '@/lib/auth/store';
import type { ApiError, AuthResponse } from './types';

const baseURL = process.env.NEXT_PUBLIC_API_BASE_URL ?? 'http://localhost:5180';

export const apiClient = axios.create({
  baseURL,
  headers: { 'Content-Type': 'application/json' },
  withCredentials: true,
});

apiClient.interceptors.request.use((config: InternalAxiosRequestConfig) => {
  const token = useAuthStore.getState().token;
  if (token) {
    config.headers.set('Authorization', `Bearer ${token}`);
  }
  return config;
});

let refreshPromise: Promise<string | null> | null = null;

async function tryRefresh(): Promise<string | null> {
  if (refreshPromise) {
    return refreshPromise;
  }
  refreshPromise = (async () => {
    try {
      const { data } = await axios.post<AuthResponse>(
        `${baseURL}/api/auth/refresh`,
        {},
        { withCredentials: true },
      );
      useAuthStore.getState().setSession(data.accessToken, data.user, data.expiresAtUtc);
      return data.accessToken;
    } catch {
      return null;
    } finally {
      refreshPromise = null;
    }
  })();
  return refreshPromise;
}

apiClient.interceptors.response.use(
  (response) => response,
  async (error: AxiosError<ApiError>) => {
    const original = error.config as InternalAxiosRequestConfig & { _retry?: boolean };

    if (error.response?.status === 401 && !original._retry) {
      original._retry = true;
      const newToken = await tryRefresh();

      if (newToken) {
        original.headers.set('Authorization', `Bearer ${newToken}`);
        return apiClient(original);
      }

      const { token, clear } = useAuthStore.getState();
      if (token) {
        clear();
        if (typeof window !== 'undefined' && !window.location.pathname.startsWith('/login')) {
          window.location.replace('/login');
        }
      }
    }

    return Promise.reject(error);
  },
);

export function extractApiError(error: unknown): ApiError {
  if (axios.isAxiosError(error)) {
    const data = error.response?.data as ApiError | undefined;
    if (data?.message) return { code: data.code, message: data.message };
    return { message: error.message };
  }
  if (error instanceof Error) return { message: error.message };
  return { message: 'Unexpected error' };
}
