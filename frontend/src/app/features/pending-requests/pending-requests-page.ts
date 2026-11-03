import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, effect, inject, signal, untracked } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { Observable, finalize } from 'rxjs';
import { errorMessage } from '../../core/api-errors';
import { LEAVE_TYPE_LABELS, LeaveRequest, LeaveType } from '../../core/api.models';
import { CurrentUserService } from '../../core/current-user.service';
import { LeaveRequestService } from '../../core/leave-request.service';
import { ConfirmDialog } from '../../shared/confirm-dialog/confirm-dialog';
import { DataTable, DataTableCell, DataTableColumn } from '../../shared/data-table/data-table';
import { StatusBadge } from '../../shared/status-badge/status-badge';
import { ToastService } from '../../shared/toast/toast.service';
import { EMPTY_FILTERS, PendingFilters, filterPendingRequests } from './pending-filters';
import { PENDING_MESSAGES, rejectDialogMessage } from './pending-messages';

/** RG-13 : longueur minimale du motif de refus. */
const REJECTION_REASON_MIN_LENGTH = 10;

/**
 * Écran manager « Demandes en attente » (wiki : maquette, Interactions, Messages, Conventions data-testid).
 * Approbation en un clic ; refus via app-confirm-dialog avec motif obligatoire.
 */
@Component({
  selector: 'app-pending-requests-page',
  imports: [DataTable, DataTableCell, StatusBadge, ConfirmDialog, DatePipe],
  templateUrl: './pending-requests-page.html',
  styleUrl: './pending-requests-page.css',
})
export class PendingRequestsPage {
  private readonly leaveRequests = inject(LeaveRequestService);
  private readonly currentUser = inject(CurrentUserService);
  private readonly toast = inject(ToastService);

  protected readonly messages = PENDING_MESSAGES;
  protected readonly reasonMinLength = REJECTION_REASON_MIN_LENGTH;
  protected readonly rejectDialogMessage = rejectDialogMessage;
  protected readonly leaveTypes = Object.entries(LEAVE_TYPE_LABELS) as [LeaveType, string][];

  /** Rechargée à chaque changement d'utilisateur (l'en-tête X-User-Id est posé par l'interceptor). */
  protected readonly requests = rxResource({
    params: () => ({ userId: this.currentUser.userId() }),
    stream: () => this.leaveRequests.pending(),
  });

  protected readonly filters = signal<PendingFilters>(EMPTY_FILTERS);
  protected readonly filtered = computed(() =>
    filterPendingRequests(this.requests.value() ?? [], this.filters()),
  );

  /** Demande dont l'appel de décision est en cours : ses boutons sont désactivés. */
  protected readonly busyId = signal<number | null>(null);
  /** Demande dont le dialogue de refus est ouvert. */
  protected readonly rejecting = signal<LeaveRequest | null>(null);

  protected readonly columns: DataTableColumn<LeaveRequest>[] = [
    { key: 'employee', header: 'Collaborateur', value: (r) => r.employeeName },
    { key: 'team', header: 'Équipe', value: (r) => r.teamName },
    { key: 'type', header: 'Type', value: (r) => LEAVE_TYPE_LABELS[r.type] },
    { key: 'period', header: 'Période' },
    { key: 'days', header: 'Jours', align: 'end' },
    { key: 'status', header: 'Statut' },
    { key: 'actions', header: 'Actions' },
  ];

  protected readonly trackById = (r: LeaveRequest) => r.id;
  protected readonly rowTestId = (r: LeaveRequest) => 'leave-row-' + r.id;

  constructor() {
    // Changement d'identité : le dialogue de refus ouvert n'a plus de sens.
    effect(() => {
      this.currentUser.userId();
      untracked(() => this.rejecting.set(null));
    });
  }

  protected setFilter<K extends keyof PendingFilters>(key: K, value: PendingFilters[K]): void {
    this.filters.update((f) => ({ ...f, [key]: value }));
  }

  /** Approbation : un seul clic, sans confirmation (wiki, Interactions). */
  protected approve(request: LeaveRequest): void {
    this.decide(request, this.leaveRequests.approve(request.id), PENDING_MESSAGES.approved);
  }

  protected openReject(request: LeaveRequest): void {
    this.rejecting.set(request);
  }

  protected cancelReject(): void {
    if (this.busyId() === null) this.rejecting.set(null);
  }

  /** Refus confirmé : le dialogue se ferme après la réponse de l'API, succès ou échec. */
  protected confirmReject(reason: string): void {
    const request = this.rejecting();
    if (!request) return;
    this.decide(request, this.leaveRequests.reject(request.id, reason), PENDING_MESSAGES.rejected);
  }

  private decide(request: LeaveRequest, call: Observable<LeaveRequest>, successMessage: string): void {
    if (this.busyId() !== null) return;
    this.busyId.set(request.id);

    call
      .pipe(
        finalize(() => {
          this.busyId.set(null);
          this.rejecting.set(null);
        }),
      )
      .subscribe({
        next: () => {
          this.toast.success(successMessage);
          this.requests.update((list) => list?.filter((r) => r.id !== request.id));
        },
        error: (err: HttpErrorResponse) => {
          this.toast.error(errorMessage(err));
          this.requests.reload();
        },
      });
  }
}
