import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-status-badge',
  standalone: true,
  imports: [CommonModule],
  template: `<span class="badge" [ngClass]="badgeClass()">{{ label }}</span>`
})
export class StatusBadgeComponent {
  @Input() label = '';
  @Input() variant: 'success' | 'warning' | 'danger' | 'info' | 'neutral' = 'neutral';

  badgeClass() {
    return `badge-${this.variant}`;
  }
}

