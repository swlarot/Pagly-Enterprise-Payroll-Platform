import config from './config';

const API_BASE_URL = config.apiUrl;

export interface ApiError {
  message: string;
  statusCode: number;
}

export class ApiException extends Error {
  constructor(
    public statusCode: number,
    message: string,
    public code?: string,
    public details?: any
  ) {
    super(message);
    this.name = 'ApiException';
  }
}

// Flag to prevent infinite refresh loops
let isRefreshing = false;
let refreshPromise: Promise<string> | null = null;

async function tryRefreshToken(): Promise<string | null> {
  const refreshToken = localStorage.getItem('refresh_token');

  if (!refreshToken) {
    return null;
  }

  // Prevent concurrent refresh attempts
  if (isRefreshing && refreshPromise) {
    return refreshPromise;
  }

  isRefreshing = true;
  refreshPromise = (async () => {
    try {
      const response = await fetch(`${API_BASE_URL}/api/auth/refresh`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({ refreshToken }),
      });

      if (response.ok) {
        const data = await response.json();
        localStorage.setItem('auth_token', data.token);
        localStorage.setItem('refresh_token', data.refreshToken);
        return data.token;
      }

      return null;
    } catch (error) {
      if (import.meta.env.DEV) console.error('Token refresh failed:', error);
      return null;
    } finally {
      isRefreshing = false;
      refreshPromise = null;
    }
  })();

  return refreshPromise;
}

async function handleResponse<T>(response: Response, originalRequest?: {
  url: string;
  method: string;
  headers: HeadersInit;
  body?: any;
}): Promise<T> {
  // Handle 401 - Try refresh token first
  if (response.status === 401) {
    const newToken = await tryRefreshToken();

    if (newToken && originalRequest) {
      // Retry original request with new token
      const retryHeaders = { ...originalRequest.headers, Authorization: `Bearer ${newToken}` };
      const retryResponse = await fetch(originalRequest.url, {
        method: originalRequest.method,
        headers: retryHeaders,
        body: originalRequest.body ? JSON.stringify(originalRequest.body) : undefined,
      });

      // Recursively handle the retry response (but don't pass originalRequest to prevent infinite loop)
      if (retryResponse.status === 401) {
        // Refresh failed, logout
        localStorage.clear();
        window.location.href = '/login';
        throw new ApiException(401, 'Sesión expirada. Por favor inicia sesión nuevamente.');
      }

      return handleResponse<T>(retryResponse);
    }

    // No refresh token or refresh failed, logout
    localStorage.clear();
    window.location.href = '/login';
    throw new ApiException(401, 'Sesión expirada. Por favor inicia sesión nuevamente.');
  }

  // Try to parse JSON
  let data: any;
  const contentType = response.headers.get('content-type');

  if (contentType && contentType.includes('application/json')) {
    data = await response.json();
  } else {
    const text = await response.text();
    data = { message: text || 'Error en la solicitud' };
  }

  // Handle non-OK responses
  if (!response.ok) {
    // El body puede venir en dos shapes distintos:
    //   1) SaaS legacy: { message, error?, code? }
    //   2) API Platform v1 (RFC 7807 problem+json): { type, title, status, detail, instance, errorCode, requestId }
    // Aceptamos ambos.
    const errorCode = data?.error || data?.code || data?.errorCode;
    const message = data?.message || data?.detail || data?.error || data?.title || 'Error en la solicitud';

    // Dispatch specific error events
    if (errorCode === 'PLAN_LIMIT_REACHED') {
      window.dispatchEvent(new CustomEvent('planLimitReached', {
        detail: {
          message,
          limitType: data?.limitType,
          currentCount: data?.currentCount,
          maxCount: data?.maxCount,
          currentPlan: data?.currentPlan,
          upgradeUrl: data?.upgradeUrl || '/billing',
        }
      }));
    }

    if (errorCode === 'SUBSCRIPTION_INACTIVE' || errorCode === 'SUBSCRIPTION_PAST_DUE') {
      window.dispatchEvent(new CustomEvent('subscriptionIssue', {
        detail: {
          message,
          status: data?.status,
          billingUrl: data?.billingUrl || '/billing',
        }
      }));
    }

    // Rate limit del API Platform (/v1/*). Detectado por status code — el backend retorna
    // un body RFC 7807 con errorCode="rate_limit_error" + header Retry-After.
    // El listener global en App.tsx muestra un toast con countdown.
    if (response.status === 429) {
      const retryAfterHeader = response.headers.get('Retry-After') ?? response.headers.get('retry-after');
      const retryAfterSeconds = retryAfterHeader ? parseInt(retryAfterHeader, 10) : NaN;
      window.dispatchEvent(new CustomEvent('rateLimitExceeded', {
        detail: {
          message: data?.detail || data?.title || 'Has alcanzado el límite de requests.',
          retryAfterSeconds: Number.isFinite(retryAfterSeconds) ? retryAfterSeconds : 60,
          path: data?.instance ?? '',
          requestId: data?.requestId ?? '',
        }
      }));
    }

    throw new ApiException(response.status, message, errorCode, data);
  }

  return data;
}

