import { useState, type ReactNode } from 'react';
import { NavLink, useNavigate } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { NAV } from './nav';

export function AppLayout({ children }: { children: ReactNode }) {
  const { profile, logout, hasAny } = useAuth();
  const navigate = useNavigate();
  const [menuOpen, setMenuOpen] = useState(false);

  const sections = NAV.map((s) => ({
    ...s,
    items: s.items.filter((i) => i.anyOf.length === 0 || hasAny(...i.anyOf)),
  })).filter((s) => s.items.length > 0);

  async function onLogout() {
    await logout();
    navigate('/login', { replace: true });
  }

  return (
    <div style={{ display: 'flex', minHeight: '100vh' }}>
      <aside
        style={{
          width: 'var(--sidebar-width)',
          background: 'var(--color-surface)',
          borderInlineEnd: '1px solid var(--color-border)',
          position: 'sticky',
          top: 0,
          height: '100vh',
          overflowY: 'auto',
          flexShrink: 0,
        }}
      >
        <div
          style={{
            height: 'var(--header-height)',
            display: 'flex',
            alignItems: 'center',
            padding: '0 18px',
            fontWeight: 700,
            fontSize: 18,
            color: 'var(--color-primary)',
            borderBottom: '1px solid var(--color-border)',
          }}
        >
          SmartApp
        </div>
        <nav style={{ padding: '10px 8px' }}>
          {sections.map((section) => (
            <div key={section.title} style={{ marginBottom: 14 }}>
              <div
                className="muted"
                style={{ fontSize: 11, textTransform: 'uppercase', padding: '4px 10px', letterSpacing: 0.4 }}
              >
                {section.title}
              </div>
              {section.items.map((item) => (
                <NavLink
                  key={item.path}
                  to={item.path}
                  style={({ isActive }) => ({
                    display: 'block',
                    padding: '8px 12px',
                    borderRadius: 'var(--radius)',
                    color: isActive ? 'var(--color-primary-contrast)' : 'var(--color-text)',
                    background: isActive ? 'var(--color-primary)' : 'transparent',
                    fontWeight: isActive ? 600 : 400,
                    marginBottom: 2,
                  })}
                >
                  {item.label}
                </NavLink>
              ))}
            </div>
          ))}
        </nav>
      </aside>

      <div style={{ flex: 1, display: 'flex', flexDirection: 'column', minWidth: 0 }}>
        <header
          style={{
            height: 'var(--header-height)',
            background: 'var(--color-surface)',
            borderBottom: '1px solid var(--color-border)',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'flex-end',
            padding: '0 20px',
            position: 'sticky',
            top: 0,
            zIndex: 10,
          }}
        >
          <div style={{ position: 'relative' }}>
            <button className="btn btn-secondary btn-sm" onClick={() => setMenuOpen((o) => !o)}>
              {profile?.fullName ?? 'المستخدم'} ▾
            </button>
            {menuOpen && (
              <div
                className="card"
                style={{ position: 'absolute', insetInlineEnd: 0, top: '110%', width: 220, padding: 8, zIndex: 20 }}
              >
                <div style={{ padding: '6px 10px' }}>
                  <div style={{ fontWeight: 600 }}>{profile?.fullName}</div>
                  <div className="muted" style={{ fontSize: 12 }} dir="ltr">
                    {profile?.email}
                  </div>
                </div>
                <NavLink
                  to="/profile"
                  onClick={() => setMenuOpen(false)}
                  style={{ display: 'block', padding: '8px 10px', color: 'var(--color-text)' }}
                >
                  الملف الشخصي
                </NavLink>
                <button
                  className="btn btn-danger btn-sm"
                  onClick={onLogout}
                  style={{ width: '100%', marginTop: 4 }}
                >
                  تسجيل الخروج
                </button>
              </div>
            )}
          </div>
        </header>

        <main style={{ padding: 24, flex: 1 }}>{children}</main>
      </div>
    </div>
  );
}
