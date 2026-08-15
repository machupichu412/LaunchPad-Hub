import { useState } from 'react';
import { useMsal } from '@azure/msal-react';
import { useNavigate } from 'react-router-dom';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Avatar,
  Body1,
  Button,
  Caption1,
  CounterBadge,
  Divider,
  Menu,
  MenuItem,
  MenuList,
  MenuPopover,
  MenuTrigger,
  Popover,
  PopoverSurface,
  PopoverTrigger,
  Spinner,
  Tooltip,
  makeStyles,
  mergeClasses,
  tokens,
} from '@fluentui/react-components';
import { AlertRegular, ChevronDownRegular, WeatherMoonRegular, WeatherSunnyRegular } from '@fluentui/react-icons';
import { useActiveRole } from '../auth/ActiveRoleContext';
import { roleHomePath, roleLabel, type AppRole } from '../auth/roles';
import { useThemeMode } from '../theme/ThemeModeContext';
import { getMyNotifications, getUnreadNotificationCount, markAllNotificationsRead, markNotificationRead } from '../api/notifications';
import { useMyAvatarUrl } from '../auth/useMyAvatarUrl';
import { AvatarEditorDialog } from './AvatarEditorDialog';

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
  // Everything past this point is "account controls" — a distinct group
  // pinned to the far right, set apart by the divider below rather than blending
  // into one undifferentiated row of buttons.
  actions: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalS,
    marginLeft: 'auto',
  },
  divider: {
    height: '24px',
  },
  bellIconWrap: {
    position: 'relative',
    display: 'inline-flex',
  },
  bellBadge: {
    position: 'absolute',
    top: '-4px',
    right: '-4px',
  },
  notificationPanel: {
    width: '360px',
    maxHeight: '420px',
    display: 'flex',
    flexDirection: 'column',
  },
  notificationHeader: {
    display: 'flex',
    justifyContent: 'space-between',
    alignItems: 'center',
    padding: `${tokens.spacingVerticalS} ${tokens.spacingHorizontalM}`,
  },
  notificationList: {
    overflowY: 'auto',
  },
  notificationItem: {
    display: 'flex',
    flexDirection: 'column',
    width: '100%',
    textAlign: 'left',
    gap: tokens.spacingVerticalXXS,
    padding: `${tokens.spacingVerticalS} ${tokens.spacingHorizontalM}`,
    borderTopWidth: '1px',
    borderTopStyle: 'solid',
    borderTopColor: tokens.colorNeutralStroke2,
    borderLeftStyle: 'none',
    borderRightStyle: 'none',
    borderBottomStyle: 'none',
    backgroundColor: 'transparent',
    cursor: 'pointer',
    ':hover': {
      backgroundColor: tokens.colorSubtleBackgroundHover,
    },
  },
  notificationItemUnread: {
    backgroundColor: tokens.colorBrandBackground2,
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
  // A role-label-sized trigger — reads like the plain Caption1 it replaces when
  // there's more than one role to switch between, not a full-size button.
  roleMenuTrigger: {
    minHeight: 'auto',
    padding: 0,
    border: 'none',
    justifyContent: 'flex-start',
    fontSize: tokens.fontSizeBase200,
    fontWeight: tokens.fontWeightRegular,
    color: tokens.colorNeutralForeground3,
  },
});

