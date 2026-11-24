// Larger shared pieces used by several pages: loading data, form controls, paging and the invite code dialog.

import { useCallback, useEffect, useId, useState, type ReactNode, type SelectHTMLAttributes } from "react";
import type { Invite } from "../api/types";
import { useI18n } from "../i18n";
import { Button, Dialog } from "./ui";

// ---------- Loading data for a page ----------

interface Loaded<T> {
  // null while loading (or after an error).
  data: T | null;
  // The thrown error, or null.
  error: unknown;
  reload: () => void;
}

// Calls "loader" when the page opens and whenever "loader" changes.
// Wrap the loader in useCallback so it only changes when its inputs change.
export function useLoad<T>(loader: () => Promise<T>): Loaded<T> {
  const [data, setData] = useState<T | null>(null);
  const [error, setError] = useState<unknown>(null);
  const [version, setVersion] = useState(0);

  useEffect(() => {
    // If the inputs change while a request is running, the older answer must be ignored.
    let isLatest = true;
    setError(null);

    loader()
      .then((result) => {
        if (isLatest) {
          setData(result);
        }
      })
      .catch((loadError) => {
        if (isLatest) {
          setData(null);
          setError(loadError);
        }
      });

    return () => {
      isLatest = false;
    };
  }, [loader, version]);

  const reload = useCallback(() => setVersion((current) => current + 1), []);
  return { data, error, reload };
}

// ---------- Form controls ----------

interface SelectFieldProps extends SelectHTMLAttributes<HTMLSelectElement> {
  label: string;
  children: ReactNode;
}

export function SelectField({ label, children, ...selectProps }: SelectFieldProps) {
  const id = useId();
  return (
    <div className="field">
      <label htmlFor={id}>{label}</label>
      <select id={id} className="input" {...selectProps}>
        {children}
      </select>
    </div>
  );
}

export function CheckboxField({ label, checked, onChange }: { label: string; checked: boolean; onChange: (checked: boolean) => void }) {
  const id = useId();
  return (
    <div className="checkbox-field">
      <input id={id} type="checkbox" checked={checked} onChange={(event) => onChange(event.target.checked)} />
      <label htmlFor={id}>{label}</label>
    </div>
  );
}

// ---------- Paging ----------

export function Pagination({ pageIndex, pageSize, totalCount, onChange }: {
  pageIndex: number;
  pageSize: number;
  totalCount: number;
  onChange: (pageIndex: number) => void;
}) {
  const { t } = useI18n();
  const pageCount = Math.max(1, Math.ceil(totalCount / pageSize));

  if (pageCount === 1) {
    return null;
  }

  return (
    <div className="pagination">
      <Button variant="secondary" disabled={pageIndex <= 1} onClick={() => onChange(pageIndex - 1)}>
        {t("common.previous")}
      </Button>
      <span className="muted">{t("common.pageOf", { page: String(pageIndex), pages: String(pageCount) })}</span>
      <Button variant="secondary" disabled={pageIndex >= pageCount} onClick={() => onChange(pageIndex + 1)}>
        {t("common.next")}
      </Button>
    </div>
  );
}

// ---------- Invite code ----------

// Shows a one-time invite code. After this dialog closes, the code cannot be read again.
// Pass invite = null when the person already had a password and needs no code.
export function InviteCodeDialog({ invite, body, noInviteText, onClose }: {
  invite: Invite | null;
  body: string;
  noInviteText: string;
  onClose: () => void;
}) {
  const { t, formatDate } = useI18n();
  const [copied, setCopied] = useState(false);

  async function copyCode(code: string) {
    await navigator.clipboard.writeText(code);
    setCopied(true);
  }

  return (
    <Dialog title={t("teachers.inviteTitle")} onClose={onClose} footer={<Button onClick={onClose}>{t("common.close")}</Button>}>
      {invite === null ? (
        <p>{noInviteText}</p>
      ) : (
        <>
          <p>{body}</p>
          <div className="invite-code" dir="ltr">
            {invite.code}
          </div>
          <p className="muted small">{t("teachers.inviteExpires", { date: formatDate(invite.expiresAt) })}</p>
          <Button variant="secondary" onClick={() => copyCode(invite.code)}>
            {copied ? t("common.copied") : t("common.copy")}
          </Button>
        </>
      )}
    </Dialog>
  );
}
