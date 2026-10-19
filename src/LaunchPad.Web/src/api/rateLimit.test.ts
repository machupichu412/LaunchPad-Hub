import { describe, expect, it } from 'vitest';
import { checkRateLimit, RateLimitError } from './rateLimit';
import { queryClient } from './queryClient';

describe('rate limit handling', () => {
  it('preserves the server retry period and stops automatic query retries', () => {
    let error: unknown;
    try {
      checkRateLimit(new Response(null, { status: 429, headers: { 'Retry-After': '120' } }));
    } catch (caught) { error = caught; }
    expect(error).toBeInstanceOf(RateLimitError);
    expect((error as RateLimitError).retryAfterSeconds).toBe(120);
    const retry = queryClient.getDefaultOptions().queries?.retry;
    expect(typeof retry).toBe('function');
    if (typeof retry === 'function') expect(retry(0, error as Error)).toBe(false);
  });

  it('uses a safe fallback for malformed retry headers', () => {
    expect(() => checkRateLimit(new Response(null, { status: 429, headers: { 'Retry-After': 'invalid' } })))
      .toThrow('Try again in 60 seconds');
  });
});
