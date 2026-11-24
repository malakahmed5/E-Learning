import { useCallback, useEffect, useState } from "react";
import * as api from "../api/endpoints";
import type { TenantProfile } from "../api/types";
import { useAuth } from "../auth/AuthContext";
import { AppLayout, PageHeader } from "../components/layouts";
import { Alert, ErrorState, SkeletonRows } from "../components/ui";
import { useI18n } from "../i18n";
import type { TranslationKey } from "../i18n/ar";
import { SubscriptionCard } from "./student/SubscriptionCard";
import { teacherNavItems } from "./teacher/teacherNav";

// The first screen inside a teacher's platform.
// It reads the user's own details from that teacher's database.
export function HomePage() {
  const { t, errorMessage } = useI18n();
  const { session } = useAuth();

  const [profile, setProfile] = useState<TenantProfile | null>(null);
  const [error, setError] = useState<unknown>(null);

  const tenantId = session?.activeMembership?.tenantId;

  const load = useCallback(() => {
    setError(null);
    setProfile(null);
    api.getMyProfile().then(setProfile).catch(setError);
  }, []);

  // Load again when the user switches to another teacher.
  useEffect(() => {
    load();
  }, [load, tenantId]);

  if (!session?.activeMembership) {
    return null;
  }

  const membership = session.activeMembership;
  const roleName = t(`role.${membership.role}` as TranslationKey);

  return (
    <AppLayout navItems={teacherNavItems(membership.role)}>
      <PageHeader
        title={t("home.welcome", { name: session.account.fullName })}
        subtitle={t("home.youAre", { teacher: membership.tenantName, role: roleName })}
      />

      <section className="card">
        <h2>{t("home.profileTitle")}</h2>

        {error !== null && <ErrorState message={errorMessage(error)} onRetry={load} />}
        {error === null && !profile && <SkeletonRows rows={3} />}
        {profile && (
          <dl className="details">
            <div>
              <dt>{t("home.name")}</dt>
              <dd>{profile.fullName}</dd>
            </div>
            <div>
              <dt>{t("home.phone")}</dt>
              <dd dir="ltr">{profile.phoneNumber}</dd>
            </div>
            <div>
              <dt>{t("home.role")}</dt>
              <dd>{t(`role.${profile.role}` as TranslationKey)}</dd>
            </div>
          </dl>
        )}
      </section>

      {membership.role === "Student" && <SubscriptionCard />}

      <Alert kind="info">{t("home.comingSoon")}</Alert>
    </AppLayout>
  );
}
