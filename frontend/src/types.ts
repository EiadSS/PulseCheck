export type MonitorType = 'Website' | 'Api';
export type MonitorStatus = 'Up' | 'Degraded' | 'Error' | 'Down' | 'Paused';
export type IncidentStatus = 'Open' | 'Resolved';
export type SslCertificateStatus = 'NotApplicable' | 'Valid' | 'ExpiringSoon' | 'Critical' | 'Expired' | 'Unavailable';
export type NotificationType = 'MonitorFailed' | 'MonitorRecovered' | 'SslCertificateWarning' | 'SslCertificateCritical';
export type NotificationEmailStatus = 'NotConfigured' | 'Sent' | 'Failed' | 'Skipped';

export interface User {
  id: string;
  email: string;
  workspaceName: string;
  publicStatusSlug: string;
  emailAlertsEnabled: boolean;
  isAdmin: boolean;
}

export interface AuthResponse {
  token: string;
  user: User;
}

export interface MonitorRequest {
  name: string;
  url: string;
  type: MonitorType;
  checkIntervalSeconds: number;
  timeoutSeconds: number;
  degradedThresholdMs: number;
  expectedStatusCode: number;
  expectedKeyword?: string | null;
  isPublic: boolean;
}

export interface MonitorSummary {
  id: string;
  name: string;
  url: string;
  type: MonitorType;
  currentStatus: MonitorStatus;
  isPaused: boolean;
  isPublic: boolean;
  lastCheckedAt?: string | null;
  lastStatusCode?: number | null;
  lastResponseTimeMs?: number | null;
  lastErrorMessage?: string | null;
  sslCertificateStatus: SslCertificateStatus;
  sslCertificateExpiresAt?: string | null;
  sslCertificateDaysRemaining?: number | null;
  lastSslErrorMessage?: string | null;
  uptimePercentage: number;
  openIncidentCount: number;
}

export interface MonitorDetail extends MonitorSummary {
  checkIntervalSeconds: number;
  timeoutSeconds: number;
  degradedThresholdMs: number;
  expectedStatusCode: number;
  expectedKeyword?: string | null;
  uptime24Hours: number;
  uptime7Days: number;
  uptime30Days: number;
}

export interface MonitorCheck {
  id: string;
  status: MonitorStatus;
  statusCode?: number | null;
  responseTimeMs?: number | null;
  errorMessage?: string | null;
  checkedAt: string;
}

export interface MonitorResponseTimePoint {
  checkedAt: string;
  responseTimeMs: number;
  checkCount: number;
}

export interface Incident {
  id: string;
  status: IncidentStatus;
  startedStatus: MonitorStatus;
  resolvedStatus?: MonitorStatus | null;
  title: string;
  summary?: string | null;
  startedAt: string;
  resolvedAt?: string | null;
}

export interface DashboardSummary {
  totalMonitors: number;
  up: number;
  degraded: number;
  error: number;
  down: number;
  paused: number;
  openIncidents: number;
  averageUptime24Hours: number;
}

export interface Notification {
  id: string;
  type: NotificationType;
  monitorId?: string | null;
  incidentId?: string | null;
  title: string;
  message: string;
  isRead: boolean;
  emailStatus: NotificationEmailStatus;
  emailErrorMessage?: string | null;
  createdAt: string;
  readAt?: string | null;
}

export interface NotificationUnreadCount {
  unreadCount: number;
}

export interface NotificationPreferences {
  emailAlertsEnabled: boolean;
  emailDeliveryConfigured: boolean;
}

export interface SloSummary {
  targetPercentage: number;
  windows: SloWindow[];
  monitors: SloMonitor[];
}

export interface SloWindow {
  range: '24h' | '7d' | '30d';
  uptimePercentage: number;
  errorBudgetUsedPercentage: number;
  errorBudgetRemainingPercentage: number;
  isCompliant: boolean;
}

export interface SloMonitor {
  id: string;
  name: string;
  currentStatus: MonitorStatus;
  uptime24Hours: number;
  uptime7Days: number;
  uptime30Days: number;
  errorBudgetUsed30Days: number;
  isCompliant30Days: boolean;
}

export interface PublicStatusPage {
  slug: string;
  title: string;
  overallStatus: MonitorStatus;
  uptime24Hours: number;
  uptime7Days: number;
  uptime30Days: number;
  monitors: PublicMonitor[];
  recentIncidents: PublicIncident[];
}

export interface PublicMonitor {
  id: string;
  name: string;
  type: MonitorType;
  currentStatus: MonitorStatus;
  lastCheckedAt?: string | null;
  lastResponseTimeMs?: number | null;
  uptime24Hours: number;
  uptime7Days: number;
  uptime30Days: number;
}

export interface PublicIncident {
  id: string;
  monitorName: string;
  status: IncidentStatus;
  title: string;
  startedAt: string;
  resolvedAt?: string | null;
}

export type AnalyticsRange = '24h' | '7d' | '30d';

export interface AnalyticsSummary {
  range: AnalyticsRange;
  since: string;
  generatedAt: string;
  totalUsers: number;
  newUsers: number;
  activeUsers: number;
  totalMonitors: number;
  monitorsCreated: number;
  averageMonitorsPerUser: number;
  pageViews: number;
  publicStatusPageViews: number;
  monitorChecks: number;
  averageResponseTimeMs?: number | null;
  incidentsOpened: number;
  incidentsResolved: number;
  notificationsCreated: number;
  topPages: AnalyticsTopPage[];
  checkStatusCounts: AnalyticsMonitorStatusCount[];
  emailStatusCounts: AnalyticsEmailStatusCount[];
  monitorActivity: AnalyticsMonitorActivity[];
  newUsersOverTime: AnalyticsSeriesPoint[];
  recentSignups: AnalyticsRecentSignup[];
}

export interface AnalyticsTopPage {
  path: string;
  views: number;
}

export interface AnalyticsMonitorStatusCount {
  status: MonitorStatus;
  count: number;
}

export interface AnalyticsEmailStatusCount {
  status: NotificationEmailStatus;
  count: number;
}

export interface AnalyticsMonitorActivity {
  id: string;
  name: string;
  url: string;
  currentStatus: MonitorStatus;
  checkCount: number;
  lastCheckedAt?: string | null;
}

export interface AnalyticsSeriesPoint {
  periodStart: string;
  count: number;
}

export interface AnalyticsRecentSignup {
  id: string;
  email: string;
  workspaceName: string;
  createdAt: string;
}
