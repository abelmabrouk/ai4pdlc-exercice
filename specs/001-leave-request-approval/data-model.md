# Data Model: Soumission et validation des demandes de congé

**Feature**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md) | **Date**: 2026-11-03

Les entités existent déjà dans `Conges.Domain`. **Aucune propriété ni colonne n'est ajoutée.** La seule
modification du modèle EF Core est le jeton de concurrence sur `LeaveRequest.Status` (voir research R6). Il n'y a
pas de migration (ADR-002).

## Entités existantes utilisées

### LeaveRequest (`Conges.Domain/LeaveRequests/LeaveRequest.cs`)

| Champ | Type | Renseigné par | Règle |
|---|---|---|---|
| `Id` | int | Base | — |
| `EmployeeId` | int | Soumission, à partir de l'utilisateur courant (`ICurrentUser`) | FR-001 |
| `Type` | `LeaveType` (`PaidLeave`, `FamilyEvent`) | Soumission | FR-001 |
| `StartDate`, `EndDate` | `DateOnly` | Soumission | RG-01 : `StartDate ≤ EndDate`, `StartDate ≥ aujourd'hui` |
| `Days` | int | Soumission, calculé par `LeaveDayCounter.CountLeaveDays` | RG-02 : ≥ 1, en jours ouvrables |
| `Status` | `LeaveStatus` | `Pending` à la soumission, puis décision | RG-07, RG-11. **Jeton de concurrence** |
| `Comment` | string? | Soumission (libre) | — |
| `RejectionReason` | string? | Refus (valeur après `Trim()`) | RG-13 : ≥ 10 caractères |
| `SubmittedAt` | `DateTimeOffset` | `TimeProvider.GetUtcNow()` à la soumission | RG-07 |
| `DecidedAt` | `DateTimeOffset?` | `TimeProvider.GetUtcNow()` à la décision | RG-14 |
| `DecidedById` | int? | Utilisateur courant à la décision | RG-14 |

### Employee (`Conges.Domain/Organization/Employee.cs`)

| Champ | Usage |
|---|---|
| `PaidLeaveBalance` | **Solde CP**, en jours ouvrables. Il est lu à la soumission (solde disponible) et débité à l'approbation d'un `PaidLeave` (RG-12). |
| `TeamId` / `Team` | Ils servent à déterminer le valideur. |

### Team (`Conges.Domain/Organization/Team.cs`)

| Champ | Usage |
|---|---|
| `ManagerId` | N+1 des membres de l'équipe. Il est lui-même membre de l'équipe. |
| `ParentTeamId` / `ParentTeam` | Le manager de l'équipe parente est le valideur des demandes du manager de l'équipe. |

## Valeurs calculées (non stockées)

| Valeur | Définition | Règle |
|---|---|---|
| **Solde disponible** | `Employee.PaidLeaveBalance − Σ Days` des autres demandes `PaidLeave` du collaborateur au statut `Pending` | RG-05 |
| **Valideur** | `ApprovalCircuit.ApproverOf(requester)` : si `Team.ManagerId == requester.Id`, alors `Team.ParentTeam?.ManagerId`, sinon `Team.ManagerId`. `null` signifie qu'il n'y a pas de valideur. | RG-10, FR-011 |
| **Prévenance** | `LeaveDayCounter.CountWorkingDaysBetween(aujourd'hui, StartDate)`, soit les jours ouvrés strictement entre les deux dates, qui doivent être au moins 2 pour un `PaidLeave` | RG-03, RG-04 |

## Transitions d'état

```text
            soumission (RG-01..07)
                   │
                   ▼
              ┌─────────┐   approve (RG-10, RG-11, RG-12, RG-14)   ┌──────────┐
              │ Pending │ ───────────────────────────────────────▶ │ Approved │  (PaidLeave : solde −= Days)
              └─────────┘                                          └──────────┘
                   │
                   │ reject (RG-10, RG-11, RG-13, RG-14)
                   ▼
              ┌──────────┐
              │ Rejected │  (solde inchangé)
              └──────────┘
```

`Approved` et `Rejected` sont terminaux : toute nouvelle décision donne `LEAVE_ALREADY_DECIDED` (409).

## Invariants

- **I1** *(SC-002)* : `PaidLeaveBalance` = solde initial − Σ `Days` des `PaidLeave` approuvés.
- **I2** *(SC-003)* : Σ `Days` des `PaidLeave` en attente ≤ `PaidLeaveBalance` ≤ solde initial, et donc
  `PaidLeaveBalance ≥ 0`. RG-05 garantit cet invariant à la soumission, et l'approbation le conserve.
- **I3** *(RG-06)* : les demandes `Pending` ou `Approved` d'un même collaborateur ne se chevauchent pas deux à deux.

## Nouveaux types applicatifs (DTOs et erreurs)

Ce ne sont pas des entités : ils sont listés ici pour donner une vue d'ensemble.

| Type | Couche | Rôle |
|---|---|---|
| `SubmitLeaveRequest(LeaveType? Type, DateOnly? StartDate, DateOnly? EndDate, string? Comment)` | Application | Corps de la soumission. Ses champs sont nullables pour que l'absence d'un champ donne un 400 explicite (R2). |
| `RejectLeaveRequest(string? Reason)` | Application | Corps du refus. |
| `LeaveRequestDto` | Application (**existant**) | Réponse de toutes les opérations, sans changement. |
| `ErrorKind`, `LeaveError`, `LeaveErrors`, `BusinessRuleException` | Application/Common | Erreurs métier porteuses d'un code (R1). |
