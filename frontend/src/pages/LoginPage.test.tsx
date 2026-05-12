import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { ApiError } from '../api/client';
import { LoginPage, RegisterPage } from './LoginPage';

const authMock = vi.hoisted(() => ({
  user: null,
  isLoading: false,
  login: vi.fn(),
  register: vi.fn(),
  logout: vi.fn()
}));

vi.mock('../contexts/AuthContext', () => ({
  useAuth: () => authMock
}));

describe('LoginPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders sign-in without demo actions', () => {
    render(
      <MemoryRouter>
        <LoginPage />
      </MemoryRouter>
    );

    expect(screen.getByRole('heading', { name: /welcome back/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /sign in/i })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /try demo/i })).not.toBeInTheDocument();
  });

  it('renders registration without the email verification helper copy', () => {
    render(
      <MemoryRouter>
        <RegisterPage />
      </MemoryRouter>
    );

    expect(screen.getByRole('heading', { name: /create your workspace/i })).toBeInTheDocument();
    expect(screen.queryByText(/use a real email address/i)).not.toBeInTheDocument();
  });

  it('shows a friendly password rule message on registration', async () => {
    render(
      <MemoryRouter>
        <RegisterPage />
      </MemoryRouter>
    );

    await userEvent.type(screen.getByLabelText(/workspace name/i), 'Acme Ops');
    await userEvent.type(screen.getByLabelText(/email/i), 'ops@example.com');
    await userEvent.type(screen.getByLabelText(/^password$/i), 'password1');
    await userEvent.click(screen.getByRole('button', { name: /create account/i }));

    expect(screen.getByText('Add at least one uppercase letter.')).toBeInTheDocument();
    expect(authMock.register).not.toHaveBeenCalled();
  });

  it('shows a friendly invalid email message on registration', async () => {
    render(
      <MemoryRouter>
        <RegisterPage />
      </MemoryRouter>
    );

    await userEvent.type(screen.getByLabelText(/workspace name/i), 'Acme Ops');
    await userEvent.type(screen.getByLabelText(/email/i), 'not-an-email');
    await userEvent.type(screen.getByLabelText(/^password$/i), 'Password1');
    await userEvent.click(screen.getByRole('button', { name: /create account/i }));

    expect(screen.getByText('Enter a valid email address.')).toBeInTheDocument();
    expect(authMock.register).not.toHaveBeenCalled();
  });

  it('shows a friendly duplicate email message on registration', async () => {
    authMock.register.mockRejectedValueOnce(
      new ApiError('An account with this email already exists. Sign in instead.', [
        'An account with this email already exists. Sign in instead.'
      ])
    );

    render(
      <MemoryRouter>
        <RegisterPage />
      </MemoryRouter>
    );

    await userEvent.type(screen.getByLabelText(/workspace name/i), 'Acme Ops');
    await userEvent.type(screen.getByLabelText(/email/i), 'ops@example.com');
    await userEvent.type(screen.getByLabelText(/^password$/i), 'Password1');
    await userEvent.click(screen.getByRole('button', { name: /create account/i }));

    expect(await screen.findByText('An account with this email already exists. Sign in instead.')).toBeInTheDocument();
  });
});
