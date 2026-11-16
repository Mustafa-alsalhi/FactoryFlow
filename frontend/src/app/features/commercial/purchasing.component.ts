import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../core/services/api.service';
import { I18nService } from '../../core/services/i18n.service';

@Component({
  selector: 'app-purchasing',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="page-header">
      <div>
        <h1 class="page-title">{{ i18n.t('المشتريات والموردون', 'Purchasing & Suppliers') }}</h1>
        <p class="page-subtitle">{{ i18n.t('إدارة الموردين وفواتير الشراء مع الترحيل الآلي للمخزون والمحاسبة', 'Suppliers, purchase invoices with automated stock & GL posting') }}</p>
      </div>
      <div style="display:flex;gap:8px">
        <button class="btn btn-secondary" (click)="showSup.set(true)">+ {{ i18n.t('مورد جديد', 'New Supplier') }}</button>
        <button class="btn btn-primary" (click)="openInv()">+ {{ i18n.t('فاتورة مشتريات', 'Post Purchase Invoice') }}</button>
      </div>
    </div>

    <div class="grid-2">
      <div class="card">
        <div class="card-header"><h3 class="card-title">{{ i18n.t('الموردون', 'Suppliers') }}</h3></div>
        <div class="table-wrapper">
          <table class="erp-table">
            <thead>
              <tr><th>{{ i18n.t('الكود', 'Code') }}</th><th>{{ i18n.t('الاسم', 'Name') }}</th><th>{{ i18n.t('الهاتف', 'Phone') }}</th><th>{{ i18n.t('الرصيد المستحق', 'Balance') }}</th></tr>
            </thead>
            <tbody>
              <tr *ngFor="let s of suppliers()">
                <td>{{ s.code }}</td><td>{{ s.name }}</td><td>{{ s.phone }}</td><td><strong>{{ s.balance | number:'1.2-2' }}</strong></td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>

      <div class="card">
        <div class="card-header"><h3 class="card-title">{{ i18n.t('فواتير المشتريات', 'Purchase Invoices') }}</h3></div>
        <div class="table-wrapper">
          <table class="erp-table">
            <thead>
              <tr><th>{{ i18n.t('الرقم', 'Invoice #') }}</th><th>{{ i18n.t('المورد', 'Supplier') }}</th><th>{{ i18n.t('الإجمالي', 'Total') }}</th><th>{{ i18n.t('النوع', 'Type') }}</th></tr>
            </thead>
            <tbody>
              <tr *ngFor="let i of invoices()">
                <td><strong>{{ i.invoiceNumber }}</strong></td>
                <td>{{ i.supplier?.name }}</td>
                <td>{{ i.totalAmount | number:'1.2-2' }}</td>
                <td><span class="badge" [class.badge-warning]="i.isReturn" [class.badge-success]="!i.isReturn">{{ i.isReturn ? 'Return' : 'Purchase' }}</span></td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </div>

    <div *ngIf="showSup()" class="modal-backdrop">
      <div class="modal-dialog">
        <div class="modal-header"><h3 class="card-title">{{ i18n.t('إضافة مورد', 'New Supplier') }}</h3><button class="btn btn-secondary btn-sm" (click)="showSup.set(false)">✕</button></div>
        <div class="modal-body">
          <div class="form-grid">
            <div class="form-group"><label class="form-label">Code</label><input class="form-control" [(ngModel)]="supForm.code" /></div>
            <div class="form-group"><label class="form-label">Name</label><input class="form-control" [(ngModel)]="supForm.name" /></div>
            <div class="form-group"><label class="form-label">Phone</label><input class="form-control" [(ngModel)]="supForm.phone" /></div>
            <div class="form-group"><label class="form-label">Email</label><input class="form-control" [(ngModel)]="supForm.email" /></div>
          </div>
        </div>
        <div class="modal-footer">
          <button class="btn btn-secondary" (click)="showSup.set(false)">Cancel</button>
          <button class="btn btn-primary" (click)="saveSup()">Save</button>
        </div>
      </div>
    </div>

    <div *ngIf="showInv()" class="modal-backdrop">
      <div class="modal-dialog">
        <div class="modal-header"><h3 class="card-title">{{ i18n.t('ترحيل فاتورة مشتريات', 'Post Purchase Invoice') }}</h3><button class="btn btn-secondary btn-sm" (click)="showInv.set(false)">✕</button></div>
        <div class="modal-body">
          <div class="form-grid" style="margin-bottom:12px">
            <div class="form-group"><label class="form-label">Supplier</label>
              <select class="form-control" [(ngModel)]="invForm.supplierId"><option *ngFor="let s of suppliers()" [value]="s.id">{{ s.name }}</option></select>
            </div>
            <div class="form-group"><label class="form-label">Warehouse</label>
              <select class="form-control" [(ngModel)]="invForm.warehouseId"><option *ngFor="let w of warehouses()" [value]="w.id">{{ w.name }}</option></select>
            </div>
          </div>
          <div *ngFor="let line of invForm.items" class="form-grid">
            <div class="form-group"><label class="form-label">Raw Material</label>
              <select class="form-control" [(ngModel)]="line.rawMaterialId"><option *ngFor="let r of rawMaterials()" [value]="r.id">{{ r.name }}</option></select>
            </div>
            <div class="form-group"><label class="form-label">Qty</label><input class="form-control" type="number" [(ngModel)]="line.quantity" /></div>
            <div class="form-group"><label class="form-label">Unit Price</label><input class="form-control" type="number" [(ngModel)]="line.unitPrice" /></div>
            <div class="form-group"><label class="form-label">Tax Amount</label><input class="form-control" type="number" [(ngModel)]="line.taxAmount" /></div>
          </div>
        </div>
        <div class="modal-footer">
          <button class="btn btn-secondary" (click)="showInv.set(false)">Cancel</button>
          <button class="btn btn-primary" (click)="saveInv()">Post Invoice</button>
        </div>
      </div>
    </div>
  `
})
export class PurchasingComponent implements OnInit {
  private api = inject(ApiService);
  i18n = inject(I18nService);

  suppliers = signal<any[]>([]);
  invoices = signal<any[]>([]);
  warehouses = signal<any[]>([]);
  rawMaterials = signal<any[]>([]);
  showSup = signal(false);
  showInv = signal(false);
  supForm: any = { code: '', name: '', phone: '', email: '' };
  invForm: any = { items: [] };

  ngOnInit() {
    this.load();
    this.api.get<any[]>('masterdata/warehouses').subscribe((d) => this.warehouses.set(d));
    this.api.get<any[]>('masterdata/raw-materials').subscribe((d) => this.rawMaterials.set(d));
  }

  load() {
    this.api.get<any[]>('purchasing/suppliers').subscribe((d) => this.suppliers.set(d));
    this.api.get<any[]>('purchasing/invoices').subscribe((d) => this.invoices.set(d));
  }

  saveSup() {
    this.api.post('purchasing/suppliers', this.supForm).subscribe(() => { this.showSup.set(false); this.load(); });
  }

  openInv() {
    this.invForm = {
      supplierId: this.suppliers()[0]?.id || '',
      warehouseId: this.warehouses()[0]?.id || '',
      isReturn: false, currencyCode: 'USD', exchangeRate: 1, discountAmount: 0,
      items: [{ rawMaterialId: this.rawMaterials()[0]?.id || '', quantity: 100, unitPrice: 2.5, taxAmount: 0 }]
    };
    this.showInv.set(true);
  }

  saveInv() {
    this.api.post('purchasing/invoices', this.invForm).subscribe(() => { this.showInv.set(false); this.load(); });
  }
}

