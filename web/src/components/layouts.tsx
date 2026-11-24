// The two page frames of the website:
//   AuthLayout - the login screens (before the user is inside).
//   AppLayout  - the screens after login (top bar, side menu, content).

import type { ReactNode } from "react";
import { NavLink, useNavigate } from "react-router-dom";
import { useAuth } from "../auth/AuthContext";
import { useI18n } from "../i18n";
import type { TranslationKey } from "../i18n/ar";
import { LanguageSwitch } from "./ui";

export function AuthLayout({ title, subtitle, children }: { title: string; subtitle?: string; children: ReactNode }) {
  const { t } = useI18n();
  return (
    <div className="auth-page">
      <aside className="auth-brand">
        <div className="brand-mark" aria-hidden="true">
          ✦
        </div>
        <h1>{t("app.name")}</h1>
        <p>{t("app.tagline")}</p>
      </aside>

      <main className="auth-main">
        <div className="auth-top">
          <LanguageSwitch />
        </div>
        <div className="auth-card">
          <h2>{title}</h2>
          {subtitle && <p className="muted">{subtitle}</p>}
          {children}
        </div>
      </main>
    </div>
  );
}

interface NavItem {
  to: string;
  label: TranslationKey;
}

export function AppLayout({ navItems, children }: { navItems: NavItem[]; children: ReactNode }) {
  const { session, logout } = useAuth();
  const { t } = useI18n();
  const navigate = useNavigate();

  if (!session) {
    return null;
  }

  const membership = session.activeMembership;
  const placeName = membership ? membership.tenantName : t("role.admin");
  const roleName = membership ? t(`role.${membership.role}` as TranslationKey) : t("role.admin");

  async function handleLogout() {
    await logout();
    navigate("/login");
  }

  return (
    <div className="app-page">
      <header className="app-header">
        <div className="app-title">
          <span className="brand-mark brand-mark-small" aria-hidden="true">
            ✦
          </span>
          <div>
            <strong>{placeName}</strong>
            <span className="muted small">{roleName}</span>
          </div>
        </div>

        <div className="app-header-actions">
          {session.memberships.length > 1 && (
            <button type="button" className="btn btn-ghost btn-small" onClick={() => navigate("/choose-teacher")}>
              {t("choose.switch")}
            </button>
          )}
          <LanguageSwitch />
          <span className="user-name">{session.account.fullName}</span>
          <button type="button" className="btn btn-secondary btn-small" onClick={handleLogout}>
            {t("common.logout")}
          </button>
        </div>
      </header>

      <div className="app-body">
        {navItems.length > 0 && (
          <nav className="app-nav" aria-label={placeName}>
            {navItems.map((item) => (
              <NavLink key={item.to} to={item.to} className={({ isActive }) => (isActive ? "nav-link active" : "nav-link")}>
                {t(item.label)}
              </NavLink>
            ))}
          </nav>
        )}
        <main className="app-content">{children}</main>
      </div>
    </div>
  );
}

export function PageHeader({ title, subtitle, action }: { title: string; subtitle?: string; action?: ReactNode }) {
  return (
    <div className="page-header">
      <div>
        <h1>{title}</h1>
        {subtitle && <p className="muted">{subtitle}</p>}
      </div>
      {action}
    </div>
  );
}
