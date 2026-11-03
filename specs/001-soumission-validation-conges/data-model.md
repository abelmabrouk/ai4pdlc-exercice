# Data Model — Soumission et validation des demandes de congé

**Aucun changement de schéma.** Les entités existantes (`backend/src/Conges.Domain`) portent déjà tous les champs
nécessaires. La seule modification du modèle EF Core est une configuration : `LeaveRequest.Status` devient un
jeton de concurrence (voir [research.md](research.md), R6). Elle n'ajoute aucune colonne.

## Entités existantes

### LeaveRequest (`Conges.Domain/LeaveRequests/LeaveRequest.cs`)

| Champ | Type | Rôle dans la fonctionnalité | Règle |
|---|---|---|---|
| `Id` | `int` | Identifiant | — |
| `EmployeeId` | `int` | Demandeur, c'est-à-dire l'utilisateur courant à la soumission | — |
| `Type` | `LeaveType` | `PaidLeave` ou `FamilyEvent` | — |
| `StartDate`, `EndDate` | `DateOnly` | Période, bornes incluses ; `StartDate ≤ EndDate` ; `StartDate ≥ aujourd'hui` | RG-01 |
| `Days` | `int` | Jours ouvrables décomptés, calculés à la soumission ; toujours `≥ 1` | RG-02 |
| `Status` | `LeaveStatus` | `Pending` à la création, puis `Approved` ou `Rejected` ; **jeton de concurrence** | RG-07, RG-11 |
| `Comment` | `string?` | Commentaire facultatif de soumission | — |
| `RejectionReason` | `string?` | Motif réduit (`Trim`), d'au moins 10 caractères ; renseigné uniquement si `Rejected` | RG-13 |
| `SubmittedAt` | `DateTimeOffset` | Instant de la requête de soumission (`TimeProvider`) | RG-07 |
| `DecidedAt` | `DateTimeOffset?` | Instant de la décision | RG-14 |
| `DecidedById` | `int?` | Auteur de la décision, qui est le valideur | RG-14 |

### Employee (`Conges.Domain/Organization/Employee.cs`)

| Champ | Rôle dans la fonctionnalité | Règle |
|---|---|---|
| `TeamId` / `Team` | Détermine le valideur | Circuit de validation |
| `PaidLeaveBalance` | Solde CP en jours ouvrables. Il est débité **à l'approbation** d'une demande `PaidLeave`, jamais à la soumission ni au refus. | RG-05, RG-12, RG-13 |

### Team (`Conges.Domain/Organization/Team.cs`)

| Champ | Rôle dans la fonctionnalité |
|---|---|
| `ManagerId` | Valideur des membres de l'équipe, sauf de lui-même |
| `ParentTeamId` / `ParentTeam` | Le manager de l'équipe parente valide le manager de l'équipe. Sans équipe parente, il n'y a pas de valideur. |

## Valeurs dérivées (non stockées)

| Valeur | Définition | Où |
|---|---|---|
| **Jours décomptés** | Nombre de jours du lundi au samedi dans `[StartDate, EndDate]`, hors jours renvoyés par `IHolidayCalendarService` | `LeaveDayCounter.CountLeaveDays` |
| **Jours de prévenance** | Nombre de jours du lundi au vendredi strictement entre la date du jour et `StartDate`, hors jours du calendrier | `LeaveDayCounter.CountNoticeDays` |
| **Solde disponible** | `PaidLeaveBalance` moins la somme des `Days` des demandes `PaidLeave` du collaborateur au statut `Pending` | Service de soumission |
| **Valideur** | Le manager de l'équipe du demandeur ; si le demandeur est ce manager, le manager de l'équipe parente ; s'il n'y a pas d'équipe parente, `null` | `ApprovalCircuit.ApproverIdOf` (`Conges.Application/LeaveRequests`) |

## Cycle de vie d'une demande

```text
              soumission valide (RG-01 à RG-07)
                         │
                         ▼
                    ┌─────────┐
                    │ Pending │
                    └─────────┘
          approve │             │ reject (motif ≥ 10 car.)
   (valideur seul,│             │ (valideur seul, RG-10)
          RG-10)  ▼             ▼
            ┌──────────┐   ┌──────────┐
            │ Approved │   │ Rejected │
            └──────────┘   └──────────┘
        solde CP − Days      solde inchangé
        (PaidLeave seul.)
```

- `Approved` et `Rejected` sont **terminaux**. Toute décision sur un statut autre que `Pending` donne
  `LEAVE_ALREADY_DECIDED` (RG-11).
- **Chevauchement (RG-06).** Seules les demandes `Pending` et `Approved` bloquent une nouvelle demande. Les
  demandes `Rejected` sont ignorées.

## Objets d'échange (DTO)

| DTO | Couche | Champs | Remarque |
|---|---|---|---|
| `SubmitLeaveRequest` (nouveau) | Application | `LeaveType? Type`, `DateOnly? StartDate`, `DateOnly? EndDate`, `string? Comment` | Champs nullables pour détecter l'absence. Validateur FluentValidation de **forme** uniquement. |
| `RejectLeaveRequest` (nouveau) | Application | `string? Reason` | Aucun validateur de forme : l'absence du motif relève de RG-13 (422). |
| `LeaveRequestDto` (existant) | Application | inchangé | Réponse de toutes les opérations, via `LeaveRequest.ToDto()`. |

## Erreurs

| Code | `BusinessErrorKind` → statut | Fabrique |
|---|---|---|
| `LEAVE_INVALID_PERIOD` | `RuleViolation` → 422 | `LeaveErrors.InvalidPeriod()` |
| `LEAVE_NOTICE_TOO_SHORT` | `RuleViolation` → 422 | `LeaveErrors.NoticeTooShort()` |
| `LEAVE_INSUFFICIENT_BALANCE` | `RuleViolation` → 422 | `LeaveErrors.InsufficientBalance(requested, available)` |
| `LEAVE_OVERLAP` | `RuleViolation` → 422 | `LeaveErrors.Overlap()` |
| `LEAVE_REJECTION_REASON_REQUIRED` | `RuleViolation` → 422 | `LeaveErrors.RejectionReasonRequired()` |
| `LEAVE_APPROVER_NOT_ALLOWED` | `Forbidden` → 403 | `LeaveErrors.ApproverNotAllowed()` |
| `LEAVE_ALREADY_DECIDED` | `Conflict` → 409 | `LeaveErrors.AlreadyDecided()` |
| `LEAVE_NOT_FOUND` | `NotFound` → 404 | `LeaveErrors.NotFound(id)` |
