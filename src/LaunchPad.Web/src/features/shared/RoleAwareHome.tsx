import type { ReactElement, ReactNode } from 'react';
import { useNavigate } from 'react-router-dom';
import { useMsal } from '@azure/msal-react';
import { useQuery } from '@tanstack/react-query';
import {
  Accordion,
  AccordionHeader,
  AccordionItem,
  AccordionPanel,
  Badge,
  Body1,
  Button,
  Card,
  Spinner,
  Title1,
  Title3,
  makeStyles,
  mergeClasses,
  tokens,
} from '@fluentui/react-components';
import {
  BriefcaseRegular,
  CheckmarkCircleRegular,
  ChartMultipleRegular,
  ClipboardTaskRegular,
  DocumentArrowUpRegular,
  DocumentCheckmarkRegular,
  FolderRegular,
  GridRegular,
  PeopleCommunityRegular,
  PeopleRegular,
  PeopleTeamRegular,
  PersonRegular,
  RocketRegular,
  ShoppingBagRegular,
  TaskListSquareLtrRegular,
  WarningRegular,
} from '@fluentui/react-icons';
import { useSurfaceStyles } from '../../theme/surfaces';
import { useApiTokenDiagnostics } from '../../auth/useApiTokenDiagnostics';
import { useActiveRole } from '../../auth/ActiveRoleContext';
import { AppRoles, roleLabel, type AppRole } from '../../auth/roles';
import { getMyCandidateProfile } from '../../api/candidates';
import { getMySponsorProfile } from '../../api/sponsors';
import { getPendingApprovalProjects } from '../../api/projects';
import { getMatchingQueue } from '../../api/matching';

// Demo-only: same single-cohort simplification used elsewhere until real cohort
// selection exists.
const DEMO_COHORT_ID = 1;

const useStyles = makeStyles({
  banner: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalL,
    borderRadius: tokens.borderRadiusLarge,
    padding: tokens.spacingVerticalXL,
    backgroundColor: tokens.colorBrandBackground2,
    border: `${tokens.strokeWidthThin} solid ${tokens.colorBrandStroke2}`,
    marginBottom: tokens.spacingVerticalXL,
  },
  bannerIcon: {
    display: 'flex',
    flexShrink: 0,
    alignItems: 'center',
    justifyContent: 'center',
    width: '56px',
    height: '56px',
    borderRadius: tokens.borderRadiusCircular,
    backgroundColor: tokens.colorBrandBackground,
    color: tokens.colorNeutralForegroundOnBrand,
    fontSize: '28px',
  },
  bannerTitle: {
    color: tokens.colorBrandForeground2,
  },
  roleRow: {
    display: 'flex',
    gap: tokens.spacingHorizontalXS,
    marginTop: tokens.spacingVerticalS,
  },
  ctaCard: {
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'space-between',
    gap: tokens.spacingHorizontalL,
    padding: tokens.spacingVerticalXL,
    marginBottom: tokens.spacingVerticalXL,
  },
  grid: {
    display: 'grid',
    gridTemplateColumns: 'repeat(auto-fill, minmax(240px, 1fr))',
    gap: tokens.spacingHorizontalM,
    marginBottom: tokens.spacingVerticalXL,
  },
  linkCard: {
    padding: tokens.spacingVerticalL,
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalXS,
    textAlign: 'left',
    border: 'none',
    ':hover': {
      backgroundColor: tokens.colorSubtleBackgroundHover,
    },
  },
  linkCardHeader: {
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'space-between',
  },
  linkIcon: {
    fontSize: '24px',
    color: tokens.colorBrandForeground1,
  },
  diagnosticsCard: {
    padding: tokens.spacingVerticalM,
  },
  pre: {
    fontSize: tokens.fontSizeBase200,
    fontFamily: tokens.fontFamilyMonospace,
    backgroundColor: tokens.colorNeutralBackground3,
    borderRadius: tokens.borderRadiusMedium,
    padding: tokens.spacingVerticalM,
    overflow: 'auto',
    color: tokens.colorNeutralForeground1,
  },
});

interface QuickLink {
  to: string;
  icon: ReactElement;
  title: string;
  description: string;
  badge?: number;
}

