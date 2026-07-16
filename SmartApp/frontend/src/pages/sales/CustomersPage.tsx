import { useState, type FormEvent } from 'react';
import { customersApi } from '../../api/endpoints';
import type { Customer } from '../../api/models';
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
  creditLimit: number;
  isActive: boolean;
}

const blank: Editing = { name: '', phone: '', email: '', address: '', creditLimit: 0, isActive: true };

export function CustomersPage() {
  const { hasPermission } = useAuth();
  const [search, setSearch] = useState('');
  const { data, loading, error, reload } = useAsync(() => customersApi.list(search), [search]);
  const [editing, setEditing] = useState<Editing | null>(null);
  const [saveError, setSaveError] = useState<string | null>(null);

  const canCreate = hasPermission(P.customers.create);
  const canUpdate = hasPermission(P.customers.update);
  const canDelete = hasPermission(P.customers.delete);

  async function save(e: FormEvent) {
    e.preventDefault();
    if (!editing) return;
    setSaveError(null);
    const body = {
      name: editing.name.trim(),
      phone: editing.phone.trim() || null,
      email: editing.email.trim() || null,
      address: editing.address.trim() || null,
      creditLimit: editing.creditLimit,
    };
    try {
      if (editing.id) {
        await customersApi.update(editing.id, { ...body, isActive: editing.isActive });
      } else {
        await customersApi.create(body);
      }
      setEditing(null);
      reload();
    } catch (err) {
      setSaveError(errorMessage(err));
    }
  }

  async function remove(c: Customer) {
    if (!confirm(`حذف العميل "${c.name}"؟`)) return;
    try {
      await customersApi.remove(c.id);
      reload();
    } catch (err) {
      alert(errorMessage(err));
    }
  }

  return (
    <div className="stack">
      <PageHeader
        title="العملاء"
        actions={canCreate && <button className="btn" onClick={() => setEditing({ ...blank })}>+ عميل جديد</button>}
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
          rowKey={(c) => c.id}
          columns={[
            { header: 'الاسم', render: (c) => c.name },
            { header: 'الهاتف', render: (c) => c.phone ?? '-' },
            { header: 'الرصيد', render: (c) => money(c.balance) },
            { header: 'حدّ الائتمان', render: (c) => money(c.creditLimit) },
            { header: 'الحالة', render: (c) => (c.isActive ? <span className="badge">مفعّل</span> : 'معطّل') },
            {
              header: '',
              render: (c) => (
                <div className="row">
                  {canUpdate && (
                    <button
                      className="btn btn-secondary btn-sm"
                      onClick={() => setEditing({ id: c.id, name: c.name, phone: c.phone ?? '', email: c.email ?? '', address: c.address ?? '', creditLimit: c.creditLimit, isActive: c.isActive })}
                    >
                      تعديل
                    </button>
                  )}
                  {canDelete && (
                    <button className="btn btn-danger btn-sm" onClick={() => remove(c)}>حذف</button>
                  )}
                </div>
              ),
            },
          ]}
        />
      )}

      {editing && (
        <Modal
          title={editing.id ? 'تعديل عميل' : 'عميل جديد'}
          onClose={() => setEditing(null)}
          footer={
            <>
              <button className="btn btn-secondary" onClick={() => setEditing(null)}>إلغاء</button>
              <button className="btn" onClick={save} type="submit" form="customer-form">حفظ</button>
            </>
          }
        >
          <form id="customer-form" onSubmit={save}>
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
            <Field label="حدّ الائتمان">
              <input
                className="input"
                type="number"
                value={editing.creditLimit}
                onChange={(e) => setEditing({ ...editing, creditLimit: Number(e.target.value) })}
              />
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
