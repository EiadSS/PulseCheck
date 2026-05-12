import { Activity, ArrowLeft, ShieldCheck } from 'lucide-react';
import { useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { StatusBadge } from '../components/StatusBadge';
import { api } from '../api/client';
import { useAuth } from '../contexts/AuthContext';
import type { PublicStatusPage as PublicStatusPageData } from '../types';

export function PublicStatusPage() {
  const { slug } = useParams();
  const { user } = useAuth();
  const [page, setPage] = useState<PublicStatusPageData | null>(null);
  const [error, setError] = useState<string | null>(null);
  const homePath = user ? '/dashboard' : '/';

  useEffect(() => {
    if (!slug) {
      return;
    }

    api
      .publicStatus(slug)
      .then(setPage)
      .catch((err) => setError(err instanceof Error ? err.message : 'Unable to load status page.'));
  }, [slug]);

  if (error) {
    return (
      <main className="grid min-h-screen place-items-center bg-slate-50 px-4">
        <div className="rounded-lg bg-rose-50 px-4 py-3 text-sm font-medium text-rose-700">{error}</div>
      </main>
    );
  }

  if (!page) {
    return <main className="grid min-h-screen place-items-center bg-slate-50 text-sm font-medium text-slate-500">Loading status page...</main>;
  }

  return (
    <main className="min-h-screen bg-slate-50">
      <header className="border-b border-slate-200 bg-white">
        <div className="mx-auto flex max-w-5xl flex-col gap-4 px-4 py-5 sm:flex-row sm:items-center sm:justify-between sm:px-6 lg:px-8">
          <Link to={homePath} className="flex items-center gap-3 font-semibold text-slate-950">
            <Activity className="h-5 w-5 text-sky-600" aria-hidden="true" />
            PulseCheck
          </Link>
          <div className="flex flex-wrap items-center gap-3">
            <span className="text-sm font-medium text-slate-500">Public status</span>
            {user && (
              <Link
                to="/dashboard"
                className="focus-ring inline-flex items-center gap-2 rounded-lg bg-slate-950 px-3 py-2 text-sm font-semibold text-white shadow-sm hover:bg-slate-800"
              >
                <ArrowLeft className="h-4 w-4" aria-hidden="true" />
                Back to dashboard
              </Link>
            )}
          </div>
        </div>
      </header>

      <div className="mx-auto max-w-5xl px-4 py-10 sm:px-6 lg:px-8">
        <section className="rounded-lg border border-slate-200 bg-white p-6 shadow-sm">
          <div className="flex flex-col justify-between gap-4 sm:flex-row sm:items-center">
            <div>
              <p className="text-sm font-semibold uppercase text-sky-700">Status page</p>
              <h1 className="mt-1 text-3xl font-semibold text-slate-950">{page.title}</h1>
            </div>
            <StatusBadge status={page.overallStatus} className="text-sm" />
          </div>

          <div className="mt-6 grid gap-4 md:grid-cols-3">
            <Summary label="24h uptime" value={`${page.uptime24Hours.toFixed(2)}%`} />
            <Summary label="7d uptime" value={`${page.uptime7Days.toFixed(2)}%`} />
            <Summary label="30d uptime" value={`${page.uptime30Days.toFixed(2)}%`} />
          </div>
        </section>

        <section className="mt-6 rounded-lg border border-slate-200 bg-white p-6 shadow-sm">
          <h2 className="text-lg font-semibold text-slate-950">Monitors</h2>
          <div className="mt-4 divide-y divide-slate-100">
            {page.monitors.map((monitor) => (
              <div key={monitor.id} className="flex flex-col justify-between gap-3 py-4 sm:flex-row sm:items-center">
                <div>
                  <p className="font-semibold text-slate-950">{monitor.name}</p>
                  <p className="text-sm text-slate-500">
                    {monitor.type} · {monitor.lastResponseTimeMs ? `${monitor.lastResponseTimeMs} ms` : 'No response time yet'}
                  </p>
                </div>
                <StatusBadge status={monitor.currentStatus} />
              </div>
            ))}
          </div>
        </section>

        <section className="mt-6 rounded-lg border border-slate-200 bg-white p-6 shadow-sm">
          <div className="flex items-center gap-2">
            <ShieldCheck className="h-5 w-5 text-sky-600" aria-hidden="true" />
            <h2 className="text-lg font-semibold text-slate-950">Recent incidents</h2>
          </div>
          <div className="mt-4 space-y-3">
            {page.recentIncidents.length === 0 ? (
              <p className="rounded-lg bg-emerald-50 px-4 py-3 text-sm font-medium text-emerald-700">No recent incidents.</p>
            ) : (
              page.recentIncidents.map((incident) => (
                <div key={incident.id} className="rounded-lg border border-slate-200 p-4">
                  <div className="flex flex-col justify-between gap-2 sm:flex-row sm:items-center">
                    <div>
                      <p className="font-semibold text-slate-950">{incident.title}</p>
                      <p className="text-sm text-slate-500">{incident.monitorName}</p>
                    </div>
                    <span className="w-max rounded-full bg-slate-100 px-2.5 py-1 text-xs font-semibold text-slate-600">{incident.status}</span>
                  </div>
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
    </main>
  );
}

function Summary({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-lg bg-slate-50 p-4">
      <p className="text-sm font-medium text-slate-500">{label}</p>
      <p className="mt-1 text-2xl font-semibold text-slate-950">{value}</p>
    </div>
  );
}
