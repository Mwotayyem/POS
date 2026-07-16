// Persists the JWT access + refresh tokens. localStorage keeps the session across reloads.
// (For higher security, refresh tokens can be moved to an httpOnly cookie server-side later.)

const ACCESS_KEY = 'smartapp.accessToken';
const REFRESH_KEY = 'smartapp.refreshToken';

export const tokenStore = {
  getAccess(): string | null {
    return localStorage.getItem(ACCESS_KEY);
  },
  getRefresh(): string | null {
    return localStorage.getItem(REFRESH_KEY);
  },
  set(accessToken: string, refreshToken: string): void {
    localStorage.setItem(ACCESS_KEY, accessToken);
    localStorage.setItem(REFRESH_KEY, refreshToken);
  },
  clear(): void {
    localStorage.removeItem(ACCESS_KEY);
    localStorage.removeItem(REFRESH_KEY);
  },
};
