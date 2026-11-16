import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../core/services/api.service';
import { I18nService } from '../../core/services/i18n.service';

@Component({
  selector: 'app-products',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="page-header">
      <div>
        <h1 class="page-title">{{ i18n.t('المنتجات النهائية ونصف المصنعة', 'Finished & Semi-Finished Products') }}</h1>
        <p class="page-subtitle">{{ i18n.t('الأكواد SKU، الباركود، أسعار البيع وتكلفة التصنيع', 'SKUs, barcodes, selling prices & manufacturing costs') }}</p>
      </div>
      <button class="btn btn-primary" (click)="openModal()">+ {{ i18n.t('منتج جديد', 'New Product') }}</button>
    </div>

    <div class="card">
      <div class="table-toolbar">
        <input class="form-control" style="max-width:300px" [(ngModel)]="search" (ngModelChange)="load()" [placeholder]="i18n.t('بحث بالباركود أو SKU...', 'Search SKU or barcode...')" />
      </div>
      <div class="table-wrapper">
        <table class="erp-table">
          <thead>
            <tr>
              <th>SKU</th>
              <th>{{ i18n.t('الباركود', 'Barcode') }}</th>
              <th>{{ i18n.t('المنتج', 'Product') }}</th>
              <th>{{ i18n.t('النوع', 'Type') }}</th>
              <th>{{ i18n.t('تكلفة التصنيع', 'Std Cost') }}</th>
              <th>{{ i18n.t('سعر البيع', 'Selling Price') }}</th>
              <th>{{ i18n.t('الرصيد', 'On Hand') }}</th>
              <th>{{ i18n.t('إجراءات', 'Actions') }}</th>
            </tr>
          </thead>
          <tbody>
            <tr *ngFor="let p of items()">
              <td><strong>{{ p.sku }}</strong></td>
              <td>{{ p.barcode || '-' }}</td>
              <td>{{ i18n.lang() === 'ar' ? (p.nameAr || p.name) : p.name }}</td>
              <td><span class="badge badge-info">{{ p.productType === 1 ? i18n.t('نصف مصنع', 'Semi-Finished') : i18n.t('نهائي', 'Finished') }}</span></td>
              <td>{{ p.standardCost | number:'1.2-2' }}</td>
              <td>{{ p.sellingPrice | number:'1.2-2' }}</td>
              <td><span class="badge badge-success">{{ p.quantityOnHand | number:'1.2-2' }}</span></td>
              <td><button class="btn btn-secondary btn-sm" (click)="edit(p)">{{ i18n.t('تعديل', 'Edit') }}</button></td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>

    <div *ngIf="showModal()" class="modal-backdrop">
      <div class="modal-dialog">
        <div class="modal-header">
          <h3 class="card-title">{{ editingId ? i18n.t('تعديل منتج', 'Edit Product') : i18n.t('منتج جديد', 'New Product') }}</h3>
          <button class="btn btn-secondary btn-sm" (click)="showModal.set(false)">✕</button>
        </div>
        <div class="modal-body">
          <div class="form-grid">
            <div class="form-group">
              <label class="form-label">SKU</label>
              <input class="form-control" [(ngModel)]="form.sku" />
            </div>
            <div class="form-group">
              <label class="form-label">{{ i18n.t('الباركود', 'Barcode') }}</label>
              <input class="form-control" [(ngModel)]="form.barcode" />
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
              <label class="form-label">{{ i18n.t('النوع', 'Product Type') }}</label>
              <select class="form-control" [(ngModel)]="form.productType">
                <option [ngValue]="2">{{ i18n.t('منتج نهائي', 'Finished') }}</option>
                <option [ngValue]="1">{{ i18n.t('نصف مصنع', 'Semi-Finished') }}</option>
              </select>
            </div>
            <div class="form-group">
              <label class="form-label">{{ i18n.t('التصنيف', 'Category') }}</label>
              <select class="form-control" [(ngModel)]="form.categoryId">
                <option *ngFor="let c of categories()" [value]="c.id">{{ c.name }}</option>
              </select>
            </div>
            <div class="form-group">
              <label class="form-label">{{ i18n.t('الوحدة', 'Unit') }}</label>
              <select class="form-control" [(ngModel)]="form.unitOfMeasureId">
                <option *ngFor="let u of units()" [value]="u.id">{{ u.name }}</option>
              </select>
            </div>
            <div class="form-group">
              <label class="form-label">{{ i18n.t('سعر البيع', 'Selling Price') }}</label>
              <input class="form-control" type="number" [(ngModel)]="form.sellingPrice" />
            </div>
            <div class="form-group">
              <label class="form-label">{{ i18n.t('التكلفة المعيارية', 'Standard Cost') }}</label>
              <input class="form-control" type="number" [(ngModel)]="form.standardCost" />
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
export class ProductsComponent implements OnInit {
  private api = inject(ApiService);
  i18n = inject(I18nService);

  items = signal<any[]>([]);
  categories = signal<any[]>([]);
  units = signal<any[]>([]);
  showModal = signal(false);
  search = '';
  editingId: string | null = null;
  form: any = {};

  ngOnInit() {
    this.api.get<any[]>('masterdata/categories').subscribe((d) => this.categories.set(d));
    this.api.get<any[]>('masterdata/units').subscribe((d) => this.units.set(d));
    this.load();
  }

  load() {
    this.api.get<any[]>('masterdata/products', { search: this.search }).subscribe((d) => this.items.set(d));
  }

  openModal() {
    this.editingId = null;
    this.form = {
      sku: '', barcode: '', name: '', nameAr: '', productType: 2,
      categoryId: this.categories()[0]?.id || '', unitOfMeasureId: this.units()[0]?.id || '',
      sellingPrice: 0, standardCost: 0, minimumStockLevel: 10, isActive: true
    };
    this.showModal.set(true);
  }

  edit(p: any) {
    this.editingId = p.id;
    this.form = { ...p };
    this.showModal.set(true);
  }

  save() {
    const req$ = this.editingId
      ? this.api.put(`masterdata/products/${this.editingId}`, this.form)
      : this.api.post('masterdata/products', this.form);
    req$.subscribe(() => { this.showModal.set(false); this.load(); });
  }
}

