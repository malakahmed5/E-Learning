import type { ReactNode } from "react";
import { BrowserRouter, Navigate, Route, Routes } from "react-router-dom";
import { AuthProvider, useAuth } from "./auth/AuthContext";
import { LoadingScreen } from "./components/ui";
import { I18nProvider } from "./i18n";
import { AcceptInvitePage } from "./pages/AcceptInvitePage";
import { ActivatePage } from "./pages/ActivatePage";
import { ChooseTeacherPage } from "./pages/ChooseTeacherPage";
import { HomePage } from "./pages/HomePage";
import { LoginPage } from "./pages/LoginPage";
import { TwoFactorPage } from "./pages/TwoFactorPage";
import { TeachersPage } from "./pages/admin/TeachersPage";
import { CodesPage } from "./pages/admin/CodesPage";
import { TermsPage } from "./pages/admin/TermsPage";
import { ActivityLogPage } from "./pages/teacher/ActivityLogPage";
import { AssistantsPage } from "./pages/teacher/AssistantsPage";
import { CentersPage } from "./pages/teacher/CentersPage";
import { GroupsPage } from "./pages/teacher/GroupsPage";
import { StudentsPage } from "./pages/teacher/StudentsPage";
import type { Role } from "./api/types";

export function App() {
  return (
    <I18nProvider>
      <AuthProvider>
        <BrowserRouter>
          <AppRoutes />
        </BrowserRouter>
      </AuthProvider>
    </I18nProvider>
  );
}

function AppRoutes() {
  const { isRestoring } = useAuth();

  // Wait until we know whether the user is still logged in, so we do not flash the login page.
  if (isRestoring) {
    return <LoadingScreen />;
  }

  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route path="/two-factor" element={<TwoFactorPage mode="verify" />} />
      <Route path="/two-factor/setup" element={<TwoFactorPage mode="setup" />} />
      <Route path="/accept-invite" element={<AcceptInvitePage />} />
      <Route path="/activate" element={<ActivatePage />} />

      <Route path="/choose-teacher" element={<RequireLogin><ChooseTeacherPage /></RequireLogin>} />
      <Route path="/admin/teachers" element={<RequireAdmin><TeachersPage /></RequireAdmin>} />
      <Route path="/admin/terms" element={<RequireAdmin><TermsPage /></RequireAdmin>} />
      <Route path="/admin/codes" element={<RequireAdmin><CodesPage /></RequireAdmin>} />

      <Route path="/students" element={<RequireRole roles={["Teacher", "Assistant"]}><StudentsPage /></RequireRole>} />
      <Route path="/groups" element={<RequireRole roles={["Teacher", "Assistant"]}><GroupsPage /></RequireRole>} />
      <Route path="/centers" element={<RequireRole roles={["Teacher", "Assistant"]}><CentersPage /></RequireRole>} />
      <Route path="/assistants" element={<RequireRole roles={["Teacher"]}><AssistantsPage /></RequireRole>} />
      <Route path="/activity" element={<RequireRole roles={["Teacher"]}><ActivityLogPage /></RequireRole>} />

      <Route path="/" element={<RequireLogin><StartPage /></RequireLogin>} />
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}

// These guards only decide what the browser shows. They are NOT the security:
// the API checks the token and the permissions on every request.

function RequireLogin({ children }: { children: ReactNode }) {
  const { session } = useAuth();
  if (!session) {
    return <Navigate to="/login" replace />;
  }
  return <>{children}</>;
}

function RequireAdmin({ children }: { children: ReactNode }) {
  const { session } = useAuth();
  if (!session) {
    return <Navigate to="/login" replace />;
  }
  if (!session.account.isPlatformAdmin) {
    return <Navigate to="/" replace />;
  }
  return <>{children}</>;
}

// For pages inside a teacher's platform that only some roles may open.
function RequireRole({ roles, children }: { roles: Role[]; children: ReactNode }) {
  const { session } = useAuth();
  if (!session) {
    return <Navigate to="/login" replace />;
  }

  const role = session.activeMembership?.role;
  if (!role || !roles.includes(role)) {
    return <Navigate to="/" replace />;
  }
  return <>{children}</>;
}

// Decides where a logged-in user starts.
function StartPage() {
  const { session } = useAuth();
  if (!session) {
    return null;
  }

  // Inside a teacher's platform.
  if (session.activeMembership) {
    return <HomePage />;
  }

  // The platform team works outside any teacher.
  if (session.account.isPlatformAdmin) {
    return <Navigate to="/admin/teachers" replace />;
  }

  // Several teachers (or none): the user has to choose.
  return <Navigate to="/choose-teacher" replace />;
}
