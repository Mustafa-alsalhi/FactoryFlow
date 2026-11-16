import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../core/services/api.service';
import { I18nService } from '../../core/services/i18n.service';

@Component({
  selector: 'app-settings',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="page-header">
      <div>
        <h1 class="page-title">{{ i18n.t('إعدادات المؤسسة والمستخدمين والصلاحيات', 'Tenant Settings, Users & RBAC') }}</h1>
        <p class="page-subtitle">{{ i18n.t('سياسة تقييم المخزون، توزيع التكاليف، إدارة الموظفين والأدوار', 'Inventory valuation, overhead allocation, users & role permissions') }}</p>
      </div>
    </div>

    <div *ngIf="savedMsg()" class="alert alert-success">{{ savedMsg() }}</div>

    <div class="card" style="margin-bottom:20px" *ngIf="tenant">
      <div class="card-header"><h3 class="card-title">{{ i18n.t('بيانات المؤسسة والسياسات المحاسبية', 'Company Profile & Policies') }}</h3></div>
      <div class="form-grid">
        <div class="form-group"><label class="form-label">Name (EN)</label><input class="form-control" [(ngModel)]="tenant.name" /></div>
        <div class="form-group"><label class="form-label">Name (AR)</label><input class="form-control" [(ngModel)]="tenant.nameAr" /></div>
        <div class="form-group"><label class="form-label">Base Currency</label><input class="form-control" [(ngModel)]="tenant.baseCurrencyCode" /></div>
        <div class="form-group"><label class="form-label">Inventory Valuation</label>
          <select class="form-control" [(ngModel)]="tenant.valuationMethod">
            <option [ngValue]="1">FIFO</option>
            <option [ngValue]="2">Weighted Average</option>
          </select>
        </div>
        <div class="form-group"><label class="form-label">Overhead Allocation</label>
          <select class="form-control" [(ngModel)]="tenant.overheadAllocationMethod">
            <option [ngValue]="1">Labor Hours</option>
            <option [ngValue]="2">Machine Hours</option>
          </select>
        </div>
        <div class="form-group"><label class="form-label">Tax Number</label><input class="form-control" [(ngModel)]="tenant.taxNumber" /></div>
      </div>
      <button class="btn btn-primary" style="margin-top:14px" (click)="saveTenant()">{{ i18n.t('حفظ الإعدادات', 'Save Settings') }}</button>
    </div>

    <div class="card">
      <div class="card-header">
        <h3 class="card-title">{{ i18n.t('المستخدمون', 'Users') }}</h3>
        <button class="btn btn-primary btn-sm" (click)="openUserModal()">+ {{ i18n.t('دعوة مستخدم', 'Invite User') }}</button>
      </div>
      <div class="table-wrapper">
        <table class="erp-table">
          <thead><tr><th>{{ i18n.t('الاسم', 'Name') }}</th><th>{{ i18n.t('البريد', 'Email') }}</th><th>{{ i18n.t('الأدوار', 'Roles') }}</th><th>{{ i18n.t('الحالة', 'Status') }}</th><th>{{ i18n.t('إجراء', 'Action') }}</th></tr></thead>
          <tbody>
            <tr *ngFor="let u of users()">
              <td>{{ u.fullName }}</td><td>{{ u.email }}</td>
              <td><span *ngFor="let r of u.roles" class="badge badge-info" style="margin-inline-end:4px">{{ r.name }}</span></td>
              <td><span class="badge" [class.badge-success]="u.isActive" [class.badge-danger]="!u.isActive">{{ u.isActive ? 'Active' : 'Disabled' }}</span></td>
              <td><button class="btn btn-secondary btn-sm" (click)="toggleUser(u.id)">{{ u.isActive ? 'Disable' : 'Enable' }}</button></td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>

    <div *ngIf="showUserModal()" class="modal-backdrop">
      <div class="modal-dialog">
        <div class="modal-header"><h3 class="card-title">{{ i18n.t('إضافة موظف للنظام', 'Invite User') }}</h3><button class="btn btn-secondary btn-sm" (click)="showUserModal.set(false)">✕</button></div>
        <div class="modal-body">
          <div class="form-grid">
            <div class="form-group"><label class="form-label">Full Name</label><input class="form-control" [(ngModel)]="userForm.fullName" /></div>
            <div class="form-group"><label class="form-label">Email</label><input class="form-control" [(ngModel)]="userForm.email" /></div>
            <div class="form-group"><label class="form-label">Password</label><input class="form-control" type="password" [(ngModel)]="userForm.password" /></div>
            <div class="form-group"><label class="form-label">Role</label>
              <select class="form-control" [(ngModel)]="userForm.roleId"><option *ngFor="let r of roles()" [value]="r.id">{{ r.name }}</option></select>
            </div>
          </div>
        </div>
        <div class="modal-footer">
          <button class="btn btn-secondary" (click)="showUserModal.set(false)">Cancel</button>
          <button class="btn btn-primary" (click)="createUser()">Create User</button>
        </div>
      </div>
    </div>
  `
})
export class SettingsComponent implements OnInit {
  private api = inject(ApiService);
  i18n = inject(I18nService);

  tenant: any = null;
  users = signal<any[]>([]);
  roles = signal<any[]>([]);
  savedMsg = signal('');
  showUserModal = signal(false);
  userForm: any = { fullName: '', email: '', password: '', roleId: '' };

  ngOnInit() {
    this.api.get<any>('tenantsandusers/tenant').subscribe((t) => this.tenant = t);
    this.api.get<any[]>('tenantsandusers/roles').subscribe((r) => this.roles.set(r));
    this.loadUsers();
  }

  loadUsers() {
    this.api.get<any[]>('tenantsandusers/users').subscribe((u) => this.users.set(u));
  }

  saveTenant() {
    this.api.put('tenantsandusers/tenant', this.tenant).subscribe(() => this.savedMsg.set('Settings saved.'));
  }

  openUserModal() {
    this.userForm = { fullName: '', email: '', password: '', roleId: this.roles()[0]?.id || '' };
    this.showUserModal.set(true);
  }

  createUser() {
    this.api.post('tenantsandusers/users', this.userForm).subscribe(() => { this.showUserModal.set(false); this.loadUsers(); });
  }

  toggleUser(id: string) {
    this.api.patch(`tenantsandusers/users/${id}/toggle-active`).subscribe(() => this.loadUsers());
  }
}