function getHeaders(): HeadersInit {
  const token = localStorage.getItem('auth_token');
  const headers: HeadersInit = {
    'Content-Type': 'application/json',
  };

  if (token) {
    headers['Authorization'] = `Bearer ${token}`;
  }

  return headers;
}

export const api = {
  async get<T>(endpoint: string): Promise<T> {
    const url = `${API_BASE_URL}${endpoint}`;
    const headers = getHeaders();
    const response = await fetch(url, {
      method: 'GET',
      headers,
    });
    return handleResponse<T>(response, { url, method: 'GET', headers });
  },

  async post<T>(endpoint: string, body?: any): Promise<T> {
    const url = `${API_BASE_URL}${endpoint}`;
    const headers = getHeaders();
    const response = await fetch(url, {
      method: 'POST',
      headers,
      body: body ? JSON.stringify(body) : undefined,
    });
    return handleResponse<T>(response, { url, method: 'POST', headers, body });
  },

  async put<T>(endpoint: string, body?: any): Promise<T> {
    const url = `${API_BASE_URL}${endpoint}`;
    const headers = getHeaders();
    const response = await fetch(url, {
      method: 'PUT',
      headers,
      body: body ? JSON.stringify(body) : undefined,
    });
    return handleResponse<T>(response, { url, method: 'PUT', headers, body });
  },

  async patch<T>(endpoint: string, body?: any): Promise<T> {
    const url = `${API_BASE_URL}${endpoint}`;
    const headers = getHeaders();
    const response = await fetch(url, {
      method: 'PATCH',
      headers,
      body: body ? JSON.stringify(body) : undefined,
    });
    return handleResponse<T>(response, { url, method: 'PATCH', headers, body });
  },

  async delete<T>(endpoint: string): Promise<T> {
    const url = `${API_BASE_URL}${endpoint}`;
    const headers = getHeaders();
    const response = await fetch(url, {
      method: 'DELETE',
      headers,
    });
    return handleResponse<T>(response, { url, method: 'DELETE', headers });
  },

  async download(endpoint: string, filename: string): Promise<void> {
    const doFetch = (token: string | null) => {
      const headers: HeadersInit = {};
      if (token) headers['Authorization'] = `Bearer ${token}`;
      return fetch(`${API_BASE_URL}${endpoint}`, { method: 'GET', headers });
    };

    let response = await doFetch(localStorage.getItem('auth_token'));

    // 401: intentar refresh token igual que handleResponse()
    if (response.status === 401) {
      const newToken = await tryRefreshToken();
      if (newToken) {
        response = await doFetch(newToken);
      }
      if (response.status === 401) {
        localStorage.clear();
        window.location.href = '/login';
        throw new ApiException(401, 'Sesión expirada. Por favor inicia sesión nuevamente.');
      }
    }

    if (!response.ok) {
      let message = 'Error al descargar archivo';
      try {
        const errorData = await response.json();
        message = errorData?.message || message;
      } catch { /* body no parseable */ }
      throw new ApiException(response.status, message);
    }

    const blob = await response.blob();
    const url = window.URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = filename;
    document.body.appendChild(a);
    a.click();
    window.URL.revokeObjectURL(url);
    a.remove();
  },
};
