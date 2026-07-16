// Permission keys mirroring the backend Permissions constants (resource.action).
// Used to build the dynamic menu and to show/hide UI actions.

export const P = {
  users: { view: 'users.view', create: 'users.create', update: 'users.update', delete: 'users.delete' },
  roles: {
    view: 'roles.view',
    create: 'roles.create',
    update: 'roles.update',
    delete: 'roles.delete',
    managePermissions: 'roles.permissions.manage',
  },
  settings: { view: 'settings.view', manage: 'settings.manage' },
  categories: {
    view: 'catalog.categories.view',
    create: 'catalog.categories.create',
    update: 'catalog.categories.update',
    delete: 'catalog.categories.delete',
  },
  units: {
    view: 'catalog.units.view',
    create: 'catalog.units.create',
    update: 'catalog.units.update',
    delete: 'catalog.units.delete',
  },
  brands: {
    view: 'catalog.brands.view',
    create: 'catalog.brands.create',
    update: 'catalog.brands.update',
    delete: 'catalog.brands.delete',
  },
  products: {
    view: 'catalog.products.view',
    create: 'catalog.products.create',
    update: 'catalog.products.update',
    delete: 'catalog.products.delete',
  },
  warehouses: {
    view: 'inventory.warehouses.view',
    create: 'inventory.warehouses.create',
    update: 'inventory.warehouses.update',
    delete: 'inventory.warehouses.delete',
  },
  stock: {
    view: 'inventory.stock.view',
    adjust: 'inventory.stock.adjust',
    transfer: 'inventory.stock.transfer',
  },
  suppliers: {
    view: 'purchasing.suppliers.view',
    create: 'purchasing.suppliers.create',
    update: 'purchasing.suppliers.update',
    delete: 'purchasing.suppliers.delete',
  },
  purchases: {
    view: 'purchasing.view',
    create: 'purchasing.create',
    update: 'purchasing.update',
    delete: 'purchasing.delete',
    post: 'purchasing.post',
  },
  customers: {
    view: 'sales.customers.view',
    create: 'sales.customers.create',
    update: 'sales.customers.update',
    delete: 'sales.customers.delete',
  },
  sales: {
    view: 'sales.view',
    create: 'sales.create',
    update: 'sales.update',
    delete: 'sales.delete',
    post: 'sales.post',
  },
  reports: { view: 'reports.view' },
} as const;
