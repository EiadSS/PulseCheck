import clsx from 'clsx';
import { Lock, MinusCircle, ShieldAlert, ShieldCheck } from 'lucide-react';
import type { SslCertificateStatus } from '../types';

const styles: Record<SslCertificateStatus, string> = {
  NotApplicable: 'bg-slate-100 text-slate-600 ring-slate-200',
  Valid: 'bg-emerald-50 text-emerald-700 ring-emerald-200',
  ExpiringSoon: 'bg-amber-50 text-amber-700 ring-amber-200',
  Critical: 'bg-orange-50 text-orange-700 ring-orange-200',
  Expired: 'bg-rose-50 text-rose-700 ring-rose-200',
  Unavailable: 'bg-slate-100 text-slate-700 ring-slate-200'
};

const icons = {
  NotApplicable: MinusCircle,
  Valid: ShieldCheck,
  ExpiringSoon: ShieldAlert,
  Critical: ShieldAlert,
  Expired: ShieldAlert,
  Unavailable: Lock
};

export function SslBadge({
  status,
  daysRemaining,
  className
}: {
  status: SslCertificateStatus;
  daysRemaining?: number | null;
  className?: string;
}) {
  const Icon = icons[status];

  return (
    <span
      className={clsx(
        'inline-flex items-center gap-1.5 rounded-full px-2.5 py-1 text-xs font-semibold ring-1 ring-inset',
        styles[status],
        className
      )}
    >
      <Icon className="h-3.5 w-3.5" aria-hidden="true" />
      {formatSslStatus(status, daysRemaining)}
    </span>
  );
}

export function formatSslStatus(status: SslCertificateStatus, daysRemaining?: number | null) {
  switch (status) {
    case 'Valid':
      return daysRemaining == null ? 'SSL valid' : `SSL ${daysRemaining}d`;
    case 'ExpiringSoon':
      return daysRemaining == null ? 'SSL expiring' : `SSL ${daysRemaining}d`;
    case 'Critical':
      return daysRemaining == null ? 'SSL critical' : `SSL ${daysRemaining}d`;
    case 'Expired':
      return 'SSL expired';
    case 'Unavailable':
      return 'SSL unavailable';
    default:
      return 'No SSL';
  }
}
