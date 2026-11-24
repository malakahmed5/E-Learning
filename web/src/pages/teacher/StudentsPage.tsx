import { useCallback, useEffect, useState, type FormEvent } from "react";
import * as api from "../../api/endpoints";
import type { Group, Student, StudentInput } from "../../api/types";
import { useAuth } from "../../auth/AuthContext";
import { AppLayout, PageHeader } from "../../components/layouts";
import { CheckboxField, Pagination, SelectField, useLoad } from "../../components/shared";
import { Alert, Button, ConfirmDialog, Dialog, EmptyState, ErrorState, Field, SkeletonRows, Toast } from "../../components/ui";
import { useI18n } from "../../i18n";
import type { TranslationKey } from "../../i18n/ar";
import { isEgyptianMobile } from "../../validation";
import { GRADE_LEVELS, teacherNavItems } from "./teacherNav";

const PAGE_SIZE = 20;

// What the user asked to confirm: suspending a student, or unlocking their device.
type PendingAction = { kind: "suspend" | "unlock"; student: Student };

// The teacher's students: search, filter, add, edit, suspend, and unlock for a new device.
export function StudentsPage() {
  const { t, errorMessage } = useI18n();
  const { session } = useAuth();

  // What the user is typing, and the value we actually search for (set a moment after typing stops).
  const [searchText, setSearchText] = useState("");
  const [search, setSearch] = useState("");
  const [groupId, setGroupId] = useState<number | null>(null);
  const [gradeLevel, setGradeLevel] = useState<number | null>(null);
  const [subscription, setSubscription] = useState<"Expired" | "ExpiringSoon" | null>(null);
  const [pageIndex, setPageIndex] = useState(1);

  // Wait until the user stops typing before calling the API, instead of calling on every letter.
  useEffect(() => {
    const timer = window.setTimeout(() => {
      setSearch(searchText.trim());
      setPageIndex(1);
    }, 350);
    return () => window.clearTimeout(timer);
  }, [searchText]);

  const loadStudents = useCallback(
    () => api.listStudents({ search, groupId, gradeLevel, subscription, pageIndex, pageSize: PAGE_SIZE }),
    [search, groupId, gradeLevel, subscription, pageIndex],
  );
  const students = useLoad(loadStudents);

  // Only active groups can receive students.
  const loadGroups = useCallback(() => api.listGroups(false), []);
  const groups = useLoad(loadGroups);

  const [editing, setEditing] = useState<Student | "new" | null>(null);
  const [pendingAction, setPendingAction] = useState<PendingAction | null>(null);
  const [busy, setBusy] = useState(false);
  const [actionError, setActionError] = useState<string | null>(null);
  const [toast, setToast] = useState<string | null>(null);
  const clearToast = useCallback(() => setToast(null), []);

  if (!session?.activeMembership) {
    return null;
  }

  const hasFilters = search !== "" || groupId !== null || gradeLevel !== null || subscription !== null;
  const hasGroups = (groups.data?.length ?? 0) > 0;

  async function runAction(action: () => Promise<string>) {
    setBusy(true);
    setActionError(null);
    try {
      setToast(await action());
      students.reload();
    } catch (error) {
      setActionError(errorMessage(error));
    } finally {
      setPendingAction(null);
      setBusy(false);
    }
  }

  async function changeStatus(student: Student, isActive: boolean) {
    await runAction(async () => {
      await api.setStudentActive(student.id, isActive);
      return t("students.statusChanged");
    });
  }

  async function unlockDevice(student: Student) {
    await runAction(async () => {
      const result = await api.resetStudentDevice(student.id);
      return t("students.unlocked", { used: String(result.usedInCurrentTerm), limit: String(result.limit) });
    });
  }

  function handleSaved() {
    setEditing(null);
    setToast(t("students.saved"));
    students.reload();
  }

  const addButton = (
    <Button onClick={() => setEditing("new")} disabled={!hasGroups}>
      {t("students.add")}
    </Button>
  );

  return (
    <AppLayout navItems={teacherNavItems(session.activeMembership.role)}>
      <PageHeader title={t("students.title")} subtitle={t("students.subtitle")} action={addButton} />

      {actionError && <Alert>{actionError}</Alert>}
      {groups.data !== null && !hasGroups && <Alert kind="info">{t("students.needGroup")}</Alert>}

      <section className="card">
        <div className="toolbar">
          <input
            className="input"
            type="search"
            aria-label={t("common.search")}
            placeholder={t("students.searchPlaceholder")}
            value={searchText}
            onChange={(event) => setSearchText(event.target.value)}
          />
          <select
            className="input"
            aria-label={t("students.group")}
            value={groupId ?? ""}
            onChange={(event) => {
              setGroupId(event.target.value === "" ? null : Number(event.target.value));
              setPageIndex(1);
            }}
          >
            <option value="">{t("students.allGroups")}</option>
            {(groups.data ?? []).map((group) => (
              <option key={group.id} value={group.id}>
                {group.name}
              </option>
            ))}
          </select>
          <select
            className="input"
            aria-label={t("groups.grade")}
            value={gradeLevel ?? ""}
            onChange={(event) => {
              setGradeLevel(event.target.value === "" ? null : Number(event.target.value));
              setPageIndex(1);
            }}
          >
            <option value="">{t("students.allGrades")}</option>
            {GRADE_LEVELS.map((grade) => (
              <option key={grade} value={grade}>
                {t(`grade.${grade}` as TranslationKey)}
              </option>
            ))}
          </select>
          <select
            className="input"
            aria-label={t("students.subscription")}
            value={subscription ?? ""}
            onChange={(event) => {
              setSubscription(event.target.value === "" ? null : (event.target.value as "Expired" | "ExpiringSoon"));
              setPageIndex(1);
            }}
          >
            <option value="">{t("students.filter.all")}</option>
            <option value="Expired">{t("students.filter.Expired")}</option>
            <option value="ExpiringSoon">{t("students.filter.ExpiringSoon")}</option>
          </select>
        </div>

        {students.error !== null && <ErrorState message={errorMessage(students.error)} onRetry={students.reload} />}
        {students.error === null && students.data === null && <SkeletonRows />}
        {students.data !== null && students.data.count === 0 && (
          <EmptyState message={hasFilters ? t("students.emptySearch") : t("students.empty")} />
        )}
        {students.data !== null && students.data.count > 0 && (
          <>
            <p className="muted small">{t("students.count", { count: String(students.data.count) })}</p>
            <div className="table-wrap">
              <table className="table">
                <thead>
                  <tr>
                    <th>{t("students.name")}</th>
                    <th>{t("students.phone")}</th>
                    <th>{t("students.group")}</th>
                    <th>{t("students.parent")}</th>
                    <th>{t("teachers.status")}</th>
                    <th>{t("common.actions")}</th>
                  </tr>
                </thead>
                <tbody>
                  {students.data.data.map((student) => (
                    <tr key={student.id}>
                      <td>
                        <strong>{student.fullName}</strong>
                      </td>
                      <td dir="ltr">{student.phoneNumber}</td>
                      <td>
                        {student.groupName}
                        <div className="muted small">{t(`grade.${student.gradeLevel}` as TranslationKey)}</div>
                      </td>
                      <td>
                        {student.parentName}
                        <div className="muted small" dir="ltr">
                          {student.parentWhatsApp}
                        </div>
                      </td>
                      <td>
                        <StatusBadge student={student} />
                      </td>
                      <td>
                        <div className="row-actions">
                          <Button variant="ghost" onClick={() => setEditing(student)}>
                            {t("common.edit")}
                          </Button>
                          {student.isActive ? (
                            <Button variant="ghost" onClick={() => setPendingAction({ kind: "suspend", student })}>
                              {t("common.suspend")}
                            </Button>
                          ) : (
                            <Button variant="ghost" disabled={busy} onClick={() => changeStatus(student, true)}>
                              {t("common.restore")}
                            </Button>
                          )}
                          <Button variant="ghost" onClick={() => setPendingAction({ kind: "unlock", student })}>
                            {t("students.unlockDevice")}
                          </Button>
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <Pagination pageIndex={pageIndex} pageSize={PAGE_SIZE} totalCount={students.data.count} onChange={setPageIndex} />
          </>
        )}
      </section>

      {editing !== null && (
        <StudentDialog
          student={editing === "new" ? null : editing}
          groups={groups.data ?? []}
          onClose={() => setEditing(null)}
          onSaved={handleSaved}
        />
      )}

      {pendingAction?.kind === "suspend" && (
        <ConfirmDialog
          title={t("students.confirmSuspendTitle")}
          body={t("students.confirmSuspendBody", { name: pendingAction.student.fullName })}
          confirmLabel={t("common.suspend")}
          danger
          busy={busy}
          onConfirm={() => changeStatus(pendingAction.student, false)}
          onCancel={() => setPendingAction(null)}
        />
      )}

      {pendingAction?.kind === "unlock" && (
        <ConfirmDialog
          title={t("students.confirmUnlockTitle")}
          body={t("students.confirmUnlockBody", { name: pendingAction.student.fullName })}
          confirmLabel={t("students.unlockDevice")}
          busy={busy}
          onConfirm={() => unlockDevice(pendingAction.student)}
          onCancel={() => setPendingAction(null)}
        />
      )}

      <Toast message={toast} onDone={clearToast} />
    </AppLayout>
  );
}

// One label that tells the teacher where the student stands:
// suspended, not activated yet, free week, subscribed, or subscription ended.
function StatusBadge({ student }: { student: Student }) {
  const { t, formatDate } = useI18n();

  if (!student.isActive) {
    return <span className="badge badge-muted">{t("common.suspended")}</span>;
  }
  if (student.subscription === "NotActivated") {
    return <span className="badge badge-warning">{t("students.notActivated")}</span>;
  }

  const badgeClass =
    student.subscription === "Expired" ? "badge badge-warning" : student.subscription === "Trial" ? "badge badge-info" : "badge badge-success";

  return (
    <>
      <span className={badgeClass}>{t(`subscription.state.${student.subscription}` as TranslationKey)}</span>
      {student.accessEndsAt && <div className="muted small">{t("subscription.endsAt", { date: formatDate(student.accessEndsAt) })}</div>}
    </>
  );
}
// The form to add a student, or to edit one.
function StudentDialog({ student, groups, onClose, onSaved }: {
  student: Student | null;
  groups: Group[];
  onClose: () => void;
  onSaved: () => void;
}) {
  const { t, errorMessage } = useI18n();
  const isEdit = student !== null;

  const [fullName, setFullName] = useState(student?.fullName ?? "");
  const [phoneNumber, setPhoneNumber] = useState(student?.phoneNumber ?? "");
  const [groupId, setGroupId] = useState<number>(student?.groupId ?? groups[0]?.id ?? 0);
  const [parentName, setParentName] = useState(student?.parentName ?? "");
  // When editing, the parent number starts empty: empty means "keep the saved number".
  // (An assistant only ever sees the number with hidden digits, so it cannot be pre-filled.)
  const [parentWhatsApp, setParentWhatsApp] = useState("");
  const [parentConsent, setParentConsent] = useState(student?.parentConsent ?? false);
  const [hideFromHonorBoard, setHideFromHonorBoard] = useState(student?.hideFromHonorBoard ?? false);
  const [phoneError, setPhoneError] = useState<string | null>(null);
  const [parentPhoneError, setParentPhoneError] = useState<string | null>(null);
  const [formError, setFormError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    setFormError(null);

    const phoneIsValid = isEdit || isEgyptianMobile(phoneNumber);
    const parentPhoneMayBeEmpty = isEdit && parentWhatsApp === "";
    const parentPhoneIsValid = parentPhoneMayBeEmpty || isEgyptianMobile(parentWhatsApp);
    setPhoneError(phoneIsValid ? null : t("validation.phone"));
    setParentPhoneError(parentPhoneIsValid ? null : t("validation.phone"));
    if (!phoneIsValid || !parentPhoneIsValid) {
      return;
    }

    const input: StudentInput = {
      fullName: fullName.trim(),
      phoneNumber,
      groupId,
      parentName: parentName.trim(),
      parentWhatsApp,
      parentConsent,
      hideFromHonorBoard,
    };

    setBusy(true);
    try {
      if (student) {
        await api.updateStudent(student.id, input);
      } else {
        await api.createStudent(input);
      }
      onSaved();
    } catch (error) {
      setFormError(errorMessage(error));
    } finally {
      setBusy(false);
    }
  }

  const canSubmit =
    fullName.trim().length >= 2 && parentName.trim().length >= 2 && groupId > 0 && (isEdit || (phoneNumber !== "" && parentWhatsApp !== ""));

  return (
    <Dialog title={isEdit ? t("students.edit") : t("students.add")} onClose={onClose}>
      <form onSubmit={handleSubmit} noValidate>
        {formError && <Alert>{formError}</Alert>}

        <Field label={t("students.name")} value={fullName} onChange={(event) => setFullName(event.target.value)} maxLength={150} autoFocus required />

        {/* The phone number is the login name, so it cannot be changed after the student is added. */}
        <Field
          label={t("students.phone")}
          type="tel"
          inputMode="numeric"
          dir="ltr"
          placeholder={t("login.phonePlaceholder")}
          value={phoneNumber}
          onChange={(event) => setPhoneNumber(event.target.value)}
          error={phoneError}
          disabled={isEdit}
          required
        />

        <SelectField label={t("students.group")} value={groupId} onChange={(event) => setGroupId(Number(event.target.value))}>
          {/* A student in a turned-off group still needs to see their current group in the list. */}
          {student && !groups.some((group) => group.id === student.groupId) && (
            <option value={student.groupId}>{student.groupName}</option>
          )}
          {groups.map((group) => (
            <option key={group.id} value={group.id}>
              {group.name} ({t(`grade.${group.gradeLevel}` as TranslationKey)})
            </option>
          ))}
        </SelectField>

        <Field label={t("students.parentName")} value={parentName} onChange={(event) => setParentName(event.target.value)} maxLength={150} required />
        <Field
          label={t("students.parentPhone")}
          type="tel"
          inputMode="numeric"
          dir="ltr"
          placeholder={isEdit ? student.parentWhatsApp : t("login.phonePlaceholder")}
          hint={isEdit ? t("students.parentPhoneKeep") : undefined}
          value={parentWhatsApp}
          onChange={(event) => setParentWhatsApp(event.target.value)}
          error={parentPhoneError}
          required={!isEdit}
        />

        <CheckboxField label={t("students.consent")} checked={parentConsent} onChange={setParentConsent} />
        <CheckboxField label={t("students.hide")} checked={hideFromHonorBoard} onChange={setHideFromHonorBoard} />

        <div className="dialog-footer">
          <Button type="button" variant="ghost" onClick={onClose} disabled={busy}>
            {t("common.cancel")}
          </Button>
          <Button type="submit" busy={busy} disabled={!canSubmit}>
            {t("common.save")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}
