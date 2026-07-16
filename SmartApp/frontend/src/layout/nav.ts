import { P } from '../auth/permissions';

export interface NavItem {
  label: string;
  path: string;
  /** Any of these permissions grants visibility. Empty = always visible (authenticated). */
  anyOf: string[];
}

export interface NavSection {
  title: string;
  items: NavItem[];
}

/** The full navigation tree. The sidebar filters items by the user's permissions. */
export const NAV: NavSection[] = [
  {
    title: 'عام',
    items: [{ label: 'لوحة المعلومات', path: '/dashboard', anyOf: [P.reports.view] }],
  },
  {
    title: 'الكتالوج',
    items: [
      { label: 'المنتجات', path: '/catalog/products', anyOf: [P.products.view] },
      { label: 'التصنيفات', path: '/catalog/categories', anyOf: [P.categories.view] },
      { label: 'وحدات القياس', path: '/catalog/units', anyOf: [P.units.view] },
      { label: 'العلامات التجارية', path: '/catalog/brands', anyOf: [P.brands.view] },
    ],
  },
  {
    title: 'المخزون',
    items: [
      { label: 'المستودعات', path: '/inventory/warehouses', anyOf: [P.warehouses.view] },
      { label: 'أرصدة المخزون', path: '/inventory/stock', anyOf: [P.stock.view] },
    ],
  },
  {
    title: 'المشتريات',
    items: [
      { label: 'المورّدون', path: '/purchasing/suppliers', anyOf: [P.suppliers.view] },
      { label: 'فواتير الشراء', path: '/purchasing/invoices', anyOf: [P.purchases.view] },
    ],
  },
  {
    title: 'المبيعات',
    items: [
      { label: 'العملاء', path: '/sales/customers', anyOf: [P.customers.view] },
      { label: 'فواتير المبيعات', path: '/sales/invoices', anyOf: [P.sales.view] },
    ],
  },
  {
    title: 'التقارير',
    items: [{ label: 'التقارير', path: '/reports', anyOf: [P.reports.view] }],
  },
  {
    title: 'الإدارة',
    items: [
      { label: 'المستخدمون', path: '/admin/users', anyOf: [P.users.view] },
      { label: 'الأدوار', path: '/admin/roles', anyOf: [P.roles.view] },
      { label: 'الصلاحيات', path: '/admin/permissions', anyOf: [P.roles.view] },
      { label: 'إعدادات المستأجر', path: '/admin/settings', anyOf: [P.settings.view] },
    ],
  },
];
