import { Activity, AlertCircle, BarChart3, Clock3, Mail, MonitorCheck, Users } from 'lucide-react';
import { useEffect, useMemo, useState } from 'react';
import { Bar, BarChart, CartesianGrid, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts';
import { ApiError, api } from '../api/client';
import { MetricCard } from '../components/MetricCard';
import { StatusBadge } from '../components/StatusBadge';
import type { AnalyticsRange, AnalyticsSummary, MonitorStatus, NotificationEmailStatus } from '../types';

const ranges: { value: AnalyticsRange; label: string }[] = [
  { value: '24h', label: '24h' },
  { value: '7d', label: '7d' },
  { value: '30d', label: '30d' },
  { value: 'all', label: 'All time' }
];
const checkStatusOrder: MonitorStatus[] = ['Up', 'Degraded', 'Error', 'Down', 'Paused'];

export function AnalyticsPage() {
  const [range, setRange] = useState<AnalyticsRange>('7d');
  const [summary, setSummary] = useState<AnalyticsSummary | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    let active = true;

    async function load() {
      setIsLoading(true);
      setError(null);

      try {
        const data = await api.analyticsSummary(range);
        if (active) {
          setSummary(data);
        }
      } catch (err) {
        if (active) {
          setError(toAnalyticsError(err));
        }
      } finally {
        if (active) {
          setIsLoading(false);
        }
      }
    }

    void load();
    return () => {
      active = false;
    };
  }, [range]);

  const signupSeries = useMemo(
    () =>
      summary?.newUsersOverTime.map((point) => ({
        label: formatBucket(point.periodStart, summary.range),
        count: point.count
      })) ?? [],
    [summary]
  );

  const emailSent = countEmailStatus(summary, 'Sent');
  const emailFailed = countEmailStatus(summary, 'Failed');
  const emailNotConfigured = countEmailStatus(summary, 'NotConfigured');

  return (
    <div className="space-y-6">
      <div className="flex flex-col justify-between gap-4 sm:flex-row sm:items-center">
        <div>
          <p className="text-sm font-semibold uppercase text-sky-700">Admin analytics</p>
          <h1 className="mt-1 text-3xl font-semibold tracking-normal text-slate-950">Analytics</h1>
          <p className="mt-2 max-w-3xl text-sm leading-6 text-slate-500">
            Track product usage, monitor activity, and alert health for PulseCheck.
          </p>
        </div>
        <div className="inline-flex rounded-lg border border-slate-200 bg-white p-1">
          {ranges.map((item) => (
            <button
              key={item.value}
              type="button"
              onClick={() => setRange(item.value)}
              className={`focus-ring rounded-md px-3 py-2 text-sm font-semibold ${
                range === item.value ? 'bg-slate-950 text-white' : 'text-slate-600 hover:bg-slate-50 hover:text-slate-950'
              }`}
            >
              {item.label}
            </button>
          ))}
        </div>
      </div>

      {error && <div className="rounded-lg bg-rose-50 px-4 py-3 text-sm font-medium text-rose-700">{error}</div>}

      {isLoading && !summary ? (
        <div className="rounded-lg border border-slate-200 bg-white p-8 text-sm font-medium text-slate-500">Loading analytics...</div>
      ) : summary ? (
        <>
          <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
            <MetricCard label="Total users" value={summary.totalUsers} icon={Users} tone="sky" />
            <MetricCard label="Active users" value={summary.activeUsers} icon={Activity} tone="emerald" />
            <MetricCard label="Page views" value={summary.pageViews} icon={BarChart3} tone="slate" />
            <MetricCard label="Monitors" value={summary.totalMonitors} icon={MonitorCheck} tone="amber" />
          </div>

          <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
            <MetricCard label="New users" value={summary.newUsers} icon={Users} tone="sky" />
            <MetricCard label="Monitors created" value={summary.monitorsCreated} icon={MonitorCheck} tone="emerald" />
            <MetricCard label="Public status views" value={summary.publicStatusPageViews} icon={BarChart3} tone="slate" />
            <MetricCard
              label="Avg response"
              value={summary.averageResponseTimeMs == null ? '-' : `${summary.averageResponseTimeMs.toFixed(0)} ms`}
              icon={Clock3}
              tone="amber"
            />
          </div>

          <div className="grid gap-6 xl:grid-cols-[1fr_360px]">
            <section className="rounded-lg border border-slate-200 bg-white p-5 shadow-sm">
              <div className="flex items-start justify-between gap-4">
                <div>
                  <h2 className="text-lg font-semibold text-slate-950">New users over time</h2>
                  <p className="mt-1 text-sm text-slate-500">Account creation trend for the selected range.</p>
                </div>
                <span className="rounded-full bg-sky-50 px-2.5 py-1 text-xs font-semibold text-sky-700">
                  {formatRangeLabel(summary.range)}
                </span>
              </div>
              <div className="mt-4 h-72">
                {summary.newUsersOverTime.some((point) => point.count > 0) ? (
                  <ResponsiveContainer width="100%" height="100%">
                    <BarChart data={signupSeries}>
                      <CartesianGrid strokeDasharray="3 3" stroke="#e2e8f0" />
                      <XAxis dataKey="label" tickLine={false} axisLine={false} tick={{ fontSize: 12 }} />
                      <YAxis allowDecimals={false} tickLine={false} axisLine={false} tick={{ fontSize: 12 }} width={32} />
                      <Tooltip />
                      <Bar dataKey="count" name="New users" fill="#0284c7" radius={[6, 6, 0, 0]} />
                    </BarChart>
                  </ResponsiveContainer>
                ) : (
                  <EmptyState message="No new users in this window yet." />
                )}
              </div>
            </section>

            <section className="rounded-lg border border-slate-200 bg-white p-5 shadow-sm">
              <h2 className="text-lg font-semibold text-slate-950">Top app pages</h2>
              <p className="mt-1 text-sm text-slate-500">Most viewed PulseCheck routes, such as dashboard, analytics, auth, and status pages.</p>
              <div className="mt-4 space-y-3">
                {summary.topPages.length === 0 ? (
                  <EmptyState message="No page views collected yet." compact />
                ) : (
                  summary.topPages.map((page) => (
                    <ProgressRow
                      key={page.path}
                      label={formatAppPageLabel(page.path)}
                      value={page.views}
                      max={summary.topPages[0]?.views ?? 1}
                    />
                  ))
                )}
              </div>
            </section>
          </div>

          <section className="rounded-lg border border-slate-200 bg-white p-5 shadow-sm">
            <div className="flex items-center gap-2">
              <MonitorCheck className="h-5 w-5 text-sky-600" aria-hidden="true" />
              <h2 className="text-lg font-semibold text-slate-950">Monitor activity</h2>
            </div>
            <p className="mt-1 text-sm text-slate-500">Monitored URLs with check activity in the selected range.</p>
            <div className="mt-4 overflow-x-auto">
              {summary.monitorActivity.filter((monitor) => monitor.checkCount > 0).length === 0 ? (
                <EmptyState message="Monitor activity appears after scheduled or manual checks run." compact />
              ) : (
                <table className="min-w-full divide-y divide-slate-200 text-sm">
                  <thead>
                    <tr className="text-left text-xs font-semibold uppercase text-slate-500">
                      <th className="py-2 pr-4">Monitor</th>
                      <th className="py-2 pr-4">URL</th>
                      <th className="py-2 pr-4">Status</th>
                      <th className="py-2 pr-4">Checks</th>
                      <th className="py-2 pr-4">Last checked</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-slate-100">
                    {summary.monitorActivity
                      .filter((monitor) => monitor.checkCount > 0)
                      .map((monitor) => (
                        <tr key={monitor.id}>
                          <td className="py-3 pr-4 font-semibold text-slate-950">{monitor.name}</td>
                          <td className="max-w-md truncate py-3 pr-4 text-slate-600" title={monitor.url}>
                            {monitor.url}
                          </td>
                          <td className="py-3 pr-4">
                            <StatusBadge status={monitor.currentStatus} />
                          </td>
                          <td className="py-3 pr-4 font-semibold text-slate-950">{monitor.checkCount}</td>
                          <td className="py-3 pr-4 text-slate-600">
                            {monitor.lastCheckedAt ? formatDateTime(monitor.lastCheckedAt) : '-'}
                          </td>
                        </tr>
                      ))}
                  </tbody>
                </table>
              )}
            </div>
          </section>

          <div className="grid gap-6 xl:grid-cols-3">
            <section className="rounded-lg border border-slate-200 bg-white p-5 shadow-sm">
              <h2 className="text-lg font-semibold text-slate-950">Checks</h2>
              <p className="mt-1 text-sm text-slate-500">{summary.monitorChecks} checks recorded.</p>
              <div className="mt-4 space-y-3">
                {checkStatusOrder.map((status) => (
                  <ProgressRow
                    key={status}
                    label={status}
                    value={countCheckStatus(summary, status)}
                    max={Math.max(1, summary.monitorChecks)}
                  />
                ))}
              </div>
            </section>

            <section className="rounded-lg border border-slate-200 bg-white p-5 shadow-sm">
              <h2 className="text-lg font-semibold text-slate-950">Incidents</h2>
              <p className="mt-1 text-sm text-slate-500">Open and recovery flow health.</p>
              <div className="mt-5 grid grid-cols-2 gap-3">
                <MiniMetric label="Opened" value={summary.incidentsOpened} />
                <MiniMetric label="Resolved" value={summary.incidentsResolved} />
                <MiniMetric label="Notifications" value={summary.notificationsCreated} />
                <MiniMetric label="Monitors/user" value={summary.averageMonitorsPerUser.toFixed(2)} />
              </div>
            </section>

            <section className="rounded-lg border border-slate-200 bg-white p-5 shadow-sm">
              <div className="flex items-center gap-2">
                <Mail className="h-5 w-5 text-sky-600" aria-hidden="true" />
                <h2 className="text-lg font-semibold text-slate-950">Email alert health</h2>
              </div>
              <p className="mt-1 text-sm text-slate-500">Delivery status for alert emails.</p>
              <div className="mt-5 grid grid-cols-2 gap-3">
                <MiniMetric label="Sent" value={emailSent} />
                <MiniMetric label="Failed" value={emailFailed} />
                <MiniMetric label="Not configured" value={emailNotConfigured} />
                <MiniMetric label="Skipped" value={countEmailStatus(summary, 'Skipped')} />
              </div>
            </section>
          </div>

          <section className="rounded-lg border border-slate-200 bg-white p-5 shadow-sm">
            <div className="flex items-center gap-2">
              <Users className="h-5 w-5 text-sky-600" aria-hidden="true" />
              <h2 className="text-lg font-semibold text-slate-950">Recent signups</h2>
            </div>
            <div className="mt-4 overflow-x-auto">
              {summary.recentSignups.length === 0 ? (
                <EmptyState message="No accounts have been created yet." compact />
              ) : (
                <table className="min-w-full divide-y divide-slate-200 text-sm">
                  <thead>
                    <tr className="text-left text-xs font-semibold uppercase text-slate-500">
                      <th className="py-2 pr-4">Email</th>
                      <th className="py-2 pr-4">Workspace</th>
                      <th className="py-2 pr-4">Created</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-slate-100">
                    {summary.recentSignups.map((signup) => (
                      <tr key={signup.id}>
                        <td className="py-3 pr-4 font-semibold text-slate-950">{signup.email}</td>
                        <td className="py-3 pr-4 text-slate-600">{signup.workspaceName}</td>
                        <td className="py-3 pr-4 text-slate-600">{formatDateTime(signup.createdAt)}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              )}
            </div>
          </section>
        </>
      ) : null}
    </div>
  );
}

function toAnalyticsError(error: unknown) {
  if (error instanceof ApiError && error.status === 403) {
    return 'Analytics is only available to the configured app owner.';
  }

  return error instanceof Error ? error.message : 'Unable to load analytics.';
}

function countCheckStatus(summary: AnalyticsSummary | null, status: MonitorStatus) {
  return summary?.checkStatusCounts.find((item) => item.status === status)?.count ?? 0;
}

function countEmailStatus(summary: AnalyticsSummary | null, status: NotificationEmailStatus) {
  return summary?.emailStatusCounts.find((item) => item.status === status)?.count ?? 0;
}

function ProgressRow({ label, value, max }: { label: string; value: number; max: number }) {
  const width = `${Math.max(2, Math.round((value / Math.max(1, max)) * 100))}%`;

  return (
    <div>
      <div className="flex items-center justify-between gap-3 text-sm">
        <span className="truncate font-medium text-slate-600">{label}</span>
        <span className="font-semibold text-slate-950">{value}</span>
      </div>
      <div className="mt-1 h-2 overflow-hidden rounded-full bg-slate-100">
        <div className="h-full rounded-full bg-sky-600" style={{ width }} />
      </div>
    </div>
  );
}

function MiniMetric({ label, value }: { label: string; value: number | string }) {
  return (
    <div className="rounded-lg bg-slate-50 p-3">
      <p className="text-xs font-semibold uppercase text-slate-500">{label}</p>
      <p className="mt-2 text-2xl font-semibold text-slate-950">{value}</p>
    </div>
  );
}

function EmptyState({ message, compact = false }: { message: string; compact?: boolean }) {
  return (
    <div className={`flex items-center justify-center rounded-lg border border-dashed border-slate-300 bg-slate-50 ${compact ? 'p-4' : 'h-full p-6'}`}>
      <div className="flex items-center gap-2 text-sm font-medium text-slate-500">
        <AlertCircle className="h-4 w-4" aria-hidden="true" />
        {message}
      </div>
    </div>
  );
}

function formatBucket(value: string, range: AnalyticsRange) {
  const date = new Date(value);

  if (range === '24h') {
    return date.toLocaleTimeString(undefined, { hour: 'numeric' });
  }

  if (range === 'all') {
    return date.toLocaleDateString(undefined, { month: 'short', year: 'numeric' });
  }

  return date.toLocaleDateString(undefined, { month: 'short', day: 'numeric' });
}

function formatRangeLabel(range: AnalyticsRange) {
  if (range === 'all') return 'All time';

  return range;
}

function formatDateTime(value: string) {
  return new Date(value).toLocaleString(undefined, {
    month: 'short',
    day: 'numeric',
    hour: 'numeric',
    minute: '2-digit'
  });
}

function formatAppPageLabel(path: string) {
  if (path === '/') return 'Landing page';
  if (path === '/dashboard') return 'Dashboard';
  if (path === '/analytics') return 'Analytics';
  if (path === '/slo') return 'Reliability';
  if (path === '/login') return 'Sign in';
  if (path === '/register') return 'Create account';
  if (path === '/monitors/new') return 'New monitor';
  if (path.startsWith('/monitors/') && path.endsWith('/edit')) return 'Edit monitor';
  if (path.startsWith('/monitors/')) return 'Monitor details';
  if (path.startsWith('/status/')) return 'Public status page';

  return path;
}
