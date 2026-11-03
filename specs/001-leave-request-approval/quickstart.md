# Quickstart: valider la fonctionnalité

**Feature**: [spec.md](spec.md) | **Contrat** : [contracts/leave-requests-api.md](contracts/leave-requests-api.md)

## Prérequis

- La stack Docker démarre (`docker compose config -q` ne renvoie pas d'erreur).
- Aucun SDK local n'est nécessaire.

## 1. Tests automatisés (critère principal, SC-001)

```bash
docker compose run --rm backend-tests
```

Résultat attendu : tous les tests passent, y compris les tests existants (`ReadApiTests` et
`HolidayCalendarServiceTests`) et les nouveaux :

| Classe de test | Couvre |
|---|---|
| `LeaveDayCounterTests` | RG-02 (jours ouvrables), RG-03 (jours ouvrés de prévenance) |
| `ApprovalCircuitTests` | RG-10 (algorithme du valideur, absence de valideur) |
| `SubmitLeaveRequestTests` | US1 : RG-01 à RG-07, erreurs 400 et 401 |
| `ApproveLeaveRequestTests` | US2 : RG-10 à RG-12, RG-14 |
| `RejectLeaveRequestTests` | US3 : RG-10, RG-11, RG-13, RG-14 |
| `PendingLeaveRequestsTests` | US4 : RG-10, RG-11 |

## 2. Parcours manuel (Swagger ou curl)

```bash
docker compose restart api      # base remise aux données de démonstration
```

Les commandes ci-dessous supposent `API=http://localhost:5080`. Utilisateurs : 2 = Hugo (manager de Produit),
3 = Léa, 4 = Karim, 5 = Inès (solde 3), 1 = Claire (Direction).

| # | Action | Attendu |
|---|---|---|
| 1 | `curl -s -H "X-User-Id: 2" $API/api/leave-requests/pending` | Les demandes de Léa, Karim et Inès, sans celle de Hugo |
| 2 | `curl -s -H "X-User-Id: 1" $API/api/leave-requests/pending` | Uniquement la demande de Hugo |
| 3 | `curl -si -X POST -H "X-User-Id: 3" -H "Content-Type: application/json" -d '{"type":"PaidLeave","startDate":"2027-03-08","endDate":"2027-03-13","comment":"Vacances"}' $API/api/leave-requests` | `201`, `days: 6`, `status: Pending` |
| 4 | Répéter l'étape 3 | `422`, `code: LEAVE_OVERLAP` |
| 5 | `curl -si -X POST -H "X-User-Id: 5" -H "Content-Type: application/json" -d '{"type":"PaidLeave","startDate":"2027-06-07","endDate":"2027-06-08"}' $API/api/leave-requests` | `422`, `code: LEAVE_INSUFFICIENT_BALANCE` (solde 3, dont 2 jours déjà en attente) |
| 6 | Approuver la demande créée à l'étape 3 avec `X-User-Id: 2` : `POST $API/api/leave-requests/{id}/approve` | `200`, `status: Approved`. Puis `GET $API/api/employees/3` renvoie `paidLeaveBalance: 24`. |
| 7 | Rejouer l'étape 6 | `409`, `code: LEAVE_ALREADY_DECIDED` |
| 8 | Refuser une demande de Karim avec `X-User-Id: 6` (Sophie) | `403`, `code: LEAVE_APPROVER_NOT_ALLOWED` |
| 9 | Refuser la même demande avec `X-User-Id: 2` et `{"reason":"  Non merci  "}` | `422`, `code: LEAVE_REJECTION_REASON_REQUIRED` |
| 10 | Refuser la même demande avec `X-User-Id: 2` et `{"reason":"Période de forte activité"}` | `200`, `status: Rejected`, solde de Karim inchangé |
| 11 | `curl -si -X POST $API/api/leave-requests/9999/approve -H "X-User-Id: 2"` | `404`, `code: LEAVE_NOT_FOUND` |

Les scénarios qui dépendent de la date du jour (délai de prévenance RG-03, début dans le passé) sont validés par
les tests automatisés, qui fixent l'horloge.

## 3. Front (non-régression)

```bash
docker compose exec web npx ng build
```

Le build doit passer, sans modification attendue du front pour cette partie (SC-006).
