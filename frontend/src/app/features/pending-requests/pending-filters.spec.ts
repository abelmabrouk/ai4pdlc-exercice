// Exécuté par `npm run test:unit`.
import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import type { LeaveRequest } from '../../core/api.models';
import { EMPTY_FILTERS, filterPendingRequests } from './pending-filters.ts';

function request(id: number, employeeName: string, type: LeaveRequest['type'] = 'PaidLeave'): LeaveRequest {
  return {
    id, employeeId: id, employeeName, teamId: 2, teamName: 'Produit', type,
    startDate: '2027-03-08', endDate: '2027-03-12', days: 5, status: 'Pending',
    comment: null, rejectionReason: null, submittedAt: '2026-11-01T09:30:00+00:00', decidedAt: null, decidedById: null,
  };
}

const all = [request(1, 'Lea Petit'), request(2, 'Karim Benali'), request(4, 'Thomas Roux', 'FamilyEvent')];
const ids = (list: LeaveRequest[]) => list.map((r) => r.id);

// Wiki, page « Interactions » : filtres appliqués sur la liste chargée, recherche texte insensible à la casse.
describe('filterPendingRequests', () => {
  it('keeps every request, in order, without filters', () => {
    assert.deepEqual(ids(filterPendingRequests(all, EMPTY_FILTERS)), [1, 2, 4]);
  });

  it('searches the employee name ignoring case', () => {
    assert.deepEqual(ids(filterPendingRequests(all, { ...EMPTY_FILTERS, search: 'KAR' })), [2]);
    assert.deepEqual(ids(filterPendingRequests(all, { ...EMPTY_FILTERS, search: 'petit' })), [1]);
  });

  it('ignores surrounding spaces in the search', () => {
    assert.deepEqual(ids(filterPendingRequests(all, { ...EMPTY_FILTERS, search: '  roux ' })), [4]);
  });

  it('filters by leave type', () => {
    assert.deepEqual(ids(filterPendingRequests(all, { ...EMPTY_FILTERS, type: 'FamilyEvent' })), [4]);
    assert.deepEqual(ids(filterPendingRequests(all, { ...EMPTY_FILTERS, type: 'PaidLeave' })), [1, 2]);
  });

  it('combines type and search', () => {
    assert.deepEqual(filterPendingRequests(all, { type: 'FamilyEvent', search: 'karim' }), []);
  });
});
