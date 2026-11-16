import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../core/services/api.service';
import { I18nService } from '../../core/services/i18n.service';

@Component({
  selector: 'app-warehouses',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="page-header">
      <div>
        <h1 class="page-title">{{ i18n.t('المستودعات وحركة المخزون', 'Warehouses & Stock Movements') }}</h1>
        <p class="page-subtitle">{{ i18n.t('أرصدة المستودعات، التحويل بين المستودعات، وسجل الحركات', 'Balances, inter-warehouse transfers & stock ledger') }}</p>
      </div>
      <button class="btn btn-primary" (click)="showTransfer.set(true)">⇄ {{ i18n.t('تحويل مخزون', 'Transfer Stock') }}</button>
    </div>

    <div class="grid-2">
      <div class="card">
        <div class="card-header"><h3 class="card-title">{{ i18n.t('أرصدة المخزون', 'Stock Balances') }}</h3></div>
        <div class="table-wrapper">
          <table class="erp-table">
            <thead>
              <tr>
                <th>{{ i18n.t('المستودع', 'Warehouse') }}</th>
                <th>{{ i18n.t('الصنف', 'Item') }}</th>
                <th>{{ i18n.t('الكمية', 'Qty On Hand') }}</th>
                <th>{{ i18n.t('متوسط التكلفة', 'Avg Cost') }}</th>
                <th>{{ i18n.t('القيمة', 'Total Value') }}</th>
              </tr>
            </thead>
            <tbody>
              <tr *ngFor="let b of balances()">
                <td>{{ b.warehouse?.name }}</td>
                <td>{{ b.rawMaterial?.name || b.product?.name }}</td>
                <td><strong>{{ b.quantityOnHand | number:'1.2-2' }}</strong></td>
                <td>{{ b.averageUnitCost | number:'1.2-4' }}</td>
                <td>{{ b.totalValue | number:'1.2-2' }}</td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>

      <div class="card">
        <div class="card-header"><h3 class="card-title">{{ i18n.t('سجل حركات المخزون', 'Inventory Transactions') }}</h3></div>
        <div class="table-wrapper">
          <table class="erp-table">
            <thead>
              <tr>
                <th>{{ i18n.t('المرجع', 'Tx #') }}</th>
                <th>{{ i18n.t('المستودع', 'Warehouse') }}</th>
                <th>{{ i18n.t('الصنف', 'Item') }}</th>
                <th>{{ i18n.t('الكمية', 'Qty') }}</th>
                <th>{{ i18n.t('التكلفة', 'Total Cost') }}</th>
              </tr>
            </thead>
            <tbody>
              <tr *ngFor="let t of transactions()">
                <td>{{ t.transactionNumber }}</td>
                <td>{{ t.warehouse?.name }}</td>
                <td>{{ t.rawMaterial?.name || t.product?.name }}</td>
                <td>{{ t.quantity }}</td>
                <td>{{ t.totalCost | number:'1.2-2' }}</td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </div>

    <div *ngIf="showTransfer()" class="modal-backdrop">
      <div class="modal-dialog">
        <div class="modal-header"><h3 class="card-title">{{ i18n.t('تحويل بين المستودعات', 'Inter-Warehouse Transfer') }}</h3><button class="btn btn-secondary btn-sm" (click)="showTransfer.set(false)">✕</button></div>
        <div class="modal-body">
          <div class="form-grid">
            <div class="form-group"><label class="form-label">From Warehouse</label>
              <select class="form-control" [(ngModel)]="tf.fromWarehouseId"><option *ngFor="let w of warehouses()" [value]="w.id">{{ w.name }}</option></select>
            </div>
            <div class="form-group"><label class="form-label">To Warehouse</label>
              <select class="form-control" [(ngModel)]="tf.toWarehouseId"><option *ngFor="let w of warehouses()" [value]="w.id">{{ w.name }}</option></select>
            </div>
            <div class="form-group"><label class="form-label">Raw Material</label>
              <select class="form-control" [(ngModel)]="tf.rawMaterialId"><option *ngFor="let rm of rawMaterials()" [value]="rm.id">{{ rm.name }}</option></select>
            </div>
            <div class="form-group"><label class="form-label">Quantity</label><input class="form-control" type="number" [(ngModel)]="tf.quantity" /></div>
          </div>
        </div>
        <div class="modal-footer">
          <button class="btn btn-secondary" (click)="showTransfer.set(false)">Cancel</button>
          <button class="btn btn-primary" (click)="transfer()">Transfer</button>
        </div>
      </div>
    </div>
  `
})
export class WarehousesComponent implements OnInit {
  private api = inject(ApiService);
  i18n = inject(I18nService);

  balances = signal<any[]>([]);
  transactions = signal<any[]>([]);
  warehouses = signal<any[]>([]);
  rawMaterials = signal<any[]>([]);
  showTransfer = signal(false);
  tf: any = { fromWarehouseId: '', toWarehouseId: '', rawMaterialId: '', quantity: 10 };

  ngOnInit() {
    this.load();
    this.api.get<any[]>('masterdata/warehouses').subscribe((w) => {
      this.warehouses.set(w);
      this.tf.fromWarehouseId = w[0]?.id || '';
      this.tf.toWarehouseId = w[1]?.id || w[0]?.id || '';
    });
    this.api.get<any[]>('masterdata/raw-materials').subscribe((r) => {
      this.rawMaterials.set(r);
      this.tf.rawMaterialId = r[0]?.id || '';
    });
  }

  load() {
    this.api.get<any[]>('masterdata/stock-balances').subscribe((d) => this.balances.set(d));
    this.api.get<any[]>('masterdata/inventory-transactions').subscribe((d) => this.transactions.set(d));
  }

  transfer() {
    this.api.post('masterdata/stock-transfer', this.tf).subscribe(() => { this.showTransfer.set(false); this.load(); });
  }
}

