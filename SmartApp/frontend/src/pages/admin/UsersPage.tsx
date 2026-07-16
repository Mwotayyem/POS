import { useState, type FormEvent } from 'react';
import { rolesApi, usersApi } from '../../api/endpoints';
import type { User } from '../../api/models';
import { useAuth } from '../../auth/AuthContext';
import { P } from '../../auth/permissions';
import { useAsync } from '../../lib/useAsync';
import { date, errorMessage } from '../../lib/format';
import { DataTable, ErrorBanner, Field, Loading, Modal, PageHeader } from '../../components/ui';

interface Editing {
  id?: number;
  email: string;
  fullName: string;
  phone: string;
  password: string;
  roleIds: Set<number>;
}

export function UsersPage() {
  const { hasPermission } = useAuth();
  const [search, setSearch] = useState('');
  const users = useAsync(() => usersApi.list(search || undefined), [search]);
  const roles = useAsync(() => rolesApi.list(), []);
  const [editing, setEditing] = useState<Editing | null>(null);
  const [saveError, setSaveError] = useState<string | null>(null);

  const canCreate = hasPermission(P.users.create);
  const canUpdate = hasPermission(P.users.update);

  async function save(e: FormEvent) {
    e.preventDefault();
    if (!editing) return;
    setSaveError(null);
    const roleIds = [...editing.roleIds];
    try {
      if (editing.id) {
        await usersApi.update(editing.id, { fullName: editing.fullName.trim(), phone: editing.phone.trim() || null, roleIds });
      } else {
        await usersApi.create({
          email: editing.email.trim(),
          fullName: editing.fullName.trim(),
          password: editing.password,
          phone: editing.phone.trim() || null,
          roleIds,
        });
      }
      setEditing(null);
      users.reload();
    } catch (err) {
      setSaveError(errorMessage(err));
    }
  }

  async function toggleActive(u: User) {
    try {
      if (u.isActive) await usersApi.deactivate(u.id);
      else await usersApi.activate(u.id);
      users.reload();
    } catch (err) {
      alert(errorMessage(err));
    }
  }

  return (
    <div className="stack">
      <PageHeader
        title="المستخدمون"
        actions={
          canCreate && (
            <button
              className="btn"
              onClick={() => setEditing({ email: '', fullName: '', phone: '', password: '', roleIds: new Set() })}
            >
              + مستخدم جديد
            </button>
          )
        }
      />

      <div className="row">
        <input
          className="input"
          placeholder="بحث بالاسم أو البريد…"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          style={{ maxWidth: 280 }}
        />
      </div>

      {(users.loading || roles.loading) && <Loading />}
      {users.error && <ErrorBanner message={users.error} />}
      {users.data && (
        <DataTable
          rows={users.data}
          rowKey={(u) => u.id}
          columns={[
            { header: 'الاسم', render: (u) => u.fullName },
            { header: 'البريد', render: (u) => <span dir="ltr">{u.email}</span> },
            { header: 'الأدوار', render: (u) => u.roles.map((r) => r.name).join('، ') || '-' },
            { header: 'آخر دخول', render: (u) => date(u.lastLoginAt) },
            { header: 'الحالة', render: (u) => (u.isActive ? <span className="badge">مفعّل</span> : 'معطّل') },
            {
              header: '',
              render: (u) =>
                canUpdate && (
                  <div className="row">
                    <button
                      className="btn btn-secondary btn-sm"
                      onClick={() =>
                        setEditing({
                          id: u.id,
                          email: u.email,
                          fullName: u.fullName,
                          phone: u.phone ?? '',
                          password: '',
                          roleIds: new Set(u.roles.map((r) => r.id)),
                        })
                      }
                    >
                      تعديل
                    </button>
                    <button className="btn btn-secondary btn-sm" onClick={() => toggleActive(u)}>
                      {u.isActive ? 'تعطيل' : 'تفعيل'}
                    </button>
                  </div>
                ),
            },
          ]}
        />
      )}

      {editing && (
        <Modal
          title={editing.id ? 'تعديل مستخدم' : 'مستخدم جديد'}
          onClose={() => setEditing(null)}
          footer={
            <>
              <button className="btn btn-secondary" onClick={() => setEditing(null)}>إلغاء</button>
              <button className="btn" type="submit" form="user-form">حفظ</button>
            </>
          }
        >
          <form id="user-form" onSubmit={save}>
            {saveError && <ErrorBanner message={saveError} />}
            {!editing.id && (
              <>
                <Field label="البريد الإلكتروني">
                  <input className="input" type="email" value={editing.email} onChange={(e) => setEditing({ ...editing, email: e.target.value })} required dir="ltr" />
                </Field>
                <Field label="كلمة المرور">
                  <input className="input" type="password" value={editing.password} onChange={(e) => setEditing({ ...editing, password: e.target.value })} required dir="ltr" />
                </Field>
              </>
            )}
            <Field label="الاسم الكامل">
              <input className="input" value={editing.fullName} onChange={(e) => setEditing({ ...editing, fullName: e.target.value })} required />
            </Field>
            <Field label="الهاتف">
              <input className="input" value={editing.phone} onChange={(e) => setEditing({ ...editing, phone: e.target.value })} dir="ltr" />
            </Field>
            <Field label="الأدوار">
              <div style={{ border: '1px solid var(--color-border)', borderRadius: 'var(--radius)', padding: 8, maxHeight: 160, overflowY: 'auto' }}>
                {(roles.data ?? []).map((r) => (
                  <label key={r.id} className="row" style={{ padding: '3px 0', cursor: 'pointer' }}>
                    <input
                      type="checkbox"
                      checked={editing.roleIds.has(r.id)}
                      onChange={(e) => {
                        const next = new Set(editing.roleIds);
                        if (e.target.checked) next.add(r.id);
                        else next.delete(r.id);
                        setEditing({ ...editing, roleIds: next });
                      }}
                    />
                    <span>{r.name}</span>
                  </label>
                ))}
              </div>
            </Field>
          </form>
        </Modal>
      )}
    </div>
  );
}
