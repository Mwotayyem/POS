import axios, {
  type AxiosInstance,
  type AxiosResponse,
  type InternalAxiosRequestConfig,
} from 'axios';
import { tokenStore } from '../auth/tokenStore';
import { ApiRequestError, type ApiError, type ApiResponse } from './types';

const BASE_URL = (import.meta.env.VITE_API_BASE_URL as string | undefined) ?? '/api/v1';

/** Called when refreshing fails / no session — set by the auth layer to force a logout. */
let onAuthFailure: (() => void) | null = null;
export function setAuthFailureHandler(handler: () => void): void {
  onAuthFailure = handler;
}

const http: AxiosInstance = axios.create({
  baseURL: BASE_URL,
  headers: { 'Content-Type': 'application/json' },
});

// Attach the access token to every request.
http.interceptors.request.use((config: InternalAxiosRequestConfig) => {
  const token = tokenStore.getAccess();
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

// ---- Refresh-on-401 with a single in-flight refresh shared by concurrent requests ----
let refreshing: Promise<boolean> | null = null;

async function refreshTokens(): Promise<boolean> {
  const refreshToken = tokenStore.getRefresh();
  if (!refreshToken) return false;
  try {
    const res = await axios.post<ApiResponse<{ accessToken: string; refreshToken: string }>>(
      `${BASE_URL}/auth/refresh`,
      { refreshToken },
      { headers: { 'Content-Type': 'application/json' } },
    );
    const data = res.data.data;
    if (res.data.success && data) {
      tokenStore.set(data.accessToken, data.refreshToken);
      return true;
    }
    return false;
  } catch {
    return false;
  }
}

http.interceptors.response.use(
  (response) => response,
  async (error) => {
    const original = error.config as (InternalAxiosRequestConfig & { _retried?: boolean }) | undefined;
    const status = error.response?.status;

    // Attempt one refresh on 401 (but never for the refresh/login calls themselves).
    const isAuthCall =
      typeof original?.url === 'string' &&
      (original.url.includes('/auth/refresh') || original.url.includes('/auth/login'));

    if (status === 401 && original && !original._retried && !isAuthCall) {
      original._retried = true;
      refreshing = refreshing ?? refreshTokens();
      const ok = await refreshing;
      refreshing = null;
      if (ok) {
        original.headers.Authorization = `Bearer ${tokenStore.getAccess()}`;
        return http(original);
      }
      onAuthFailure?.();
    }

    // Normalize the error into an ApiRequestError carrying the parsed ApiError.
    const apiError: ApiError = error.response?.data?.error ?? {
      code: status ? `HTTP_${status}` : 'NETWORK_ERROR',
      message: error.message ?? 'حدث خطأ غير متوقع.',
    };
    return Promise.reject(new ApiRequestError(apiError, status ?? 0));
  },
);

/** Unwraps the response envelope, returning `data` (or throwing an ApiRequestError). */
function unwrap<T>(response: AxiosResponse<ApiResponse<T>>): T {
  const raw: unknown = response.data;
  // 204 No Content: body is empty/absent; treat as success with no data.
  if (raw === null || raw === undefined || raw === '') {
    return undefined as T;
  }
  const body = raw as ApiResponse<T>;
  if (!body.success) {
    throw new ApiRequestError(
      body.error ?? { code: 'UNKNOWN', message: 'Unknown error' },
      response.status,
    );
  }
  return body.data as T;
}

export const api = {
  async get<T>(url: string, params?: Record<string, unknown>): Promise<T> {
    return unwrap<T>(await http.get<ApiResponse<T>>(url, { params }));
  },
  async post<T>(url: string, data?: unknown): Promise<T> {
    return unwrap<T>(await http.post<ApiResponse<T>>(url, data));
  },
  async put<T>(url: string, data?: unknown): Promise<T> {
    return unwrap<T>(await http.put<ApiResponse<T>>(url, data));
  },
  async del<T>(url: string): Promise<T> {
    return unwrap<T>(await http.delete<ApiResponse<T>>(url));
  },
};
