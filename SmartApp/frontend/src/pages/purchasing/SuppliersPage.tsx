import { useState, type FormEvent } from 'react';
import { suppliersApi } from '../../api/endpoints';
import type { Supplier } from '../../api/models';
import { useAuth } from '../../auth/AuthContext';
import { P } from '../../auth/permissions';
import { useAsync } from '../../lib/useAsync';
import { errorMessage, money } from '../../lib/format';
import { DataTable, ErrorBanner, Field, Loading, Modal, PageHeader } from '../../components/ui';

interface Editing {
  id?: number;
  name: string;
  phone: string;
  email: string;
  address: string;
  isActive: boolean;
}

const blank: Editing = { name: '', phone: '', email: '', address: '', isActive: true };

export function SuppliersPage() {
  const { hasPermission } = useAuth();
  const [search, setSearch] = useState('');
  const { data, loading, error, reload } = useAsync(() => suppliersApi.list(search), [search]);
  const [editing, setEditing] = useState<Editing | null>(null);
  const [saveError, setSaveError] = useState<string | null>(null);

  const canCreate = hasPermission(P.suppliers.create);
  const canUpdate = hasPermission(P.suppliers.update);
  const canDelete = hasPermission(P.suppliers.delete);

  async function save(e: FormEvent) {
    e.preventDefault();
    if (!editing) return;
    setSaveError(null);
    const body = {
      name: editing.name.trim(),
      phone: editing.phone.trim() || null,
      email: editing.email.trim() || null,
      address: editing.address.trim() || null,
    };
    try {
      if (editing.id) {
        await suppliersApi.update(editing.id, { ...body, isActive: editing.isActive });
      } else {
        await suppliersApi.create(body);
      }
      setEditing(null);
      reload();
    } catch (err) {
      setSaveError(errorMessage(err));
    }
  }

  async function remove(s: Supplier) {
    if (!confirm(`حذف المورّد "${s.name}"؟`)) return;
    try {
      await suppliersApi.remove(s.id);
      reload();
    } catch (err) {
      alert(errorMessage(err));
    }
  }

  return (
    <div className="stack">
      <PageHeader
        title="الموردون"
        actions={canCreate && <button className="btn" onClick={() => setEditing({ ...blank })}>+ مورّد جديد</button>}
      />
      <div className="row">
        <input
          className="input"
          placeholder="بحث…"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
        />
      </div>
      {loading && <Loading />}
      {error && <ErrorBanner message={error} />}
      {data && (
        <DataTable
          rows={data}
          rowKey={(s) => s.id}
          columns={[
            { header: 'الاسم', render: (s) => s.name },
            { header: 'الهاتف', render: (s) => s.phone ?? '-' },
            { header: 'البريد الإلكتروني', render: (s) => s.email ?? '-' },
            { header: 'الرصيد', render: (s) => money(s.balance) },
            { header: 'الحالة', render: (s) => (s.isActive ? <span className="badge">مفعّل</span> : 'معطّل') },
            {
              header: '',
              render: (s) => (
                <div className="row">
                  {canUpdate && (
                    <button
                      className="btn btn-secondary btn-sm"
                      onClick={() => setEditing({ id: s.id, name: s.name, phone: s.phone ?? '', email: s.email ?? '', address: s.address ?? '', isActive: s.isActive })}
                    >
                      تعديل
                    </button>
                  )}
                  {canDelete && (
                    <button className="btn btn-danger btn-sm" onClick={() => remove(s)}>حذف</button>
                  )}
                </div>
              ),
            },
          ]}
        />
      )}

      {editing && (
        <Modal
          title={editing.id ? 'تعديل مورّد' : 'مورّد جديد'}
          onClose={() => setEditing(null)}
          footer={
            <>
              <button className="btn btn-secondary" onClick={() => setEditing(null)}>إلغاء</button>
              <button className="btn" onClick={save} type="submit" form="supplier-form">حفظ</button>
            </>
          }
        >
          <form id="supplier-form" onSubmit={save}>
            {saveError && <ErrorBanner message={saveError} />}
            <Field label="الاسم">
              <input className="input" value={editing.name} onChange={(e) => setEditing({ ...editing, name: e.target.value })} required />
            </Field>
            <Field label="الهاتف">
              <input className="input" value={editing.phone} onChange={(e) => setEditing({ ...editing, phone: e.target.value })} />
            </Field>
            <Field label="البريد الإلكتروني">
              <input className="input" type="email" value={editing.email} onChange={(e) => setEditing({ ...editing, email: e.target.value })} />
            </Field>
            <Field label="العنوان">
              <textarea className="input" rows={3} value={editing.address} onChange={(e) => setEditing({ ...editing, address: e.target.value })} />
            </Field>
            {editing.id && (
              <Field label="الحالة">
                <select className="select" value={editing.isActive ? '1' : '0'} onChange={(e) => setEditing({ ...editing, isActive: e.target.value === '1' })}>
                  <option value="1">مفعّل</option>
                  <option value="0">معطّل</option>
                </select>
              </Field>
            )}
          </form>
        </Modal>
      )}
    </div>
  );
}
