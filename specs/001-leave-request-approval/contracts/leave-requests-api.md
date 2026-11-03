# Contrat d'API — Demandes de congé (écriture et décision)

Sources : `ENONCE.md` partie 1 (routes et formats non négociables) ; wiki : ADR-003, « Codes d'erreur »,
« Règles de gestion », « Circuit de validation ».

Conventions : en-tête `X-User-Id` obligatoire (sinon **401**, ADR-005) ; JSON camelCase ; énumérations en chaînes ;
dates `yyyy-MM-dd`. Erreurs en `application/problem+json`. Toute erreur métier porte l'extension `code` du
référentiel et un `type` `https://conges.example/errors/{code-en-kebab-case}` ; les clients ne lisent jamais `title`
ni `detail`. Les erreurs de forme (FluentValidation) restent en **400** avec `errors`, sans `code`.

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

| Cas | Statut | `code` | Règle |
|---|---|---|---|
| Succès | **201** + `Location` | — (`status: "Pending"`) | RG-07 |
| Type inconnu, date mal formée, commentaire > 500 | 400 (`errors`) | — | forme |
| Demandeur sans valideur (Direction) | 403 | `LEAVE_APPROVER_NOT_ALLOWED` | Circuit de validation ⚠️ code réutilisé |
| Fin < début, début passé, aucun jour ouvrable | 422 | `LEAVE_INVALID_PERIOD` | RG-01, RG-02 |
| Congés payés avec moins de 2 jours ouvrés de prévenance | 422 | `LEAVE_NOTICE_TOO_SHORT` | RG-03 (RG-04) |
| Chevauchement avec une demande Pending/Approved | 422 | `LEAVE_OVERLAP` | RG-06 |
| Solde disponible insuffisant (congés payés) | 422 | `LEAVE_INSUFFICIENT_BALANCE` | RG-05 |

Ordre d'évaluation : 401 → 400 → 403 → RG-01 → RG-02 → RG-03 → RG-06 → RG-05.

## GET `/api/leave-requests/pending` — demandes à décider

**200** : demandes `Pending` dont l'utilisateur courant est le valideur, triées par `startDate` puis `id` ; jamais sa
propre demande ; vide s'il n'est valideur de rien.

## POST `/api/leave-requests/{id}/approve` — approuver

Corps : aucun (`{}` accepté).

| Cas | Statut | `code` | Règle |
|---|---|---|---|
| Succès | **200** | — (`status: "Approved"`, solde débité si CP) | RG-12, RG-14 |
| Demande inexistante | 404 | `LEAVE_NOT_FOUND` | — |
| Pas le valideur (y compris sa propre demande) | 403 | `LEAVE_APPROVER_NOT_ALLOWED` | RG-10 |
| Demande non `Pending`, ou décision concurrente | 409 | `LEAVE_ALREADY_DECIDED` | RG-11 |
| Solde insuffisant au moment de la décision | 422 | `LEAVE_INSUFFICIENT_BALANCE` | RG-12 |

## POST `/api/leave-requests/{id}/reject` — refuser

Corps : `{ "reason": string }`

| Cas | Statut | `code` | Règle |
|---|---|---|---|
| Succès | **200** | — (`status: "Rejected"`, motif enregistré sans espaces de bord) | RG-13, RG-14 |
| `reason` > 500 caractères | 400 (`errors.reason`) | — | forme |
| Demande inexistante | 404 | `LEAVE_NOT_FOUND` | — |
| Pas le valideur | 403 | `LEAVE_APPROVER_NOT_ALLOWED` | RG-10 |
| Demande non `Pending` | 409 | `LEAVE_ALREADY_DECIDED` | RG-11 |
| Motif absent ou < 10 caractères (espaces de bord exclus) | 422 | `LEAVE_REJECTION_REASON_REQUIRED` | RG-13 |

Ordre d'évaluation (approve et reject) : 401 → 400 → 404 → 403 → 409 → 422.

`GET /api/leave-requests/{id}` sur une demande inexistante renvoie aussi 404 `LEAVE_NOT_FOUND`.
