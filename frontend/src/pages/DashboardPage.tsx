import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import { Activity, AlertTriangle, CheckCircle2, Clock3, Plus, ShieldAlert } from 'lucide-react';
import { useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { Cell, Pie, PieChart, ResponsiveContainer, Tooltip } from 'recharts';
import { API_BASE_URL, api, getStoredToken } from '../api/client';
import { MetricCard } from '../components/MetricCard';
import { MonitorCard } from '../components/MonitorCard';
import type { DashboardSummary, MonitorStatus, MonitorSummary } from '../types';

const filters: Array<'All' | MonitorStatus> = ['All', 'Up', 'Down', 'Degraded', 'Error', 'Paused'];
const chartColors: Record<string, string> = {
  Up: '#059669',
  Degraded: '#d97706',
  Error: '#e11d48',
  Down: '#dc2626',
  Paused: '#64748b'
};

export function DashboardPage() {
  const [monitors, setMonitors] = useState<MonitorSummary[]>([]);
  const [summary, setSummary] = useState<DashboardSummary | null>(null);
  const [filter, setFilter] = useState<'All' | MonitorStatus>('All');
  const [isLoading, setIsLoading] = useState(true);
  const [checkingMonitorIds, setCheckingMonitorIds] = useState<Set<string>>(new Set());
  const [error, setError] = useState<string | null>(null);

  async function load() {
    const [monitorData, summaryData] = await Promise.all([api.monitors(), api.dashboardSummary()]);
    setMonitors(monitorData);
    setSummary(summaryData);
  }

  useEffect(() => {
    let active = true;

    async function initialLoad() {
      setError(null);
      setIsLoading(true);
      try {
        const [monitorData, summaryData] = await Promise.all([api.monitors(), api.dashboardSummary()]);
        if (active) {
          setMonitors(monitorData);
          setSummary(summaryData);
        }
      } catch (err) {
        if (active) {
          setError(err instanceof Error ? err.message : 'Unable to load dashboard.');
        }
      } finally {
        if (active) {
          setIsLoading(false);
        }
      }
    }

    void initialLoad();
    return () => {
      active = false;
    };
  }, []);

  useEffect(() => {
    const token = getStoredToken();
    if (!token) {
      return;
    }

    const connection = new HubConnectionBuilder()
      .withUrl(`${API_BASE_URL}/hubs/monitors`, { accessTokenFactory: () => getStoredToken() ?? '' })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();

    connection.on('monitorUpdated', () => {
      void load();
    });

    void connection.start().catch(() => undefined);

    return () => {
      void connection.stop();
    };
  }, []);

  const filteredMonitors = useMemo(
    () => (filter === 'All' ? monitors : monitors.filter((monitor) => monitor.currentStatus === filter)),
    [filter, monitors]
  );

  const chartData = summary
    ? [
        { name: 'Up', value: summary.up },
        { name: 'Degraded', value: summary.degraded },
        { name: 'Error', value: summary.error },
        { name: 'Down', value: summary.down },
        { name: 'Paused', value: summary.paused }
      ].filter((item) => item.value > 0)
    : [];

  async function mutate(action: () => Promise<unknown>) {
    await action();
    await load();
  }

  async function runCheckNow(id: string) {
    setError(null);
    setCheckingMonitorIds((current) => new Set(current).add(id));

    try {
      await api.runMonitorCheck(id);
      await load();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Unable to run check.');
    } finally {
      setCheckingMonitorIds((current) => {
        const next = new Set(current);
        next.delete(id);
        return next;
      });
    }
  }

  return (
    <div className="space-y-6">
      <div className="flex flex-col justify-between gap-4 sm:flex-row sm:items-center">
        <div>
          <p className="text-sm font-semibold uppercase text-sky-700">Dashboard</p>
          <h1 className="mt-1 text-3xl font-semibold tracking-normal text-slate-950">Service health</h1>
        </div>
        <Link
          to="/monitors/new"
          className="focus-ring inline-flex items-center justify-center gap-2 rounded-lg bg-sky-600 px-4 py-2.5 text-sm font-semibold text-white shadow-sm hover:bg-sky-700"
        >
          <Plus className="h-4 w-4" aria-hidden="true" />
          Add monitor
        </Link>
      </div>

      {error && <div className="rounded-lg bg-rose-50 px-4 py-3 text-sm font-medium text-rose-700">{error}</div>}

      <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
        <MetricCard label="Monitors" value={summary?.totalMonitors ?? 0} icon={Activity} tone="sky" />
        <MetricCard label="Up" value={summary?.up ?? 0} icon={CheckCircle2} tone="emerald" />
        <MetricCard label="Open incidents" value={summary?.openIncidents ?? 0} icon={ShieldAlert} tone="rose" />
        <MetricCard
          label="24h uptime"
          value={`${(summary?.averageUptime24Hours ?? 100).toFixed(2)}%`}
          icon={Clock3}
          tone="amber"
        />
      </div>

      <div className="grid gap-6 xl:grid-cols-[1fr_320px]">
        <section className="space-y-4">
          <div className="flex flex-wrap gap-2">
            {filters.map((item) => (
              <button
                key={item}
                type="button"
                onClick={() => setFilter(item)}
                className={`focus-ring rounded-lg px-3 py-2 text-sm font-semibold ${
                  filter === item ? 'bg-slate-950 text-white' : 'border border-slate-200 bg-white text-slate-600 hover:text-slate-950'
                }`}
              >
                {item}
              </button>
            ))}
          </div>

          {isLoading ? (
            <div className="rounded-lg border border-slate-200 bg-white p-8 text-center text-sm font-medium text-slate-500">
              Loading monitors...
            </div>
          ) : filteredMonitors.length === 0 ? (
            <div className="rounded-lg border border-dashed border-slate-300 bg-white p-10 text-center">
              <AlertTriangle className="mx-auto h-8 w-8 text-slate-400" aria-hidden="true" />
              <h2 className="mt-3 text-lg font-semibold text-slate-950">No monitors found</h2>
              <p className="mt-1 text-sm text-slate-500">Add a website or API endpoint to start collecting checks.</p>
            </div>
          ) : (
            <div className="grid gap-4">
              {filteredMonitors.map((monitor) => (
                <MonitorCard
                  key={monitor.id}
                  monitor={monitor}
                  onPause={(id) => void mutate(() => api.pauseMonitor(id))}
                  onResume={(id) => void mutate(() => api.resumeMonitor(id))}
                  onRunCheck={(id) => void runCheckNow(id)}
                  isChecking={checkingMonitorIds.has(monitor.id)}
                  onDelete={(id) => {
                    if (window.confirm('Delete this monitor and its history?')) {
                      void mutate(() => api.deleteMonitor(id));
                    }
                  }}
                />
              ))}
            </div>
          )}
        </section>

        <aside className="rounded-lg border border-slate-200 bg-white p-5 shadow-sm">
          <h2 className="text-lg font-semibold text-slate-950">Status mix</h2>
          <div className="mt-4 h-64">
            <ResponsiveContainer width="100%" height="100%">
              <PieChart>
                <Pie dataKey="value" data={chartData} innerRadius={60} outerRadius={90} paddingAngle={4}>
                  {chartData.map((item) => (
                    <Cell key={item.name} fill={chartColors[item.name]} />
                  ))}
                </Pie>
                <Tooltip />
              </PieChart>
            </ResponsiveContainer>
          </div>
          <div className="mt-4 space-y-2">
            {chartData.map((item) => (
              <div key={item.name} className="flex items-center justify-between text-sm">
                <span className="flex items-center gap-2 text-slate-600">
                  <span className="h-2.5 w-2.5 rounded-full" style={{ backgroundColor: chartColors[item.name] }} />
                  {item.name}
                </span>
                <span className="font-semibold text-slate-950">{item.value}</span>
              </div>
            ))}
          </div>
        </aside>
      </div>
    </div>
  );
}
