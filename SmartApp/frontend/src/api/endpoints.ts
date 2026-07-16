import { api } from './client';
import type {
  AuthTokens,
  Brand,
  Category,
  Customer,
  DashboardSummary,
  LowStockItem,
  Permission,
  ProductListItem,
  ProductSales,
  Profile,
  PurchaseInvoiceListItem,
  Role,
  SalesInvoiceListItem,
  StockBalance,
  StockMovement,
  Supplier,
  TenantSettings,
  Unit,
  User,
  Warehouse,
} from './models';

// A product with its child collections (get-by-id).
export interface Product extends ProductListItem {
  reorderLevel: number;
  units: { id: number; unitId: number; conversionFactor: number; barcode?: string | null }[];
  barcodes: { id: number; barcode: string; isPrimary: boolean }[];
  prices: { id: number; priceType: number; amount: number }[];
}

export const authApi = {
  login: (email: string, password: string) => api.post<AuthTokens>('/auth/login', { email, password }),
  logout: (refreshToken: string) => api.post<void>('/auth/logout', { refreshToken }),
};

export const profileApi = {
  me: () => api.get<Profile>('/profile'),
  update: (body: { fullName: string; phone?: string | null }) => api.put<void>('/profile', body),
  changePassword: (body: { currentPassword: string; newPassword: string }) =>
    api.post<void>('/profile/change-password', body),
};

export const usersApi = {
  list: (search?: string) => api.get<User[]>('/users', search ? { search } : undefined),
  get: (id: number) => api.get<User>(`/users/${id}`),
  create: (body: unknown) => api.post<number>('/users', body),
  update: (id: number, body: unknown) => api.put<void>(`/users/${id}`, body),
  activate: (id: number) => api.post<void>(`/users/${id}/activate`),
  deactivate: (id: number) => api.post<void>(`/users/${id}/deactivate`),
};

export const rolesApi = {
  list: () => api.get<Role[]>('/roles'),
  get: (id: number) => api.get<Role>(`/roles/${id}`),
  create: (body: unknown) => api.post<number>('/roles', body),
  update: (id: number, body: unknown) => api.put<void>(`/roles/${id}`, body),
  remove: (id: number) => api.del<void>(`/roles/${id}`),
  setPermissions: (id: number, permissions: string[]) =>
    api.put<void>(`/roles/${id}/permissions`, { permissions }),
};

export const permissionsApi = {
  list: () => api.get<Permission[]>('/permissions'),
};

export const settingsApi = {
  get: () => api.get<TenantSettings>('/tenant/settings'),
  update: (body: TenantSettings) => api.put<void>('/tenant/settings', body),
};

export const categoriesApi = {
  list: () => api.get<Category[]>('/categories'),
  create: (body: unknown) => api.post<number>('/categories', body),
  update: (id: number, body: unknown) => api.put<void>(`/categories/${id}`, body),
  remove: (id: number) => api.del<void>(`/categories/${id}`),
};

export const unitsApi = {
  list: () => api.get<Unit[]>('/units'),
  create: (body: unknown) => api.post<number>('/units', body),
  update: (id: number, body: unknown) => api.put<void>(`/units/${id}`, body),
  remove: (id: number) => api.del<void>(`/units/${id}`),
};

export const brandsApi = {
  list: () => api.get<Brand[]>('/brands'),
  create: (body: unknown) => api.post<number>('/brands', body),
  update: (id: number, body: unknown) => api.put<void>(`/brands/${id}`, body),
  remove: (id: number) => api.del<void>(`/brands/${id}`),
};

export const productsApi = {
  list: (params?: { search?: string; categoryId?: number; brandId?: number }) =>
    api.get<ProductListItem[]>('/products', params),
  get: (id: number) => api.get<Product>(`/products/${id}`),
  create: (body: unknown) => api.post<number>('/products', body),
  update: (id: number, body: unknown) => api.put<void>(`/products/${id}`, body),
  remove: (id: number) => api.del<void>(`/products/${id}`),
};

export const warehousesApi = {
  list: () => api.get<Warehouse[]>('/warehouses'),
  create: (body: unknown) => api.post<number>('/warehouses', body),
  update: (id: number, body: unknown) => api.put<void>(`/warehouses/${id}`, body),
  remove: (id: number) => api.del<void>(`/warehouses/${id}`),
};

export const stockApi = {
  balances: (params?: { warehouseId?: number; productId?: number }) =>
    api.get<StockBalance[]>('/stock/balances', params),
  movements: (productId: number, warehouseId?: number) =>
    api.get<StockMovement[]>('/stock/movements', { productId, warehouseId }),
  adjust: (body: unknown) => api.post<void>('/stock/adjust', body),
  transfer: (body: unknown) => api.post<void>('/stock/transfer', body),
};

export const suppliersApi = {
  list: (search?: string) => api.get<Supplier[]>('/suppliers', search ? { search } : undefined),
  create: (body: unknown) => api.post<number>('/suppliers', body),
  update: (id: number, body: unknown) => api.put<void>(`/suppliers/${id}`, body),
  remove: (id: number) => api.del<void>(`/suppliers/${id}`),
};

export const purchaseInvoicesApi = {
  list: (supplierId?: number) =>
    api.get<PurchaseInvoiceListItem[]>('/purchase-invoices', supplierId ? { supplierId } : undefined),
  get: (id: number) => api.get<unknown>(`/purchase-invoices/${id}`),
  create: (body: unknown) => api.post<number>('/purchase-invoices', body),
};

export const customersApi = {
  list: (search?: string) => api.get<Customer[]>('/customers', search ? { search } : undefined),
  create: (body: unknown) => api.post<number>('/customers', body),
  update: (id: number, body: unknown) => api.put<void>(`/customers/${id}`, body),
  remove: (id: number) => api.del<void>(`/customers/${id}`),
};

export const salesInvoicesApi = {
  list: (customerId?: number) =>
    api.get<SalesInvoiceListItem[]>('/sales-invoices', customerId ? { customerId } : undefined),
  get: (id: number) => api.get<unknown>(`/sales-invoices/${id}`),
  create: (body: unknown) => api.post<number>('/sales-invoices', body),
};

export const reportsApi = {
  dashboard: () => api.get<DashboardSummary>('/dashboard/summary'),
  lowStock: () => api.get<LowStockItem[]>('/reports/low-stock'),
  productSales: () => api.get<ProductSales[]>('/reports/products'),
};
