import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { AuthService } from '../core/services/auth.service';
import { I18nService } from '../core/services/i18n.service';
import { ApiService } from '../core/services/api.service';

interface NavGroup {
  key: string;
  icon: string;
  titleAr: string;
  titleEn: string;
  items: { route: string; icon: string; labelAr: string; labelEn: string }[];
}

@Component({
  selector: 'app-main-layout',
  standalone: true,
  imports: [CommonModule, RouterModule],
  styles: [`
    .nav-group-btn {
      width: 100%;
      display: flex;
      align-items: center;
      justify-content: space-between;
      padding: 10px 12px;
      margin-top: 4px;
      background: rgba(255, 255, 255, 0.03);
      border: 1px solid rgba(255, 255, 255, 0.05);
      border-radius: var(--radius-sm);
      color: var(--text-sidebar);
      font-size: 13px;
      font-weight: 600;
      cursor: pointer;
      transition: background 0.15s ease, border-color 0.15s ease;
    }
    .nav-group-btn:hover {
      background: var(--bg-sidebar-hover);
      border-color: rgba(255, 255, 255, 0.12);
    }
    .nav-group-btn.open {
      background: rgba(37, 99, 235, 0.14);
      border-color: rgba(59, 130, 246, 0.35);
      color: #fff;
    }
    .nav-group-title {
      display: flex;
      align-items: center;
      gap: 9px;
    }
    .nav-arrow {
      font-size: 11px;
      transition: transform 0.2s ease;
      opacity: 0.8;
    }
    .nav-arrow.rotated {
      transform: rotate(180deg);
    }
    .nav-submenu {
      display: flex;
      flex-direction: column;
      gap: 3px;
      padding: 6px 0 6px;
      padding-inline-start: 14px;
      margin-inline-start: 10px;
      border-inline-start: 1px dashed rgba(148, 163, 184, 0.3);
    }
    .nav-subitem {
      display: flex;
      align-items: center;
      gap: 9px;
      padding: 8px 10px;
      border-radius: var(--radius-sm);
      color: #cbd5e1;
      text-decoration: none;
      font-size: 12.5px;
      font-weight: 500;
      transition: background 0.15s ease, color 0.15s ease;
    }
    .nav-subitem:hover {
      background: var(--bg-sidebar-hover);
      color: #fff;
    }
    .nav-subitem.active {
      background: var(--primary);
      color: #fff;
      font-weight: 600;
    }
  `],
  template: `
    <div class="erp-shell">
      <aside class="erp-sidebar" [class.open]="sidebarOpen()">
        <div class="erp-brand">
          <div class="erp-brand-badge">FF</div>
          <span>FactoryFlow ERP</span>
        </div>

        <nav class="erp-nav">
          <a class="erp-nav-item" routerLink="/dashboard" routerLinkActive="active" (click)="sidebarOpen.set(false)">
            <span>📊</span> {{ i18n.t('لوحة التحكم', 'Dashboard') }}
          </a>

          <div *ngFor="let g of navGroups">
            <button
              type="button"
              class="nav-group-btn"
              [class.open]="isOpen(g.key)"
              (click)="toggleGroup(g.key)">
              <span class="nav-group-title">
                <span>{{ g.icon }}</span>
                <span>{{ i18n.t(g.titleAr, g.titleEn) }}</span>
              </span>
              <span class="nav-arrow" [class.rotated]="isOpen(g.key)">▾</span>
            </button>

            <div class="nav-submenu" *ngIf="isOpen(g.key)">
              <a
                *ngFor="let item of g.items"
                class="nav-subitem"
                [routerLink]="item.route"
                routerLinkActive="active"
                (click)="sidebarOpen.set(false)">
                <span>{{ item.icon }}</span>
                <span>{{ i18n.t(item.labelAr, item.labelEn) }}</span>
              </a>
            </div>
          </div>
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
  private router = inject(Router);

  sidebarOpen = signal(false);
  showNotifs = signal(false);
  notifications = signal<any[]>([]);
  openGroups = signal<Record<string, boolean>>({ manufacturing: true });

  readonly navGroups: NavGroup[] = [
    {
      key: 'manufacturing',
      icon: '🏭',
      titleAr: 'الإنتاج والتصنيع',
      titleEn: 'Manufacturing',
      items: [
        { route: '/bom', icon: '📋', labelAr: 'وصفات الإنتاج BOM', labelEn: 'Bill of Materials' },
        { route: '/manufacturing', icon: '⚙️', labelAr: 'أوامر الإنتاج والتكاليف', labelEn: 'Production & Costing' },
        { route: '/resources', icon: '👷', labelAr: 'العمالة والآلات', labelEn: 'Labor & Machines' },
        { route: '/quality', icon: '✅', labelAr: 'فحص الجودة', labelEn: 'Quality Control' }
      ]
    },
    {
      key: 'inventory',
      icon: '📦',
      titleAr: 'المخزون والمواد',
      titleEn: 'Inventory & Stock',
      items: [
        { route: '/raw-materials', icon: '🧱', labelAr: 'المواد الخام', labelEn: 'Raw Materials' },
        { route: '/products', icon: '🎁', labelAr: 'المنتجات النهائية', labelEn: 'Products' },
        { route: '/warehouses', icon: '🏬', labelAr: 'المستودعات والحركة', labelEn: 'Warehouses & Stock' }
      ]
    },
    {
      key: 'commercial',
      icon: '💼',
      titleAr: 'المشتريات والمبيعات',
      titleEn: 'Commercial',
      items: [
        { route: '/purchasing', icon: '🛒', labelAr: 'المشتريات والموردون', labelEn: 'Purchasing & Suppliers' },
        { route: '/sales', icon: '💰', labelAr: 'المبيعات والعملاء', labelEn: 'Sales & Customers' }
      ]
    },
    {
      key: 'finance',
      icon: '📒',
      titleAr: 'المالية والتقارير',
      titleEn: 'Finance & Reports',
      items: [
        { route: '/accounting', icon: '⚖️', labelAr: 'المحاسبة العامة', labelEn: 'General Ledger' },
        { route: '/treasury', icon: '🏦', labelAr: 'الصندوق والبنوك', labelEn: 'Treasury & Banks' },
        { route: '/reports', icon: '📈', labelAr: 'التقارير والتحليلات', labelEn: 'Reports & Analytics' }
      ]
    },
    {
      key: 'admin',
      icon: '🛡️',
      titleAr: 'الإدارة والأمان',
      titleEn: 'Administration',
      items: [
        { route: '/settings', icon: '🏢', labelAr: 'إعدادات المؤسسة والصلاحيات', labelEn: 'Tenant & RBAC' },
        { route: '/audit-logs', icon: '📜', labelAr: 'سجل التدقيق', labelEn: 'Audit Logs' }
      ]
    }
  ];

  ngOnInit() {
    this.expandActiveGroup();
    this.api.get<any[]>('reports/notifications').subscribe({
      next: (data) => this.notifications.set(data || []),
      error: () => {}
    });
  }

  isOpen(key: string): boolean {
    return !!this.openGroups()[key];
  }

  toggleGroup(key: string) {
    this.openGroups.update((curr) => ({ ...curr, [key]: !curr[key] }));
  }

  private expandActiveGroup() {
    const url = this.router.url;
    for (const g of this.navGroups) {
      if (g.items.some((i) => url.startsWith(i.route))) {
        this.openGroups.update((curr) => ({ ...curr, [g.key]: true }));
      }
    }
  }

  unreadCount() {
    return this.notifications().filter((n) => !n.isRead).length;
  }
}
