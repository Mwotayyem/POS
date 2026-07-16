import { Navigate, Route, Routes } from 'react-router-dom';
import { useAuth } from './auth/AuthContext';
import { AppLayout } from './layout/AppLayout';
import { Loading } from './components/ui';
import { LoginPage } from './pages/LoginPage';
import { DashboardPage } from './pages/DashboardPage';
import { ProfilePage } from './pages/ProfilePage';
import { UsersPage } from './pages/admin/UsersPage';
import { RolesPage } from './pages/admin/RolesPage';
import { PermissionsPage } from './pages/admin/PermissionsPage';
import { TenantSettingsPage } from './pages/admin/TenantSettingsPage';
import { ProductsPage } from './pages/catalog/ProductsPage';
import { CategoriesPage } from './pages/catalog/CategoriesPage';
import { UnitsPage } from './pages/catalog/UnitsPage';
import { BrandsPage } from './pages/catalog/BrandsPage';
import { WarehousesPage } from './pages/inventory/WarehousesPage';
import { StockPage } from './pages/inventory/StockPage';
import { SuppliersPage } from './pages/purchasing/SuppliersPage';
import { PurchaseInvoicesPage } from './pages/purchasing/PurchaseInvoicesPage';
import { CustomersPage } from './pages/sales/CustomersPage';
import { SalesInvoicesPage } from './pages/sales/SalesInvoicesPage';
import { ReportsPage } from './pages/ReportsPage';

export function App() {
  const { profile, loading } = useAuth();

  if (loading) {
    return <Loading label="جارٍ التحميل…" />;
  }

  if (!profile) {
    return (
      <Routes>
        <Route path="/login" element={<LoginPage />} />
        <Route path="*" element={<Navigate to="/login" replace />} />
      </Routes>
    );
  }

  return (
    <AppLayout>
      <Routes>
        <Route path="/" element={<Navigate to="/dashboard" replace />} />
        <Route path="/dashboard" element={<DashboardPage />} />
        <Route path="/profile" element={<ProfilePage />} />

        <Route path="/catalog/products" element={<ProductsPage />} />
        <Route path="/catalog/categories" element={<CategoriesPage />} />
        <Route path="/catalog/units" element={<UnitsPage />} />
        <Route path="/catalog/brands" element={<BrandsPage />} />

        <Route path="/inventory/warehouses" element={<WarehousesPage />} />
        <Route path="/inventory/stock" element={<StockPage />} />

        <Route path="/purchasing/suppliers" element={<SuppliersPage />} />
        <Route path="/purchasing/invoices" element={<PurchaseInvoicesPage />} />

        <Route path="/sales/customers" element={<CustomersPage />} />
        <Route path="/sales/invoices" element={<SalesInvoicesPage />} />

        <Route path="/reports" element={<ReportsPage />} />

        <Route path="/admin/users" element={<UsersPage />} />
        <Route path="/admin/roles" element={<RolesPage />} />
        <Route path="/admin/permissions" element={<PermissionsPage />} />
        <Route path="/admin/settings" element={<TenantSettingsPage />} />

        <Route path="*" element={<Navigate to="/dashboard" replace />} />
      </Routes>
    </AppLayout>
  );
}
