import { Suspense, lazy, type ReactNode } from 'react';
import { BrowserRouter, Routes, Route } from 'react-router-dom';
import { FluentProvider, Spinner } from '@fluentui/react-components';
import { AuthenticatedTemplate, UnauthenticatedTemplate } from '@azure/msal-react';
import { AppShell } from './components/AppShell';
import { ErrorBoundary } from './components/ErrorBoundary';
import { SignInPrompt } from './components/SignInPrompt';
import { isMockMode } from './dev/mockMode';
import { getActivePersona, isPersonaMode } from './dev/devPersonas';
import { PersonaPicker } from './dev/PersonaPicker';
import { RequireRole } from './auth/RequireRole';
import { RequireCandidateProfile } from './auth/RequireCandidateProfile';
import { RequireSponsorProfile } from './auth/RequireSponsorProfile';
import { ActiveRoleProvider } from './auth/ActiveRoleContext';
import { AppRoles } from './auth/roles';
import { ThemeModeProvider, useThemeMode } from './theme/ThemeModeContext';
import { RoleAwareHome } from './features/shared/RoleAwareHome';
import { Unauthorized } from './features/shared/Unauthorized';
import { NotFound } from './features/shared/NotFound';

// Route components load per role area rather than in the initial bundle: an Ops user
// should not download the candidate, sponsor, and exec screens to see a dashboard.
// The .then() mapping is because these are named exports, which lazy() cannot take directly.
const TalentPipeline = lazy(() => import('./features/ops/TalentPipeline').then(m => ({ default: m.TalentPipeline })));
const ApprovalQueue = lazy(() => import('./features/ops/ApprovalQueue').then(m => ({ default: m.ApprovalQueue })));
const OpsDashboard = lazy(() => import('./features/ops/OpsDashboard').then(m => ({ default: m.OpsDashboard })));
const OpsProjects = lazy(() => import('./features/ops/OpsProjects').then(m => ({ default: m.OpsProjects })));
const ProjectApprovals = lazy(() => import('./features/ops/ProjectApprovals').then(m => ({ default: m.ProjectApprovals })));
const Cohorts = lazy(() => import('./features/ops/Cohorts').then(m => ({ default: m.Cohorts })));
const Risks = lazy(() => import('./features/ops/Risks').then(m => ({ default: m.Risks })));
const SkillsManagement = lazy(() => import('./features/ops/SkillsManagement').then(m => ({ default: m.SkillsManagement })));
const HelpCenter = lazy(() => import('./features/ops/help/HelpCenter').then(m => ({ default: m.HelpCenter })));
const ExecutiveDashboard = lazy(() => import('./features/exec/ExecutiveDashboard').then(m => ({ default: m.ExecutiveDashboard })));
const CandidateDashboard = lazy(() => import('./features/candidate/CandidateDashboard').then(m => ({ default: m.CandidateDashboard })));
const MyProfile = lazy(() => import('./features/candidate/MyProfile').then(m => ({ default: m.MyProfile })));
const Assignments = lazy(() => import('./features/candidate/Assignments').then(m => ({ default: m.Assignments })));
const Tasks = lazy(() => import('./features/candidate/Tasks').then(m => ({ default: m.Tasks })));
const Deliverables = lazy(() => import('./features/candidate/Deliverables').then(m => ({ default: m.Deliverables })));
const Evaluations = lazy(() => import('./features/candidate/Evaluations').then(m => ({ default: m.Evaluations })));
const Community = lazy(() => import('./features/candidate/Community').then(m => ({ default: m.Community })));
const MyProjects = lazy(() => import('./features/sponsor/MyProjects').then(m => ({ default: m.MyProjects })));
const ProjectMatches = lazy(() => import('./features/sponsor/ProjectMatches').then(m => ({ default: m.ProjectMatches })));
const MyCandidates = lazy(() => import('./features/sponsor/MyCandidates').then(m => ({ default: m.MyCandidates })));
const SubmitReview = lazy(() => import('./features/sponsor/SubmitReview').then(m => ({ default: m.SubmitReview })));
const ManageTodos = lazy(() => import('./features/sponsor/ManageTodos').then(m => ({ default: m.ManageTodos })));
const ProjectMarketplace = lazy(() => import('./features/candidate/ProjectMarketplace').then(m => ({ default: m.ProjectMarketplace })));
const ProjectDetail = lazy(() => import('./features/candidate/ProjectDetail').then(m => ({ default: m.ProjectDetail })));
const Onboarding = lazy(() => import('./features/candidate/Onboarding').then(m => ({ default: m.Onboarding })));
const SponsorOnboarding = lazy(() => import('./features/sponsor/SponsorOnboarding').then(m => ({ default: m.SponsorOnboarding })));
const ProjectEditor = lazy(() => import('./components/ProjectEditor').then(m => ({ default: m.ProjectEditor })));

