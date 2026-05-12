import { render, screen } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { AppLayout } from './AppLayout';

const authMock = vi.hoisted(() => ({
  user: {
    id: 'user-1',
    email: 'owner@example.com',
    workspaceName: 'Owner',
    publicStatusSlug: 'owner',
    emailAlertsEnabled: true,
    isAdmin: true
  },
  logout: vi.fn()
}));

vi.mock('../contexts/AuthContext', () => ({
  useAuth: () => authMock
}));

vi.mock('./NotificationBell', () => ({
  NotificationBell: () => <div>Alerts</div>
}));

describe('AppLayout', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    authMock.user = {
      id: 'user-1',
      email: 'owner@example.com',
      workspaceName: 'Owner',
      publicStatusSlug: 'owner',
      emailAlertsEnabled: true,
      isAdmin: true
    };
  });

  it('shows analytics navigation for admin users', () => {
    renderLayout();

    expect(screen.getAllByRole('link', { name: /analytics/i })[0]).toHaveAttribute('href', '/analytics');
  });

  it('hides analytics navigation for non-admin users', () => {
    authMock.user = { ...authMock.user, isAdmin: false };

    renderLayout();

    expect(screen.queryByRole('link', { name: /analytics/i })).not.toBeInTheDocument();
  });
});

function renderLayout() {
  render(
    <MemoryRouter initialEntries={['/dashboard']}>
      <Routes>
        <Route element={<AppLayout />}>
          <Route path="/dashboard" element={<div>Dashboard</div>} />
        </Route>
      </Routes>
    </MemoryRouter>
  );
}
