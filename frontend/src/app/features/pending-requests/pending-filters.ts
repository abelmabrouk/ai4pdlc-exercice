import type { LeaveRequest, LeaveType } from '../../core/api.models';

/** Filtres de l'écran « Demandes en attente » ; dates au format yyyy-MM-dd, chaîne vide = filtre inactif. */
export interface PendingFilters {
  employee: string;
  type: LeaveType | '';
  from: string;
  to: string;
}

export const EMPTY_FILTERS: PendingFilters = { employee: '', type: '', from: '', to: '' };

/** Minuscules sans accents, pour une recherche tolérante. */
function normalize(value: string): string {
  return value.normalize('NFD').replace(/\p{Diacritic}/gu, '').toLowerCase();
}

/**
 * Filtres combinés (ET) ; l'ordre d'entrée est conservé.
 * Période : on garde les demandes qui chevauchent [from, to] (comparaison de chaînes yyyy-MM-dd).
 */
export function filterPendingRequests(requests: readonly LeaveRequest[], filters: PendingFilters): LeaveRequest[] {
  const employee = normalize(filters.employee.trim());
  return requests.filter(
    (r) =>
      (!employee || normalize(r.employeeName).includes(employee)) &&
      (!filters.type || r.type === filters.type) &&
      (!filters.from || r.endDate >= filters.from) &&
      (!filters.to || r.startDate <= filters.to),
  );
}

export function hasActiveFilters(filters: PendingFilters): boolean {
  return Object.values(filters).some((value) => value.trim() !== '');
}
