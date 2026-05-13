import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Link, MemoryRouter, Route, Routes } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { AnalyticsTracker } from './AnalyticsTracker';

const apiMock = vi.hoisted(() => ({
  trackAnalyticsEvent: vi.fn()
}));
const authState = vi.hoisted((): { user: { id: string } | null; isLoading: boolean } => ({
  user: { id: 'user-1' },
  isLoading: false
}));

vi.mock('../api/client', () => ({
  api: apiMock
}));

vi.mock('../contexts/AuthContext', () => ({
  useAuth: () => authState
}));

describe('AnalyticsTracker', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    authState.user = { id: 'user-1' };
    authState.isLoading = false;
    apiMock.trackAnalyticsEvent.mockResolvedValue(undefined);
  });

  it('sends page-view events on navigation', async () => {
    const user = userEvent.setup();

    render(
      <MemoryRouter initialEntries={['/dashboard']}>
        <AnalyticsTracker />
        <Routes>
          <Route path="/dashboard" element={<Link to="/analytics">Analytics</Link>} />
          <Route path="/analytics" element={<h1>Analytics</h1>} />
        </Routes>
      </MemoryRouter>
    );

    await waitFor(() => {
      expect(apiMock.trackAnalyticsEvent).toHaveBeenCalledWith({ eventType: 'PageView', path: '/dashboard' });
    });

    await user.click(screen.getByRole('link', { name: /analytics/i }));

    await waitFor(() => {
      expect(apiMock.trackAnalyticsEvent).toHaveBeenCalledWith({ eventType: 'PageView', path: '/analytics' });
    });
  });

  it('does not record the authenticated root redirect as a page view', async () => {
    render(
      <MemoryRouter initialEntries={['/']}>
        <AnalyticsTracker />
        <Routes>
          <Route path="/" element={<h1>Redirecting</h1>} />
        </Routes>
      </MemoryRouter>
    );

    await waitFor(() => {
      expect(apiMock.trackAnalyticsEvent).not.toHaveBeenCalled();
    });
  });

  it('records the public landing page for signed-out visitors', async () => {
    authState.user = null;

    render(
      <MemoryRouter initialEntries={['/']}>
        <AnalyticsTracker />
        <Routes>
          <Route path="/" element={<h1>Landing</h1>} />
        </Routes>
      </MemoryRouter>
    );

    await waitFor(() => {
      expect(apiMock.trackAnalyticsEvent).toHaveBeenCalledWith({ eventType: 'PageView', path: '/' });
    });
  });

  it('waits for auth state before tracking page views', async () => {
    authState.isLoading = true;

    render(
      <MemoryRouter initialEntries={['/dashboard']}>
        <AnalyticsTracker />
        <Routes>
          <Route path="/dashboard" element={<h1>Dashboard</h1>} />
        </Routes>
      </MemoryRouter>
    );

    await waitFor(() => {
      expect(apiMock.trackAnalyticsEvent).not.toHaveBeenCalled();
    });
  });
});
