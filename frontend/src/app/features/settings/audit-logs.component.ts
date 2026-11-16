import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ApiService } from '../../core/services/api.service';
import { I18nService } from '../../core/services/i18n.service';

@Component({
  selector: 'app-audit-logs',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="page-header">
      <div>
        <h1 class="page-title">{{ i18n.t('سجل التدقيق والرقابة', 'Audit Trail & Security Logs') }}</h1>
        <p class="page-subtitle">{{ i18n.t('تتبع كافة العمليات المالية والإنتاجية وتغييرات البيانات', 'Immutable log of financial, manufacturing & administrative actions') }}</p>
      </div>
    </div>

    <div class="card">
      <div class="table-wrapper">
        <table class="erp-table">
          <thead><tr><th>{{ i18n.t('التاريخ', 'Timestamp') }}</th><th>{{ i18n.t('المستخدم', 'User') }}</th><th>{{ i18n.t('العملية', 'Action') }}</th><th>{{ i18n.t('الكيان', 'Entity') }}</th><th>IP</th></tr></thead>
          <tbody>
            <tr *ngFor="let a of logs()">
              <td>{{ a.createdAt | date:'yyyy-MM-dd HH:mm:ss' }}</td>
              <td>{{ a.userEmail }}</td>
              <td><span class="badge badge-info">{{ a.action }}</span></td>
              <td>{{ a.entityName }} ({{ a.entityId | slice:0:8 }})</td>
              <td>{{ a.ipAddress || '-' }}</td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>
  `
})
export class AuditLogsComponent implements OnInit {
  private api = inject(ApiService);
  i18n = inject(I18nService);
  logs = signal<any[]>([]);

  ngOnInit() {
    this.api.get<any[]>('reports/audit-logs').subscribe((d) => this.logs.set(d));
  }
}

