import { Bell, CheckCheck, Mail, MailWarning, X } from 'lucide-react';
import { useEffect, useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import { api } from '../api/client';
import type { Notification } from '../types';
import { formatRelative } from './MonitorCard';

export function NotificationBell({ placement = 'header' }: { placement?: 'sidebar' | 'header' }) {
  const [isOpen, setIsOpen] = useState(false);
  const [unreadCount, setUnreadCount] = useState(0);
  const [notifications, setNotifications] = useState<Notification[]>([]);
  const [emailAlertsEnabled, setEmailAlertsEnabled] = useState(true);
  const [emailDeliveryConfigured, setEmailDeliveryConfigured] = useState<boolean | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const containerRef = useRef<HTMLDivElement>(null);

  async function loadNotifications() {
    setIsLoading(true);
    try {
      const [items, count, preferences] = await Promise.all([
        api.notifications(),
        api.notificationUnreadCount(),
        api.notificationPreferences()
      ]);
      setNotifications(items);
      setUnreadCount(count.unreadCount);
      setEmailAlertsEnabled(preferences.emailAlertsEnabled);
      setEmailDeliveryConfigured(preferences.emailDeliveryConfigured);
    } finally {
      setIsLoading(false);
    }
  }

  useEffect(() => {
    void api.notificationUnreadCount().then((count) => setUnreadCount(count.unreadCount)).catch(() => undefined);
  }, []);

  useEffect(() => {
    if (!isOpen) {
      return;
    }

    function handlePointerDown(event: MouseEvent) {
      if (containerRef.current && !containerRef.current.contains(event.target as Node)) {
        setIsOpen(false);
      }
    }

    function handleKeyDown(event: KeyboardEvent) {
      if (event.key === 'Escape') {
        setIsOpen(false);
      }
    }

    document.addEventListener('mousedown', handlePointerDown);
    document.addEventListener('keydown', handleKeyDown);
    return () => {
      document.removeEventListener('mousedown', handlePointerDown);
      document.removeEventListener('keydown', handleKeyDown);
    };
  }, [isOpen]);

  async function toggleOpen() {
    const nextOpen = !isOpen;
    setIsOpen(nextOpen);

    if (nextOpen) {
      await loadNotifications();
    }
  }

  async function markRead(id: string) {
    const updated = await api.markNotificationRead(id);
    setNotifications((items) => items.map((item) => (item.id === id ? updated : item)));
    setUnreadCount((count) => Math.max(0, count - 1));
  }

  async function markAllRead() {
    await api.markAllNotificationsRead();
    setNotifications((items) => items.map((item) => ({ ...item, isRead: true, readAt: new Date().toISOString() })));
    setUnreadCount(0);
  }

  async function deleteNotification(notification: Notification) {
    await api.deleteNotification(notification.id);
    setNotifications((items) => items.filter((item) => item.id !== notification.id));

    if (!notification.isRead) {
      setUnreadCount((count) => Math.max(0, count - 1));
    }
  }

  async function toggleEmailAlerts() {
    if (emailDeliveryConfigured !== true) {
      return;
    }

    const nextValue = !emailAlertsEnabled;
    setEmailAlertsEnabled(nextValue);
    const updated = await api.updateNotificationPreferences({ emailAlertsEnabled: nextValue });
    setEmailAlertsEnabled(updated.emailAlertsEnabled);
    setEmailDeliveryConfigured(updated.emailDeliveryConfigured);
  }

  const panelClass =
    placement === 'sidebar'
      ? 'mt-2 w-full rounded-lg border border-slate-200 bg-white p-3 shadow-lg'
      : 'absolute right-0 z-30 mt-2 w-[min(22rem,calc(100vw-2rem))] rounded-lg border border-slate-200 bg-white p-3 shadow-xl';

  return (
    <div ref={containerRef} className={placement === 'sidebar' ? 'w-full' : 'relative'}>
      <button
        type="button"
        onClick={() => void toggleOpen()}
        className={`focus-ring relative inline-flex items-center gap-2 rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm font-semibold text-slate-700 hover:bg-slate-50 ${
          placement === 'sidebar' ? 'w-full justify-start' : ''
        }`}
        aria-label="Notifications"
        aria-expanded={isOpen}
      >
        <Bell className="h-4 w-4" aria-hidden="true" />
        <span className="hidden sm:inline">Alerts</span>
        {unreadCount > 0 && (
          <span className="min-w-5 rounded-full bg-rose-600 px-1.5 py-0.5 text-center text-xs font-semibold text-white">
            {unreadCount}
          </span>
        )}
      </button>

      {isOpen && (
        <div className={panelClass} role="region" aria-label="Alerts">
          <div className="flex items-center justify-between gap-3 border-b border-slate-100 pb-3">
            <div>
              <p className="text-sm font-semibold text-slate-950">Alerts</p>
              <p className="text-xs text-slate-500">{unreadCount} unread</p>
            </div>
            <button
              type="button"
              onClick={() => void markAllRead()}
              className="focus-ring inline-flex items-center gap-1.5 rounded-md px-2 py-1 text-xs font-semibold text-slate-600 hover:bg-slate-50"
              disabled={unreadCount === 0}
            >
              <CheckCheck className="h-3.5 w-3.5" aria-hidden="true" />
              Read all
            </button>
          </div>

          <label className="mt-3 flex items-center justify-between gap-3 rounded-lg bg-slate-50 px-3 py-2 text-sm font-medium text-slate-700">
            <span>
              <span>Email alerts</span>
              {emailDeliveryConfigured === false && (
                <span className="block text-xs font-medium text-amber-700">
                  Email alerts unavailable: app email delivery is not configured yet.
                </span>
              )}
            </span>
            <input
              type="checkbox"
              checked={emailDeliveryConfigured === true && emailAlertsEnabled}
              onChange={() => void toggleEmailAlerts()}
              disabled={emailDeliveryConfigured !== true}
              className="h-4 w-4 rounded border-slate-300 text-sky-600 focus:ring-sky-500"
            />
          </label>

          <div className="max-h-96 overflow-y-auto py-2">
            {isLoading ? (
              <p className="px-2 py-6 text-center text-sm font-medium text-slate-500">Loading alerts...</p>
            ) : notifications.length === 0 ? (
              <p className="px-2 py-6 text-center text-sm font-medium text-slate-500">No alerts yet.</p>
            ) : (
              <div className="space-y-2">
                {notifications.map((notification) => (
                  <article key={notification.id} className="rounded-lg border border-slate-100 bg-slate-50 p-3">
                    <div className="flex items-start justify-between gap-3">
                      <div>
                        <p className="text-sm font-semibold text-slate-950">{notification.title}</p>
                        <p className="mt-1 text-sm text-slate-600">{notification.message}</p>
                      </div>
                      <div className="flex shrink-0 items-center gap-2">
                        {!notification.isRead && <span className="h-2 w-2 rounded-full bg-rose-600" aria-label="Unread" />}
                        <button
                          type="button"
                          onClick={() => void deleteNotification(notification)}
                          className="focus-ring rounded-md p-1 text-slate-400 hover:bg-white hover:text-slate-700"
                          aria-label="Delete alert"
                          title="Delete alert"
                        >
                          <X className="h-3.5 w-3.5" aria-hidden="true" />
                        </button>
                      </div>
                    </div>
                    <div className="mt-3 flex flex-wrap items-center justify-between gap-2 text-xs font-medium text-slate-500">
                      <span>{formatRelative(notification.createdAt)}</span>
                      <span className="inline-flex items-center gap-1">
                        {notification.emailStatus === 'Failed' ? (
                          <MailWarning className="h-3.5 w-3.5 text-amber-600" aria-hidden="true" />
                        ) : (
                          <Mail className="h-3.5 w-3.5" aria-hidden="true" />
                        )}
                        {formatEmailStatus(notification.emailStatus, notification.emailErrorMessage)}
                      </span>
                    </div>
                    <div className="mt-3 flex flex-wrap gap-2">
                      {notification.monitorId && (
                        <Link
                          to={`/monitors/${notification.monitorId}`}
                          onClick={() => setIsOpen(false)}
                          className="focus-ring rounded-md bg-white px-2 py-1 text-xs font-semibold text-sky-700 hover:text-sky-800"
                        >
                          View monitor
                        </Link>
                      )}
                      {!notification.isRead && (
                        <button
                          type="button"
                          onClick={() => void markRead(notification.id)}
                          className="focus-ring rounded-md bg-white px-2 py-1 text-xs font-semibold text-slate-600 hover:text-slate-950"
                        >
                          Mark read
                        </button>
                      )}
                    </div>
                  </article>
                ))}
              </div>
            )}
          </div>
        </div>
      )}
    </div>
  );
}

function formatEmailStatus(status: Notification['emailStatus'], errorMessage?: string | null) {
  if (status === 'Skipped' && errorMessage?.toLowerCase().includes('disabled')) {
    return 'Email disabled';
  }

  switch (status) {
    case 'Sent':
      return 'Email sent';
    case 'Failed':
      return 'Email failed';
    case 'Skipped':
      return 'Email skipped';
    default:
      return 'Email not configured';
  }
}
