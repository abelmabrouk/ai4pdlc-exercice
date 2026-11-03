import type { LeaveRequest, LeaveType } from '../../core/api.models';

/** Filtres de l'écran « Demandes en attente » (wiki : Type, Collaborateur). Chaîne vide = filtre inactif. */
export interface PendingFilters {
  type: LeaveType | '';
  search: string;
}

export const EMPTY_FILTERS: PendingFilters = { type: '', search: '' };

/** Filtres combinés sur la liste chargée ; recherche sur le nom insensible à la casse ; ordre conservé. */
export function filterPendingRequests(requests: readonly LeaveRequest[], filters: PendingFilters): LeaveRequest[] {
  const search = filters.search.trim().toLowerCase();
  return requests.filter(
    (r) =>
      (!filters.type || r.type === filters.type) &&
      (!search || r.employeeName.toLowerCase().includes(search)),
  );
}
