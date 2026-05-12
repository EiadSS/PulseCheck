import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Link, MemoryRouter, Route, Routes } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { AnalyticsTracker } from './AnalyticsTracker';

const apiMock = vi.hoisted(() => ({
  trackAnalyticsEvent: vi.fn()
}));

vi.mock('../api/client', () => ({
  api: apiMock
}));

describe('AnalyticsTracker', () => {
  beforeEach(() => {
    vi.clearAllMocks();
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
});
