import { stockApi } from '../../api/endpoints';
import { useAsync } from '../../lib/useAsync';
import { money, num } from '../../lib/format';
import { DataTable, ErrorBanner, Loading, PageHeader } from '../../components/ui';

export function StockPage() {
  const { data, loading, error } = useAsync(() => stockApi.balances(), []);

  return (
    <div className="stack">
      <PageHeader title="أرصدة المخزون" />
      {loading && <Loading />}
      {error && <ErrorBanner message={error} />}
      {data && (
        <DataTable
          rows={data}
          rowKey={(r) => r.id}
          empty="لا توجد أرصدة مخزون."
          columns={[
            { header: 'المنتج', render: (r) => r.productName },
            { header: 'المستودع', render: (r) => r.warehouseName },
            { header: 'الكمية المتوفّرة', render: (r) => num(r.qtyOnHand) },
            { header: 'متوسط التكلفة', render: (r) => money(r.avgCost) },
          ]}
        />
      )}
    </div>
  );
}
