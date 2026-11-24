import { useState, type FormEvent } from "react";
import * as api from "../../api/endpoints";
import type { RedeemResult, SubscriptionState } from "../../api/types";
import { useLoad } from "../../components/shared";
import { Alert, Button, ErrorState, Field, SkeletonRows } from "../../components/ui";
import { useI18n } from "../../i18n";
import type { TranslationKey } from "../../i18n/ar";

// The colour of the status label for each state.
const STATE_BADGE: Record<SubscriptionState, string> = {
  NotActivated: "badge badge-muted",
  Trial: "badge badge-info",
  Active: "badge badge-success",
  Expired: "badge badge-warning",
};

// Shown on the student's home page: how many days are left, and a box to enter a new code.
export function SubscriptionCard() {
  const { t, errorMessage, formatDate } = useI18n();

  const subscription = useLoad(api.getMySubscription);
  const [code, setCode] = useState("");
  const [formError, setFormError] = useState<string | null>(null);
  const [result, setResult] = useState<RedeemResult | null>(null);
  const [busy, setBusy] = useState(false);

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    setFormError(null);
    setResult(null);
    setBusy(true);
    try {
      setResult(await api.redeemCode(code));
      setCode("");
      subscription.reload();
    } catch (error) {
      setFormError(errorMessage(error));
    } finally {
      setBusy(false);
    }
  }

  const status = subscription.data;

  return (
    <section className="card" data-testid="subscription-card">
      <h2>{t("subscription.title")}</h2>

      {subscription.error !== null && <ErrorState message={errorMessage(subscription.error)} onRetry={subscription.reload} />}
      {subscription.error === null && status === null && <SkeletonRows rows={2} />}

      {status && (
        <>
          <p>
            <span className={STATE_BADGE[status.status]}>{t(`subscription.state.${status.status}` as TranslationKey)}</span>
            {status.endsAt && status.status !== "Expired" && (
              <span className="muted">
                {" "}
                {t("subscription.daysLeft", { days: String(status.daysLeft) })} · {t("subscription.endsAt", { date: formatDate(status.endsAt) })}
              </span>
            )}
          </p>

          {status.status === "Expired" && <Alert>{t("subscription.expired")}</Alert>}
          {status.expiringSoon && <Alert kind="info">{t("subscription.expiringSoon")}</Alert>}
        </>
      )}

      {result && (
        <Alert kind="success">
          <strong>{t("subscription.success")}. </strong>
          {t("subscription.result", {
            type: t(`codes.type.${result.type}` as TranslationKey),
            start: formatDate(result.startsAt),
            end: formatDate(result.endsAt),
          })}
        </Alert>
      )}

      <form onSubmit={handleSubmit} noValidate>
        {formError && <Alert>{formError}</Alert>}
        <Field
          label={t("subscription.enterCode")}
          dir="ltr"
          autoComplete="off"
          placeholder={t("invite.codePlaceholder")}
          value={code}
          onChange={(event) => setCode(event.target.value.toUpperCase())}
          required
        />
        <Button type="submit" busy={busy} disabled={code.trim() === ""}>
          {t("subscription.redeem")}
        </Button>
      </form>
    </section>
  );
}
