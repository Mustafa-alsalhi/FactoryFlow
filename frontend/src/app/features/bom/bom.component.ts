import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../core/services/api.service';
import { I18nService } from '../../core/services/i18n.service';

@Component({
  selector: 'app-bom',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="page-header">
      <div>
        <h1 class="page-title">{{ i18n.t('وصفات الإنتاج (BOM)', 'Bill of Materials (BOM)') }}</h1>
        <p class="page-subtitle">{{ i18n.t('تحديد المواد الخام ونسب الهدر وحساب تكلفة التصنيع', 'Define raw material recipes, waste %, and cost estimation') }}</p>
      </div>
      <button class="btn btn-primary" (click)="openModal()">+ {{ i18n.t('وصفة إنتاج جديدة', 'New BOM') }}</button>
    </div>

    <div class="card" style="margin-bottom:16px">
      <div class="table-wrapper">
        <table class="erp-table">
          <thead>
            <tr>
              <th>{{ i18n.t('الكود', 'Code') }}</th>
              <th>{{ i18n.t('المنتج', 'Product') }}</th>
              <th>{{ i18n.t('الإصدار', 'Version') }}</th>
              <th>{{ i18n.t('كمية المخرج', 'Output Qty') }}</th>
              <th>{{ i18n.t('تكلفة المواد', 'Material Cost') }}</th>
              <th>{{ i18n.t('حساب التكلفة لـ', 'Calculate For Qty') }}</th>
            </tr>
          </thead>
          <tbody>
            <tr *ngFor="let b of boms()">
              <td><strong>{{ b.code }}</strong></td>
              <td>{{ b.product?.name }}</td>
              <td>v{{ b.version }}</td>
              <td>{{ b.outputQuantity }}</td>
              <td>{{ b.estimatedMaterialCost | number:'1.2-2' }}</td>
              <td>
                <div style="display:flex;gap:6px;align-items:center">
                  <input class="form-control" type="number" [(ngModel)]="calcQty" style="width:90px" />
                  <button class="btn btn-secondary btn-sm" (click)="calculate(b.id)">{{ i18n.t('احسب', 'Calculate') }}</button>
                </div>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>

    <div *ngIf="calcResult() as c" class="card">
      <div class="card-header">
        <h3 class="card-title">{{ i18n.t('نتيجة حساب تكلفة الوصفة:', 'BOM Cost Calculation:') }} {{ c.code }} ({{ c.targetQuantity }} {{ i18n.t('وحدة', 'units') }})</h3>
        <span class="badge badge-info">{{ i18n.t('تكلفة الوحدة:', 'Unit Cost:') }} {{ c.estimatedUnitCost | number:'1.2-4' }}</span>
      </div>
      <div class="kpi-grid">
        <div class="kpi-card"><div class="kpi-label">{{ i18n.t('تكلفة المواد', 'Materials') }}</div><div class="kpi-value">{{ c.totalMaterialCost | number:'1.2-2' }}</div></div>
        <div class="kpi-card"><div class="kpi-label">{{ i18n.t('الأجور المقدرة', 'Est. Labor') }}</div><div class="kpi-value">{{ c.totalLaborCost | number:'1.2-2' }}</div></div>
        <div class="kpi-card"><div class="kpi-label">{{ i18n.t('غير المباشرة المقدرة', 'Est. Overhead') }}</div><div class="kpi-value">{{ c.totalOverheadCost | number:'1.2-2' }}</div></div>
        <div class="kpi-card"><div class="kpi-label">{{ i18n.t('الإجمالي المقدر', 'Grand Total') }}</div><div class="kpi-value">{{ c.totalEstimatedCost | number:'1.2-2' }}</div></div>
      </div>
    </div>

    <div *ngIf="showModal()" class="modal-backdrop">
      <div class="modal-dialog">
        <div class="modal-header">
          <h3 class="card-title">{{ i18n.t('إنشاء وصفة إنتاج', 'Create BOM') }}</h3>
          <button class="btn btn-secondary btn-sm" (click)="showModal.set(false)">✕</button>
        </div>
        <div class="modal-body">
          <div class="form-grid" style="margin-bottom:14px">
            <div class="form-group">
              <label class="form-label">{{ i18n.t('الكود', 'Code') }}</label>
              <input class="form-control" [(ngModel)]="form.code" />
            </div>
            <div class="form-group">
              <label class="form-label">{{ i18n.t('المنتج', 'Product') }}</label>
              <select class="form-control" [(ngModel)]="form.productId">
                <option *ngFor="let p of products()" [value]="p.id">{{ p.name }}</option>
              </select>
            </div>
            <div class="form-group">
              <label class="form-label">{{ i18n.t('الإصدار', 'Version') }}</label>
              <input class="form-control" type="number" [(ngModel)]="form.version" />
            </div>
            <div class="form-group">
              <label class="form-label">{{ i18n.t('كمية المخرج', 'Output Qty') }}</label>
              <input class="form-control" type="number" [(ngModel)]="form.outputQuantity" />
            </div>
            <div class="form-group">
              <label class="form-label">{{ i18n.t('أجور مقدرة', 'Est. Labor') }}</label>
              <input class="form-control" type="number" [(ngModel)]="form.estimatedLaborCost" />
            </div>
            <div class="form-group">
              <label class="form-label">{{ i18n.t('تكاليف غير مباشرة', 'Est. Overhead') }}</label>
              <input class="form-control" type="number" [(ngModel)]="form.estimatedOverheadCost" />
            </div>
          </div>
          <h4>{{ i18n.t('المكونات', 'Components') }}</h4>
          <div *ngFor="let item of form.items; let idx = index" class="form-grid" style="margin-bottom:8px;align-items:end">
            <div class="form-group">
              <label class="form-label">{{ i18n.t('المادة الخام', 'Raw Material') }}</label>
              <select class="form-control" [(ngModel)]="item.rawMaterialId">
                <option *ngFor="let rm of rawMaterials()" [value]="rm.id">{{ rm.name }}</option>
              </select>
            </div>
            <div class="form-group">
              <label class="form-label">{{ i18n.t('الكمية', 'Quantity') }}</label>
              <input class="form-control" type="number" [(ngModel)]="item.quantity" />
            </div>
            <div class="form-group">
              <label class="form-label">{{ i18n.t('نسبة الهدر %', 'Waste %') }}</label>
              <input class="form-control" type="number" [(ngModel)]="item.wastePercentage" />
            </div>
            <button class="btn btn-danger btn-sm" (click)="form.items.splice(idx, 1)">✕</button>
          </div>
          <button class="btn btn-secondary btn-sm" (click)="addLine()">+ {{ i18n.t('إضافة مادة', 'Add Material') }}</button>
        </div>
        <div class="modal-footer">
          <button class="btn btn-secondary" (click)="showModal.set(false)">{{ i18n.t('إلغاء', 'Cancel') }}</button>
          <button class="btn btn-primary" (click)="save()">{{ i18n.t('حفظ الوصفة', 'Save BOM') }}</button>
        </div>
      </div>
    </div>
  `
})
export class BomComponent implements OnInit {
  private api = inject(ApiService);
  i18n = inject(I18nService);

  boms = signal<any[]>([]);
  products = signal<any[]>([]);
  rawMaterials = signal<any[]>([]);
  units = signal<any[]>([]);
  calcResult = signal<any>(null);
  showModal = signal(false);
  calcQty = 100;
  form: any = { items: [] };

  ngOnInit() {
    this.load();
    this.api.get<any[]>('masterdata/products').subscribe((d) => this.products.set(d));
    this.api.get<any[]>('masterdata/raw-materials').subscribe((d) => this.rawMaterials.set(d));
    this.api.get<any[]>('masterdata/units').subscribe((d) => this.units.set(d));
  }

  load() {
    this.api.get<any[]>('manufacturing/boms').subscribe((d) => this.boms.set(d));
  }

  calculate(id: string) {
    this.api.get<any>(`manufacturing/boms/${id}/calculate-cost`, { quantity: this.calcQty }).subscribe((r) => this.calcResult.set(r));
  }

  openModal() {
    this.form = {
      code: `BOM-${Date.now().toString().slice(-4)}`,
      productId: this.products()[0]?.id || '',
      version: 1, outputQuantity: 1, estimatedLaborCost: 0, estimatedOverheadCost: 0,
      items: []
    };
    this.addLine();
    this.showModal.set(true);
  }

  addLine() {
    this.form.items.push({
      rawMaterialId: this.rawMaterials()[0]?.id || '',
      unitOfMeasureId: this.units()[0]?.id || '',
      quantity: 1, wastePercentage: 0
    });
  }

  save() {
    this.api.post('manufacturing/boms', this.form).subscribe(() => { this.showModal.set(false); this.load(); });
  }
}