export default function App() {
  return (
    <ThemeModeProvider>
      <AppContent />
    </ThemeModeProvider>
  );
}

function AppContent() {
  const { theme } = useThemeMode();

  return (
    <FluentProvider theme={theme}>
      <AuthGate>
        <ActiveRoleProvider>
          <BrowserRouter>
            <AppShell>
              <ErrorBoundary>
                <Suspense fallback={<Spinner size="large" label="Loading..." />}>
                <Routes>
                <Route path="/" element={<RoleAwareHome />} />
                <Route path="/unauthorized" element={<Unauthorized />} />
                <Route
                  path="/pipeline"
                  element={
                    <RequireRole
                      allow={[AppRoles.Executive, AppRoles.ProgramOps, AppRoles.Sponsor, AppRoles.HiringManager]}
                    >
                      <TalentPipeline />
                    </RequireRole>
                  }
                />
                <Route
                  path="/pipeline/:cohortId"
                  element={
                    <RequireRole
                      allow={[AppRoles.Executive, AppRoles.ProgramOps, AppRoles.Sponsor, AppRoles.HiringManager]}
                    >
                      <TalentPipeline />
                    </RequireRole>
                  }
                />
                <Route
                  path="/ops/dashboard"
                  element={
                    <RequireRole allow={[AppRoles.ProgramOps]}>
                      <OpsDashboard />
                    </RequireRole>
                  }
                />
                <Route
                  path="/ops/projects"
                  element={
                    <RequireRole allow={[AppRoles.ProgramOps]}>
                      <OpsProjects />
                    </RequireRole>
                  }
                />
                <Route
                  path="/ops/projects/:id"
                  element={
                    <RequireRole allow={[AppRoles.ProgramOps]}>
                      <ProjectEditor />
                    </RequireRole>
                  }
                />
                <Route
                  path="/ops/approvals"
                  element={
                    <RequireRole allow={[AppRoles.ProgramOps]}>
                      <ApprovalQueue />
                    </RequireRole>
                  }
                />
                <Route
                  path="/ops/cohorts"
                  element={
                    <RequireRole allow={[AppRoles.ProgramOps]}>
                      <Cohorts />
                    </RequireRole>
                  }
                />
                <Route
                  path="/ops/risks"
                  element={
                    <RequireRole allow={[AppRoles.ProgramOps]}>
                      <Risks />
                    </RequireRole>
                  }
                />
                <Route
                  path="/ops/skills"
                  element={
                    <RequireRole allow={[AppRoles.ProgramOps]}>
                      <SkillsManagement />
                    </RequireRole>
                  }
                />
                <Route
                  path="/ops/project-approvals"
                  element={
                    <RequireRole allow={[AppRoles.ProgramOps]}>
                      <ProjectApprovals />
                    </RequireRole>
                  }
                />
                {/* Static content — no API calls, so no profile gate. Both the index
                    and a single guide render from the same component. */}
                <Route
                  path="/ops/help"
                  element={
                    <RequireRole allow={[AppRoles.ProgramOps]}>
                      <HelpCenter />
                    </RequireRole>
                  }
                />
                <Route
                  path="/ops/help/:topicId"
                  element={
                    <RequireRole allow={[AppRoles.ProgramOps]}>
                      <HelpCenter />
                    </RequireRole>
                  }
                />
                <Route
                  path="/exec"
                  element={
                    <RequireRole allow={[AppRoles.Executive, AppRoles.ProgramOps]}>
                      <ExecutiveDashboard />
                    </RequireRole>
                  }
                />
                <Route
                  path="/onboarding"
                  element={
                    <RequireRole allow={[AppRoles.Candidate]}>
                      <Onboarding />
                    </RequireRole>
                  }
                />
                <Route
                  path="/dashboard"
                  element={
                    <RequireRole allow={[AppRoles.Candidate]}>
                      <RequireCandidateProfile>
                        <CandidateDashboard />
                      </RequireCandidateProfile>
                    </RequireRole>
                  }
                />
                <Route
                  path="/profile"
                  element={
                    <RequireRole allow={[AppRoles.Candidate]}>
                      <RequireCandidateProfile>
                        <MyProfile />
                      </RequireCandidateProfile>
                    </RequireRole>
                  }
                />
                <Route
                  path="/assignments"
                  element={
                    <RequireRole allow={[AppRoles.Candidate]}>
                      <RequireCandidateProfile>
                        <Assignments />
                      </RequireCandidateProfile>
                    </RequireRole>
                  }
                />
                <Route
                  path="/tasks"
                  element={
                    <RequireRole allow={[AppRoles.Candidate]}>
                      <RequireCandidateProfile>
                        <Tasks />
                      </RequireCandidateProfile>
                    </RequireRole>
                  }
                />
                <Route
                  path="/deliverables"
                  element={
                    <RequireRole allow={[AppRoles.Candidate]}>
                      <RequireCandidateProfile>
                        <Deliverables />
                      </RequireCandidateProfile>
                    </RequireRole>
                  }
                />
                <Route
                  path="/evaluations"
                  element={
                    <RequireRole allow={[AppRoles.Candidate]}>
                      <RequireCandidateProfile>
                        <Evaluations />
                      </RequireCandidateProfile>
                    </RequireRole>
                  }
                />
                <Route
                  path="/community"
                  element={
                    <RequireRole allow={[AppRoles.Candidate, AppRoles.ProgramOps, AppRoles.Sponsor]}>
                      <Community />
                    </RequireRole>
                  }
                />
                <Route
                  path="/marketplace"
                  element={
                    <RequireRole allow={[AppRoles.Candidate]}>
                      <RequireCandidateProfile>
                        <ProjectMarketplace />
                      </RequireCandidateProfile>
                    </RequireRole>
                  }
                />
                <Route
                  path="/marketplace/:id"
                  element={
                    <RequireRole allow={[AppRoles.Candidate]}>
                      <RequireCandidateProfile>
                        <ProjectDetail />
                      </RequireCandidateProfile>
                    </RequireRole>
                  }
                />
                <Route
                  path="/sponsor-onboarding"
                  element={
                    <RequireRole allow={[AppRoles.Sponsor]}>
                      <SponsorOnboarding />
                    </RequireRole>
                  }
                />
                <Route
                  path="/projects"
                  element={
                    <RequireRole allow={[AppRoles.Sponsor]}>
                      <RequireSponsorProfile>
                        <MyProjects />
                      </RequireSponsorProfile>
                    </RequireRole>
                  }
                />
                <Route
                  path="/projects/:id/edit"
                  element={
                    <RequireRole allow={[AppRoles.Sponsor]}>
                      <RequireSponsorProfile>
                        <ProjectEditor />
                      </RequireSponsorProfile>
                    </RequireRole>
                  }
                />
                <Route
                  path="/projects/:id/matches"
                  element={
                    <RequireRole allow={[AppRoles.Sponsor]}>
                      <RequireSponsorProfile>
                        <ProjectMatches />
                      </RequireSponsorProfile>
                    </RequireRole>
                  }
                />
                <Route
                  path="/candidates"
                  element={
                    <RequireRole allow={[AppRoles.Sponsor]}>
                      <RequireSponsorProfile>
                        <MyCandidates />
                      </RequireSponsorProfile>
                    </RequireRole>
                  }
                />
                <Route
                  path="/candidates/:assignmentId/review"
                  element={
                    <RequireRole allow={[AppRoles.Sponsor]}>
                      <RequireSponsorProfile>
                        <SubmitReview />
                      </RequireSponsorProfile>
                    </RequireRole>
                  }
                />
                <Route
                  path="/candidates/:assignmentId/todos"
                  element={
                    <RequireRole allow={[AppRoles.Sponsor]}>
                      <RequireSponsorProfile>
                        <ManageTodos />
                      </RequireSponsorProfile>
                    </RequireRole>
                  }
                />
                {/* Generalized review submission — reached from an Ops-scheduled review
                    to-do's "Submit review" link on either Tasks.tsx (Candidate) or
                    ManageTodos.tsx (Sponsor). No profile-gate wrapper here, unlike the
                    role-specific routes above — RequireSponsorProfile/RequireCandidateProfile
                    would incorrectly block whichever role they don't target. */}
                <Route
                  path="/reviews/submit/:assignmentId/:reviewType/:checkpoint"
                  element={
                    <RequireRole allow={[AppRoles.Sponsor, AppRoles.Candidate]}>
                      <SubmitReview />
                    </RequireRole>
                  }
                />
                {/* Without a catch-all an unknown URL rendered an empty page with no way back. */}
                <Route path="*" element={<NotFound />} />
                </Routes>
                </Suspense>
              </ErrorBoundary>
            </AppShell>
          </BrowserRouter>
        </ActiveRoleProvider>
      </AuthGate>
    </FluentProvider>
  );
}

/**
 * Skips MSAL's real sign-in check in mock mode (see dev/mockMode.ts) — nothing
 * downstream needs a real account: useApiAccessTokenClaims already returns every
 * role synthetically, and authedFetch never leaves the browser. This branch is
 * stripped from production builds. The real (non-mock) path is unchanged.
 */
function AuthGate({ children }: { children: ReactNode }) {
  if (isMockMode) return <>{children}</>;
  if (isPersonaMode) return getActivePersona() ? <>{children}</> : <PersonaPicker />;
  return (
    <>
      <AuthenticatedTemplate>{children}</AuthenticatedTemplate>
      <UnauthenticatedTemplate>
        <SignInPrompt />
      </UnauthenticatedTemplate>
    </>
  );
}
