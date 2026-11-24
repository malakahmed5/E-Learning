import { useState, type FormEvent } from "react";
import { Link } from "react-router-dom";
import * as api from "../api/endpoints";
import { AuthLayout } from "../components/layouts";
import { Alert, Button, Field } from "../components/ui";
import { useI18n } from "../i18n";
import { isEgyptianMobile, isStrongPassword } from "../validation";

// A teacher or assistant sets their first password with the one-time invite code.
export function AcceptInvitePage() {
  const { t, errorMessage } = useI18n();

  const [phone, setPhone] = useState("");
  const [inviteCode, setInviteCode] = useState("");
  const [password, setPassword] = useState("");
  const [phoneError, setPhoneError] = useState<string | null>(null);
  const [passwordError, setPasswordError] = useState<string | null>(null);
  const [formError, setFormError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [done, setDone] = useState(false);

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
      await api.acceptInvite(phone, inviteCode, password);
      setDone(true);
    } catch (error) {
      setFormError(errorMessage(error));
    } finally {
      setBusy(false);
    }
  }

  if (done) {
    return (
      <AuthLayout title={t("invite.title")}>
        <Alert kind="success">{t("invite.success")}</Alert>
        <Link className="btn btn-primary btn-block" to="/login">
          {t("invite.backToLogin")}
        </Link>
      </AuthLayout>
    );
  }

  return (
    <AuthLayout title={t("invite.title")} subtitle={t("invite.subtitle")}>
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
          label={t("invite.code")}
          dir="ltr"
          autoComplete="off"
          placeholder={t("invite.codePlaceholder")}
          value={inviteCode}
          onChange={(event) => setInviteCode(event.target.value.toUpperCase())}
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

        <Button type="submit" block busy={busy} disabled={!phone || !inviteCode || !password}>
          {t("invite.submit")}
        </Button>
      </form>

      <p className="auth-footer">
        <Link to="/login">{t("invite.backToLogin")}</Link>
      </p>
    </AuthLayout>
  );
}
