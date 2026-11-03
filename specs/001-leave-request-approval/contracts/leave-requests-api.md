# Contract: API des demandes de congé (Partie 1)

**Source** : ENONCE.md, Partie 1 (routes et formats non négociables) et wiki
[Codes d'erreur](https://github.com/abelmabrouk/ai4pdlc-exercice/wiki/Codes-d-erreur) (codes et statuts).
Le front existant (`frontend/src/app/core/leave-request.service.ts` et `api.models.ts`) implémente déjà ce contrat
côté client.

## Conventions communes

- **Utilisateur courant** : en-tête `X-User-Id`. S'il est absent ou invalide, la réponse est
  `401 application/problem+json`, sans `code`.
- **Format JSON** : le JSON est en camelCase, les énumérations sont des chaînes et les dates sont au format
  `yyyy-MM-dd`.
- **Format de succès** : il est le même que `GET /api/leave-requests/{id}` (`LeaveRequestDto`, sans changement).

```json
{
  "id": 9, "employeeId": 3, "employeeName": "Lea Petit", "teamId": 2, "teamName": "Produit",
  "type": "PaidLeave", "startDate": "2027-03-08", "endDate": "2027-03-12", "days": 5,
  "status": "Pending", "comment": "Vacances", "rejectionReason": null,
  "submittedAt": "2026-11-03T14:00:00+00:00", "decidedAt": null, "decidedById": null
}
```

- **Erreur métier** : `application/problem+json`, avec l'extension `code` (ADR-003). Les clients se fondent
  uniquement sur `code`, jamais sur `title` ni sur `detail`.

```json
{
  "type": "https://conges.example/errors/leave-insufficient-balance",
  "title": "Solde de congés insuffisant.",
  "status": 422,
  "detail": "5 jours demandés, 3 jours disponibles.",
  "code": "LEAVE_INSUFFICIENT_BALANCE"
}
```

- **Erreur de forme** : `400 application/problem+json` avec `errors` (par champ), sans `code`.

---

## POST /api/leave-requests — Soumettre une demande

```http
POST /api/leave-requests
X-User-Id: 3
Content-Type: application/json

{ "type": "PaidLeave", "startDate": "2027-03-08", "endDate": "2027-03-12", "comment": "Vacances" }
```

| Champ | Obligatoire | Valeurs |
|---|---|---|
| `type` | oui | `PaidLeave` ou `FamilyEvent` |
| `startDate`, `endDate` | oui | `yyyy-MM-dd` |
| `comment` | non | texte libre |

| Résultat | Statut | `code` |
|---|---|---|
| Demande créée (`status: Pending`), en-tête `Location: /api/leave-requests/{id}` | **201** | — |
| Champ absent, type inconnu, date mal formée | 400 | — (`errors`) |
| `X-User-Id` absent ou invalide | 401 | — |
| Fin < début, début passé, aucun jour décompté | 422 | `LEAVE_INVALID_PERIOD` |
| Congé payé sans les 2 jours ouvrés de prévenance | 422 | `LEAVE_NOTICE_TOO_SHORT` |
| Chevauchement avec une demande `Pending` ou `Approved` | 422 | `LEAVE_OVERLAP` |
| Congé payé au-delà du solde disponible | 422 | `LEAVE_INSUFFICIENT_BALANCE` |

Ordre des contrôles : celui des lignes ci-dessus (research R7).

---

## GET /api/leave-requests/pending — Demandes à décider

```http
GET /api/leave-requests/pending
X-User-Id: 2
```

| Résultat | Statut |
|---|---|
| Tableau des demandes `Pending` dont l'utilisateur courant est le valideur, triées par `startDate` puis par `id`. Le tableau peut être vide. | **200** |
| `X-User-Id` absent ou invalide | 401 |

---

## POST /api/leave-requests/{id}/approve — Approuver

```http
POST /api/leave-requests/{id}/approve
X-User-Id: 2
```

Le corps est ignoré : le front envoie `{}`.

| Résultat | Statut | `code` |
|---|---|---|
| Demande mise à jour (`status: Approved`, `decidedAt`, `decidedById`). Le solde CP est débité si `type = PaidLeave`. | **200** | — |
| `X-User-Id` absent ou invalide | 401 | — |
| Demande inexistante | 404 | `LEAVE_NOT_FOUND` |
| L'utilisateur courant n'est pas le valideur | 403 | `LEAVE_APPROVER_NOT_ALLOWED` |
| La demande n'est plus `Pending` | 409 | `LEAVE_ALREADY_DECIDED` |

---

## POST /api/leave-requests/{id}/reject — Refuser

```http
POST /api/leave-requests/{id}/reject
X-User-Id: 2
Content-Type: application/json

{ "reason": "Période de forte activité" }
```

| Résultat | Statut | `code` |
|---|---|---|
| Demande mise à jour (`status: Rejected`, `rejectionReason`, `decidedAt`, `decidedById`). Le solde n'est pas modifié. | **200** | — |
| `X-User-Id` absent ou invalide | 401 | — |
| Demande inexistante | 404 | `LEAVE_NOT_FOUND` |
| L'utilisateur courant n'est pas le valideur | 403 | `LEAVE_APPROVER_NOT_ALLOWED` |
| La demande n'est plus `Pending` | 409 | `LEAVE_ALREADY_DECIDED` |
| Motif absent, corps vide, ou moins de 10 caractères après suppression des espaces de début et de fin | 422 | `LEAVE_REJECTION_REASON_REQUIRED` |

Ordre des contrôles : celui des lignes ci-dessus (research R7).

---

## Catalogue des codes (fermé)

| Code | Statut | `title` |
|---|---|---|
| `LEAVE_INVALID_PERIOD` | 422 | Période de congé invalide. |
| `LEAVE_NOTICE_TOO_SHORT` | 422 | Délai de prévenance non respecté. |
| `LEAVE_INSUFFICIENT_BALANCE` | 422 | Solde de congés insuffisant. |
| `LEAVE_OVERLAP` | 422 | Chevauchement avec une autre demande. |
| `LEAVE_REJECTION_REASON_REQUIRED` | 422 | Motif de refus requis. |
| `LEAVE_APPROVER_NOT_ALLOWED` | 403 | Utilisateur non habilité à décider de cette demande. |
| `LEAVE_ALREADY_DECIDED` | 409 | Demande déjà décidée. |
| `LEAVE_NOT_FOUND` | 404 | Demande de congé introuvable. |

Les titres sont indicatifs : seul `code` est contractuel.
