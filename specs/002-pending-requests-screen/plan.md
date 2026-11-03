# Implementation Plan: Écran « Demandes en attente »

**Branch**: `julien-guillaume-damien` | **Date**: 2026-11-03 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/002-pending-requests-screen/spec.md`

> **Révisé le 2026-11-03** (wiki) : décisions W1 à W6 dans `research.md`, contrat dans `contracts/ui-contract.md`.
> Fichiers : `core/api-errors.ts` (+ spec), `pending-filters.ts` (+ spec), `pending-messages.ts` (+ spec) ;
> `pending-decision.ts` supprimé.

## Summary

Implémenter le composant vide `PendingRequestsPage` (`/manager/demandes`) : liste des demandes à décider chargée
par `rxResource` sur `LeaveRequestService.pending()`, filtres client (collaborateur, type, période) en signals et
`computed`, décisions via le `ConfirmDialog` partagé (motif obligatoire pour le refus), retours par `ToastService`,
rechargement après chaque décision. Aucune modification du back-end, des services `core/` ni des routes.

## Technical Context

**Language/Version**: TypeScript, Angular 20.3

**Primary Dependencies**: `@angular/core` (signals, `rxResource`), `@angular/common` (`DatePipe`), composants partagés
`DataTable`, `ConfirmDialog`, `StatusBadge` (non requis), `ToastService`

**Storage**: N/A (état local du composant ; données via l'API de la partie 1)

**Testing**: `node --test` natif (script `npm run test:unit`, sans dépendance) sur les fonctions pures ; `ng build`,
`lint:styles` et scénarios navigateur pour le composant (research R7)

**Target Platform**: navigateur, servi par `ng serve` dans le conteneur `web`

**Project Type**: web application (front uniquement pour cette partie)

**Performance Goals**: filtrage instantané (liste d'une équipe, quelques dizaines de lignes au plus)

**Constraints**: constitution VI ; route et service HTTP existants imposés par l'énoncé ; stylelint (pas de couleurs
en dur, tokens uniquement)

**Scale/Scope**: 1 écran, 4 fichiers dans `features/pending-requests/`

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principe | Vérification | Statut |
|---|---|---|
| I. Couches (back) | Back-end non modifié | ✅ N/A |
| II. Contrats | Consomme le contrat existant ; dates `yyyy-MM-dd` comparées en chaînes ; erreurs `ApiProblem` | ✅ |
| III. Réutilisation | `LeaveRequestService`, `CurrentUserService`, `DataTable`/`DataTableCell`, `ConfirmDialog`, `ToastService`, `LEAVE_TYPE_LABELS`, classes globales (`.toolbar`, `.field`, `.input`, `.select`, `.btn`, `.state-panel`, `.skeleton`) | ✅ |
| IV. Tests | Exigence xUnit portant sur le back ; `backend-tests` relancé en non-régression | ✅ |
| V. Temps et données | N/A (pas de date courante utilisée) | ✅ N/A |
| VI. Angular moderne | Composant standalone ; `signal`, `computed`, `effect`, `rxResource` ; `@if`/`@for` ; styles dans `pending-requests-page.css` via `styleUrl` ; HTTP uniquement via `core/` | ✅ |
| VII. Simplicité | Filtres client sans Reactive Forms ; un seul dialogue ; aucune lib ni outillage ajouté | ✅ |

**Re-check post-design** : ✅ conforme, aucune dérogation.

## Project Structure

### Documentation (this feature)

```text
specs/002-pending-requests-screen/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/ui-contract.md
├── checklists/requirements.md
└── tasks.md             # /speckit-tasks
```

### Source Code (repository root)

```text
frontend/src/app/features/pending-requests/
├── pending-requests-page.ts      # MODIFIÉ : template inline remplacé par templateUrl + styleUrl, logique de l'écran
├── pending-requests-page.html    # NOUVEAU : toolbar de filtres, états, tableau, dialogue
├── pending-requests-page.css     # NOUVEAU : mise en page spécifique (tokens uniquement)
├── pending-filters.ts            # NOUVEAU : PendingFilters + filterPendingRequests (fonction pure)
├── pending-filters.spec.ts       # NOUVEAU : tests node:test
├── pending-decision.ts           # NOUVEAU : messages de confirmation / succès / erreur (fonctions pures)
└── pending-decision.spec.ts      # NOUVEAU : tests node:test
```

Inchangés : `core/*`, `shared/*`, `app.routes.ts`, `app.html`, back-end.

**Structure Decision**: tout le travail est confiné à la feature `pending-requests`, conformément à l'énoncé
(« Vous ne construisez que l'écran »).

## Design notes

- `requests = rxResource({ params: () => ({ userId: currentUser.userId() }), stream: () => leaveRequests.pending() })`.
- `decide(reason)` : `busy.set(true)` ; appelle `approve(id)` ou `reject(id, reason)` ; en succès toast + fermeture ;
  en erreur toast avec message extrait (`problemMessage(err)`) + fermeture ; dans les deux cas `requests.reload()` et
  `busy.set(false)` (`finalize`).
- `ConfirmDialog` : `[reasonLabel]` uniquement pour le refus, `[reasonMinLength]="1"` (sinon motif vide accepté).
- `effect(() => { currentUser.userId(); untracked(() => pendingDecision.set(null)); })`.
- Les dates de la ligne sont formatées avec `DatePipe` (`dd/MM/yyyy`), comme `MyRequestsPage`.

## Complexity Tracking

Aucune violation.
