import { useState, type FormEvent } from 'react';
import { unitsApi } from '../../api/endpoints';
import type { Unit } from '../../api/models';
import { useAuth } from '../../auth/AuthContext';
import { P } from '../../auth/permissions';
import { useAsync } from '../../lib/useAsync';
import { errorMessage } from '../../lib/format';
import { DataTable, ErrorBanner, Field, Loading, Modal, PageHeader } from '../../components/ui';

interface Editing {
  id?: number;
  name: string;
  symbol: string;
  precision: number;
  isActive: boolean;
}

const blank: Editing = { name: '', symbol: '', precision: 0, isActive: true };

export function UnitsPage() {
  const { hasPermission } = useAuth();
  const { data, loading, error, reload } = useAsync(() => unitsApi.list(), []);
  const [editing, setEditing] = useState<Editing | null>(null);
  const [saveError, setSaveError] = useState<string | null>(null);

  const canCreate = hasPermission(P.units.create);
  const canUpdate = hasPermission(P.units.update);
  const canDelete = hasPermission(P.units.delete);

  async function save(e: FormEvent) {
    e.preventDefault();
    if (!editing) return;
    setSaveError(null);
    const body = {
      name: editing.name.trim(),
      symbol: editing.symbol.trim() || null,
      precision: editing.precision,
    };
    try {
      if (editing.id) {
        await unitsApi.update(editing.id, { ...body, isActive: editing.isActive });
      } else {
        await unitsApi.create(body);
      }
      setEditing(null);
      reload();
    } catch (err) {
      setSaveError(errorMessage(err));
    }
  }

  async function remove(u: Unit) {
    if (!confirm(`حذف وحدة القياس "${u.name}"؟`)) return;
    try {
      await unitsApi.remove(u.id);
      reload();
    } catch (err) {
      alert(errorMessage(err));
    }
  }

  return (
    <div className="stack">
      <PageHeader
        title="وحدات القياس"
        actions={canCreate && <button className="btn" onClick={() => setEditing({ ...blank })}>+ وحدة جديدة</button>}
      />
      {loading && <Loading />}
      {error && <ErrorBanner message={error} />}
      {data && (
        <DataTable
          rows={data}
          rowKey={(u) => u.id}
          columns={[
            { header: 'الاسم', render: (u) => u.name },
            { header: 'الرمز', render: (u) => u.symbol ?? '-' },
            { header: 'الدقة', render: (u) => u.precision },
            { header: 'الحالة', render: (u) => (u.isActive ? <span className="badge">مفعّل</span> : 'معطّل') },
            {
              header: '',
              render: (u) => (
                <div className="row">
                  {canUpdate && (
                    <button
                      className="btn btn-secondary btn-sm"
                      onClick={() => setEditing({ id: u.id, name: u.name, symbol: u.symbol ?? '', precision: u.precision, isActive: u.isActive })}
                    >
                      تعديل
                    </button>
                  )}
                  {canDelete && (
                    <button className="btn btn-danger btn-sm" onClick={() => remove(u)}>حذف</button>
                  )}
                </div>
              ),
            },
          ]}
        />
      )}

      {editing && (
        <Modal
          title={editing.id ? 'تعديل وحدة قياس' : 'وحدة قياس جديدة'}
          onClose={() => setEditing(null)}
          footer={
            <>
              <button className="btn btn-secondary" onClick={() => setEditing(null)}>إلغاء</button>
              <button className="btn" onClick={save} type="submit" form="unit-form">حفظ</button>
            </>
          }
        >
          <form id="unit-form" onSubmit={save}>
            {saveError && <ErrorBanner message={saveError} />}
            <Field label="الاسم">
              <input className="input" value={editing.name} onChange={(e) => setEditing({ ...editing, name: e.target.value })} required />
            </Field>
            <Field label="الرمز">
              <input className="input" value={editing.symbol} onChange={(e) => setEditing({ ...editing, symbol: e.target.value })} />
            </Field>
            <Field label="الدقة (عدد المنازل العشرية، 0–6)">
              <input
                className="input"
                type="number"
                min={0}
                max={6}
                value={editing.precision}
                onChange={(e) => setEditing({ ...editing, precision: Number(e.target.value) })}
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
