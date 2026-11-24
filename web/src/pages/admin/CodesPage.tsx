import { useCallback, useEffect, useState, type FormEvent } from "react";
import * as api from "../../api/endpoints";
import type { CodeItem, CodeKind, CodeState, GeneratedBatch } from "../../api/types";
import { AppLayout, PageHeader } from "../../components/layouts";
import { Pagination, SelectField, useLoad } from "../../components/shared";
import { Alert, Button, ConfirmDialog, Dialog, EmptyState, ErrorState, Field, SkeletonRows, Toast } from "../../components/ui";
import { useI18n } from "../../i18n";
import type { TranslationKey } from "../../i18n/ar";
import { adminNavItems } from "./adminNav";

const PAGE_SIZE = 20;
const CODE_KINDS: CodeKind[] = ["FreeWeek", "Subscription30"];
const CODE_STATES: CodeState[] = ["Available", "Redeemed", "Expired", "Revoked"];

// The colour of the status label for each state.
const STATE_BADGE: Record<CodeState, string> = {
  Available: "badge badge-success",
  Redeemed: "badge badge-info",
  Expired: "badge badge-warning",
  Revoked: "badge badge-muted",
};

// Platform owner: generate subscription codes, follow their use, and revoke unused ones.
export function CodesPage() {
  const { t, errorMessage, formatDate } = useI18n();

  const [type, setType] = useState<CodeKind | null>(null);
  const [status, setStatus] = useState<CodeState | null>(null);
  // What the owner is typing, and the value we search for (set a moment after typing stops).
  const [searchText, setSearchText] = useState("");
  const [search, setSearch] = useState("");
  const [pageIndex, setPageIndex] = useState(1);

  useEffect(() => {
    const timer = window.setTimeout(() => {
      setSearch(searchText.trim());
      setPageIndex(1);
    }, 350);
    return () => window.clearTimeout(timer);
  }, [searchText]);

  const loadCodes = useCallback(
    () => api.listCodes({ type, status, search, pageIndex, pageSize: PAGE_SIZE }),
    [type, status, search, pageIndex],
  );
  const codes = useLoad(loadCodes);
  const stats = useLoad(api.getCodeStats);

  const [showGenerate, setShowGenerate] = useState(false);
  const [generated, setGenerated] = useState<GeneratedBatch | null>(null);
  const [revoking, setRevoking] = useState<CodeItem | null>(null);
  const [busy, setBusy] = useState(false);
  const [actionError, setActionError] = useState<string | null>(null);
  const [toast, setToast] = useState<string | null>(null);
  const clearToast = useCallback(() => setToast(null), []);

  const hasFilters = type !== null || status !== null || search !== "";

  function reloadAll() {
    codes.reload();
    stats.reload();
  }

  function handleGenerated(batch: GeneratedBatch) {
    setShowGenerate(false);
    setGenerated(batch);
    reloadAll();
  }

  async function handleRevoke(code: CodeItem) {
    setBusy(true);
    setActionError(null);
    try {
      await api.revokeCode(code.id);
      setToast(t("codes.revoked"));
      reloadAll();
    } catch (error) {
      setActionError(errorMessage(error));
    } finally {
      setRevoking(null);
      setBusy(false);
    }
  }

  async function copyCode(code: string) {
    await navigator.clipboard.writeText(code);
    setToast(t("common.copied"));
  }

  return (
    <AppLayout navItems={adminNavItems}>
      <PageHeader
        title={t("codes.title")}
        subtitle={t("codes.subtitle")}
        action={<Button onClick={() => setShowGenerate(true)}>{t("codes.generate")}</Button>}
      />

      {actionError && <Alert>{actionError}</Alert>}

      {/* Numbers for each code type. */}
      <div className="stat-grid">
        {(stats.data ?? []).map((item) => (
          <section className="card stat-card" key={item.type} data-testid={`stats-${item.type}`}>
            <h2>{t(`codes.type.${item.type}` as TranslationKey)}</h2>
            <dl className="stat-list">
              <div>
                <dt>{t("codes.total")}</dt>
                <dd>{item.total}</dd>
              </div>
              <div>
                <dt>{t("codes.status.Available")}</dt>
                <dd>{item.available}</dd>
              </div>
              <div>
                <dt>{t("codes.status.Redeemed")}</dt>
                <dd>{item.redeemed}</dd>
              </div>
              <div>
                <dt>{t("codes.status.Expired")}</dt>
                <dd>{item.expired}</dd>
              </div>
              <div>
                <dt>{t("codes.status.Revoked")}</dt>
                <dd>{item.revoked}</dd>
              </div>
            </dl>
          </section>
        ))}
      </div>

      <section className="card">
        <div className="toolbar">
          <input
            className="input"
            type="search"
            aria-label={t("common.search")}
            placeholder={t("codes.searchPlaceholder")}
            value={searchText}
            onChange={(event) => setSearchText(event.target.value)}
          />
          <select
            className="input"
            aria-label={t("codes.type")}
            value={type ?? ""}
            onChange={(event) => {
              setType(event.target.value === "" ? null : (event.target.value as CodeKind));
              setPageIndex(1);
            }}
          >
            <option value="">{t("codes.allTypes")}</option>
            {CODE_KINDS.map((kind) => (
              <option key={kind} value={kind}>
                {t(`codes.type.${kind}` as TranslationKey)}
              </option>
            ))}
          </select>
          <select
            className="input"
            aria-label={t("codes.status")}
            value={status ?? ""}
            onChange={(event) => {
              setStatus(event.target.value === "" ? null : (event.target.value as CodeState));
              setPageIndex(1);
            }}
          >
            <option value="">{t("codes.allStatuses")}</option>
            {CODE_STATES.map((state) => (
              <option key={state} value={state}>
                {t(`codes.status.${state}` as TranslationKey)}
              </option>
            ))}
          </select>
        </div>

        {codes.error !== null && <ErrorState message={errorMessage(codes.error)} onRetry={codes.reload} />}
        {codes.error === null && codes.data === null && <SkeletonRows />}
        {codes.data !== null && codes.data.count === 0 && (
          <EmptyState message={hasFilters ? t("codes.emptySearch") : t("codes.empty")} />
        )}
        {codes.data !== null && codes.data.count > 0 && (
          <>
            <div className="table-wrap">
              <table className="table">
                <thead>
                  <tr>
                    <th>{t("codes.code")}</th>
                    <th>{t("codes.type")}</th>
                    <th>{t("codes.status")}</th>
                    <th>{t("codes.validUntil")}</th>
                    <th>{t("codes.redeemedBy")}</th>
                    <th>{t("common.actions")}</th>
                  </tr>
                </thead>
                <tbody>
                  {codes.data.data.map((code) => (
                    <tr key={code.id}>
                      <td dir="ltr">
                        <code>{code.code}</code>
                        {code.batchNote && <div className="muted small">{code.batchNote}</div>}
                      </td>
                      <td>{t(`codes.type.${code.type}` as TranslationKey)}</td>
                      <td>
                        <span className={STATE_BADGE[code.status]}>{t(`codes.status.${code.status}` as TranslationKey)}</span>
                      </td>
                      <td>{code.redeemBefore ? formatDate(code.redeemBefore) : t("codes.noLimit")}</td>
                      <td>
                        {code.redeemedByName ? (
                          <>
                            {code.redeemedByName}
                            <div className="muted small">
                              <span dir="ltr">{code.redeemedByPhone}</span> · {code.redeemedAtTeacher}
                              {code.redeemedAt && <> · {formatDate(code.redeemedAt)}</>}
                            </div>
                          </>
                        ) : (
                          "—"
                        )}
                      </td>
                      <td>
                        <div className="row-actions">
                          <Button variant="ghost" onClick={() => copyCode(code.code)}>
                            {t("codes.copyOne")}
                          </Button>
                          {/* Only a code nobody used yet can be revoked. */}
                          {code.status === "Available" && (
                            <Button variant="ghost" onClick={() => setRevoking(code)}>
                              {t("codes.revoke")}
                            </Button>
                          )}
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <Pagination pageIndex={pageIndex} pageSize={PAGE_SIZE} totalCount={codes.data.count} onChange={setPageIndex} />
          </>
        )}
      </section>

      {showGenerate && <GenerateDialog onClose={() => setShowGenerate(false)} onGenerated={handleGenerated} />}
      {generated !== null && <GeneratedDialog batch={generated} onClose={() => setGenerated(null)} />}

      {revoking !== null && (
        <ConfirmDialog
          title={t("codes.confirmRevokeTitle")}
          body={t("codes.confirmRevokeBody", { code: revoking.code })}
          confirmLabel={t("codes.revoke")}
          danger
          busy={busy}
          onConfirm={() => handleRevoke(revoking)}
          onCancel={() => setRevoking(null)}
        />
      )}

      <Toast message={toast} onDone={clearToast} />
    </AppLayout>
  );
}

function GenerateDialog({ onClose, onGenerated }: { onClose: () => void; onGenerated: (batch: GeneratedBatch) => void }) {
  const { t, errorMessage } = useI18n();

  const [type, setType] = useState<CodeKind>("FreeWeek");
  const [count, setCount] = useState("1");
  const [redeemBefore, setRedeemBefore] = useState("");
  const [note, setNote] = useState("");
  const [countError, setCountError] = useState<string | null>(null);
  const [formError, setFormError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    setFormError(null);

    const wantedCount = Number(count);
    if (!Number.isInteger(wantedCount) || wantedCount < 1 || wantedCount > 2000) {
      setCountError(t("validation.count"));
      return;
    }
    setCountError(null);

    setBusy(true);
    try {
      const batch = await api.generateCodes({
        type,
        count: wantedCount,
        // The date field gives "2027-09-30". The codes stay valid until the end of that day.
        redeemBefore: redeemBefore ? new Date(`${redeemBefore}T23:59:59`).toISOString() : null,
        note: note.trim() || null,
      });
      onGenerated(batch);
    } catch (error) {
      setFormError(errorMessage(error));
    } finally {
      setBusy(false);
    }
  }

  return (
    <Dialog title={t("codes.generate")} onClose={onClose}>
      <form onSubmit={handleSubmit} noValidate>
        {formError && <Alert>{formError}</Alert>}

        <SelectField label={t("codes.type")} value={type} onChange={(event) => setType(event.target.value as CodeKind)}>
          {CODE_KINDS.map((kind) => (
            <option key={kind} value={kind}>
              {t(`codes.type.${kind}` as TranslationKey)}
            </option>
          ))}
        </SelectField>

        <Field
          label={t("codes.count")}
          type="number"
          inputMode="numeric"
          min={1}
          max={2000}
          dir="ltr"
          value={count}
          onChange={(event) => setCount(event.target.value)}
          error={countError}
          required
        />
        <Field
          label={t("codes.redeemBefore")}
          hint={t("codes.redeemBeforeHint")}
          type="date"
          value={redeemBefore}
          onChange={(event) => setRedeemBefore(event.target.value)}
        />
        <Field label={t("codes.note")} value={note} onChange={(event) => setNote(event.target.value)} maxLength={200} />

        <div className="dialog-footer">
          <Button type="button" variant="ghost" onClick={onClose} disabled={busy}>
            {t("common.cancel")}
          </Button>
          <Button type="submit" busy={busy} disabled={count === ""}>
            {t("codes.generate")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}

// Shows the new codes right after generating, with "copy all" and "download".
function GeneratedDialog({ batch, onClose }: { batch: GeneratedBatch; onClose: () => void }) {
  const { t } = useI18n();
  const [copied, setCopied] = useState(false);

  async function copyAll() {
    await navigator.clipboard.writeText(batch.codes.join("\n"));
    setCopied(true);
  }

  // Builds a small text file in the browser (one code per line) and downloads it.
  function download() {
    const file = new Blob([batch.codes.join("\r\n")], { type: "text/plain" });
    const link = document.createElement("a");
    link.href = URL.createObjectURL(file);
    link.download = `codes-${batch.type}-${batch.batchId}.txt`;
    link.click();
    URL.revokeObjectURL(link.href);
  }

  return (
    <Dialog
      title={t("codes.generatedTitle", { count: String(batch.codes.length) })}
      onClose={onClose}
      footer={<Button onClick={onClose}>{t("common.close")}</Button>}
    >
      <p>{t("codes.generatedBody")}</p>
      <ul className="code-list" dir="ltr">
        {batch.codes.map((code) => (
          <li key={code}>{code}</li>
        ))}
      </ul>
      <div className="row-actions">
        <Button variant="secondary" onClick={copyAll}>
          {copied ? t("common.copied") : t("codes.copyAll")}
        </Button>
        <Button variant="secondary" onClick={download}>
          {t("codes.download")}
        </Button>
      </div>
    </Dialog>
  );
}
