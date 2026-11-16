import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../core/services/api.service';
import { I18nService } from '../../core/services/i18n.service';

@Component({
  selector: 'app-accounting',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="page-header">
      <div>
        <h1 class="page-title">{{ i18n.t('المحاسبة العامة والقيد المزدوج', 'General Ledger & Double-Entry Accounting') }}</h1>
        <p class="page-subtitle">{{ i18n.t('دليل الحسابات، القيود اليومية، ميزان المراجعة، قائمة الدخل والميزانية العمومية', 'Chart of Accounts, Journal Entries, Trial Balance, Income Statement & Balance Sheet') }}</p>
      </div>
      <button class="btn btn-primary" (click)="openJe()">+ {{ i18n.t('قيد يومي جديد', 'New Journal Entry') }}</button>
    </div>

    <div style="display:flex;gap:8px;margin-bottom:16px;flex-wrap:wrap">
      <button class="btn" [class.btn-primary]="tab() === 'je'" [class.btn-secondary]="tab() !== 'je'" (click)="tab.set('je')">{{ i18n.t('القيود اليومية', 'Journal Entries') }}</button>
      <button class="btn" [class.btn-primary]="tab() === 'coa'" [class.btn-secondary]="tab() !== 'coa'" (click)="tab.set('coa')">{{ i18n.t('دليل الحسابات', 'Chart of Accounts') }}</button>
      <button class="btn" [class.btn-primary]="tab() === 'tb'" [class.btn-secondary]="tab() !== 'tb'" (click)="loadTb()">{{ i18n.t('ميزان المراجعة', 'Trial Balance') }}</button>
      <button class="btn" [class.btn-primary]="tab() === 'is'" [class.btn-secondary]="tab() !== 'is'" (click)="loadIs()">{{ i18n.t('قائمة الدخل', 'Income Statement') }}</button>
      <button class="btn" [class.btn-primary]="tab() === 'bs'" [class.btn-secondary]="tab() !== 'bs'" (click)="loadBs()">{{ i18n.t('الميزانية العمومية', 'Balance Sheet') }}</button>
    </div>

    <div *ngIf="error()" class="alert alert-error">{{ error() }}</div>

    <!-- Journal Entries -->
    <div *ngIf="tab() === 'je'" class="card">
      <div class="table-wrapper">
        <table class="erp-table">
          <thead>
            <tr><th>{{ i18n.t('رقم القيد', 'Entry #') }}</th><th>{{ i18n.t('التاريخ', 'Date') }}</th><th>{{ i18n.t('البيان', 'Description') }}</th><th>{{ i18n.t('المدين', 'Debit') }}</th><th>{{ i18n.t('الدائن', 'Credit') }}</th><th>{{ i18n.t('الحالة', 'Status') }}</th><th>{{ i18n.t('إجراء', 'Action') }}</th></tr>
          </thead>
          <tbody>
            <tr *ngFor="let j of entries()">
              <td><strong>{{ j.entryNumber }}</strong></td>
              <td>{{ j.entryDate | date:'yyyy-MM-dd' }}</td>
              <td>{{ j.description }}</td>
              <td>{{ sumDebit(j.lines) | number:'1.2-2' }}</td>
              <td>{{ sumCredit(j.lines) | number:'1.2-2' }}</td>
              <td><span class="badge" [class.badge-success]="j.status === 2" [class.badge-warning]="j.status === 3">{{ j.status === 2 ? 'Posted' : 'Reversed' }}</span></td>
              <td>
                <button *ngIf="j.status === 2" class="btn btn-danger btn-sm" (click)="reverse(j.id)">{{ i18n.t('عكس القيد', 'Reverse') }}</button>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>

    <!-- Chart of Accounts -->
    <div *ngIf="tab() === 'coa'" class="card">
      <div class="table-wrapper">
        <table class="erp-table">
          <thead><tr><th>{{ i18n.t('الكود', 'Code') }}</th><th>{{ i18n.t('الحساب', 'Account') }}</th><th>{{ i18n.t('النوع', 'Type') }}</th></tr></thead>
          <tbody>
            <tr *ngFor="let a of accounts()">
              <td><strong>{{ a.code }}</strong></td>
              <td>{{ i18n.lang() === 'ar' ? (a.nameAr || a.name) : a.name }}</td>
              <td>{{ accountTypeName(a.accountType) }}</td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>

    <!-- Trial Balance -->
    <div *ngIf="tab() === 'tb' && tb()" class="card">
      <div class="table-wrapper">
        <table class="erp-table">
          <thead><tr><th>{{ i18n.t('الكود', 'Code') }}</th><th>{{ i18n.t('الحساب', 'Account') }}</th><th>{{ i18n.t('مدين', 'Debit') }}</th><th>{{ i18n.t('دائن', 'Credit') }}</th><th>{{ i18n.t('الصافي', 'Net') }}</th></tr></thead>
          <tbody>
            <tr *ngFor="let r of tb().rows">
              <td>{{ r.code }}</td><td>{{ i18n.lang() === 'ar' ? (r.nameAr || r.name) : r.name }}</td>
              <td>{{ r.totalDebit | number:'1.2-2' }}</td><td>{{ r.totalCredit | number:'1.2-2' }}</td><td>{{ r.netBalance | number:'1.2-2' }}</td>
            </tr>
          </tbody>
          <tfoot>
            <tr><th colspan="2">{{ i18n.t('الإجمالي', 'Totals') }}</th><th>{{ tb().totalDebit | number:'1.2-2' }}</th><th>{{ tb().totalCredit | number:'1.2-2' }}</th><th></th></tr>
          </tfoot>
        </table>
      </div>
    </div>

    <!-- Income Statement -->
    <div *ngIf="tab() === 'is' && isData()" class="card">
      <div class="kpi-grid">
        <div class="kpi-card"><div class="kpi-label">{{ i18n.t('إجمالي الإيرادات', 'Total Revenue') }}</div><div class="kpi-value">{{ isData().totalRevenue | number:'1.2-2' }}</div></div>
        <div class="kpi-card"><div class="kpi-label">{{ i18n.t('إجمالي المصروفات', 'Total Expenses') }}</div><div class="kpi-value">{{ isData().totalExpenses | number:'1.2-2' }}</div></div>
        <div class="kpi-card"><div class="kpi-label">{{ i18n.t('صافي الدخل', 'Net Income') }}</div><div class="kpi-value" [style.color]="isData().netIncome >= 0 ? 'var(--success)' : 'var(--danger)'">{{ isData().netIncome | number:'1.2-2' }}</div></div>
      </div>
    </div>

    <!-- Balance Sheet -->
    <div *ngIf="tab() === 'bs' && bsData()" class="card">
      <div class="kpi-grid">
        <div class="kpi-card"><div class="kpi-label">{{ i18n.t('إجمالي الأصول', 'Total Assets') }}</div><div class="kpi-value">{{ bsData().totalAssets | number:'1.2-2' }}</div></div>
        <div class="kpi-card"><div class="kpi-label">{{ i18n.t('الالتزامات وحقوق الملكية', 'Liabilities & Equity') }}</div><div class="kpi-value">{{ bsData().totalLiabilitiesAndEquity | number:'1.2-2' }}</div></div>
      </div>
    </div>

    <!-- Manual JE Modal -->
    <div *ngIf="showJe()" class="modal-backdrop">
      <div class="modal-dialog">
        <div class="modal-header"><h3 class="card-title">{{ i18n.t('قيد يومي يدوي', 'Manual Journal Entry') }}</h3><button class="btn btn-secondary btn-sm" (click)="showJe.set(false)">✕</button></div>
        <div class="modal-body">
          <div class="form-group" style="margin-bottom:12px">
            <label class="form-label">{{ i18n.t('البيان', 'Description') }}</label>
            <input class="form-control" [(ngModel)]="jeForm.description" />
          </div>
          <div *ngFor="let l of jeForm.lines" class="form-grid" style="margin-bottom:8px">
            <div class="form-group"><label class="form-label">Account</label>
              <select class="form-control" [(ngModel)]="l.accountKeyOrId"><option *ngFor="let a of accounts()" [value]="a.id">{{ a.code }} - {{ a.name }}</option></select>
            </div>
            <div class="form-group"><label class="form-label">Debit</label><input class="form-control" type="number" [(ngModel)]="l.debit" /></div>
            <div class="form-group"><label class="form-label">Credit</label><input class="form-control" type="number" [(ngModel)]="l.credit" /></div>
          </div>
          <button class="btn btn-secondary btn-sm" (click)="addJeLine()">+ Add Line</button>
        </div>
        <div class="modal-footer">
          <button class="btn btn-secondary" (click)="showJe.set(false)">Cancel</button>
          <button class="btn btn-primary" (click)="saveJe()">Post Entry</button>
        </div>
      </div>
    </div>
  `
})
export class AccountingComponent implements OnInit {
  private api = inject(ApiService);
  i18n = inject(I18nService);

  tab = signal<'je' | 'coa' | 'tb' | 'is' | 'bs'>('je');
  entries = signal<any[]>([]);
  accounts = signal<any[]>([]);
  tb = signal<any>(null);
  isData = signal<any>(null);
  bsData = signal<any>(null);
  showJe = signal(false);
  error = signal('');
  jeForm: any = { entryDate: new Date().toISOString(), description: '', lines: [] };

  ngOnInit() {
    this.load();
  }

  load() {
    this.api.get<any[]>('accounting/journal-entries').subscribe((d) => this.entries.set(d));
    this.api.get<any[]>('accounting/accounts').subscribe((d) => this.accounts.set(d));
  }

  loadTb() { this.tab.set('tb'); this.api.get('accounting/trial-balance').subscribe((d) => this.tb.set(d)); }
  loadIs() { this.tab.set('is'); this.api.get('accounting/income-statement').subscribe((d) => this.isData.set(d)); }
  loadBs() { this.tab.set('bs'); this.api.get('accounting/balance-sheet').subscribe((d) => this.bsData.set(d)); }

  openJe() {
    const firstId = this.accounts()[0]?.id || '';
    this.jeForm = {
      entryDate: new Date().toISOString(), description: '',
      lines: [
        { accountKeyOrId: firstId, debit: 0, credit: 0, memo: '' },
        { accountKeyOrId: firstId, debit: 0, credit: 0, memo: '' }
      ]
    };
    this.showJe.set(true);
  }

  addJeLine() {
    this.jeForm.lines.push({ accountKeyOrId: this.accounts()[0]?.id || '', debit: 0, credit: 0, memo: '' });
  }

  saveJe() {
    this.error.set('');
    this.api.post('accounting/journal-entries', this.jeForm).subscribe({
      next: () => { this.showJe.set(false); this.load(); },
      error: (err) => this.error.set(err?.error?.message || 'Unbalanced entry')
    });
  }

  reverse(id: string) {
    const reason = prompt(this.i18n.t('سبب عكس القيد:', 'Reversal reason:'));
    if (!reason) return;
    this.api.post(`accounting/journal-entries/${id}/reverse`, { reason }).subscribe(() => this.load());
  }

  sumDebit(lines: any[]) { return (lines || []).reduce((s, l) => s + l.debit, 0); }
  sumCredit(lines: any[]) { return (lines || []).reduce((s, l) => s + l.credit, 0); }
  accountTypeName(t: number) { return ['', 'Asset', 'Liability', 'Equity', 'Revenue', 'Expense'][t] || ''; }
}

