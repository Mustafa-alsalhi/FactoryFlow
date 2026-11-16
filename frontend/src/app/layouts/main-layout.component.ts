import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { AuthService } from '../core/services/auth.service';
import { I18nService } from '../core/services/i18n.service';
import { ApiService } from '../core/services/api.service';

@Component({
  selector: 'app-main-layout',
  standalone: true,
  imports: [CommonModule, RouterModule],
  template: `
    <div class="erp-shell">
      <aside class="erp-sidebar" [class.open]="sidebarOpen()">
        <div class="erp-brand">
          <div class="erp-brand-badge">FF</div>
          <span>FactoryFlow ERP</span>
        </div>
        <nav class="erp-nav">
          <div class="erp-nav-group">{{ i18n.t('الرئيسية', 'OVERVIEW') }}</div>
          <a class="erp-nav-item" routerLink="/dashboard" routerLinkActive="active" (click)="sidebarOpen.set(false)">
            <span>📊</span> {{ i18n.t('لوحة التحكم', 'Dashboard') }}
          </a>

          <div class="erp-nav-group">{{ i18n.t('الإنتاج والمخزون', 'MANUFACTURING & STOCK') }}</div>
          <a class="erp-nav-item" routerLink="/raw-materials" routerLinkActive="active" (click)="sidebarOpen.set(false)">
            <span>🧱</span> {{ i18n.t('المواد الخام', 'Raw Materials') }}
          </a>
          <a class="erp-nav-item" routerLink="/products" routerLinkActive="active" (click)="sidebarOpen.set(false)">
            <span>📦</span> {{ i18n.t('المنتجات النهائية', 'Products') }}
          </a>
          <a class="erp-nav-item" routerLink="/bom" routerLinkActive="active" (click)="sidebarOpen.set(false)">
            <span>📋</span> {{ i18n.t('وصفات الإنتاج BOM', 'Bill of Materials') }}
          </a>
          <a class="erp-nav-item" routerLink="/manufacturing" routerLinkActive="active" (click)="sidebarOpen.set(false)">
            <span>🏭</span> {{ i18n.t('أوامر الإنتاج والتكاليف', 'Production & Costing') }}
          </a>
          <a class="erp-nav-item" routerLink="/resources" routerLinkActive="active" (click)="sidebarOpen.set(false)">
            <span>⚙️</span> {{ i18n.t('العمالة والآلات', 'Labor & Machines') }}
          </a>
          <a class="erp-nav-item" routerLink="/quality" routerLinkActive="active" (click)="sidebarOpen.set(false)">
            <span>✅</span> {{ i18n.t('فحص الجودة', 'Quality Control') }}
          </a>
          <a class="erp-nav-item" routerLink="/warehouses" routerLinkActive="active" (click)="sidebarOpen.set(false)">
            <span>🏬</span> {{ i18n.t('المستودعات والمخزون', 'Warehouses & Stock') }}
          </a>

          <div class="erp-nav-group">{{ i18n.t('المبيعات والمشتريات', 'COMMERCIAL') }}</div>
          <a class="erp-nav-item" routerLink="/purchasing" routerLinkActive="active" (click)="sidebarOpen.set(false)">
            <span>🛒</span> {{ i18n.t('المشتريات والموردون', 'Purchasing & Suppliers') }}
          </a>
          <a class="erp-nav-item" routerLink="/sales" routerLinkActive="active" (click)="sidebarOpen.set(false)">
            <span>💰</span> {{ i18n.t('المبيعات والعملاء', 'Sales & Customers') }}
          </a>

          <div class="erp-nav-group">{{ i18n.t('المالية والتقارير', 'FINANCE & REPORTS') }}</div>
          <a class="erp-nav-item" routerLink="/accounting" routerLinkActive="active" (click)="sidebarOpen.set(false)">
            <span>📒</span> {{ i18n.t('المحاسبة العامة', 'General Ledger') }}
          </a>
          <a class="erp-nav-item" routerLink="/treasury" routerLinkActive="active" (click)="sidebarOpen.set(false)">
            <span>🏦</span> {{ i18n.t('الصندوق والبنوك', 'Treasury & Banks') }}
          </a>
          <a class="erp-nav-item" routerLink="/reports" routerLinkActive="active" (click)="sidebarOpen.set(false)">
            <span>📈</span> {{ i18n.t('التقارير والتحليلات', 'Reports & Analytics') }}
          </a>

          <div class="erp-nav-group">{{ i18n.t('الإدارة', 'ADMINISTRATION') }}</div>
          <a class="erp-nav-item" routerLink="/settings" routerLinkActive="active" (click)="sidebarOpen.set(false)">
            <span>🏢</span> {{ i18n.t('إعدادات المؤسسة والصلاحيات', 'Tenant & RBAC') }}
          </a>
          <a class="erp-nav-item" routerLink="/audit-logs" routerLinkActive="active" (click)="sidebarOpen.set(false)">
            <span>🛡️</span> {{ i18n.t('سجل التدقيق', 'Audit Logs') }}
          </a>
        </nav>
      </aside>

      <div class="erp-main">
        <header class="erp-topbar">
          <div class="erp-topbar-left">
            <button class="btn btn-secondary btn-sm mobile-toggle" (click)="sidebarOpen.set(!sidebarOpen())">☰</button>
            <div>
              <strong>{{ i18n.lang() === 'ar' ? (auth.tenant()?.nameAr || auth.tenant()?.name) : auth.tenant()?.name }}</strong>
              <span class="badge badge-info" style="margin-inline-start:8px">{{ auth.tenant()?.code }}</span>
            </div>
          </div>
          <div class="erp-topbar-right">
            <button class="btn btn-secondary btn-sm" (click)="showNotifs.set(!showNotifs())">
              🔔 {{ unreadCount() }}
            </button>
            <button class="btn btn-secondary btn-sm" (click)="i18n.toggleLang()">
              {{ i18n.lang() === 'ar' ? 'EN' : 'عربي' }}
            </button>
            <button class="btn btn-secondary btn-sm" (click)="i18n.toggleTheme()">
              {{ i18n.theme() === 'light' ? '🌙' : '☀️' }}
            </button>
            <span style="font-size:13px;color:var(--text-secondary)">{{ auth.user()?.fullName }}</span>
            <button class="btn btn-danger btn-sm" (click)="auth.logout()">
              {{ i18n.t('خروج', 'Logout') }}
            </button>
          </div>
        </header>

        <div *ngIf="showNotifs()" class="card" style="position:fixed;top:70px;inset-inline-end:24px;width:340px;z-index:50;max-height:400px;overflow-y:auto">
          <div class="card-header">
            <h4 class="card-title">{{ i18n.t('الإشعارات', 'Notifications') }}</h4>
            <button class="btn btn-secondary btn-sm" (click)="showNotifs.set(false)">✕</button>
          </div>
          <div *ngIf="notifications().length === 0" class="empty-state">{{ i18n.t('لا توجد إشعارات', 'No notifications') }}</div>
          <div *ngFor="let n of notifications()" style="padding:8px 0;border-bottom:1px solid var(--border-color)">
            <div style="font-weight:600;font-size:13px">{{ i18n.lang() === 'ar' ? n.titleAr : n.title }}</div>
            <div style="font-size:12px;color:var(--text-muted)">{{ i18n.lang() === 'ar' ? n.messageAr : n.message }}</div>
          </div>
        </div>

        <main class="erp-content">
          <router-outlet></router-outlet>
        </main>
      </div>
    </div>
  `
})
export class MainLayoutComponent implements OnInit {
  auth = inject(AuthService);
  i18n = inject(I18nService);
  private api = inject(ApiService);

  sidebarOpen = signal(false);
  showNotifs = signal(false);
  notifications = signal<any[]>([]);

  ngOnInit() {
    this.api.get<any[]>('reports/notifications').subscribe({
      next: (data) => this.notifications.set(data || []),
      error: () => {}
    });
  }

  unreadCount() {
    return this.notifications().filter((n) => !n.isRead).length;
  }
}

