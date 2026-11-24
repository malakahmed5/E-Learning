import type { TranslationKey } from "../../i18n/ar";

// The side menu of the platform admin area.
export const adminNavItems: { to: string; label: TranslationKey }[] = [
  { to: "/admin/teachers", label: "admin.nav.teachers" },
  { to: "/admin/codes", label: "admin.nav.codes" },
  { to: "/admin/terms", label: "admin.nav.terms" },
];
