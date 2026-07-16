import { useState, type FormEvent } from 'react';
import { brandsApi } from '../../api/endpoints';
import type { Brand } from '../../api/models';
import { useAuth } from '../../auth/AuthContext';
import { P } from '../../auth/permissions';
import { useAsync } from '../../lib/useAsync';
import { errorMessage } from '../../lib/format';
import { DataTable, ErrorBanner, Field, Loading, Modal, PageHeader } from '../../components/ui';

interface Editing {
  id?: number;
  name: string;
  code: string;
  description: string;
  isActive: boolean;
}

const blank: Editing = { name: '', code: '', description: '', isActive: true };

export function BrandsPage() {
  const { hasPermission } = useAuth();
  const { data, loading, error, reload } = useAsync(() => brandsApi.list(), []);
  const [editing, setEditing] = useState<Editing | null>(null);
  const [saveError, setSaveError] = useState<string | null>(null);

  const canCreate = hasPermission(P.brands.create);
  const canUpdate = hasPermission(P.brands.update);
  const canDelete = hasPermission(P.brands.delete);

  async function save(e: FormEvent) {
    e.preventDefault();
    if (!editing) return;
    setSaveError(null);
    const body = {
      name: editing.name.trim(),
      code: editing.code.trim() || null,
      description: editing.description.trim() || null,
    };
    try {
      if (editing.id) {
        await brandsApi.update(editing.id, { ...body, isActive: editing.isActive });
      } else {
        await brandsApi.create(body);
      }
      setEditing(null);
      reload();
    } catch (err) {
      setSaveError(errorMessage(err));
    }
  }

  async function remove(b: Brand) {
    if (!confirm(`حذف العلامة التجارية "${b.name}"؟`)) return;
    try {
      await brandsApi.remove(b.id);
      reload();
    } catch (err) {
      alert(errorMessage(err));
    }
  }

  return (
    <div className="stack">
      <PageHeader
        title="العلامات التجارية"
        actions={canCreate && <button className="btn" onClick={() => setEditing({ ...blank })}>+ علامة جديدة</button>}
      />
      {loading && <Loading />}
      {error && <ErrorBanner message={error} />}
      {data && (
        <DataTable
          rows={data}
          rowKey={(b) => b.id}
          columns={[
            { header: 'الاسم', render: (b) => b.name },
            { header: 'الرمز', render: (b) => b.code ?? '-' },
            { header: 'الوصف', render: (b) => b.description ?? '-' },
            { header: 'الحالة', render: (b) => (b.isActive ? <span className="badge">مفعّل</span> : 'معطّل') },
            {
              header: '',
              render: (b) => (
                <div className="row">
                  {canUpdate && (
                    <button
                      className="btn btn-secondary btn-sm"
                      onClick={() => setEditing({ id: b.id, name: b.name, code: b.code ?? '', description: b.description ?? '', isActive: b.isActive })}
                    >
                      تعديل
                    </button>
                  )}
                  {canDelete && (
                    <button className="btn btn-danger btn-sm" onClick={() => remove(b)}>حذف</button>
                  )}
                </div>
              ),
            },
          ]}
        />
      )}

      {editing && (
        <Modal
          title={editing.id ? 'تعديل علامة تجارية' : 'علامة تجارية جديدة'}
          onClose={() => setEditing(null)}
          footer={
            <>
              <button className="btn btn-secondary" onClick={() => setEditing(null)}>إلغاء</button>
              <button className="btn" onClick={save} type="submit" form="brand-form">حفظ</button>
            </>
          }
        >
          <form id="brand-form" onSubmit={save}>
            {saveError && <ErrorBanner message={saveError} />}
            <Field label="الاسم">
              <input className="input" value={editing.name} onChange={(e) => setEditing({ ...editing, name: e.target.value })} required />
            </Field>
            <Field label="الرمز">
              <input className="input" value={editing.code} onChange={(e) => setEditing({ ...editing, code: e.target.value })} />
            </Field>
            <Field label="الوصف">
              <textarea className="input" rows={3} value={editing.description} onChange={(e) => setEditing({ ...editing, description: e.target.value })} />
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
