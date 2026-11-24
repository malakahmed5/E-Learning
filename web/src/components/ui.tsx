// Small building blocks used by every page, so all screens look and behave the same.

import { useEffect, useId, type ButtonHTMLAttributes, type InputHTMLAttributes, type ReactNode } from "react";
import { useI18n } from "../i18n";

// ---------- Button ----------

interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: "primary" | "secondary" | "danger" | "ghost";
  // Shows a spinner and blocks clicks while work is in progress (prevents double submits).
  busy?: boolean;
  block?: boolean;
}

export function Button({ variant = "primary", busy = false, block = false, children, disabled, ...rest }: ButtonProps) {
  const className = ["btn", `btn-${variant}`, block ? "btn-block" : ""].join(" ").trim();
  return (
    <button className={className} disabled={disabled || busy} aria-busy={busy} {...rest}>
      {busy && <span className="spinner" aria-hidden="true" />}
      <span>{children}</span>
    </button>
  );
}

// ---------- Text field ----------

interface FieldProps extends InputHTMLAttributes<HTMLInputElement> {
  label: string;
  hint?: string;
  error?: string | null;
}

export function Field({ label, hint, error, ...inputProps }: FieldProps) {
  const id = useId();
  const messageId = `${id}-message`;
  return (
    <div className="field">
      <label htmlFor={id}>{label}</label>
      <input
        id={id}
        className={error ? "input input-error" : "input"}
        aria-invalid={error ? true : undefined}
        aria-describedby={error || hint ? messageId : undefined}
        {...inputProps}
      />
      {error ? (
        <p id={messageId} className="field-error" role="alert">
          {error}
        </p>
      ) : hint ? (
        <p id={messageId} className="field-hint">
          {hint}
        </p>
      ) : null}
    </div>
  );
}

// ---------- Messages ----------

export function Alert({ kind = "error", children }: { kind?: "error" | "success" | "info"; children: ReactNode }) {
  return (
    <div className={`alert alert-${kind}`} role={kind === "error" ? "alert" : "status"}>
      {children}
    </div>
  );
}

// A short message that appears at the bottom and disappears by itself.
export function Toast({ message, onDone }: { message: string | null; onDone: () => void }) {
  useEffect(() => {
    if (!message) {
      return;
    }
    const timer = window.setTimeout(onDone, 3500);
    return () => window.clearTimeout(timer);
  }, [message, onDone]);

  if (!message) {
    return null;
  }
  return (
    <div className="toast" role="status">
      {message}
    </div>
  );
}

// ---------- States of a page ----------

export function LoadingScreen() {
  const { t } = useI18n();
  return (
    <div className="center-screen" role="status">
      <span className="spinner spinner-large" aria-hidden="true" />
      <p>{t("common.loading")}</p>
    </div>
  );
}

// Grey placeholder rows shown while a table is loading.
export function SkeletonRows({ rows = 4 }: { rows?: number }) {
  return (
    <div className="skeleton-list" aria-hidden="true">
      {Array.from({ length: rows }, (_, index) => (
        <div key={index} className="skeleton-row" />
      ))}
    </div>
  );
}

export function EmptyState({ message, action }: { message: string; action?: ReactNode }) {
  return (
    <div className="empty-state">
      <div className="empty-icon" aria-hidden="true">
        ○
      </div>
      <p>{message}</p>
      {action}
    </div>
  );
}

export function ErrorState({ message, onRetry }: { message: string; onRetry: () => void }) {
  const { t } = useI18n();
  return (
    <div className="empty-state">
      <Alert>{message}</Alert>
      <Button variant="secondary" onClick={onRetry}>
        {t("common.retry")}
      </Button>
    </div>
  );
}

// ---------- Dialog ----------

interface DialogProps {
  title: string;
  onClose: () => void;
  children: ReactNode;
  footer?: ReactNode;
}

export function Dialog({ title, onClose, children, footer }: DialogProps) {
  const titleId = useId();

  // Close with the Escape key, like users expect from a dialog.
  useEffect(() => {
    function onKeyDown(event: KeyboardEvent) {
      if (event.key === "Escape") {
        onClose();
      }
    }
    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, [onClose]);

  return (
    <div className="dialog-backdrop" onMouseDown={onClose}>
      <div
        className="dialog"
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        onMouseDown={(event) => event.stopPropagation()}
      >
        <h2 id={titleId}>{title}</h2>
        <div className="dialog-body">{children}</div>
        {footer && <div className="dialog-footer">{footer}</div>}
      </div>
    </div>
  );
}

interface ConfirmDialogProps {
  title: string;
  body: string;
  confirmLabel: string;
  danger?: boolean;
  busy?: boolean;
  onConfirm: () => void;
  onCancel: () => void;
}

// Asks "are you sure?" before an action that affects other people.
export function ConfirmDialog({ title, body, confirmLabel, danger, busy, onConfirm, onCancel }: ConfirmDialogProps) {
  const { t } = useI18n();
  return (
    <Dialog
      title={title}
      onClose={onCancel}
      footer={
        <>
          <Button variant="ghost" onClick={onCancel} disabled={busy}>
            {t("common.cancel")}
          </Button>
          <Button variant={danger ? "danger" : "primary"} onClick={onConfirm} busy={busy}>
            {confirmLabel}
          </Button>
        </>
      }
    >
      <p>{body}</p>
    </Dialog>
  );
}

// ---------- Language switch ----------

export function LanguageSwitch() {
  const { language, setLanguage, t } = useI18n();
  return (
    <button
      type="button"
      className="btn btn-ghost btn-small"
      onClick={() => setLanguage(language === "ar" ? "en" : "ar")}
    >
      {t("app.language")}
    </button>
  );
}
