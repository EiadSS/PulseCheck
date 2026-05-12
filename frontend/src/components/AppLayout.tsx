import { Activity, BarChart3, ExternalLink, LayoutDashboard, LogOut, Plus, Target } from 'lucide-react';
import type { ComponentType } from 'react';
import { Link, NavLink, Outlet, useNavigate } from 'react-router-dom';
import { useAuth } from '../contexts/AuthContext';
import { NotificationBell } from './NotificationBell';

export function AppLayout() {
  const { user, logout } = useAuth();
  const navigate = useNavigate();

  function handleLogout() {
    logout();
    navigate('/');
  }

  return (
    <div className="min-h-screen bg-slate-50 text-slate-900">
      <aside className="fixed inset-y-0 left-0 hidden w-72 border-r border-slate-200 bg-white px-5 py-6 lg:block">
        <Link to="/dashboard" className="flex items-center gap-3">
          <span className="flex h-10 w-10 items-center justify-center rounded-lg bg-sky-600 text-white">
            <Activity className="h-5 w-5" aria-hidden="true" />
          </span>
          <div>
            <p className="font-semibold text-slate-950">PulseCheck</p>
            <p className="text-xs text-slate-500">{user?.workspaceName}</p>
          </div>
        </Link>

        <nav className="mt-8 space-y-1">
          <NavItem to="/dashboard" icon={LayoutDashboard} label="Dashboard" />
          {user?.isAdmin && <NavItem to="/analytics" icon={BarChart3} label="Analytics" />}
          <NavItem to="/slo" icon={Target} label="Reliability" />
          <NavItem to="/monitors/new" icon={Plus} label="New monitor" />
          {user && <NavItem to={`/status/${user.publicStatusSlug}`} icon={ExternalLink} label="View status page" />}
        </nav>

        <div className="mt-6">
          <NotificationBell placement="sidebar" />
        </div>

        <div className="absolute bottom-6 left-5 right-5 rounded-lg border border-slate-200 bg-slate-50 p-4">
          <p className="text-sm font-medium text-slate-900">{user?.email}</p>
          <button
            type="button"
            onClick={handleLogout}
            className="focus-ring mt-3 inline-flex items-center gap-2 text-sm font-semibold text-slate-600 hover:text-slate-950"
          >
            <LogOut className="h-4 w-4" aria-hidden="true" />
            Sign out
          </button>
        </div>
      </aside>

      <div className="lg:pl-72">
        <header className="sticky top-0 z-20 border-b border-slate-200 bg-white/95 px-4 py-3 backdrop-blur lg:hidden">
          <div className="flex items-center justify-between">
            <Link to="/dashboard" className="flex items-center gap-2 font-semibold">
              <Activity className="h-5 w-5 text-sky-600" aria-hidden="true" />
              PulseCheck
            </Link>
            <div className="flex items-center gap-2">
              <NotificationBell />
              {user?.isAdmin && (
                <Link
                  to="/analytics"
                  className="focus-ring rounded-lg border border-slate-200 p-2 text-slate-700"
                  title="Analytics"
                  aria-label="Analytics"
                >
                  <BarChart3 className="h-4 w-4" aria-hidden="true" />
                </Link>
              )}
              {user && (
                <Link
                  to={`/status/${user.publicStatusSlug}`}
                  className="focus-ring rounded-lg border border-slate-200 p-2 text-slate-700"
                  title="View status page"
                  aria-label="View status page"
                >
                  <ExternalLink className="h-4 w-4" aria-hidden="true" />
                </Link>
              )}
              <button
                type="button"
                onClick={handleLogout}
                className="focus-ring rounded-lg border border-slate-200 p-2 text-slate-700"
                title="Sign out"
              >
                <LogOut className="h-4 w-4" aria-hidden="true" />
              </button>
            </div>
          </div>
        </header>

        <main className="mx-auto w-full max-w-7xl px-4 py-6 sm:px-6 lg:px-8">
          <Outlet />
        </main>
      </div>
    </div>
  );
}

function NavItem({
  to,
  icon: Icon,
  label
}: {
  to: string;
  icon: ComponentType<{ className?: string }>;
  label: string;
}) {
  return (
    <NavLink
      to={to}
      className={({ isActive }) =>
        `focus-ring flex items-center gap-3 rounded-lg px-3 py-2 text-sm font-medium ${
          isActive ? 'bg-sky-50 text-sky-700' : 'text-slate-600 hover:bg-slate-50 hover:text-slate-950'
        }`
      }
    >
      <Icon className="h-4 w-4" aria-hidden="true" />
      {label}
    </NavLink>
  );
}
