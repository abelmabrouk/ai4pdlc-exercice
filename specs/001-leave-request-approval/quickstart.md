# Quickstart — validation de la fonctionnalité

> Révisé le 2026-11-03 (wiki) : codes d'erreur du référentiel, statuts ADR-003, jours ouvrables, prévenance, Direction.

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
| 2 | même commande une seconde fois | 422 `LEAVE_OVERLAP` |
| 3 | `curl -s $API/leave-requests/pending -H 'X-User-Id: 2'` | demandes Pending de Lea, Karim, Ines (dont celle du #1) ; pas celle de Hugo |
| 4 | `curl -s $API/leave-requests/pending -H 'X-User-Id: 1'` | uniquement la demande de Hugo (jamais une demande de Claire) |
| 5 | `curl -si -X POST $API/leave-requests/<id #1>/approve -H 'X-User-Id: 6'` | 403 `LEAVE_APPROVER_NOT_ALLOWED` |
| 6 | `curl -si -X POST $API/leave-requests/<id #1>/approve -H 'X-User-Id: 2'` | 200 `Approved` ; `GET $API/employees/3` → `paidLeaveBalance: 25` |
| 7 | rejouer #6 | 409 `LEAVE_ALREADY_DECIDED` |
| 8 | `curl -si -X POST $API/leave-requests/2/reject -H 'X-User-Id: 2' -H 'Content-Type: application/json' -d '{"reason":""}'` | 422 `LEAVE_REJECTION_REASON_REQUIRED` (idem avec `"Trop tôt"`, 8 caractères) |
| 9 | idem avec `{"reason":"Période de forte activité"}` | 200 `Rejected` ; solde de Karim inchangé (30) |
| 10 | `curl -si -X POST $API/leave-requests -H 'X-User-Id: 5' -H 'Content-Type: application/json' -d '{"type":"PaidLeave","startDate":"2027-03-08","endDate":"2027-03-09"}'` | 422 `LEAVE_INSUFFICIENT_BALANCE` (Ines : 3 − 2 en attente = 1) |
| 11 | `curl -si -X POST $API/leave-requests -H 'X-User-Id: 1' -H 'Content-Type: application/json' -d '{"type":"PaidLeave","startDate":"2027-03-08","endDate":"2027-03-12"}'` | 403 `LEAVE_APPROVER_NOT_ALLOWED` (Direction validée hors application) |
| 12 | `curl -si -X POST $API/leave-requests -H 'X-User-Id: 4' -H 'Content-Type: application/json' -d '{"type":"PaidLeave","startDate":"2027-05-06","endDate":"2027-05-07"}'` | 422 `LEAVE_INVALID_PERIOD` (aucun jour ouvrable : jeudi 6 mai 2027 = Ascension, vendredi 7 = fermeture) |
| 13 | n'importe quelle commande sans `X-User-Id` | 401 |
| 14 | `curl -si -X POST $API/leave-requests -H 'X-User-Id: 4' -H 'Content-Type: application/json' -d '{"type":"PaidLeave","startDate":"2027-03-15","endDate":"2027-03-20"}'` | 201, `days: 6` (le samedi est un jour ouvrable, RG-02) |
| 15 | soumettre des congés payés commençant **demain** (Karim) | 422 `LEAVE_NOTICE_TOO_SHORT` (RG-03) ; même période en `FamilyEvent` → 201 (RG-04) |

## 3. Swagger

http://localhost:5080/swagger : les quatre opérations apparaissent sous `LeaveRequests`.
