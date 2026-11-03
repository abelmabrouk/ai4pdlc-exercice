# Research — Écran « Demandes en attente »

Pile imposée (Angular 20, constitution VI) ; aucune inconnue technique. Décisions de conception :

## R1 — Source des données et rechargement

- **Decision** : `rxResource` alimenté par `LeaveRequestService.pending()` (déjà existant), avec
  `params: () => ({ userId: currentUser.userId() })` pour recharger automatiquement au changement d'identité
  (FR-004) ; `reload()` après chaque décision.
- **Rationale** : même patron que `MyRequestsPage` (cohérence, principe III) ; l'en-tête `X-User-Id` est posé par
  `user-id.interceptor.ts`.
- **Alternatives** : retirer localement la ligne décidée sans recharger (plus rapide mais risque de divergence avec
  le serveur) ; `HttpClient` direct dans le composant (interdit : appels HTTP via `core/`).

## R2 — Filtrage

- **Decision** : filtrage côté client dans un `computed` à partir de signals de filtre (`employee`, `type`, `from`,
  `to`). Logique isolée dans une fonction pure `filterPendingRequests(requests, filters)` dans
  `features/pending-requests/pending-filters.ts` (normalisation casse/accents via
  `normalize('NFD').replace(/\p{Diacritic}/gu, '')`, chevauchement de période par comparaison de chaînes
  `yyyy-MM-dd`).
- **Rationale** : la route `/pending` n'a pas de paramètres (contrat non négociable) et la liste est petite ;
  `computed` = constitution VI ; fonction pure = lisible et testable.
- **Alternatives** : filtres serveur (contrat à modifier) ; Reactive Forms (inutile pour 4 champs, signals suffisent).

## R3 — Champs de filtre

- **Decision** : contrôles natifs liés aux signals par `[value]` / `(input)` / `(change)`, classes globales existantes
  `.toolbar`, `.field`, `.field__label`, `.input`, `.select`, `.btn btn--secondary`. Pas de `FormsModule`.
- **Rationale** : même style que le sélecteur « Connecté en tant que » (`app.html`) et que `ConfirmDialog`.

## R4 — Décisions (approbation / refus)

- **Decision** : un seul `ConfirmDialog` partagé piloté par un signal `pendingDecision: { kind: 'approve' | 'reject';
  request: LeaveRequest } | null` et un signal `busy`.
  - Approbation : `tone="default"`, `confirmLabel="Approuver"`, message « {nom} — du {début} au {fin} ({n} jours) ».
  - Refus : `tone="danger"`, `reasonLabel="Motif du refus"`, **`[reasonMinLength]="1"`** — avec la valeur par défaut
    `0`, `ConfirmDialog` autoriserait un motif vide (`reasonLength() >= 0`), ce qui contredirait FR-008.
  - `[busy]="busy()"` désactive les boutons pendant l'envoi (FR-009) ; le dialogue reste ouvert jusqu'à la réponse.
- **Rationale** : réutilisation du composant partagé (FR-013, principe III) ; Échap et clic extérieur déjà gérés.
- **Alternatives** : boutons inline sans confirmation (risque d'erreur de clic) ; deux dialogues distincts
  (duplication).

## R5 — Retours utilisateur et erreurs

- **Decision** : `ToastService.success("Demande de {nom} approuvée." / "… refusée.")` ; en erreur,
  `ToastService.error(err.error?.detail ?? premier message de err.error?.errors ?? "La décision n'a pas pu être
  enregistrée.")` (corps `ApiProblem`), puis `reload()` dans les deux cas (FR-010, FR-011).
- **Rationale** : les erreurs 403/409 portent `detail` ; les 400 portent `errors.reason` (camelCase depuis la partie 1).

## R6 — Changement d'identité pendant un dialogue

- **Decision** : `effect` sur `currentUser.userId()` qui remet `pendingDecision` à `null` (edge case de la spec).

## R7 — Vérification

- **Decision** (révisée à l'implémentation, choix de l'équipe) : TDD sur la logique extraite en fonctions pures —
  `pending-filters.ts` (filtres) et `pending-decision.ts` (messages de confirmation, de succès et d'erreur) —
  avec le lanceur natif `node --test` (Node 22.23 du conteneur `web` retire nativement le typage TypeScript).
  Script `npm run test:unit` ; fichiers `*.spec.ts`, déjà exclus de `ng build` par `tsconfig.app.json`.
  Aucune dépendance ajoutée. Le câblage du composant (rendu, clics, dialogue) est vérifié par `ng build`,
  `lint:styles` et les scénarios du `quickstart.md` dans le navigateur.
- **Contraintes** : les modules testés n'importent que des types depuis `core/` (`import type`), pour rester
  exécutables par Node sans Angular.
- **Alternatives** : Vitest + jsdom via `@angular/build:unit-test` (outillage nouveau, écarté) ; aucun test front
  (pas de TDD possible, écarté).
