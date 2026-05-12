import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { MonitorForm } from './MonitorForm';

describe('MonitorForm', () => {
  it('keeps public status visibility off by default', () => {
    render(<MonitorForm submitLabel="Create monitor" onSubmit={vi.fn()} />);

    expect(screen.getByLabelText(/show this monitor on the public status page/i)).not.toBeChecked();
  });
});
