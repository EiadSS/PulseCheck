import type {
  AnalyticsRange,
  AnalyticsSummary,
  AuthResponse,
  DashboardSummary,
  Incident,
  MonitorCheck,
  MonitorDetail,
  MonitorRequest,
  MonitorResponseTimePoint,
  MonitorSummary,
  Notification,
  NotificationPreferences,
  NotificationUnreadCount,
  PublicStatusPage,
  SloSummary,
  User
} from '../types';

export const API_BASE_URL = import.meta.env.VITE_API_URL ?? 'http://localhost:5080';
const tokenKey = 'pulsecheck.token';
const invalidApiBaseUrl =
  API_BASE_URL.trim().length > 0 && !API_BASE_URL.startsWith('http://') && !API_BASE_URL.startsWith('https://');
const apiUnavailableMessage = 'Unable to reach PulseCheck API. Please try again shortly.';

export class ApiError extends Error {
  constructor(
    message: string,
    public readonly messages: string[] = [message],
    public readonly status?: number
  ) {
    super(message);
    this.name = 'ApiError';
  }
}

export function getStoredToken() {
  return localStorage.getItem(tokenKey);
}

export function storeToken(token: string) {
  localStorage.setItem(tokenKey, token);
}

export function clearStoredToken() {
  localStorage.removeItem(tokenKey);
}

export async function request<T>(path: string, options: RequestInit = {}): Promise<T> {
  if (invalidApiBaseUrl) {
    throw new ApiError(apiUnavailableMessage, [apiUnavailableMessage]);
  }

  const token = getStoredToken();
  const headers = new Headers(options.headers);

  if (!headers.has('Content-Type') && options.body) {
    headers.set('Content-Type', 'application/json');
  }

  if (token) {
    headers.set('Authorization', `Bearer ${token}`);
  }

  let response: Response;
  try {
    response = await fetch(`${API_BASE_URL}${path}`, {
      ...options,
      headers
    });
  } catch {
    throw new ApiError(apiUnavailableMessage, [apiUnavailableMessage]);
  }

  if (response.status === 204) {
    return undefined as T;
  }

  const contentType = response.headers.get('content-type') ?? '';
  const body = contentType.includes('application/json') ? await response.json() : await response.text();

  if (!response.ok) {
    const messages = getErrorMessages(body);
    throw new ApiError(messages[0] ?? `Request failed with ${response.status}`, messages, response.status);
  }

  return body as T;
}

