import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { SloPage } from './SloPage';

const apiMock = vi.hoisted(() => ({
  sloSummary: vi.fn()
}));

vi.mock('../api/client', () => ({
  api: apiMock
}));

describe('SloPage', () => {
  beforeEach(() => {
    apiMock.sloSummary.mockResolvedValue({
      targetPercentage: 99.9,
      windows: [
        { range: '24h', uptimePercentage: 100, errorBudgetUsedPercentage: 0, errorBudgetRemainingPercentage: 100, isCompliant: true },
        { range: '7d', uptimePercentage: 99.95, errorBudgetUsedPercentage: 50, errorBudgetRemainingPercentage: 50, isCompliant: true },
        { range: '30d', uptimePercentage: 99.8, errorBudgetUsedPercentage: 100, errorBudgetRemainingPercentage: 0, isCompliant: false }
      ],
      monitors: [
        {
          id: 'm1',
          name: 'API',
          currentStatus: 'Down',
          uptime24Hours: 99,
          uptime7Days: 99.5,
          uptime30Days: 99.8,
          errorBudgetUsed30Days: 100,
          isCompliant30Days: false
        }
      ]
    });
  });

  it('renders recruiter-friendly reliability copy', async () => {
    render(
      <MemoryRouter>
        <SloPage />
      </MemoryRouter>
    );

    expect(await screen.findByRole('heading', { name: /reliability targets/i })).toBeInTheDocument();
    expect(screen.getAllByText(/uptime target/i).length).toBeGreaterThan(0);
    expect(screen.getByRole('heading', { name: /uptime over time/i })).toBeInTheDocument();
    expect(screen.getAllByText(/downtime budget used/i).length).toBeGreaterThan(0);
    expect(screen.getByRole('heading', { name: /monitors at risk/i })).toBeInTheDocument();
    expect(screen.getByTitle(/availability goal/i)).toBeInTheDocument();
    expect(screen.getByTitle(/amount of downtime allowed/i)).toBeInTheDocument();
    expect(screen.getByTitle(/allowed downtime has already been used/i)).toBeInTheDocument();
    expect(screen.getByText('99.9%')).toBeInTheDocument();
    expect(screen.getByText('API')).toBeInTheDocument();
    expect(screen.getByText('At risk')).toBeInTheDocument();
    expect(screen.queryByText(/SLO\/SLA/i)).not.toBeInTheDocument();
    expect(screen.queryByText(/\bSLA\b/i)).not.toBeInTheDocument();
  });
});
