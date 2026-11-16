import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../core/services/api.service';
import { I18nService } from '../../core/services/i18n.service';

@Component({
  selector: 'app-reports',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="page-header">
      <div>
        <h1 class="page-title">{{ i18n.t('التقارير والتحليلات', 'Production, Costing & Financial Reports') }}</h1>
        <p class="page-subtitle">{{ i18n.t('تصفية حسب التاريخ والمنتج مع التصدير إلى Excel (CSV) والطباعة', 'Filter by date & product, export to Excel CSV or Print PDF') }}</p>
      </div>
      <div style="display:flex;gap:8px" class="no-print">
        <button class="btn btn-secondary" (click)="exportCsv()">⬇ {{ i18n.t('تصدير Excel', 'Export CSV') }}</button>
        <button class="btn btn-primary" (click)="printReport()">🖨 {{ i18n.t('طباعة / PDF', 'Print / PDF') }}</button>
      </div>
    </div>

    <div class="card">
      <div class="table-toolbar no-print">
        <div style="display:flex;gap:8px;flex-wrap:wrap">
          <input class="form-control" type="date" [(ngModel)]="fromDate" style="width:auto" />
          <input class="form-control" type="date" [(ngModel)]="toDate" style="width:auto" />
          <select class="form-control" [(ngModel)]="productId" style="width:auto">
            <option value="">{{ i18n.t('كل المنتجات', 'All Products') }}</option>
            <option *ngFor="let p of products()" [value]="p.id">{{ p.name }}</option>
          </select>
          <button class="btn btn-primary" (click)="load()">{{ i18n.t('عرض التقرير', 'Run Report') }}</button>
        </div>
      </div>

      <div class="table-wrapper">
        <table class="erp-table">
          <thead>
            <tr>
              <th>{{ i18n.t('أمر الإنتاج', 'Order #') }}</th>
              <th>{{ i18n.t('المنتج', 'Product') }}</th>
              <th>{{ i18n.t('المنتج الفعلي', 'Produced') }}</th>
              <th>{{ i18n.t('تكلفة المواد', 'Materials') }}</th>
              <th>{{ i18n.t('العمالة', 'Labor') }}</th>
              <th>{{ i18n.t('غير المباشرة', 'Overhead') }}</th>
              <th>{{ i18n.t('الإجمالي الفعلي', 'Actual Total') }}</th>
              <th>{{ i18n.t('الفرق', 'Variance') }}</th>
              <th>{{ i18n.t('تكلفة الوحدة', 'Unit Cost') }}</th>
              <th>{{ i18n.t('هامش الربح', 'Unit Margin') }}</th>
            </tr>
          </thead>
          <tbody>
            <tr *ngFor="let r of rows()">
              <td><strong>{{ r.orderNumber }}</strong></td>
              <td>{{ r.productName }}</td>
              <td>{{ r.producedQuantity }}</td>
              <td>{{ r.actualMaterialCost | number:'1.2-2' }}</td>
              <td>{{ r.actualLaborCost | number:'1.2-2' }}</td>
              <td>{{ r.actualOverheadCost | number:'1.2-2' }}</td>
              <td>{{ r.actualTotalCost | number:'1.2-2' }}</td>
              <td [style.color]="r.costVariance > 0 ? 'var(--danger)' : 'var(--success)'">{{ r.costVariance | number:'1.2-2' }}</td>
              <td>{{ r.actualUnitCost | number:'1.2-4' }}</td>
              <td>{{ r.estimatedMargin | number:'1.2-2' }}</td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>
  `
})
export class ReportsComponent implements OnInit {
  private api = inject(ApiService);
  i18n = inject(I18nService);

  rows = signal<any[]>([]);
  products = signal<any[]>([]);
  fromDate = '';
  toDate = '';
  productId = '';

  ngOnInit() {
    this.api.get<any[]>('masterdata/products').subscribe((d) => this.products.set(d));
    this.load();
  }

  load() {
    this.api.get<any[]>('reports/production-costing', { from: this.fromDate, to: this.toDate, productId: this.productId })
      .subscribe((d) => this.rows.set(d));
  }

  printReport() { window.print(); }

  exportCsv() {
    const headers = ['Order', 'Product', 'Produced', 'Materials', 'Labor', 'Overhead', 'ActualTotal', 'Variance', 'UnitCost', 'Margin'];
    const lines = this.rows().map((r) => [
      r.orderNumber, r.productName, r.producedQuantity, r.actualMaterialCost,
      r.actualLaborCost, r.actualOverheadCost, r.actualTotalCost, r.costVariance, r.actualUnitCost, r.estimatedMargin
    ].join(','));
    const csv = [headers.join(','), ...lines].join('\n');
    const blob = new Blob([csv], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `factoryflow-costing-${new Date().toISOString().slice(0, 10)}.csv`;
    a.click();
    URL.revokeObjectURL(url);
  }
}

