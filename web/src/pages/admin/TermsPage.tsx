import { useCallback, useEffect, useState, type FormEvent } from "react";
import * as api from "../../api/endpoints";
import type { AcademicTerm } from "../../api/types";
import { AppLayout, PageHeader } from "../../components/layouts";
import { Alert, Button, Dialog, EmptyState, ErrorState, Field, SkeletonRows, Toast } from "../../components/ui";
import { useI18n } from "../../i18n";
import { adminNavItems } from "./adminNav";

// Platform admin: the dates of each school term.
export function TermsPage() {
  const { t, errorMessage, formatDate } = useI18n();

  const [terms, setTerms] = useState<AcademicTerm[] | null>(null);
  const [loadError, setLoadError] = useState<unknown>(null);
  const [showCreate, setShowCreate] = useState(false);
  const [toast, setToast] = useState<string | null>(null);

  const load = useCallback(() => {
    setLoadError(null);
    api.listAcademicTerms().then(setTerms).catch(setLoadError);
  }, []);

  useEffect(load, [load]);

  const clearToast = useCallback(() => setToast(null), []);

  function handleCreated() {
    setShowCreate(false);
    setToast(t("terms.created"));
    load();
  }

  return (
    <AppLayout navItems={adminNavItems}>
      <PageHeader
        title={t("terms.title")}
        subtitle={t("terms.subtitle")}
        action={<Button onClick={() => setShowCreate(true)}>{t("terms.add")}</Button>}
      />

      <section className="card">
        {loadError !== null && <ErrorState message={errorMessage(loadError)} onRetry={load} />}
        {loadError === null && terms === null && <SkeletonRows />}
        {terms !== null && terms.length === 0 && <EmptyState message={t("terms.empty")} />}
        {terms !== null && terms.length > 0 && (
          <div className="table-wrap">
            <table className="table">
              <thead>
                <tr>
                  <th>{t("terms.name")}</th>
                  <th>{t("terms.start")}</th>
                  <th>{t("terms.end")}</th>
                </tr>
              </thead>
              <tbody>
                {terms.map((term) => (
                  <tr key={term.id}>
                    <td>
                      <strong>{term.name}</strong>
                    </td>
                    <td>{formatDate(term.startDate)}</td>
                    <td>{formatDate(term.endDate)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>

      {showCreate && <CreateTermDialog onClose={() => setShowCreate(false)} onCreated={handleCreated} />}

      <Toast message={toast} onDone={clearToast} />
    </AppLayout>
  );
}

function CreateTermDialog({ onClose, onCreated }: { onClose: () => void; onCreated: () => void }) {
  const { t, errorMessage } = useI18n();

  const [name, setName] = useState("");
  const [startDate, setStartDate] = useState("");
  const [endDate, setEndDate] = useState("");
  const [datesError, setDatesError] = useState<string | null>(null);
  const [formError, setFormError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    setFormError(null);

    // Dates are in the form 2027-09-18, so comparing them as text gives the right order.
    if (endDate <= startDate) {
      setDatesError(t("validation.dates"));
      return;
    }
    setDatesError(null);

    setBusy(true);
    try {
      await api.createAcademicTerm({ name: name.trim(), startDate, endDate });
      onCreated();
    } catch (error) {
      setFormError(errorMessage(error));
    } finally {
      setBusy(false);
    }
  }

  return (
    <Dialog title={t("terms.add")} onClose={onClose}>
      <form onSubmit={handleSubmit} noValidate>
        {formError && <Alert>{formError}</Alert>}

        <Field
          label={t("terms.name")}
          value={name}
          onChange={(event) => setName(event.target.value)}
          maxLength={100}
          autoFocus
          required
        />
        <Field
          label={t("terms.start")}
          type="date"
          value={startDate}
          onChange={(event) => setStartDate(event.target.value)}
          required
        />
        <Field
          label={t("terms.end")}
          type="date"
          value={endDate}
          onChange={(event) => setEndDate(event.target.value)}
          error={datesError}
          required
        />

        <div className="dialog-footer">
          <Button type="button" variant="ghost" onClick={onClose} disabled={busy}>
            {t("common.cancel")}
          </Button>
          <Button type="submit" busy={busy} disabled={name.trim().length < 2 || !startDate || !endDate}>
            {t("common.save")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}
