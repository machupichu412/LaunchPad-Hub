import type { CSSProperties, ReactElement } from 'react';
import { Link, useLocation } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { Body1, makeStyles, mergeClasses, tokens } from '@fluentui/react-components';
import {
  BriefcaseRegular,
  CheckmarkCircleRegular,
  ChartMultipleRegular,
  ClipboardTaskRegular,
  DocumentArrowUpRegular,
  DocumentCheckmarkRegular,
  FolderRegular,
  GridRegular,
  HomeRegular,
  PeopleCommunityRegular,
  PeopleRegular,
  PeopleTeamRegular,
  PersonRegular,
  QuestionCircleRegular,
  ShoppingBagRegular,
  TagRegular,
  TaskListSquareLtrRegular,
  WarningRegular,
} from '@fluentui/react-icons';
import { AppRoles } from '../auth/roles';
import { hueForPath, sectionHues } from '../theme/brand';
import { useThemeMode } from '../theme/ThemeModeContext';
import { useActiveRole } from '../auth/ActiveRoleContext';
import { getMyCandidateProfile } from '../api/candidates';
import { getMySponsorProfile } from '../api/sponsors';

const useStyles = makeStyles({
  list: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalXXS,
    marginTop: tokens.spacingVerticalM,
    listStyle: 'none',
    padding: 0,
    // On a phone the rail lies down and scrolls sideways on its own, so the page never
    // has to. The hue rule moves to the bottom edge to stay readable in that direction.
    '@media (max-width: 767px)': {
      flexDirection: 'row',
      marginTop: tokens.spacingVerticalS,
      overflowX: 'auto',
      gap: tokens.spacingHorizontalXXS,
      scrollbarWidth: 'thin',
    },
  },
  item: {
    display: 'flex',
    alignItems: 'center',
    '@media (max-width: 767px)': {
      whiteSpace: 'nowrap',
      // Fluent's Body1 renders its own span, which wraps independently of the anchor.
      '& span': {
        whiteSpace: 'nowrap',
      },
    },
    gap: tokens.spacingHorizontalS,
    padding: `${tokens.spacingVerticalSNudge} ${tokens.spacingHorizontalM}`,
    borderRadius: tokens.borderRadiusMedium,
    color: tokens.colorNeutralForeground2,
    textDecorationLine: 'none',
    fontSize: tokens.fontSizeBase300,
    transitionProperty: 'background-color, color',
    transitionDuration: tokens.durationFaster,
    transitionTimingFunction: tokens.curveEasyEase,
    ':hover': {
      backgroundColor: tokens.colorSubtleBackgroundHover,
      color: tokens.colorNeutralForeground2Hover,
    },
    ':focus-visible': {
      outlineStyle: 'solid',
      outlineWidth: '2px',
      outlineColor: tokens.colorBrandStroke1,
      outlineOffset: '2px',
    },
  },
  itemSelected: {
    backgroundColor: tokens.colorBrandBackground2,
    color: tokens.colorBrandForeground2,
    fontWeight: tokens.fontWeightSemibold,
    ':hover': {
      backgroundColor: tokens.colorBrandBackground2Hover,
      color: tokens.colorBrandForeground2,
    },
  },
  // Candidate destinations each carry one hue from the logo's trail, so eight screens
  // built from identical grey parts stay tellable apart at a glance. The 3px rule does
  // the identifying and uses the raw logo value; the label and icon use the solved-for
  // `ink` variant, since most of the trail is too light to read as text. Ops and
  // Executive keep stock Fluent blue — the color story belongs to the candidate.
  // The hue arrives as a custom property rather than an inline border color, because the
  // rule switches edges at the breakpoint — left on the rail, bottom on the phone strip —
  // and an inline style can only ever set one of them.
  itemHued: {
    borderLeftWidth: '3px',
    borderLeftStyle: 'solid',
    borderLeftColor: 'var(--lp-hue, transparent)',
    paddingLeft: `calc(${tokens.spacingHorizontalM} - 3px)`,
    '@media (max-width: 767px)': {
      borderLeftStyle: 'none',
      paddingLeft: tokens.spacingHorizontalM,
      borderBottomWidth: '3px',
      borderBottomStyle: 'solid',
      borderBottomColor: 'var(--lp-hue, transparent)',
      paddingBottom: `calc(${tokens.spacingVerticalSNudge} - 3px)`,
    },
  },
  icon: {
    display: 'flex',
    fontSize: '20px',
    flexShrink: 0,
  },
});

function NavLink({ to, icon, children }: { to: string; icon: ReactElement; children: string }) {
  const styles = useStyles();
  const { pathname } = useLocation();
  const { mode } = useThemeMode();
  const isSelected = to === '/' ? pathname === '/' : pathname === to || pathname.startsWith(`${to}/`);

  const hueName = hueForPath(to);
  const hue = hueName ? sectionHues[hueName] : null;
  const hueStyle = hue
    ? ({
        '--lp-hue': isSelected ? hue.fill : 'transparent',
        color: isSelected ? (mode === 'dark' ? hue.inkDark : hue.ink) : undefined,
      } as CSSProperties)
    : undefined;

  return (
    <li>
      <Link
        to={to}
        className={mergeClasses(styles.item, isSelected && styles.itemSelected, hue !== null && styles.itemHued)}
        style={hueStyle}
      >
        <span className={styles.icon} aria-hidden="true">
          {icon}
        </span>
        <Body1 as="span" style={{ color: 'inherit', fontWeight: 'inherit' }}>
          {children}
        </Body1>
      </Link>
    </li>
  );
}

