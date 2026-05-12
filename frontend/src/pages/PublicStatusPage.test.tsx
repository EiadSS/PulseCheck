import { render, screen } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { PublicStatusPage } from './PublicStatusPage';

const apiMock = vi.hoisted(() => ({
  publicStatus: vi.fn()
}));

const authMock = vi.hoisted(() => ({
  user: null as null | { id: string; email: string; workspaceName: string; publicStatusSlug: string; emailAlertsEnabled: boolean; isAdmin: boolean }
}));

vi.mock('../api/client', () => ({
  api: apiMock
}));

vi.mock('../contexts/AuthContext', () => ({
  useAuth: () => authMock
}));

describe('PublicStatusPage', () => {
  beforeEach(() => {
    authMock.user = {
      id: 'user-1',
      email: 'ops@example.com',
      workspaceName: 'Acme Ops',
      publicStatusSlug: 'acme-ops',
      emailAlertsEnabled: true,
      isAdmin: false
    };
    apiMock.publicStatus.mockResolvedValue({
      slug: 'acme-ops',
      title: 'Acme Ops Status',
      overallStatus: 'Up',
      uptime24Hours: 100,
      uptime7Days: 99.95,
      uptime30Days: 99.9,
      monitors: [
        {
          id: 'monitor-1',
          name: 'Marketing Website',
          type: 'Website',
          currentStatus: 'Up',
          lastCheckedAt: new Date().toISOString(),
          lastResponseTimeMs: 180,
          uptime24Hours: 100,
          uptime7Days: 99.95,
          uptime30Days: 99.9
        }
      ],
      recentIncidents: []
    });
  });

  it('shows a dashboard return link for signed-in users', async () => {
    render(
      <MemoryRouter initialEntries={['/status/acme-ops']}>
        <Routes>
          <Route path="/status/:slug" element={<PublicStatusPage />} />
        </Routes>
      </MemoryRouter>
    );

    expect(await screen.findByRole('heading', { name: /acme ops status/i })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /back to dashboard/i })).toHaveAttribute('href', '/dashboard');
    expect(screen.getByRole('link', { name: /pulsecheck/i })).toHaveAttribute('href', '/dashboard');
  });
});
