import { useState, type FormEvent } from 'react';
import { brandsApi, categoriesApi, productsApi, unitsApi } from '../../api/endpoints';
import type { ProductListItem } from '../../api/models';
import { useAuth } from '../../auth/AuthContext';
import { P } from '../../auth/permissions';
import { useAsync } from '../../lib/useAsync';
import { errorMessage, money } from '../../lib/format';
import { DataTable, ErrorBanner, Field, Loading, Modal, PageHeader } from '../../components/ui';

interface Editing {
  id?: number;
  name: string;
  sku: string;
  categoryId: string;
  brandId: string;
  baseUnitId: string;
  costPrice: number;
  salePrice: number;
  taxRate: number;
  reorderLevel: number;
  trackStock: boolean;
  isActive: boolean;
}

const blank: Editing = {
  name: '',
  sku: '',
  categoryId: '',
  brandId: '',
  baseUnitId: '',
  costPrice: 0,
  salePrice: 0,
  taxRate: 0,
  reorderLevel: 0,
  trackStock: true,
  isActive: true,
};

export function ProductsPage() {
  const { hasPermission } = useAuth();
  const { data, loading, error, reload } = useAsync(() => productsApi.list(), []);
  const categories = useAsync(() => categoriesApi.list(), []);
  const units = useAsync(() => unitsApi.list(), []);
  const brands = useAsync(() => brandsApi.list(), []);
  const [editing, setEditing] = useState<Editing | null>(null);
  const [saveError, setSaveError] = useState<string | null>(null);

  const canCreate = hasPermission(P.products.create);
  const canUpdate = hasPermission(P.products.update);
  const canDelete = hasPermission(P.products.delete);

  const categoryList = categories.data ?? [];
  const unitList = units.data ?? [];
  const brandList = brands.data ?? [];

  const categoryName = (id?: number | null) => categoryList.find((c) => c.id === id)?.name ?? '-';
  const brandName = (id?: number | null) => brandList.find((b) => b.id === id)?.name ?? '-';
  const unitName = (id?: number | null) => unitList.find((u) => u.id === id)?.name ?? '-';

  async function save(e: FormEvent) {
    e.preventDefault();
    if (!editing) return;
    setSaveError(null);
    if (!editing.baseUnitId) {
      setSaveError('يجب اختيار وحدة القياس الأساسية.');
      return;
    }
    const body = {
      name: editing.name.trim(),
      sku: editing.sku.trim() || null,
      categoryId: editing.categoryId ? Number(editing.categoryId) : null,
      brandId: editing.brandId ? Number(editing.brandId) : null,
      baseUnitId: Number(editing.baseUnitId),
      costPrice: editing.costPrice,
      salePrice: editing.salePrice,
      taxRate: editing.taxRate,
      reorderLevel: editing.reorderLevel,
      trackStock: editing.trackStock,
      customFieldsJson: null,
      units: [],
      barcodes: [],
      prices: [],
    };
    try {
      if (editing.id) {
        await productsApi.update(editing.id, { ...body, isActive: editing.isActive });
      } else {
        await productsApi.create(body);
      }
      setEditing(null);
      reload();
    } catch (err) {
      setSaveError(errorMessage(err));
    }
  }

  async function remove(p: ProductListItem) {
    if (!confirm(`حذف المنتج "${p.name}"؟`)) return;
    try {
      await productsApi.remove(p.id);
      reload();
    } catch (err) {
      alert(errorMessage(err));
    }
  }

  return (
    <div className="stack">
      <PageHeader
        title="المنتجات"
        actions={canCreate && <button className="btn" onClick={() => setEditing({ ...blank })}>+ منتج جديد</button>}
      />
      {loading && <Loading />}
      {error && <ErrorBanner message={error} />}
      {data && (
        <DataTable
          rows={data}
          rowKey={(p) => p.id}
          columns={[
            { header: 'الاسم', render: (p) => p.name },
            { header: 'SKU', render: (p) => p.sku ?? '-' },
            { header: 'الفئة', render: (p) => categoryName(p.categoryId) },
            { header: 'العلامة التجارية', render: (p) => brandName(p.brandId) },
            { header: 'وحدة القياس', render: (p) => unitName(p.baseUnitId) },
            { header: 'التكلفة', render: (p) => money(p.costPrice) },
            { header: 'سعر البيع', render: (p) => money(p.salePrice) },
            { header: 'الحالة', render: (p) => (p.isActive ? <span className="badge">مفعّل</span> : 'معطّل') },
            {
              header: '',
              render: (p) => (
                <div className="row">
                  {canUpdate && (
                    <button
                      className="btn btn-secondary btn-sm"
                      onClick={() =>
                        setEditing({
                          id: p.id,
                          name: p.name,
                          sku: p.sku ?? '',
                          categoryId: p.categoryId != null ? String(p.categoryId) : '',
                          brandId: p.brandId != null ? String(p.brandId) : '',
                          baseUnitId: String(p.baseUnitId),
                          costPrice: p.costPrice,
                          salePrice: p.salePrice,
                          taxRate: p.taxRate,
                          reorderLevel: 0,
                          trackStock: p.trackStock,
                          isActive: p.isActive,
                        })
                      }
                    >
                      تعديل
                    </button>
                  )}
                  {canDelete && (
                    <button className="btn btn-danger btn-sm" onClick={() => remove(p)}>حذف</button>
                  )}
                </div>
              ),
            },
          ]}
        />
      )}

      {editing && (
        <Modal
          title={editing.id ? 'تعديل منتج' : 'منتج جديد'}
          onClose={() => setEditing(null)}
          footer={
            <>
              <button className="btn btn-secondary" onClick={() => setEditing(null)}>إلغاء</button>
              <button className="btn" onClick={save} type="submit" form="product-form" disabled={!editing.baseUnitId}>حفظ</button>
            </>
          }
        >
          <form id="product-form" onSubmit={save}>
            {saveError && <ErrorBanner message={saveError} />}
            <Field label="الاسم">
              <input className="input" value={editing.name} onChange={(e) => setEditing({ ...editing, name: e.target.value })} required />
            </Field>
            <Field label="SKU">
              <input className="input" value={editing.sku} onChange={(e) => setEditing({ ...editing, sku: e.target.value })} />
            </Field>
            <Field label="الفئة">
              <select className="select" value={editing.categoryId} onChange={(e) => setEditing({ ...editing, categoryId: e.target.value })}>
                <option value="">— بدون —</option>
                {categoryList.map((c) => (
                  <option key={c.id} value={c.id}>{c.name}</option>
                ))}
              </select>
            </Field>
            <Field label="العلامة التجارية">
              <select className="select" value={editing.brandId} onChange={(e) => setEditing({ ...editing, brandId: e.target.value })}>
                <option value="">— بدون —</option>
                {brandList.map((b) => (
                  <option key={b.id} value={b.id}>{b.name}</option>
                ))}
              </select>
            </Field>
            <Field label="وحدة القياس الأساسية" error={!editing.baseUnitId ? 'مطلوب' : undefined}>
              <select className="select" value={editing.baseUnitId} onChange={(e) => setEditing({ ...editing, baseUnitId: e.target.value })} required>
                <option value="">— اختر —</option>
                {unitList.map((u) => (
                  <option key={u.id} value={u.id}>{u.name}</option>
                ))}
              </select>
            </Field>
            <Field label="سعر التكلفة">
              <input className="input" type="number" value={editing.costPrice} onChange={(e) => setEditing({ ...editing, costPrice: Number(e.target.value) })} />
            </Field>
            <Field label="سعر البيع">
              <input className="input" type="number" value={editing.salePrice} onChange={(e) => setEditing({ ...editing, salePrice: Number(e.target.value) })} />
            </Field>
            <Field label="نسبة الضريبة (%)">
              <input className="input" type="number" value={editing.taxRate} onChange={(e) => setEditing({ ...editing, taxRate: Number(e.target.value) })} />
            </Field>
            <Field label="حدّ إعادة الطلب">
              <input className="input" type="number" value={editing.reorderLevel} onChange={(e) => setEditing({ ...editing, reorderLevel: Number(e.target.value) })} />
            </Field>
            <Field label="تتبّع المخزون">
              <select className="select" value={editing.trackStock ? '1' : '0'} onChange={(e) => setEditing({ ...editing, trackStock: e.target.value === '1' })}>
                <option value="1">نعم</option>
                <option value="0">لا</option>
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
