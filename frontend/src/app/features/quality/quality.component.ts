import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../core/services/api.service';
import { I18nService } from '../../core/services/i18n.service';

@Component({
  selector: 'app-quality',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="page-header">
      <div>
        <h1 class="page-title">{{ i18n.t('إدارة الجودة والفحص', 'Quality Control & Inspections') }}</h1>
        <p class="page-subtitle">{{ i18n.t('فحص المواد الخام وأثناء الإنتاج والمنتج النهائي وتسجيل العيوب', 'Raw material, in-process & finished goods inspections') }}</p>
      </div>
      <button class="btn btn-primary" (click)="showModal.set(true)">+ {{ i18n.t('فحص جودة جديد', 'New Inspection') }}</button>
    </div>

    <div class="card">
      <div class="table-wrapper">
        <table class="erp-table">
          <thead>
            <tr>
              <th>{{ i18n.t('رقم الفحص', 'Inspection #') }}</th>
              <th>{{ i18n.t('النوع', 'Type') }}</th>
              <th>{{ i18n.t('المفحوص', 'Inspected') }}</th>
              <th>{{ i18n.t('المقبول', 'Accepted') }}</th>
              <th>{{ i18n.t('المرفوض', 'Rejected') }}</th>
              <th>{{ i18n.t('السبب', 'Defect Reason') }}</th>
              <th>{{ i18n.t('الحالة', 'Status') }}</th>
            </tr>
          </thead>
          <tbody>
            <tr *ngFor="let q of items()">
              <td><strong>{{ q.inspectionNumber }}</strong></td>
              <td>{{ q.inspectionType === 1 ? 'Raw Material' : q.inspectionType === 2 ? 'In-Process' : 'Finished Product' }}</td>
              <td>{{ q.inspectedQuantity }}</td>
              <td>{{ q.acceptedQuantity }}</td>
              <td>{{ q.rejectedQuantity }}</td>
              <td>{{ q.defectReason || '-' }}</td>
              <td>
                <span class="badge" [class.badge-success]="q.status === 2" [class.badge-warning]="q.status === 3" [class.badge-danger]="q.status === 4">
                  {{ q.status === 2 ? 'Passed' : q.status === 3 ? 'Partial' : 'Failed' }}
                </span>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>

    <div *ngIf="showModal()" class="modal-backdrop">
      <div class="modal-dialog">
        <div class="modal-header"><h3 class="card-title">{{ i18n.t('تسجيل فحص جودة', 'Record Quality Inspection') }}</h3><button class="btn btn-secondary btn-sm" (click)="showModal.set(false)">✕</button></div>
        <div class="modal-body">
          <div class="form-grid">
            <div class="form-group"><label class="form-label">Type</label>
              <select class="form-control" [(ngModel)]="form.inspectionType">
                <option [ngValue]="1">Raw Material</option><option [ngValue]="2">In-Process</option><option [ngValue]="3">Finished Product</option>
              </select>
            </div>
            <div class="form-group"><label class="form-label">Inspected Qty</label><input class="form-control" type="number" [(ngModel)]="form.inspectedQuantity" /></div>
            <div class="form-group"><label class="form-label">Accepted Qty</label><input class="form-control" type="number" [(ngModel)]="form.acceptedQuantity" /></div>
            <div class="form-group"><label class="form-label">Rejected Qty</label><input class="form-control" type="number" [(ngModel)]="form.rejectedQuantity" /></div>
            <div class="form-group"><label class="form-label">Defect Reason</label><input class="form-control" [(ngModel)]="form.defectReason" /></div>
          </div>
        </div>
        <div class="modal-footer">
          <button class="btn btn-secondary" (click)="showModal.set(false)">Cancel</button>
          <button class="btn btn-primary" (click)="save()">Save</button>
        </div>
      </div>
    </div>
  `
})
export class QualityComponent implements OnInit {
  private api = inject(ApiService);
  i18n = inject(I18nService);
  items = signal<any[]>([]);
  showModal = signal(false);
  form: any = { inspectionType: 3, inspectedQuantity: 100, acceptedQuantity: 98, rejectedQuantity: 2, defectReason: '' };

  ngOnInit() { this.load(); }
  load() { this.api.get<any[]>('quality').subscribe((d) => this.items.set(d)); }
  save() { this.api.post('quality', this.form).subscribe(() => { this.showModal.set(false); this.load(); }); }
}

