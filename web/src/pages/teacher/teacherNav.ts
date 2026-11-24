import type { Role } from "../../api/types";
import type { TranslationKey } from "../../i18n/ar";

interface NavItem {
  to: string;
  label: TranslationKey;
}

// The side menu inside a teacher's platform. It depends on the role:
// students see only the home page, and only the teacher sees the two "settings" areas.
// (This only hides links. The API refuses the calls anyway.)
export function teacherNavItems(role: Role): NavItem[] {
  if (role === "Student") {
    return [];
  }

  const items: NavItem[] = [
    { to: "/", label: "teacher.nav.home" },
    { to: "/students", label: "teacher.nav.students" },
    { to: "/groups", label: "teacher.nav.groups" },
    { to: "/centers", label: "teacher.nav.centers" },
  ];

  if (role === "Teacher") {
    items.push({ to: "/assistants", label: "teacher.nav.assistants" });
    items.push({ to: "/activity", label: "teacher.nav.activity" });
  }
  return items;
}

export const GRADE_LEVELS = [1, 2, 3, 4, 5, 6];

// Saturday first, the way a school week is written in Egypt.
export const DAY_NAMES = ["Saturday", "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday"] as const;
