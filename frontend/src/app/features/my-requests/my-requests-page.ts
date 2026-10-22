import { DatePipe } from '@angular/common';
import { Component, inject } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { LEAVE_TYPE_LABELS, LeaveRequest } from '../../core/api.models';
import { CurrentUserService } from '../../core/current-user.service';
import { LeaveRequestService } from '../../core/leave-request.service';
import { DataTable, DataTableCell, DataTableColumn } from '../../shared/data-table/data-table';
import { StatusBadge } from '../../shared/status-badge/status-badge';

@Component({
  selector: 'app-my-requests-page',
  imports: [DataTable, DataTableCell, StatusBadge, DatePipe],
  templateUrl: './my-requests-page.html',
})
export class MyRequestsPage {
  private readonly leaveRequests = inject(LeaveRequestService);
  protected readonly currentUser = inject(CurrentUserService);

  protected readonly requests = rxResource({
    params: () => ({ employeeId: this.currentUser.userId() }),
    stream: ({ params }) => this.leaveRequests.list(params),
  });

  protected readonly columns: DataTableColumn<LeaveRequest>[] = [
    { key: 'type', header: 'Type', value: (r) => LEAVE_TYPE_LABELS[r.type] },
    { key: 'period', header: 'Période' },
    { key: 'days', header: 'Jours', align: 'end' },
    { key: 'status', header: 'Statut' },
    { key: 'comment', header: 'Commentaire', value: (r) => r.rejectionReason ?? r.comment },
  ];

  protected readonly trackById = (r: LeaveRequest) => r.id;
}
