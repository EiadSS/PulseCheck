import { ArrowLeft, Edit, Pause, Play, RefreshCw } from 'lucide-react';
import { useEffect, useMemo, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import {
  CartesianGrid,
  Line,
  LineChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis
} from 'recharts';
import { api } from '../api/client';
import { SslBadge } from '../components/SslBadge';
import { StatusBadge } from '../components/StatusBadge';
import type { Incident, MonitorCheck, MonitorDetail, MonitorResponseTimePoint } from '../types';

type Range = '24h' | '7d' | '30d';
const recentChecksRange: Range = '30d';

export function MonitorDetailsPage() {
  const { id } = useParams();
  const [monitor, setMonitor] = useState<MonitorDetail | null>(null);
  const [checks, setChecks] = useState<MonitorCheck[]>([]);
  const [responseTimes, setResponseTimes] = useState<MonitorResponseTimePoint[]>([]);
  const [incidents, setIncidents] = useState<Incident[]>([]);
  const [range, setRange] = useState<Range>('24h');
  const [isChecking, setIsChecking] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function loadDetails() {
    if (!id) {
      return;
    }

    const [monitorData, checkData, incidentData] = await Promise.all([
      api.monitor(id),
      api.checks(id, recentChecksRange),
      api.incidents(id)
    ]);
    setMonitor(monitorData);
    setChecks(checkData);
    setIncidents(incidentData);
  }

  async function loadResponseTimes(currentRange = range) {
    if (!id) {
      return;
    }

    setResponseTimes(await api.responseTimes(id, currentRange));
  }

  useEffect(() => {
    loadDetails().catch((err) => setError(err instanceof Error ? err.message : 'Unable to load monitor.'));
  }, [id]);

  useEffect(() => {
    loadResponseTimes(range).catch((err) => setError(err instanceof Error ? err.message : 'Unable to load response times.'));
  }, [id, range]);

  const chartData = useMemo(
    () =>
      responseTimes.map((point) => ({
        time: new Date(point.checkedAt).toLocaleString([], { month: 'short', day: 'numeric', hour: 'numeric', minute: '2-digit' }),
        responseTimeMs: point.responseTimeMs,
        checkCount: point.checkCount
      })),
    [responseTimes]
  );

  if (error) {
    return <div className="rounded-lg bg-rose-50 px-4 py-3 text-sm font-medium text-rose-700">{error}</div>;
  }

  if (!monitor || !id) {
    return <div className="rounded-lg border border-slate-200 bg-white p-8 text-sm font-medium text-slate-500">Loading monitor...</div>;
  }

  async function togglePause() {
    if (!monitor) {
      return;
    }

    if (monitor.isPaused) {
      await api.resumeMonitor(monitor.id);
    } else {
      await api.pauseMonitor(monitor.id);
    }

    await loadDetails();
    await loadResponseTimes(range);
  }

  async function runCheckNow() {
    if (!monitor) {
      return;
    }

    setError(null);
    setIsChecking(true);

    try {
      await api.runMonitorCheck(monitor.id);
      await Promise.all([loadDetails(), loadResponseTimes(range)]);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Unable to run check.');
    } finally {
      setIsChecking(false);
    }
  }

  return (
    <div className="space-y-6">
      <div className="flex flex-col justify-between gap-4 lg:flex-row lg:items-start">
        <div>
          <Link
            to="/dashboard"
            className="focus-ring inline-flex w-max items-center gap-2 rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm font-semibold text-slate-700 shadow-sm hover:bg-slate-50"
          >
            <ArrowLeft className="h-4 w-4" aria-hidden="true" />
            Back to dashboard
          </Link>
          <div className="mt-4 flex flex-wrap items-center gap-3">
            <h1 className="text-3xl font-semibold text-slate-950">{monitor.name}</h1>
            <StatusBadge status={monitor.currentStatus} />
            {monitor.sslCertificateStatus !== 'NotApplicable' && (
              <SslBadge status={monitor.sslCertificateStatus} daysRemaining={monitor.sslCertificateDaysRemaining} />
            )}
          </div>
          <a href={monitor.url} target="_blank" rel="noreferrer" className="mt-2 block break-all text-sm text-slate-500 hover:text-sky-700">
            {monitor.url}
          </a>
        </div>
        <div className="flex gap-2">
          <button
            type="button"
            onClick={() => void runCheckNow()}
            disabled={monitor.isPaused || isChecking}
            className="focus-ring inline-flex items-center gap-2 rounded-lg border border-slate-300 bg-white px-4 py-2.5 text-sm font-semibold text-slate-700 hover:bg-sky-50 hover:text-sky-700 disabled:cursor-not-allowed disabled:opacity-60"
            title={monitor.isPaused ? 'Resume monitor to run a check' : 'Run check now'}
          >
            <RefreshCw className={`h-4 w-4 ${isChecking ? 'animate-spin' : ''}`} aria-hidden="true" />
            {isChecking ? 'Checking...' : 'Run check now'}
          </button>
          <button
            type="button"
            onClick={() => void togglePause()}
            className="focus-ring inline-flex items-center gap-2 rounded-lg border border-slate-300 bg-white px-4 py-2.5 text-sm font-semibold text-slate-700 hover:bg-slate-50"
          >
            {monitor.isPaused ? <Play className="h-4 w-4" aria-hidden="true" /> : <Pause className="h-4 w-4" aria-hidden="true" />}
            {monitor.isPaused ? 'Resume' : 'Pause'}
          </button>
          <Link
            to={`/monitors/${monitor.id}/edit`}
            className="focus-ring inline-flex items-center gap-2 rounded-lg bg-slate-950 px-4 py-2.5 text-sm font-semibold text-white hover:bg-slate-800"
          >
            <Edit className="h-4 w-4" aria-hidden="true" />
            Edit
          </Link>
        </div>
      </div>

      <div className="grid gap-4 md:grid-cols-3">
        <Uptime label="Last 24 hours" value={monitor.uptime24Hours} />
        <Uptime label="Last 7 days" value={monitor.uptime7Days} />
        <Uptime label="Last 30 days" value={monitor.uptime30Days} />
      </div>

      <section className="rounded-lg border border-slate-200 bg-white p-5 shadow-sm">
        <div className="flex flex-col justify-between gap-3 sm:flex-row sm:items-center">
          <div>
            <h2 className="text-lg font-semibold text-slate-950">SSL certificate</h2>
            <p className="text-sm text-slate-500">
              {monitor.sslCertificateExpiresAt
                ? `Expires ${new Date(monitor.sslCertificateExpiresAt).toLocaleDateString()}`
                : monitor.lastSslErrorMessage ?? 'No HTTPS certificate data yet.'}
            </p>
          </div>
          <SslBadge status={monitor.sslCertificateStatus} daysRemaining={monitor.sslCertificateDaysRemaining} className="w-max" />
        </div>
        {monitor.lastSslErrorMessage && <p className="mt-3 text-sm font-medium text-amber-700">{monitor.lastSslErrorMessage}</p>}
      </section>

      <section className="rounded-lg border border-slate-200 bg-white p-5 shadow-sm">
        <div className="flex flex-col justify-between gap-4 sm:flex-row sm:items-center">
          <div>
            <h2 className="text-lg font-semibold text-slate-950">Response time</h2>
            <p className="text-sm text-slate-500">Measured in milliseconds from recent checks.</p>
          </div>
          <div className="flex rounded-lg border border-slate-200 bg-slate-50 p-1">
            {(['24h', '7d', '30d'] as Range[]).map((item) => (
              <button
                key={item}
                type="button"
                onClick={() => setRange(item)}
                className={`focus-ring rounded-md px-3 py-1.5 text-sm font-semibold ${
                  range === item ? 'bg-white text-slate-950 shadow-sm' : 'text-slate-500 hover:text-slate-900'
                }`}
              >
                {item}
              </button>
            ))}
          </div>
        </div>
        <div className="mt-5 h-80">
          {chartData.length === 0 ? (
            <div className="flex h-full items-center justify-center rounded-lg border border-dashed border-slate-300 bg-slate-50 px-4 text-sm font-medium text-slate-500">
              No response-time data for this range yet.
            </div>
          ) : (
            <ResponsiveContainer width="100%" height="100%">
              <LineChart data={chartData}>
                <CartesianGrid strokeDasharray="3 3" stroke="#e2e8f0" />
                <XAxis dataKey="time" minTickGap={32} tick={{ fontSize: 12 }} />
                <YAxis tick={{ fontSize: 12 }} unit=" ms" />
                <Tooltip />
                <Line type="monotone" dataKey="responseTimeMs" stroke="#0284c7" strokeWidth={2.5} dot={false} />
              </LineChart>
            </ResponsiveContainer>
          )}
        </div>
      </section>

      <div className="grid gap-6 xl:grid-cols-2">
        <section className="rounded-lg border border-slate-200 bg-white p-5 shadow-sm">
          <h2 className="text-lg font-semibold text-slate-950">Recent checks</h2>
          <div className="mt-4 overflow-x-auto">
            <table className="min-w-full divide-y divide-slate-200 text-sm">
              <thead>
                <tr className="text-left text-xs font-semibold uppercase text-slate-500">
                  <th className="py-2 pr-4">Status</th>
                  <th className="py-2 pr-4">Code</th>
                  <th className="py-2 pr-4">Response</th>
                  <th className="py-2 pr-4">Message</th>
                  <th className="py-2 pr-4">Checked</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {checks.slice(0, 12).map((check) => (
                  <tr key={check.id}>
                    <td className="py-3 pr-4">
                      <StatusBadge status={check.status} />
                    </td>
                    <td className="py-3 pr-4 text-slate-600">{check.statusCode ?? '-'}</td>
                    <td className="py-3 pr-4 text-slate-600">{check.responseTimeMs ? `${check.responseTimeMs} ms` : '-'}</td>
                    <td className="max-w-xs py-3 pr-4 text-slate-600">{check.errorMessage ?? '-'}</td>
                    <td className="py-3 pr-4 text-slate-600">{new Date(check.checkedAt).toLocaleString()}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </section>

        <section className="rounded-lg border border-slate-200 bg-white p-5 shadow-sm">
          <h2 className="text-lg font-semibold text-slate-950">Incidents</h2>
          <div className="mt-4 space-y-3">
            {incidents.length === 0 ? (
              <p className="rounded-lg bg-emerald-50 px-4 py-3 text-sm font-medium text-emerald-700">No incidents recorded.</p>
            ) : (
              incidents.map((incident) => (
                <div key={incident.id} className="rounded-lg border border-slate-200 p-4">
                  <div className="flex items-center justify-between gap-3">
                    <p className="font-semibold text-slate-950">{incident.title}</p>
                    <span className="rounded-full bg-slate-100 px-2.5 py-1 text-xs font-semibold text-slate-600">{incident.status}</span>
                  </div>
                  <p className="mt-2 text-sm text-slate-500">{incident.summary ?? 'No summary captured.'}</p>
                  <p className="mt-3 text-xs font-medium text-slate-500">
                    {new Date(incident.startedAt).toLocaleString()}
                    {incident.resolvedAt ? ` - ${new Date(incident.resolvedAt).toLocaleString()}` : ''}
                  </p>
                </div>
              ))
            )}
          </div>
        </section>
      </div>
    </div>
  );
}

function Uptime({ label, value }: { label: string; value: number }) {
  return (
    <div className="rounded-lg border border-slate-200 bg-white p-5 shadow-sm">
      <p className="text-sm font-medium text-slate-500">{label}</p>
      <p className="mt-2 text-3xl font-semibold text-slate-950">{value.toFixed(2)}%</p>
    </div>
  );
}
