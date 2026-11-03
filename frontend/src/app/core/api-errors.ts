import type { ApiProblem } from './api.models';

/** Wiki, page « Messages » : libellé générique (code inconnu, pas de code, erreur réseau). */
export const GENERIC_ERROR_MESSAGE = 'Une erreur inattendue est survenue. Veuillez réessayer.';

/** Wiki, page « Messages » : libellé affiché pour chaque code du référentiel « Codes d'erreur ». */
const MESSAGES_BY_CODE: Readonly<Record<string, string>> = {
  LEAVE_INVALID_PERIOD: "La période demandée n'est pas valide.",
  LEAVE_NOTICE_TOO_SHORT: "Le délai de prévenance n'est pas respecté.",
  LEAVE_INSUFFICIENT_BALANCE: 'Le solde de congés est insuffisant.',
  LEAVE_OVERLAP: 'Cette demande chevauche une autre absence.',
  LEAVE_REJECTION_REASON_REQUIRED: 'Le motif du refus doit contenir au moins 10 caractères.',
  LEAVE_APPROVER_NOT_ALLOWED: "Vous n'êtes pas habilité à décider de cette demande.",
  LEAVE_ALREADY_DECIDED: 'Cette demande a déjà été traitée.',
  LEAVE_NOT_FOUND: "Cette demande n'existe plus.",
};

/**
 * Libellé à afficher pour une erreur HTTP de l'API (ADR-003) : uniquement à partir du champ `code`,
 * jamais de `title`, de `detail` ni du message HTTP.
 */
export function errorMessage(err: { error: unknown }): string {
  const code = typeof err.error === 'object' && err.error !== null ? (err.error as ApiProblem).code : undefined;
  return (code && Object.hasOwn(MESSAGES_BY_CODE, code) && MESSAGES_BY_CODE[code]) || GENERIC_ERROR_MESSAGE;
}
