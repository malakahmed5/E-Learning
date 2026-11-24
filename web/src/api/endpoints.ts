// Every API endpoint the website uses, as one small function each.
// Pages call these functions and never build URLs themselves.

import { apiRequest } from "./client";
import type {
  AcademicTerm,
  Assistant,
  AuditLogEntry,
  CodeItem,
  CodeKind,
  CodeState,
  CodeStats,
  GeneratedBatch,
  RedeemResult,
  SubscriptionStatus,
  Center,
  CreateAssistantResponse,
  DeviceReset,
  Group,
  GroupInput,
  Paginated,
  Student,
  StudentInput,
  CreateTenantResponse,
  Invite,
  LoginResponse,
  Session,
  Tenant,
  TenantProfile,
  TwoFactorSetup,
} from "./types";

// ---------- Login ----------

export function login(phoneNumber: string, password: string, deviceId: string) {
  return apiRequest<LoginResponse>("/api/auth/login", {
    method: "POST",
    anonymous: true,
    body: { phoneNumber, password, deviceId },
  });
}

export function setupTwoFactor(challengeToken: string) {
  return apiRequest<TwoFactorSetup>("/api/auth/two-factor/setup", {
    method: "POST",
    anonymous: true,
    body: { challengeToken },
  });
}

export function verifyTwoFactor(challengeToken: string, code: string) {
  return apiRequest<Session>("/api/auth/two-factor/verify", {
    method: "POST",
    anonymous: true,
    body: { challengeToken, code },
  });
}

// Also used to choose or switch teacher: pass the teacher's id.
export function refreshSession(refreshToken: string, deviceId: string | null, tenantId: number | null) {
  return apiRequest<Session>("/api/auth/refresh", {
    method: "POST",
    anonymous: true,
    body: { refreshToken, deviceId, tenantId },
  });
}

export function logout(refreshToken: string) {
  return apiRequest<void>("/api/auth/logout", {
    method: "POST",
    anonymous: true,
    body: { refreshToken },
  });
}

export function acceptInvite(phoneNumber: string, inviteCode: string, password: string) {
  return apiRequest<void>("/api/auth/accept-invite", {
    method: "POST",
    anonymous: true,
    body: { phoneNumber, inviteCode, password },
  });
}

// ---------- Inside a teacher's platform ----------

export function getMyProfile() {
  return apiRequest<TenantProfile>("/api/profile");
}

// ---------- Platform admin ----------

export function listTenants() {
  return apiRequest<Tenant[]>("/api/admin/tenants");
}

export function createTenant(input: {
  slug: string;
  displayName: string;
  teacherFullName: string;
  teacherPhoneNumber: string;
}) {
  return apiRequest<CreateTenantResponse>("/api/admin/tenants", { method: "POST", body: input });
}

export function setTenantStatus(tenantId: number, isActive: boolean) {
  return apiRequest<void>(`/api/admin/tenants/${tenantId}/status`, { method: "PUT", body: { isActive } });
}

export function resetTeacherInvite(tenantId: number) {
  return apiRequest<Invite>(`/api/admin/tenants/${tenantId}/teacher-invite`, { method: "POST" });
}

export function listAcademicTerms() {
  return apiRequest<AcademicTerm[]>("/api/admin/academic-terms");
}

export function createAcademicTerm(input: { name: string; startDate: string; endDate: string }) {
  return apiRequest<AcademicTerm>("/api/admin/academic-terms", { method: "POST", body: input });
}

// ---------- Teacher management (inside a teacher's platform) ----------

// Builds "?a=1&b=2" from an object, skipping empty values.
function toQuery(values: Record<string, string | number | boolean | null | undefined>): string {
  const params = new URLSearchParams();
  for (const key of Object.keys(values)) {
    const value = values[key];
    if (value !== null && value !== undefined && value !== "") {
      params.set(key, String(value));
    }
  }
  const text = params.toString();
  return text ? `?${text}` : "";
}

export function listCenters() {
  return apiRequest<Center[]>("/api/centers");
}

