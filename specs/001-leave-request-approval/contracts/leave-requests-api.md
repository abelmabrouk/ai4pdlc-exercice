# Contrat d'API — Demandes de congé (écriture et décision)

Source : `ENONCE.md`, partie 1 (routes et formats non négociables). Ce document précise les réponses d'erreur.

Conventions : en-tête `X-User-Id` obligatoire (sinon **401**) ; JSON camelCase ; énumérations en chaînes ;
dates `yyyy-MM-dd`. Les erreurs sont des ProblemDetails (RFC 7807) ; les erreurs métier portent une extension
`code`.

## Ressource `LeaveRequest` (inchangée — identique à `GET /api/leave-requests/{id}`)

```json
{
  "id": 9, "employeeId": 3, "employeeName": "Lea Petit", "teamId": 2, "teamName": "Produit",
  "type": "PaidLeave", "startDate": "2027-03-08", "endDate": "2027-03-12", "days": 5,
  "status": "Pending", "comment": "Vacances", "rejectionReason": null,
  "submittedAt": "2026-11-03T09:30:00+00:00", "decidedAt": null, "decidedById": null
}
```

## POST `/api/leave-requests` — soumettre

Corps : `{ "type": "PaidLeave" | "FamilyEvent", "startDate": "yyyy-MM-dd", "endDate": "yyyy-MM-dd", "comment"?: string }`

| Cas | Statut | Détail |
|---|---|---|
| Succès | **201 Created** | `Location: /api/leave-requests/{id}`, corps = demande, `status: "Pending"` |
| Type inconnu, date mal formée | 400 | ValidationProblemDetails |
| `endDate < startDate`, `startDate` passée, aucun jour ouvré, commentaire > 500 | 400 | ValidationProblemDetails (`errors.startDate` / `errors.endDate` / `errors.comment`) |
| Chevauchement avec une demande Pending/Approved | 409 | `code: "Overlap"` |
| Solde disponible insuffisant (PaidLeave) | 409 | `code: "InsufficientBalance"` |

## GET `/api/leave-requests/pending` — demandes à décider

| Cas | Statut | Détail |
|---|---|---|
| Succès | **200 OK** | tableau de demandes `Pending` que l'utilisateur courant peut décider, triées par `startDate` puis `id` ; vide si aucune |

La route `pending` doit être déclarée avant ou distinctement de `{id:int}` (la contrainte `int` suffit à éviter
le conflit).

## POST `/api/leave-requests/{id}/approve` — approuver

Corps : aucun (le front envoie `{}`, qui doit être accepté).

| Cas | Statut | Détail |
|---|---|---|
| Succès | **200 OK** | demande avec `status: "Approved"`, `decidedAt`, `decidedById` ; solde débité si PaidLeave |
| Demande inexistante | 404 | |
| Utilisateur non habilité ou propre demande (sauf manager de l'équipe racine, FR-018) | 403 | |
| Demande non `Pending` | 409 | `code: "NotPending"` |
| Solde insuffisant au moment de la décision | 409 | `code: "InsufficientBalance"` |
| Décision concurrente | 409 | `code: "ConcurrentDecision"` |

## POST `/api/leave-requests/{id}/reject` — refuser

Corps : `{ "reason": string }`

| Cas | Statut | Détail |
|---|---|---|
| Succès | **200 OK** | demande avec `status: "Rejected"`, `rejectionReason`, `decidedAt`, `decidedById` |
| `reason` absent, vide ou blanc, > 500 caractères | 400 | ValidationProblemDetails (`errors.reason`) |
| Demande inexistante | 404 | |
| Non habilité / propre demande | 403 | |
| Demande non `Pending` / concurrente | 409 | `code: "NotPending"` / `"ConcurrentDecision"` |

**Ordre d'évaluation** (approve et reject) : 401 → 400 (corps) → 404 → 403 → 409.
