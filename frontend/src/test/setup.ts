import '@testing-library/jest-dom/vitest';
import { afterAll, beforeAll, vi } from 'vitest';

class ResizeObserverMock {
  observe() {}
  unobserve() {}
  disconnect() {}
}

Object.defineProperty(window, 'ResizeObserver', {
  writable: true,
  configurable: true,
  value: ResizeObserverMock
});

Object.defineProperty(window, 'matchMedia', {
  writable: true,
  value: vi.fn().mockImplementation((query: string) => ({
    matches: false,
    media: query,
    onchange: null,
    addListener: vi.fn(),
    removeListener: vi.fn(),
    addEventListener: vi.fn(),
    removeEventListener: vi.fn(),
    dispatchEvent: vi.fn()
  }))
});

const originalWarn = console.warn;
const originalError = console.error;

beforeAll(() => {
  vi.spyOn(console, 'warn').mockImplementation((...args: unknown[]) => {
    if (String(args[0]).includes('width(0) and height(0) of chart')) {
      return;
    }

    originalWarn(...args);
  });

  vi.spyOn(console, 'error').mockImplementation((...args: unknown[]) => {
    if (String(args[0]).includes('width(0) and height(0) of chart')) {
      return;
    }

    originalError(...args);
  });
});

afterAll(() => {
  vi.restoreAllMocks();
});
