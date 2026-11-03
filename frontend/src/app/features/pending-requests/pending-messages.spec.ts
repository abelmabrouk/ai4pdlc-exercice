// Exécuté par `npm run test:unit`.
import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import type { LeaveRequest } from '../../core/api.models';
import { PENDING_MESSAGES, rejectDialogMessage } from './pending-messages.ts';

// Wiki, page « Messages », section « Écran Demandes en attente » : libellés repris à l'identique.
describe('PENDING_MESSAGES', () => {
  it('matches the wiki labels', () => {
    assert.deepEqual(PENDING_MESSAGES, {
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
    });
  });
});

describe('rejectDialogMessage', () => {
  it('names the employee and the period in dd/MM/yyyy', () => {
    const karim = { employeeName: 'Karim Benali', startDate: '2026-11-30', endDate: '2026-12-02' } as LeaveRequest;
    assert.equal(rejectDialogMessage(karim), 'Demande de Karim Benali du 30/11/2026 au 02/12/2026.');
  });
});
