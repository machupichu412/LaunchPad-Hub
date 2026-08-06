import { useMsal } from '@azure/msal-react';
import { useNavigate } from 'react-router-dom';
import { Avatar, Body1, Button, Caption1, Input, Tooltip, makeStyles, tokens } from '@fluentui/react-components';
import { AlertRegular, SearchRegular, WeatherMoonRegular, WeatherSunnyRegular } from '@fluentui/react-icons';
import { useActiveRole } from '../auth/ActiveRoleContext';
import { roleHomePath, roleLabel, type AppRole } from '../auth/roles';
import { useThemeMode } from '../theme/ThemeModeContext';

const useStyles = makeStyles({
  root: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalL,
    padding: `${tokens.spacingVerticalS} ${tokens.spacingHorizontalXL}`,
    borderBottomWidth: '1px',
    borderBottomStyle: 'solid',
    borderBottomColor: tokens.colorNeutralStroke2,
  },
  search: {
    flexGrow: 1,
    maxWidth: '360px',
  },
  rolePills: {
    display: 'flex',
    gap: tokens.spacingHorizontalXS,
  },
  identity: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalS,
  },
  identityText: {
    display: 'flex',
    flexDirection: 'column',
    lineHeight: '1.2',
  },
});

/**
 * Search and the notification bell are decorative (no backend) — out of scope for
 * this build-out. Role pills only render when multiple roles are held, per the
 * explicit requirement; a single-role user never sees a switcher with nothing to
 * switch between.
 */
export function Header() {
  const styles = useStyles();
  const navigate = useNavigate();
  const { accounts } = useMsal();
  const { activeRole, roles, setActiveRole } = useActiveRole();
  const { mode, toggleMode } = useThemeMode();
  const account = accounts[0];
  const displayName = account?.name ?? account?.username ?? 'Signed in';

  const switchRole = (role: AppRole) => {
    setActiveRole(role);
    navigate(roleHomePath(role));
  };

  return (
    <header className={styles.root}>
      <div className={styles.search}>
        <Input
          contentBefore={<SearchRegular />}
          placeholder="Search..."
          appearance="filled-lighter"
          disabled
          style={{ width: '100%' }}
        />
      </div>

      {roles.length > 1 && (
        <div className={styles.rolePills}>
          {roles.map((role) => (
            <Button
              key={role}
              size="small"
              shape="rounded"
              appearance={role === activeRole ? 'primary' : 'outline'}
              onClick={() => switchRole(role)}
            >
              {roleLabel(role)}
            </Button>
          ))}
        </div>
      )}

      <Tooltip content={mode === 'dark' ? 'Switch to light theme' : 'Switch to dark theme'} relationship="label">
        <Button
          icon={mode === 'dark' ? <WeatherSunnyRegular /> : <WeatherMoonRegular />}
          appearance="subtle"
          onClick={toggleMode}
          aria-label={mode === 'dark' ? 'Switch to light theme' : 'Switch to dark theme'}
        />
      </Tooltip>

      <Button icon={<AlertRegular />} appearance="subtle" disabled title="Notifications" />

      <div className={styles.identity}>
        <Avatar name={displayName} />
        <div className={styles.identityText}>
          <Body1>{displayName}</Body1>
          {activeRole && <Caption1>{roleLabel(activeRole)}</Caption1>}
        </div>
      </div>
    </header>
  );
}
