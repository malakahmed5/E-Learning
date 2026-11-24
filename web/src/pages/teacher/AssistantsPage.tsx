import { useCallback, useState, type FormEvent } from "react";
import * as api from "../../api/endpoints";
import type { Assistant, Invite } from "../../api/types";
import { useAuth } from "../../auth/AuthContext";
import { AppLayout, PageHeader } from "../../components/layouts";
import { InviteCodeDialog, useLoad } from "../../components/shared";
import { Alert, Button, ConfirmDialog, Dialog, EmptyState, ErrorState, Field, SkeletonRows, Toast } from "../../components/ui";
import { useI18n } from "../../i18n";
import { isEgyptianMobile } from "../../validation";
import { teacherNavItems } from "./teacherNav";

// Teacher only: add assistants and suspend or restore them.
export function AssistantsPage() {
  const { t, errorMessage } = useI18n();
  const { session } = useAuth();

  const assistants = useLoad(api.listAssistants);
  const [showCreate, setShowCreate] = useState(false);
  const [suspending, setSuspending] = useState<Assistant | null>(null);
  // After adding: the invite to show once. "none" means the person already had a password.
  const [inviteToShow, setInviteToShow] = useState<Invite | "none" | null>(null);
  const [busy, setBusy] = useState(false);
  const [actionError, setActionError] = useState<string | null>(null);
  const [toast, setToast] = useState<string | null>(null);
  const clearToast = useCallback(() => setToast(null), []);

  if (!session?.activeMembership) {
    return null;
  }

  async function changeStatus(assistant: Assistant, isActive: boolean) {
    setBusy(true);
    setActionError(null);
    try {
      await api.setAssistantActive(assistant.id, isActive);
      setToast(t("assistants.statusChanged"));
      assistants.reload();
    } catch (error) {
      setActionError(errorMessage(error));
    } finally {
      setSuspending(null);
      setBusy(false);
    }
  }

  function handleCreated(invite: Invite | null) {
    setShowCreate(false);
    setToast(t("assistants.created"));
    setInviteToShow(invite ?? "none");
    assistants.reload();
  }

  return (
    <AppLayout navItems={teacherNavItems(session.activeMembership.role)}>
      <PageHeader
        title={t("assistants.title")}
        subtitle={t("assistants.subtitle")}
        action={<Button onClick={() => setShowCreate(true)}>{t("assistants.add")}</Button>}
      />

      {actionError && <Alert>{actionError}</Alert>}

      <section className="card">
        {assistants.error !== null && <ErrorState message={errorMessage(assistants.error)} onRetry={assistants.reload} />}
        {assistants.error === null && assistants.data === null && <SkeletonRows />}
        {assistants.data !== null && assistants.data.length === 0 && <EmptyState message={t("assistants.empty")} />}
        {assistants.data !== null && assistants.data.length > 0 && (
          <div className="table-wrap">
            <table className="table">
              <thead>
                <tr>
                  <th>{t("assistants.name")}</th>
                  <th>{t("assistants.phone")}</th>
                  <th>{t("teachers.status")}</th>
                  <th>{t("common.actions")}</th>
                </tr>
              </thead>
              <tbody>
                {assistants.data.map((assistant) => (
                  <tr key={assistant.id}>
                    <td>
                      <strong>{assistant.fullName}</strong>
                    </td>
                    <td dir="ltr">{assistant.phoneNumber}</td>
                    <td>
                      <span className={assistant.isActive ? "badge badge-success" : "badge badge-muted"}>
                        {assistant.isActive ? t("common.active") : t("common.suspended")}
                      </span>
                    </td>
                    <td>
                      <div className="row-actions">
                        {assistant.isActive ? (
                          <Button variant="ghost" onClick={() => setSuspending(assistant)}>
                            {t("common.suspend")}
                          </Button>
                        ) : (
                          <Button variant="ghost" disabled={busy} onClick={() => changeStatus(assistant, true)}>
                            {t("common.restore")}
                          </Button>
                        )}
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>

      {showCreate && <CreateAssistantDialog onClose={() => setShowCreate(false)} onCreated={handleCreated} />}

      {suspending !== null && (
        <ConfirmDialog
          title={t("assistants.confirmSuspendTitle")}
          body={t("assistants.confirmSuspendBody", { name: suspending.fullName })}
          confirmLabel={t("common.suspend")}
          danger
          busy={busy}
          onConfirm={() => changeStatus(suspending, false)}
          onCancel={() => setSuspending(null)}
        />
      )}

      {inviteToShow !== null && (
        <InviteCodeDialog
          invite={inviteToShow === "none" ? null : inviteToShow}
          body={t("assistants.inviteBody")}
          noInviteText={t("teachers.noInviteNeeded")}
          onClose={() => setInviteToShow(null)}
        />
      )}

      <Toast message={toast} onDone={clearToast} />
    </AppLayout>
  );
}

function CreateAssistantDialog({ onClose, onCreated }: { onClose: () => void; onCreated: (invite: Invite | null) => void }) {
  const { t, errorMessage } = useI18n();

  const [fullName, setFullName] = useState("");
  const [phoneNumber, setPhoneNumber] = useState("");
  const [phoneError, setPhoneError] = useState<string | null>(null);
  const [formError, setFormError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    setFormError(null);

    if (!isEgyptianMobile(phoneNumber)) {
      setPhoneError(t("validation.phone"));
      return;
    }
    setPhoneError(null);

    setBusy(true);
    try {
      const response = await api.createAssistant({ fullName: fullName.trim(), phoneNumber });
      onCreated(response.invite);
    } catch (error) {
      setFormError(errorMessage(error));
    } finally {
      setBusy(false);
    }
  }

  return (
    <Dialog title={t("assistants.add")} onClose={onClose}>
      <form onSubmit={handleSubmit} noValidate>
        {formError && <Alert>{formError}</Alert>}

        <Field label={t("assistants.name")} value={fullName} onChange={(event) => setFullName(event.target.value)} maxLength={150} autoFocus required />
        <Field
          label={t("assistants.phone")}
          type="tel"
          inputMode="numeric"
          dir="ltr"
          placeholder={t("login.phonePlaceholder")}
          value={phoneNumber}
          onChange={(event) => setPhoneNumber(event.target.value)}
          error={phoneError}
          required
        />

        <div className="dialog-footer">
          <Button type="button" variant="ghost" onClick={onClose} disabled={busy}>
            {t("common.cancel")}
          </Button>
          <Button type="submit" busy={busy} disabled={fullName.trim().length < 2 || !phoneNumber}>
            {t("common.save")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}
