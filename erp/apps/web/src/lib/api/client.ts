import axios, { AxiosError, type InternalAxiosRequestConfig } from 'axios';
import { useAuthStore } from '@/lib/auth/store';
import type { ApiError } from './types';

const baseURL = process.env.NEXT_PUBLIC_API_BASE_URL ?? 'http://localhost:5180';

export const apiClient = axios.create({
  baseURL,
  headers: { 'Content-Type': 'application/json' },
});

apiClient.interceptors.request.use((config: InternalAxiosRequestConfig) => {
  const token = useAuthStore.getState().token;
  if (token) {
    config.headers.set('Authorization', `Bearer ${token}`);
  }
  return config;
});

apiClient.interceptors.response.use(
  (response) => response,
  (error: AxiosError<ApiError>) => {
    if (error.response?.status === 401) {
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
