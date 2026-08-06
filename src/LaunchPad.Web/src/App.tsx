import { BrowserRouter, Routes, Route } from 'react-router-dom';
import { FluentProvider } from '@fluentui/react-components';
import { AuthenticatedTemplate, UnauthenticatedTemplate } from '@azure/msal-react';
import { AppShell } from './components/AppShell';
import { SignInPrompt } from './components/SignInPrompt';
import { RequireRole } from './auth/RequireRole';
import { ActiveRoleProvider } from './auth/ActiveRoleContext';
import { AppRoles } from './auth/roles';
import { ThemeModeProvider, useThemeMode } from './theme/ThemeModeContext';
import { RoleAwareHome } from './features/shared/RoleAwareHome';
import { Unauthorized } from './features/shared/Unauthorized';
import { TalentPipeline } from './features/ops/TalentPipeline';
import { ApprovalQueue } from './features/ops/ApprovalQueue';
import { OpsDashboard } from './features/ops/OpsDashboard';
import { OpsProjects } from './features/ops/OpsProjects';
import { OpsProjectDetail } from './features/ops/OpsProjectDetail';
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
import { ProjectMarketplace } from './features/candidate/ProjectMarketplace';
import { ProjectDetail } from './features/candidate/ProjectDetail';

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
      <AuthenticatedTemplate>
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
                      <OpsProjectDetail />
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
                  path="/dashboard"
                  element={
                    <RequireRole allow={[AppRoles.Candidate]}>
                      <CandidateDashboard />
                    </RequireRole>
                  }
                />
                <Route
                  path="/profile"
                  element={
                    <RequireRole allow={[AppRoles.Candidate]}>
                      <MyProfile />
                    </RequireRole>
                  }
                />
                <Route
                  path="/assignments"
                  element={
                    <RequireRole allow={[AppRoles.Candidate]}>
                      <Assignments />
                    </RequireRole>
                  }
                />
                <Route
                  path="/tasks"
                  element={
                    <RequireRole allow={[AppRoles.Candidate]}>
                      <Tasks />
                    </RequireRole>
                  }
                />
                <Route
                  path="/deliverables"
                  element={
                    <RequireRole allow={[AppRoles.Candidate]}>
                      <Deliverables />
                    </RequireRole>
                  }
                />
                <Route
                  path="/evaluations"
                  element={
                    <RequireRole allow={[AppRoles.Candidate]}>
                      <Evaluations />
                    </RequireRole>
                  }
                />
                <Route
                  path="/community"
                  element={
                    <RequireRole allow={[AppRoles.Candidate, AppRoles.ProgramOps]}>
                      <Community />
                    </RequireRole>
                  }
                />
                <Route
                  path="/marketplace"
                  element={
                    <RequireRole allow={[AppRoles.Candidate]}>
                      <ProjectMarketplace />
                    </RequireRole>
                  }
                />
                <Route
                  path="/marketplace/:id"
                  element={
                    <RequireRole allow={[AppRoles.Candidate]}>
                      <ProjectDetail />
                    </RequireRole>
                  }
                />
                <Route
                  path="/projects"
                  element={
                    <RequireRole allow={[AppRoles.Sponsor]}>
                      <MyProjects />
                    </RequireRole>
                  }
                />
                <Route
                  path="/projects/:id/matches"
                  element={
                    <RequireRole allow={[AppRoles.Sponsor]}>
                      <ProjectMatches />
                    </RequireRole>
                  }
                />
                <Route
                  path="/candidates"
                  element={
                    <RequireRole allow={[AppRoles.Sponsor]}>
                      <MyCandidates />
                    </RequireRole>
                  }
                />
                <Route
                  path="/candidates/:assignmentId/review"
                  element={
                    <RequireRole allow={[AppRoles.Sponsor]}>
                      <SubmitReview />
                    </RequireRole>
                  }
                />
              </Routes>
            </AppShell>
          </BrowserRouter>
        </ActiveRoleProvider>
      </AuthenticatedTemplate>
      <UnauthenticatedTemplate>
        <SignInPrompt />
      </UnauthenticatedTemplate>
    </FluentProvider>
  );
}
