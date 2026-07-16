import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import { setAuthFailureHandler } from '../api/client';
import { authApi, profileApi } from '../api/endpoints';
import type { Profile } from '../api/models';
import { tokenStore } from './tokenStore';

interface AuthState {
  profile: Profile | null;
  loading: boolean;
  login: (email: string, password: string) => Promise<void>;
  logout: () => Promise<void>;
  hasPermission: (permission: string) => boolean;
  hasAny: (...permissions: string[]) => boolean;
}

const AuthCtx = createContext<AuthState | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [profile, setProfile] = useState<Profile | null>(null);
  const [loading, setLoading] = useState(true);

  const clearSession = useCallback(() => {
    tokenStore.clear();
    setProfile(null);
  }, []);

  // On a refresh failure inside the API client, drop the session.
  useEffect(() => {
    setAuthFailureHandler(clearSession);
  }, [clearSession]);

  // Restore the session on first load if a token is present.
  useEffect(() => {
    let cancelled = false;
    async function restore() {
      if (!tokenStore.getAccess()) {
        setLoading(false);
        return;
      }
      try {
        const me = await profileApi.me();
        if (!cancelled) setProfile(me);
      } catch {
        if (!cancelled) clearSession();
      } finally {
        if (!cancelled) setLoading(false);
      }
    }
    void restore();
    return () => {
      cancelled = true;
    };
  }, [clearSession]);

  const login = useCallback(async (email: string, password: string) => {
    const tokens = await authApi.login(email, password);
    tokenStore.set(tokens.accessToken, tokens.refreshToken);
    const me = await profileApi.me();
    setProfile(me);
  }, []);

  const logout = useCallback(async () => {
    const refresh = tokenStore.getRefresh();
    if (refresh) {
      try {
        await authApi.logout(refresh);
      } catch {
        // Ignore — we clear the local session regardless.
      }
    }
    clearSession();
  }, [clearSession]);

  const hasPermission = useCallback(
    (permission: string) => (profile ? profile.permissions.includes(permission) : false),
    [profile],
  );
  const hasAny = useCallback(
    (...permissions: string[]) => permissions.some((p) => hasPermission(p)),
    [hasPermission],
  );

  const value = useMemo<AuthState>(
    () => ({ profile, loading, login, logout, hasPermission, hasAny }),
    [profile, loading, login, logout, hasPermission, hasAny],
  );

  return <AuthCtx.Provider value={value}>{children}</AuthCtx.Provider>;
}

export function useAuth(): AuthState {
  const ctx = useContext(AuthCtx);
  if (!ctx) throw new Error('useAuth must be used within AuthProvider');
  return ctx;
}
