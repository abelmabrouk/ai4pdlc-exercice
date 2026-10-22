export type LeaveType = 'PaidLeave' | 'FamilyEvent';
export type LeaveStatus = 'Pending' | 'Approved' | 'Rejected';

export interface Employee {
  id: number;
  firstName: string;
  lastName: string;
  fullName: string;
  email: string;
  teamId: number;
  teamName: string;
  isManager: boolean;
  paidLeaveBalance: number;
}

export interface LeaveRequest {
  id: number;
  employeeId: number;
  employeeName: string;
  teamId: number;
  teamName: string;
  type: LeaveType;
  /** yyyy-MM-dd */
  startDate: string;
  /** yyyy-MM-dd */
  endDate: string;
  days: number;
  status: LeaveStatus;
  comment: string | null;
  rejectionReason: string | null;
  submittedAt: string;
  decidedAt: string | null;
  decidedById: number | null;
}

export interface SubmitLeaveRequest {
  type: LeaveType;
  startDate: string;
  endDate: string;
  comment?: string;
}

/** Corps d'erreur renvoyé par l'API (RFC 7807). */
export interface ApiProblem {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  code?: string;
  errors?: Record<string, string[]>;
}

export const LEAVE_TYPE_LABELS: Record<LeaveType, string> = {
  PaidLeave: 'Congés payés',
  FamilyEvent: 'Événement familial',
};
