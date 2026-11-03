# Contrat d'interface — Écran « Demandes en attente »

## Dépendance API (inchangée, partie 1)

Via `LeaveRequestService` (`frontend/src/app/core/leave-request.service.ts`) :
`pending()` → `GET /api/leave-requests/pending` ; `approve(id)` → `POST …/{id}/approve` ;
`reject(id, reason)` → `POST …/{id}/reject`. Erreurs : `ApiProblem` (`detail`, `code`, `errors.reason`).
Voir `specs/001-leave-request-approval/contracts/leave-requests-api.md`.

## Route

`/manager/demandes` (déjà déclarée dans `app.routes.ts`, titre « Demandes en attente », lien `nav-pending` visible
pour les managers).

## Sélecteurs de test (`data-testid`)

| testid | Élément |
|---|---|
| `pending-filter-employee` | champ texte « Collaborateur » |
| `pending-filter-type` | liste « Type » (valeurs `''`, `PaidLeave`, `FamilyEvent`) |
| `pending-filter-from` | date « Du » |
| `pending-filter-to` | date « Au » |
| `pending-filters-clear` | bouton « Effacer les filtres » (visible si au moins un filtre) |
| `pending-row-{id}` | ligne du tableau (via `rowTestId` de `DataTable`) |
| `approve-{id}` | bouton « Approuver » d'une ligne |
| `reject-{id}` | bouton « Refuser » d'une ligne |
| `pending-empty` | panneau « Aucune demande en attente » |
| `pending-no-match` | panneau « Aucune demande ne correspond aux filtres » |
| `pending-error` | panneau d'erreur de chargement (avec « Réessayer ») |
| `confirm-dialog`, `confirm-dialog-reason`, `confirm-dialog-confirm`, `confirm-dialog-cancel` | existants (`ConfirmDialog`) |
| `toast` (`data-kind` = `success` / `error`) | existant (`Toast`) |

## Colonnes du tableau

| key | En-tête | Contenu |
|---|---|---|
| `employee` | Collaborateur | `employeeName` |
| `team` | Équipe | `teamName` |
| `type` | Type | `LEAVE_TYPE_LABELS[type]` |
| `period` | Période | « du dd/MM/yyyy au dd/MM/yyyy » (template `appCell`) |
| `days` | Jours | `days`, aligné à droite |
| `comment` | Commentaire | `comment` (vide si `null`) |
| `actions` | Actions | boutons Approuver (`btn btn--primary btn--sm`) / Refuser (`btn btn--danger btn--sm`), `aria-label` « Approuver la demande de {nom} » |

## Textes

| Situation | Texte |
|---|---|
| Dialogue approbation | titre « Approuver la demande », message « {nom} — du {début} au {fin} ({n} jour(s)) », bouton « Approuver » |
| Dialogue refus | titre « Refuser la demande », même message, champ « Motif du refus », bouton « Refuser » (danger) |
| Succès | « Demande de {nom} approuvée. » / « Demande de {nom} refusée. » |
| Erreur | `detail` du problème, sinon premier message de `errors`, sinon « La décision n'a pas pu être enregistrée. » |
