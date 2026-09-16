import { describe, expect, it } from 'vitest';
import { composeRequestSignal } from './authedFetch';

/**
 * The timeout exists so a hung request cannot leave a view spinning forever. The part
 * worth pinning is that adding it did not cost the caller's own cancellation — getting
 * that wrong breaks cancel-on-unmount, which fails quietly rather than loudly.
 */
describe('composeRequestSignal', () => {
  it('aborts when the caller aborts, well before the timeout', () => {
    const controller = new AbortController();
    const signal = composeRequestSignal(controller.signal, 500);

    expect(signal.aborted).toBe(false);
    controller.abort();

    expect(signal.aborted).toBe(true);
  });

  it('aborts on timeout when the caller never does', async () => {
    const controller = new AbortController();
    const signal = composeRequestSignal(controller.signal, 5);

    await new Promise((resolve) => setTimeout(resolve, 25));

    expect(signal.aborted).toBe(true);
    expect(controller.signal.aborted).toBe(false);
  });

  it('still applies a timeout when the caller supplies no signal', async () => {
    const signal = composeRequestSignal(undefined, 5);

    await new Promise((resolve) => setTimeout(resolve, 25));

    expect(signal.aborted).toBe(true);
  });
});
