# Quickstart — validation de la fonctionnalité

Références : [contrat](contracts/leave-requests-api.md), [modèle](data-model.md).

## Prérequis

```bash
docker compose up -d            # API sur http://localhost:5080
docker compose restart api      # repartir des données de démonstration avant chaque scénario manuel
```

## 1. Tests automatisés (critère de fin, constitution IV)

```bash
docker compose run --rm backend-tests
```

Attendu : tous les tests passent, dont `SubmitLeaveRequestTests`, `PendingLeaveRequestsTests`,
`DecideLeaveRequestTests` et les nouveaux cas de `HolidayCalendarServiceTests`.

## 2. Scénarios manuels (curl)

```bash
API=http://localhost:5080/api
```

| # | Commande | Attendu |
|---|---|---|
| 1 | `curl -si -X POST $API/leave-requests -H 'X-User-Id: 3' -H 'Content-Type: application/json' -d '{"type":"PaidLeave","startDate":"2027-03-08","endDate":"2027-03-12","comment":"Vacances"}'` | 201, `status: Pending`, `days: 5` |
| 2 | même commande une seconde fois | 409 `Overlap` |
| 3 | `curl -s $API/leave-requests/pending -H 'X-User-Id: 2'` | demandes Pending de Lea, Karim, Ines (dont celle du #1) ; pas celle de Hugo |
| 4 | `curl -s $API/leave-requests/pending -H 'X-User-Id: 1'` | uniquement la demande de Hugo (Claire n'a pas encore de demande) |
| 5 | `curl -si -X POST $API/leave-requests/<id #1>/approve -H 'X-User-Id: 6'` | 403 |
| 6 | `curl -si -X POST $API/leave-requests/<id #1>/approve -H 'X-User-Id: 2'` | 200 `Approved` ; `GET $API/employees/3` → `paidLeaveBalance: 25` |
| 7 | rejouer #6 | 409 `NotPending` |
| 8 | `curl -si -X POST $API/leave-requests/2/reject -H 'X-User-Id: 2' -H 'Content-Type: application/json' -d '{"reason":""}'` | 400 `errors.reason` |
| 9 | idem avec `{"reason":"Période de forte activité"}` | 200 `Rejected` ; solde de Karim inchangé (30) |
| 10 | `curl -si -X POST $API/leave-requests -H 'X-User-Id: 5' -H 'Content-Type: application/json' -d '{"type":"PaidLeave","startDate":"2027-03-08","endDate":"2027-03-09"}'` | 409 `InsufficientBalance` (Ines : 3 − 2 en attente = 1) |
| 11 | `curl -si -X POST $API/leave-requests -H 'X-User-Id: 1' -H 'Content-Type: application/json' -d '{"type":"PaidLeave","startDate":"2027-03-08","endDate":"2027-03-12"}'` | 201 `Pending` ; elle apparaît dans `GET $API/leave-requests/pending` avec `X-User-Id: 1` ; `POST …/<id>/approve` avec `X-User-Id: 1` → 200 `Approved`, `decidedById: 1`, solde de Claire → 25 |
| 12 | `curl -si -X POST $API/leave-requests -H 'X-User-Id: 4' -H 'Content-Type: application/json' -d '{"type":"PaidLeave","startDate":"2027-05-06","endDate":"2027-05-07"}'` | 400 `errors.endDate` (aucun jour ouvré : jeudi 6 mai 2027 = Ascension, vendredi 7 = fermeture entreprise) |
| 13 | n'importe quelle commande sans `X-User-Id` | 401 |

## 3. Swagger

http://localhost:5080/swagger : les quatre opérations apparaissent sous `LeaveRequests`.
