import { BrowserRouter, Routes, Route } from 'react-router-dom';
import { FluentProvider, webLightTheme } from '@fluentui/react-components';
import { AuthenticatedTemplate, UnauthenticatedTemplate } from '@azure/msal-react';
import { AppShell } from './components/AppShell';
import { SignInPrompt } from './components/SignInPrompt';
import { RequireRole } from './auth/RequireRole';
import { AppRoles } from './auth/roles';
import { RoleAwareHome } from './features/shared/RoleAwareHome';
import { Unauthorized } from './features/shared/Unauthorized';
import { TalentPipeline } from './features/ops/TalentPipeline';
import { ApprovalQueue } from './features/ops/ApprovalQueue';
import { ExecutiveDashboard } from './features/exec/ExecutiveDashboard';
import { MyProfile } from './features/candidate/MyProfile';
import { MyProjects } from './features/sponsor/MyProjects';

export default function App() {
  return (
    <FluentProvider theme={webLightTheme}>
      <AuthenticatedTemplate>
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
                path="/ops/approvals"
                element={
                  <RequireRole allow={[AppRoles.ProgramOps]}>
                    <ApprovalQueue />
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
                path="/profile"
                element={
                  <RequireRole allow={[AppRoles.Candidate]}>
                    <MyProfile />
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
            </Routes>
          </AppShell>
        </BrowserRouter>
      </AuthenticatedTemplate>
      <UnauthenticatedTemplate>
        <SignInPrompt />
      </UnauthenticatedTemplate>
    </FluentProvider>
  );
}