function QuickLinkGrid({ links }: { links: QuickLink[] }) {
  const styles = useStyles();
  const surfaces = useSurfaceStyles();
  const navigate = useNavigate();

  return (
    <div className={styles.grid}>
      {links.map((link) => (
        <Card
          key={link.to}
          className={mergeClasses(styles.linkCard, surfaces.interactive, surfaces.fadeInUp)}
          onClick={() => navigate(link.to)}
        >
          <div className={styles.linkCardHeader}>
            <span className={styles.linkIcon} aria-hidden="true">{link.icon}</span>
            {!!link.badge && link.badge > 0 && (
              <Badge appearance="filled" color="danger" shape="circular">{link.badge}</Badge>
            )}
          </div>
          <Title3>{link.title}</Title3>
          <Body1>{link.description}</Body1>
        </Card>
      ))}
    </div>
  );
}

function CandidateHome() {
  const styles = useStyles();
  const navigate = useNavigate();
  const { data: profile, isLoading } = useQuery({ queryKey: ['candidates', 'me'], queryFn: getMyCandidateProfile });

  if (isLoading) return <Spinner label="Checking your profile..." />;

  if (!profile) {
    return (
      <Card className={styles.ctaCard}>
        <div>
          <Title3>Complete your candidate profile</Title3>
          <Body1>Add your skills and background so sponsors and Program Ops can start matching you to projects.</Body1>
        </div>
        <Button appearance="primary" onClick={() => navigate('/onboarding')}>Get started</Button>
      </Card>
    );
  }

  return (
    <QuickLinkGrid
      links={[
        { to: '/dashboard', icon: <GridRegular />, title: 'Dashboard', description: 'Your active project and progress at a glance.' },
        { to: '/profile', icon: <PersonRegular />, title: 'My Profile', description: 'Update your skills, bio, and links.' },
        { to: '/marketplace', icon: <ShoppingBagRegular />, title: 'Project Marketplace', description: 'Browse open projects in your cohort.' },
        { to: '/assignments', icon: <BriefcaseRegular />, title: 'Assignments', description: 'Your current and past project assignments.' },
        { to: '/tasks', icon: <TaskListSquareLtrRegular />, title: 'Tasks', description: 'To-dos for your active project.' },
        { to: '/deliverables', icon: <DocumentArrowUpRegular />, title: 'Deliverables', description: 'Files you have submitted.' },
        { to: '/evaluations', icon: <DocumentCheckmarkRegular />, title: 'Evaluations', description: 'Feedback from your sponsor.' },
        { to: '/community', icon: <PeopleCommunityRegular />, title: 'Community', description: 'See what other candidates are sharing.' },
      ]}
    />
  );
}

function SponsorHome() {
  const styles = useStyles();
  const navigate = useNavigate();
  const { data: profile, isLoading } = useQuery({ queryKey: ['sponsors', 'me'], queryFn: getMySponsorProfile });

  if (isLoading) return <Spinner label="Checking your profile..." />;

  if (!profile) {
    return (
      <Card className={styles.ctaCard}>
        <div>
          <Title3>Complete your sponsor profile</Title3>
          <Body1>Add your organization so you can post projects and review candidates.</Body1>
        </div>
        <Button appearance="primary" onClick={() => navigate('/sponsor-onboarding')}>Get started</Button>
      </Card>
    );
  }

  return (
    <QuickLinkGrid
      links={[
        { to: '/projects', icon: <FolderRegular />, title: 'My Projects', description: 'Manage the projects you sponsor.' },
        { to: '/candidates', icon: <PeopleRegular />, title: 'My Candidates', description: 'Candidates working on your projects.' },
        { to: '/pipeline', icon: <PeopleTeamRegular />, title: 'Talent Pipeline', description: 'Browse the current candidate cohort.' },
      ]}
    />
  );
}

