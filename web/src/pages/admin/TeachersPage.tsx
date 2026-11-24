import { useCallback, useEffect, useState, type FormEvent } from "react";
import * as api from "../../api/endpoints";
import type { Invite, Tenant } from "../../api/types";
import { AppLayout, PageHeader } from "../../components/layouts";
import { InviteCodeDialog } from "../../components/shared";
import { Alert, Button, ConfirmDialog, Dialog, EmptyState, ErrorState, Field, SkeletonRows, Toast } from "../../components/ui";
import { useI18n } from "../../i18n";
import { isEgyptianMobile, isValidSlug } from "../../validation";
import { adminNavItems } from "./adminNav";

// What the admin asked to confirm: turning a teacher off, or issuing a new invite code.
type PendingAction = { kind: "turnOff" | "newInvite"; tenant: Tenant };

// Platform admin: the list of teachers, registering a new one, and turning platforms on or off.
export function TeachersPage() {
  const { t, errorMessage, formatDate } = useI18n();

  const [tenants, setTenants] = useState<Tenant[] | null>(null);
  const [loadError, setLoadError] = useState<unknown>(null);
  const [showCreate, setShowCreate] = useState(false);
  const [pendingAction, setPendingAction] = useState<PendingAction | null>(null);
  const [actionBusy, setActionBusy] = useState(false);
  const [actionError, setActionError] = useState<string | null>(null);
  // The invite code to show once. "none" means the teacher needed no invite.
  const [inviteToShow, setInviteToShow] = useState<Invite | "none" | null>(null);
  const [toast, setToast] = useState<string | null>(null);

  const load = useCallback(() => {
    setLoadError(null);
    api.listTenants().then(setTenants).catch(setLoadError);
  }, []);

  useEffect(load, [load]);

  const clearToast = useCallback(() => setToast(null), []);

  async function changeStatus(tenant: Tenant, isActive: boolean) {
    setActionBusy(true);
    setActionError(null);
    try {
      await api.setTenantStatus(tenant.id, isActive);
      setToast(t("teachers.statusChanged"));
      setPendingAction(null);
      load();
    } catch (error) {
      setActionError(errorMessage(error));
      setPendingAction(null);
    } finally {
      setActionBusy(false);
    }
  }

  async function issueNewInvite(tenant: Tenant) {
    setActionBusy(true);
    setActionError(null);
    try {
      const invite = await api.resetTeacherInvite(tenant.id);
      setInviteToShow(invite);
    } catch (error) {
      setActionError(errorMessage(error));
    } finally {
      setPendingAction(null);
      setActionBusy(false);
    }
  }

  function handleCreated(invite: Invite | null) {
    setShowCreate(false);
    setToast(t("teachers.created"));
    setInviteToShow(invite ?? "none");
    load();
  }

  return (
    <AppLayout navItems={adminNavItems}>
      <PageHeader
        title={t("teachers.title")}
        subtitle={t("teachers.subtitle")}
        action={<Button onClick={() => setShowCreate(true)}>{t("teachers.add")}</Button>}
      />

      {actionError && <Alert>{actionError}</Alert>}

      <section className="card">
        {loadError !== null && <ErrorState message={errorMessage(loadError)} onRetry={load} />}
        {loadError === null && tenants === null && <SkeletonRows />}
        {tenants !== null && tenants.length === 0 && (
          <EmptyState
            message={t("teachers.empty")}
            action={<Button onClick={() => setShowCreate(true)}>{t("teachers.add")}</Button>}
          />
        )}
        {tenants !== null && tenants.length > 0 && (
          <div className="table-wrap">
            <table className="table">
              <thead>
                <tr>
                  <th>{t("teachers.name")}</th>
                  <th>{t("teachers.link")}</th>
                  <th>{t("teachers.teacher")}</th>
                  <th>{t("teachers.phone")}</th>
                  <th>{t("teachers.status")}</th>
                  <th>{t("common.actions")}</th>
                </tr>
              </thead>
              <tbody>
                {tenants.map((tenant) => (
                  <tr key={tenant.id}>
                    <td>
                      <strong>{tenant.displayName}</strong>
                      <div className="muted small">{formatDate(tenant.createdAt)}</div>
                    </td>
                    <td dir="ltr">
                      <code>{tenant.slug}</code>
                    </td>
                    <td>{tenant.teacherFullName ?? "—"}</td>
                    <td dir="ltr">{tenant.teacherPhoneNumber ?? "—"}</td>
                    <td>
                      <span className={tenant.isActive ? "badge badge-success" : "badge badge-muted"}>
                        {tenant.isActive ? t("common.active") : t("common.inactive")}
                      </span>
                    </td>
                    <td>
                      <div className="row-actions">
                        {tenant.isActive ? (
                          <Button variant="ghost" onClick={() => setPendingAction({ kind: "turnOff", tenant })}>
                            {t("teachers.turnOff")}
                          </Button>
                        ) : (
                          <Button variant="ghost" busy={actionBusy} onClick={() => changeStatus(tenant, true)}>
                            {t("teachers.turnOn")}
                          </Button>
                        )}
                        <Button variant="ghost" onClick={() => setPendingAction({ kind: "newInvite", tenant })}>
                          {t("teachers.newInvite")}
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

      {showCreate && <CreateTeacherDialog onClose={() => setShowCreate(false)} onCreated={handleCreated} />}

      {pendingAction?.kind === "turnOff" && (
        <ConfirmDialog
          title={t("teachers.confirmOffTitle")}
          body={t("teachers.confirmOffBody", { name: pendingAction.tenant.displayName })}
          confirmLabel={t("teachers.turnOff")}
          danger
          busy={actionBusy}
          onConfirm={() => changeStatus(pendingAction.tenant, false)}
          onCancel={() => setPendingAction(null)}
        />
      )}

      {pendingAction?.kind === "newInvite" && (
        <ConfirmDialog
          title={t("teachers.confirmInviteTitle")}
          body={t("teachers.confirmInviteBody", { name: pendingAction.tenant.displayName })}
          confirmLabel={t("teachers.newInvite")}
          busy={actionBusy}
          onConfirm={() => issueNewInvite(pendingAction.tenant)}
          onCancel={() => setPendingAction(null)}
        />
      )}

      {inviteToShow !== null && (
        <InviteCodeDialog
          invite={inviteToShow === "none" ? null : inviteToShow}
          body={t("teachers.inviteBody")}
          noInviteText={t("teachers.noInviteNeeded")}
          onClose={() => setInviteToShow(null)}
        />
      )}

      <Toast message={toast} onDone={clearToast} />
    </AppLayout>
  );
}

// The form to register a teacher.
function CreateTeacherDialog({ onClose, onCreated }: { onClose: () => void; onCreated: (invite: Invite | null) => void }) {
  const { t, errorMessage } = useI18n();

  const [displayName, setDisplayName] = useState("");
  const [slug, setSlug] = useState("");
  const [teacherName, setTeacherName] = useState("");
  const [teacherPhone, setTeacherPhone] = useState("");
  const [slugError, setSlugError] = useState<string | null>(null);
  const [phoneError, setPhoneError] = useState<string | null>(null);
  const [formError, setFormError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    setFormError(null);

    const slugIsValid = isValidSlug(slug);
    const phoneIsValid = isEgyptianMobile(teacherPhone);
    setSlugError(slugIsValid ? null : t("validation.slug"));
    setPhoneError(phoneIsValid ? null : t("validation.phone"));
    if (!slugIsValid || !phoneIsValid) {
      return;
    }

    setBusy(true);
    try {
      const response = await api.createTenant({
        slug,
        displayName: displayName.trim(),
        teacherFullName: teacherName.trim(),
        teacherPhoneNumber: teacherPhone,
      });
      onCreated(response.invite);
    } catch (error) {
      setFormError(errorMessage(error));
    } finally {
      setBusy(false);
    }
  }

  const canSubmit = displayName.trim().length >= 2 && teacherName.trim().length >= 2 && slug && teacherPhone;

  return (
    <Dialog title={t("teachers.add")} onClose={onClose}>
      <form onSubmit={handleSubmit} noValidate>
        {formError && <Alert>{formError}</Alert>}

        <Field
          label={t("teachers.form.displayName")}
          value={displayName}
          onChange={(event) => setDisplayName(event.target.value)}
          maxLength={150}
          autoFocus
          required
        />
        <Field
          label={t("teachers.form.slug")}
          hint={t("teachers.form.slugHint")}
          dir="ltr"
          value={slug}
          onChange={(event) => setSlug(event.target.value.toLowerCase())}
          error={slugError}
          maxLength={50}
          required
        />
        <Field
          label={t("teachers.form.teacherName")}
          value={teacherName}
          onChange={(event) => setTeacherName(event.target.value)}
          maxLength={150}
          required
        />
        <Field
          label={t("teachers.form.teacherPhone")}
          type="tel"
          inputMode="numeric"
          dir="ltr"
          placeholder={t("login.phonePlaceholder")}
          value={teacherPhone}
          onChange={(event) => setTeacherPhone(event.target.value)}
          error={phoneError}
          required
        />

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
