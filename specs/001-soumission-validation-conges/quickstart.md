# Quickstart — Valider la fonctionnalité

Ce guide permet de vérifier de bout en bout que la soumission et la validation fonctionnent. Les formats et les
codes sont décrits dans [contracts/leave-requests-api.md](contracts/leave-requests-api.md), et les règles dans
[spec.md](spec.md).

## Prérequis

- Docker. Aucun SDK .NET n'est nécessaire.
- La stack est démarrée : `docker compose up -d` (API sur http://localhost:5080, Swagger sur `/swagger`).
- **Base propre** : `docker compose restart api` recrée la base et les données de démonstration. Les demandes
  seedées sont datées par rapport au jour courant ; les exemples ci-dessous utilisent des dates de mars 2027 qui
  ne les chevauchent pas.

## 1. Tests automatisés (critère SC-001)

```bash
docker compose run --rm backend-tests
```

Résultat attendu : tous les tests passent, y compris les tests existants (`ReadApiTests`,
`HolidayCalendarServiceTests`). Les tests de la fonctionnalité fixent l'horloge (par exemple au lundi
2027-03-01) : leur résultat ne dépend pas du jour d'exécution.

## 2. Parcours manuel

```bash
API=http://localhost:5080/api/leave-requests
```

### Soumettre (Lea Petit, id 3)

```bash
curl -s -X POST $API -H 'X-User-Id: 3' -H 'Content-Type: application/json' \
  -d '{"type":"PaidLeave","startDate":"2027-03-08","endDate":"2027-03-12","comment":"Vacances"}'
```

Attendu : `201`, `status: "Pending"`, `days: 5`. Notez l'`id` renvoyé (noté `<ID>` ci-dessous).

### La demande apparaît chez son valideur (Hugo Bernard, id 2), pas chez Claire Martin (id 1)

```bash
curl -s $API/pending -H 'X-User-Id: 2'   # contient <ID>
curl -s $API/pending -H 'X-User-Id: 1'   # ne contient pas <ID> (Claire est N+2 de Lea)
```

### Approuver, puis vérifier le solde

```bash
curl -s -X POST $API/<ID>/approve -H 'X-User-Id: 2'                  # 200, status "Approved"
curl -s http://localhost:5080/api/employees/3                       # paidLeaveBalance : 30 → 25
curl -s -o /dev/null -w '%{http_code}\n' -X POST $API/<ID>/approve -H 'X-User-Id: 2'   # 409
```

### Refuser avec un motif trop court, puis valide (demande seedée de Karim Benali)

```bash
curl -s $API/pending -H 'X-User-Id: 2'   # repérer l'id de la demande de Karim Benali (<KID>)
curl -s -X POST $API/<KID>/reject -H 'X-User-Id: 2' -H 'Content-Type: application/json' \
  -d '{"reason":"Trop tôt"}'                                         # 422, code LEAVE_REJECTION_REASON_REQUIRED
curl -s -X POST $API/<KID>/reject -H 'X-User-Id: 2' -H 'Content-Type: application/json' \
  -d '{"reason":"Période de forte activité"}'                        # 200, status "Rejected"
```

### Quelques refus métier à la soumission

| Requête (`X-User-Id: 3` sauf mention) | Attendu |
|---|---|
| `{"type":"PaidLeave","startDate":"2027-03-12","endDate":"2027-03-08"}` | 422 `LEAVE_INVALID_PERIOD` (et non 400) |
| `{"type":"PaidLeave","startDate":"2027-03-14","endDate":"2027-03-14"}` (un dimanche) | 422 `LEAVE_INVALID_PERIOD` |
| `{"type":"PaidLeave","startDate":"2027-05-17","endDate":"2027-05-17"}` (lundi de Pentecôte) | 201, `days: 1` |
| `{"type":"PaidLeave","startDate":"2027-05-03","endDate":"2027-05-08"}` | 201, `days: 3` |
| Avec `X-User-Id: 5` (Ines Moreau, solde disponible 1) : `{"type":"PaidLeave","startDate":"2027-03-08","endDate":"2027-03-09"}` | 422 `LEAVE_INSUFFICIENT_BALANCE` |
| Une demande dont le début est aujourd'hui, en `PaidLeave` | 422 `LEAVE_NOTICE_TOO_SHORT` |
| La même, en `FamilyEvent` | 201 |
| Sans `X-User-Id`, ou avec `X-User-Id: 999` | 401 |
| `{"type":"Holiday","startDate":"2027-03-08","endDate":"2027-03-12"}` | 400, avec `errors` et sans `code` |

### Habilitation

```bash
curl -s $API/pending -H 'X-User-Id: 2'   # repérer la demande de Hugo lui-même : elle n'y est PAS
curl -s $API/pending -H 'X-User-Id: 1'   # elle est chez Claire Martin (<HID>)
curl -s -o /dev/null -w '%{http_code}\n' -X POST $API/<HID>/approve -H 'X-User-Id: 2'  # 403 (sa propre demande)
curl -s -o /dev/null -w '%{http_code}\n' -X POST $API/9999/approve -H 'X-User-Id: 2'   # 404
```

## 3. Vérification de conformité (revue)

- Aucun calcul de jour férié hors de `HolidayCalendarService` (ADR-004) :
  `grep -rniE "easter|p[aâ]ques|pentec" backend/src --include=*.cs` ne doit trouver que
  `Conges.Infrastructure/Calendar`.
- Aucun accès au `DbContext` depuis un controller ou un filtre (principe I de la constitution).
- Aucune règle de gestion dans un validateur FluentValidation (ADR-003) : seuls les contrôles de présence et de
  format y figurent.
