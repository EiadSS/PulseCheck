import { Activity, ArrowRight, BarChart3, Clock3, ShieldCheck } from 'lucide-react';
import type { ComponentType } from 'react';
import { Link, Navigate } from 'react-router-dom';
import { useAuth } from '../contexts/AuthContext';
import { StatusBadge } from '../components/StatusBadge';

export function LandingPage() {
  const { user } = useAuth();

  if (user) {
    return <Navigate to="/dashboard" replace />;
  }

  return (
    <main className="min-h-screen bg-slate-950 text-white">
      <section className="relative overflow-hidden">
        <div className="absolute inset-0 bg-[linear-gradient(145deg,rgba(2,132,199,0.22),transparent_38%),linear-gradient(35deg,rgba(16,185,129,0.18),transparent_35%)]" />
        <div className="relative mx-auto grid min-h-screen max-w-7xl content-center gap-12 px-4 py-12 sm:px-6 lg:grid-cols-[0.95fr_1.05fr] lg:px-8">
          <div className="max-w-2xl">
            <Link to="/" className="inline-flex items-center gap-3 text-sm font-semibold text-sky-200">
              <span className="flex h-10 w-10 items-center justify-center rounded-lg bg-sky-500 text-white">
                <Activity className="h-5 w-5" aria-hidden="true" />
              </span>
              PulseCheck
            </Link>
            <h1 className="mt-10 text-4xl font-semibold leading-tight tracking-normal sm:text-6xl">
              Monitor websites and APIs, track uptime, detect slowdowns, and view incident history.
            </h1>
            <p className="mt-6 max-w-xl text-lg leading-8 text-slate-300">
              A production-style uptime monitoring dashboard for teams that need clear service health, useful history,
              and recruiter-friendly polish.
            </p>
            <div className="mt-8 flex flex-wrap gap-3">
              <Link
                to="/register"
                className="focus-ring inline-flex items-center gap-2 rounded-lg bg-sky-500 px-5 py-3 text-sm font-semibold text-white shadow-lg shadow-sky-950/30 hover:bg-sky-400"
              >
                Create account
                <ArrowRight className="h-4 w-4" aria-hidden="true" />
              </Link>
              <Link
                to="/login"
                className="focus-ring inline-flex items-center rounded-lg border border-white/20 px-5 py-3 text-sm font-semibold text-white hover:bg-white/10"
              >
                Sign in
              </Link>
            </div>
          </div>

          <div className="rounded-lg border border-white/10 bg-white/95 p-4 text-slate-950 shadow-2xl">
            <div className="flex items-center justify-between border-b border-slate-200 pb-4">
              <div>
                <p className="text-sm font-semibold text-slate-500">Live overview</p>
                <p className="text-xl font-semibold">Service health</p>
              </div>
              <StatusBadge status="Up" />
            </div>
            <div className="grid gap-3 py-4 sm:grid-cols-3">
              <PreviewMetric icon={ShieldCheck} label="24h uptime" value="99.94%" />
              <PreviewMetric icon={Clock3} label="Median response" value="184 ms" />
              <PreviewMetric icon={BarChart3} label="Open incidents" value="1" />
            </div>
            <div className="space-y-3">
              {[
                ['Marketing Website', 'Up', '182 ms'],
                ['Billing API', 'Degraded', '812 ms'],
                ['Search API', 'Error', '239 ms']
              ].map(([name, status, response]) => (
                <div key={name} className="flex items-center justify-between rounded-lg border border-slate-200 bg-slate-50 px-4 py-3">
                  <div>
                    <p className="font-semibold">{name}</p>
                    <p className="text-sm text-slate-500">Last checked moments ago</p>
                  </div>
                  <div className="text-right">
                    <StatusBadge status={status as 'Up' | 'Degraded' | 'Error'} />
                    <p className="mt-1 text-sm font-semibold text-slate-600">{response}</p>
                  </div>
                </div>
              ))}
            </div>
          </div>
        </div>
      </section>
    </main>
  );
}

function PreviewMetric({
  icon: Icon,
  label,
  value
}: {
  icon: ComponentType<{ className?: string }>;
  label: string;
  value: string;
}) {
  return (
    <div className="rounded-lg bg-slate-50 p-4">
      <Icon className="h-5 w-5 text-sky-600" aria-hidden="true" />
      <p className="mt-3 text-xs font-semibold uppercase text-slate-500">{label}</p>
      <p className="mt-1 text-xl font-semibold text-slate-950">{value}</p>
    </div>
  );
}
