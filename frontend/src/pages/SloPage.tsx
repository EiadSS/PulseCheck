import { Activity, Gauge, HelpCircle, ShieldCheck, Target } from 'lucide-react';
import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { api } from '../api/client';
import { MetricCard } from '../components/MetricCard';
import { StatusBadge } from '../components/StatusBadge';
import type { SloSummary } from '../types';

export function SloPage() {
  const [summary, setSummary] = useState<SloSummary | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    api
      .sloSummary()
      .then(setSummary)
      .catch((err) => setError(err instanceof Error ? err.message : 'Unable to load reliability dashboard.'));
  }, []);

  if (error) {
    return <div className="rounded-lg bg-rose-50 px-4 py-3 text-sm font-medium text-rose-700">{error}</div>;
  }

  if (!summary) {
    return <div className="rounded-lg border border-slate-200 bg-white p-8 text-sm font-medium text-slate-500">Loading reliability dashboard...</div>;
  }

  const thirtyDay = summary.windows.find((window) => window.range === '30d') ?? summary.windows[summary.windows.length - 1];
  const burningMonitors = summary.monitors.filter((monitor) => monitor.errorBudgetUsed30Days > 0).slice(0, 5);

  return (
    <div className="space-y-6">
      <div>
        <p className="text-sm font-semibold uppercase text-sky-700">Reliability</p>
        <h1 className="mt-1 text-3xl font-semibold tracking-normal text-slate-950">Reliability targets</h1>
        <p className="mt-2 max-w-3xl text-sm leading-6 text-slate-500">
          Uptime is measured from check history. Up and Degraded count as available; Down and Error use downtime budget.
        </p>
      </div>

      <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
        <MetricCard label="Uptime target" value={`${summary.targetPercentage.toFixed(1)}%`} icon={Target} tone="sky" />
        <MetricCard label="30d uptime" value={`${thirtyDay.uptimePercentage.toFixed(2)}%`} icon={ShieldCheck} tone="emerald" />
        <MetricCard label="Downtime budget used" value={`${thirtyDay.errorBudgetUsedPercentage.toFixed(0)}%`} icon={Gauge} tone="amber" />
        <MetricCard label="Tracked monitors" value={summary.monitors.length} icon={Activity} tone="slate" />
      </div>

      <section className="rounded-lg border border-slate-200 bg-white p-5 shadow-sm">
        <div className="flex flex-col justify-between gap-3 sm:flex-row sm:items-start">
          <div>
            <h2 className="text-lg font-semibold text-slate-950">Uptime over time</h2>
            <p className="mt-1 text-sm text-slate-500">Compare uptime against the target across recent windows.</p>
          </div>
          <div className="flex flex-wrap gap-2">
            <Helper label="Uptime target" text="The availability goal, such as 99.9%." />
            <Helper label="Downtime budget" text="The amount of downtime allowed before missing the target." />
            <Helper label="Budget used" text="How much of the allowed downtime has already been used." />
          </div>
        </div>
        <div className="mt-4 grid gap-4 md:grid-cols-3">
          {summary.windows.map((window) => (
            <div key={window.range} className="rounded-lg border border-slate-200 p-4">
              <div className="flex items-center justify-between gap-3">
                <p className="text-sm font-semibold uppercase text-slate-500">{window.range}</p>
                <span
                  className={`rounded-full px-2.5 py-1 text-xs font-semibold ${
                    window.isCompliant ? 'bg-emerald-50 text-emerald-700' : 'bg-rose-50 text-rose-700'
                  }`}
                >
                  {window.isCompliant ? 'On target' : 'At risk'}
                </span>
              </div>
              <p className="mt-3 text-3xl font-semibold text-slate-950">{window.uptimePercentage.toFixed(2)}%</p>
              <BudgetBar value={window.errorBudgetUsedPercentage} />
              <p className="mt-2 text-sm font-medium text-slate-500">
                {window.errorBudgetRemainingPercentage.toFixed(0)}% budget remaining
              </p>
            </div>
          ))}
        </div>
      </section>

      <section className="rounded-lg border border-slate-200 bg-white p-5 shadow-sm">
        <div className="flex flex-col justify-between gap-3 sm:flex-row sm:items-center">
          <div>
            <h2 className="text-lg font-semibold text-slate-950">Monitors at risk</h2>
            <p className="text-sm text-slate-500">Sorted by downtime budget used over the last 30 days.</p>
          </div>
        </div>

        <div className="mt-4 overflow-x-auto">
          <table className="min-w-full divide-y divide-slate-200 text-sm">
            <thead>
              <tr className="text-left text-xs font-semibold uppercase text-slate-500">
                <th className="py-2 pr-4">Monitor</th>
                <th className="py-2 pr-4">Status</th>
                <th className="py-2 pr-4">24h</th>
                <th className="py-2 pr-4">7d</th>
                <th className="py-2 pr-4">30d</th>
                <th className="py-2 pr-4">Downtime budget used</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {summary.monitors.map((monitor) => (
                <tr key={monitor.id}>
                  <td className="py-3 pr-4">
                    <Link to={`/monitors/${monitor.id}`} className="font-semibold text-slate-950 hover:text-sky-700">
                      {monitor.name}
                    </Link>
                  </td>
                  <td className="py-3 pr-4">
                    <StatusBadge status={monitor.currentStatus} />
                  </td>
                  <td className="py-3 pr-4 text-slate-600">{monitor.uptime24Hours.toFixed(2)}%</td>
                  <td className="py-3 pr-4 text-slate-600">{monitor.uptime7Days.toFixed(2)}%</td>
                  <td className="py-3 pr-4 text-slate-600">{monitor.uptime30Days.toFixed(2)}%</td>
                  <td className="py-3 pr-4">
                    <div className="min-w-32">
                      <BudgetBar value={monitor.errorBudgetUsed30Days} compact />
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        {burningMonitors.length === 0 && (
          <p className="mt-4 rounded-lg bg-emerald-50 px-4 py-3 text-sm font-medium text-emerald-700">
            No monitors are currently using downtime budget.
          </p>
        )}
      </section>
    </div>
  );
}

function Helper({ label, text }: { label: string; text: string }) {
  return (
    <span
      title={text}
      className="inline-flex items-center gap-1.5 rounded-full bg-slate-100 px-2.5 py-1 text-xs font-semibold text-slate-600"
    >
      <HelpCircle className="h-3.5 w-3.5" aria-hidden="true" />
      {label}
    </span>
  );
}

function BudgetBar({ value, compact = false }: { value: number; compact?: boolean }) {
  const width = `${Math.min(100, Math.max(0, value))}%`;
  const tone = value >= 100 ? 'bg-rose-600' : value >= 75 ? 'bg-amber-500' : 'bg-sky-600';

  return (
    <div className={compact ? 'flex items-center gap-2' : 'mt-4'}>
      <div className="h-2 w-full overflow-hidden rounded-full bg-slate-100">
        <div className={`h-full rounded-full ${tone}`} style={{ width }} />
      </div>
      <span className="text-xs font-semibold text-slate-600">{value.toFixed(0)}%</span>
    </div>
  );
}
