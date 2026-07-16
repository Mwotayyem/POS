import { useState, type FormEvent } from 'react';
import { warehousesApi } from '../../api/endpoints';
import type { Warehouse } from '../../api/models';
import { useAuth } from '../../auth/AuthContext';
import { P } from '../../auth/permissions';
import { useAsync } from '../../lib/useAsync';
import { errorMessage } from '../../lib/format';
import { DataTable, ErrorBanner, Field, Loading, Modal, PageHeader } from '../../components/ui';

interface Editing {
  id?: number;
  name: string;
  code: string;
  address: string;
  isDefault: boolean;
  isActive: boolean;
}

const blank: Editing = { name: '', code: '', address: '', isDefault: false, isActive: true };

export function WarehousesPage() {
  const { hasPermission } = useAuth();
  const { data, loading, error, reload } = useAsync(() => warehousesApi.list(), []);
  const [editing, setEditing] = useState<Editing | null>(null);
  const [saveError, setSaveError] = useState<string | null>(null);

  const canCreate = hasPermission(P.warehouses.create);
  const canUpdate = hasPermission(P.warehouses.update);
  const canDelete = hasPermission(P.warehouses.delete);

  async function save(e: FormEvent) {
    e.preventDefault();
    if (!editing) return;
    setSaveError(null);
    const body = {
      name: editing.name.trim(),
      code: editing.code.trim() || null,
      address: editing.address.trim() || null,
      isDefault: editing.isDefault,
    };
    try {
      if (editing.id) {
        await warehousesApi.update(editing.id, { ...body, isActive: editing.isActive });
      } else {
        await warehousesApi.create(body);
      }
      setEditing(null);
      reload();
    } catch (err) {
      setSaveError(errorMessage(err));
    }
  }

  async function remove(w: Warehouse) {
    if (!confirm(`حذف المستودع "${w.name}"؟`)) return;
    try {
      await warehousesApi.remove(w.id);
      reload();
    } catch (err) {
      alert(errorMessage(err));
    }
  }

  return (
    <div className="stack">
      <PageHeader
        title="المستودعات"
        actions={canCreate && <button className="btn" onClick={() => setEditing({ ...blank })}>+ مستودع جديد</button>}
      />
      {loading && <Loading />}
      {error && <ErrorBanner message={error} />}
      {data && (
        <DataTable
          rows={data}
          rowKey={(w) => w.id}
          columns={[
            { header: 'الاسم', render: (w) => w.name },
            { header: 'الرمز', render: (w) => w.code ?? '-' },
            { header: 'العنوان', render: (w) => w.address ?? '-' },
            { header: 'افتراضي', render: (w) => (w.isDefault ? <span className="badge">افتراضي</span> : '-') },
            { header: 'الحالة', render: (w) => (w.isActive ? <span className="badge">مفعّل</span> : 'معطّل') },
            {
              header: '',
              render: (w) => (
                <div className="row">
                  {canUpdate && (
                    <button
                      className="btn btn-secondary btn-sm"
                      onClick={() => setEditing({ id: w.id, name: w.name, code: w.code ?? '', address: w.address ?? '', isDefault: w.isDefault, isActive: w.isActive })}
                    >
                      تعديل
                    </button>
                  )}
                  {canDelete && (
                    <button className="btn btn-danger btn-sm" onClick={() => remove(w)}>حذف</button>
                  )}
                </div>
              ),
            },
          ]}
        />
      )}

      {editing && (
        <Modal
          title={editing.id ? 'تعديل مستودع' : 'مستودع جديد'}
          onClose={() => setEditing(null)}
          footer={
            <>
              <button className="btn btn-secondary" onClick={() => setEditing(null)}>إلغاء</button>
              <button className="btn" onClick={save} type="submit" form="warehouse-form">حفظ</button>
            </>
          }
        >
          <form id="warehouse-form" onSubmit={save}>
            {saveError && <ErrorBanner message={saveError} />}
            <Field label="الاسم">
              <input className="input" value={editing.name} onChange={(e) => setEditing({ ...editing, name: e.target.value })} required />
            </Field>
            <Field label="الرمز">
              <input className="input" value={editing.code} onChange={(e) => setEditing({ ...editing, code: e.target.value })} />
            </Field>
            <Field label="العنوان">
              <textarea className="input" rows={3} value={editing.address} onChange={(e) => setEditing({ ...editing, address: e.target.value })} />
            </Field>
            <Field label="افتراضي">
              <select className="select" value={editing.isDefault ? '1' : '0'} onChange={(e) => setEditing({ ...editing, isDefault: e.target.value === '1' })}>
                <option value="0">لا</option>
                <option value="1">نعم</option>
              </select>
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
