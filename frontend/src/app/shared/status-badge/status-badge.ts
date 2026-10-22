import { Component, computed, input } from '@angular/core';
import { LeaveStatus } from '../../core/api.models';

const LABELS: Record<LeaveStatus, string> = {
  Pending: 'En attente',
  Approved: 'Approuvée',
  Rejected: 'Refusée',
};

/**
 * Pastille de statut d'une demande. Couleurs : tokens --color-status-*.
 *
 * <app-status-badge [status]="request.status" />
 */
@Component({
  selector: 'app-status-badge',
  template: `<span class="badge" [class]="'badge badge--' + status().toLowerCase()" data-testid="status-badge">{{ label() }}</span>`,
  styleUrl: './status-badge.css',
})
export class StatusBadge {
  readonly status = input.required<LeaveStatus>();
  protected readonly label = computed(() => LABELS[this.status()]);
}
