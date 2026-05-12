import { render, screen } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { LandingPage } from './LandingPage';

const authMock = vi.hoisted(() => ({
  user: null as null | { id: string; email: string; workspaceName: string; publicStatusSlug: string; emailAlertsEnabled: boolean; isAdmin: boolean }
}));

vi.mock('../contexts/AuthContext', () => ({
  useAuth: () => authMock
}));

describe('LandingPage', () => {
  beforeEach(() => {
    authMock.user = null;
  });

  it('promotes account creation without demo mode', () => {
    render(
      <MemoryRouter>
        <LandingPage />
      </MemoryRouter>
    );

    expect(screen.getByRole('link', { name: /create account/i })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /sign in/i })).toBeInTheDocument();
    expect(screen.queryByText(/try demo/i)).not.toBeInTheDocument();
  });

  it('sends signed-in users straight to the dashboard', () => {
    authMock.user = {
      id: 'user-1',
      email: 'ops@example.com',
      workspaceName: 'Acme Ops',
      publicStatusSlug: 'acme-ops',
      emailAlertsEnabled: true,
      isAdmin: false
    };

    render(
      <MemoryRouter initialEntries={['/']}>
        <Routes>
          <Route path="/" element={<LandingPage />} />
          <Route path="/dashboard" element={<h1>Dashboard</h1>} />
        </Routes>
      </MemoryRouter>
    );

    expect(screen.getByRole('heading', { name: /dashboard/i })).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: /open dashboard/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: /go to app/i })).not.toBeInTheDocument();
  });
});
