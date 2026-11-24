import { useState, type FormEvent } from "react";
import { Link, useSearchParams } from "react-router-dom";
import * as api from "../api/endpoints";
import type { RedeemResult } from "../api/types";
import { AuthLayout } from "../components/layouts";
import { Alert, Button, Field } from "../components/ui";
import { useI18n } from "../i18n";
import type { TranslationKey } from "../i18n/ar";
import { isEgyptianMobile, isStrongPassword } from "../validation";

// A new student opens the account for the first time:
// phone number + the code they received + a password they choose.
// The teacher's link can carry the teacher's name: /activate?teacher=mr-ahmed
// (needed only when the student was added by more than one teacher).
export function ActivatePage() {
  const { t, errorMessage, formatDate } = useI18n();
  const [searchParams] = useSearchParams();
  const tenantSlug = searchParams.get("teacher");

  const [phone, setPhone] = useState("");
  const [code, setCode] = useState("");
  const [password, setPassword] = useState("");
  const [phoneError, setPhoneError] = useState<string | null>(null);
  const [passwordError, setPasswordError] = useState<string | null>(null);
  const [formError, setFormError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [result, setResult] = useState<RedeemResult | null>(null);

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    setFormError(null);

    const phoneIsValid = isEgyptianMobile(phone);
    const passwordIsValid = isStrongPassword(password);
    setPhoneError(phoneIsValid ? null : t("validation.phone"));
    setPasswordError(passwordIsValid ? null : t("validation.password"));
    if (!phoneIsValid || !passwordIsValid) {
      return;
    }

    setBusy(true);
    try {
      setResult(await api.activateAccount({ phoneNumber: phone, code, password, tenantSlug }));
    } catch (error) {
      setFormError(errorMessage(error));
    } finally {
      setBusy(false);
    }
  }

  if (result) {
    return (
      <AuthLayout title={t("activate.successTitle")}>
        <Alert kind="success">
          {t("subscription.result", {
            type: t(`codes.type.${result.type}` as TranslationKey),
            start: formatDate(result.startsAt),
            end: formatDate(result.endsAt),
          })}
        </Alert>
        <Link className="btn btn-primary btn-block" to="/login">
          {t("invite.backToLogin")}
        </Link>
      </AuthLayout>
    );
  }

  return (
    <AuthLayout title={t("activate.title")} subtitle={t("activate.subtitle")}>
      <form onSubmit={handleSubmit} noValidate>
        {formError && <Alert>{formError}</Alert>}

        <Field
          label={t("login.phone")}
          type="tel"
          inputMode="numeric"
          dir="ltr"
          placeholder={t("login.phonePlaceholder")}
          value={phone}
          onChange={(event) => setPhone(event.target.value)}
          error={phoneError}
          required
        />
        <Field
          label={t("activate.code")}
          dir="ltr"
          autoComplete="off"
          placeholder={t("invite.codePlaceholder")}
          value={code}
          onChange={(event) => setCode(event.target.value.toUpperCase())}
          required
        />
        <Field
          label={t("invite.newPassword")}
          type="password"
          autoComplete="new-password"
          dir="ltr"
          hint={t("invite.passwordHint")}
          value={password}
          onChange={(event) => setPassword(event.target.value)}
          error={passwordError}
          required
        />

        <Button type="submit" block busy={busy} disabled={!phone || !code || !password}>
          {t("activate.submit")}
        </Button>
      </form>

      <p className="auth-footer">
        <Link to="/login">{t("invite.backToLogin")}</Link>
      </p>
    </AuthLayout>
  );
}
