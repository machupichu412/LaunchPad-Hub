import { Body1, Title2 } from '@fluentui/react-components';
import { useApiTokenDiagnostics } from '../../auth/useApiTokenDiagnostics';

export function RoleAwareHome() {
  const { roles, spaIdTokenClaims, apiAccessTokenClaims } = useApiTokenDiagnostics();

  return (
    <>
      <Title2>Welcome to LaunchPad</Title2>

      <Body1>Roles: {roles.length > 0 ? roles.join(', ') : 'none assigned'}</Body1>

      {/* TEMPORARY diagnostic — remove once the missing-roles issue is confirmed fixed. */}
      <h3>SPA ID token</h3>
      <pre style={{ fontSize: 12, background: '#f4f4f4', padding: 12, overflow: 'auto' }}>
        {JSON.stringify(spaIdTokenClaims, null, 2)}
      </pre>

      <h3>API access token</h3>
      <pre style={{ fontSize: 12, background: '#f4f4f4', padding: 12, overflow: 'auto' }}>
        {JSON.stringify(apiAccessTokenClaims, null, 2)}
      </pre>
    </>
  );
}
