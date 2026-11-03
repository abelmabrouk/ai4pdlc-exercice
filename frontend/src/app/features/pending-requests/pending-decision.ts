import type { ApiProblem, LeaveRequest } from '../../core/api.models';

export type DecisionKind = 'approve' | 'reject';

const GENERIC_ERROR = "La décision n'a pas pu être enregistrée.";

/** yyyy-MM-dd → dd/MM/yyyy, sans passer par Date (aucun décalage de fuseau). */
function formatDate(isoDate: string): string {
  const [year, month, day] = isoDate.split('-');
  return `${day}/${month}/${year}`;
}

/** Rappel affiché dans la fenêtre de confirmation. */
export function decisionMessage(r: LeaveRequest): string {
  const days = `${r.days} jour${r.days > 1 ? 's' : ''}`;
  return `${r.employeeName} — du ${formatDate(r.startDate)} au ${formatDate(r.endDate)} (${days})`;
}

export function successMessage(kind: DecisionKind, r: LeaveRequest): string {
  return `Demande de ${r.employeeName} ${kind === 'approve' ? 'approuvée' : 'refusée'}.`;
}

/** Message d'une erreur HTTP dont le corps est un ApiProblem (RFC 7807). */
export function problemMessage(err: { error: unknown }): string {
  const problem = typeof err.error === 'object' && err.error !== null ? (err.error as ApiProblem) : null;
  return problem?.detail ?? Object.values(problem?.errors ?? {})[0]?.[0] ?? GENERIC_ERROR;
}
