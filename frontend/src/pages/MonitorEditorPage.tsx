import { useEffect, useState } from 'react';
import { ArrowLeft } from 'lucide-react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { api } from '../api/client';
import { MonitorForm } from '../components/MonitorForm';
import type { MonitorDetail } from '../types';

export function NewMonitorPage() {
  const navigate = useNavigate();

  return (
    <div className="mx-auto max-w-3xl space-y-6">
      <Header title="Add monitor" subtitle="Create a scheduled check for a website or API endpoint." />
      <MonitorForm
        submitLabel="Create monitor"
        onSubmit={async (payload) => {
          const monitor = await api.createMonitor(payload);
          navigate(`/monitors/${monitor.id}`);
        }}
      />
    </div>
  );
}

export function EditMonitorPage() {
  const { id } = useParams();
  const navigate = useNavigate();
  const [monitor, setMonitor] = useState<MonitorDetail | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!id) {
      return;
    }

    api
      .monitor(id)
      .then(setMonitor)
      .catch((err) => setError(err instanceof Error ? err.message : 'Unable to load monitor.'));
  }, [id]);

  if (error) {
    return <div className="rounded-lg bg-rose-50 px-4 py-3 text-sm font-medium text-rose-700">{error}</div>;
  }

  if (!monitor || !id) {
    return <div className="rounded-lg border border-slate-200 bg-white p-8 text-sm font-medium text-slate-500">Loading monitor...</div>;
  }

  return (
    <div className="mx-auto max-w-3xl space-y-6">
      <Header title="Edit monitor" subtitle="Tune check cadence, thresholds, expected response, and public visibility." />
      <MonitorForm
        initialValue={monitor}
        submitLabel="Save changes"
        onSubmit={async (payload) => {
          await api.updateMonitor(id, payload);
          navigate(`/monitors/${id}`);
        }}
      />
    </div>
  );
}

function Header({ title, subtitle }: { title: string; subtitle: string }) {
  return (
    <div>
      <Link
        to="/dashboard"
        className="focus-ring inline-flex w-max items-center gap-2 rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm font-semibold text-slate-700 shadow-sm hover:bg-slate-50"
      >
        <ArrowLeft className="h-4 w-4" aria-hidden="true" />
        Back to dashboard
      </Link>
      <h1 className="mt-4 text-3xl font-semibold text-slate-950">{title}</h1>
      <p className="mt-2 text-slate-500">{subtitle}</p>
    </div>
  );
}
