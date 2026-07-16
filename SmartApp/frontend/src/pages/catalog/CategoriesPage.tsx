import { useState, type FormEvent } from 'react';
import { categoriesApi } from '../../api/endpoints';
import type { Category } from '../../api/models';
import { useAuth } from '../../auth/AuthContext';
import { P } from '../../auth/permissions';
import { useAsync } from '../../lib/useAsync';
import { errorMessage } from '../../lib/format';
import { DataTable, ErrorBanner, Field, Loading, Modal, PageHeader } from '../../components/ui';

interface Editing {
  id?: number;
  name: string;
  parentId: string;
  code: string;
  sortOrder: number;
  isActive: boolean;
}

const blank: Editing = { name: '', parentId: '', code: '', sortOrder: 0, isActive: true };

export function CategoriesPage() {
  const { hasPermission } = useAuth();
  const { data, loading, error, reload } = useAsync(() => categoriesApi.list(), []);
  const [editing, setEditing] = useState<Editing | null>(null);
  const [saveError, setSaveError] = useState<string | null>(null);

  const canCreate = hasPermission(P.categories.create);
  const canUpdate = hasPermission(P.categories.update);
  const canDelete = hasPermission(P.categories.delete);

  const categories = data ?? [];
  const nameById = (id?: number | null) => categories.find((c) => c.id === id)?.name ?? '-';

  async function save(e: FormEvent) {
    e.preventDefault();
    if (!editing) return;
    setSaveError(null);
    const parentId = editing.parentId ? Number(editing.parentId) : null;
    const body = {
      name: editing.name.trim(),
      parentId,
      code: editing.code.trim() || null,
      sortOrder: editing.sortOrder,
    };
    try {
      if (editing.id) {
        await categoriesApi.update(editing.id, { ...body, isActive: editing.isActive });
      } else {
        await categoriesApi.create(body);
      }
      setEditing(null);
      reload();
    } catch (err) {
      setSaveError(errorMessage(err));
    }
  }

  async function remove(c: Category) {
    if (!confirm(`حذف الفئة "${c.name}"؟`)) return;
    try {
      await categoriesApi.remove(c.id);
      reload();
    } catch (err) {
      alert(errorMessage(err));
    }
  }

  return (
    <div className="stack">
      <PageHeader
        title="الفئات"
        actions={canCreate && <button className="btn" onClick={() => setEditing({ ...blank })}>+ فئة جديدة</button>}
      />
      {loading && <Loading />}
      {error && <ErrorBanner message={error} />}
      {data && (
        <DataTable
          rows={data}
          rowKey={(c) => c.id}
          columns={[
            { header: 'الاسم', render: (c) => c.name },
            { header: 'الفئة الأصل', render: (c) => nameById(c.parentId) },
            { header: 'الرمز', render: (c) => c.code ?? '-' },
            { header: 'الترتيب', render: (c) => c.sortOrder },
            { header: 'الحالة', render: (c) => (c.isActive ? <span className="badge">مفعّل</span> : 'معطّل') },
            {
              header: '',
              render: (c) => (
                <div className="row">
                  {canUpdate && (
                    <button
                      className="btn btn-secondary btn-sm"
                      onClick={() => setEditing({ id: c.id, name: c.name, parentId: c.parentId != null ? String(c.parentId) : '', code: c.code ?? '', sortOrder: c.sortOrder, isActive: c.isActive })}
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
          title={editing.id ? 'تعديل فئة' : 'فئة جديدة'}
          onClose={() => setEditing(null)}
          footer={
            <>
              <button className="btn btn-secondary" onClick={() => setEditing(null)}>إلغاء</button>
              <button className="btn" onClick={save} type="submit" form="category-form">حفظ</button>
            </>
          }
        >
          <form id="category-form" onSubmit={save}>
            {saveError && <ErrorBanner message={saveError} />}
            <Field label="الاسم">
              <input className="input" value={editing.name} onChange={(e) => setEditing({ ...editing, name: e.target.value })} required />
            </Field>
            <Field label="الفئة الأصل">
              <select className="select" value={editing.parentId} onChange={(e) => setEditing({ ...editing, parentId: e.target.value })}>
                <option value="">— بدون —</option>
                {categories
                  .filter((c) => c.id !== editing.id)
                  .map((c) => (
                    <option key={c.id} value={c.id}>{c.name}</option>
                  ))}
              </select>
            </Field>
            <Field label="الرمز">
              <input className="input" value={editing.code} onChange={(e) => setEditing({ ...editing, code: e.target.value })} />
            </Field>
            <Field label="الترتيب">
              <input
                className="input"
                type="number"
                value={editing.sortOrder}
                onChange={(e) => setEditing({ ...editing, sortOrder: Number(e.target.value) })}
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
