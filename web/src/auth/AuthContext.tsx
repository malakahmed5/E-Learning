// Keeps the login session for the whole website.
//
// Where tokens live (a security decision):
//   - The access token is kept only in memory. It is gone when the page is closed or reloaded.
//   - The refresh token is kept in sessionStorage, so a page reload keeps the user logged in,
//     but closing the tab logs them out.
// Neither is put in localStorage, which would keep them on the computer for a long time.

import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState, type ReactNode } from "react";
import { connectAuth } from "../api/client";
import * as api from "../api/endpoints";
import type { Session } from "../api/types";
import { getDeviceId } from "./deviceId";

const REFRESH_TOKEN_KEY = "elearning.refreshToken";
const TENANT_ID_KEY = "elearning.tenantId";

interface Auth {
  session: Session | null;
  // True while we check, on page load, whether the user is still logged in.
  isRestoring: boolean;
  startSession: (session: Session) => void;
  chooseTeacher: (tenantId: number) => Promise<void>;
  logout: () => Promise<void>;
}

const AuthContext = createContext<Auth | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [session, setSession] = useState<Session | null>(null);
  const [isRestoring, setIsRestoring] = useState(true);

  // The API client needs the newest session without waiting for React to re-render.
  const sessionRef = useRef<Session | null>(null);

  const saveSession = useCallback((next: Session | null) => {
    sessionRef.current = next;
    setSession(next);

    if (next) {
      window.sessionStorage.setItem(REFRESH_TOKEN_KEY, next.refreshToken);
      if (next.activeMembership) {
        window.sessionStorage.setItem(TENANT_ID_KEY, String(next.activeMembership.tenantId));
      } else {
        window.sessionStorage.removeItem(TENANT_ID_KEY);
      }
    } else {
      window.sessionStorage.removeItem(REFRESH_TOKEN_KEY);
      window.sessionStorage.removeItem(TENANT_ID_KEY);
    }
  }, []);

  // A refresh token can be used only once. If two parts of the page tried to renew at the same
  // moment, the second one would send an already-used token, and the API would treat that as theft
  // and end every session. So while one renewal is running, everyone else waits for that same one.
  const renewalInProgress = useRef<Promise<boolean> | null>(null);

  // Asks the API for a new session using the saved refresh token. Returns false when that fails.
  const renew = useCallback(
    (tenantId: number | null): Promise<boolean> => {
      if (renewalInProgress.current) {
        return renewalInProgress.current;
      }

      const renewal = (async () => {
        const refreshToken = sessionRef.current?.refreshToken ?? window.sessionStorage.getItem(REFRESH_TOKEN_KEY);
        if (!refreshToken) {
          return false;
        }
        try {
          const next = await api.refreshSession(refreshToken, getDeviceId(), tenantId);
          saveSession(next);
          return true;
        } catch {
          // The refresh token is no longer valid: the user must log in again.
          saveSession(null);
          return false;
        }
      })();

      renewalInProgress.current = renewal;
      renewal.finally(() => {
        renewalInProgress.current = null;
      });
      return renewal;
    },
    [saveSession],
  );

  // Tell the API client how to read the token and how to renew it.
  useEffect(() => {
    connectAuth(
      () => sessionRef.current?.accessToken ?? null,
      () => renew(sessionRef.current?.activeMembership?.tenantId ?? null),
    );
  }, [renew]);

  // On page load: if a refresh token was saved, get a fresh session with it.
  useEffect(() => {
    const savedTenantId = window.sessionStorage.getItem(TENANT_ID_KEY);
    const tenantId = savedTenantId ? Number(savedTenantId) : null;
    renew(tenantId).finally(() => setIsRestoring(false));
  }, [renew]);

  const chooseTeacher = useCallback(
    async (tenantId: number) => {
      const current = sessionRef.current;
      if (!current) {
        return;
      }
      // This call throws an ApiError when the teacher cannot be opened; the page shows it.
      const next = await api.refreshSession(current.refreshToken, getDeviceId(), tenantId);
      saveSession(next);
    },
    [saveSession],
  );

  const logout = useCallback(async () => {
    const refreshToken = sessionRef.current?.refreshToken;
    saveSession(null);
    if (refreshToken) {
      // Best effort: even if this call fails, the user is logged out on this device.
      await api.logout(refreshToken).catch(() => undefined);
    }
  }, [saveSession]);

  const value = useMemo<Auth>(
    () => ({ session, isRestoring, startSession: saveSession, chooseTeacher, logout }),
    [session, isRestoring, saveSession, chooseTeacher, logout],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): Auth {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error("useAuth must be used inside AuthProvider");
  }
  return context;
}
