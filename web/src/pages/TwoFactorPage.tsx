import { QRCodeSVG } from "qrcode.react";
import { useEffect, useRef, useState, type FormEvent } from "react";
import { Navigate, useLocation, useNavigate } from "react-router-dom";
import * as api from "../api/endpoints";
import type { TwoFactorSetup } from "../api/types";
import { useAuth } from "../auth/AuthContext";
import { AuthLayout } from "../components/layouts";
import { Alert, Button, Field, LoadingScreen } from "../components/ui";
import { useI18n } from "../i18n";
import { isSixDigitCode } from "../validation";

// Step 2 of login for staff: the 6-digit code from the authenticator app.
// With mode "setup" (first login) the page first shows the QR code to add the account to the app.
export function TwoFactorPage({ mode }: { mode: "setup" | "verify" }) {
  const { t, errorMessage } = useI18n();
  const { startSession } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();

  // The login page passed the challenge token in memory. After a page reload it is gone on purpose.
  const challengeToken: string | undefined = location.state?.challengeToken;

  const [setup, setSetup] = useState<TwoFactorSetup | null>(null);
  const [setupError, setSetupError] = useState<string | null>(null);
  const [code, setCode] = useState("");
  const [codeError, setCodeError] = useState<string | null>(null);
  const [formError, setFormError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  // Each setup call creates a NEW secret on the server. If we asked twice, the QR code on screen
  // could belong to the older secret and the codes would never match. So we ask exactly once.
  const setupRequested = useRef(false);

  useEffect(() => {
    if (mode !== "setup" || !challengeToken || setupRequested.current) {
      return;
    }
    setupRequested.current = true;
    api
      .setupTwoFactor(challengeToken)
      .then(setSetup)
      .catch((error) => setSetupError(errorMessage(error)));
    // Run once for this challenge token. errorMessage changes with the language and must not re-run the setup.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [mode, challengeToken]);

  if (!challengeToken) {
    return <Navigate to="/login" replace />;
  }

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    setFormError(null);

    if (!isSixDigitCode(code)) {
      setCodeError(t("validation.code6"));
      return;
    }
    setCodeError(null);

    setBusy(true);
    try {
      const session = await api.verifyTwoFactor(challengeToken!, code);
      startSession(session);
      navigate("/");
    } catch (error) {
      setFormError(errorMessage(error));
      setCode("");
    } finally {
      setBusy(false);
    }
  }

  const isSetup = mode === "setup";

  if (isSetup && setupError) {
    return (
      <AuthLayout title={t("twoFactor.setupTitle")}>
        <Alert>{setupError}</Alert>
        <Button variant="secondary" block onClick={() => navigate("/login")}>
          {t("invite.backToLogin")}
        </Button>
      </AuthLayout>
    );
  }

  if (isSetup && !setup) {
    return <LoadingScreen />;
  }

  return (
    <AuthLayout
      title={isSetup ? t("twoFactor.setupTitle") : t("twoFactor.title")}
      subtitle={isSetup ? t("twoFactor.setupSubtitle") : t("twoFactor.subtitle")}
    >
      {isSetup && setup && (
        <ol className="steps">
          <li>{t("twoFactor.step1")}</li>
          <li>
            {t("twoFactor.step2")}
            <div className="qr-box">
              <QRCodeSVG value={setup.otpAuthUri} size={168} />
            </div>
            <p className="secret-line">
              <span className="muted">{t("twoFactor.secret")}: </span>
              <code dir="ltr">{setup.secret}</code>
            </p>
          </li>
          <li>{t("twoFactor.step3")}</li>
        </ol>
      )}

      <form onSubmit={handleSubmit} noValidate>
        {formError && <Alert>{formError}</Alert>}

        <Field
          label={t("twoFactor.code")}
          inputMode="numeric"
          autoComplete="one-time-code"
          dir="ltr"
          maxLength={6}
          className="input input-code"
          value={code}
          onChange={(event) => setCode(event.target.value.replace(/\D/g, ""))}
          error={codeError}
          autoFocus
          required
        />

        <Button type="submit" block busy={busy} disabled={code.length !== 6}>
          {t("twoFactor.submit")}
        </Button>
      </form>
    </AuthLayout>
  );
}
