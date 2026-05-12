import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { DashboardPage } from './DashboardPage';

const apiMock = vi.hoisted(() => ({
  monitors: vi.fn(),
  dashboardSummary: vi.fn(),
  pauseMonitor: vi.fn(),
  resumeMonitor: vi.fn(),
  runMonitorCheck: vi.fn(),
  deleteMonitor: vi.fn()
}));

vi.mock('../api/client', () => ({
  API_BASE_URL: 'http://localhost:5080',
  getStoredToken: () => null,
  api: apiMock
}));

describe('DashboardPage', () => {
  beforeEach(() => {
    apiMock.monitors.mockResolvedValue([
      {
        id: '1',
        name: 'Marketing Website',
        url: 'https://example.com',
        type: 'Website',
        currentStatus: 'Up',
        isPaused: false,
        isPublic: true,
        lastCheckedAt: new Date().toISOString(),
        lastStatusCode: 200,
        lastResponseTimeMs: 180,
        lastErrorMessage: null,
        sslCertificateStatus: 'Valid',
        sslCertificateExpiresAt: new Date(Date.now() + 30 * 86400000).toISOString(),
        sslCertificateDaysRemaining: 30,
        lastSslErrorMessage: null,
        uptimePercentage: 100,
        openIncidentCount: 0
      },
      {
        id: '2',
        name: 'Search API',
        url: 'https://api.example.com',
        type: 'Api',
        currentStatus: 'Error',
        isPaused: false,
        isPublic: true,
        lastCheckedAt: new Date().toISOString(),
        lastStatusCode: 500,
        lastResponseTimeMs: 230,
        lastErrorMessage: 'Expected HTTP 200',
        sslCertificateStatus: 'ExpiringSoon',
        sslCertificateExpiresAt: new Date(Date.now() + 10 * 86400000).toISOString(),
        sslCertificateDaysRemaining: 10,
        lastSslErrorMessage: null,
        uptimePercentage: 92,
        openIncidentCount: 1
      }
    ]);
    apiMock.dashboardSummary.mockResolvedValue({
      totalMonitors: 2,
      up: 1,
      degraded: 0,
      error: 1,
      down: 0,
      paused: 0,
      openIncidents: 1,
      averageUptime24Hours: 96
    });
    apiMock.runMonitorCheck.mockResolvedValue({});
  });

  it('filters monitors by status', async () => {
    render(
      <MemoryRouter>
        <DashboardPage />
      </MemoryRouter>
    );

    expect(await screen.findByText('Marketing Website')).toBeInTheDocument();
    expect(screen.getByText('Search API')).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Error' }));

    await waitFor(() => {
      expect(screen.queryByText('Marketing Website')).not.toBeInTheDocument();
      expect(screen.getByText('Search API')).toBeInTheDocument();
    });
  });

  it('runs a manual check from a monitor card', async () => {
    const user = userEvent.setup();

    render(
      <MemoryRouter>
        <DashboardPage />
      </MemoryRouter>
    );

    expect(await screen.findByText('Marketing Website')).toBeInTheDocument();

    await user.click(screen.getAllByRole('button', { name: /run check now/i })[0]);

    await waitFor(() => {
      expect(apiMock.runMonitorCheck).toHaveBeenCalledWith('1');
    });
  });
});
