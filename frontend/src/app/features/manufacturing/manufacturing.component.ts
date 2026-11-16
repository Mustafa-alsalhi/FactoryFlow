import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../core/services/api.service';
import { I18nService } from '../../core/services/i18n.service';

@Component({
  selector: 'app-manufacturing',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="page-header">
      <div>
        <h1 class="page-title">{{ i18n.t('أوامر الإنتاج وحساب التكاليف', 'Production Orders & Cost Accounting') }}</h1>
        <p class="page-subtitle">{{ i18n.t('إدارة دورة التصنيع الكاملة: صرف المواد، الأجور، التكاليف غير المباشرة، والإغلاق', 'Full manufacturing lifecycle: WIP materials, direct labor, overhead & completion') }}</p>
      </div>
      <button class="btn btn-primary" (click)="openNewOrder()">+ {{ i18n.t('أمر إنتاج جديد', 'New Production Order') }}</button>
    </div>

    <div *ngIf="error()" class="alert alert-error">{{ error() }}</div>

    <div class="card">
      <div class="table-wrapper">
        <table class="erp-table">
          <thead>
            <tr>
              <th>{{ i18n.t('رقم الأمر', 'Order #') }}</th>
              <th>{{ i18n.t('المنتج', 'Product') }}</th>
              <th>{{ i18n.t('المخطط / المنتج', 'Planned / Produced') }}</th>
              <th>{{ i18n.t('التكلفة الفعلية', 'Actual Cost') }}</th>
              <th>{{ i18n.t('تكلفة الوحدة', 'Unit Cost') }}</th>
              <th>{{ i18n.t('الحالة', 'Status') }}</th>
              <th>{{ i18n.t('العمليات', 'Operations') }}</th>
            </tr>
          </thead>
          <tbody>
            <tr *ngFor="let o of orders()">
              <td><strong>{{ o.orderNumber }}</strong></td>
              <td>{{ o.product?.name }}</td>
              <td>{{ o.plannedQuantity }} / {{ o.producedQuantity }} ({{ i18n.t('تالف:', 'Def:') }} {{ o.defectiveQuantity }})</td>
              <td>{{ (o.actualMaterialCost + o.actualLaborCost + o.actualOverheadCost) | number:'1.2-2' }}</td>
              <td>{{ o.actualUnitCost | number:'1.2-4' }}</td>
              <td><span class="badge" [ngClass]="statusClass(o.status)">{{ statusLabel(o.status) }}</span></td>
              <td style="display:flex;gap:6px;flex-wrap:wrap">
                <button *ngIf="o.status === 1 || o.status === 2 || o.status === 4" class="btn btn-success btn-sm" (click)="start(o.id)">{{ i18n.t('بدء', 'Start') }}</button>
                <button *ngIf="o.status === 3" class="btn btn-secondary btn-sm" (click)="pause(o.id)">{{ i18n.t('إيقاف', 'Pause') }}</button>
                <button *ngIf="o.status === 3" class="btn btn-primary btn-sm" (click)="openAction(o, 'material')">{{ i18n.t('صرف مواد', 'Issue Mat') }}</button>
                <button *ngIf="o.status === 3" class="btn btn-primary btn-sm" (click)="openAction(o, 'labor')">{{ i18n.t('تسجيل عمالة', 'Log Labor') }}</button>
                <button *ngIf="o.status === 3" class="btn btn-primary btn-sm" (click)="openAction(o, 'overhead')">{{ i18n.t('تكاليف غير مباشرة', 'Overhead') }}</button>
                <button *ngIf="o.status === 3" class="btn btn-success btn-sm" (click)="openAction(o, 'complete')">{{ i18n.t('إكمال', 'Complete') }}</button>
                <button *ngIf="o.status !== 5 && o.status !== 6" class="btn btn-danger btn-sm" (click)="cancel(o.id)">{{ i18n.t('إلغاء', 'Cancel') }}</button>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>

    <!-- New Order Modal -->
    <div *ngIf="showNew()" class="modal-backdrop">
      <div class="modal-dialog">
        <div class="modal-header">
          <h3 class="card-title">{{ i18n.t('إنشاء أمر إنتاج', 'Create Production Order') }}</h3>
          <button class="btn btn-secondary btn-sm" (click)="showNew.set(false)">✕</button>
        </div>
        <div class="modal-body">
          <div class="form-grid">
            <div class="form-group">
              <label class="form-label">{{ i18n.t('المنتج', 'Product') }}</label>
              <select class="form-control" [(ngModel)]="orderForm.productId">
                <option *ngFor="let p of products()" [value]="p.id">{{ p.name }}</option>
              </select>
            </div>
            <div class="form-group">
              <label class="form-label">{{ i18n.t('الوصفة BOM', 'BOM') }}</label>
              <select class="form-control" [(ngModel)]="orderForm.bomId">
                <option *ngFor="let b of boms()" [value]="b.id">{{ b.code }} ({{ b.product?.name }})</option>
              </select>
            </div>
            <div class="form-group">
              <label class="form-label">{{ i18n.t('الكمية المطلوبة', 'Planned Qty') }}</label>
              <input class="form-control" type="number" [(ngModel)]="orderForm.plannedQuantity" />
            </div>
            <div class="form-group">
              <label class="form-label">{{ i18n.t('مستودع الخام', 'Raw Warehouse') }}</label>
              <select class="form-control" [(ngModel)]="orderForm.warehouseId">
                <option *ngFor="let w of warehouses()" [value]="w.id">{{ w.name }}</option>
              </select>
            </div>
            <div class="form-group">
              <label class="form-label">{{ i18n.t('مستودع المنتج النهائي', 'Output Warehouse') }}</label>
              <select class="form-control" [(ngModel)]="orderForm.outputWarehouseId">
                <option *ngFor="let w of warehouses()" [value]="w.id">{{ w.name }}</option>
              </select>
            </div>
            <div class="form-group">
              <label class="form-label">{{ i18n.t('خط الإنتاج', 'Production Line') }}</label>
              <select class="form-control" [(ngModel)]="orderForm.productionLineId">
                <option *ngFor="let l of lines()" [value]="l.id">{{ l.name }}</option>
              </select>
            </div>
          </div>
        </div>
        <div class="modal-footer">
          <button class="btn btn-secondary" (click)="showNew.set(false)">{{ i18n.t('إلغاء', 'Cancel') }}</button>
          <button class="btn btn-primary" (click)="createOrder()">{{ i18n.t('إنشاء الأمر', 'Create') }}</button>
        </div>
      </div>
    </div>

    <!-- Operation Modal -->
    <div *ngIf="actionType()" class="modal-backdrop">
      <div class="modal-dialog">
        <div class="modal-header">
          <h3 class="card-title">{{ selectedOrder?.orderNumber }} - {{ actionType() }}</h3>
          <button class="btn btn-secondary btn-sm" (click)="actionType.set(null)">✕</button>
        </div>
        <div class="modal-body">
          <div *ngIf="actionType() === 'material'" class="form-grid">
            <div class="form-group">
              <label class="form-label">{{ i18n.t('المادة الخام', 'Raw Material') }}</label>
              <select class="form-control" [(ngModel)]="opForm.rawMaterialId">
                <option *ngFor="let rm of rawMaterials()" [value]="rm.id">{{ rm.name }}</option>
              </select>
            </div>
            <div class="form-group">
              <label class="form-label">{{ i18n.t('الكمية المصروفة', 'Quantity') }}</label>
              <input class="form-control" type="number" [(ngModel)]="opForm.quantity" />
            </div>
            <div class="form-group">
              <label class="form-label">{{ i18n.t('كمية الهدر', 'Waste Qty') }}</label>
              <input class="form-control" type="number" [(ngModel)]="opForm.wasteQuantity" />
            </div>
          </div>

          <div *ngIf="actionType() === 'labor'" class="form-grid">
            <div class="form-group">
              <label class="form-label">{{ i18n.t('العامل', 'Employee') }}</label>
              <select class="form-control" [(ngModel)]="opForm.employeeId">
                <option *ngFor="let e of employees()" [value]="e.id">{{ e.fullName }}</option>
              </select>
            </div>
            <div class="form-group">
              <label class="form-label">{{ i18n.t('ساعات عادية', 'Regular Hours') }}</label>
              <input class="form-control" type="number" [(ngModel)]="opForm.regularHours" />
            </div>
            <div class="form-group">
              <label class="form-label">{{ i18n.t('ساعات إضافية', 'Overtime Hours') }}</label>
              <input class="form-control" type="number" [(ngModel)]="opForm.overtimeHours" />
            </div>
            <div class="form-group">
              <label class="form-label">{{ i18n.t('مكافآت', 'Bonuses') }}</label>
              <input class="form-control" type="number" [(ngModel)]="opForm.bonuses" />
            </div>
          </div>

          <div *ngIf="actionType() === 'overhead'" class="form-grid">
            <div class="form-group">
              <label class="form-label">{{ i18n.t('البند', 'Category') }}</label>
              <select class="form-control" [(ngModel)]="opForm.category">
                <option value="Electricity">Electricity / كهرباء</option>
                <option value="Water">Water / مياه</option>
                <option value="Maintenance">Maintenance / صيانة</option>
                <option value="Depreciation">Depreciation / إهلاك</option>
                <option value="Rent">Rent / إيجار</option>
              </select>
            </div>
            <div class="form-group">
              <label class="form-label">{{ i18n.t('المبلغ', 'Amount') }}</label>
              <input class="form-control" type="number" [(ngModel)]="opForm.amount" />
            </div>
          </div>

          <div *ngIf="actionType() === 'complete'" class="form-grid">
            <div class="form-group">
              <label class="form-label">{{ i18n.t('الكمية السليمة المنتجة', 'Good Quantity') }}</label>
              <input class="form-control" type="number" [(ngModel)]="opForm.goodQuantity" />
            </div>
            <div class="form-group">
              <label class="form-label">{{ i18n.t('الكمية التالفة', 'Defective Quantity') }}</label>
              <input class="form-control" type="number" [(ngModel)]="opForm.defectiveQuantity" />
            </div>
          </div>
        </div>
        <div class="modal-footer">
          <button class="btn btn-secondary" (click)="actionType.set(null)">{{ i18n.t('إلغاء', 'Cancel') }}</button>
          <button class="btn btn-primary" (click)="submitAction()">{{ i18n.t('تنفيذ', 'Submit') }}</button>
        </div>
      </div>
    </div>
  `
})
export class ManufacturingComponent implements OnInit {
  private api = inject(ApiService);
  i18n = inject(I18nService);

  orders = signal<any[]>([]);
  products = signal<any[]>([]);
  boms = signal<any[]>([]);
  warehouses = signal<any[]>([]);
  lines = signal<any[]>([]);
  rawMaterials = signal<any[]>([]);
  employees = signal<any[]>([]);
  branches = signal<any[]>([]);

  showNew = signal(false);
  actionType = signal<'material' | 'labor' | 'overhead' | 'complete' | null>(null);
  selectedOrder: any = null;
  error = signal('');
  orderForm: any = {};
  opForm: any = {};

  ngOnInit() {
    this.load();
    this.api.get<any[]>('masterdata/products').subscribe((d) => this.products.set(d));
    this.api.get<any[]>('manufacturing/boms').subscribe((d) => this.boms.set(d));
    this.api.get<any[]>('masterdata/warehouses').subscribe((d) => this.warehouses.set(d));
    this.api.get<any[]>('manufacturing/lines').subscribe((d) => this.lines.set(d));
    this.api.get<any[]>('masterdata/raw-materials').subscribe((d) => this.rawMaterials.set(d));
    this.api.get<any[]>('manufacturing/employees').subscribe((d) => this.employees.set(d));
    this.api.get<any[]>('tenantsandusers/branches').subscribe((d) => this.branches.set(d));
  }

  load() {
    this.api.get<any[]>('manufacturing/orders').subscribe((d) => this.orders.set(d));
  }

  openNewOrder() {
    this.orderForm = {
      branchId: this.branches()[0]?.id || '',
      warehouseId: this.warehouses()[0]?.id || '',
      outputWarehouseId: this.warehouses()[1]?.id || this.warehouses()[0]?.id || '',
      productId: this.products()[0]?.id || '',
      bomId: this.boms()[0]?.id || '',
      productionLineId: this.lines()[0]?.id || null,
      plannedQuantity: 10,
      plannedStartDate: new Date().toISOString(),
      plannedEndDate: new Date(Date.now() + 86400000).toISOString()
    };
    this.showNew.set(true);
  }

  createOrder() {
    this.api.post('manufacturing/orders', this.orderForm).subscribe({
      next: () => { this.showNew.set(false); this.load(); },
      error: (err) => this.error.set(err?.error?.message || 'Failed')
    });
  }

  start(id: string) { this.api.post(`manufacturing/orders/${id}/start`, {}).subscribe(() => this.load()); }
  pause(id: string) { this.api.post(`manufacturing/orders/${id}/pause`, {}).subscribe(() => this.load()); }
  cancel(id: string) {
    this.api.post(`manufacturing/orders/${id}/cancel`, {}).subscribe({
      next: () => this.load(),
      error: (err) => this.error.set(err?.error?.message || 'Cannot cancel')
    });
  }

  openAction(order: any, type: 'material' | 'labor' | 'overhead' | 'complete') {
    this.selectedOrder = order;
    this.opForm = {
      rawMaterialId: this.rawMaterials()[0]?.id || '',
      warehouseId: order.warehouseId,
      quantity: 1, wasteQuantity: 0,
      employeeId: this.employees()[0]?.id || '',
      regularHours: 8, overtimeHours: 0, bonuses: 0, deductions: 0,
      category: 'Electricity', machineHours: 0, amount: 50,
      goodQuantity: order.plannedQuantity, defectiveQuantity: 0
    };
    this.actionType.set(type);
  }

  submitAction() {
    const id = this.selectedOrder.id;
    const t = this.actionType();
    const endpoint = t === 'material' ? 'issue-material'
      : t === 'labor' ? 'log-labor'
      : t === 'overhead' ? 'log-overhead'
      : 'complete';

    this.api.post(`manufacturing/orders/${id}/${endpoint}`, this.opForm).subscribe({
      next: () => { this.actionType.set(null); this.load(); },
      error: (err) => this.error.set(err?.error?.message || 'Operation failed')
    });
  }

  statusLabel(s: number) {
    const map: any = { 1: 'Draft', 2: 'Planned', 3: 'InProgress', 4: 'Paused', 5: 'Completed', 6: 'Cancelled' };
    return map[s] || 'Unknown';
  }

  statusClass(s: number) {
    return s === 5 ? 'badge-success' : s === 3 ? 'badge-info' : s === 6 ? 'badge-danger' : 'badge-warning';
  }
}

