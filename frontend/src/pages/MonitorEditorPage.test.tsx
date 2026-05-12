import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { describe, expect, it } from 'vitest';
import { NewMonitorPage } from './MonitorEditorPage';

describe('MonitorEditorPage', () => {
  it('shows a visible dashboard return link near the editor heading', () => {
    render(
      <MemoryRouter>
        <NewMonitorPage />
      </MemoryRouter>
    );

    expect(screen.getByRole('heading', { name: /add monitor/i })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /back to dashboard/i })).toHaveClass('rounded-lg');
  });
});