function ProgramOpsHome() {
  const { data: pendingProjects } = useQuery({
    queryKey: ['projects', 'pending-approval', DEMO_COHORT_ID],
    queryFn: () => getPendingApprovalProjects(DEMO_COHORT_ID),
  });
  const { data: matchQueue } = useQuery({
    queryKey: ['matching', 'queue', DEMO_COHORT_ID],
    queryFn: () => getMatchingQueue(DEMO_COHORT_ID),
  });

  return (
    <QuickLinkGrid
      links={[
        { to: '/ops/dashboard', icon: <GridRegular />, title: 'Dashboard', description: 'Program-wide stats and risk signals.' },
        { to: '/ops/projects', icon: <FolderRegular />, title: 'Projects', description: 'Every project in the current cohort.' },
        {
          to: '/ops/project-approvals',
          icon: <ClipboardTaskRegular />,
          title: 'Project Approvals',
          description: 'Sponsor-submitted projects awaiting review.',
          badge: pendingProjects?.length,
        },
        {
          to: '/ops/approvals',
          icon: <CheckmarkCircleRegular />,
          title: 'Approvals',
          description: 'Proposed matches awaiting your approval.',
          badge: matchQueue?.length,
        },
        { to: '/ops/cohorts', icon: <PeopleTeamRegular />, title: 'Cohorts', description: 'Manage running seasonal cohorts.' },
        { to: '/ops/risks', icon: <WarningRegular />, title: 'Risks', description: 'Candidates flagged for performance or engagement risk.' },
        { to: '/pipeline', icon: <PeopleRegular />, title: 'Candidates', description: 'Browse the current candidate cohort.' },
        { to: '/community', icon: <PeopleCommunityRegular />, title: 'Community', description: 'See what candidates are sharing.' },
      ]}
    />
  );
}

function ExecutiveHome() {
  return (
    <QuickLinkGrid
      links={[
        { to: '/exec', icon: <ChartMultipleRegular />, title: 'Executive Dashboard', description: 'Program funnel and outcomes.' },
        { to: '/pipeline', icon: <PeopleTeamRegular />, title: 'Talent Pipeline', description: 'Browse the current candidate cohort.' },
      ]}
    />
  );
}

function HiringManagerHome() {
  return (
    <QuickLinkGrid
      links={[
        { to: '/pipeline', icon: <PeopleTeamRegular />, title: 'Talent Pipeline', description: 'Browse the current candidate cohort.' },
      ]}
    />
  );
}

const roleHomes: Record<AppRole, () => ReactNode> = {
  [AppRoles.Candidate]: CandidateHome,
  [AppRoles.Sponsor]: SponsorHome,
  [AppRoles.ProgramOps]: ProgramOpsHome,
  [AppRoles.Executive]: ExecutiveHome,
  [AppRoles.HiringManager]: HiringManagerHome,
};

// Each role sees a distinct home: a Candidate with no profile yet gets a direct path
// to onboarding (previously only reachable by typing the URL — the nav menu hides
// every Candidate link, including any path to /onboarding, until a profile exists);
// everyone else gets quick links to the handful of screens their role actually uses,
// rather than one generic banner identical for every role.
export function RoleAwareHome() {
  const styles = useStyles();
  const surfaces = useSurfaceStyles();
  const { accounts } = useMsal();
  const { activeRole } = useActiveRole();
  const { roles, spaIdTokenClaims, apiAccessTokenClaims } = useApiTokenDiagnostics();
  const firstName = (accounts[0]?.name ?? 'there').split(' ')[0];

  const RoleHome = activeRole ? roleHomes[activeRole] : null;

  return (
    <>
      <div className={mergeClasses(styles.banner, surfaces.fadeInUp)}>
        <span className={styles.bannerIcon} aria-hidden="true">
          <RocketRegular />
        </span>
        <div>
          <Title1 block className={styles.bannerTitle}>Welcome to LaunchPad, {firstName}</Title1>
          <Body1 block style={{ marginTop: tokens.spacingVerticalXS }}>
            Use the role switcher in the header to change perspective if you hold more than one role.
          </Body1>
          <div className={styles.roleRow}>
            {roles.length > 0 ? (
              roles.map((role) => (
                <Badge key={role} appearance="tint" color="brand">
                  {roleLabel(role as AppRole)}
                </Badge>
              ))
            ) : (
              <Badge appearance="tint" color="warning">
                No roles assigned yet
              </Badge>
            )}
          </div>
        </div>
      </div>

      {RoleHome && <RoleHome />}

      <Accordion collapsible>
        <AccordionItem value="diagnostics">
          <AccordionHeader>Token diagnostics</AccordionHeader>
          <AccordionPanel>
            <Card className={styles.diagnosticsCard}>
              <Body1>
                <strong>SPA ID token claims</strong>
              </Body1>
              <pre className={styles.pre}>{JSON.stringify(spaIdTokenClaims, null, 2)}</pre>
              <Body1>
                <strong>API access token claims</strong>
              </Body1>
              <pre className={styles.pre}>{JSON.stringify(apiAccessTokenClaims, null, 2)}</pre>
            </Card>
          </AccordionPanel>
        </AccordionItem>
      </Accordion>
    </>
  );
}
