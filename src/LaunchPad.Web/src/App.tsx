import type { ReactNode } from 'react';
import { BrowserRouter, Routes, Route } from 'react-router-dom';
import { FluentProvider } from '@fluentui/react-components';
import { AuthenticatedTemplate, UnauthenticatedTemplate } from '@azure/msal-react';
import { AppShell } from './components/AppShell';
import { SignInPrompt } from './components/SignInPrompt';
import { isMockMode } from './dev/mockMode';
import { RequireRole } from './auth/RequireRole';
import { RequireCandidateProfile } from './auth/RequireCandidateProfile';
import { RequireSponsorProfile } from './auth/RequireSponsorProfile';
import { ActiveRoleProvider } from './auth/ActiveRoleContext';
import { AppRoles } from './auth/roles';
import { ThemeModeProvider, useThemeMode } from './theme/ThemeModeContext';
import { RoleAwareHome } from './features/shared/RoleAwareHome';
import { Unauthorized } from './features/shared/Unauthorized';
import { TalentPipeline } from './features/ops/TalentPipeline';
import { ApprovalQueue } from './features/ops/ApprovalQueue';
import { OpsDashboard } from './features/ops/OpsDashboard';
import { OpsProjects } from './features/ops/OpsProjects';
import { ProjectApprovals } from './features/ops/ProjectApprovals';
import { Cohorts } from './features/ops/Cohorts';
import { Risks } from './features/ops/Risks';
import { ExecutiveDashboard } from './features/exec/ExecutiveDashboard';
import { CandidateDashboard } from './features/candidate/CandidateDashboard';
import { MyProfile } from './features/candidate/MyProfile';
import { Assignments } from './features/candidate/Assignments';
import { Tasks } from './features/candidate/Tasks';
import { Deliverables } from './features/candidate/Deliverables';
import { Evaluations } from './features/candidate/Evaluations';
import { Community } from './features/candidate/Community';
import { MyProjects } from './features/sponsor/MyProjects';
import { ProjectMatches } from './features/sponsor/ProjectMatches';
import { MyCandidates } from './features/sponsor/MyCandidates';
import { SubmitReview } from './features/sponsor/SubmitReview';
import { ManageTodos } from './features/sponsor/ManageTodos';
import { ProjectMarketplace } from './features/candidate/ProjectMarketplace';
import { ProjectDetail } from './features/candidate/ProjectDetail';
import { Onboarding } from './features/candidate/Onboarding';
import { SponsorOnboarding } from './features/sponsor/SponsorOnboarding';
import { ProjectEditor } from './components/ProjectEditor';

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
                  path="/ops/project-approvals"
                  element={
                    <RequireRole allow={[AppRoles.ProgramOps]}>
                      <ProjectApprovals />
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
              </Routes>
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
  return (
    <>
      <AuthenticatedTemplate>{children}</AuthenticatedTemplate>
      <UnauthenticatedTemplate>
        <SignInPrompt />
      </UnauthenticatedTemplate>
    </>
  );
}
