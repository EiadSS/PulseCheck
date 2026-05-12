import { ExternalLink, Pause, Play, RefreshCw, Trash2 } from 'lucide-react';
import { Link } from 'react-router-dom';
import type { MonitorSummary } from '../types';
import { formatSslStatus, SslBadge } from './SslBadge';
import { StatusBadge } from './StatusBadge';

export function MonitorCard({
  monitor,
  onPause,
  onResume,
  onRunCheck,
  isChecking = false,
  onDelete
}: {
  monitor: MonitorSummary;
  onPause: (id: string) => void;
  onResume: (id: string) => void;
  onRunCheck: (id: string) => void;
  isChecking?: boolean;
  onDelete: (id: string) => void;
}) {
  return (
    <article className="rounded-lg border border-slate-200 bg-white p-5 shadow-sm transition hover:-translate-y-0.5 hover:shadow-soft">
      <div className="flex items-start justify-between gap-4">
        <div className="min-w-0">
          <div className="flex flex-wrap items-center gap-2">
            <Link to={`/monitors/${monitor.id}`} className="truncate text-lg font-semibold text-slate-950 hover:text-sky-700">
              {monitor.name}
            </Link>
            <StatusBadge status={monitor.currentStatus} />
            {monitor.sslCertificateStatus !== 'NotApplicable' && (
              <SslBadge status={monitor.sslCertificateStatus} daysRemaining={monitor.sslCertificateDaysRemaining} />
            )}
          </div>
          <a
            href={monitor.url}
            target="_blank"
            rel="noreferrer"
            className="mt-1 inline-flex max-w-full items-center gap-1 truncate text-sm text-slate-500 hover:text-sky-700"
          >
            <span className="truncate">{monitor.url}</span>
            <ExternalLink className="h-3.5 w-3.5 shrink-0" aria-hidden="true" />
          </a>
        </div>
        <div className="flex shrink-0 items-center gap-1">
          <button
            type="button"
            onClick={() => onRunCheck(monitor.id)}
            disabled={monitor.isPaused || isChecking}
            className="focus-ring rounded-lg border border-slate-200 p-2 text-slate-600 hover:bg-sky-50 hover:text-sky-700 disabled:cursor-not-allowed disabled:opacity-50"
            title={monitor.isPaused ? 'Resume monitor to run a check' : 'Run check now'}
            aria-label={monitor.isPaused ? 'Resume monitor to run a check' : 'Run check now'}
          >
            <RefreshCw className={`h-4 w-4 ${isChecking ? 'animate-spin' : ''}`} aria-hidden="true" />
          </button>
          {monitor.isPaused ? (
            <button
              type="button"
              onClick={() => onResume(monitor.id)}
              className="focus-ring rounded-lg border border-slate-200 p-2 text-slate-600 hover:bg-emerald-50 hover:text-emerald-700"
              title="Resume monitor"
            >
              <Play className="h-4 w-4" aria-hidden="true" />
            </button>
          ) : (
            <button
              type="button"
              onClick={() => onPause(monitor.id)}
              className="focus-ring rounded-lg border border-slate-200 p-2 text-slate-600 hover:bg-amber-50 hover:text-amber-700"
              title="Pause monitor"
            >
              <Pause className="h-4 w-4" aria-hidden="true" />
            </button>
          )}
          <button
            type="button"
            onClick={() => onDelete(monitor.id)}
            className="focus-ring rounded-lg border border-slate-200 p-2 text-slate-600 hover:bg-rose-50 hover:text-rose-700"
            title="Delete monitor"
          >
            <Trash2 className="h-4 w-4" aria-hidden="true" />
          </button>
        </div>
      </div>

      <dl className="mt-5 grid grid-cols-2 gap-3 text-sm sm:grid-cols-5">
        <Metric label="Last check" value={formatRelative(monitor.lastCheckedAt)} />
        <Metric label="Response" value={monitor.lastResponseTimeMs ? `${monitor.lastResponseTimeMs} ms` : 'No data'} />
        <Metric label="Uptime" value={`${monitor.uptimePercentage.toFixed(2)}%`} />
        <Metric label="Incidents" value={monitor.openIncidentCount} />
        <Metric label="SSL" value={formatSslStatus(monitor.sslCertificateStatus, monitor.sslCertificateDaysRemaining)} />
      </dl>
    </article>
  );
}

function Metric({ label, value }: { label: string; value: string | number }) {
  return (
    <div className="rounded-md bg-slate-50 px-3 py-2">
      <dt className="text-xs font-medium uppercase text-slate-500">{label}</dt>
      <dd className="mt-1 truncate font-semibold text-slate-900">{value}</dd>
    </div>
  );
}

export function formatRelative(value?: string | null) {
  if (!value) {
    return 'No data';
  }

  const diffMs = Date.now() - new Date(value).getTime();
  const minutes = Math.max(0, Math.round(diffMs / 60000));

  if (minutes < 1) {
    return 'Just now';
  }

  if (minutes < 60) {
    return `${minutes}m ago`;
  }

  return `${Math.round(minutes / 60)}h ago`;
}
