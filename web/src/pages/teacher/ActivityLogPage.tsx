import { useCallback, useState } from "react";
import * as api from "../../api/endpoints";
import { useAuth } from "../../auth/AuthContext";
import { AppLayout, PageHeader } from "../../components/layouts";
import { Pagination, useLoad } from "../../components/shared";
import { EmptyState, ErrorState, SkeletonRows } from "../../components/ui";
import { useI18n } from "../../i18n";
import type { TranslationKey } from "../../i18n/ar";
import { ar } from "../../i18n/ar";
import { teacherNavItems } from "./teacherNav";

const PAGE_SIZE = 20;

// Teacher only: everything the assistants did, newest first.
// The rows are written by the server automatically; this page only reads them.
export function ActivityLogPage() {
  const { t, errorMessage, language } = useI18n();
  const { session } = useAuth();

  const [assistantId, setAssistantId] = useState<number | null>(null);
  const [pageIndex, setPageIndex] = useState(1);

  const loadLog = useCallback(
    () => api.listAuditLogs({ userId: assistantId, pageIndex, pageSize: PAGE_SIZE }),
    [assistantId, pageIndex],
  );
  const log = useLoad(loadLog);
  const assistants = useLoad(api.listAssistants);

  if (!session?.activeMembership) {
    return null;
  }

  // Shows a translated name when we have one, and the raw name otherwise
  // (so a new kind of record still shows up, just not translated yet).
  function translateOrKeep(prefix: string, value: string): string {
    const key = `${prefix}.${value}`;
    return key in ar ? t(key as TranslationKey) : value;
  }

  function formatDateTime(isoDate: string): string {
    return new Date(isoDate).toLocaleString(language === "ar" ? "ar-EG" : "en-GB", {
      dateStyle: "medium",
      timeStyle: "short",
    });
  }

  return (
    <AppLayout navItems={teacherNavItems(session.activeMembership.role)}>
      <PageHeader title={t("activity.title")} subtitle={t("activity.subtitle")} />

      <section className="card">
        <div className="toolbar">
          <select
            className="input"
            aria-label={t("activity.who")}
            value={assistantId ?? ""}
            onChange={(event) => {
              setAssistantId(event.target.value === "" ? null : Number(event.target.value));
              setPageIndex(1);
            }}
          >
            <option value="">{t("activity.allAssistants")}</option>
            {(assistants.data ?? []).map((assistant) => (
              <option key={assistant.id} value={assistant.id}>
                {assistant.fullName}
              </option>
            ))}
          </select>
        </div>

        {log.error !== null && <ErrorState message={errorMessage(log.error)} onRetry={log.reload} />}
        {log.error === null && log.data === null && <SkeletonRows />}
        {log.data !== null && log.data.count === 0 && <EmptyState message={t("activity.empty")} />}
        {log.data !== null && log.data.count > 0 && (
          <>
            <div className="table-wrap">
              <table className="table">
                <thead>
                  <tr>
                    <th>{t("activity.time")}</th>
                    <th>{t("activity.who")}</th>
                    <th>{t("activity.action")}</th>
                    <th>{t("activity.what")}</th>
                    <th>{t("activity.details")}</th>
                  </tr>
                </thead>
                <tbody>
                  {log.data.data.map((entry) => (
                    <tr key={entry.id}>
                      <td>{formatDateTime(entry.createdAt)}</td>
                      <td>
                        <strong>{entry.userName}</strong>
                      </td>
                      <td>{translateOrKeep("activity.action", entry.action)}</td>
                      <td>
                        {translateOrKeep("activity.entity", entry.entityName)} <span dir="ltr">#{entry.entityId}</span>
                      </td>
                      <td>
                        {/* The details are shown as plain text. React escapes it, so stored text cannot run as code. */}
                        <div className="details-text" dir="ltr">
                          {entry.details ?? "—"}
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <Pagination pageIndex={pageIndex} pageSize={PAGE_SIZE} totalCount={log.data.count} onChange={setPageIndex} />
          </>
        )}
      </section>
    </AppLayout>
  );
}