export const api = {
  register(email: string, password: string, workspaceName: string) {
    return request<AuthResponse>('/api/auth/register', {
      method: 'POST',
      body: JSON.stringify({ email, password, workspaceName })
    });
  },
  login(email: string, password: string) {
    return request<AuthResponse>('/api/auth/login', {
      method: 'POST',
      body: JSON.stringify({ email, password })
    });
  },
  me() {
    return request<User>('/api/auth/me');
  },
  monitors() {
    return request<MonitorSummary[]>('/api/monitors');
  },
  createMonitor(payload: MonitorRequest) {
    return request<MonitorSummary>('/api/monitors', {
      method: 'POST',
      body: JSON.stringify(payload)
    });
  },
  monitor(id: string) {
    return request<MonitorDetail>(`/api/monitors/${id}`);
  },
  updateMonitor(id: string, payload: MonitorRequest) {
    return request<MonitorDetail>(`/api/monitors/${id}`, {
      method: 'PUT',
      body: JSON.stringify(payload)
    });
  },
  pauseMonitor(id: string) {
    return request<MonitorSummary>(`/api/monitors/${id}/pause`, { method: 'POST' });
  },
  resumeMonitor(id: string) {
    return request<MonitorSummary>(`/api/monitors/${id}/resume`, { method: 'POST' });
  },
  runMonitorCheck(id: string) {
    return request<MonitorDetail>(`/api/monitors/${id}/check`, { method: 'POST' });
  },
  deleteMonitor(id: string) {
    return request<void>(`/api/monitors/${id}`, { method: 'DELETE' });
  },
  checks(id: string, range: '24h' | '7d' | '30d') {
    return request<MonitorCheck[]>(`/api/monitors/${id}/checks?range=${range}`);
  },
  responseTimes(id: string, range: '24h' | '7d' | '30d') {
    return request<MonitorResponseTimePoint[]>(`/api/monitors/${id}/response-times?range=${range}`);
  },
  incidents(id: string) {
    return request<Incident[]>(`/api/monitors/${id}/incidents`);
  },
  dashboardSummary() {
    return request<DashboardSummary>('/api/dashboard/summary');
  },
  sloSummary() {
    return request<SloSummary>('/api/dashboard/slo');
  },
  trackAnalyticsEvent(payload: { eventType: 'PageView'; path: string; visitorId?: string | null }) {
    return request<void>('/api/analytics/events', {
      method: 'POST',
      body: JSON.stringify(payload)
    });
  },
  analyticsSummary(range: AnalyticsRange) {
    return request<AnalyticsSummary>(`/api/admin/analytics/summary?range=${range}`);
  },
  notifications() {
    return request<Notification[]>('/api/notifications');
  },
  notificationUnreadCount() {
    return request<NotificationUnreadCount>('/api/notifications/unread-count');
  },
  markNotificationRead(id: string) {
    return request<Notification>(`/api/notifications/${id}/read`, { method: 'POST' });
  },
  markAllNotificationsRead() {
    return request<NotificationUnreadCount>('/api/notifications/read-all', { method: 'POST' });
  },
  deleteNotification(id: string) {
    return request<void>(`/api/notifications/${id}`, { method: 'DELETE' });
  },
  notificationPreferences() {
    return request<NotificationPreferences>('/api/account/notification-preferences');
  },
  updateNotificationPreferences(payload: Pick<NotificationPreferences, 'emailAlertsEnabled'>) {
    return request<NotificationPreferences>('/api/account/notification-preferences', {
      method: 'PUT',
      body: JSON.stringify(payload)
    });
  },
  publicStatus(slug: string) {
    return request<PublicStatusPage>(`/api/public/status/${slug}`);
  }
};

function getErrorMessages(body: unknown) {
  if (typeof body === 'string') {
    return [cleanRawErrorMessage(body)].filter(Boolean);
  }

  if (!body || typeof body !== 'object') {
    return [];
  }

  const problem = body as {
    title?: string;
    detail?: string;
    errors?: Record<string, string[] | string>;
  };

  if (problem.errors) {
    return Object.entries(problem.errors)
      .flatMap(([field, value]) => {
        const messages = Array.isArray(value) ? value : [value];
        return messages.map((message) => cleanValidationMessage(field, message));
      })
      .filter(Boolean);
  }

  return [problem.detail ?? problem.title].filter(Boolean) as string[];
}

function cleanRawErrorMessage(message: string) {
  const normalized = message.toLowerCase();

  if (
    normalized.includes('not_found') ||
    normalized.includes('the page could not be found') ||
    normalized.includes('404') ||
    normalized.includes('failed to fetch')
  ) {
    return apiUnavailableMessage;
  }

  return message;
}

function cleanValidationMessage(field: string, message: string) {
  const normalized = message.toLowerCase();
  const normalizedField = field.toLowerCase();

  if (normalizedField.includes('email') && normalized.includes('not a valid')) {
    return 'Enter a valid email address.';
  }

  if (normalizedField.includes('password')) {
    if (normalized.includes('uppercase')) return 'Add at least one uppercase letter.';
    if (normalized.includes('lowercase')) return 'Add at least one lowercase letter.';
    if (normalized.includes('digit') || normalized.includes('number')) return 'Add at least one number.';
    if (normalized.includes('non alphanumeric') || normalized.includes('symbol')) return 'Add at least one symbol.';

    const lengthMatch = message.match(/at least (\d+) characters/i);
    if (lengthMatch) return `Use at least ${lengthMatch[1]} characters.`;
  }

  return message;
}
