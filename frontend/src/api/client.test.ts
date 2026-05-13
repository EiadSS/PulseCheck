import { afterEach, describe, expect, it, vi } from 'vitest';

describe('api client errors', () => {
  afterEach(() => {
    vi.unstubAllEnvs();
    vi.unstubAllGlobals();
    vi.resetModules();
  });

  it('turns Vercel NOT_FOUND text into a friendly API error', async () => {
    vi.stubEnv('VITE_API_URL', 'https://api.example.com');
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue(
        new Response('The page could not be found NOT_FOUND yul1::abc', {
          status: 404,
          headers: { 'content-type': 'text/plain' }
        })
      )
    );
    const { request } = await import('./client');

    await expect(request('/api/auth/register')).rejects.toMatchObject({
      message: 'Unable to reach PulseCheck API. Please try again shortly.'
    });
  });

  it('catches a missing protocol in VITE_API_URL before making a request', async () => {
    vi.stubEnv('VITE_API_URL', 'pulsecheck-production-2678.up.railway.app');
    const fetchMock = vi.fn();
    vi.stubGlobal('fetch', fetchMock);
    const { request } = await import('./client');

    await expect(request('/api/auth/register')).rejects.toMatchObject({
      message: 'Unable to reach PulseCheck API. Please try again shortly.'
    });
    expect(fetchMock).not.toHaveBeenCalled();
  });
});
