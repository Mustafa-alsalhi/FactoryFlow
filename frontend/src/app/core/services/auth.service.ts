import { Injectable, signal, computed, inject } from '@angular/core';
import { Router } from '@angular/router';
import { tap } from 'rxjs';
import { ApiService } from './api.service';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private api = inject(ApiService);
  private router = inject(Router);

  token = signal<string | null>(localStorage.getItem('ff_access_token'));
  user = signal<any>(this.readJson('ff_user'));
  tenant = signal<any>(this.readJson('ff_tenant'));
  isAuthenticated = computed(() => !!this.token());

  getAccessToken(): string | null {
    return this.token();
  }

  getRefreshToken(): string | null {
    return localStorage.getItem('ff_refresh_token');
  }

  login(payload: { tenantCode?: string; email: string; password: string }) {
    return this.api.post<any>('auth/login', payload).pipe(
      tap((res) => {
        localStorage.setItem('ff_access_token', res.accessToken);
        localStorage.setItem('ff_refresh_token', res.refreshToken);
        localStorage.setItem('ff_user', JSON.stringify(res.user));
        localStorage.setItem('ff_tenant', JSON.stringify(res.tenant));
        this.token.set(res.accessToken);
        this.user.set(res.user);
        this.tenant.set(res.tenant);
      })
    );
  }

  registerTenant(payload: any) {
    return this.api.post<any>('auth/register-tenant', payload);
  }

  refreshToken() {
    const refreshToken = this.getRefreshToken();
    return this.api.post<any>('auth/refresh', { refreshToken }).pipe(
      tap((res) => {
        localStorage.setItem('ff_access_token', res.accessToken);
        localStorage.setItem('ff_refresh_token', res.refreshToken);
        this.token.set(res.accessToken);
      })
    );
  }

  logout() {
    const refreshToken = this.getRefreshToken();
    if (refreshToken) {
      this.api.post('auth/logout', { refreshToken }).subscribe({ error: () => {} });
    }
    localStorage.removeItem('ff_access_token');
    localStorage.removeItem('ff_refresh_token');
    localStorage.removeItem('ff_user');
    localStorage.removeItem('ff_tenant');
    this.token.set(null);
    this.user.set(null);
    this.tenant.set(null);
    this.router.navigate(['/login']);
  }

  private readJson(key: string): any {
    try {
      const v = localStorage.getItem(key);
      return v ? JSON.parse(v) : null;
    } catch {
      return null;
    }
  }
}
