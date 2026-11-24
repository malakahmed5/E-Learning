import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { useAuth } from "../auth/AuthContext";
import { AuthLayout } from "../components/layouts";
import { Alert, Button, EmptyState } from "../components/ui";
import { useI18n } from "../i18n";
import type { TranslationKey } from "../i18n/ar";

// Shown after login when the account belongs to more than one teacher.
// The user logs in once and picks which teacher's platform to open.
export function ChooseTeacherPage() {
  const { t, errorMessage } = useI18n();
  const { session, chooseTeacher, logout } = useAuth();
  const navigate = useNavigate();

  const [busyTenantId, setBusyTenantId] = useState<number | null>(null);
  const [error, setError] = useState<string | null>(null);

  if (!session) {
    return null;
  }

  async function handleChoose(tenantId: number) {
    setError(null);
    setBusyTenantId(tenantId);
    try {
      await chooseTeacher(tenantId);
      navigate("/");
    } catch (chooseError) {
      setError(errorMessage(chooseError));
    } finally {
      setBusyTenantId(null);
    }
  }

  async function handleLogout() {
    await logout();
    navigate("/login");
  }

  return (
    <AuthLayout title={t("choose.title")} subtitle={session.memberships.length > 0 ? t("choose.subtitle") : undefined}>
      {error && <Alert>{error}</Alert>}

      {session.memberships.length === 0 ? (
        <EmptyState message={t("choose.empty")} />
      ) : (
        <ul className="teacher-list">
          {session.memberships.map((membership) => (
            <li key={membership.tenantId}>
              <button
                type="button"
                className="teacher-card"
                disabled={busyTenantId !== null}
                onClick={() => handleChoose(membership.tenantId)}
              >
                <span className="teacher-avatar" aria-hidden="true">
                  {membership.tenantName.charAt(0)}
                </span>
                <span className="teacher-info">
                  <strong>{membership.tenantName}</strong>
                  <span className="muted small">{t(`role.${membership.role}` as TranslationKey)}</span>
                </span>
                {busyTenantId === membership.tenantId && <span className="spinner" aria-hidden="true" />}
              </button>
            </li>
          ))}
        </ul>
      )}

      <Button variant="ghost" block onClick={handleLogout}>
        {t("common.logout")}
      </Button>
    </AuthLayout>
  );
}
