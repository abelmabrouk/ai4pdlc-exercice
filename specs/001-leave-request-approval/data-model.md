# Data Model — Soumission et validation des demandes de congé

Aucune nouvelle entité ni colonne : le modèle existant (`Conges.Domain`) couvre déjà la fonctionnalité.
Seule évolution de mapping : `LeaveRequest.Status` devient jeton de concurrence (voir research R5).

## LeaveRequest (existante — `Conges.Domain/LeaveRequests/LeaveRequest.cs`)

| Champ | Type | Règles |
|---|---|---|
| `Id` | int | identifiant |
| `EmployeeId` | int | = utilisateur courant à la soumission (FR-001) |
| `Type` | `LeaveType` (`PaidLeave`, `FamilyEvent`) | obligatoire, valeur connue |
| `StartDate` | DateOnly | ≥ aujourd'hui (FR-004) |
| `EndDate` | DateOnly | ≥ `StartDate` (FR-004) |
| `Days` | int | = `CountWorkingDays(StartDate, EndDate)`, ≥ 1 (FR-003, FR-004) |
| `Status` | `LeaveStatus` | voir transitions ; **jeton de concurrence** |
| `Comment` | string? | ≤ 500 caractères |
| `RejectionReason` | string? | obligatoire et non vide si `Rejected`, ≤ 500 caractères (FR-014) |
| `SubmittedAt` | DateTimeOffset | `TimeProvider.GetUtcNow()` à la soumission (FR-016) |
| `DecidedAt` | DateTimeOffset? | `TimeProvider.GetUtcNow()` à la décision |
| `DecidedById` | int? | décideur ; = demandeur pour le manager de l'équipe racine (FR-018) |

**Unicité temporelle** : pour un même `EmployeeId`, pas deux demandes `Pending`/`Approved` dont les intervalles
`[StartDate, EndDate]` se chevauchent (FR-005). Chevauchement ⇔ `a.StartDate <= b.EndDate && b.StartDate <= a.EndDate`.

### Transitions d'état

```text
  (nouvelle) ── soumission ──► Pending ── approve par l'approbateur ──────────► Approved
                                  │
                                  └────── reject(reason) par l'approbateur ───► Rejected
```

- `Approved` et `Rejected` sont terminaux : toute décision ultérieure → 409 `NotPending` (FR-015).
- Effet de `→ Approved` si `Type = PaidLeave` : `Employee.PaidLeaveBalance -= Days`, refusé si le résultat < 0
  (409 `InsufficientBalance`, FR-012). Aucun effet sur le solde pour `FamilyEvent` (FR-013) ni pour `→ Rejected`.

## Employee (existante)

| Champ | Usage |
|---|---|
| `PaidLeaveBalance` | débité à l'approbation (FR-012) ; solde disponible à la soumission = `PaidLeaveBalance − Σ Days(PaidLeave, Pending)` (FR-006) |
| `TeamId` / `Team` | détermine l'approbateur |

## Team (existante)

| Champ | Usage |
|---|---|
| `ManagerId` | approbateur des membres autres que le manager |
| `ParentTeamId` / `ParentTeam.ManagerId` | approbateur du manager de l'équipe |

### Règle d'habilitation (FR-008, FR-009)

```text
ApproverOf(e) = e.Team.ManagerId              si e.Id ≠ e.Team.ManagerId
              = e.Team.ParentTeam.ManagerId   si e est manager et l'équipe a une équipe parente
              = e.Id                          sinon (manager de l'équipe racine, FR-018)
```

Avec les données de démonstration : Lea/Karim/Ines → Hugo (2) ; Thomas/Yasmine/Nicolas → Sophie (6) ;
Hugo, Sophie → Claire (1) ; Claire → Claire (1).

## Calendrier (service existant `IHolidayCalendarService`)

Nouvelle opération : `CountWorkingDays(from, to)` = nombre de jours du lundi au vendredi dans `[from, to]`,
moins les jours fériés et fermetures renvoyés par `GetHolidays(from, to)` tombant en semaine.