export function Header() {
  const styles = useStyles();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const { accounts } = useMsal();
  const { activeRole, roles, setActiveRole } = useActiveRole();
  const { mode, toggleMode } = useThemeMode();
  const account = accounts[0];
  const displayName = account?.name ?? account?.username ?? 'Signed in';
  const { url: avatarUrl } = useMyAvatarUrl();

  const switchRole = (role: AppRole) => {
    setActiveRole(role);
    navigate(roleHomePath(role));
  };

  // --- Notifications: unread count polls in the background; the list only loads
  // once the popover is actually opened. ---
  const { data: unreadCount } = useQuery({
    queryKey: ['notifications', 'unread-count'],
    queryFn: getUnreadNotificationCount,
    refetchInterval: 30_000,
  });

  const [notificationsOpen, setNotificationsOpen] = useState(false);
  const { data: notifications, isLoading: notificationsLoading } = useQuery({
    queryKey: ['notifications', 'recent'],
    queryFn: getMyNotifications,
    enabled: notificationsOpen,
  });

  const handleNotificationClick = async (notificationId: number, isRead: boolean) => {
    if (isRead) return;
    await markNotificationRead(notificationId);
    queryClient.invalidateQueries({ queryKey: ['notifications'] });
  };

  const handleMarkAllRead = async () => {
    await markAllNotificationsRead();
    queryClient.invalidateQueries({ queryKey: ['notifications'] });
  };

  return (
    <header className={styles.root}>
      <div className={styles.actions}>
        <Divider vertical className={styles.divider} />

        <Tooltip content={mode === 'dark' ? 'Switch to light theme' : 'Switch to dark theme'} relationship="label">
          <Button
            icon={mode === 'dark' ? <WeatherSunnyRegular /> : <WeatherMoonRegular />}
            appearance="subtle"
            onClick={toggleMode}
            aria-label={mode === 'dark' ? 'Switch to light theme' : 'Switch to dark theme'}
          />
        </Tooltip>

        <Popover open={notificationsOpen} onOpenChange={(_, data) => setNotificationsOpen(data.open)} positioning="below-end">
          <PopoverTrigger disableButtonEnhancement>
            <Button
              appearance="subtle"
              aria-label={unreadCount ? `Notifications, ${unreadCount} unread` : 'Notifications'}
              icon={
                <span className={styles.bellIconWrap}>
                  <AlertRegular />
                  {!!unreadCount && unreadCount > 0 && (
                    <CounterBadge className={styles.bellBadge} count={unreadCount} size="small" color="danger" />
                  )}
                </span>
              }
            />
          </PopoverTrigger>
          <PopoverSurface>
            <div className={styles.notificationPanel}>
              <div className={styles.notificationHeader}>
                <Body1>
                  <strong>Notifications</strong>
                </Body1>
                <Button appearance="transparent" size="small" disabled={!unreadCount} onClick={handleMarkAllRead}>
                  Mark all read
                </Button>
              </div>
              <div className={styles.notificationList}>
                {notificationsLoading && <Spinner size="tiny" label="Loading..." style={{ padding: tokens.spacingVerticalM }} />}
                {!notificationsLoading && (notifications?.length ?? 0) === 0 && (
                  <Body1 style={{ display: 'block', padding: tokens.spacingVerticalM }}>You're all caught up.</Body1>
                )}
                {notifications?.map((n) => (
                  <button
                    key={n.notificationId}
                    type="button"
                    className={mergeClasses(styles.notificationItem, !n.isRead && styles.notificationItemUnread)}
                    onClick={() => handleNotificationClick(n.notificationId, n.isRead)}
                  >
                    <Body1 block>
                      <strong>{n.subject}</strong>
                    </Body1>
                    <Caption1 block>{n.body}</Caption1>
                    <Caption1 block>{new Date(n.createdUtc).toLocaleString()}</Caption1>
                  </button>
                ))}
              </div>
            </div>
          </PopoverSurface>
        </Popover>

        <div className={styles.identity}>
          <AvatarEditorDialog
            trigger={
              <Avatar
                name={displayName}
                image={avatarUrl ? { src: avatarUrl } : undefined}
                style={{ cursor: 'pointer' }}
                aria-label="Update your photo"
              />
            }
          />
          <div className={styles.identityText}>
            <Body1 block>{displayName}</Body1>
            {activeRole && roles.length > 1 ? (
              <Menu>
                <MenuTrigger disableButtonEnhancement>
                  <Button
                    appearance="transparent"
                    size="small"
                    className={styles.roleMenuTrigger}
                    icon={<ChevronDownRegular fontSize={14} />}
                    iconPosition="after"
                  >
                    {roleLabel(activeRole)}
                  </Button>
                </MenuTrigger>
                <MenuPopover>
                  <MenuList>
                    {roles.map((role) => (
                      <MenuItem key={role} onClick={() => switchRole(role)}>
                        {roleLabel(role)}
                      </MenuItem>
                    ))}
                  </MenuList>
                </MenuPopover>
              </Menu>
            ) : (
              activeRole && <Caption1 block>{roleLabel(activeRole)}</Caption1>
            )}
          </div>
        </div>
      </div>
    </header>
  );
}
