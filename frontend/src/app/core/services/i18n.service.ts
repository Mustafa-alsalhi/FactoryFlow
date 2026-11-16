import { Injectable, signal } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class I18nService {
  lang = signal<'ar' | 'en'>((localStorage.getItem('ff_lang') as 'ar' | 'en') || 'ar');
  theme = signal<'light' | 'dark'>((localStorage.getItem('ff_theme') as 'light' | 'dark') || 'light');

  constructor() {
    this.applyDirection();
    this.applyTheme();
  }

  toggleLang() {
    const next = this.lang() === 'ar' ? 'en' : 'ar';
    this.lang.set(next);
    localStorage.setItem('ff_lang', next);
    this.applyDirection();
  }

  toggleTheme() {
    const next = this.theme() === 'light' ? 'dark' : 'light';
    this.theme.set(next);
    localStorage.setItem('ff_theme', next);
    this.applyTheme();
  }

  t(ar: string, en: string): string {
    return this.lang() === 'ar' ? ar : en;
  }

  private applyDirection() {
    const dir = this.lang() === 'ar' ? 'rtl' : 'ltr';
    document.documentElement.setAttribute('dir', dir);
    document.documentElement.setAttribute('lang', this.lang());
  }

  private applyTheme() {
    document.documentElement.setAttribute('data-theme', this.theme());
  }
}

