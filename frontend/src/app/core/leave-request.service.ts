import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { Employee, LeaveRequest, LeaveStatus, SubmitLeaveRequest } from './api.models';

/**
 * Accès HTTP à l'API des congés. Couvre le contrat d'API de ENONCE.md.
 * Les erreurs sont propagées telles quelles (HttpErrorResponse dont le corps est un ApiProblem).
 */
@Injectable({ providedIn: 'root' })
export class LeaveRequestService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/leave-requests';

  me(): Observable<Employee> {
    return this.http.get<Employee>('/api/me');
  }

  list(filter: { employeeId?: number; teamId?: number; status?: LeaveStatus } = {}): Observable<LeaveRequest[]> {
    let params = new HttpParams();
    for (const [key, value] of Object.entries(filter)) {
      if (value !== undefined && value !== null) params = params.set(key, String(value));
    }
    return this.http.get<LeaveRequest[]>(this.baseUrl, { params });
  }

  get(id: number): Observable<LeaveRequest> {
    return this.http.get<LeaveRequest>(`${this.baseUrl}/${id}`);
  }

  submit(request: SubmitLeaveRequest): Observable<LeaveRequest> {
    return this.http.post<LeaveRequest>(this.baseUrl, request);
  }

  /** Demandes en attente que l'utilisateur courant peut décider. */
  pending(): Observable<LeaveRequest[]> {
    return this.http.get<LeaveRequest[]>(`${this.baseUrl}/pending`);
  }

  approve(id: number): Observable<LeaveRequest> {
    return this.http.post<LeaveRequest>(`${this.baseUrl}/${id}/approve`, {});
  }

  reject(id: number, reason: string): Observable<LeaveRequest> {
    return this.http.post<LeaveRequest>(`${this.baseUrl}/${id}/reject`, { reason });
  }
}
