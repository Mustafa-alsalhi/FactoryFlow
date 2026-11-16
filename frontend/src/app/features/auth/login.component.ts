import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';
import { I18nService } from '../../core/services/i18n.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  template: `
    <div style="min-height:100vh;display:grid;place-items:center;padding:20px;background:var(--bg-app)">
      <div class="card" style="width:100%;max-width:460px">
        <div style="display:flex;justify-content:space-between;align-items:center;margin-bottom:18px">
          <div style="display:flex;align-items:center;gap:10px">
            <div class="erp-brand-badge">FF</div>
            <div>
              <h2 style="margin:0;font-size:18px">FactoryFlow ERP</h2>
              <div style="font-size:12px;color:var(--text-muted)">{{ i18n.t('نظام إدارة المصانع والتكاليف الصناعية', 'Manufacturing & Cost Accounting ERP') }}</div>
            </div>
          </div>
          <button class="btn btn-secondary btn-sm" (click)="i18n.toggleLang()">{{ i18n.lang() === 'ar' ? 'EN' : 'عربي' }}</button>
        </div>

        <div style="display:flex;gap:8px;margin-bottom:16px">
          <button class="btn" [class.btn-primary]="mode() === 'login'" [class.btn-secondary]="mode() !== 'login'" style="flex:1" (click)="mode.set('login')">
            {{ i18n.t('تسجيل الدخول', 'Sign In') }}
          </button>
          <button class="btn" [class.btn-primary]="mode() === 'register'" [class.btn-secondary]="mode() !== 'register'" style="flex:1" (click)="mode.set('register')">
            {{ i18n.t('تسجيل مؤسسة جديدة', 'Register Company') }}
          </button>
        </div>

        <div *ngIf="error()" class="alert alert-error">{{ error() }}</div>
        <div *ngIf="success()" class="alert alert-success">{{ success() }}</div>

        <form *ngIf="mode() === 'login'" [formGroup]="loginForm" (ngSubmit)="submitLogin()">
          <div class="form-group" style="margin-bottom:12px">
            <label class="form-label">{{ i18n.t('رمز المؤسسة', 'Company Code') }}</label>
            <input class="form-control" formControlName="tenantCode" placeholder="DEMO" />
          </div>
          <div class="form-group" style="margin-bottom:12px">
            <label class="form-label">{{ i18n.t('البريد الإلكتروني', 'Email') }}</label>
            <input class="form-control" type="email" formControlName="email" />
          </div>
          <div class="form-group" style="margin-bottom:16px">
            <label class="form-label">{{ i18n.t('كلمة المرور', 'Password') }}</label>
            <input class="form-control" type="password" formControlName="password" />
          </div>
          <button class="btn btn-primary" type="submit" style="width:100%" [disabled]="loading()">
            {{ loading() ? i18n.t('جاري الدخول...', 'Signing in...') : i18n.t('دخول', 'Sign In') }}
          </button>
          <button class="btn btn-secondary" type="button" style="width:100%;margin-top:8px" (click)="fillDemo()">
            {{ i18n.t('تعبئة بيانات الحساب التجريبي (DEMO)', 'Fill Demo Account (DEMO)') }}
          </button>
        </form>

        <form *ngIf="mode() === 'register'" [formGroup]="regForm" (ngSubmit)="submitRegister()">
          <div class="form-grid" style="margin-bottom:12px">
            <div class="form-group">
              <label class="form-label">{{ i18n.t('رمز المؤسسة', 'Company Code') }}</label>
              <input class="form-control" formControlName="companyCode" placeholder="ACME" />
            </div>
            <div class="form-group">
              <label class="form-label">{{ i18n.t('العملة الأساسية', 'Base Currency') }}</label>
              <input class="form-control" formControlName="baseCurrencyCode" placeholder="USD" />
            </div>
          </div>
          <div class="form-group" style="margin-bottom:12px">
            <label class="form-label">{{ i18n.t('اسم المؤسسة (إنجليزي)', 'Company Name (EN)') }}</label>
            <input class="form-control" formControlName="companyName" />
          </div>
          <div class="form-group" style="margin-bottom:12px">
            <label class="form-label">{{ i18n.t('اسم المؤسسة (عربي)', 'Company Name (AR)') }}</label>
            <input class="form-control" formControlName="companyNameAr" />
          </div>
          <div class="form-group" style="margin-bottom:12px">
            <label class="form-label">{{ i18n.t('اسم المالك', 'Owner Full Name') }}</label>
            <input class="form-control" formControlName="ownerFullName" />
          </div>
          <div class="form-group" style="margin-bottom:12px">
            <label class="form-label">{{ i18n.t('البريد الإلكتروني', 'Email') }}</label>
            <input class="form-control" type="email" formControlName="email" />
          </div>
          <div class="form-group" style="margin-bottom:16px">
            <label class="form-label">{{ i18n.t('كلمة المرور', 'Password') }}</label>
            <input class="form-control" type="password" formControlName="password" />
          </div>
          <button class="btn btn-primary" type="submit" style="width:100%" [disabled]="loading()">
            {{ i18n.t('إنشاء حساب المؤسسة', 'Create Company Account') }}
          </button>
        </form>
      </div>
    </div>
  `
})
export class LoginComponent {
  private fb = inject(FormBuilder);
  private auth = inject(AuthService);
  private router = inject(Router);
  i18n = inject(I18nService);

  mode = signal<'login' | 'register'>('login');
  loading = signal(false);
  error = signal('');
  success = signal('');

  loginForm = this.fb.group({
    tenantCode: ['DEMO'],
    email: ['', [Validators.required, Validators.email]],
    password: ['', Validators.required]
  });

  regForm = this.fb.group({
    companyCode: ['', Validators.required],
    companyName: ['', Validators.required],
    companyNameAr: [''],
    baseCurrencyCode: ['USD', Validators.required],
    ownerFullName: ['', Validators.required],
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required, Validators.minLength(6)]]
  });

  fillDemo() {
    this.loginForm.patchValue({
      tenantCode: 'DEMO',
      email: 'admin@factoryflow.demo',
      password: 'Admin@123456'
    });
  }

  submitLogin() {
    if (this.loginForm.invalid) return;
    this.loading.set(true);
    this.error.set('');
    this.auth.login(this.loginForm.value as any).subscribe({
      next: () => {
        this.loading.set(false);
        this.router.navigate(['/dashboard']);
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(err?.error?.message || this.i18n.t('فشل تسجيل الدخول', 'Login failed'));
      }
    });
  }

  submitRegister() {
    if (this.regForm.invalid) return;
    this.loading.set(true);
    this.error.set('');
    this.auth.registerTenant(this.regForm.value).subscribe({
      next: () => {
        this.loading.set(false);
        this.success.set(this.i18n.t('تم إنشاء المؤسسة بنجاح! يمكنك تسجيل الدخول الآن.', 'Company created! You can sign in now.'));
        this.mode.set('login');
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(err?.error?.message || this.i18n.t('فشل التسجيل', 'Registration failed'));
      }
    });
  }
}

