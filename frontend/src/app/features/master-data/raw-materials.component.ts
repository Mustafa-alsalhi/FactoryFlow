import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../core/services/api.service';
import { I18nService } from '../../core/services/i18n.service';

@Component({
  selector: 'app-raw-materials',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="page-header">
      <div>
        <h1 class="page-title">{{ i18n.t('إدارة المواد الخام', 'Raw Materials Management') }}</h1>
        <p class="page-subtitle">{{ i18n.t('إضافة وتعديل المواد الخام، حدود التنبيه، والأرصدة الفعلية', 'Materials, reorder thresholds, and live stock balances') }}</p>
      </div>
      <button class="btn btn-primary" (click)="openModal()">+ {{ i18n.t('مادة خام جديدة', 'New Raw Material') }}</button>
    </div>

    <div class="card">
      <div class="table-toolbar">
        <input class="form-control" style="max-width:300px" [(ngModel)]="search" (ngModelChange)="load()" [placeholder]="i18n.t('بحث بالكود أو الاسم...', 'Search by code or name...')" />
      </div>
      <div *ngIf="error()" class="alert alert-error">{{ error() }}</div>
      <div class="table-wrapper">
        <table class="erp-table">
          <thead>
            <tr>
              <th>{{ i18n.t('الكود', 'Code') }}</th>
              <th>{{ i18n.t('الاسم', 'Name') }}</th>
              <th>{{ i18n.t('التصنيف', 'Category') }}</th>
              <th>{{ i18n.t('الوحدة', 'Unit') }}</th>
              <th>{{ i18n.t('التكلفة المعيارية', 'Std Cost') }}</th>
              <th>{{ i18n.t('الرصيد الحالي', 'On Hand') }}</th>
              <th>{{ i18n.t('الحد الأدنى', 'Min Stock') }}</th>
              <th>{{ i18n.t('الحالة', 'Status') }}</th>
              <th>{{ i18n.t('إجراءات', 'Actions') }}</th>
            </tr>
          </thead>
          <tbody>
            <tr *ngFor="let item of items()">
              <td><strong>{{ item.code }}</strong></td>
              <td>{{ i18n.lang() === 'ar' ? (item.nameAr || item.name) : item.name }}</td>
              <td>{{ item.categoryName }}</td>
              <td>{{ item.unitCode }}</td>
              <td>{{ item.standardCost | number:'1.2-4' }}</td>
              <td>
                <span class="badge" [class.badge-danger]="item.quantityOnHand <= item.minimumStockLevel" [class.badge-success]="item.quantityOnHand > item.minimumStockLevel">
                  {{ item.quantityOnHand | number:'1.2-2' }}
                </span>
              </td>
              <td>{{ item.minimumStockLevel }}</td>
              <td><span class="badge" [class.badge-success]="item.isActive" [class.badge-neutral]="!item.isActive">{{ item.isActive ? i18n.t('نشط', 'Active') : i18n.t('معطل', 'Inactive') }}</span></td>
              <td>
                <button class="btn btn-secondary btn-sm" (click)="edit(item)">{{ i18n.t('تعديل', 'Edit') }}</button>
                <button class="btn btn-danger btn-sm" style="margin-inline-start:6px" (click)="remove(item.id)">{{ i18n.t('حذف', 'Delete') }}</button>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>

    <div *ngIf="showModal()" class="modal-backdrop">
      <div class="modal-dialog">
        <div class="modal-header">
          <h3 class="card-title">{{ editingId ? i18n.t('تعديل مادة خام', 'Edit Raw Material') : i18n.t('إضافة مادة خام', 'Add Raw Material') }}</h3>
          <button class="btn btn-secondary btn-sm" (click)="showModal.set(false)">✕</button>
        </div>
        <div class="modal-body">
          <div class="form-grid">
            <div class="form-group">
              <label class="form-label">{{ i18n.t('الكود', 'Code') }}</label>
              <input class="form-control" [(ngModel)]="form.code" [disabled]="!!editingId" />
            </div>
            <div class="form-group">
              <label class="form-label">{{ i18n.t('الاسم (EN)', 'Name (EN)') }}</label>
              <input class="form-control" [(ngModel)]="form.name" />
            </div>
            <div class="form-group">
              <label class="form-label">{{ i18n.t('الاسم (AR)', 'Name (AR)') }}</label>
              <input class="form-control" [(ngModel)]="form.nameAr" />
            </div>
            <div class="form-group">
              <label class="form-label">{{ i18n.t('التصنيف', 'Category') }}</label>
              <select class="form-control" [(ngModel)]="form.categoryId">
                <option *ngFor="let c of categories()" [value]="c.id">{{ c.name }}</option>
              </select>
            </div>
            <div class="form-group">
              <label class="form-label">{{ i18n.t('وحدة القياس', 'Unit') }}</label>
              <select class="form-control" [(ngModel)]="form.unitOfMeasureId">
                <option *ngFor="let u of units()" [value]="u.id">{{ u.name }} ({{ u.code }})</option>
              </select>
            </div>
            <div class="form-group">
              <label class="form-label">{{ i18n.t('التكلفة المعيارية', 'Standard Cost') }}</label>
              <input class="form-control" type="number" [(ngModel)]="form.standardCost" />
            </div>
            <div class="form-group">
              <label class="form-label">{{ i18n.t('الحد الأدنى للمخزون', 'Minimum Stock') }}</label>
              <input class="form-control" type="number" [(ngModel)]="form.minimumStockLevel" />
            </div>
          </div>
        </div>
        <div class="modal-footer">
          <button class="btn btn-secondary" (click)="showModal.set(false)">{{ i18n.t('إلغاء', 'Cancel') }}</button>
          <button class="btn btn-primary" (click)="save()">{{ i18n.t('حفظ', 'Save') }}</button>
        </div>
      </div>
    </div>
  `
})
export class RawMaterialsComponent implements OnInit {
  private api = inject(ApiService);
  i18n = inject(I18nService);

  items = signal<any[]>([]);
  categories = signal<any[]>([]);
  units = signal<any[]>([]);
  showModal = signal(false);
  error = signal('');
  search = '';
  editingId: string | null = null;
  form: any = {};

  ngOnInit() {
    this.api.get<any[]>('masterdata/categories').subscribe((d) => this.categories.set(d));
    this.api.get<any[]>('masterdata/units').subscribe((d) => this.units.set(d));
    this.load();
  }

  load() {
    this.api.get<any[]>('masterdata/raw-materials', { search: this.search }).subscribe((d) => this.items.set(d));
  }

  openModal() {
    this.editingId = null;
    this.form = {
      code: '', name: '', nameAr: '',
      categoryId: this.categories()[0]?.id || '',
      unitOfMeasureId: this.units()[0]?.id || '',
      standardCost: 0, minimumStockLevel: 10, trackBatch: true, isActive: true
    };
    this.showModal.set(true);
  }

  edit(item: any) {
    this.editingId = item.id;
    this.form = { ...item };
    this.showModal.set(true);
  }

  save() {
    this.error.set('');
    const req$ = this.editingId
      ? this.api.put(`masterdata/raw-materials/${this.editingId}`, this.form)
      : this.api.post('masterdata/raw-materials', this.form);
    req$.subscribe({
      next: () => { this.showModal.set(false); this.load(); },
      error: (err) => this.error.set(err?.error?.message || 'Error saving')
    });
  }

  remove(id: string) {
    if (!confirm(this.i18n.t('هل أنت متأكد من الحذف؟', 'Delete this raw material?'))) return;
    this.api.delete(`masterdata/raw-materials/${id}`).subscribe({
      next: () => this.load(),
      error: (err) => this.error.set(err?.error?.message || 'Cannot delete')
    });
  }
}

