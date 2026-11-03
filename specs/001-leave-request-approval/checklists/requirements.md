# Specification Quality Checklist: Soumission et validation des demandes de congé

**Purpose**: Validate specification completeness and quality before proceeding to planning
**Created**: 2026-11-03
**Feature**: [spec.md](../spec.md)

## Content Quality

- [x] No implementation details (languages, frameworks, APIs)
- [x] Focused on user value and business needs
- [x] Written for non-technical stakeholders
- [x] All mandatory sections completed

## Requirement Completeness

- [x] No [NEEDS CLARIFICATION] markers remain
- [x] Requirements are testable and unambiguous
- [x] Success criteria are measurable
- [x] Success criteria are technology-agnostic (no implementation details)
- [x] All acceptance scenarios are defined
- [x] Edge cases are identified
- [x] Scope is clearly bounded
- [x] Dependencies and assumptions identified

## Feature Readiness

- [x] All functional requirements have clear acceptance criteria
- [x] User scenarios cover primary flows
- [x] Feature meets measurable outcomes defined in Success Criteria
- [x] No implementation details leak into specification

## Project-specific (ADR-006)

- [x] « Références » section cites applicable ADRs and RG-xx rules
- [x] Acceptance scenarios carry the RG-xx identifiers they cover
- [x] Every error case cites its code from the Codes d'erreur referential

## Notes

- Iteration 1: 2 open questions (decider of top-level manager's requests; balance check including pending
  requests), answered by the user.
- Iteration 2: rewrite based on the project wiki (règles de gestion, décompte des jours, circuit de validation,
  codes d'erreur, ADRs). Main changes: jours ouvrables instead of jours ouvrés, notice period (RG-03/RG-04), refusal
  reason ≥ 10 characters (RG-13), error codes and statuses (ADR-003), no balance re-check at approval, Références
  section (ADR-006). The Q1 answer (« Claire decides her own requests ») is superseded by the Circuit de validation
  page, which prevails: the direction générale's requests have no decider in the application.
- Error codes and HTTP statuses appear in the spec on purpose: ADR-006 requires them, and they are part of the
  contract consumed by the front and partner applications, not an implementation choice.
- Items marked incomplete require spec updates before `/speckit-clarify` or `/speckit-plan`
