import { reportsApi } from '../api/endpoints';
import { useAsync } from '../lib/useAsync';
import { money, num } from '../lib/format';
import { DataTable, ErrorBanner, Loading, PageHeader } from '../components/ui';

export function ReportsPage() {
  const lowStock = useAsync(() => reportsApi.lowStock(), []);
  const products = useAsync(() => reportsApi.productSales(), []);

  return (
    <div className="stack">
      <PageHeader title="التقارير" />

      <section className="stack">
        <h2 style={{ fontSize: 17 }}>المخزون المنخفض</h2>
        {lowStock.loading && <Loading />}
        {lowStock.error && <ErrorBanner message={lowStock.error} />}
        {lowStock.data && (
          <DataTable
            rows={lowStock.data}
            rowKey={(r) => r.productId}
            empty="لا توجد منتجات تحت حدّ إعادة الطلب."
            columns={[
              { header: 'المنتج', render: (r) => r.productName },
              { header: 'SKU', render: (r) => r.sku ?? '-' },
              { header: 'المتوفّر', render: (r) => num(r.qtyOnHand) },
              { header: 'حدّ الطلب', render: (r) => num(r.reorderLevel) },
            ]}
          />
        )}
      </section>

      <section className="stack">
        <h2 style={{ fontSize: 17 }}>أداء المنتجات (آخر 30 يوماً)</h2>
        {products.loading && <Loading />}
        {products.error && <ErrorBanner message={products.error} />}
        {products.data && (
          <DataTable
            rows={products.data}
            rowKey={(r) => r.productId}
            empty="لا توجد مبيعات في الفترة."
            columns={[
              { header: 'المنتج', render: (r) => r.productName },
              { header: 'الكمية المباعة', render: (r) => num(r.quantitySold) },
              { header: 'الإيراد', render: (r) => money(r.revenue) },
              { header: 'التكلفة', render: (r) => money(r.cost) },
              { header: 'الربح', render: (r) => money(r.profit) },
            ]}
          />
        )}
      </section>
    </div>
  );
}
