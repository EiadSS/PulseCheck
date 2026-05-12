import type { LucideIcon } from 'lucide-react';

export function MetricCard({
  label,
  value,
  icon: Icon,
  tone = 'slate'
}: {
  label: string;
  value: string | number;
  icon: LucideIcon;
  tone?: 'slate' | 'emerald' | 'amber' | 'rose' | 'sky';
}) {
  const tones = {
    slate: 'bg-slate-100 text-slate-700',
    emerald: 'bg-emerald-50 text-emerald-700',
    amber: 'bg-amber-50 text-amber-700',
    rose: 'bg-rose-50 text-rose-700',
    sky: 'bg-sky-50 text-sky-700'
  };

  return (
    <div className="rounded-lg border border-slate-200 bg-white p-4 shadow-sm">
      <div className="flex items-center justify-between gap-3">
        <div>
          <p className="text-sm font-medium text-slate-500">{label}</p>
          <p className="mt-1 text-2xl font-semibold text-slate-950">{value}</p>
        </div>
        <span className={`rounded-lg p-2 ${tones[tone]}`}>
          <Icon className="h-5 w-5" aria-hidden="true" />
        </span>
      </div>
    </div>
  );
}
