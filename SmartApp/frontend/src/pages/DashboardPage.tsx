import { reportsApi } from '../api/endpoints';
import { useAsync } from '../lib/useAsync';
import { money } from '../lib/format';
import { ErrorBanner, Loading, PageHeader } from '../components/ui';

function StatCard({ label, value, accent }: { label: string; value: string; accent?: string }) {
  return (
    <div className="card" style={{ flex: '1 1 180px', minWidth: 180 }}>
      <div className="muted" style={{ fontSize: 13 }}>{label}</div>
      <div style={{ fontSize: 26, fontWeight: 700, marginTop: 6, color: accent }}>{value}</div>
    </div>
  );
}

export function DashboardPage() {
  const { data, loading, error } = useAsync(() => reportsApi.dashboard(), []);

  return (
    <div className="stack">
      <PageHeader title="لوحة المعلومات" />
      {loading && <Loading />}
      {error && <ErrorBanner message={error} />}
      {data && (
        <>
          <div className="row" style={{ alignItems: 'stretch' }}>
            <StatCard label="إجمالي المبيعات" value={money(data.salesTotal)} accent="var(--color-primary)" />
            <StatCard label="إجمالي المشتريات" value={money(data.purchasesTotal)} />
            <StatCard label="إجمالي الربح" value={money(data.grossProfit)} accent="var(--color-success)" />
          </div>
          <div className="row" style={{ alignItems: 'stretch' }}>
            <StatCard label="ذمم مدينة (لنا)" value={money(data.outstandingReceivables)} />
            <StatCard label="ذمم دائنة (علينا)" value={money(data.outstandingPayables)} />
            <StatCard
              label="منتجات تحت حدّ الطلب"
              value={String(data.lowStockProductCount)}
              accent={data.lowStockProductCount > 0 ? 'var(--color-warning)' : undefined}
            />
          </div>
          <div className="row" style={{ alignItems: 'stretch' }}>
            <StatCard label="عدد فواتير المبيعات" value={String(data.salesInvoiceCount)} />
            <StatCard label="عدد فواتير الشراء" value={String(data.purchaseInvoiceCount)} />
          </div>
        </>
      )}
    </div>
  );
}
