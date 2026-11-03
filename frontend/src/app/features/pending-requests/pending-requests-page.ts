import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, effect, inject, signal, untracked } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { finalize } from 'rxjs';
import { LEAVE_TYPE_LABELS, LeaveRequest, LeaveType } from '../../core/api.models';
import { CurrentUserService } from '../../core/current-user.service';
import { LeaveRequestService } from '../../core/leave-request.service';
import { ConfirmDialog } from '../../shared/confirm-dialog/confirm-dialog';
import { DataTable, DataTableCell, DataTableColumn } from '../../shared/data-table/data-table';
import { ToastService } from '../../shared/toast/toast.service';
import { DecisionKind, decisionMessage, problemMessage, successMessage } from './pending-decision';
import { EMPTY_FILTERS, PendingFilters, filterPendingRequests, hasActiveFilters } from './pending-filters';

/**
 * Écran manager « Demandes en attente » : demandes que l'utilisateur courant peut décider,
 * filtres locaux, approbation et refus motivé.
 */
@Component({
  selector: 'app-pending-requests-page',
  imports: [DataTable, DataTableCell, ConfirmDialog, DatePipe],
  templateUrl: './pending-requests-page.html',
  styleUrl: './pending-requests-page.css',
})
export class PendingRequestsPage {
  private readonly leaveRequests = inject(LeaveRequestService);
  private readonly currentUser = inject(CurrentUserService);
  private readonly toast = inject(ToastService);

  /** Rechargée à chaque changement d'utilisateur (l'en-tête X-User-Id est posé par l'interceptor). */
  protected readonly requests = rxResource({
    params: () => ({ userId: this.currentUser.userId() }),
    stream: () => this.leaveRequests.pending(),
  });

  protected readonly filters = signal<PendingFilters>(EMPTY_FILTERS);
  protected readonly filtered = computed(() =>
    filterPendingRequests(this.requests.value() ?? [], this.filters()),
  );
  protected readonly hasFilters = computed(() => hasActiveFilters(this.filters()));
  protected readonly leaveTypes = Object.entries(LEAVE_TYPE_LABELS) as [LeaveType, string][];

  protected readonly pendingDecision = signal<{ kind: DecisionKind; request: LeaveRequest } | null>(null);
  protected readonly busy = signal(false);
  protected readonly decisionMessage = decisionMessage;

  protected readonly columns: DataTableColumn<LeaveRequest>[] = [
    { key: 'employee', header: 'Collaborateur', value: (r) => r.employeeName },
    { key: 'team', header: 'Équipe', value: (r) => r.teamName },
    { key: 'type', header: 'Type', value: (r) => LEAVE_TYPE_LABELS[r.type] },
    { key: 'period', header: 'Période' },
    { key: 'days', header: 'Jours', align: 'end' },
    { key: 'comment', header: 'Commentaire', value: (r) => r.comment },
    { key: 'actions', header: 'Actions', align: 'end' },
  ];

  protected readonly trackById = (r: LeaveRequest) => r.id;
  protected readonly rowTestId = (r: LeaveRequest) => 'pending-row-' + r.id;

  constructor() {
    // Changement d'identité : la fenêtre de décision ouverte n'a plus de sens.
    effect(() => {
      this.currentUser.userId();
      untracked(() => this.pendingDecision.set(null));
    });
  }

  protected setFilter<K extends keyof PendingFilters>(key: K, value: PendingFilters[K]): void {
    this.filters.update((f) => ({ ...f, [key]: value }));
  }

  protected clearFilters(): void {
    this.filters.set(EMPTY_FILTERS);
  }

  protected open(kind: DecisionKind, request: LeaveRequest): void {
    this.pendingDecision.set({ kind, request });
  }

  protected close(): void {
    if (!this.busy()) this.pendingDecision.set(null);
  }

  protected decide(reason: string): void {
    const decision = this.pendingDecision();
    if (!decision || this.busy()) return;

    this.busy.set(true);
    const { kind, request } = decision;
    const call =
      kind === 'approve'
        ? this.leaveRequests.approve(request.id)
        : this.leaveRequests.reject(request.id, reason);

    call
      .pipe(
        finalize(() => {
          this.busy.set(false);
          this.pendingDecision.set(null);
          this.requests.reload();
        }),
      )
      .subscribe({
        next: () => this.toast.success(successMessage(kind, request)),
        error: (err: HttpErrorResponse) => this.toast.error(problemMessage(err)),
      });
  }
}
