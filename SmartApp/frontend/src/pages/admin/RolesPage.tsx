import { useState, type FormEvent } from 'react';
import { permissionsApi, rolesApi } from '../../api/endpoints';
import type { Role } from '../../api/models';
import { useAuth } from '../../auth/AuthContext';
import { P } from '../../auth/permissions';
import { useAsync } from '../../lib/useAsync';
import { errorMessage } from '../../lib/format';
import { DataTable, ErrorBanner, Field, Loading, Modal, PageHeader } from '../../components/ui';

export function RolesPage() {
  const { hasPermission } = useAuth();
  const roles = useAsync(() => rolesApi.list(), []);
  const perms = useAsync(() => permissionsApi.list(), []);

  const [editing, setEditing] = useState<{ id?: number; name: string; description: string } | null>(null);
  const [permsFor, setPermsFor] = useState<{ role: Role; selected: Set<string> } | null>(null);
  const [saveError, setSaveError] = useState<string | null>(null);

  const canCreate = hasPermission(P.roles.create);
  const canUpdate = hasPermission(P.roles.update);
  const canDelete = hasPermission(P.roles.delete);
  const canManagePerms = hasPermission(P.roles.managePermissions);

  async function saveRole(e: FormEvent) {
    e.preventDefault();
    if (!editing) return;
    setSaveError(null);
    const body = { name: editing.name.trim(), description: editing.description.trim() || null };
    try {
      if (editing.id) await rolesApi.update(editing.id, body);
      else await rolesApi.create({ ...body, permissions: [] });
      setEditing(null);
      roles.reload();
    } catch (err) {
      setSaveError(errorMessage(err));
    }
  }

  async function savePerms() {
    if (!permsFor) return;
    setSaveError(null);
    try {
      await rolesApi.setPermissions(permsFor.role.id, [...permsFor.selected]);
      setPermsFor(null);
      roles.reload();
    } catch (err) {
      setSaveError(errorMessage(err));
    }
  }

  async function remove(r: Role) {
    if (!confirm(`حذف الدور "${r.name}"؟`)) return;
    try {
      await rolesApi.remove(r.id);
      roles.reload();
    } catch (err) {
      alert(errorMessage(err));
    }
  }

  return (
    <div className="stack">
      <PageHeader
        title="الأدوار"
        actions={canCreate && <button className="btn" onClick={() => setEditing({ name: '', description: '' })}>+ دور جديد</button>}
      />
      {(roles.loading || perms.loading) && <Loading />}
      {roles.error && <ErrorBanner message={roles.error} />}
      {roles.data && (
        <DataTable
          rows={roles.data}
          rowKey={(r) => r.id}
          columns={[
            { header: 'الاسم', render: (r) => r.name },
            { header: 'الوصف', render: (r) => r.description ?? '-' },
            { header: 'نظامي', render: (r) => (r.isSystemRole ? <span className="badge">نظامي</span> : '-') },
            { header: 'عدد الصلاحيات', render: (r) => r.permissions.length },
            {
              header: '',
              render: (r) => (
                <div className="row">
                  {canManagePerms && (
                    <button className="btn btn-secondary btn-sm" onClick={() => setPermsFor({ role: r, selected: new Set(r.permissions) })}>
                      الصلاحيات
                    </button>
                  )}
                  {canUpdate && !r.isSystemRole && (
                    <button className="btn btn-secondary btn-sm" onClick={() => setEditing({ id: r.id, name: r.name, description: r.description ?? '' })}>
                      تعديل
                    </button>
                  )}
                  {canDelete && !r.isSystemRole && (
                    <button className="btn btn-danger btn-sm" onClick={() => remove(r)}>حذف</button>
                  )}
                </div>
              ),
            },
          ]}
        />
      )}

      {editing && (
        <Modal
          title={editing.id ? 'تعديل دور' : 'دور جديد'}
          onClose={() => setEditing(null)}
          footer={
            <>
              <button className="btn btn-secondary" onClick={() => setEditing(null)}>إلغاء</button>
              <button className="btn" type="submit" form="role-form">حفظ</button>
            </>
          }
        >
          <form id="role-form" onSubmit={saveRole}>
            {saveError && <ErrorBanner message={saveError} />}
            <Field label="الاسم">
              <input className="input" value={editing.name} onChange={(e) => setEditing({ ...editing, name: e.target.value })} required />
            </Field>
            <Field label="الوصف">
              <textarea className="input" rows={3} value={editing.description} onChange={(e) => setEditing({ ...editing, description: e.target.value })} />
            </Field>
          </form>
        </Modal>
      )}

      {permsFor && (
        <Modal
          title={`صلاحيات الدور: ${permsFor.role.name}`}
          onClose={() => setPermsFor(null)}
          footer={
            <>
              <button className="btn btn-secondary" onClick={() => setPermsFor(null)}>إلغاء</button>
              <button className="btn" onClick={savePerms}>حفظ</button>
            </>
          }
        >
          {saveError && <ErrorBanner message={saveError} />}
          <div style={{ maxHeight: '50vh', overflowY: 'auto' }}>
            {(perms.data ?? []).map((p) => {
              const checked = permsFor.selected.has(p.code);
              return (
                <label key={p.code} className="row" style={{ padding: '4px 0', cursor: 'pointer' }}>
                  <input
                    type="checkbox"
                    checked={checked}
                    onChange={(e) => {
                      const next = new Set(permsFor.selected);
                      if (e.target.checked) next.add(p.code);
                      else next.delete(p.code);
                      setPermsFor({ ...permsFor, selected: next });
                    }}
                  />
                  <span>{p.displayName}</span>
                  <code className="muted" style={{ fontSize: 12 }} dir="ltr">{p.code}</code>
                </label>
              );
            })}
          </div>
        </Modal>
      )}
    </div>
  );
}
