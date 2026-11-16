import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../core/services/api.service';
import { I18nService } from '../../core/services/i18n.service';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, FormsModule],
  styles: [`
    .chart-box {
      display: flex;
      align-items: flex-end;
      justify-content: space-between;
      gap: 12px;
      height: 220px;
      padding: 16px 8px 8px;
      border-bottom: 1px solid var(--border-color);
    }
    .chart-col {
      flex: 1;
      display: flex;
      flex-direction: column;
      align-items: center;
      height: 100%;
      justify-content: flex-end;
      gap: 6px;
    }
    .chart-bars {
      display: flex;
      align-items: flex-end;
      gap: 4px;
      height: 170px;
      width: 100%;
      justify-content: center;
    }
    .chart-bar {
      width: 14px;
      min-height: 4px;
      border-radius: 4px 4px 0 0;
      transition: height 0.3s ease;
    }
    .bar-sales { background: var(--primary); }
    .bar-profit { background: var(--success); }
    .bar-cost { background: var(--warning); }
    .chart-label {
      font-size: 11px;
      color: var(--text-muted);
      white-space: nowrap;
    }
    .chart-legend {
      display: flex;
      gap: 16px;
      flex-wrap: wrap;
      margin-top: 12px;
      font-size: 12px;
      color: var(--text-secondary);
    }
    .legend-dot {
      display: inline-block;
      width: 10px;
      height: 10px;
      border-radius: 2px;
      margin-inline-end: 6px;
    }
    .progress-track {
      height: 10px;
      background: var(--bg-surface-alt);
      border-radius: 999px;
      overflow: hidden;
      border: 1px solid var(--border-color);
    }
    .progress-fill {
      height: 100%;
      border-radius: 999px;
      transition: width 0.3s ease;
    }
  `],
  template: `
    <div class="page-header">
      <div>
        <h1 class="page-title">{{ i18n.t('لوحة التحكم التنفيذية', 'Executive Dashboard') }}</h1>
        <p class="page-subtitle">{{ i18n.t('مؤشرات الإنتاج والتكاليف والمبيعات والمخزون الفعلية', 'Real-time production, costing, sales & inventory metrics') }}</p>
      </div>
      <div style="display:flex;gap:8px;flex-wrap:wrap">
        <input class="form-control" type="date" [(ngModel)]="fromDate" style="width:auto" />
        <input class="form-control" type="date" [(ngModel)]="toDate" style="width:auto" />
        <select class="form-control" [(ngModel)]="branchId" style="width:auto">
          <option value="">{{ i18n.t('كل الفروع', 'All Branches') }}</option>
          <option *ngFor="let b of branches()" [value]="b.id">{{ b.name }}</option>
        </select>
        <select class="form-control" [(ngModel)]="warehouseId" style="width:auto">
          <option value="">{{ i18n.t('كل المستودعات', 'All Warehouses') }}</option>
          <option *ngFor="let w of warehouses()" [value]="w.id">{{ w.name }}</option>
        </select>
        <select class="form-control" [(ngModel)]="productId" style="width:auto">
          <option value="">{{ i18n.t('كل المنتجات', 'All Products') }}</option>
          <option *ngFor="let p of products()" [value]="p.id">{{ p.name }}</option>
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

      <!-- Visual Charts Row -->
      <div class="grid-2">
        <div class="card">
          <div class="card-header">
            <h3 class="card-title">{{ i18n.t('مخطط المبيعات والأرباح وتكلفة الإنتاج (6 أشهر)', 'Monthly Sales, Profit & Production Cost (6 Months)') }}</h3>
          </div>
          <div class="chart-box">
            <div class="chart-col" *ngFor="let m of d.monthlyChart">
              <div class="chart-bars">
                <div class="chart-bar bar-sales" [style.height.%]="barPct(m.sales, maxMonthly(d.monthlyChart))" [title]="i18n.t('المبيعات: ', 'Sales: ') + (m.sales | number:'1.2-2')"></div>
                <div class="chart-bar bar-profit" [style.height.%]="barPct(m.grossProfit, maxMonthly(d.monthlyChart))" [title]="i18n.t('الربح: ', 'Profit: ') + (m.grossProfit | number:'1.2-2')"></div>
                <div class="chart-bar bar-cost" [style.height.%]="barPct(m.productionCost, maxMonthly(d.monthlyChart))" [title]="i18n.t('التكلفة: ', 'Cost: ') + (m.productionCost | number:'1.2-2')"></div>
              </div>
              <div class="chart-label">{{ m.month }}</div>
            </div>
          </div>
          <div class="chart-legend">
            <span><span class="legend-dot bar-sales"></span>{{ i18n.t('المبيعات', 'Sales') }}</span>
            <span><span class="legend-dot bar-profit"></span>{{ i18n.t('إجمالي الربح', 'Gross Profit') }}</span>
            <span><span class="legend-dot bar-cost"></span>{{ i18n.t('تكلفة الإنتاج', 'Production Cost') }}</span>
          </div>
        </div>

        <div class="card">
          <div class="card-header">
            <h3 class="card-title">{{ i18n.t('توزيع قيمة المخزون والأداء التشغيلي', 'Inventory Valuation & Production Mix') }}</h3>
          </div>
          <div style="display:flex;flex-direction:column;gap:16px;padding-top:8px">
            <div>
              <div style="display:flex;justify-content:space-between;font-size:13px;margin-bottom:6px">
                <span>{{ i18n.t('المواد الخام في المخزون', 'Raw Materials Inventory') }}</span>
                <strong>{{ d.rawMaterialValue | number:'1.2-2' }}</strong>
              </div>
              <div class="progress-track">
                <div class="progress-fill" style="background:var(--primary)" [style.width.%]="invPct(d.rawMaterialValue, d.rawMaterialValue + d.finishedGoodsValue)"></div>
              </div>
            </div>
            <div>
              <div style="display:flex;justify-content:space-between;font-size:13px;margin-bottom:6px">
                <span>{{ i18n.t('المنتجات النهائية في المخزون', 'Finished Goods Inventory') }}</span>
                <strong>{{ d.finishedGoodsValue | number:'1.2-2' }}</strong>
              </div>
              <div class="progress-track">
                <div class="progress-fill" style="background:var(--success)" [style.width.%]="invPct(d.finishedGoodsValue, d.rawMaterialValue + d.finishedGoodsValue)"></div>
              </div>
            </div>
            <div>
              <div style="display:flex;justify-content:space-between;font-size:13px;margin-bottom:6px">
                <span>{{ i18n.t('نسبة إتمام أوامر الإنتاج', 'Production Order Completion Rate') }}</span>
                <strong>{{ d.completedOrders }} / {{ d.completedOrders + d.inProgressOrders }}</strong>
              </div>
              <div class="progress-track">
                <div class="progress-fill" style="background:var(--info)" [style.width.%]="invPct(d.completedOrders, d.completedOrders + d.inProgressOrders)"></div>
              </div>
            </div>
          </div>
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
  branches = signal<any[]>([]);
  warehouses = signal<any[]>([]);
  products = signal<any[]>([]);
  loading = signal(false);
  fromDate = '';
  toDate = '';
  branchId = '';
  warehouseId = '';
  productId = '';

  ngOnInit() {
    this.api.get<any[]>('tenantsandusers/branches').subscribe((b) => this.branches.set(b));
    this.api.get<any[]>('masterdata/warehouses').subscribe((w) => this.warehouses.set(w));
    this.api.get<any[]>('masterdata/products').subscribe((p) => this.products.set(p));
    this.load();
  }

  load() {
    this.loading.set(true);
    this.api.get<any>('dashboard', {
      from: this.fromDate,
      to: this.toDate,
      branchId: this.branchId,
      warehouseId: this.warehouseId,
      productId: this.productId
    }).subscribe({
      next: (res) => { this.data.set(res); this.loading.set(false); },
      error: () => this.loading.set(false)
    });
  }

  maxMonthly(series: any[]): number {
    if (!series?.length) return 1;
    const max = Math.max(...series.flatMap((m) => [Math.abs(m.sales || 0), Math.abs(m.grossProfit || 0), Math.abs(m.productionCost || 0)]));
    return max > 0 ? max : 1;
  }

  barPct(val: number, max: number): number {
    return Math.max(3, Math.min(100, Math.round((Math.abs(val || 0) / max) * 100)));
  }

  invPct(part: number, total: number): number {
    if (!total || total <= 0) return 0;
    return Math.round((part / total) * 100);
  }
}
