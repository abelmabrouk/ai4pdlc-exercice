// Exécuté par `npm run test:unit`.
import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import type { LeaveRequest } from '../../core/api.models';
import { decisionMessage, problemMessage, successMessage } from './pending-decision.ts';

const lea = {
  employeeName: 'Lea Petit',
  startDate: '2027-03-08',
  endDate: '2027-03-12',
  days: 5,
} as LeaveRequest;

describe('decisionMessage', () => {
  it('recalls employee, period in dd/MM/yyyy and days', () => {
    assert.equal(decisionMessage(lea), 'Lea Petit — du 08/03/2027 au 12/03/2027 (5 jours)');
  });

  it('uses the singular for a single day', () => {
    assert.equal(
      decisionMessage({ ...lea, startDate: '2027-03-08', endDate: '2027-03-08', days: 1 }),
      'Lea Petit — du 08/03/2027 au 08/03/2027 (1 jour)',
    );
  });
});

describe('successMessage', () => {
  it('names the employee and the decision', () => {
    assert.equal(successMessage('approve', lea), 'Demande de Lea Petit approuvée.');
    assert.equal(successMessage('reject', lea), 'Demande de Lea Petit refusée.');
  });
});

describe('problemMessage', () => {
  it('prefers the problem detail', () => {
    const error = { error: { status: 409, detail: 'La demande a déjà été décidée.', code: 'NotPending' } };
    assert.equal(problemMessage(error), 'La demande a déjà été décidée.');
  });

  it('falls back to the first validation message', () => {
    const error = { error: { status: 400, errors: { reason: ['Le motif de refus est obligatoire.'] } } };
    assert.equal(problemMessage(error), 'Le motif de refus est obligatoire.');
  });

  it('uses a generic message when the body is not a problem', () => {
    assert.equal(problemMessage({ error: null }), "La décision n'a pas pu être enregistrée.");
    assert.equal(problemMessage({ error: 'Bad Gateway' }), "La décision n'a pas pu être enregistrée.");
  });
});