export function saveCenter(centerId: number | null, input: { name: string; address: string | null }) {
  return centerId === null
    ? apiRequest<Center>("/api/centers", { method: "POST", body: input })
    : apiRequest<Center>(`/api/centers/${centerId}`, { method: "PUT", body: input });
}

export function deleteCenter(centerId: number) {
  return apiRequest<void>(`/api/centers/${centerId}`, { method: "DELETE" });
}

export function listGroups(includeInactive: boolean) {
  return apiRequest<Group[]>(`/api/groups${toQuery({ includeInactive })}`);
}

export function saveGroup(groupId: number | null, input: GroupInput) {
  return groupId === null
    ? apiRequest<Group>("/api/groups", { method: "POST", body: input })
    : apiRequest<Group>(`/api/groups/${groupId}`, { method: "PUT", body: input });
}

export function setGroupActive(groupId: number, isActive: boolean) {
  return apiRequest<void>(`/api/groups/${groupId}/status`, { method: "PUT", body: { isActive } });
}

export function deleteGroup(groupId: number) {
  return apiRequest<void>(`/api/groups/${groupId}`, { method: "DELETE" });
}

export function listStudents(query: {
  search: string;
  groupId: number | null;
  gradeLevel: number | null;
  subscription: "Expired" | "ExpiringSoon" | null;
  pageIndex: number;
  pageSize: number;
}) {
  return apiRequest<Paginated<Student>>(`/api/students${toQuery(query)}`);
}

export function createStudent(input: StudentInput) {
  return apiRequest<Student>("/api/students", { method: "POST", body: input });
}

export function updateStudent(studentId: number, input: StudentInput) {
  // An empty parent number tells the API to keep the saved one.
  const body = { ...input, parentWhatsApp: input.parentWhatsApp === "" ? null : input.parentWhatsApp };
  return apiRequest<Student>(`/api/students/${studentId}`, { method: "PUT", body });
}

export function setStudentActive(studentId: number, isActive: boolean) {
  return apiRequest<void>(`/api/students/${studentId}/status`, { method: "PUT", body: { isActive } });
}

export function resetStudentDevice(studentId: number) {
  return apiRequest<DeviceReset>(`/api/students/${studentId}/device-reset`, { method: "POST" });
}

export function listAssistants() {
  return apiRequest<Assistant[]>("/api/assistants");
}

export function createAssistant(input: { fullName: string; phoneNumber: string }) {
  return apiRequest<CreateAssistantResponse>("/api/assistants", { method: "POST", body: input });
}

export function setAssistantActive(assistantId: number, isActive: boolean) {
  return apiRequest<void>(`/api/assistants/${assistantId}/status`, { method: "PUT", body: { isActive } });
}

export function listAuditLogs(query: { userId: number | null; pageIndex: number; pageSize: number }) {
  return apiRequest<Paginated<AuditLogEntry>>(`/api/audit-logs${toQuery(query)}`);
}

// ---------- Subscription codes ----------

// Platform owner.
export function generateCodes(input: { type: CodeKind; count: number; redeemBefore: string | null; note: string | null }) {
  return apiRequest<GeneratedBatch>("/api/admin/codes/batches", { method: "POST", body: input });
}

export function listCodes(query: {
  type: CodeKind | null;
  status: CodeState | null;
  search: string;
  pageIndex: number;
  pageSize: number;
}) {
  return apiRequest<Paginated<CodeItem>>(`/api/admin/codes${toQuery(query)}`);
}

export function getCodeStats() {
  return apiRequest<CodeStats[]>("/api/admin/codes/stats");
}

export function revokeCode(codeId: number) {
  return apiRequest<void>(`/api/admin/codes/${codeId}/revoke`, { method: "POST" });
}

// Student.
export function getMySubscription() {
  return apiRequest<SubscriptionStatus>("/api/subscription");
}

export function redeemCode(code: string) {
  return apiRequest<RedeemResult>("/api/subscription/redeem", { method: "POST", body: { code } });
}

// A new student opens the account with a code and chooses a password.
export function activateAccount(input: { phoneNumber: string; code: string; password: string; tenantSlug: string | null }) {
  return apiRequest<RedeemResult>("/api/auth/activate", { method: "POST", anonymous: true, body: input });
}