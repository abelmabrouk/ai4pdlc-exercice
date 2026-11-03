import type { LeaveRequest } from '../../core/api.models';

/** Wiki, page « Messages », section « Écran Demandes en attente » : libellés repris à l'identique. */
export const PENDING_MESSAGES = {
  empty: 'Aucune demande en attente.',
  noMatch: 'Aucune demande ne correspond aux filtres.',
  loadError: 'Impossible de charger les demandes en attente.',
  retry: 'Réessayer',
  approve: 'Approuver',
  reject: 'Refuser',
  approved: 'Demande approuvée.',
  rejected: 'Demande refusée.',
  rejectDialogTitle: 'Refuser la demande',
  rejectDialogReason: 'Motif du refus',
  rejectDialogConfirm: 'Refuser',
  searchLabel: 'Collaborateur',
  searchPlaceholder: 'Rechercher un collaborateur',
  typeLabel: 'Type',
  allTypes: 'Tous les types',
} as const;

/** yyyy-MM-dd → jj/mm/aaaa, sans passer par Date (aucun décalage de fuseau). */
function formatDate(isoDate: string): string {
  const [year, month, day] = isoDate.split('-');
  return `${day}/${month}/${year}`;
}

/** « Demande de {collaborateur} du {début} au {fin}. » */
export function rejectDialogMessage(r: LeaveRequest): string {
  return `Demande de ${r.employeeName} du ${formatDate(r.startDate)} au ${formatDate(r.endDate)}.`;
}
