import { useCallback, useState, type FormEvent } from "react";
import * as api from "../../api/endpoints";
import type { Center, DayName, Group, Schedule } from "../../api/types";
import { useAuth } from "../../auth/AuthContext";
import { AppLayout, PageHeader } from "../../components/layouts";
import { CheckboxField, SelectField, useLoad } from "../../components/shared";
import { Alert, Button, ConfirmDialog, Dialog, EmptyState, ErrorState, Field, SkeletonRows, Toast } from "../../components/ui";
import { useI18n } from "../../i18n";
import type { TranslationKey } from "../../i18n/ar";
import { DAY_NAMES, GRADE_LEVELS, teacherNavItems } from "./teacherNav";

// "16:00:00" from the API becomes "16:00" for the time input and for display.
function shortTime(time: string): string {
  return time.substring(0, 5);
}

// Groups of students and their weekly lesson times.
export function GroupsPage() {
  const { t, errorMessage } = useI18n();
  const { session } = useAuth();

  const [showInactive, setShowInactive] = useState(false);
  const loadGroups = useCallback(() => api.listGroups(showInactive), [showInactive]);
  const groups = useLoad(loadGroups);
  const centers = useLoad(api.listCenters);

  const [editing, setEditing] = useState<Group | "new" | null>(null);
  const [deleting, setDeleting] = useState<Group | null>(null);
  const [busy, setBusy] = useState(false);
  const [actionError, setActionError] = useState<string | null>(null);
  const [toast, setToast] = useState<string | null>(null);
  const clearToast = useCallback(() => setToast(null), []);

  if (!session?.activeMembership) {
    return null;
  }

  async function runAction(action: () => Promise<void>, successMessage: string) {
    setBusy(true);
    setActionError(null);
    try {
      await action();
      setToast(successMessage);
      groups.reload();
    } catch (error) {
      setActionError(errorMessage(error));
    } finally {
      setDeleting(null);
      setBusy(false);
    }
  }

  function handleSaved() {
    setEditing(null);
    setToast(t("groups.saved"));
    groups.reload();
  }

  return (
    <AppLayout navItems={teacherNavItems(session.activeMembership.role)}>
      <PageHeader
        title={t("groups.title")}
        subtitle={t("groups.subtitle")}
        action={<Button onClick={() => setEditing("new")}>{t("groups.add")}</Button>}
      />

      {actionError && <Alert>{actionError}</Alert>}

      <section className="card">
        <CheckboxField label={t("groups.showInactive")} checked={showInactive} onChange={setShowInactive} />

        {groups.error !== null && <ErrorState message={errorMessage(groups.error)} onRetry={groups.reload} />}
        {groups.error === null && groups.data === null && <SkeletonRows />}
        {groups.data !== null && groups.data.length === 0 && (
          <EmptyState message={t("groups.empty")} action={<Button onClick={() => setEditing("new")}>{t("groups.add")}</Button>} />
        )}
        {groups.data !== null && groups.data.length > 0 && (
          <div className="table-wrap">
            <table className="table">
              <thead>
                <tr>
                  <th>{t("groups.name")}</th>
                  <th>{t("groups.grade")}</th>
                  <th>{t("groups.center")}</th>
                  <th>{t("groups.lessons")}</th>
                  <th>{t("groups.students")}</th>
                  <th>{t("teachers.status")}</th>
                  <th>{t("common.actions")}</th>
                </tr>
              </thead>
              <tbody>
                {groups.data.map((group) => (
                  <tr key={group.id}>
                    <td>
                      <strong>{group.name}</strong>
                    </td>
                    <td>{t(`grade.${group.gradeLevel}` as TranslationKey)}</td>
                    <td>{group.centerName ?? t("common.online")}</td>
                    <td>
                      <ul className="lesson-list">
                        {group.schedules.map((lesson) => (
                          <li key={`${lesson.dayOfWeek}-${lesson.startTime}`}>
                            {t(`day.${lesson.dayOfWeek}` as TranslationKey)}{" "}
                            <span dir="ltr">
                              {shortTime(lesson.startTime)} - {shortTime(lesson.endTime)}
                            </span>
                          </li>
                        ))}
                      </ul>
                    </td>
                    <td>{group.studentCount}</td>
                    <td>
                      <span className={group.isActive ? "badge badge-success" : "badge badge-muted"}>
                        {group.isActive ? t("common.active") : t("common.inactive")}
                      </span>
                    </td>
                    <td>
                      <div className="row-actions">
                        <Button variant="ghost" onClick={() => setEditing(group)}>
                          {t("common.edit")}
                        </Button>
                        <Button
                          variant="ghost"
                          disabled={busy}
                          onClick={() => runAction(() => api.setGroupActive(group.id, !group.isActive), t("teachers.statusChanged"))}
                        >
                          {group.isActive ? t("teachers.turnOff") : t("teachers.turnOn")}
                        </Button>
                        <Button variant="ghost" onClick={() => setDeleting(group)}>
                          {t("common.delete")}
                        </Button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>

      {editing !== null && (
        <GroupDialog
          group={editing === "new" ? null : editing}
          centers={centers.data ?? []}
          onClose={() => setEditing(null)}
          onSaved={handleSaved}
        />
      )}

      {deleting !== null && (
        <ConfirmDialog
          title={t("groups.confirmDeleteTitle")}
          body={t("groups.confirmDeleteBody", { name: deleting.name })}
          confirmLabel={t("common.delete")}
          danger
          busy={busy}
          onConfirm={() => runAction(() => api.deleteGroup(deleting.id), t("groups.deleted"))}
          onCancel={() => setDeleting(null)}
        />
      )}

      <Toast message={toast} onDone={clearToast} />
    </AppLayout>
  );
}

// One lesson row in the form. Times are kept as "HH:mm" while editing.
interface LessonDraft {
  dayOfWeek: DayName;
  startTime: string;
  endTime: string;
}

function GroupDialog({ group, centers, onClose, onSaved }: {
  group: Group | null;
  centers: Center[];
  onClose: () => void;
  onSaved: () => void;
}) {
  const { t, errorMessage } = useI18n();

  const [name, setName] = useState(group?.name ?? "");
  const [gradeLevel, setGradeLevel] = useState(group?.gradeLevel ?? 1);
  const [centerId, setCenterId] = useState<number | null>(group?.centerId ?? null);
  const [lessons, setLessons] = useState<LessonDraft[]>(
    group
      ? group.schedules.map((lesson) => ({
          dayOfWeek: lesson.dayOfWeek,
          startTime: shortTime(lesson.startTime),
          endTime: shortTime(lesson.endTime),
        }))
      : [{ dayOfWeek: "Saturday", startTime: "16:00", endTime: "18:00" }],
  );
  const [formError, setFormError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  function changeLesson(index: number, change: Partial<LessonDraft>) {
    setLessons(lessons.map((lesson, position) => (position === index ? { ...lesson, ...change } : lesson)));
  }

  function addLesson() {
    setLessons([...lessons, { dayOfWeek: "Tuesday", startTime: "16:00", endTime: "18:00" }]);
  }

  function removeLesson(index: number) {
    setLessons(lessons.filter((_, position) => position !== index));
  }

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    setFormError(null);
    setBusy(true);
    try {
      // The API expects times with seconds: "16:00:00".
      const schedules: Schedule[] = lessons.map((lesson) => ({
        dayOfWeek: lesson.dayOfWeek,
        startTime: `${lesson.startTime}:00`,
        endTime: `${lesson.endTime}:00`,
      }));
      await api.saveGroup(group?.id ?? null, { name: name.trim(), gradeLevel, centerId, schedules });
      onSaved();
    } catch (error) {
      setFormError(errorMessage(error));
    } finally {
      setBusy(false);
    }
  }

  const everyLessonHasTimes = lessons.every((lesson) => lesson.startTime && lesson.endTime);
  const canSubmit = name.trim().length >= 2 && lessons.length > 0 && everyLessonHasTimes;

  return (
    <Dialog title={group ? t("groups.edit") : t("groups.add")} onClose={onClose}>
      <form onSubmit={handleSubmit} noValidate>
        {formError && <Alert>{formError}</Alert>}

        <Field label={t("groups.name")} value={name} onChange={(event) => setName(event.target.value)} maxLength={150} autoFocus required />

        <SelectField label={t("groups.grade")} value={gradeLevel} onChange={(event) => setGradeLevel(Number(event.target.value))}>
          {GRADE_LEVELS.map((grade) => (
            <option key={grade} value={grade}>
              {t(`grade.${grade}` as TranslationKey)}
            </option>
          ))}
        </SelectField>

        <SelectField
          label={t("groups.center")}
          value={centerId ?? ""}
          onChange={(event) => setCenterId(event.target.value === "" ? null : Number(event.target.value))}
        >
          <option value="">{t("common.online")}</option>
          {centers.map((center) => (
            <option key={center.id} value={center.id}>
              {center.name}
            </option>
          ))}
        </SelectField>

        {lessons.map((lesson, index) => (
          <div className="lesson-row" key={index}>
            <SelectField
              label={t("groups.day")}
              value={lesson.dayOfWeek}
              onChange={(event) => changeLesson(index, { dayOfWeek: event.target.value as DayName })}
            >
              {DAY_NAMES.map((day) => (
                <option key={day} value={day}>
                  {t(`day.${day}` as TranslationKey)}
                </option>
              ))}
            </SelectField>
            <Field label={t("groups.from")} type="time" value={lesson.startTime} onChange={(event) => changeLesson(index, { startTime: event.target.value })} required />
            <Field label={t("groups.to")} type="time" value={lesson.endTime} onChange={(event) => changeLesson(index, { endTime: event.target.value })} required />
            <div className="field">
              <Button type="button" variant="ghost" disabled={lessons.length === 1} onClick={() => removeLesson(index)} aria-label={t("groups.removeLesson")}>
                ✕
              </Button>
            </div>
          </div>
        ))}

        <Button type="button" variant="secondary" onClick={addLesson}>
          {t("groups.addLesson")}
        </Button>

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
