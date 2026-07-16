import { purchaseInvoicesApi } from '../../api/endpoints';
import { useAsync } from '../../lib/useAsync';
import { date, money } from '../../lib/format';
import { DataTable, ErrorBanner, Loading, PageHeader } from '../../components/ui';

function statusLabel(status: number): string {
  switch (status) {
    case 1:
      return 'مسودة';
    case 2:
      return 'مؤكدة';
    case 3:
      return 'مرتجعة جزئياً';
    case 4:
      return 'مرتجعة كلياً';
    case 5:
      return 'ملغاة';
    default:
      return '-';
  }
}

export function PurchaseInvoicesPage() {
  const { data, loading, error } = useAsync(() => purchaseInvoicesApi.list(), []);

  return (
    <div className="stack">
      <PageHeader title="فواتير الشراء" />
      {loading && <Loading />}
      {error && <ErrorBanner message={error} />}
      {data && (
        <DataTable
          rows={data}
          rowKey={(r) => r.id}
          empty="لا توجد فواتير شراء."
          columns={[
            { header: 'رقم الفاتورة', render: (r) => r.invoiceNumber },
            { header: 'المورّد', render: (r) => r.supplierName },
            { header: 'التاريخ', render: (r) => date(r.invoiceDate) },
            { header: 'الحالة', render: (r) => statusLabel(r.status) },
            { header: 'الإجمالي', render: (r) => money(r.grandTotal) },
            { header: 'المدفوع', render: (r) => money(r.paidAmount) },
          ]}
        />
      )}
    </div>
  );
}
