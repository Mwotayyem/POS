import { permissionsApi } from '../../api/endpoints';
import { useAsync } from '../../lib/useAsync';
import { DataTable, ErrorBanner, Loading, PageHeader } from '../../components/ui';

export function PermissionsPage() {
  const { data, loading, error } = useAsync(() => permissionsApi.list(), []);

  // Group by module for readability.
  const grouped = (data ?? []).reduce<Record<string, typeof data>>((acc, p) => {
    (acc[p.module] ??= []).push(p);
    return acc;
  }, {});

  return (
    <div className="stack">
      <PageHeader title="الصلاحيات" />
      <p className="muted">قائمة الصلاحيات المعرّفة في النظام (تُسنَد عبر الأدوار).</p>
      {loading && <Loading />}
      {error && <ErrorBanner message={error} />}
      {data &&
        Object.entries(grouped).map(([module, perms]) => (
          <section key={module} className="stack">
            <h2 style={{ fontSize: 16 }}>{module}</h2>
            <DataTable
              rows={perms ?? []}
              rowKey={(p) => p.code}
              columns={[
                { header: 'الوصف', render: (p) => p.displayName },
                { header: 'المفتاح', render: (p) => <code dir="ltr">{p.code}</code> },
              ]}
            />
          </section>
        ))}
    </div>
  );
}
