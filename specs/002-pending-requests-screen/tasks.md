---
description: "Tâches d'implémentation — Écran « Demandes en attente »"
---

# Tasks: Écran « Demandes en attente »

**Input**: Design documents from `specs/002-pending-requests-screen/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/ui-contract.md, quickstart.md

**Tests**: décision prise à l'implémentation (research R7) : TDD sur la logique extraite en fonctions pures
(`pending-filters.ts`, `pending-decision.ts`) avec `node --test`, natif dans Node 22, **sans aucune dépendance
ajoutée** : `docker compose exec web npm run test:unit`. Le rendu et les interactions restent vérifiés dans le
navigateur. Chaque phase se termine par une vérification :
- `docker compose exec web npx ng build` ;
- `docker compose exec web npm run lint:styles` ;
- les scénarios du `quickstart.md` concernés, déroulés dans le navigateur.

**Organization**: les tâches sont regroupées par user story, dans l'ordre de priorité de la spec : US1 (P1),
US2 (P1), US3 (P2), US4 (P2).

## Format: `[ID] [P?] [Story] Description`

## Conventions communes

- **Emplacement** : tous les fichiers sont dans `frontend/src/app/features/pending-requests/`.
- **Fichiers à ne pas modifier** : `core/`, `shared/`, `app.routes.ts`, `app.html` et le back-end.
- **Angular 20** (constitution VI) :
  - composant standalone ;
  - `signal`, `computed`, `effect`, `rxResource` (depuis `@angular/core/rxjs-interop`) ;
  - control flow `@if` / `@for` ;
  - aucun `*ngIf` ni `*ngFor` dans les nouveaux templates ;
  - styles dans un fichier `.css` référencé par `styleUrl`.
- **Modèle à suivre** : `frontend/src/app/features/my-requests/my-requests-page.ts` et `.html`.
- **Éléments réutilisés** :
  - `LeaveRequestService` (`pending()`, `approve(id)`, `reject(id, reason)`) ;
  - `CurrentUserService` (`userId()`) ;
  - `DataTable`, `DataTableCell` et `DataTableColumn` (`shared/data-table/data-table`) ;
  - `ConfirmDialog` (`shared/confirm-dialog/confirm-dialog`) ;
  - `ToastService` (`shared/toast/toast.service`) ;
  - `LEAVE_TYPE_LABELS`, `LeaveRequest`, `LeaveType` et `ApiProblem` (`core/api.models`) ;
  - `DatePipe`.
- **Classes globales existantes** (`frontend/src/styles.css`) : `.toolbar`, `.field`, `.field__label`, `.input`,
  `.select`, `.btn`, `.btn--primary`, `.btn--secondary`, `.btn--danger`, `.btn--sm`, `.state-panel`,
  `.state-panel--error`, `.state-panel__icon`, `.skeleton` et `.skeleton__row`.
- **Styles** : stylelint interdit les couleurs en dur et les fonctions `rgb()` / `hsl()`. Utiliser uniquement les
  variables `var(--space-*)` et `var(--color-*)` de `frontend/src/styles/tokens.css`.
- **Données de démonstration** (relancer `docker compose restart api` pour les réinitialiser) :
  - Hugo (2) décide les demandes de Lea, Karim et Ines ;
  - Sophie (6) décide celles de Thomas (événement familial) et de Yasmine ;
  - Claire (1) décide celle de Hugo ;
  - Lea (3) n'est pas manager.

---

## Phase 1: Setup (Shared Infrastructure)

- [x] T001 Vérifier le point de départ : `docker compose up -d`, puis `docker compose exec web npx ng build` et
  `docker compose exec web npm run lint:styles`, qui doivent réussir. Contrôler aussi que
  `curl -s localhost:4200/api/leave-requests/pending -H 'X-User-Id: 2'` renvoie les demandes 1, 2 et 7.

---

## Phase 2: Foundational (Blocking Prerequisites)

**⚠️ CRITICAL**: prépare le squelette de l'écran, nécessaire à toutes les stories.

- [x] T002 Remplacer le template inline de `frontend/src/app/features/pending-requests/pending-requests-page.ts`.
  - Décorateur : `@Component({ selector: 'app-pending-requests-page', imports: [DataTable, DataTableCell,
    ConfirmDialog, DatePipe], templateUrl: './pending-requests-page.html', styleUrl: './pending-requests-page.css' })`.
  - Classe `export class PendingRequestsPage` : injecter `LeaveRequestService` (`private readonly leaveRequests`),
    `CurrentUserService` (`protected readonly currentUser`) et `ToastService` (`private readonly toast`).
  - Retirer le commentaire « À IMPLÉMENTER ».
- [x] T003 [P] Créer `frontend/src/app/features/pending-requests/pending-requests-page.html` avec seulement
  `<h1>Demandes en attente</h1>`.
- [x] T004 [P] Créer `frontend/src/app/features/pending-requests/pending-requests-page.css` avec une règle
  `.actions { display: flex; gap: var(--space-2); }` pour les boutons d'action d'une ligne. Il n'est pas vide, car
  stylelint pourrait refuser un fichier vide.

**Checkpoint** : `ng build` et `lint:styles` passent, et `/manager/demandes` affiche le titre.

---

## Phase 3: User Story 1 — Voir les demandes à décider (Priority: P1) 🎯 MVP

**Goal**: lister les demandes en attente que l'utilisateur courant peut décider, avec les états chargement,
erreur et vide, et un rechargement au changement d'identité (FR-001 à FR-004).

**Independent Test**: en tant que Hugo, l'écran affiche 3 lignes (Lea, Karim, Ines). En basculant sur Sophie, la
liste se recharge et affiche Thomas et Yasmine (quickstart #1 à #4).

### Implementation for User Story 1

- [x] T005 [US1] Dans `frontend/src/app/features/pending-requests/pending-requests-page.ts`, ajouter les membres
  suivants.
  - `protected readonly requests = rxResource({ params: () => ({ userId: this.currentUser.userId() }), stream: () => this.leaveRequests.pending() });`
    Le paramètre `userId` sert uniquement à déclencher le rechargement : l'en-tête est posé par l'interceptor.
  - `protected readonly columns: DataTableColumn<LeaveRequest>[]`, conformément à `contracts/ui-contract.md` :
    - `{ key: 'employee', header: 'Collaborateur', value: (r) => r.employeeName }` ;
    - `{ key: 'team', header: 'Équipe', value: (r) => r.teamName }` ;
    - `{ key: 'type', header: 'Type', value: (r) => LEAVE_TYPE_LABELS[r.type] }` ;
    - `{ key: 'period', header: 'Période' }` ;
    - `{ key: 'days', header: 'Jours', align: 'end' }` ;
    - `{ key: 'comment', header: 'Commentaire', value: (r) => r.comment }` ;
    - `{ key: 'actions', header: 'Actions', align: 'end' }`.
  - `protected readonly trackById = (r: LeaveRequest) => r.id;`
  - `protected readonly rowTestId = (r: LeaveRequest) => 'pending-row-' + r.id;`
- [x] T006 [US1] Compléter `frontend/src/app/features/pending-requests/pending-requests-page.html` sous le `<h1>`,
  sur le modèle de `my-requests-page.html`. Dépend de T005.
  - **Chargement** : `@if (requests.isLoading())` affiche `<div class="skeleton" aria-busy="true">` avec deux
    `<div class="skeleton__row">`.
  - **Erreur** : `@else if (requests.error())` affiche
    `<div class="state-panel state-panel--error" data-testid="pending-error">`, avec le texte « Impossible de
    charger les demandes en attente. » et un bouton `btn btn--secondary` « Réessayer » qui appelle
    `requests.reload()`.
  - **Liste** : `@else if (requests.value()?.length)` affiche
    `<app-data-table [columns]="columns" [rows]="requests.value() ?? []" [trackBy]="trackById" [rowTestId]="rowTestId" caption="Demandes en attente de décision">`,
    avec deux templates de cellule :
    - `<ng-template appCell="period" let-row>du {{ row.startDate | date: 'dd/MM/yyyy' }} au {{ row.endDate | date: 'dd/MM/yyyy' }}</ng-template>` ;
    - `<ng-template appCell="actions" let-row><div class="actions"></div></ng-template>`. Ce conteneur reste vide
      à ce stade ; il est rempli en US2 et US3.
  - **Liste vide** : `@else` affiche `<div class="state-panel" data-testid="pending-empty">`, avec
    `<span class="state-panel__icon" aria-hidden="true">✅</span>` et le texte « Aucune demande en attente. ».
- [x] T007 [US1] Vérifier : `ng build` et `lint:styles` passent. Dérouler les scénarios #1 à #4 de
  `specs/002-pending-requests-screen/quickstart.md`, et le #15 (API arrêtée : panneau d'erreur, puis « Réessayer »).

**Checkpoint** : l'écran est consultable seul.

---

## Phase 4: User Story 2 — Approuver une demande (Priority: P1)

**Goal**: approuver après confirmation, avec une notification, un rechargement et un double envoi impossible
(FR-007, FR-009 à FR-012).

**Independent Test**: Hugo approuve la demande de Lea et confirme. Un toast de succès s'affiche, la ligne
disparaît, et « Mes demandes » de Lea affiche « Approuvée » avec un solde de 25 (quickstart #9 et #10).

### Implementation for User Story 2

- [x] T008 [US2] Dans `frontend/src/app/features/pending-requests/pending-requests-page.ts`, ajouter l'état et la
  logique de décision (data-model, research R4, R5 et R6).
  - **Type** : `type DecisionKind = 'approve' | 'reject';`
  - **Signals** :
    - `protected readonly pendingDecision = signal<{ kind: DecisionKind; request: LeaveRequest } | null>(null);`
    - `protected readonly busy = signal(false);`
  - **`protected open(kind: DecisionKind, request: LeaveRequest): void`** : appelle
    `this.pendingDecision.set({ kind, request })`.
  - **`protected close(): void`** : si `!this.busy()`, appelle `this.pendingDecision.set(null)`.
  - **`protected decisionMessage(r: LeaveRequest): string`** : renvoie
    `` `${r.employeeName} — du ${fmt(r.startDate)} au ${fmt(r.endDate)} (${r.days} jour${r.days > 1 ? 's' : ''})` ``.
    `fmt` convertit `yyyy-MM-dd` en `dd/MM/yyyy` par découpage de la chaîne, sans `new Date`, pour éviter tout
    décalage de fuseau horaire.
  - **`protected decide(reason: string): void`**, déroulé :
    1. Lire `const decision = this.pendingDecision();`. Si elle est nulle ou si `this.busy()` est vrai, sortir.
    2. `this.busy.set(true)`.
    3. Construire la requête :
       `const call = decision.kind === 'approve' ? this.leaveRequests.approve(decision.request.id) : this.leaveRequests.reject(decision.request.id, reason);`
    4. Appeler `call.pipe(finalize(() => { this.busy.set(false); this.pendingDecision.set(null); this.requests.reload(); })).subscribe(...)`.
    5. En succès (`next`) :
       `this.toast.success(`Demande de ${decision.request.employeeName} ${decision.kind === 'approve' ? 'approuvée' : 'refusée'}.`)`.
    6. En erreur (`error: (err: HttpErrorResponse)`) : `this.toast.error(problemMessage(err))`.
  - **Fonction locale `problemMessage(err: HttpErrorResponse): string`**, définie hors de la classe : avec
    `const problem = err.error as ApiProblem | null;`, renvoyer
    `problem?.detail ?? Object.values(problem?.errors ?? {})[0]?.[0] ?? "La décision n'a pas pu être enregistrée."`.
  - **Changement d'identité** : dans le constructeur,
    `effect(() => { this.currentUser.userId(); untracked(() => this.pendingDecision.set(null)); });`. Ce code
    ferme le dialogue si l'utilisateur change (edge case).
  - **Imports** : `finalize` depuis `rxjs` et `HttpErrorResponse` depuis `@angular/common/http`.
- [x] T009 [US2] Dans `frontend/src/app/features/pending-requests/pending-requests-page.html`. Dépend de T008.
  - Remplir le `<div class="actions">` du template `actions` avec :
    `<button type="button" class="btn btn--primary btn--sm" [attr.data-testid]="'approve-' + row.id" [attr.aria-label]="'Approuver la demande de ' + row.employeeName" (click)="open('approve', row)">Approuver</button>`.
  - Ajouter en fin de fichier, hors des `@if` :
    ```html
    @if (pendingDecision(); as decision) {
      <app-confirm-dialog
        [open]="true"
        [title]="decision.kind === 'approve' ? 'Approuver la demande' : 'Refuser la demande'"
        [message]="decisionMessage(decision.request)"
        [confirmLabel]="decision.kind === 'approve' ? 'Approuver' : 'Refuser'"
        [tone]="decision.kind === 'approve' ? 'default' : 'danger'"
        [busy]="busy()"
        (confirmed)="decide($event)"
        (cancelled)="close()" />
    }
    ```
- [x] T010 [US2] Vérifier : `ng build` et `lint:styles` passent. Dérouler les scénarios #9, #10 et #14 du
  quickstart. Vérifier aussi qu'un double clic sur « Approuver » dans le dialogue n'envoie qu'un seul appel (onglet
  Réseau du navigateur).

**Checkpoint** : le parcours P1 est complet (voir les demandes, puis approuver).

---

## Phase 5: User Story 3 — Refuser une demande avec un motif (Priority: P2)

**Goal**: refuser avec un motif obligatoire, qui doit être « non vide après suppression des espaces » (FR-008).

**Independent Test**: Hugo refuse la demande de Karim. Le bouton reste désactivé tant que le motif est vide.
Après la saisie de « Période de forte activité », un toast de succès s'affiche, la ligne disparaît et « Mes
demandes » de Karim affiche « Refusée » avec le motif (quickstart #11 et #12).

### Implementation for User Story 3

- [x] T011 [US3] Dans `frontend/src/app/features/pending-requests/pending-requests-page.html` :
  - ajouter, après le bouton « Approuver » du template `actions` :
    `<button type="button" class="btn btn--danger btn--sm" [attr.data-testid]="'reject-' + row.id" [attr.aria-label]="'Refuser la demande de ' + row.employeeName" (click)="open('reject', row)">Refuser</button>` ;
  - ajouter sur `<app-confirm-dialog>` :
    `[reasonLabel]="decision.kind === 'reject' ? 'Motif du refus' : undefined"` et
    `[reasonMinLength]="decision.kind === 'reject' ? 1 : 0"`.

  **Important** : avec la valeur par défaut `0`, `ConfirmDialog` accepterait un motif vide (research R4). La
  logique `decide(reason)` de T008 transmet déjà le motif, nettoyé de ses espaces par le dialogue.
- [x] T012 [US3] Vérifier : `ng build` passe. Dérouler les scénarios #11 et #12 du quickstart, avec un motif vide
  puis un motif fait seulement d'espaces : le bouton reste désactivé dans les deux cas.

**Checkpoint** : l'approbation et le refus fonctionnent.

---

## Phase 6: User Story 4 — Filtrer la liste (Priority: P2)

**Goal**: filtres combinés par collaborateur, type et période, avec « Effacer les filtres » et un état « aucun
résultat » (FR-005, FR-006).

**Independent Test**: Hugo saisit « kar » : seule la ligne de Karim reste. « Effacer les filtres » réaffiche les 3
lignes (quickstart #5 à #8 et #13).

### Implementation for User Story 4

- [x] T013 [P] [US4] Créer `frontend/src/app/features/pending-requests/pending-filters.ts`, conformément à
  data-model.md.
  - **Interface** : `export interface PendingFilters { employee: string; type: LeaveType | ''; from: string; to: string; }`
  - **Constante** : `export const EMPTY_FILTERS: PendingFilters = { employee: '', type: '', from: '', to: '' };`
  - **Fonction locale `normalize(s: string)`** : renvoie `s.normalize('NFD').replace(/\p{Diacritic}/gu, '').toLowerCase()`.
  - **`export function filterPendingRequests(requests: readonly LeaveRequest[], f: PendingFilters): LeaveRequest[]`**,
    qui renvoie `requests.filter(...)`, en conservant l'ordre, avec ces conditions combinées en ET :
    - si `f.employee.trim()` est rempli : `normalize(r.employeeName).includes(normalize(f.employee.trim()))` ;
    - si `f.type` est rempli : `r.type === f.type` ;
    - si `f.from` est rempli : `r.endDate >= f.from` ;
    - si `f.to` est rempli : `r.startDate <= f.to`.

    Les comparaisons de dates se font sur les chaînes `yyyy-MM-dd`.
  - **`export function hasActiveFilters(f: PendingFilters): boolean`** : vrai si au moins un champ est non vide
    après suppression des espaces.
- [x] T014 [US4] Dans `frontend/src/app/features/pending-requests/pending-requests-page.ts`. Dépend de T013.
  - Ajouter :
    - `protected readonly filters = signal<PendingFilters>(EMPTY_FILTERS);`
    - `protected readonly filtered = computed(() => filterPendingRequests(this.requests.value() ?? [], this.filters()));`
    - `protected readonly hasFilters = computed(() => hasActiveFilters(this.filters()));`
    - `protected readonly leaveTypes = Object.entries(LEAVE_TYPE_LABELS) as [LeaveType, string][];`
    - `protected setFilter<K extends keyof PendingFilters>(key: K, value: PendingFilters[K]): void { this.filters.update((f) => ({ ...f, [key]: value })); }`
    - `protected clearFilters(): void { this.filters.set(EMPTY_FILTERS); }`
  - Les filtres ne sont **pas** réinitialisés après une décision (FR-010).
- [x] T015 [US4] Dans `frontend/src/app/features/pending-requests/pending-requests-page.html`. Dépend de T014.
  1. **Barre de filtres** : insérer, juste après le `<h1>`,
     `<div class="toolbar" role="search" aria-label="Filtrer les demandes">`, qui contient quatre
     `<label class="field">` avec `<span class="field__label">` :
     - « Collaborateur » :
       `<input class="input" type="search" data-testid="pending-filter-employee" [value]="filters().employee" (input)="setFilter('employee', $any($event.target).value)" />` ;
     - « Type » :
       `<select class="select" data-testid="pending-filter-type" [value]="filters().type" (change)="setFilter('type', $any($event.target).value)">`,
       avec `<option value="">Tous les types</option>` puis
       `@for (t of leaveTypes; track t[0]) { <option [value]="t[0]">{{ t[1] }}</option> }` ;
     - « Du » :
       `<input class="input" type="date" data-testid="pending-filter-from" [value]="filters().from" (input)="setFilter('from', $any($event.target).value)" />` ;
     - « Au » : même structure, avec `data-testid="pending-filter-to"` et la clé `to`.

     Ajouter ensuite le bouton
     `@if (hasFilters()) { <button type="button" class="btn btn--secondary" data-testid="pending-filters-clear" (click)="clearFilters()">Effacer les filtres</button> }`.
  2. **Liste filtrée** : remplacer `[rows]="requests.value() ?? []"` par `[rows]="filtered()"`. Insérer, entre la
     branche erreur et la branche liste, la branche « aucun résultat » :
     `@else if (requests.value()?.length && !filtered().length)`, qui affiche
     `<div class="state-panel" data-testid="pending-no-match">`, avec le texte « Aucune demande ne correspond aux
     filtres. » et un bouton `btn btn--secondary` « Effacer les filtres » appelant `clearFilters()`.
     La branche liste reste `@else if (requests.value()?.length)` et la branche vide reste `@else`.
- [x] T016 [US4] Vérifier : `ng build` et `lint:styles` passent. Dérouler les scénarios #5 à #8 et #13 du
  quickstart : casse et accents ignorés (« KAR », « inès »), filtre par type, période sans résultat, effacement,
  filtre conservé après une décision.

**Checkpoint** : les 4 stories fonctionnent.

---

## Phase 7: Polish & Cross-Cutting Concerns

- [x] T017 Relire `frontend/src/app/features/pending-requests/` au regard de la constitution VI. Vérifier, avec
  `grep -n "ngIf\|ngFor\|NgModule\|HttpClient" frontend/src/app/features/pending-requests/*`, qu'aucun résultat
  n'apparaît : ni `*ngIf`, ni `*ngFor`, ni `NgModule`, ni `HttpClient` direct.
- [x] T018 Accessibilité (FR-012) : parcourir l'écran au clavier. La tabulation doit atteindre les filtres puis les
  boutons des lignes, Échap ferme le dialogue, et les `aria-label` des boutons nomment le collaborateur.
- [x] T019 Vérification finale (`/superpowers:verification-before-completion`) :
  - `docker compose exec web npx ng build` ;
  - `docker compose exec web npm run lint:styles` ;
  - `docker compose run --rm backend-tests`, en non-régression.

  Joindre la sortie réelle de chaque commande. Dérouler ensuite les 15 scénarios du quickstart après
  `docker compose restart api`.

---

## Dependencies & Execution Order

- **T001**, puis la **phase 2** (T002, puis T003 et T004 en parallèle), puis **US1** (T005 à T007).
- **US2** (T008 à T010) dépend de US1, car elle ajoute un bouton dans la cellule `actions`.
- **US3** (T011, T012) dépend de US2, car elle réutilise le dialogue et `decide`.
- **US4** (T013 à T016) dépend de US1 seulement. T013 est une fonction pure, faisable en parallèle dès la fin de
  la phase 2.
- **Polish** (T017 à T019) vient après toutes les stories.

```text
Setup ─► Foundational ─► US1 ─► US2 ─► US3
                          └──► US4 (T013 parallélisable dès Foundational)
```

### Parallel Opportunities

- T003 et T004 (fichiers distincts).
- T013 (`pending-filters.ts`) en parallèle de US1, US2 et US3.
- Les autres tâches touchent `pending-requests-page.ts` ou `.html` et sont donc séquentielles.

## Parallel Example

```text
Task: "T013 [US4] filterPendingRequests dans frontend/src/app/features/pending-requests/pending-filters.ts"
Task: "T008 [US2] logique de décision dans frontend/src/app/features/pending-requests/pending-requests-page.ts"
```

---

## Implementation Strategy

### MVP First

Phases 1 à 3 (US1) : l'écran liste les demandes à décider. **Arrêt et validation** avec les scénarios #1 à #4.

### Incremental Delivery

1. US1 : consultation.
2. Ajout de US2 : approbation, qui complète le parcours P1.
3. Ajout de US3 : refus motivé.
4. Ajout de US4 : filtres.
5. Polish, puis commit et push sur `julien-guillaume-damien`.

### Notes

- En cas de comportement inattendu, utiliser `/superpowers:systematic-debugging` avant toute correction.
- Faire un commit par phase terminée.
