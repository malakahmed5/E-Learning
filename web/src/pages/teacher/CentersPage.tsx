import { useCallback, useState, type FormEvent } from "react";
import * as api from "../../api/endpoints";
import type { Center } from "../../api/types";
import { useAuth } from "../../auth/AuthContext";
import { AppLayout, PageHeader } from "../../components/layouts";
import { useLoad } from "../../components/shared";
import { Alert, Button, ConfirmDialog, Dialog, EmptyState, ErrorState, Field, SkeletonRows, Toast } from "../../components/ui";
import { useI18n } from "../../i18n";
import { teacherNavItems } from "./teacherNav";

// The places where the teacher gives lessons.
export function CentersPage() {
  const { t, errorMessage } = useI18n();
  const { session } = useAuth();

  const centers = useLoad(api.listCenters);
  // The center being edited, "new" for the add dialog, or null when no dialog is open.
  const [editing, setEditing] = useState<Center | "new" | null>(null);
  const [deleting, setDeleting] = useState<Center | null>(null);
  const [busy, setBusy] = useState(false);
  const [actionError, setActionError] = useState<string | null>(null);
  const [toast, setToast] = useState<string | null>(null);
  const clearToast = useCallback(() => setToast(null), []);

  if (!session?.activeMembership) {
    return null;
  }

  async function handleDelete(center: Center) {
    setBusy(true);
    setActionError(null);
    try {
      await api.deleteCenter(center.id);
      setToast(t("centers.deleted"));
      centers.reload();
    } catch (error) {
      setActionError(errorMessage(error));
    } finally {
      setDeleting(null);
      setBusy(false);
    }
  }

  function handleSaved() {
    setEditing(null);
    setToast(t("centers.saved"));
    centers.reload();
  }

  return (
    <AppLayout navItems={teacherNavItems(session.activeMembership.role)}>
      <PageHeader
        title={t("centers.title")}
        subtitle={t("centers.subtitle")}
        action={<Button onClick={() => setEditing("new")}>{t("centers.add")}</Button>}
      />

      {actionError && <Alert>{actionError}</Alert>}

      <section className="card">
        {centers.error !== null && <ErrorState message={errorMessage(centers.error)} onRetry={centers.reload} />}
        {centers.error === null && centers.data === null && <SkeletonRows />}
        {centers.data !== null && centers.data.length === 0 && <EmptyState message={t("centers.empty")} />}
        {centers.data !== null && centers.data.length > 0 && (
          <div className="table-wrap">
            <table className="table">
              <thead>
                <tr>
                  <th>{t("centers.name")}</th>
                  <th>{t("centers.address")}</th>
                  <th>{t("centers.groups")}</th>
                  <th>{t("common.actions")}</th>
                </tr>
              </thead>
              <tbody>
                {centers.data.map((center) => (
                  <tr key={center.id}>
                    <td>
                      <strong>{center.name}</strong>
                    </td>
                    <td>{center.address ?? "—"}</td>
                    <td>{center.groupCount}</td>
                    <td>
                      <div className="row-actions">
                        <Button variant="ghost" onClick={() => setEditing(center)}>
                          {t("common.edit")}
                        </Button>
                        <Button variant="ghost" onClick={() => setDeleting(center)}>
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
        <CenterDialog center={editing === "new" ? null : editing} onClose={() => setEditing(null)} onSaved={handleSaved} />
      )}

      {deleting !== null && (
        <ConfirmDialog
          title={t("centers.confirmDeleteTitle")}
          body={t("centers.confirmDeleteBody", { name: deleting.name })}
          confirmLabel={t("common.delete")}
          danger
          busy={busy}
          onConfirm={() => handleDelete(deleting)}
          onCancel={() => setDeleting(null)}
        />
      )}

      <Toast message={toast} onDone={clearToast} />
    </AppLayout>
  );
}

function CenterDialog({ center, onClose, onSaved }: { center: Center | null; onClose: () => void; onSaved: () => void }) {
  const { t, errorMessage } = useI18n();

  const [name, setName] = useState(center?.name ?? "");
  const [address, setAddress] = useState(center?.address ?? "");
  const [formError, setFormError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    setFormError(null);
    setBusy(true);
    try {
      await api.saveCenter(center?.id ?? null, { name: name.trim(), address: address.trim() || null });
      onSaved();
    } catch (error) {
      setFormError(errorMessage(error));
    } finally {
      setBusy(false);
    }
  }

  return (
    <Dialog title={center ? t("centers.edit") : t("centers.add")} onClose={onClose}>
      <form onSubmit={handleSubmit} noValidate>
        {formError && <Alert>{formError}</Alert>}

        <Field label={t("centers.name")} value={name} onChange={(event) => setName(event.target.value)} maxLength={150} autoFocus required />
        <Field label={t("centers.address")} value={address} onChange={(event) => setAddress(event.target.value)} maxLength={300} />

        <div className="dialog-footer">
          <Button type="button" variant="ghost" onClick={onClose} disabled={busy}>
            {t("common.cancel")}
          </Button>
          <Button type="submit" busy={busy} disabled={name.trim().length < 2}>
            {t("common.save")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}
