import { Title2, Body1 } from '@fluentui/react-components';
import { useRoles } from '../../auth/useRoles';

export function RoleAwareHome() {
  const roles = useRoles();
  return (
    <>
      <Title2>Welcome to LaunchPad</Title2>
      <Body1>Your roles: {roles.length > 0 ? roles.join(', ') : 'none assigned yet'}</Body1>
    </>
  );
}
