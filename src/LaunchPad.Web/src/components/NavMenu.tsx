import type { ReactElement } from 'react';
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
  ShoppingBagRegular,
  TaskListSquareLtrRegular,
  WarningRegular,
} from '@fluentui/react-icons';
import { AppRoles } from '../auth/roles';
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
  },
  item: {
    display: 'flex',
    alignItems: 'center',
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
  icon: {
    display: 'flex',
    fontSize: '20px',
    flexShrink: 0,
  },
});

function NavLink({ to, icon, children }: { to: string; icon: ReactElement; children: string }) {
  const styles = useStyles();
  const { pathname } = useLocation();
  const isSelected = to === '/' ? pathname === '/' : pathname === to || pathname.startsWith(`${to}/`);

  return (
    <li>
      <Link to={to} className={mergeClasses(styles.item, isSelected && styles.itemSelected)}>
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
        </>
      )}

      {activeRole === AppRoles.ProgramOps && (
        <>
          <NavLink to="/ops/dashboard" icon={<GridRegular />}>Dashboard</NavLink>
          <NavLink to="/ops/projects" icon={<FolderRegular />}>Projects</NavLink>
          <NavLink to="/ops/project-approvals" icon={<ClipboardTaskRegular />}>Project Approvals</NavLink>
          <NavLink to="/ops/approvals" icon={<CheckmarkCircleRegular />}>Approvals</NavLink>
          <NavLink to="/ops/cohorts" icon={<PeopleTeamRegular />}>Cohorts</NavLink>
          <NavLink to="/ops/risks" icon={<WarningRegular />}>Risks</NavLink>
          <NavLink to="/pipeline" icon={<PeopleRegular />}>Candidates</NavLink>
          <NavLink to="/community" icon={<PeopleCommunityRegular />}>Community</NavLink>
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
