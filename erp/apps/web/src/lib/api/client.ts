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

const NO_REFRESH_PATHS = ['/api/auth/login', '/api/auth/refresh', '/api/auth/logout'];

function isRefreshable(url: string | undefined): boolean {
  return !NO_REFRESH_PATHS.some((path) => url?.includes(path));
}

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

    if (error.response?.status === 401 && !original._retry && isRefreshable(original?.url)) {
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

    // Blob-response requests (CSV exports) get their JSON error body back as a Blob, which
    // hides the server's message from extractApiError. Unwrap it so the toast shows the real
    // reason (e.g. "narrow your filters") instead of "Request failed with status code 400".
    if (error.response?.data instanceof Blob && error.response.data.type.includes('json')) {
      try {
        error.response.data = JSON.parse(await error.response.data.text());
      } catch {
        // Not JSON after all — leave the blob alone and fall through to the generic message.
      }
    }

    return Promise.reject(error);
  },
);

/**
 * FastEndpoints' validation-failure shape: {statusCode, message: "One or more errors
 * occurred!", errors: {fieldName: [...], generalErrors: [...]}}. The top-level message is
 * always that same generic line — the actual reason lives in `errors`, so it has to be
 * flattened out for anything that only reads `message` (toasts, non-form callers).
 */
interface FastEndpointsErrorBody {
  message?: string;
  errors?: Record<string, string[]>;
}

function isFastEndpointsErrorBody(data: unknown): data is FastEndpointsErrorBody {
  return typeof data === 'object' && data !== null && 'errors' in data;
}

export function extractApiError(error: unknown): ApiError {
  if (axios.isAxiosError(error)) {
    const data = error.response?.data as unknown;

    if (isFastEndpointsErrorBody(data) && data.errors) {
      const message = Object.values(data.errors).flat().join(' ') || data.message || error.message;
      return { message, fieldErrors: data.errors };
    }

    const domainError = data as ApiError | undefined;
    if (domainError?.message) return { code: domainError.code, message: domainError.message };
    return { message: error.message };
  }
  if (error instanceof Error) return { message: error.message };
  return { message: 'Unexpected error' };
}
