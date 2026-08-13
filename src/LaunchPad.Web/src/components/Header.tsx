import { useEffect, useRef, useState } from 'react';
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
  Input,
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
import { AlertRegular, ChevronDownRegular, SearchRegular, WeatherMoonRegular, WeatherSunnyRegular } from '@fluentui/react-icons';
import { useActiveRole } from '../auth/ActiveRoleContext';
import { roleHomePath, roleLabel, type AppRole } from '../auth/roles';
import { useThemeMode } from '../theme/ThemeModeContext';
import { getMyNotifications, getUnreadNotificationCount, markAllNotificationsRead, markNotificationRead } from '../api/notifications';
import { search } from '../api/search';
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
  searchWrap: {
    position: 'relative',
    flexGrow: 1,
    maxWidth: '360px',
  },
  searchResults: {
    position: 'absolute',
    top: 'calc(100% + 4px)',
    left: 0,
    right: 0,
    zIndex: 10,
    backgroundColor: tokens.colorNeutralBackground1,
    border: `${tokens.strokeWidthThin} solid ${tokens.colorNeutralStroke2}`,
    borderRadius: tokens.borderRadiusMedium,
    boxShadow: tokens.shadow16,
    maxHeight: '360px',
    overflowY: 'auto',
    padding: tokens.spacingVerticalXS,
  },
  searchGroupLabel: {
    display: 'block',
    padding: `${tokens.spacingVerticalXXS} ${tokens.spacingHorizontalS}`,
  },
  resultButton: {
    display: 'flex',
    flexDirection: 'column',
    width: '100%',
    textAlign: 'left',
    padding: `${tokens.spacingVerticalXS} ${tokens.spacingHorizontalS}`,
    borderRadius: tokens.borderRadiusSmall,
    border: 'none',
    backgroundColor: 'transparent',
    cursor: 'pointer',
    ':hover': {
      backgroundColor: tokens.colorSubtleBackgroundHover,
    },
  },
  // Everything past the search box is "account controls" — a distinct group
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

  // --- Search: debounced, server-side, role-scoped (see SearchController) ---
  const searchBoxRef = useRef<HTMLDivElement>(null);
  const [searchInput, setSearchInput] = useState('');
  const [debouncedQuery, setDebouncedQuery] = useState('');
  const [searchOpen, setSearchOpen] = useState(false);

  useEffect(() => {
    const handle = setTimeout(() => setDebouncedQuery(searchInput.trim()), 300);
    return () => clearTimeout(handle);
  }, [searchInput]);

  useEffect(() => {
    function closeIfOutside(event: MouseEvent) {
      if (searchBoxRef.current && !searchBoxRef.current.contains(event.target as Node)) {
        setSearchOpen(false);
      }
    }
    document.addEventListener('mousedown', closeIfOutside);
    return () => document.removeEventListener('mousedown', closeIfOutside);
  }, []);

  const { data: searchResults, isFetching: searching } = useQuery({
    queryKey: ['search', debouncedQuery],
    queryFn: () => search(debouncedQuery),
    enabled: debouncedQuery.length >= 2,
  });

  const goToResult = (url: string) => {
    setSearchOpen(false);
    setSearchInput('');
    setDebouncedQuery('');
    navigate(url);
  };

  const projectResults = (searchResults ?? []).filter((r) => r.type === 'Project');
  const candidateResults = (searchResults ?? []).filter((r) => r.type === 'Candidate');
  const showDropdown = searchOpen && debouncedQuery.length >= 2;

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
      <div className={styles.searchWrap} ref={searchBoxRef}>
        <Input
          contentBefore={<SearchRegular />}
          placeholder="Search projects, candidates..."
          appearance="filled-lighter"
          value={searchInput}
          onChange={(_, data) => {
            setSearchInput(data.value);
            setSearchOpen(true);
          }}
          onFocus={() => setSearchOpen(true)}
          style={{ width: '100%' }}
        />
        {showDropdown && (
          <div className={styles.searchResults}>
            {searching && <Spinner size="tiny" label="Searching..." style={{ padding: tokens.spacingVerticalS }} />}
            {!searching && projectResults.length === 0 && candidateResults.length === 0 && (
              <Body1 style={{ display: 'block', padding: tokens.spacingVerticalS }}>No results.</Body1>
            )}
            {projectResults.length > 0 && (
              <div>
                <Caption1 className={styles.searchGroupLabel}>
                  <strong>Projects</strong>
                </Caption1>
                {projectResults.map((r) => (
                  <button key={`project-${r.id}`} type="button" className={styles.resultButton} onClick={() => goToResult(r.url)}>
                    <Body1 block>{r.title}</Body1>
                    <Caption1 block>{r.subtitle}</Caption1>
                  </button>
                ))}
              </div>
            )}
            {candidateResults.length > 0 && (
              <div>
                <Caption1 className={styles.searchGroupLabel}>
                  <strong>Candidates</strong>
                </Caption1>
                {candidateResults.map((r) => (
                  <button key={`candidate-${r.id}`} type="button" className={styles.resultButton} onClick={() => goToResult(r.url)}>
                    <Body1 block>{r.title}</Body1>
                    <Caption1 block>{r.subtitle}</Caption1>
                  </button>
                ))}
              </div>
            )}
          </div>
        )}
      </div>

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
