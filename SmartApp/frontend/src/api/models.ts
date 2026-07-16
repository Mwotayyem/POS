// TypeScript models mirroring the backend DTOs (subset used by the UI).

export interface AuthTokens {
  accessToken: string;
  accessTokenExpiresAtUtc: string;
  refreshToken: string;
}

export interface Profile {
  id: number;
  email: string;
  fullName: string;
  phone?: string | null;
  isSystemOwner: boolean;
  roles: string[];
  permissions: string[];
}

// ---- Administration ----
export interface RoleSummary {
  id: number;
  name: string;
}
export interface User {
  id: number;
  email: string;
  fullName: string;
  phone?: string | null;
  isActive: boolean;
  lastLoginAt?: string | null;
  roles: RoleSummary[];
}
export interface Role {
  id: number;
  name: string;
  description?: string | null;
  isSystemRole: boolean;
  permissions: string[];
}
export interface Permission {
  code: string;
  module: string;
  displayName: string;
}
export interface TenantSettings {
  currency: string;
  timeZone: string;
  defaultTaxRate: number;
  locale: string;
  themeJson?: string | null;
}

// ---- Catalog ----
export interface Category {
  id: number;
  name: string;
  parentId?: number | null;
  code?: string | null;
  sortOrder: number;
  isActive: boolean;
}
export interface Unit {
  id: number;
  name: string;
  symbol?: string | null;
  precision: number;
  isActive: boolean;
}
export interface Brand {
  id: number;
  name: string;
  code?: string | null;
  description?: string | null;
  isActive: boolean;
}
export interface ProductListItem {
  id: number;
  name: string;
  sku?: string | null;
  categoryId?: number | null;
  brandId?: number | null;
  baseUnitId: number;
  costPrice: number;
  salePrice: number;
  taxRate: number;
  isActive: boolean;
  trackStock: boolean;
}

// ---- Inventory ----
export interface Warehouse {
  id: number;
  name: string;
  code?: string | null;
  address?: string | null;
  isDefault: boolean;
  isActive: boolean;
}
export interface StockBalance {
  id: number;
  productId: number;
  productName: string;
  warehouseId: number;
  warehouseName: string;
  qtyOnHand: number;
  avgCost: number;
}
export interface StockMovement {
  id: number;
  productId: number;
  warehouseId: number;
  movementType: number;
  quantityChange: number;
  unitCost: number;
  resultingQty: number;
  resultingAvgCost: number;
  occurredAt: string;
  reason?: string | null;
  referenceCode?: string | null;
}

// ---- Purchasing ----
export interface Supplier {
  id: number;
  name: string;
  phone?: string | null;
  email?: string | null;
  address?: string | null;
  balance: number;
  isActive: boolean;
}
export interface PurchaseInvoiceListItem {
  id: number;
  invoiceNumber: string;
  supplierId: number;
  supplierName: string;
  warehouseId: number;
  invoiceDate: string;
  status: number;
  grandTotal: number;
  paidAmount: number;
}

// ---- Sales ----
export interface Customer {
  id: number;
  name: string;
  phone?: string | null;
  email?: string | null;
  address?: string | null;
  creditLimit: number;
  balance: number;
  isActive: boolean;
}
export interface SalesInvoiceListItem {
  id: number;
  invoiceNumber: string;
  customerId?: number | null;
  customerName?: string | null;
  warehouseId: number;
  invoiceDate: string;
  status: number;
  grandTotal: number;
  paidAmount: number;
}

// ---- Reporting ----
export interface DashboardSummary {
  fromUtc: string;
  toUtc: string;
  salesTotal: number;
  purchasesTotal: number;
  grossProfit: number;
  outstandingReceivables: number;
  outstandingPayables: number;
  salesInvoiceCount: number;
  purchaseInvoiceCount: number;
  lowStockProductCount: number;
}
export interface LowStockItem {
  productId: number;
  productName: string;
  sku?: string | null;
  qtyOnHand: number;
  reorderLevel: number;
}
export interface ProductSales {
  productId: number;
  productName: string;
  sku?: string | null;
  quantitySold: number;
  revenue: number;
  cost: number;
  profit: number;
}
