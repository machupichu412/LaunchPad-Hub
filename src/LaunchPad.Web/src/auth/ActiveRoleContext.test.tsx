import { useState } from 'react';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { AppRoles } from './roles';

// useRoles reaches MSAL through useApiAccessTokenClaims; the identity of what it returns is
// the thing under test, so it is mocked to return a stable array like the real one now does.
const ROLES = [AppRoles.Candidate];
vi.mock('./useRoles', () => ({
  useRoles: () => ({ roles: ROLES, isLoading: false }),
}));

const { ActiveRoleProvider, useActiveRole } = await import('./ActiveRoleContext');

/**
 * The provider wraps the entire router, so a new context value on every render re-renders
 * every consumer in the app. This pins the fix: re-rendering the provider for an unrelated
 * reason must hand consumers the same object back.
 */
describe('ActiveRoleProvider', () => {
  function Harness() {
    const [unrelated, setUnrelated] = useState(0);

    return (
      <ActiveRoleProvider>
        <button onClick={() => setUnrelated((n) => n + 1)}>re-render</button>
        <span data-testid="unrelated">{unrelated}</span>
        <Consumer />
      </ActiveRoleProvider>
    );
  }

  const seen: unknown[] = [];
  function Consumer() {
    const value = useActiveRole();
    seen.push(value);
    return <span data-testid="active-role">{value.activeRole ?? 'none'}</span>;
  }

  it('keeps one context value identity across unrelated re-renders', async () => {
    const user = userEvent.setup();
    seen.length = 0;

    render(<Harness />);
    expect(await screen.findByText(AppRoles.Candidate)).toBeInTheDocument();

    const before = seen.at(-1);
    await user.click(screen.getByRole('button', { name: 're-render' }));

    expect(screen.getByTestId('unrelated')).toHaveTextContent('1');
    expect(seen.at(-1)).toBe(before);
  });

  it('defaults the active role to Candidate when held', async () => {
    render(<Harness />);

    expect(await screen.findByTestId('active-role')).toHaveTextContent(AppRoles.Candidate);
  });
});