/**
 * Shapes the menu only — every link it exposes still hits an API endpoint that
 * independently re-checks authorization. See RequireRole / CLAUDE.md.
 *
 * Filters by the header's active role (single "viewing as" perspective), not
 * every role the user holds — a Sponsor+Candidate seeing both role's nav items
 * mixed together at once wouldn't match the mockup's per-role experience.
 */
export function NavMenu() {
  const styles = useStyles();
  const { activeRole } = useActiveRole();

  // Same ['candidates','me'] query RequireCandidateProfile/MyProfile/Onboarding
  // already use — cache-shared, so this adds no extra request. Only fetched while
  // viewing as Candidate; other roles never hit this endpoint from here.
  const { data: candidateProfile } = useQuery({
    queryKey: ['candidates', 'me'],
    queryFn: getMyCandidateProfile,
    enabled: activeRole === AppRoles.Candidate,
  });
  const isOnboardingCandidate = activeRole === AppRoles.Candidate && !candidateProfile;

  // Same ['sponsors','me'] query RequireSponsorProfile/SponsorOnboarding already
  // use — cache-shared, so this adds no extra request. Only fetched while viewing
  // as Sponsor; other roles never hit this endpoint from here.
  const { data: sponsorProfile } = useQuery({
    queryKey: ['sponsors', 'me'],
    queryFn: getMySponsorProfile,
    enabled: activeRole === AppRoles.Sponsor,
  });
  const isOnboardingSponsor = activeRole === AppRoles.Sponsor && !sponsorProfile;

  return (
    <ul className={styles.list}>
      <NavLink to="/" icon={<HomeRegular />}>Home</NavLink>

      {/* Every link below redirects straight back to /onboarding via
          RequireCandidateProfile until a profile exists — showing them before then
          is just a dead-end loop, so the Candidate nav stays down to Home alone. */}
      {activeRole === AppRoles.Candidate && !isOnboardingCandidate && (
        <>
          <NavLink to="/dashboard" icon={<GridRegular />}>Dashboard</NavLink>
          <NavLink to="/profile" icon={<PersonRegular />}>My Profile</NavLink>
          <NavLink to="/marketplace" icon={<ShoppingBagRegular />}>Project Marketplace</NavLink>
          <NavLink to="/assignments" icon={<BriefcaseRegular />}>Assignments</NavLink>
          <NavLink to="/tasks" icon={<TaskListSquareLtrRegular />}>Tasks</NavLink>
          <NavLink to="/deliverables" icon={<DocumentArrowUpRegular />}>Deliverables</NavLink>
          <NavLink to="/evaluations" icon={<DocumentCheckmarkRegular />}>Evaluations</NavLink>
          <NavLink to="/community" icon={<PeopleCommunityRegular />}>Community</NavLink>
        </>
      )}

      {/* Every link below redirects straight back to /sponsor-onboarding via
          RequireSponsorProfile until a profile exists — showing them before then
          is just a dead-end loop, so the Sponsor nav stays down to Home alone. */}
      {activeRole === AppRoles.Sponsor && !isOnboardingSponsor && (
        <>
          <NavLink to="/projects" icon={<FolderRegular />}>My Projects</NavLink>
          <NavLink to="/candidates" icon={<PeopleRegular />}>My Candidates</NavLink>
          <NavLink to="/pipeline" icon={<PeopleTeamRegular />}>Talent Pipeline</NavLink>
          <NavLink to="/community" icon={<PeopleCommunityRegular />}>Community</NavLink>
        </>
      )}

      {activeRole === AppRoles.ProgramOps && (
        <>
          <NavLink to="/ops/dashboard" icon={<GridRegular />}>Dashboard</NavLink>
          <NavLink to="/ops/projects" icon={<FolderRegular />}>Projects</NavLink>
          <NavLink to="/ops/project-approvals" icon={<ClipboardTaskRegular />}>Project Approvals</NavLink>
          <NavLink to="/ops/approvals" icon={<CheckmarkCircleRegular />}>Assignment Approvals</NavLink>
          <NavLink to="/ops/cohorts" icon={<PeopleTeamRegular />}>Cohorts</NavLink>
          <NavLink to="/ops/risks" icon={<WarningRegular />}>Risks</NavLink>
          <NavLink to="/ops/skills" icon={<TagRegular />}>Skills</NavLink>
          <NavLink to="/pipeline" icon={<PeopleRegular />}>Candidates</NavLink>
          <NavLink to="/community" icon={<PeopleCommunityRegular />}>Community</NavLink>
          <NavLink to="/ops/help" icon={<QuestionCircleRegular />}>Guides</NavLink>
        </>
      )}

      {activeRole === AppRoles.Executive && (
        <>
          <NavLink to="/pipeline" icon={<PeopleTeamRegular />}>Talent Pipeline</NavLink>
          <NavLink to="/exec" icon={<ChartMultipleRegular />}>Executive Dashboard</NavLink>
        </>
      )}

      {activeRole === AppRoles.HiringManager && (
        <NavLink to="/pipeline" icon={<PeopleTeamRegular />}>Talent Pipeline</NavLink>
      )}
    </ul>
  );
}
