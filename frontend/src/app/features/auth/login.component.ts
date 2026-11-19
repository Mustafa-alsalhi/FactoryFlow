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
  styles: [`
    .auth-wrapper {
      min-height: 100vh;
      display: grid;
      place-items: center;
      padding: 24px 16px;
      background:
        radial-gradient(circle at top right, rgba(37, 99, 235, 0.08), transparent 45%),
        radial-gradient(circle at bottom left, rgba(16, 185, 129, 0.06), transparent 45%),
        var(--bg-app);
    }
    .auth-card {
      width: 100%;
      max-width: 460px;
      background: var(--bg-surface);
      border: 1px solid var(--border-color);
      border-radius: 16px;
      padding: 30px;
      box-shadow: var(--shadow-md);
    }
    .auth-topbar {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 22px;
      padding-bottom: 16px;
      border-bottom: 1px solid var(--border-color);
    }
    .brand-row {
      display: flex;
      align-items: center;
      gap: 10px;
    }
    .brand-logo {
      width: 38px;
      height: 38px;
      border-radius: 10px;
      background: linear-gradient(135deg, var(--primary), #1d4ed8);
      color: #fff;
      display: grid;
      place-items: center;
      font-weight: 800;
      font-size: 15px;
    }
    .auth-heading {
      margin: 0 0 4px;
      font-size: 20px;
      font-weight: 700;
    }
    .auth-sub {
      margin: 0 0 18px;
      font-size: 13px;
      color: var(--text-muted);
    }
    .segmented {
      display: grid;
      grid-template-columns: 1fr 1fr;
      background: var(--bg-surface-alt);
      border: 1px solid var(--border-color);
      border-radius: 10px;
      padding: 4px;
      gap: 4px;
      margin-bottom: 20px;
    }
    .segmented button {
      border: none;
      background: transparent;
      color: var(--text-secondary);
      padding: 9px 12px;
      border-radius: 7px;
      font-size: 13px;
      font-weight: 600;
      cursor: pointer;
      transition: all 0.15s ease;
    }
    .segmented button.active {
      background: var(--bg-surface);
      color: var(--primary);
      box-shadow: var(--shadow-sm);
    }
    .password-wrap {
      position: relative;
      display: flex;
      align-items: center;
    }
    .password-wrap .form-control {
      width: 100%;
      padding-inline-end: 42px;
    }
    .password-toggle {
      position: absolute;
      inset-inline-end: 8px;
      background: transparent;
      border: none;
      color: var(--text-muted);
      cursor: pointer;
      padding: 4px 6px;
      font-size: 14px;
    }
    .submit-btn {
      width: 100%;
      padding: 11px 16px;
      font-size: 14px;
      border-radius: 8px;
      margin-top: 4px;
    }
    .field-error {
      font-size: 11px;
      color: var(--danger);
      margin-top: 2px;
    }
    @media (max-width: 520px) {
      .auth-card {
        padding: 22px 18px;
      }
    }
  `],
  template: `
    <div class="auth-wrapper">
      <div class="auth-card">
        <div class="auth-topbar">
          <div class="brand-row">
            <div class="brand-logo">FF</div>
            <div>
              <div style="font-weight:700;font-size:16px">FactoryFlow ERP</div>
              <div style="font-size:11px;color:var(--text-muted)">{{ i18n.t('إدارة المصانع والتكاليف الصناعية', 'Manufacturing & Cost Accounting') }}</div>
            </div>
          </div>
          <div style="display:flex;gap:6px">
            <button class="btn btn-secondary btn-sm" type="button" (click)="i18n.toggleTheme()">
              {{ i18n.theme() === 'light' ? '🌙' : '☀️' }}
            </button>
            <button class="btn btn-secondary btn-sm" type="button" (click)="i18n.toggleLang()">
              {{ i18n.lang() === 'ar' ? 'EN' : 'عربي' }}
            </button>
          </div>
        </div>

        <h2 class="auth-heading">
          {{ mode() === 'login' ? i18n.t('تسجيل الدخول', 'Sign In') : i18n.t('تسجيل مؤسسة جديدة', 'Register Company') }}
        </h2>
        <p class="auth-sub">
          {{ mode() === 'login'
            ? i18n.t('أدخل رمز المؤسسة وبيانات حسابك للمتابعة', 'Enter your company code and credentials to continue')
            : i18n.t('أنشئ بيئة عمل مستقلة لمصنعك مع دليل حسابات جاهز', 'Create an isolated factory workspace with a ready Chart of Accounts') }}
        </p>

        <div class="segmented">
          <button type="button" [class.active]="mode() === 'login'" (click)="switchMode('login')">
            {{ i18n.t('تسجيل الدخول', 'Sign In') }}
          </button>
          <button type="button" [class.active]="mode() === 'register'" (click)="switchMode('register')">
            {{ i18n.t('مؤسسة جديدة', 'New Company') }}
          </button>
        </div>

        <div *ngIf="error()" class="alert alert-error">{{ error() }}</div>
        <div *ngIf="success()" class="alert alert-success">{{ success() }}</div>

        <form *ngIf="mode() === 'login'" [formGroup]="loginForm" (ngSubmit)="submitLogin()">
          <div class="form-group" style="margin-bottom:14px">
            <label class="form-label">{{ i18n.t('رمز المؤسسة', 'Company Code') }}</label>
            <input class="form-control" formControlName="tenantCode" [placeholder]="i18n.t('مثال: DEMO', 'e.g. DEMO')" />
          </div>
          <div class="form-group" style="margin-bottom:14px">
            <label class="form-label">{{ i18n.t('البريد الإلكتروني', 'Email Address') }}</label>
            <input class="form-control" type="email" formControlName="email" placeholder="name@company.com" />
            <span *ngIf="loginForm.get('email')?.touched && loginForm.get('email')?.invalid" class="field-error">
              {{ i18n.t('يرجى إدخال بريد إلكتروني صحيح', 'Please enter a valid email address') }}
            </span>
          </div>
          <div class="form-group" style="margin-bottom:18px">
            <label class="form-label">{{ i18n.t('كلمة المرور', 'Password') }}</label>
            <div class="password-wrap">
              <input class="form-control" [type]="showPassword() ? 'text' : 'password'" formControlName="password" placeholder="••••••••" />
              <button type="button" class="password-toggle" (click)="showPassword.set(!showPassword())" [title]="i18n.t('إظهار/إخفاء', 'Show/Hide')">
                {{ showPassword() ? '🙈' : '👁️' }}
              </button>
            </div>
          </div>
          <button class="btn btn-primary submit-btn" type="submit" [disabled]="loading() || loginForm.invalid">
            {{ loading() ? i18n.t('جاري تسجيل الدخول...', 'Signing in...') : i18n.t('دخول إلى النظام', 'Sign In') }}
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
          <div class="form-grid" style="margin-bottom:12px">
            <div class="form-group">
              <label class="form-label">{{ i18n.t('اسم المؤسسة (إنجليزي)', 'Company Name (EN)') }}</label>
              <input class="form-control" formControlName="companyName" />
            </div>
            <div class="form-group">
              <label class="form-label">{{ i18n.t('اسم المؤسسة (عربي)', 'Company Name (AR)') }}</label>
              <input class="form-control" formControlName="companyNameAr" />
            </div>
          </div>
          <div class="form-group" style="margin-bottom:12px">
            <label class="form-label">{{ i18n.t('اسم مالك المؤسسة', 'Owner Full Name') }}</label>
            <input class="form-control" formControlName="ownerFullName" />
          </div>
          <div class="form-group" style="margin-bottom:12px">
            <label class="form-label">{{ i18n.t('البريد الإلكتروني الرسمي', 'Work Email') }}</label>
            <input class="form-control" type="email" formControlName="email" placeholder="owner@company.com" />
          </div>
          <div class="form-group" style="margin-bottom:18px">
            <label class="form-label">{{ i18n.t('كلمة المرور (6 أحرف فأكثر)', 'Password (min 6 chars)') }}</label>
            <div class="password-wrap">
              <input class="form-control" [type]="showPassword() ? 'text' : 'password'" formControlName="password" placeholder="••••••••" />
              <button type="button" class="password-toggle" (click)="showPassword.set(!showPassword())">
                {{ showPassword() ? '🙈' : '👁️' }}
              </button>
            </div>
          </div>
          <button class="btn btn-primary submit-btn" type="submit" [disabled]="loading() || regForm.invalid">
            {{ loading() ? i18n.t('جاري إنشاء المؤسسة...', 'Creating workspace...') : i18n.t('إنشاء حساب المؤسسة', 'Create Company Account') }}
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
  showPassword = signal(false);
  error = signal('');
  success = signal('');

  loginForm = this.fb.group({
    tenantCode: ['', Validators.required],
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

  switchMode(next: 'login' | 'register') {
    this.mode.set(next);
    this.error.set('');
    this.success.set('');
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
        this.error.set(err?.error?.message || this.i18n.t('فشل تسجيل الدخول، تحقق من البيانات المدخلة', 'Invalid credentials or company code'));
      }
    });
  }

  submitRegister() {
    if (this.regForm.invalid) return;
    this.loading.set(true);
    this.error.set('');
    this.auth.registerTenant(this.regForm.value).subscribe({
      next: (res) => {
        this.loading.set(false);
        this.success.set(this.i18n.t('تم إنشاء المؤسسة بنجاح! يمكنك تسجيل الدخول الآن.', 'Company workspace created! You can sign in now.'));
        this.loginForm.patchValue({
          tenantCode: res?.tenantCode || this.regForm.value.companyCode,
          email: this.regForm.value.email || ''
        });
        this.mode.set('login');
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(err?.error?.message || this.i18n.t('تعذر إنشاء المؤسسة', 'Registration failed'));
      }
    });
  }
}
