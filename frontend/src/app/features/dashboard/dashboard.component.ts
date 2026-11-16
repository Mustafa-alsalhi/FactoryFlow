import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../core/services/api.service';
import { I18nService } from '../../core/services/i18n.service';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="page-header">
      <div>
        <h1 class="page-title">{{ i18n.t('لوحة التحكم التنفيذية', 'Executive Dashboard') }}</h1>
        <p class="page-subtitle">{{ i18n.t('مؤشرات الإنتاج والتكاليف والمبيعات والمخزون الفعلية', 'Real-time production, costing, sales & inventory metrics') }}</p>
      </div>
      <div style="display:flex;gap:8px;flex-wrap:wrap">
        <input class="form-control" type="date" [(ngModel)]="fromDate" style="width:auto" />
        <input class="form-control" type="date" [(ngModel)]="toDate" style="width:auto" />
        <select class="form-control" [(ngModel)]="warehouseId" style="width:auto">
          <option value="">{{ i18n.t('كل المستودعات', 'All Warehouses') }}</option>
          <option *ngFor="let w of warehouses()" [value]="w.id">{{ w.name }}</option>
        </select>
        <button class="btn btn-primary" (click)="load()">{{ i18n.t('تحديث', 'Apply') }}</button>
      </div>
    </div>

    <div *ngIf="loading()" class="alert alert-info">{{ i18n.t('جاري تحميل المؤشرات...', 'Loading metrics...') }}</div>

    <ng-container *ngIf="data() as d">
      <div class="kpi-grid">
        <div class="kpi-card">
          <div class="kpi-label">{{ i18n.t('إجمالي قيمة الإنتاج', 'Total Production Value') }}</div>
          <div class="kpi-value">{{ d.totalProductionValue | number:'1.2-2' }}</div>
          <div class="kpi-sub">{{ i18n.t('أوامر مكتملة:', 'Completed:') }} {{ d.completedOrders }} | {{ i18n.t('جارية:', 'Active:') }} {{ d.inProgressOrders }}</div>
        </div>
        <div class="kpi-card">
          <div class="kpi-label">{{ i18n.t('إجمالي تكلفة الإنتاج', 'Total Production Cost') }}</div>
          <div class="kpi-value">{{ d.totalProductionCost | number:'1.2-2' }}</div>
          <div class="kpi-sub">{{ i18n.t('وحدات تالفة:', 'Defective:') }} {{ d.defectiveProducts }}</div>
        </div>
        <div class="kpi-card">
          <div class="kpi-label">{{ i18n.t('إجمالي المبيعات', 'Total Sales') }}</div>
          <div class="kpi-value">{{ d.totalSales | number:'1.2-2' }}</div>
          <div class="kpi-sub">{{ i18n.t('المشتريات:', 'Purchases:') }} {{ d.totalPurchases | number:'1.2-2' }}</div>
        </div>
        <div class="kpi-card">
          <div class="kpi-label">{{ i18n.t('صافي الأرباح', 'Net Profit') }}</div>
          <div class="kpi-value" [style.color]="d.netProfit >= 0 ? 'var(--success)' : 'var(--danger)'">{{ d.netProfit | number:'1.2-2' }}</div>
          <div class="kpi-sub">{{ i18n.t('المصروفات:', 'Expenses:') }} {{ d.totalExpenses | number:'1.2-2' }}</div>
        </div>
        <div class="kpi-card">
          <div class="kpi-label">{{ i18n.t('قيمة المواد الخام', 'Raw Materials Value') }}</div>
          <div class="kpi-value">{{ d.rawMaterialValue | number:'1.2-2' }}</div>
          <div class="kpi-sub">{{ i18n.t('نواقص:', 'Low stock:') }} {{ d.lowStockRawMaterials?.length || 0 }}</div>
        </div>
        <div class="kpi-card">
          <div class="kpi-label">{{ i18n.t('قيمة المنتجات النهائية', 'Finished Goods Value') }}</div>
          <div class="kpi-value">{{ d.finishedGoodsValue | number:'1.2-2' }}</div>
          <div class="kpi-sub">{{ i18n.t('منتجات منخفضة:', 'Low stock:') }} {{ d.lowStockProducts?.length || 0 }}</div>
        </div>
      </div>

      <div class="grid-2">
        <div class="card">
          <div class="card-header">
            <h3 class="card-title">{{ i18n.t('مقارنة التكاليف المخططة والفعلية', 'Planned vs Actual Production Cost') }}</h3>
          </div>
          <div class="table-wrapper">
            <table class="erp-table">
              <thead>
                <tr>
                  <th>{{ i18n.t('أمر الإنتاج', 'Order') }}</th>
                  <th>{{ i18n.t('المنتج', 'Product') }}</th>
                  <th>{{ i18n.t('المخطط', 'Planned') }}</th>
                  <th>{{ i18n.t('الفعلي', 'Actual') }}</th>
                </tr>
              </thead>
              <tbody>
                <tr *ngFor="let c of d.costComparison">
                  <td>{{ c.orderNumber }}</td>
                  <td>{{ c.productName }}</td>
                  <td>{{ c.plannedCost | number:'1.2-2' }}</td>
                  <td>{{ c.actualCost | number:'1.2-2' }}</td>
                </tr>
                <tr *ngIf="!d.costComparison?.length">
                  <td colspan="4" class="empty-state">{{ i18n.t('لا توجد أوامر إنتاج بعد', 'No production orders yet') }}</td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>

        <div class="card">
          <div class="card-header">
            <h3 class="card-title">{{ i18n.t('تنبيهات انخفاض المخزون', 'Low Stock Alerts') }}</h3>
          </div>
          <div class="table-wrapper">
            <table class="erp-table">
              <thead>
                <tr>
                  <th>{{ i18n.t('الكود', 'Code') }}</th>
                  <th>{{ i18n.t('الصنف', 'Item') }}</th>
                  <th>{{ i18n.t('الرصيد', 'On Hand') }}</th>
                  <th>{{ i18n.t('الحد الأدنى', 'Min Level') }}</th>
                </tr>
              </thead>
              <tbody>
                <tr *ngFor="let rm of d.lowStockRawMaterials">
                  <td>{{ rm.code }}</td>
                  <td>{{ i18n.lang() === 'ar' ? (rm.nameAr || rm.name) : rm.name }}</td>
                  <td><span class="badge badge-danger">{{ rm.onHand }}</span></td>
                  <td>{{ rm.minimumStockLevel }}</td>
                </tr>
                <tr *ngIf="!d.lowStockRawMaterials?.length">
                  <td colspan="4" class="empty-state">{{ i18n.t('جميع الأرصدة آمنة', 'All stock levels healthy') }}</td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>
      </div>
    </ng-container>
  `
})
export class DashboardComponent implements OnInit {
  private api = inject(ApiService);
  i18n = inject(I18nService);

  data = signal<any>(null);
  warehouses = signal<any[]>([]);
  loading = signal(false);
  fromDate = '';
  toDate = '';
  warehouseId = '';

  ngOnInit() {
    this.api.get<any[]>('masterdata/warehouses').subscribe({ next: (w) => this.warehouses.set(w) });
    this.load();
  }

  load() {
    this.loading.set(true);
    this.api.get<any>('dashboard', { from: this.fromDate, to: this.toDate, warehouseId: this.warehouseId }).subscribe({
      next: (res) => { this.data.set(res); this.loading.set(false); },
      error: () => this.loading.set(false)
    });
  }
}

