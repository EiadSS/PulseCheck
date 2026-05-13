import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { AnalyticsPage } from './AnalyticsPage';

const apiMock = vi.hoisted(() => ({
  analyticsSummary: vi.fn()
}));

vi.mock('../api/client', async () => {
  const actual = await vi.importActual<typeof import('../api/client')>('../api/client');
  return {
    ...actual,
    api: apiMock
  };
});

describe('AnalyticsPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    apiMock.analyticsSummary.mockResolvedValue(summary);
  });

  it('renders owner analytics summary and recent signups', async () => {
    render(<AnalyticsPage />);

    expect(await screen.findByRole('heading', { name: /^analytics$/i })).toBeInTheDocument();
    expect(screen.getByText('Total users')).toBeInTheDocument();
    expect(screen.getByText('Active users')).toBeInTheDocument();
    expect(screen.getByText('Page views')).toBeInTheDocument();
    expect(screen.getByText('Email alert health')).toBeInTheDocument();
    expect(screen.getByText('/dashboard')).toBeInTheDocument();
    expect(screen.getByText('owner@example.com')).toBeInTheDocument();
  });

  it('reloads analytics when the range changes', async () => {
    const user = userEvent.setup();

    render(<AnalyticsPage />);

    await screen.findByRole('heading', { name: /^analytics$/i });
    await user.click(screen.getByRole('button', { name: '30d' }));

    await waitFor(() => {
      expect(apiMock.analyticsSummary).toHaveBeenCalledWith('30d');
    });
  });

  it('shows empty states when there is no analytics data', async () => {
    apiMock.analyticsSummary.mockResolvedValue({
      ...summary,
      newUsers: 0,
      pageViews: 0,
      topPages: [],
      monitorActivity: [],
      newUsersOverTime: summary.newUsersOverTime.map((point) => ({ ...point, count: 0 })),
      recentSignups: []
    });

    render(<AnalyticsPage />);

    expect(await screen.findByText('No page views collected yet.')).toBeInTheDocument();
    expect(screen.getByText('No new users in this window yet.')).toBeInTheDocument();
    expect(screen.getByText('Monitor activity appears after scheduled or manual checks run.')).toBeInTheDocument();
    expect(screen.getByText('No accounts have been created yet.')).toBeInTheDocument();
  });

  it('labels app routes separately from monitored URL activity', async () => {
    render(<AnalyticsPage />);

    expect(await screen.findByText('Top app pages')).toBeInTheDocument();
    expect(screen.getByText('Monitor activity')).toBeInTheDocument();
    expect(screen.getByText('/dashboard')).toBeInTheDocument();
    expect(screen.getByText('Marketing Site')).toBeInTheDocument();
    expect(screen.getByText('https://example.com')).toBeInTheDocument();
  });
});

const summary = {
  range: '7d',
  since: '2026-05-05T00:00:00Z',
  generatedAt: '2026-05-12T00:00:00Z',
  totalUsers: 3,
  newUsers: 2,
  activeUsers: 1,
  totalMonitors: 4,
  monitorsCreated: 2,
  averageMonitorsPerUser: 1.33,
  pageViews: 12,
  publicStatusPageViews: 3,
  monitorChecks: 20,
  averageResponseTimeMs: 180,
  incidentsOpened: 2,
  incidentsResolved: 1,
  notificationsCreated: 4,
  topPages: [
    { path: '/dashboard', views: 6 },
    { path: '/status/acme', views: 3 }
  ],
  checkStatusCounts: [
    { status: 'Up', count: 16 },
    { status: 'Error', count: 4 }
  ],
  emailStatusCounts: [
    { status: 'Sent', count: 3 },
    { status: 'Failed', count: 1 }
  ],
  monitorActivity: [
    {
      id: 'monitor-1',
      name: 'Marketing Site',
      url: 'https://example.com',
      currentStatus: 'Up',
      checkCount: 4,
      lastCheckedAt: '2026-05-12T00:00:00Z'
    }
  ],
  newUsersOverTime: [
    { periodStart: '2026-05-06T00:00:00Z', count: 0 },
    { periodStart: '2026-05-07T00:00:00Z', count: 1 },
    { periodStart: '2026-05-08T00:00:00Z', count: 0 },
    { periodStart: '2026-05-09T00:00:00Z', count: 0 },
    { periodStart: '2026-05-10T00:00:00Z', count: 1 },
    { periodStart: '2026-05-11T00:00:00Z', count: 0 },
    { periodStart: '2026-05-12T00:00:00Z', count: 0 }
  ],
  recentSignups: [
    {
      id: 'user-1',
      email: 'owner@example.com',
      workspaceName: 'Owner',
      createdAt: '2026-05-12T00:00:00Z'
    }
  ]
};
