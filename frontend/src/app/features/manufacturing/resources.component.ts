import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../core/services/api.service';
import { I18nService } from '../../core/services/i18n.service';

@Component({
  selector: 'app-resources',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="page-header">
      <div>
        <h1 class="page-title">{{ i18n.t('العمالة والآلات وخطوط الإنتاج', 'Labor, Machines & Production Lines') }}</h1>
        <p class="page-subtitle">{{ i18n.t('الأجور بالساعة واليومي والشهري، وصيانة وتشغيل الآلات', 'Hourly/Daily/Monthly wages, machine run hours & maintenance') }}</p>
      </div>
    </div>

    <div class="grid-2">
      <div class="card">
        <div class="card-header">
          <h3 class="card-title">{{ i18n.t('العمال والمشرفون', 'Employees') }}</h3>
          <button class="btn btn-primary btn-sm" (click)="showEmp.set(true)">+ {{ i18n.t('إضافة عامل', 'Add') }}</button>
        </div>
        <div class="table-wrapper">
          <table class="erp-table">
            <thead>
              <tr>
                <th>{{ i18n.t('الكود', 'Code') }}</th>
                <th>{{ i18n.t('الاسم', 'Name') }}</th>
                <th>{{ i18n.t('الوظيفة', 'Job') }}</th>
                <th>{{ i18n.t('نظام الأجر', 'Wage Type') }}</th>
                <th>{{ i18n.t('الأجر', 'Rate') }}</th>
              </tr>
            </thead>
            <tbody>
              <tr *ngFor="let e of employees()">
                <td>{{ e.code }}</td>
                <td>{{ i18n.lang() === 'ar' ? (e.fullNameAr || e.fullName) : e.fullName }}</td>
                <td>{{ e.jobTitle }}</td>
                <td>{{ e.wageType === 1 ? 'Hourly' : e.wageType === 2 ? 'Daily' : 'Monthly' }}</td>
                <td>{{ e.baseWageRate | number:'1.2-2' }}</td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>

      <div class="card">
        <div class="card-header">
          <h3 class="card-title">{{ i18n.t('الآلات والمعدات', 'Machines & Equipment') }}</h3>
          <button class="btn btn-primary btn-sm" (click)="showMachine.set(true)">+ {{ i18n.t('إضافة آلة', 'Add') }}</button>
        </div>
        <div class="table-wrapper">
          <table class="erp-table">
            <thead>
              <tr>
                <th>{{ i18n.t('الكود', 'Code') }}</th>
                <th>{{ i18n.t('الآلة', 'Machine') }}</th>
                <th>{{ i18n.t('تكلفة الساعة', 'Hourly Cost') }}</th>
                <th>{{ i18n.t('ساعات التشغيل', 'Run Hrs') }}</th>
                <th>{{ i18n.t('ساعات التوقف', 'Downtime') }}</th>
              </tr>
            </thead>
            <tbody>
              <tr *ngFor="let m of machines()">
                <td>{{ m.code }}</td>
                <td>{{ m.name }}</td>
                <td>{{ m.hourlyOperatingCost | number:'1.2-2' }}</td>
                <td>{{ m.totalRunHours }}</td>
                <td>{{ m.totalDowntimeHours }}</td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </div>

    <div *ngIf="showEmp()" class="modal-backdrop">
      <div class="modal-dialog">
        <div class="modal-header"><h3 class="card-title">{{ i18n.t('إضافة عامل', 'Add Employee') }}</h3><button class="btn btn-secondary btn-sm" (click)="showEmp.set(false)">✕</button></div>
        <div class="modal-body">
          <div class="form-grid">
            <div class="form-group"><label class="form-label">Code</label><input class="form-control" [(ngModel)]="empForm.code" /></div>
            <div class="form-group"><label class="form-label">Full Name</label><input class="form-control" [(ngModel)]="empForm.fullName" /></div>
            <div class="form-group"><label class="form-label">Job Title</label><input class="form-control" [(ngModel)]="empForm.jobTitle" /></div>
            <div class="form-group"><label class="form-label">Wage Type</label>
              <select class="form-control" [(ngModel)]="empForm.wageType">
                <option [ngValue]="1">Hourly</option><option [ngValue]="2">Daily</option><option [ngValue]="3">Monthly</option>
              </select>
            </div>
            <div class="form-group"><label class="form-label">Base Rate</label><input class="form-control" type="number" [(ngModel)]="empForm.baseWageRate" /></div>
          </div>
        </div>
        <div class="modal-footer">
          <button class="btn btn-secondary" (click)="showEmp.set(false)">Cancel</button>
          <button class="btn btn-primary" (click)="saveEmp()">Save</button>
        </div>
      </div>
    </div>

    <div *ngIf="showMachine()" class="modal-backdrop">
      <div class="modal-dialog">
        <div class="modal-header"><h3 class="card-title">{{ i18n.t('إضافة آلة', 'Add Machine') }}</h3><button class="btn btn-secondary btn-sm" (click)="showMachine.set(false)">✕</button></div>
        <div class="modal-body">
          <div class="form-grid">
            <div class="form-group"><label class="form-label">Code</label><input class="form-control" [(ngModel)]="mForm.code" /></div>
            <div class="form-group"><label class="form-label">Name</label><input class="form-control" [(ngModel)]="mForm.name" /></div>
            <div class="form-group"><label class="form-label">Production Line</label>
              <select class="form-control" [(ngModel)]="mForm.productionLineId">
                <option *ngFor="let l of lines()" [value]="l.id">{{ l.name }}</option>
              </select>
            </div>
            <div class="form-group"><label class="form-label">Hourly Cost</label><input class="form-control" type="number" [(ngModel)]="mForm.hourlyOperatingCost" /></div>
          </div>
        </div>
        <div class="modal-footer">
          <button class="btn btn-secondary" (click)="showMachine.set(false)">Cancel</button>
          <button class="btn btn-primary" (click)="saveMachine()">Save</button>
        </div>
      </div>
    </div>
  `
})
export class ResourcesComponent implements OnInit {
  private api = inject(ApiService);
  i18n = inject(I18nService);

  employees = signal<any[]>([]);
  machines = signal<any[]>([]);
  lines = signal<any[]>([]);
  branches = signal<any[]>([]);
  showEmp = signal(false);
  showMachine = signal(false);
  empForm: any = { code: '', fullName: '', jobTitle: 'Operator', wageType: 1, baseWageRate: 15 };
  mForm: any = { code: '', name: '', productionLineId: '', hourlyOperatingCost: 10 };

  ngOnInit() {
    this.load();
    this.api.get<any[]>('tenantsandusers/branches').subscribe((b) => { this.branches.set(b); this.empForm.branchId = b[0]?.id; });
    this.api.get<any[]>('manufacturing/lines').subscribe((l) => { this.lines.set(l); this.mForm.productionLineId = l[0]?.id; });
  }

  load() {
    this.api.get<any[]>('manufacturing/employees').subscribe((d) => this.employees.set(d));
    this.api.get<any[]>('manufacturing/machines').subscribe((d) => this.machines.set(d));
  }

  saveEmp() {
    this.api.post('manufacturing/employees', this.empForm).subscribe(() => { this.showEmp.set(false); this.load(); });
  }

  saveMachine() {
    this.api.post('manufacturing/machines', this.mForm).subscribe(() => { this.showMachine.set(false); this.load(); });
  }
}

