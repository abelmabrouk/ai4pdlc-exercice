# Contrat d'API — Demandes de congé

Contrat imposé par `ENONCE.md` (partie 1) : les routes et les formats ne sont pas négociables. Les erreurs suivent
l'ADR-003 et le référentiel [Codes d'erreur](../../../docs/wiki/Codes-d-erreur.md).

## Conventions communes

- **Identification** : en-tête `X-User-Id: <id du collaborateur>` (ADR-005). Il est **obligatoire** sur les quatre
  opérations ci-dessous.
- **Format** : JSON en camelCase, énumérations en chaînes (`PaidLeave`, `FamilyEvent` ; `Pending`, `Approved`,
  `Rejected`), dates au format `yyyy-MM-dd`, horodatages en ISO 8601 avec décalage.
- **Erreurs** : `application/problem+json`. Une erreur métier porte l'extension `code`. Les clients se fondent
  **uniquement** sur `code`, jamais sur `title` ni sur `detail`.
- **Ordre des vérifications (FR-024)** : seule la première erreur rencontrée est renvoyée.
  - Soumission : 401, puis 400, puis RG-01, RG-02, RG-03, RG-05 et RG-06.
  - Décision : 401, puis 400 (refus uniquement : JSON invalide), puis 404, 403, 409, puis 422 (motif de refus).

## Représentation d'une demande (`LeaveRequest`)

Elle est identique à `GET /api/leave-requests/{id}`, qui existe déjà.

```json
{
  "id": 9,
  "employeeId": 3,
  "employeeName": "Lea Petit",
  "teamId": 2,
  "teamName": "Produit",
  "type": "PaidLeave",
  "startDate": "2027-03-08",
  "endDate": "2027-03-12",
  "days": 5,
  "status": "Pending",
  "comment": "Vacances",
  "rejectionReason": null,
  "submittedAt": "2027-03-01T09:00:00+00:00",
  "decidedAt": null,
  "decidedById": null
}
```

---

## `POST /api/leave-requests` — Soumettre une demande

```http
POST /api/leave-requests
X-User-Id: 3
Content-Type: application/json

{ "type": "PaidLeave", "startDate": "2027-03-08", "endDate": "2027-03-12", "comment": "Vacances" }
```

| Champ | Obligatoire | Contrainte de forme (400 si violée) |
|---|---|---|
| `type` | oui | `PaidLeave` ou `FamilyEvent` |
| `startDate` | oui | Date au format `yyyy-MM-dd` |
| `endDate` | oui | Date au format `yyyy-MM-dd` |
| `comment` | non | Chaîne |

**Succès** : `201 Created`, avec l'en-tête `Location: /api/leave-requests/{id}`. Le corps est la demande créée,
avec `status = "Pending"`, `days` calculé et `submittedAt` égal à l'instant de la requête.

| Statut | `code` | Cas |
|---|---|---|
| 401 | — | `X-User-Id` absent, mal formé ou inconnu |
| 400 | — (avec `errors`) | Champ obligatoire absent, date mal formée, type inconnu, JSON invalide |
| 422 | `LEAVE_INVALID_PERIOD` | `endDate < startDate`, `startDate` dans le passé, ou aucun jour décompté |
| 422 | `LEAVE_NOTICE_TOO_SHORT` | `PaidLeave` : moins de 2 jours ouvrés entre aujourd'hui et `startDate` (exclus) |
| 422 | `LEAVE_INSUFFICIENT_BALANCE` | `PaidLeave` : `days` supérieur au solde disponible |
| 422 | `LEAVE_OVERLAP` | La demande partage un jour avec une demande `Pending` ou `Approved` du même collaborateur |

## `GET /api/leave-requests/pending` — Lister les demandes à décider

```http
GET /api/leave-requests/pending
X-User-Id: 2
```

**Succès** : `200 OK`, avec un tableau de demandes. Il contient les demandes `Pending` dont l'utilisateur courant
est le valideur, triées par `startDate` puis `id` croissants. Un tableau vide `[]` est une réponse normale.

| Statut | `code` | Cas |
|---|---|---|
| 401 | — | `X-User-Id` absent, mal formé ou inconnu |

## `POST /api/leave-requests/{id}/approve` — Approuver

```http
POST /api/leave-requests/1/approve
X-User-Id: 2
```

Pas de corps.

**Succès** : `200 OK`, avec la demande mise à jour : `status = "Approved"`, `decidedAt`, `decidedById`. Pour
`PaidLeave`, le solde CP du collaborateur est débité de `days`.

| Statut | `code` | Cas |
|---|---|---|
| 401 | — | `X-User-Id` absent, mal formé ou inconnu |
| 404 | `LEAVE_NOT_FOUND` | Demande inexistante |
| 403 | `LEAVE_APPROVER_NOT_ALLOWED` | L'utilisateur n'est pas le valideur (y compris sa propre demande, un N+2 ou une demande de la direction générale) |
| 409 | `LEAVE_ALREADY_DECIDED` | Demande déjà `Approved` ou `Rejected`, y compris lors d'une décision concurrente |

## `POST /api/leave-requests/{id}/reject` — Refuser

```http
POST /api/leave-requests/2/reject
X-User-Id: 2
Content-Type: application/json

{ "reason": "Période de forte activité" }
```

| Champ | Obligatoire | Contrainte |
|---|---|---|
| `reason` | oui | Au moins 10 caractères, espaces de début et de fin exclus. **Son absence est une erreur métier (422), pas une erreur de forme.** Un corps absent est traité comme un motif absent. |

**Succès** : `200 OK`, avec la demande mise à jour : `status = "Rejected"`, `rejectionReason` (motif sans espaces
de début et de fin), `decidedAt`, `decidedById`. Le solde n'est pas modifié.

| Statut | `code` | Cas |
|---|---|---|
| 401 | — | `X-User-Id` absent, mal formé ou inconnu |
| 400 | — | JSON invalide |
| 404 | `LEAVE_NOT_FOUND` | Demande inexistante |
| 403 | `LEAVE_APPROVER_NOT_ALLOWED` | L'utilisateur n'est pas le valideur |
| 409 | `LEAVE_ALREADY_DECIDED` | Demande déjà décidée |
| 422 | `LEAVE_REJECTION_REASON_REQUIRED` | Motif absent, vide, ou de moins de 10 caractères après réduction |

## `GET /api/leave-requests/{id}` — Consulter (existant, modifié)

Seule modification : une demande inexistante renvoie `404` **avec** `code = "LEAVE_NOT_FOUND"` (clarification
Q2). L'en-tête `X-User-Id` n'est pas exigé sur cette route, comme aujourd'hui.

## Exemple d'erreur métier

```http
HTTP/1.1 422 Unprocessable Entity
Content-Type: application/problem+json

{
  "type": "https://conges.example/errors/leave-insufficient-balance",
  "title": "Solde de congés insuffisant.",
  "status": 422,
  "detail": "2 jours demandés, 1 jour disponible.",
  "code": "LEAVE_INSUFFICIENT_BALANCE"
}
```
