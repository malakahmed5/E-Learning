// The shapes of the data the API sends and receives.
// They mirror the C# classes in the ELearning.Shared project. Keep the two in sync.

export type LoginStatus = "Authenticated" | "TwoFactorRequired" | "TwoFactorSetupRequired";

export type Role = "Teacher" | "Assistant" | "Student";

export interface Account {
  id: number;
  fullName: string;
  phoneNumber: string;
  isPlatformAdmin: boolean;
}

export interface Membership {
  tenantId: number;
  tenantSlug: string;
  tenantName: string;
  tenantLogoUrl: string | null;
  role: Role;
}

export interface Session {
  accessToken: string;
  accessTokenExpiresAt: string;
  refreshToken: string;
  account: Account;
  memberships: Membership[];
  activeMembership: Membership | null;
}

export interface LoginResponse {
  status: LoginStatus;
  challengeToken: string | null;
  session: Session | null;
}

export interface TwoFactorSetup {
  secret: string;
  otpAuthUri: string;
}

export interface TenantProfile {
  userId: number;
  fullName: string;
  phoneNumber: string;
  role: Role;
}

export interface Tenant {
  id: number;
  slug: string;
  displayName: string;
  logoUrl: string | null;
  isActive: boolean;
  createdAt: string;
  teacherFullName: string | null;
  teacherPhoneNumber: string | null;
}

export interface Invite {
  code: string;
  expiresAt: string;
}

export interface CreateTenantResponse {
  tenant: Tenant;
  invite: Invite | null;
}

export interface AcademicTerm {
  id: number;
  name: string;
  startDate: string;
  endDate: string;
}

// ---------- Teacher management ----------

export interface Paginated<T> {
  count: number;
  pageSize: number;
  pageIndex: number;
  data: T[];
}

export interface Center {
  id: number;
  name: string;
  address: string | null;
  groupCount: number;
}

export type DayName = "Sunday" | "Monday" | "Tuesday" | "Wednesday" | "Thursday" | "Friday" | "Saturday";

export interface Schedule {
  dayOfWeek: DayName;
  // Times are sent as "16:00:00".
  startTime: string;
  endTime: string;
}

export interface Group {
  id: number;
  name: string;
  gradeLevel: number;
  centerId: number | null;
  centerName: string | null;
  isActive: boolean;
  studentCount: number;
  schedules: Schedule[];
}

export interface GroupInput {
  name: string;
  gradeLevel: number;
  centerId: number | null;
  schedules: Schedule[];
}

export interface Student {
  id: number;
  fullName: string;
  phoneNumber: string;
  groupId: number;
  groupName: string;
  gradeLevel: number;
  parentName: string;
  // Hidden digits when the viewer is an assistant.
  parentWhatsApp: string;
  parentConsent: boolean;
  hideFromHonorBoard: boolean;
  isActive: boolean;
  isActivated: boolean;
  subscription: SubscriptionState;
  accessEndsAt: string | null;
  expiringSoon: boolean;
}

export interface StudentInput {
  fullName: string;
  phoneNumber: string;
  groupId: number;
  parentName: string;
  // Empty when editing means "keep the saved number".
  parentWhatsApp: string;
  parentConsent: boolean;
  hideFromHonorBoard: boolean;
}

export interface DeviceReset {
  usedInCurrentTerm: number;
  limit: number;
}

export interface Assistant {
  id: number;
  fullName: string;
  phoneNumber: string;
  isActive: boolean;
}

export interface CreateAssistantResponse {
  assistant: Assistant;
  invite: Invite | null;
}

export interface AuditLogEntry {
  id: number;
  userId: number;
  userName: string;
  action: string;
  entityName: string;
  entityId: string;
  details: string | null;
  createdAt: string;
}

// ---------- Subscription codes ----------

export type CodeKind = "FreeWeek" | "Subscription30";
export type CodeState = "Available" | "Redeemed" | "Expired" | "Revoked";
export type SubscriptionState = "NotActivated" | "Trial" | "Active" | "Expired";

export interface GeneratedBatch {
  batchId: number;
  type: CodeKind;
  durationDays: number;
  redeemBefore: string | null;
  codes: string[];
}

export interface CodeItem {
  id: number;
  code: string;
  type: CodeKind;
  durationDays: number;
  status: CodeState;
  batchId: number;
  batchNote: string | null;
  createdAt: string;
  redeemBefore: string | null;
  redeemedByName: string | null;
  redeemedByPhone: string | null;
  redeemedAtTeacher: string | null;
  redeemedAt: string | null;
}

export interface CodeStats {
  type: CodeKind;
  total: number;
  available: number;
  redeemed: number;
  expired: number;
  revoked: number;
}

export interface SubscriptionStatus {
  status: SubscriptionState;
  endsAt: string | null;
  daysLeft: number;
  expiringSoon: boolean;
}

export interface RedeemResult {
  type: CodeKind;
  durationDays: number;
  startsAt: string;
  endsAt: string;
}