import clsx from 'clsx';
import { AlertTriangle, CheckCircle2, Clock3, PauseCircle, XCircle } from 'lucide-react';
import type { MonitorStatus } from '../types';

const statusStyles: Record<MonitorStatus, string> = {
  Up: 'bg-emerald-50 text-emerald-700 ring-emerald-200',
  Degraded: 'bg-amber-50 text-amber-700 ring-amber-200',
  Error: 'bg-rose-50 text-rose-700 ring-rose-200',
  Down: 'bg-red-50 text-red-700 ring-red-200',
  Paused: 'bg-slate-100 text-slate-600 ring-slate-200'
};

const statusIcons = {
  Up: CheckCircle2,
  Degraded: Clock3,
  Error: AlertTriangle,
  Down: XCircle,
  Paused: PauseCircle
};

export function StatusBadge({ status, className }: { status: MonitorStatus; className?: string }) {
  const Icon = statusIcons[status];

  return (
    <span
      className={clsx(
        'inline-flex items-center gap-1.5 rounded-full px-2.5 py-1 text-xs font-semibold ring-1 ring-inset',
        statusStyles[status],
        className
      )}
    >
      <Icon className="h-3.5 w-3.5" aria-hidden="true" />
      {status}
    </span>
  );
}
