import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { NotificationBell } from './NotificationBell';

const apiMock = vi.hoisted(() => ({
  notifications: vi.fn(),
  notificationUnreadCount: vi.fn(),
  notificationPreferences: vi.fn(),
  updateNotificationPreferences: vi.fn(),
  markNotificationRead: vi.fn(),
  markAllNotificationsRead: vi.fn(),
  deleteNotification: vi.fn()
}));

vi.mock('../api/client', () => ({
  api: apiMock
}));

describe('NotificationBell', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    apiMock.notificationUnreadCount.mockResolvedValue({ unreadCount: 1 });
    apiMock.notificationPreferences.mockResolvedValue({ emailAlertsEnabled: true, emailDeliveryConfigured: false });
    apiMock.updateNotificationPreferences.mockResolvedValue({ emailAlertsEnabled: false, emailDeliveryConfigured: false });
    apiMock.notifications.mockResolvedValue([
      {
        id: 'n1',
        type: 'MonitorFailed',
        monitorId: 'm1',
        incidentId: 'i1',
        title: 'API is Down',
        message: 'Request timed out.',
        isRead: false,
        emailStatus: 'NotConfigured',
        emailErrorMessage: 'Email delivery is not configured.',
        createdAt: new Date().toISOString(),
        readAt: null
      }
    ]);
    apiMock.markNotificationRead.mockResolvedValue({
      id: 'n1',
      type: 'MonitorFailed',
      monitorId: 'm1',
      incidentId: 'i1',
      title: 'API is Down',
      message: 'Request timed out.',
      isRead: true,
      emailStatus: 'NotConfigured',
      emailErrorMessage: 'Email delivery is not configured.',
      createdAt: new Date().toISOString(),
      readAt: new Date().toISOString()
    });
    apiMock.markAllNotificationsRead.mockResolvedValue({ unreadCount: 0 });
    apiMock.deleteNotification.mockResolvedValue(undefined);
  });

  it('opens alerts and marks a notification read', async () => {
    const user = userEvent.setup();

    render(
      <MemoryRouter>
        <NotificationBell />
      </MemoryRouter>
    );

    await user.click(screen.getByRole('button', { name: /notifications/i }));

    expect(await screen.findByText('API is Down')).toBeInTheDocument();
    expect(screen.getByText('Request timed out.')).toBeInTheDocument();
    expect(screen.getAllByText('Email not configured').length).toBeGreaterThan(0);

    await user.click(screen.getByRole('button', { name: /mark read/i }));

    expect(apiMock.markNotificationRead).toHaveBeenCalledWith('n1');
  });

  it('renders as a full-width sidebar panel', async () => {
    const user = userEvent.setup();

    render(
      <MemoryRouter>
        <NotificationBell placement="sidebar" />
      </MemoryRouter>
    );

    const button = screen.getByRole('button', { name: /notifications/i });
    expect(button).toHaveClass('w-full');

    await user.click(button);

    const panel = await screen.findByRole('region', { name: /alerts/i });
    expect(panel).toHaveClass('w-full');
    expect(panel).not.toHaveClass('absolute');
  });

  it('closes when clicking outside', async () => {
    const user = userEvent.setup();

    render(
      <MemoryRouter>
        <NotificationBell />
        <button type="button">Outside</button>
      </MemoryRouter>
    );

    await user.click(screen.getByRole('button', { name: /notifications/i }));
    expect(await screen.findByText('API is Down')).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: /outside/i }));

    await waitFor(() => expect(screen.queryByText('API is Down')).not.toBeInTheDocument());
  });

  it('closes when pressing Escape', async () => {
    const user = userEvent.setup();

    render(
      <MemoryRouter>
        <NotificationBell />
      </MemoryRouter>
    );

    await user.click(screen.getByRole('button', { name: /notifications/i }));
    expect(await screen.findByText('API is Down')).toBeInTheDocument();

    await user.keyboard('{Escape}');

    await waitFor(() => expect(screen.queryByText('API is Down')).not.toBeInTheDocument());
  });

  it('updates the email alert preference', async () => {
    apiMock.notificationPreferences.mockResolvedValue({ emailAlertsEnabled: true, emailDeliveryConfigured: true });
    apiMock.updateNotificationPreferences.mockResolvedValue({ emailAlertsEnabled: false, emailDeliveryConfigured: true });
    const user = userEvent.setup();

    render(
      <MemoryRouter>
        <NotificationBell />
      </MemoryRouter>
    );

    await user.click(screen.getByRole('button', { name: /notifications/i }));
    expect(await screen.findByText('API is Down')).toBeInTheDocument();
    const emailToggle = screen.getByLabelText(/email alerts/i);

    await user.click(emailToggle);

    await waitFor(() => {
      expect(apiMock.updateNotificationPreferences).toHaveBeenCalledWith({ emailAlertsEnabled: false });
    });
  });

  it('explains when app email delivery is not configured', async () => {
    const user = userEvent.setup();

    render(
      <MemoryRouter>
        <NotificationBell />
      </MemoryRouter>
    );

    await user.click(screen.getByRole('button', { name: /notifications/i }));

    expect(await screen.findByText('Email alerts unavailable: app email delivery is not configured yet.')).toBeInTheDocument();
    const emailToggle = screen.getByLabelText(/email alerts/i);
    expect(emailToggle).toBeDisabled();
    expect(emailToggle).not.toBeChecked();
    expect(screen.getByText('Email not configured')).toBeInTheDocument();
  });

  it('keeps the email alert toggle enabled when delivery is configured', async () => {
    apiMock.notificationPreferences.mockResolvedValue({ emailAlertsEnabled: true, emailDeliveryConfigured: true });
    const user = userEvent.setup();

    render(
      <MemoryRouter>
        <NotificationBell />
      </MemoryRouter>
    );

    await user.click(screen.getByRole('button', { name: /notifications/i }));
    expect(await screen.findByText('API is Down')).toBeInTheDocument();
    const emailToggle = screen.getByLabelText(/email alerts/i);

    expect(emailToggle).toBeEnabled();
    expect(emailToggle).toBeChecked();
    expect(screen.queryByText(/email alerts unavailable/i)).not.toBeInTheDocument();
  });

  it('deletes one alert and updates the unread count', async () => {
    const user = userEvent.setup();

    render(
      <MemoryRouter>
        <NotificationBell />
      </MemoryRouter>
    );

    await user.click(screen.getByRole('button', { name: /notifications/i }));
    expect(await screen.findByText('API is Down')).toBeInTheDocument();
    expect(screen.getByText('1 unread')).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: /delete alert/i }));

    await waitFor(() => {
      expect(apiMock.deleteNotification).toHaveBeenCalledWith('n1');
      expect(screen.queryByText('API is Down')).not.toBeInTheDocument();
    });
    expect(screen.getByText('0 unread')).toBeInTheDocument();
    expect(screen.getByText('No alerts yet.')).toBeInTheDocument();
  });

  it('closes after opening the monitor link', async () => {
    const user = userEvent.setup();

    render(
      <MemoryRouter>
        <NotificationBell />
      </MemoryRouter>
    );

    await user.click(screen.getByRole('button', { name: /notifications/i }));
    expect(await screen.findByText('API is Down')).toBeInTheDocument();

    await user.click(screen.getByRole('link', { name: /view monitor/i }));

    await waitFor(() => expect(screen.queryByText('API is Down')).not.toBeInTheDocument());
  });

  it('shows when email alerts are disabled in alert history', async () => {
    apiMock.notifications.mockResolvedValue([
      {
        id: 'n1',
        type: 'MonitorFailed',
        monitorId: 'm1',
        incidentId: 'i1',
        title: 'API is Down',
        message: 'Request timed out.',
        isRead: false,
        emailStatus: 'Skipped',
        emailErrorMessage: 'Email alerts are disabled.',
        createdAt: new Date().toISOString(),
        readAt: null
      }
    ]);
    const user = userEvent.setup();

    render(
      <MemoryRouter>
        <NotificationBell />
      </MemoryRouter>
    );

    await user.click(screen.getByRole('button', { name: /notifications/i }));

    expect(await screen.findByText('Email disabled')).toBeInTheDocument();
  });
});
