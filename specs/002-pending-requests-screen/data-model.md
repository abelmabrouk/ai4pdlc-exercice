# Data Model — Écran « Demandes en attente »

Aucun nouveau modèle serveur. Modèles front existants dans `frontend/src/app/core/api.models.ts` :
`LeaveRequest`, `LeaveType`, `ApiProblem`, `LEAVE_TYPE_LABELS`.

## État de l'écran (signals du composant `PendingRequestsPage`)

| Signal | Type | Initial | Rôle |
|---|---|---|---|
| `requests` | `rxResource<LeaveRequest[]>` | — | liste serveur, rechargée quand `userId` change |
| `employeeFilter` | `string` | `''` | texte libre sur `employeeName` |
| `typeFilter` | `LeaveType \| ''` | `''` | `''` = tous les types |
| `fromFilter` | `string` (`yyyy-MM-dd`) | `''` | borne basse facultative |
| `toFilter` | `string` (`yyyy-MM-dd`) | `''` | borne haute facultative |
| `filtered` | `computed<LeaveRequest[]>` | — | `filterPendingRequests(requests.value() ?? [], filters)` |
| `hasFilters` | `computed<boolean>` | — | au moins un filtre non vide |
| `pendingDecision` | `{ kind: 'approve' \| 'reject'; request: LeaveRequest } \| null` | `null` | dialogue ouvert |
| `busy` | `boolean` | `false` | envoi en cours |

## `PendingFilters` (`features/pending-requests/pending-filters.ts`)

```ts
interface PendingFilters { employee: string; type: LeaveType | ''; from: string; to: string }
```

Règles de `filterPendingRequests(requests, filters)` (combinées en ET) :

- `employee` : `normalize(r.employeeName).includes(normalize(filters.employee.trim()))`, où `normalize` met en
  minuscules et retire les accents ; ignoré si vide.
- `type` : `r.type === filters.type` ; ignoré si `''`.
- `from` : `r.endDate >= filters.from` ; ignoré si vide.
- `to` : `r.startDate <= filters.to` ; ignoré si vide. (`from > to` ⇒ aucun résultat, edge case.)
- L'ordre d'entrée (tri serveur par date de début) est conservé.

## Transitions d'affichage

```text
chargement ──► erreur (Réessayer → chargement)
          └──► liste vide (aucune demande à décider)
          └──► liste ──filtres──► aucun résultat (Effacer les filtres → liste)
liste ──Approuver/Refuser──► dialogue ──Annuler/Échap──► liste
                                  └──Confirmer──► busy ──► toast succès|erreur ──► reload ──► liste
```
