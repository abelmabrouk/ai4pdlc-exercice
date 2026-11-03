---
description: "Tâches d'implémentation — Soumission et validation des demandes de congé"
---

# Tasks: Soumission et validation des demandes de congé

**Input**: Design documents from `specs/001-leave-request-approval/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/leave-requests-api.md, quickstart.md

**Tests**: OBLIGATOIRES. La constitution (principe IV) l'exige et `ENONCE.md` impose le TDD
(`/superpowers:test-driven-development`). Dans chaque phase, le test est écrit **avant** le code et doit
**échouer** d'abord.

**Organization**: les tâches sont regroupées par user story. L'ordre suit la priorité de la spec : US1 (P1),
US3 (P1), US2 (P2), US4 (P2).

## Format: `[ID] [P?] [Story] Description`

- **[P]** : peut être faite en parallèle (fichier différent, aucune dépendance sur une tâche non terminée).
- **[Story]** : user story concernée (US1 à US4).

## Conventions communes à toutes les tâches

- **Chemins** : back-end uniquement, sous `backend/src/` et `backend/tests/Conges.Tests/`. Le front n'est pas
  modifié.
- **Commande de test** : `docker compose run --rm backend-tests`.
- **Isolation des tests** : les tests d'API qui **modifient** des données créent leur propre
  `CongesApiFactory`, via `using var factory = new CongesApiFactory();` dans le test, pour partir d'une base
  seedée neuve. Les tests en lecture seule peuvent partager `IClassFixture<CongesApiFactory>`. Désérialiser avec
  `Json.Options` (`backend/tests/Conges.Tests/Json.cs`).
- **Données de démonstration** :
  - Claire = 1 (Direction, équipe racine) ; Hugo = 2 (manager Produit) ; Lea = 3, Karim = 4, Ines = 5 (Produit,
    Ines a un solde de 3 et la demande seedée id 7 de 2 jours `Pending`) ; Sophie = 6 (manager Support) ;
    Thomas = 7, Yasmine = 8, Nicolas = 9 (Support).
  - Demandes seedées `Pending` : 1 (Lea), 2 (Karim), 3 (Hugo), 4 (Thomas, `FamilyEvent`), 7 (Ines), 8 (Yasmine).
    Demande 5 = `Approved`, demande 6 = `Rejected`.
- **Dates de test** : fixes, en mars 2027, pour ne pas chevaucher les demandes seedées (relatives à aujourd'hui,
  au plus tard environ huit semaines plus tard). Semaine de référence : lundi 2027-03-08 → vendredi 2027-03-12
  (5 jours ouvrés). Pour une seconde semaine, prendre 2027-03-15 → 2027-03-19.
- **Messages** : en français, comme le code existant.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: vérifier le point de départ (aucune initialisation de projet : la solution existe).

- [x] T001 Lancer `docker compose run --rm backend-tests` et confirmer que la suite existante
  (`backend/tests/Conges.Tests/ReadApiTests.cs`, `HolidayCalendarServiceTests.cs`) passe avant toute modification.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: briques partagées par toutes les user stories : décompte des jours, exceptions métier,
habilitation, concurrence.

**⚠️ CRITICAL**: aucune user story ne commence avant la fin de cette phase.

### Décompte des jours ouvrés (research R1)

- [x] T002 [P] Écrire les tests de `CountWorkingDays` dans
  `backend/tests/Conges.Tests/HolidayCalendarServiceTests.cs` (helper `Calendar(...)` existant) :
  - `2027-03-08`→`2027-03-12` = 5 ;
  - `2027-03-08`→`2027-03-08` = 1 ;
  - `2027-03-13`→`2027-03-14` (samedi-dimanche) = 0 ;
  - `2027-05-03`→`2027-05-09` avec la fermeture `2027-05-07` = 3. L'Ascension (jeudi 6) et la fermeture
    (vendredi 7) sont exclues ; le 8 mai tombe un samedi et ne doit pas être retiré deux fois ;
  - `2027-05-17` (lundi de Pentecôte) = 1 ;
  - `2026-12-28`→`2027-01-04` avec la fermeture `2026-12-31` = 4, car le 1er janvier est exclu ;
  - `to < from` = 0.

  Vérifier que les tests échouent (la méthode n'existe pas encore).
- [x] T003 Ajouter `int CountWorkingDays(DateOnly from, DateOnly to);` à
  `backend/src/Conges.Application/Common/IHolidayCalendarService.cs`, avec la doc XML « Jours du lundi au
  vendredi entre from et to inclus, hors jours fériés et fermetures ; 0 si to < from ».
- [x] T004 Implémenter `CountWorkingDays` dans
  `backend/src/Conges.Infrastructure/Calendar/HolidayCalendarService.cs`. Énumérer les jours de `from` à `to`,
  garder ceux qui ne sont ni samedi ni dimanche et qui ne sont pas dans `GetHolidays(from, to)` (comparer sur un
  `HashSet<DateOnly>`). Rendre T002 vert.

### Exceptions métier et format d'erreur (research R3)

- [x] T005 [P] Ajouter dans `backend/src/Conges.Application/Common/Exceptions.cs` :
  - `public class ForbiddenException(string message) : Exception(message)` ;
  - `public class ConflictException(string code, string message) : Exception(message)`, avec la propriété
    `public string Code { get; } = code;`.

  Ajouter une classe statique `ConflictCodes` avec les constantes `NotPending`, `Overlap`, `InsufficientBalance`
  et `ConcurrentDecision`, dont la valeur est leur nom.
- [x] T006 Mapper ces exceptions dans `backend/src/Conges.Api/Http/GlobalExceptionHandler.cs`, en ajoutant les
  bras au `switch` existant :
  - `ForbiddenException` → `ProblemDetails { Status = 403, Title = "Action non autorisée.", Detail = message }` ;
  - `ConflictException` → `ProblemDetails { Status = 409, Title = "Conflit avec l'état actuel.", Detail = message }`,
    avec `problem.Extensions["code"] = conflict.Code`.

  Dépend de T005.

### Habilitation (research R2, data-model « Règle d'habilitation »)

- [x] T007 [P] Créer `backend/src/Conges.Application/LeaveRequests/LeaveApprovers.cs`, classe `public static`
  avec deux membres.
  - **`public static int? ApproverOf(Employee e)`** : exige `e.Team` et `e.Team.ParentTeam` chargés.
    - Si `e.Id != e.Team.ManagerId`, renvoie `e.Team.ManagerId`.
    - Sinon, si `e.Team.ParentTeam is not null`, renvoie `e.Team.ParentTeam.ManagerId`.
    - Sinon, renvoie `e.Id` : le manager de l'équipe racine décide ses propres demandes (FR-018).
  - **`public static Expression<Func<LeaveRequest, bool>> DecidableBy(int userId)`** : même règle, exprimée en
    prédicat traduisible par EF Core :
    ```text
    r => (r.Employee!.Id != r.Employee.Team!.ManagerId && r.Employee.Team.ManagerId == userId)
      || (r.Employee!.Id == r.Employee.Team!.ManagerId && r.Employee.Team.ParentTeamId != null && r.Employee.Team.ParentTeam!.ManagerId == userId)
      || (r.Employee!.Id == r.Employee.Team!.ManagerId && r.Employee.Team.ParentTeamId == null && r.EmployeeId == userId)
    ```

### Concurrence (research R5)

- [x] T008 [P] Dans `backend/src/Conges.Infrastructure/Persistence/AppDbContext.cs`, bloc
  `modelBuilder.Entity<LeaveRequest>`, ajouter `.IsConcurrencyToken()` à la ligne existante
  `request.Property(r => r.Status).HasConversion<string>().HasMaxLength(30)`. Ne pas créer de migration
  (constitution V).

### Squelette du service de commandes

- [x] T009 Créer `backend/src/Conges.Application/LeaveRequests/LeaveRequestCommands.cs`. Dépend de T005 et T007.
  - Les DTOs d'entrée :
    - `public sealed record SubmitLeaveRequestDto(LeaveType Type, DateOnly StartDate, DateOnly EndDate, string? Comment);`
    - `public sealed record RejectLeaveRequestDto(string? Reason);`
  - L'interface `public interface ILeaveRequestCommands`, avec :
    - `Task<LeaveRequestDto> SubmitAsync(SubmitLeaveRequestDto input, CancellationToken ct);`
    - `Task<LeaveRequestDto> ApproveAsync(int id, CancellationToken ct);`
    - `Task<LeaveRequestDto> RejectAsync(int id, RejectLeaveRequestDto input, CancellationToken ct);`
  - L'implémentation `internal sealed class LeaveRequestCommands(...)`. Elle reçoit `IAppDbContext db`,
    `ICurrentUser currentUser`, `IHolidayCalendarService calendar`, `TimeProvider time`,
    `IValidator<SubmitLeaveRequestDto> submitValidator` et `IValidator<RejectLeaveRequestDto> rejectValidator`.
    Ses méthodes lèvent `NotImplementedException` à ce stade.
  - Un helper privé `Task<LeaveRequestDto> ReloadAsync(int id, CancellationToken ct)` : relit la demande en
    `AsNoTracking` avec `Include(r => r.Employee!).ThenInclude(e => e.Team)` et renvoie `ToDto()`, via le mapping
    existant `LeaveRequestMapping` de `LeaveRequestQueries.cs`.
- [x] T010 Enregistrer `services.AddScoped<ILeaveRequestCommands, LeaveRequestCommands>();` dans
  `backend/src/Conges.Application/DependencyInjection.cs`. Dépend de T009. Les validateurs sont déjà enregistrés
  par `AddValidatorsFromAssembly`, et `TimeProvider` par l'Infrastructure.

**Checkpoint** : `docker compose run --rm backend-tests` passe (T002 vert, aucune régression).

---

## Phase 3: User Story 1 — Soumettre une demande de congé (Priority: P1) 🎯 MVP

**Goal**: `POST /api/leave-requests` crée une demande `Pending` avec le décompte des jours ouvrés et les
contrôles de FR-001 à FR-007.

**Independent Test**: en tant que Lea (3), soumettre des congés payés du 2027-03-08 au 2027-03-12. Réponse
attendue : `201`, `status: Pending`, `days: 5`, et le solde de Lea reste à 30.

### Tests for User Story 1 ⚠️ (écrire d'abord, vérifier qu'ils échouent)

- [x] T011 [US1] Créer `backend/tests/Conges.Tests/SubmitLeaveRequestTests.cs`, avec un test par scénario. Le
  POST va vers `/api/leave-requests` et l'en-tête est posé via `factory.CreateClientAs(id)`.
  1. **Lea (3)**, `PaidLeave`, 2027-03-08→2027-03-12, commentaire « Vacances ». Attendu : `201` ; `Location`
     pointe vers `/api/leave-requests/{id}` ; corps `LeaveRequestDto` avec `Status == Pending`, `Days == 5`,
     `EmployeeId == 3`, `Comment == "Vacances"` et `DecidedById == null`. `GET /api/employees/3` donne
     `PaidLeaveBalance == 30`.
  2. **`endDate < startDate`** : `400`, avec une erreur sur `endDate`.
  3. **`startDate` passée** (2020-01-06) : `400`, avec une erreur sur `startDate`.
  4. **Aucun jour ouvré** (2027-03-13→2027-03-14) : `400`, avec une erreur sur `endDate`.
  5. **Chevauchement** : deux soumissions identiques de Lea. La seconde renvoie `409`, avec le JSON `code`
     valant `"Overlap"`.
  6. **Ines (5)**, `PaidLeave`, 2027-03-08→2027-03-09 (2 jours). Son solde de 3, moins les 2 jours de la demande
     7 en attente, laisse 1 jour disponible. Attendu : `409`, `code == "InsufficientBalance"`.
  7. **Ines (5)**, `FamilyEvent`, 2027-03-08→2027-03-12 : `201`.
  8. **Claire (1)**, `PaidLeave`, 2027-03-08→2027-03-12 : `201`, `Status == Pending` (FR-018, US1 scénario 7).
  9. **Sans en-tête `X-User-Id`** : `401`.
  10. **Commentaire de 501 caractères** : `400`.

### Implementation for User Story 1

- [x] T012 [P] [US1] Créer `backend/src/Conges.Application/LeaveRequests/LeaveRequestCommandValidators.cs` avec
  `internal sealed class SubmitLeaveRequestValidator : AbstractValidator<SubmitLeaveRequestDto>`. Le constructeur
  reçoit `TimeProvider time`.
  - `RuleFor(x => x.Type).IsInEnum()`.
  - `RuleFor(x => x.EndDate).GreaterThanOrEqualTo(x => x.StartDate)`, avec le message « La date de fin doit être
    postérieure ou égale à la date de début. ».
  - `RuleFor(x => x.StartDate).GreaterThanOrEqualTo(_ => DateOnly.FromDateTime(time.GetLocalNow().DateTime))`,
    avec le message « La date de début ne peut pas être dans le passé. ».
  - `RuleFor(x => x.Comment).MaximumLength(500)`.
- [x] T013 [US1] Implémenter `SubmitAsync` dans
  `backend/src/Conges.Application/LeaveRequests/LeaveRequestCommands.cs`. Rendre T011 vert.
  1. `await submitValidator.ValidateAndThrowAsync(input, ct)`.
  2. `var userId = currentUser.EmployeeId;`. Charger l'employé (suivi) avec
     `Include(e => e.Team!).ThenInclude(t => t.ParentTeam)` ; s'il est introuvable,
     `throw new NotFoundException("Employé", userId)`.
  3. `var days = calendar.CountWorkingDays(input.StartDate, input.EndDate);`. Si `days == 0`, lever
     `ValidationException`, avec un `ValidationFailure("EndDate", "La période ne contient aucun jour ouvré.")`.
  4. Chevauchement : `AnyAsync` sur les demandes de `userId` dont le `Status` est `Pending` ou `Approved` et qui
     vérifient `r.StartDate <= input.EndDate && input.StartDate <= r.EndDate`. Si oui,
     `throw new ConflictException(ConflictCodes.Overlap, "La période chevauche une demande existante.")`.
  5. Si `Type == PaidLeave` : calculer
     `pendingDays = SumAsync(Days)` sur les demandes `PaidLeave` `Pending` de `userId`. Si
     `days > employee.PaidLeaveBalance - pendingDays`, lever `ConflictException(ConflictCodes.InsufficientBalance,
     $"Solde insuffisant : {available} jour(s) disponible(s), {days} demandé(s).")`.
  6. Créer `new LeaveRequest { EmployeeId = userId, Type, StartDate, EndDate, Days = days,
     Status = Pending, Comment = input.Comment, SubmittedAt = time.GetUtcNow() }`. L'ajouter, puis
     `SaveChangesAsync`, puis `return await ReloadAsync(request.Id, ct)`.

  Ne jamais modifier `PaidLeaveBalance` ici (FR-007).
- [x] T014 [US1] Dans `backend/src/Conges.Api/Controllers/LeaveRequestsController.cs` :
  - injecter `ILeaveRequestCommands commands` dans le constructeur principal, à côté de `leaveRequests` ;
  - ajouter l'action suivante (controller mince, constitution I) :
    ```csharp
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<LeaveRequestDto>> Submit(SubmitLeaveRequestDto input, CancellationToken ct)
    {
        var created = await commands.SubmitAsync(input, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }
    ```

**Checkpoint** : US1 fonctionne seule, `SubmitLeaveRequestTests` est vert et le scénario #1 du quickstart passe.

---

## Phase 4: User Story 3 — Approuver une demande et mettre à jour le solde (Priority: P1)

**Goal**: `POST /api/leave-requests/{id}/approve` passe une demande `Pending` à `Approved` et débite le solde
pour les congés payés (FR-008, FR-009, FR-011 à FR-013, FR-015, FR-016, FR-018).

**Independent Test**: Hugo (2) approuve la demande seedée 1 de Lea (5 jours). Attendu : `200`, statut
`Approved`, et le solde de Lea passe à 25.

### Tests for User Story 3 ⚠️

- [x] T015 [US3] Créer `backend/tests/Conges.Tests/DecideLeaveRequestTests.cs`, avec une factory neuve par test.
  Le POST va vers `/api/leave-requests/{id}/approve` avec un corps `{}`.
  1. **Hugo (2) approuve la demande 1.** Attendu : `200`, `Status == Approved`, `DecidedById == 2` et
     `DecidedAt` non nul. `GET /api/employees/3` donne `PaidLeaveBalance == 25`.
  2. **Sophie (6) approuve la demande 4** (`FamilyEvent` de Thomas). Attendu : `200` ; le solde de Thomas (7)
     reste à 30.
  3. **Sophie (6) approuve la demande 2** (Karim, Produit) : `403`, et la demande reste `Pending`.
  4. **Hugo (2) approuve la demande 3**, qui est la sienne : `403`.
  5. **Claire (1) approuve la demande 3** (Hugo) : `200`.
  6. **Hugo approuve la demande 1 deux fois.** Le second appel renvoie `409` avec `code == "NotPending"`, et le
     solde de Lea reste à 25.
  7. **Approbation de la demande 5** (déjà `Approved`) par Sophie : `409` `NotPending`.
  8. **Demande inexistante 9999** : `404`.
  9. **Solde devenu insuffisant.** Placer Ines (5) à un solde de 1 via la base : résoudre
     `factory.Services.CreateScope()` puis `AppDbContext`, et mettre `PaidLeaveBalance = 1`. Hugo approuve
     ensuite la demande 7 (2 jours). Attendu : `409` `InsufficientBalance`, avec le solde inchangé à 1.
  10. **Claire (1) soumet** des congés payés du 2027-03-08 au 2027-03-12, puis les **approuve elle-même** :
      `200`, `DecidedById == 1`, et le solde de Claire passe à 25 (US3 scénario 7).
  11. **Sans en-tête** : `401`.

### Implementation for User Story 3

- [x] T016 [US3] Dans `backend/src/Conges.Application/LeaveRequests/LeaveRequestCommands.cs`, ajouter le helper
  privé `Task<LeaveRequest> LoadForDecisionAsync(int id, CancellationToken ct)`. Dépend de T013 (même fichier).
  1. `var userId = currentUser.EmployeeId;` : l'erreur 401 passe en premier.
  2. Charger la demande (suivie) avec
     `Include(r => r.Employee!).ThenInclude(e => e.Team!).ThenInclude(t => t.ParentTeam)` ; si elle est
     introuvable, `throw new NotFoundException("Demande de congé", id)`.
  3. Si `LeaveApprovers.ApproverOf(request.Employee!) != userId`, lever
     `ForbiddenException("Vous n'êtes pas habilité à décider cette demande.")`.
  4. Si `request.Status != LeaveStatus.Pending`, lever
     `ConflictException(ConflictCodes.NotPending, "La demande a déjà été décidée.")`.
  5. Renvoyer la demande.

  Ajouter aussi le helper privé `Task SaveDecisionAsync(CancellationToken ct)` : il appelle
  `db.SaveChangesAsync(ct)` dans un `try`, et attrape `DbUpdateConcurrencyException` pour lever
  `ConflictException(ConflictCodes.ConcurrentDecision, "La demande vient d'être décidée par ailleurs.")`.
- [x] T017 [US3] Implémenter `ApproveAsync` dans le même fichier. Rendre T015 vert.
  1. `var request = await LoadForDecisionAsync(id, ct);`.
  2. Si `request.Type == PaidLeave` :
     - si `request.Employee!.PaidLeaveBalance < request.Days`, lever `ConflictException(ConflictCodes.InsufficientBalance, …)` ;
     - sinon, `request.Employee.PaidLeaveBalance -= request.Days`.
  3. Renseigner `Status = Approved`, `DecidedAt = time.GetUtcNow()` et `DecidedById = currentUser.EmployeeId`.
  4. `await SaveDecisionAsync(ct)` (statut et solde dans le même enregistrement), puis
     `return await ReloadAsync(id, ct)`.
- [x] T018 [US3] Ajouter l'action suivante dans `backend/src/Conges.Api/Controllers/LeaveRequestsController.cs` :
  ```csharp
  [HttpPost("{id:int}/approve")]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  [ProducesResponseType(StatusCodes.Status409Conflict)]
  public Task<LeaveRequestDto> Approve(int id, CancellationToken ct) => commands.ApproveAsync(id, ct);
  ```
  Ne pas déclarer de paramètre de corps : le front envoie `{}`, qui doit être accepté.

**Checkpoint** : US1 et US3 forment le parcours de bout en bout (soumettre, puis approuver avec débit du solde).

---

## Phase 5: User Story 2 — Consulter les demandes à décider (Priority: P2)

**Goal**: `GET /api/leave-requests/pending` renvoie uniquement les demandes `Pending` que l'utilisateur courant
peut décider (FR-010), triées par `startDate` puis `id`.

**Independent Test**: Hugo (2) reçoit les demandes 1, 2 et 7, mais pas la 3 (la sienne) ni les 4 et 8 (Support).

### Tests for User Story 2 ⚠️

- [x] T019 [P] [US2] Créer `backend/tests/Conges.Tests/PendingLeaveRequestsTests.cs`. Utiliser
  `IClassFixture<CongesApiFactory>` pour les tests en lecture seule, et une factory neuve pour le test 5.
  1. **Hugo (2)** : les ids reçus sont exactement {1, 2, 7}.
  2. **Sophie (6)** : exactement {4, 8}.
  3. **Claire (1)** : exactement {3}.
  4. **Lea (3)** : liste vide.
  5. **Claire soumet une demande** (2027-03-08→2027-03-12), puis consulte sa liste : elle contient {3} et sa
     nouvelle demande (US2 scénario 5).
  6. **Statuts** : aucune demande renvoyée n'est `Approved` ni `Rejected`, et la liste est triée par `StartDate`
     puis `Id`.
  7. **Sans en-tête** : `401`.

### Implementation for User Story 2

- [x] T020 [US2] Dans `backend/src/Conges.Application/LeaveRequests/LeaveRequestQueries.cs` :
  - ajouter `Task<IReadOnlyList<LeaveRequestDto>> PendingAsync(CancellationToken ct);` à `ILeaveRequestQueries` ;
  - injecter `ICurrentUser currentUser` dans le constructeur principal de `LeaveRequestQueries` ;
  - implémenter la méthode :
    ```text
    db.LeaveRequests.AsNoTracking()
      .Include(r => r.Employee!).ThenInclude(e => e.Team)
      .Where(r => r.Status == LeaveStatus.Pending)
      .Where(LeaveApprovers.DecidableBy(currentUser.EmployeeId))
      .OrderBy(r => r.StartDate).ThenBy(r => r.Id)
    ```
    puis `ToListAsync` et `Select(r => r.ToDto())`.

  Rendre T019 vert.
- [x] T021 [US2] Ajouter l'action suivante dans `backend/src/Conges.Api/Controllers/LeaveRequestsController.cs` :
  ```csharp
  [HttpGet("pending")]
  public Task<IReadOnlyList<LeaveRequestDto>> Pending(CancellationToken ct) => leaveRequests.PendingAsync(ct);
  ```
  Il n'y a pas de conflit avec `{id:int}`, grâce à la contrainte `int`.

**Checkpoint** : US2 fonctionne seule (lecture), et en combinaison avec US1.

---

## Phase 6: User Story 4 — Refuser une demande avec un motif (Priority: P2)

**Goal**: `POST /api/leave-requests/{id}/reject` passe une demande `Pending` à `Rejected`, avec un motif
obligatoire et sans effet sur le solde (FR-014, FR-015).

**Independent Test**: Hugo (2) refuse la demande 2 de Karim avec le motif « Période de forte activité ».
Attendu : `200`, statut `Rejected`, et le solde de Karim reste à 30.

### Tests for User Story 4 ⚠️

- [x] T022 [US4] Ajouter les tests suivants dans `backend/tests/Conges.Tests/DecideLeaveRequestTests.cs`, avec
  une factory neuve par test. Le POST va vers `/api/leave-requests/{id}/reject` avec le corps `{ "reason": … }`.
  Dépend de T015 (même fichier).
  1. **Hugo (2) refuse la demande 2** avec « Période de forte activité ». Attendu : `200`, `Status == Rejected`,
     `RejectionReason == "Période de forte activité"` et `DecidedById == 2` ; le solde de Karim (4) reste à 30.
  2. **Motif vide** `""`, puis **motif blanc** `"   "`, puis **sans motif** `{}` : `400`, avec une erreur sur
     `reason`.
  3. **Motif de 501 caractères** : `400`.
  4. **Sophie (6) refuse la demande 2** : `403`.
  5. **Refus de la demande 6**, déjà `Rejected`, par Sophie : `409` `NotPending`.
  6. **Demande inexistante 9999**, avec un motif valide : `404`.
  7. **Claire (1) refuse sa propre demande**, soumise juste avant : `200`.

### Implementation for User Story 4

- [x] T023 [P] [US4] Ajouter `internal sealed class RejectLeaveRequestValidator : AbstractValidator<RejectLeaveRequestDto>`
  dans `backend/src/Conges.Application/LeaveRequests/LeaveRequestCommandValidators.cs` :
  - `RuleFor(x => x.Reason).Must(r => !string.IsNullOrWhiteSpace(r))`, avec le message « Le motif de refus est
    obligatoire. » ;
  - `RuleFor(x => x.Reason).MaximumLength(500)`.
- [x] T024 [US4] Implémenter `RejectAsync` dans
  `backend/src/Conges.Application/LeaveRequests/LeaveRequestCommands.cs`. Dépend de T016 et T023. Rendre T022 vert.
  1. `await rejectValidator.ValidateAndThrowAsync(input, ct)` : la validation (400) passe avant 404, 403 et 409.
  2. `var request = await LoadForDecisionAsync(id, ct);`.
  3. Renseigner `Status = Rejected`, `RejectionReason = input.Reason!.Trim()`, `DecidedAt = time.GetUtcNow()` et
     `DecidedById = currentUser.EmployeeId`.
  4. `await SaveDecisionAsync(ct)`, puis `ReloadAsync`.

  Ne jamais toucher au solde ici.
- [x] T025 [US4] Ajouter l'action suivante dans `backend/src/Conges.Api/Controllers/LeaveRequestsController.cs` :
  ```csharp
  [HttpPost("{id:int}/reject")]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  [ProducesResponseType(StatusCodes.Status409Conflict)]
  public Task<LeaveRequestDto> Reject(int id, RejectLeaveRequestDto input, CancellationToken ct) => commands.RejectAsync(id, input, ct);
  ```

**Checkpoint** : les quatre user stories fonctionnent.

---

## Phase 7: Polish & Cross-Cutting Concerns

- [x] T026 [P] Ajouter les quatre routes au tableau « API existante » de `README.md` :
  - `POST /api/leave-requests` ;
  - `GET /api/leave-requests/pending` ;
  - `POST /api/leave-requests/{id}/approve` ;
  - `POST /api/leave-requests/{id}/reject`.
- [x] T027 Relire `backend/src/Conges.Api/Controllers/LeaveRequestsController.cs` : chaque action doit être un
  appel unique au service, sans `DbContext` ni logique métier (constitution I). Vérifier aussi, par une
  recherche `grep -rn "DateTime.Now\|DateTimeOffset.Now" backend/src`, qu'aucun `DateTime.Now` n'a été introduit
  (constitution V).
- [x] T028 Lancer `docker compose run --rm backend-tests` : tous les tests doivent passer. Utiliser
  `/superpowers:verification-before-completion` et joindre la sortie réelle.
- [x] T029 Après `docker compose restart api`, dérouler les scénarios 1 à 13 de
  `specs/001-leave-request-approval/quickstart.md` et vérifier chaque résultat attendu. Contrôler aussi que la
  route `/pending` apparaît dans Swagger.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (T001)** : aucune dépendance.
- **Foundational (T002 à T010)** : dépend de T001 et bloque toutes les user stories.
  - T003 précède T004 ; T005 précède T006 ; T005 et T007 précèdent T009, qui précède T010.
- **US1 (T011 à T014)** : dépend de la phase 2.
- **US3 (T015 à T018)** : dépend de la phase 2 et de T013, car `LeaveRequestCommands.cs` est partagé et que le
  test 10 soumet une demande.
- **US2 (T019 à T021)** : dépend de la phase 2. Le test 5 dépend de T014 (soumission).
- **US4 (T022 à T025)** : dépend de US3 (helpers `LoadForDecisionAsync` et `SaveDecisionAsync`, fichier de tests
  partagé).
- **Polish (T026 à T029)** : après toutes les stories.

### User Story Dependencies

```text
Phase 2 ──► US1 ──► US3 ──► US4
   │         │
   └──► US2 ◄┘ (seulement pour le test 5)
```

### Within Each User Story

Test rouge, puis validateur ou DTO, puis service, puis controller, puis test vert.

### Parallel Opportunities

- Phase 2 : T002, T005, T007 et T008 sont dans des fichiers distincts et peuvent être faites en parallèle.
- US1 : T012 (validateur) en parallèle de l'écriture de T011.
- Dès la fin de US1, US2 (T019 à T021) en parallèle de US3 (T015 à T018) : fichiers différents
  (`LeaveRequestQueries.cs` contre `LeaveRequestCommands.cs`). Les actions du controller restent à faire l'une
  après l'autre, car c'est le même fichier.
- US4 : T023 en parallèle de T022.
- Polish : T026 en parallèle de T027.

---

## Parallel Example: Phase 2

```text
Task: "T002 Tests CountWorkingDays dans backend/tests/Conges.Tests/HolidayCalendarServiceTests.cs"
Task: "T005 ForbiddenException / ConflictException dans backend/src/Conges.Application/Common/Exceptions.cs"
Task: "T007 LeaveApprovers dans backend/src/Conges.Application/LeaveRequests/LeaveApprovers.cs"
Task: "T008 IsConcurrencyToken sur Status dans backend/src/Conges.Infrastructure/Persistence/AppDbContext.cs"
```

## Parallel Example: après US1

```text
Task: "T019-T020 [US2] PendingLeaveRequestsTests + PendingAsync dans LeaveRequestQueries.cs"
Task: "T015-T017 [US3] DecideLeaveRequestTests + ApproveAsync dans LeaveRequestCommands.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Phase 1, puis phase 2 (checkpoint : tests verts).
2. Phase 3 (US1). **Arrêt et validation** : `SubmitLeaveRequestTests` est vert et le scénario #1 du quickstart
   passe.

### Incremental Delivery

1. MVP US1 : les collaborateurs peuvent soumettre.
2. Ajout de US3 : l'approbation et la mise à jour du solde complètent le parcours P1 (cœur de l'énoncé).
3. Ajout de US2 : la liste des demandes à décider (prérequis de l'écran de la partie 2).
4. Ajout de US4 : le refus motivé.
5. Polish, puis commit et push (la PR se met à jour).

### Notes

- Écrire le test, le voir échouer, puis implémenter (`/superpowers:test-driven-development`).
- En cas d'échec inattendu, utiliser `/superpowers:systematic-debugging` avant toute correction.
- Faire un commit par phase terminée.

---

## Phase 8: Alignement sur le wiki de l'équipe (révision du 2026-11-03)

**Purpose**: appliquer les règles du wiki (RG-01 à RG-14, ADR-003, ADR-004, circuit de validation, codes d'erreur),
non prises en compte dans la première version. TDD : chaque test a été vu en échec avant le code.

- [x] T030 Spec révisée : section « Références », critères tracés `RG-xx`, codes d'erreur, Claire ne peut pas soumettre (`specs/001-leave-request-approval/spec.md`)
- [x] T031 Tests `CountWorkableDays` (jours ouvrables, samedi compté) dans `backend/tests/Conges.Tests/HolidayCalendarServiceTests.cs`, puis implémentation dans `backend/src/Conges.Infrastructure/Calendar/HolidayCalendarService.cs` et `backend/src/Conges.Application/Common/IHolidayCalendarService.cs`
- [x] T032 Horloge figée pour les tests : `backend/tests/Conges.Tests/FixedTimeProvider.cs`, propriété `Now` de `backend/tests/Conges.Tests/CongesApiFactory.cs` ; assertions ProblemDetails dans `backend/tests/Conges.Tests/Problems.cs`
- [x] T033 Tests réécrits avec codes et statuts du référentiel, RG-03/RG-04, RG-13 (10 caractères), Direction : `backend/tests/Conges.Tests/SubmitLeaveRequestTests.cs`, `DecideLeaveRequestTests.cs`, `PendingLeaveRequestsTests.cs`, `ReadApiTests.cs`
- [x] T034 `BusinessException` + `BusinessErrorKind` dans `backend/src/Conges.Application/Common/Exceptions.cs` ; `LeaveErrors` (codes fermés) dans `backend/src/Conges.Application/LeaveRequests/LeaveErrors.cs` ; mapping 422/403/404/409 dans `backend/src/Conges.Api/Http/GlobalExceptionHandler.cs`
- [x] T035 Règles de gestion déplacées hors FluentValidation et implémentées dans `backend/src/Conges.Application/LeaveRequests/LeaveRequestCommands.cs` ; validateurs de forme seulement dans `LeaveRequestCommandValidators.cs`
- [x] T036 Circuit de validation : `ApproverOf` nul pour la Direction, `DecidableBy` sans auto-décision dans `backend/src/Conges.Application/LeaveRequests/LeaveApprovers.cs`
- [x] T037 `docker compose run --rm backend-tests` : 73/73
