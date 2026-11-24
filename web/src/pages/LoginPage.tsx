import { useState, type FormEvent } from "react";
import { Link, useNavigate } from "react-router-dom";
import * as api from "../api/endpoints";
import { useAuth } from "../auth/AuthContext";
import { getDeviceId } from "../auth/deviceId";
import { AuthLayout } from "../components/layouts";
import { Alert, Button, Field } from "../components/ui";
import { useI18n } from "../i18n";
import { isEgyptianMobile } from "../validation";

// Step 1 of login: phone number and password.
// Students go straight in. Staff are sent to the two-factor step.
export function LoginPage() {
  const { t, errorMessage } = useI18n();
  const { startSession } = useAuth();
  const navigate = useNavigate();

  const [phone, setPhone] = useState("");
  const [password, setPassword] = useState("");
  const [phoneError, setPhoneError] = useState<string | null>(null);
  const [formError, setFormError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    setFormError(null);

    if (!isEgyptianMobile(phone)) {
      setPhoneError(t("validation.phone"));
      return;
    }
    setPhoneError(null);

    setBusy(true);
    try {
      const response = await api.login(phone, password, getDeviceId());

      if (response.status === "Authenticated" && response.session) {
        startSession(response.session);
        navigate("/");
        return;
      }

      // Staff: carry the challenge token to the next screen in memory only (never in the URL).
      const nextPath = response.status === "TwoFactorSetupRequired" ? "/two-factor/setup" : "/two-factor";
      navigate(nextPath, { state: { challengeToken: response.challengeToken } });
    } catch (error) {
      setFormError(errorMessage(error));
    } finally {
      setBusy(false);
    }
  }

  return (
    <AuthLayout title={t("login.title")} subtitle={t("login.subtitle")}>
      <form onSubmit={handleSubmit} noValidate>
        {formError && <Alert>{formError}</Alert>}

        <Field
          label={t("login.phone")}
          type="tel"
          inputMode="numeric"
          autoComplete="username"
          dir="ltr"
          placeholder={t("login.phonePlaceholder")}
          value={phone}
          onChange={(event) => setPhone(event.target.value)}
          error={phoneError}
          required
        />
        <Field
          label={t("login.password")}
          type="password"
          autoComplete="current-password"
          dir="ltr"
          value={password}
          onChange={(event) => setPassword(event.target.value)}
          required
        />

        <Button type="submit" block busy={busy} disabled={!phone || !password}>
          {t("login.submit")}
        </Button>
      </form>

      <p className="auth-footer">
        <Link to="/activate">{t("login.firstTime")}</Link>
      </p>
      <p className="auth-footer">
        <Link to="/accept-invite">{t("login.haveInvite")}</Link>
      </p>
    </AuthLayout>
  );
}
