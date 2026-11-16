import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';
import { MainLayoutComponent } from './layouts/main-layout.component';
import { LoginComponent } from './features/auth/login.component';
import { DashboardComponent } from './features/dashboard/dashboard.component';
import { RawMaterialsComponent } from './features/master-data/raw-materials.component';
import { ProductsComponent } from './features/master-data/products.component';
import { BomComponent } from './features/bom/bom.component';
import { ManufacturingComponent } from './features/manufacturing/manufacturing.component';
import { ResourcesComponent } from './features/manufacturing/resources.component';
import { QualityComponent } from './features/quality/quality.component';
import { WarehousesComponent } from './features/warehouses/warehouses.component';
import { PurchasingComponent } from './features/commercial/purchasing.component';
import { SalesComponent } from './features/commercial/sales.component';
import { AccountingComponent } from './features/accounting/accounting.component';
import { TreasuryComponent } from './features/treasury/treasury.component';
import { ReportsComponent } from './features/reports/reports.component';
import { SettingsComponent } from './features/settings/settings.component';
import { AuditLogsComponent } from './features/settings/audit-logs.component';

export const routes: Routes = [
  { path: 'login', component: LoginComponent },
  {
    path: '',
    component: MainLayoutComponent,
    canActivate: [authGuard],
    children: [
      { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
      { path: 'dashboard', component: DashboardComponent },
      { path: 'raw-materials', component: RawMaterialsComponent },
      { path: 'products', component: ProductsComponent },
      { path: 'bom', component: BomComponent },
      { path: 'manufacturing', component: ManufacturingComponent },
      { path: 'resources', component: ResourcesComponent },
      { path: 'quality', component: QualityComponent },
      { path: 'warehouses', component: WarehousesComponent },
      { path: 'purchasing', component: PurchasingComponent },
      { path: 'sales', component: SalesComponent },
      { path: 'accounting', component: AccountingComponent },
      { path: 'treasury', component: TreasuryComponent },
      { path: 'reports', component: ReportsComponent },
      { path: 'settings', component: SettingsComponent },
      { path: 'audit-logs', component: AuditLogsComponent }
    ]
  },
  { path: '**', redirectTo: 'dashboard' }
];
