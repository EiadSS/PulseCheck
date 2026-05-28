import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { MonitorDetailsPage } from './MonitorDetailsPage';

const apiMock = vi.hoisted(() => ({
  monitor: vi.fn(),
  checks: vi.fn(),
  responseTimes: vi.fn(),
  incidents: vi.fn(),
  pauseMonitor: vi.fn(),
  resumeMonitor: vi.fn(),
  runMonitorCheck: vi.fn()
}));

vi.mock('../api/client', () => ({
  api: apiMock
}));

describe('MonitorDetailsPage', () => {
  beforeEach(() => {
    apiMock.monitor.mockResolvedValue({
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
      openIncidentCount: 0,
      checkIntervalSeconds: 60,
      timeoutSeconds: 10,
      degradedThresholdMs: 800,
      expectedStatusCode: 200,
      expectedKeyword: null,
      uptime24Hours: 100,
      uptime7Days: 99.9,
      uptime30Days: 99.8
    });
    apiMock.checks.mockResolvedValue([
      {
        id: 'c1',
        status: 'Up',
        statusCode: 200,
        responseTimeMs: 180,
        errorMessage: null,
        checkedAt: new Date().toISOString()
      }
    ]);
    apiMock.responseTimes.mockResolvedValue([
      {
        checkedAt: new Date().toISOString(),
        responseTimeMs: 180,
        checkCount: 1
      }
    ]);
    apiMock.incidents.mockResolvedValue([]);
    apiMock.runMonitorCheck.mockResolvedValue({});
  });

  it('renders monitor details and recent check history', async () => {
    render(
      <MemoryRouter initialEntries={['/monitors/1']}>
        <Routes>
          <Route path="/monitors/:id" element={<MonitorDetailsPage />} />
        </Routes>
      </MemoryRouter>
    );

    expect(await screen.findByRole('heading', { name: 'Marketing Website' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /back to dashboard/i })).toHaveClass('rounded-lg');
    expect(screen.getByText('Recent checks')).toBeInTheDocument();
    expect(screen.getByText('180 ms')).toBeInTheDocument();
    expect(screen.getByText('No incidents recorded.')).toBeInTheDocument();
    expect(apiMock.checks).toHaveBeenCalledWith('1', '30d');
    expect(apiMock.responseTimes).toHaveBeenCalledWith('1', '24h');
  });

  it('reloads only response-time chart data when the range changes', async () => {
    render(
      <MemoryRouter initialEntries={['/monitors/1']}>
        <Routes>
          <Route path="/monitors/:id" element={<MonitorDetailsPage />} />
        </Routes>
      </MemoryRouter>
    );

    expect(await screen.findByRole('heading', { name: 'Marketing Website' })).toBeInTheDocument();
    apiMock.checks.mockClear();
    apiMock.responseTimes.mockClear();

    await userEvent.click(screen.getByRole('button', { name: '7d' }));

    await waitFor(() => {
      expect(apiMock.responseTimes).toHaveBeenCalledWith('1', '7d');
    });
    expect(apiMock.checks).not.toHaveBeenCalled();
  });

  it('shows an empty response-time state when no chart points exist', async () => {
    apiMock.responseTimes.mockResolvedValue([]);

    render(
      <MemoryRouter initialEntries={['/monitors/1']}>
        <Routes>
          <Route path="/monitors/:id" element={<MonitorDetailsPage />} />
        </Routes>
      </MemoryRouter>
    );

    expect(await screen.findByText('No response-time data for this range yet.')).toBeInTheDocument();
  });

  it('runs a manual check from the monitor detail page', async () => {
    render(
      <MemoryRouter initialEntries={['/monitors/1']}>
        <Routes>
          <Route path="/monitors/:id" element={<MonitorDetailsPage />} />
        </Routes>
      </MemoryRouter>
    );

    expect(await screen.findByRole('heading', { name: 'Marketing Website' })).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: /run check now/i }));

    expect(apiMock.runMonitorCheck).toHaveBeenCalledWith('1');
  });
});
