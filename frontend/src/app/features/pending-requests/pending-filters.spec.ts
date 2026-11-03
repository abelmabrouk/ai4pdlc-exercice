// Exécuté par `npm run test:unit` (node --test, typage retiré nativement par Node 22).
import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import type { LeaveRequest } from '../../core/api.models';
import { EMPTY_FILTERS, filterPendingRequests, hasActiveFilters } from './pending-filters.ts';

function request(id: number, employeeName: string, overrides: Partial<LeaveRequest> = {}): LeaveRequest {
  return {
    id,
    employeeId: id,
    employeeName,
    teamId: 2,
    teamName: 'Produit',
    type: 'PaidLeave',
    startDate: '2027-03-08',
    endDate: '2027-03-12',
    days: 5,
    status: 'Pending',
    comment: null,
    rejectionReason: null,
    submittedAt: '2026-11-01T09:30:00+00:00',
    decidedAt: null,
    decidedById: null,
    ...overrides,
  };
}

const lea = request(1, 'Lea Petit', { startDate: '2027-01-04', endDate: '2027-01-08' });
const karim = request(2, 'Karim Benali', { startDate: '2027-02-01', endDate: '2027-02-03' });
const ines = request(7, 'Inès Moreau', { type: 'FamilyEvent', startDate: '2027-03-01', endDate: '2027-03-02' });
const all = [lea, karim, ines];
const ids = (list: LeaveRequest[]) => list.map((r) => r.id);

describe('filterPendingRequests', () => {
  it('keeps every request, in order, without filters', () => {
    assert.deepEqual(ids(filterPendingRequests(all, EMPTY_FILTERS)), [1, 2, 7]);
  });

  it('matches the employee name ignoring case', () => {
    assert.deepEqual(ids(filterPendingRequests(all, { ...EMPTY_FILTERS, employee: 'KAR' })), [2]);
  });

  it('matches the employee name ignoring accents, both ways', () => {
    assert.deepEqual(ids(filterPendingRequests(all, { ...EMPTY_FILTERS, employee: 'ines' })), [7]);
    assert.deepEqual(ids(filterPendingRequests([request(9, 'Ines Moreau')], { ...EMPTY_FILTERS, employee: 'inès' })), [9]);
  });

  it('ignores surrounding spaces in the employee filter', () => {
    assert.deepEqual(ids(filterPendingRequests(all, { ...EMPTY_FILTERS, employee: '  lea ' })), [1]);
  });

  it('filters by leave type', () => {
    assert.deepEqual(ids(filterPendingRequests(all, { ...EMPTY_FILTERS, type: 'FamilyEvent' })), [7]);
  });

  it('keeps requests overlapping the period, bounds included', () => {
    const filters = { ...EMPTY_FILTERS, from: '2027-01-08', to: '2027-02-01' };
    assert.deepEqual(ids(filterPendingRequests(all, filters)), [1, 2]);
  });

  it('accepts a single bound', () => {
    assert.deepEqual(ids(filterPendingRequests(all, { ...EMPTY_FILTERS, from: '2027-02-04' })), [7]);
    assert.deepEqual(ids(filterPendingRequests(all, { ...EMPTY_FILTERS, to: '2027-01-31' })), [1]);
  });

  it('returns nothing when the period is inverted', () => {
    const filters = { ...EMPTY_FILTERS, from: '2027-03-01', to: '2027-01-01' };
    assert.deepEqual(filterPendingRequests(all, filters), []);
  });

  it('combines filters with AND', () => {
    const filters = { ...EMPTY_FILTERS, employee: 'e', type: 'PaidLeave' as const, from: '2027-02-01' };
    assert.deepEqual(ids(filterPendingRequests(all, filters)), [2]);
  });
});

describe('hasActiveFilters', () => {
  it('is false for empty or blank filters', () => {
    assert.equal(hasActiveFilters(EMPTY_FILTERS), false);
    assert.equal(hasActiveFilters({ ...EMPTY_FILTERS, employee: '   ' }), false);
  });

  it('is true as soon as one filter is set', () => {
    assert.equal(hasActiveFilters({ ...EMPTY_FILTERS, type: 'PaidLeave' }), true);
    assert.equal(hasActiveFilters({ ...EMPTY_FILTERS, to: '2027-01-01' }), true);
  });
});
