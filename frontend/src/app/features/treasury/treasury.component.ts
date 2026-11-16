import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../core/services/api.service';
import { I18nService } from '../../core/services/i18n.service';

@Component({
  selector: 'app-treasury',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="page-header">
      <div>
        <h1 class="page-title">{{ i18n.t('الصندوق والبنوك', 'Treasury, Cashboxes & Banks') }}</h1>
        <p class="page-subtitle">{{ i18n.t('سندات القبض والصرف، التحويلات البنكية، والإغلاق اليومي للصندوق', 'Receipts, payments, transfers & daily cashbox closing') }}</p>
      </div>
      <button class="btn btn-primary" (click)="openVoucher()">+ {{ i18n.t('سند جديد', 'New Voucher') }}</button>
    </div>

    <div class="kpi-grid">
      <div *ngFor="let c of cashboxes()" class="kpi-card">
        <div class="kpi-label">{{ c.name }} ({{ c.code }})</div>
        <div class="kpi-value">{{ c.currentBalance | number:'1.2-2' }} {{ c.currencyCode }}</div>
        <button class="btn btn-secondary btn-sm" style="margin-top:8px" (click)="closeCashbox(c.id)">{{ i18n.t('إغلاق يومي', 'Daily Close') }}</button>
      </div>
      <div *ngFor="let b of banks()" class="kpi-card">
        <div class="kpi-label">{{ b.bankName }} ({{ b.accountNumber }})</div>
        <div class="kpi-value">{{ b.currentBalance | number:'1.2-2' }} {{ b.currencyCode }}</div>
      </div>
    </div>

    <div class="card">
      <div class="card-header"><h3 class="card-title">{{ i18n.t('حركة السندات', 'Treasury Vouchers') }}</h3></div>
      <div class="table-wrapper">
        <table class="erp-table">
          <thead><tr><th>{{ i18n.t('الرقم', 'Voucher #') }}</th><th>{{ i18n.t('النوع', 'Type') }}</th><th>{{ i18n.t('المبلغ', 'Amount') }}</th><th>{{ i18n.t('البيان', 'Description') }}</th><th>{{ i18n.t('التاريخ', 'Date') }}</th></tr></thead>
          <tbody>
            <tr *ngFor="let v of vouchers()">
              <td><strong>{{ v.voucherNumber }}</strong></td>
              <td>{{ v.voucherType === 1 ? 'Receipt' : v.voucherType === 2 ? 'Payment' : 'Transfer' }}</td>
              <td>{{ v.amount | number:'1.2-2' }}</td>
              <td>{{ v.description }}</td>
              <td>{{ v.voucherDate | date:'yyyy-MM-dd' }}</td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>

    <div *ngIf="showModal()" class="modal-backdrop">
      <div class="modal-dialog">
        <div class="modal-header"><h3 class="card-title">{{ i18n.t('إنشاء سند مالي', 'Create Treasury Voucher') }}</h3><button class="btn btn-secondary btn-sm" (click)="showModal.set(false)">✕</button></div>
        <div class="modal-body">
          <div class="form-grid">
            <div class="form-group"><label class="form-label">Type</label>
              <select class="form-control" [(ngModel)]="form.voucherType">
                <option [ngValue]="1">Receipt (قبض)</option>
                <option [ngValue]="2">Payment (صرف)</option>
              </select>
            </div>
            <div class="form-group"><label class="form-label">Cashbox</label>
              <select class="form-control" [(ngModel)]="form.cashboxId"><option *ngFor="let c of cashboxes()" [value]="c.id">{{ c.name }}</option></select>
            </div>
            <div class="form-group"><label class="form-label">Amount</label><input class="form-control" type="number" [(ngModel)]="form.amount" /></div>
            <div class="form-group"><label class="form-label">Description</label><input class="form-control" [(ngModel)]="form.description" /></div>
          </div>
        </div>
        <div class="modal-footer">
          <button class="btn btn-secondary" (click)="showModal.set(false)">Cancel</button>
          <button class="btn btn-primary" (click)="save()">Post Voucher</button>
        </div>
      </div>
    </div>
  `
})
export class TreasuryComponent implements OnInit {
  private api = inject(ApiService);
  i18n = inject(I18nService);

  cashboxes = signal<any[]>([]);
  banks = signal<any[]>([]);
  vouchers = signal<any[]>([]);
  showModal = signal(false);
  form: any = { voucherType: 1, cashboxId: '', amount: 100, description: '' };

  ngOnInit() { this.load(); }
  load() {
    this.api.get<any[]>('accounting/cashboxes').subscribe((d) => this.cashboxes.set(d));
    this.api.get<any[]>('accounting/bank-accounts').subscribe((d) => this.banks.set(d));
    this.api.get<any[]>('accounting/vouchers').subscribe((d) => this.vouchers.set(d));
  }
  openVoucher() {
    this.form = { voucherType: 1, cashboxId: this.cashboxes()[0]?.id || '', amount: 100, description: '', currencyCode: 'USD', exchangeRate: 1 };
    this.showModal.set(true);
  }
  save() { this.api.post('accounting/vouchers', this.form).subscribe(() => { this.showModal.set(false); this.load(); }); }
  closeCashbox(id: string) { this.api.post(`accounting/cashboxes/${id}/close`, {}).subscribe(() => this.load()); }
}

