// Exécuté par `npm run test:unit`.
import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { GENERIC_ERROR_MESSAGE, errorMessage } from './api-errors.ts';

// Wiki, page « Messages » : table des codes d'erreur de l'API, libellés repris à l'identique.
const expected: Record<string, string> = {
  LEAVE_INVALID_PERIOD: "La période demandée n'est pas valide.",
  LEAVE_NOTICE_TOO_SHORT: "Le délai de prévenance n'est pas respecté.",
  LEAVE_INSUFFICIENT_BALANCE: 'Le solde de congés est insuffisant.',
  LEAVE_OVERLAP: 'Cette demande chevauche une autre absence.',
  LEAVE_REJECTION_REASON_REQUIRED: 'Le motif du refus doit contenir au moins 10 caractères.',
  LEAVE_APPROVER_NOT_ALLOWED: "Vous n'êtes pas habilité à décider de cette demande.",
  LEAVE_ALREADY_DECIDED: 'Cette demande a déjà été traitée.',
  LEAVE_NOT_FOUND: "Cette demande n'existe plus.",
};

describe('errorMessage', () => {
  for (const [code, label] of Object.entries(expected)) {
    it(`maps ${code}`, () => {
      assert.equal(errorMessage({ error: { status: 422, code, title: 'x', detail: 'détail technique' } }), label);
    });
  }

  it('never shows title or detail, even without a code', () => {
    const message = errorMessage({ error: { status: 400, title: 'La requête est invalide.', detail: 'technique' } });
    assert.equal(message, GENERIC_ERROR_MESSAGE);
  });

  it('uses the generic label for an unknown code, a network error or a non-JSON body', () => {
    assert.equal(errorMessage({ error: { code: 'LEAVE_SOMETHING_NEW' } }), GENERIC_ERROR_MESSAGE);
    assert.equal(errorMessage({ error: null }), GENERIC_ERROR_MESSAGE);
    assert.equal(errorMessage({ error: 'Bad Gateway' }), GENERIC_ERROR_MESSAGE);
  });

  it('has the exact generic label', () => {
    assert.equal(GENERIC_ERROR_MESSAGE, 'Une erreur inattendue est survenue. Veuillez réessayer.');
  });
});
