import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../core/services/api.service';
import { I18nService } from '../../core/services/i18n.service';

@Component({
  selector: 'app-sales',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="page-header">
      <div>
        <h1 class="page-title">{{ i18n.t('المبيعات والعملاء', 'Sales & Customers') }}</h1>
        <p class="page-subtitle">{{ i18n.t('فواتير البيع النقدي والآجل مع خصم المخزون وتسجيل تكلفة المبيعات COGS آلياً', 'Cash & credit sales invoices with automatic stock issue & COGS posting') }}</p>
      </div>
      <div style="display:flex;gap:8px">
        <button class="btn btn-secondary" (click)="showCust.set(true)">+ {{ i18n.t('عميل جديد', 'New Customer') }}</button>
        <button class="btn btn-primary" (click)="openInv()">+ {{ i18n.t('فاتورة مبيعات', 'Post Sales Invoice') }}</button>
      </div>
    </div>

    <div *ngIf="error()" class="alert alert-error">{{ error() }}</div>

    <div class="grid-2">
      <div class="card">
        <div class="card-header"><h3 class="card-title">{{ i18n.t('العملاء', 'Customers') }}</h3></div>
        <div class="table-wrapper">
          <table class="erp-table">
            <thead><tr><th>{{ i18n.t('الكود', 'Code') }}</th><th>{{ i18n.t('الاسم', 'Name') }}</th><th>{{ i18n.t('الرصيد المستحق', 'Balance') }}</th></tr></thead>
            <tbody>
              <tr *ngFor="let c of customers()"><td>{{ c.code }}</td><td>{{ c.name }}</td><td><strong>{{ c.balance | number:'1.2-2' }}</strong></td></tr>
            </tbody>
          </table>
        </div>
      </div>

      <div class="card">
        <div class="card-header"><h3 class="card-title">{{ i18n.t('فواتير المبيعات', 'Sales Invoices') }}</h3></div>
        <div class="table-wrapper">
          <table class="erp-table">
            <thead><tr><th>{{ i18n.t('الرقم', 'Invoice #') }}</th><th>{{ i18n.t('العميل', 'Customer') }}</th><th>{{ i18n.t('الإجمالي', 'Total') }}</th><th>COGS</th></tr></thead>
            <tbody>
              <tr *ngFor="let i of invoices()">
                <td><strong>{{ i.invoiceNumber }}</strong></td><td>{{ i.customer?.name }}</td><td>{{ i.totalAmount | number:'1.2-2' }}</td><td>{{ i.totalCogs | number:'1.2-2' }}</td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </div>

    <div *ngIf="showCust()" class="modal-backdrop">
      <div class="modal-dialog">
        <div class="modal-header"><h3 class="card-title">{{ i18n.t('إضافة عميل', 'New Customer') }}</h3><button class="btn btn-secondary btn-sm" (click)="showCust.set(false)">✕</button></div>
        <div class="modal-body">
          <div class="form-grid">
            <div class="form-group"><label class="form-label">Code</label><input class="form-control" [(ngModel)]="custForm.code" /></div>
            <div class="form-group"><label class="form-label">Name</label><input class="form-control" [(ngModel)]="custForm.name" /></div>
            <div class="form-group"><label class="form-label">Phone</label><input class="form-control" [(ngModel)]="custForm.phone" /></div>
            <div class="form-group"><label class="form-label">Credit Limit</label><input class="form-control" type="number" [(ngModel)]="custForm.creditLimit" /></div>
          </div>
        </div>
        <div class="modal-footer">
          <button class="btn btn-secondary" (click)="showCust.set(false)">Cancel</button>
          <button class="btn btn-primary" (click)="saveCust()">Save</button>
        </div>
      </div>
    </div>

    <div *ngIf="showInv()" class="modal-backdrop">
      <div class="modal-dialog">
        <div class="modal-header"><h3 class="card-title">{{ i18n.t('ترحيل فاتورة مبيعات', 'Post Sales Invoice') }}</h3><button class="btn btn-secondary btn-sm" (click)="showInv.set(false)">✕</button></div>
        <div class="modal-body">
          <div class="form-grid" style="margin-bottom:12px">
            <div class="form-group"><label class="form-label">Customer</label>
              <select class="form-control" [(ngModel)]="invForm.customerId"><option *ngFor="let c of customers()" [value]="c.id">{{ c.name }}</option></select>
            </div>
            <div class="form-group"><label class="form-label">Warehouse</label>
              <select class="form-control" [(ngModel)]="invForm.warehouseId"><option *ngFor="let w of warehouses()" [value]="w.id">{{ w.name }}</option></select>
            </div>
            <div class="form-group"><label class="form-label">Payment Type</label>
              <select class="form-control" [(ngModel)]="invForm.isCashSale"><option [ngValue]="false">Credit</option><option [ngValue]="true">Cash</option></select>
            </div>
          </div>
          <div *ngFor="let line of invForm.items" class="form-grid">
            <div class="form-group"><label class="form-label">Product</label>
              <select class="form-control" [(ngModel)]="line.productId"><option *ngFor="let p of products()" [value]="p.id">{{ p.name }}</option></select>
            </div>
            <div class="form-group"><label class="form-label">Qty</label><input class="form-control" type="number" [(ngModel)]="line.quantity" /></div>
            <div class="form-group"><label class="form-label">Unit Price</label><input class="form-control" type="number" [(ngModel)]="line.unitPrice" /></div>
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
export class SalesComponent implements OnInit {
  private api = inject(ApiService);
  i18n = inject(I18nService);

  customers = signal<any[]>([]);
  invoices = signal<any[]>([]);
  warehouses = signal<any[]>([]);
  products = signal<any[]>([]);
  showCust = signal(false);
  showInv = signal(false);
  error = signal('');
  custForm: any = { code: '', name: '', phone: '', creditLimit: 50000 };
  invForm: any = { items: [] };

  ngOnInit() {
    this.load();
    this.api.get<any[]>('masterdata/warehouses').subscribe((d) => this.warehouses.set(d));
    this.api.get<any[]>('masterdata/products').subscribe((d) => this.products.set(d));
  }

  load() {
    this.api.get<any[]>('sales/customers').subscribe((d) => this.customers.set(d));
    this.api.get<any[]>('sales/invoices').subscribe((d) => this.invoices.set(d));
  }

  saveCust() { this.api.post('sales/customers', this.custForm).subscribe(() => { this.showCust.set(false); this.load(); }); }

  openInv() {
    this.invForm = {
      customerId: this.customers()[0]?.id || '',
      warehouseId: this.warehouses()[1]?.id || this.warehouses()[0]?.id || '',
      isCashSale: false, isReturn: false, currencyCode: 'USD', exchangeRate: 1, discountAmount: 0,
      items: [{ productId: this.products()[0]?.id || '', quantity: 5, unitPrice: this.products()[0]?.sellingPrice || 45, taxAmount: 0 }]
    };
    this.showInv.set(true);
  }

  saveInv() {
    this.error.set('');
    this.api.post('sales/invoices', this.invForm).subscribe({
      next: () => { this.showInv.set(false); this.load(); },
      error: (err) => this.error.set(err?.error?.message || 'Error posting invoice')
    });
  }
}

