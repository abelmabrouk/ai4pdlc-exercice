# Contrat d'interface — Écran « Demandes en attente »

> Révisé le 2026-11-03 : aligné sur le wiki (Maquette, Composants, Interactions, Messages, Conventions data-testid).

## Dépendance API (partie 1, inchangée côté front)

`LeaveRequestService` : `pending()`, `approve(id)`, `reject(id, reason)`. Les erreurs portent `code` (ADR-003) ; le
libellé affiché vient de `core/api-errors.ts` (`errorMessage`), jamais de `title` ni `detail`.

## Route

`/manager/demandes` (existante), lien `nav-pending` visible pour les managers.

## data-testid (page « Conventions data-testid »)

| Élément | `data-testid` |
|---|---|
| Conteneur de l'écran | `pending-requests-page` |
| Ligne | `leave-row-{id}` (via `rowTestId`) |
| Bouton Approuver / Refuser d'une ligne | `btn-approve-{id}` / `btn-reject-{id}` |
| Filtre type (`""`, `PaidLeave`, `FamilyEvent`) | `filter-type` |
| Recherche collaborateur | `filter-search` |
| États | `pending-loading`, `pending-empty`, `pending-no-match`, `pending-error` |
| Bouton Réessayer | `btn-retry` |
| Composants partagés (non redéfinis) | `data-table`, `status-badge`, `confirm-dialog*`, `toast` |

## Colonnes

Collaborateur · Équipe · Type (`LEAVE_TYPE_LABELS`) · Période (« du jj/mm/aaaa au jj/mm/aaaa ») · Jours (à droite) ·
Statut (`app-status-badge`) · Actions (`.btn--primary .btn--sm` « Approuver », puis `.btn--danger .btn--sm` « Refuser »).

## Interactions

| Action | Comportement |
|---|---|
| Approuver | Un clic, sans confirmation ; boutons de la ligne désactivés pendant l'appel ; succès → toast « Demande approuvée. » + ligne retirée ; échec → toast du libellé du code + rechargement |
| Refuser | `app-confirm-dialog` : titre « Refuser la demande », message « Demande de {collaborateur} du {début} au {fin}. », `reasonLabel` « Motif du refus », `[reasonMinLength]="10"`, `tone="danger"`, bouton « Refuser », `busy` pendant l'appel ; fermeture après la réponse ; succès → toast « Demande refusée. » + ligne retirée ; échec → toast + rechargement |
| Filtres | `.toolbar` : « Type » puis « Collaborateur » (placeholder « Rechercher un collaborateur ») ; immédiats, insensibles à la casse, conservés après décision |

## États (un seul à la fois)

| État | Visuel | Libellé |
|---|---|---|
| Chargement | `.skeleton` + 3 `.skeleton__row` | — |
| Vide | `.state-panel`, icône ✅ | Aucune demande en attente. |
| Aucun résultat | `.state-panel`, icône 🔍 | Aucune demande ne correspond aux filtres. |
| Erreur | `.state-panel.state-panel--error` + « Réessayer » (`.btn--secondary`) | Impossible de charger les demandes en attente. |
| Données | `app-data-table` | — |
