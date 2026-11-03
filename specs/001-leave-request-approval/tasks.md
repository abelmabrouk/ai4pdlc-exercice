---

description: "Task list for 001-leave-request-approval"
---

# Tasks: Soumission et validation des demandes de congé

**Input**: Design documents from `/specs/001-leave-request-approval/`

**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md),
[data-model.md](data-model.md), [contracts/leave-requests-api.md](contracts/leave-requests-api.md),
[quickstart.md](quickstart.md)

**Tests**: **Inclus et écrits AVANT l'implémentation.** Ils sont exigés par la constitution (IV), par ENONCE.md
(TDD avec `/superpowers:test-driven-development`) et par SC-001. Chaque test cite la règle `RG-xx` qu'il couvre,
dans son nom ou dans un commentaire `// RG-xx` (ADR-006).

**Organization**: les tâches sont groupées par user story, dans l'ordre de priorité de la spec (US1 P1, US2 P1,
US3 P2, US4 P2).

## Format: `[ID] [P?] [Story] Description`

- **[P]** : la tâche peut être menée en parallèle (fichier différent, aucune dépendance sur une tâche non terminée).
- **[Story]** : la user story concernée (US1 à US4).
- Les chemins sont relatifs à la racine du dépôt.

## Conventions communes à toutes les tâches

- **Commande de test** : `docker compose run --rm backend-tests`. Pour une seule classe :
  `docker compose run --rm backend-tests dotnet test --artifacts-path /artifacts --filter "FullyQualifiedName~<Classe>"`.
- **Date du jour en test** : `new CongesApiFactory { Today = new DateOnly(2027, 2, 1) }` (un lundi). Avec cette
  date, le seed place les demandes en attente aux dates suivantes :

  | Id | Demandeur | Type | Période | Jours | Valideur |
  |---|---|---|---|---|---|
  | #1 | Léa (3) | PaidLeave | 2027-03-01 → 03-05 | 5 | Hugo |
  | #2 | Karim (4) | PaidLeave | 2027-03-15 → 03-17 | 3 | Hugo |
  | #3 | Hugo (2) | PaidLeave | 2027-03-08 → 03-12 | 5 | Claire |
  | #4 | Thomas (7) | FamilyEvent | 2027-02-18 → 02-19 | 2 | Sophie |
  | #7 | Inès (5) | PaidLeave | 2027-03-29 → 03-30 | 2 | Hugo |
  | #8 | Yasmine (8) | PaidLeave | 2027-03-22 → 03-25 | 4 | Sophie |

  Demandes déjà décidées : #5 (Yasmine, `Approved`) et #6 (Nicolas, `Rejected`).
- **Isolation** : un test qui écrit crée sa propre factory (`using var factory = new CongesApiFactory { ... };`).
  Un test en lecture seule peut partager une `IClassFixture<CongesApiFactory>`.
- **Assertion d'erreur métier** : statut HTTP, `Content-Type: application/problem+json` et extension `code`, via
  `ProblemAssert` (T005).
- **Calendrier** : aucun calcul de jour férié en dehors de `IHolidayCalendarService` (ADR-004). La fermeture
  `2027-05-07` est déjà configurée dans `backend/src/Conges.Api/appsettings.json`.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose** : rendre la stack de test exécutable et vérifier l'état de départ.

- [x] T001 Supprimer la dernière ligne parasite de `docker-compose.yml` (la commande shell
  `cd "/d/source/repos/Workshop IA/ai4pdlc-exercice" && docker compose up -d api ...` collée après
  `test-artifacts:`). Vérifier ensuite que `docker compose config -q` ne renvoie aucune erreur.
- [x] T002 Lancer `docker compose run --rm backend-tests` et vérifier que les tests existants
  (`ReadApiTests`, `HolidayCalendarServiceTests`) passent au vert avant toute modification. C'est la référence
  pour la suite.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose** : briques partagées par toutes les user stories (horloge de test, erreurs codées, décompte des jours,
circuit de validation, concurrence).

**⚠️ CRITICAL** : aucune user story ne peut commencer avant la fin de cette phase.

### Outillage de test

- [x] T003 [P] Créer `backend/tests/Conges.Tests/FixedTimeProvider.cs` : `internal sealed class
  FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider`, qui surcharge `GetUtcNow()` (retourne `utcNow`) et
  `LocalTimeZone` (retourne `TimeZoneInfo.Utc`).
- [x] T004 Modifier `backend/tests/Conges.Tests/CongesApiFactory.cs` (dépend de T003). Ajouter
  `public DateOnly? Today { get; init; }`. Dans `ConfigureWebHost`, si `Today` a une valeur, appeler
  `builder.ConfigureTestServices(s => s.AddSingleton<TimeProvider>(new FixedTimeProvider(new
  DateTimeOffset(Today.Value.ToDateTime(new TimeOnly(9, 0)), TimeSpan.Zero))))`. Conserver le constructeur sans
  paramètre, indispensable à `IClassFixture`, et `CreateClientAs` tel quel.
- [x] T005 [P] Créer `backend/tests/Conges.Tests/ProblemAssert.cs` : `internal static class ProblemAssert` avec
  `static async Task HasCode(HttpResponseMessage response, HttpStatusCode status, string code)`. La méthode vérifie
  le statut, `Content-Type` = `application/problem+json`, et la propriété JSON `code` (lue avec `JsonDocument`).
  Ajouter `static async Task IsValidationProblem(HttpResponseMessage response)`, qui vérifie un 400 avec une
  propriété `errors` et **sans** propriété `code`.

### Erreurs métier codées (ADR-003, research R1)

- [x] T006 [P] Créer `backend/src/Conges.Application/Common/BusinessRuleException.cs` avec :
  - `public enum ErrorKind { RuleViolation, Forbidden, NotFound, Conflict }` ;
  - `public sealed record LeaveError(string Code, string Title, ErrorKind Kind)` ;
  - `public static class LeaveErrors`, qui contient **exactement** les 8 entrées du catalogue fermé :

    | Code | Titre | Catégorie |
    |---|---|---|
    | `LEAVE_INVALID_PERIOD` | « Période de congé invalide. » | RuleViolation |
    | `LEAVE_NOTICE_TOO_SHORT` | « Délai de prévenance non respecté. » | RuleViolation |
    | `LEAVE_INSUFFICIENT_BALANCE` | « Solde de congés insuffisant. » | RuleViolation |
    | `LEAVE_OVERLAP` | « Chevauchement avec une autre demande. » | RuleViolation |
    | `LEAVE_REJECTION_REASON_REQUIRED` | « Motif de refus requis. » | RuleViolation |
    | `LEAVE_APPROVER_NOT_ALLOWED` | « Utilisateur non habilité à décider de cette demande. » | Forbidden |
    | `LEAVE_ALREADY_DECIDED` | « Demande déjà décidée. » | Conflict |
    | `LEAVE_NOT_FOUND` | « Demande de congé introuvable. » | NotFound |

  - `public sealed class BusinessRuleException(LeaveError error, string detail) : Exception(detail)`, qui expose
    `Error`.

  Pas de référence HTTP dans ce fichier.
- [x] T007 Modifier `backend/src/Conges.Api/Http/GlobalExceptionHandler.cs` (dépend de T006). Ajouter un cas
  `BusinessRuleException rule` dans le `switch` qui produit un `ProblemDetails` avec :
  - `Status` selon `rule.Error.Kind` : RuleViolation → 422, Forbidden → 403, NotFound → 404, Conflict → 409 ;
  - `Type` = `"https://conges.example/errors/" + code.ToLowerInvariant().Replace('_', '-')` ;
  - `Title` = `rule.Error.Title` ;
  - `Detail` = `rule.Message` ;
  - `Extensions["code"]` = `rule.Error.Code`.

  Les cas existants (validation 400, `NotFoundException` 404, `UnauthenticatedException` 401, 500) restent
  inchangés.

### Décompte des jours (RG-02, RG-03, ADR-004, research R3)

- [x] T008 [P] Écrire `backend/tests/Conges.Tests/LeaveDayCounterTests.cs`. Le test doit **échouer** (la classe
  n'existe pas encore). Construire le calendrier comme dans `HolidayCalendarServiceTests` :
  `new HolidayCalendarService(Options.Create(new HolidayCalendarOptions { CompanyClosures =
  [new DateOnly(2027, 5, 7)] }))`.

  `[Theory]` RG-02 `CountLeaveDays(start, end)` :

  | Période | Attendu | Cas |
  |---|---|---|
  | 2027-03-08 → 03-12 | 5 | lundi → vendredi |
  | 2027-03-08 → 03-13 | 6 | lundi → samedi |
  | 2027-03-04 → 03-09 | 5 | jeudi → mardi |
  | 2027-03-08 → 03-14 | 6 | lundi → dimanche |
  | 2027-03-12 → 03-15 | 3 | vendredi → lundi |
  | 2027-05-03 → 05-08 | 3 | Ascension 6, fermeture 7, Victoire 1945 le samedi 8 |
  | 2027-05-17 → 05-17 | 1 | lundi de Pentecôte travaillé |
  | 2027-03-14 → 03-14 | 0 | dimanche |
  | 2027-05-06 → 05-06 | 0 | férié |

  `[Theory]` RG-03 `CountWorkingDaysBetween(after, before)` (bornes exclues) :

  | Après → avant | Attendu |
  |---|---|
  | 2027-03-01 → 03-03 | 1 |
  | 2027-03-01 → 03-04 | 2 |
  | 2027-05-04 → 05-10 | 1 |
  | 2027-05-04 → 05-11 | 2 |
  | 2027-03-01 → 03-01 | 0 |
  | 2027-03-01 → 03-02 | 0 |
- [x] T009 Créer `backend/src/Conges.Application/LeaveRequests/LeaveDayCounter.cs` (dépend de T008) :
  `public sealed class LeaveDayCounter(IHolidayCalendarService calendar)`, avec deux méthodes.
  - `int CountLeaveDays(DateOnly start, DateOnly end)` compte les jours de `start` à `end` inclus avec
    `DayOfWeek != Sunday` et une date absente de `calendar.GetHolidays(start, end)`. Il fait un seul appel au
    calendrier et retourne 0 si `end < start`.
  - `int CountWorkingDaysBetween(DateOnly after, DateOnly before)` compte les jours strictement entre `after` et
    `before`, hors samedi, dimanche et dates de `calendar.GetHolidays(...)`.

  `public` (et non `internal`) parce que `Conges.Application` n'a pas d'`InternalsVisibleTo` : c'est le même
  choix que `HolidayCalendarService`. Aucun calcul de jour férié local. `LeaveDayCounterTests` doit passer.

### Circuit de validation (RG-10, research R4)

- [x] T010 [P] Écrire `backend/tests/Conges.Tests/ApprovalCircuitTests.cs`. Le test doit **échouer**. Construire
  les objets `Employee`, `Team` et `ParentTeam` en mémoire. Cas `[Fact]` couverts (RG-10) :
  - un membre d'équipe a pour valideur le manager de l'équipe (Léa → 2) ;
  - le manager d'une équipe a pour valideur le manager de l'équipe parente (Hugo → 1) ;
  - le manager d'une équipe sans équipe parente n'a pas de valideur (Claire → `null`) ;
  - un membre d'une équipe sans manager n'a pas de valideur (`null`) ;
  - le manager d'une équipe dont l'équipe parente n'a pas de manager n'a pas de valideur (`null`).
- [x] T011 Créer `backend/src/Conges.Application/LeaveRequests/ApprovalCircuit.cs` (dépend de T010) :
  `public static class ApprovalCircuit`, avec `public static int? ApproverOf(Employee requester)`. La méthode exige
  `requester.Team` chargé, et `Team.ParentTeam` chargé si nécessaire. Algorithme exact du wiki :
  `requester.Team!.ManagerId == requester.Id ? requester.Team.ParentTeam?.ManagerId : requester.Team.ManagerId`.
  `ApprovalCircuitTests` doit passer.

### Enregistrement et concurrence

- [x] T012 Modifier `backend/src/Conges.Application/DependencyInjection.cs` (dépend de T009) : ajouter
  `services.AddSingleton<LeaveDayCounter>();`.
- [x] T013 [P] Modifier `backend/src/Conges.Infrastructure/Persistence/AppDbContext.cs` : dans la configuration de
  `LeaveRequest`, ajouter `.IsConcurrencyToken()` à `request.Property(r => r.Status)` (research R6). N'ajouter
  aucune colonne ni migration (ADR-002).
- [x] T014 Lancer `docker compose run --rm backend-tests` : tous les tests (existants, `LeaveDayCounterTests` et
  `ApprovalCircuitTests`) passent.

**Checkpoint** : les fondations sont prêtes, et les user stories peuvent commencer.

---

## Phase 3: User Story 1 - Soumettre une demande de congé (Priority: P1) 🎯 MVP

**Goal** : un collaborateur soumet une demande ; elle est créée au statut `Pending` avec le nombre de jours
ouvrables décomptés, ou refusée avec le bon code.

**Independent Test** : en tant que Léa, `POST /api/leave-requests` du 2027-03-08 au 2027-03-12 renvoie `201`,
`status: Pending` et `days: 5`. Voir [contracts](contracts/leave-requests-api.md#post-apileave-requests--soumettre-une-demande).

### Tests for User Story 1 ⚠️

> Écrire ces tests D'ABORD et vérifier qu'ils ÉCHOUENT avant d'implémenter.

- [x] T015 [US1] Écrire `backend/tests/Conges.Tests/SubmitLeaveRequestTests.cs`. Sauf mention contraire, chaque
  test crée sa propre factory avec `Today = 2027-02-01` et poste avec `CreateClientAs(<id>)`. Cas à couvrir (un
  test par ligne, nom ou commentaire avec la RG) :

  | # | RG | Requête | Attendu |
  |---|---|---|---|
  | 1 | RG-02, RG-07 | Léa (3) : PaidLeave 2027-03-08 → 03-12, comment « Vacances » | `201` ; `Location` pointe sur `/api/leave-requests/{id}` ; corps `status: Pending`, `days: 5`, `comment: "Vacances"`, `employeeId: 3`, `submittedAt` = 2027-02-01T09:00:00Z ; `GET` de la ressource renvoie la même demande |
  | 2 | RG-02 | Léa : PaidLeave 2027-03-08 → 03-13 | `days: 6` |
  | 3 | RG-02 | Léa : PaidLeave 2027-03-12 → 03-15 | `days: 3` |
  | 4 | RG-02 | Léa : PaidLeave 2027-05-03 → 05-08 | `days: 3` |
  | 5 | RG-02 | Léa : PaidLeave 2027-05-17 → 05-17 | `days: 1` |
  | 6 | RG-02 | Léa : FamilyEvent 2027-03-08 → 03-13 | `days: 6` |
  | 7 | RG-01 | Léa : 2027-03-12 → 03-08 | `422 LEAVE_INVALID_PERIOD` |
  | 8 | RG-01 | Léa : 2027-01-29 → 2027-02-02 | `422 LEAVE_INVALID_PERIOD` |
  | 9 | RG-02 | Léa : 2027-03-14 → 03-14 (dimanche) ; puis 2027-05-06 → 05-06 (férié) | `422 LEAVE_INVALID_PERIOD` |
  | 10 | RG-03 | `Today = 2027-03-01`. Léa : PaidLeave 2027-03-03 → 03-03 ; puis 2027-03-04 → 03-04 | `422 LEAVE_NOTICE_TOO_SHORT`, puis `201` |
  | 11 | RG-03 | `Today = 2027-05-04`. Léa : PaidLeave 2027-05-10 → 05-10 ; puis 2027-05-11 → 05-11 | `422 LEAVE_NOTICE_TOO_SHORT`, puis `201` |
  | 12 | RG-04 | `Today = 2027-03-01`. Léa : FamilyEvent 2027-03-01 → 03-01 ; puis FamilyEvent 2027-03-02 → 03-02 | `201` les deux fois |
  | 13 | RG-05 | Inès (5, solde 3, #7 en attente pour 2 jours) : PaidLeave 2027-04-05 → 04-06 ; puis 2027-04-05 → 04-05 | `422 LEAVE_INSUFFICIENT_BALANCE`, puis `201` |
  | 14 | RG-05 | Inès : FamilyEvent 2027-04-05 → 04-09 (5 jours) | `201` |
  | 15 | RG-06 | Léa : 2027-03-08 → 03-12 (`201`), puis 2027-03-12 → 03-15 | `422 LEAVE_OVERLAP` |
  | 16 | RG-06 | Léa soumet 2027-03-08 → 03-12. La demande est refusée par Hugo, via `AppDbContext` dans un scope de `factory.Services`, ou via `/reject` une fois US3 livrée. Puis Léa soumet la même période. | `201` |
  | 17 | — | Corps sans `type` ; avec `"type": "Holiday"` ; sans `startDate` ; avec `"startDate": "08/03/2027"` | `400` validation (`ProblemAssert.IsValidationProblem`) |
  | 18 | — | Sans en-tête `X-User-Id` | `401` |
  | 19 | RG-06 | Léa : 2027-03-01 → 03-01 (chevauche #1, en attente) | `422 LEAVE_OVERLAP` |

  Vérifier aussi que les refus n'enregistrent rien : après un `422`, `GET /api/leave-requests?employeeId=3`
  renvoie le même nombre de demandes qu'avant.

### Implementation for User Story 1

- [x] T016 [US1] Créer `backend/src/Conges.Application/LeaveRequests/LeaveRequestCommands.cs`, avec :
  - `public sealed record SubmitLeaveRequest(LeaveType? Type, DateOnly? StartDate, DateOnly? EndDate, string?
    Comment);`
  - un validateur `SubmitLeaveRequestValidator : AbstractValidator<SubmitLeaveRequest>`, avec `Type` `NotNull()` et
    `IsInEnum()`, `StartDate` `NotNull()` et `EndDate` `NotNull()`, **et rien d'autre** : l'ordre des dates est
    une règle de gestion (ADR-003) ;
  - `public interface ILeaveRequestCommands { Task<LeaveRequestDto> SubmitAsync(SubmitLeaveRequest request,
    CancellationToken ct); }` ;
  - `internal sealed class LeaveRequestCommands(IAppDbContext db, ICurrentUser currentUser, TimeProvider clock,
    LeaveDayCounter dayCounter, IValidator<SubmitLeaveRequest> validator) : ILeaveRequestCommands`.

  `SubmitAsync` effectue les étapes suivantes, **dans cet ordre** :
  1. `await validator.ValidateAndThrowAsync(request, ct)` ;
  2. charger l'employé `currentUser.EmployeeId`, suivi, avec `Include(e => e.Team)`. S'il est absent :
     `throw new NotFoundException("Employé", id)` ;
  3. `today = DateOnly.FromDateTime(clock.GetLocalNow().DateTime)`. Si `End < Start` ou `Start < today`, alors
     `BusinessRuleException(LeaveErrors.InvalidPeriod, ...)` ;
  4. `days = dayCounter.CountLeaveDays(start, end)`. Si `days == 0`, alors `LEAVE_INVALID_PERIOD` ;
  5. pour `PaidLeave` : si `dayCounter.CountWorkingDaysBetween(today, start) < 2`, alors `LEAVE_NOTICE_TOO_SHORT` ;
  6. s'il existe une demande du même employé au statut `Pending` ou `Approved` avec
     `r.StartDate <= end && r.EndDate >= start`, alors `LEAVE_OVERLAP` ;
  7. pour `PaidLeave` : si `days > PaidLeaveBalance − Σ Days` (demandes `PaidLeave` `Pending` de l'employé), alors
     `LEAVE_INSUFFICIENT_BALANCE`, avec le détail « {days} jours demandés, {disponible} jours disponibles. » ;
  8. créer `new LeaveRequest { EmployeeId, Type, StartDate, EndDate, Days = days, Status = Pending, Comment,
     SubmittedAt = clock.GetUtcNow(), Employee = employee }`, appeler `SaveChangesAsync`, puis retourner `ToDto()`.

  Chaque `BusinessRuleException` porte un `detail` lisible en français.
- [x] T017 [US1] Modifier `backend/src/Conges.Application/DependencyInjection.cs` : ajouter
  `services.AddScoped<ILeaveRequestCommands, LeaveRequestCommands>();`. Le validateur est déjà enregistré par
  `AddValidatorsFromAssembly`.
- [x] T018 [US1] Modifier `backend/src/Conges.Api/Controllers/LeaveRequestsController.cs` :
  - injecter `ILeaveRequestCommands commands` dans le constructeur primaire ;
  - ajouter `[HttpPost]`, avec `[ProducesResponseType(StatusCodes.Status201Created)]`, puis 400, 401 et 422 ;
  - écrire l'action `async Task<ActionResult<LeaveRequestDto>> Submit(SubmitLeaveRequest request,
    CancellationToken ct)` : `var created = await commands.SubmitAsync(request, ct); return
    CreatedAtAction(nameof(Get), new { id = created.Id }, created);`.

  Aucune logique métier dans le controller.
- [x] T019 [US1] Lancer `docker compose run --rm backend-tests` : `SubmitLeaveRequestTests` et tous les tests
  existants passent.

**Checkpoint** : la soumission est fonctionnelle et testée indépendamment. C'est le MVP.

---

## Phase 4: User Story 2 - Approuver une demande (Priority: P1)

**Goal** : le valideur approuve une demande en attente. Le statut passe à `Approved`, la décision est tracée, et le
solde CP est débité pour un `PaidLeave`.

**Independent Test** : en tant que Hugo, `POST /api/leave-requests/1/approve` renvoie `200` et `status:
Approved`, puis `GET /api/employees/3` renvoie `paidLeaveBalance: 25`. Ce test s'appuie sur les données de
démonstration et ne dépend pas d'US1.

### Tests for User Story 2 ⚠️

- [x] T020 [US2] Écrire `backend/tests/Conges.Tests/ApproveLeaveRequestTests.cs`, avec une factory par test et
  `Today = 2027-02-01` :

  | # | RG | Action | Attendu |
  |---|---|---|---|
  | 1 | RG-10, RG-12, RG-14 | Hugo (2) approuve #1 (Léa, 5 j CP) | `200` ; `status: Approved`, `decidedById: 2`, `decidedAt` = 2027-02-01T09:00:00Z ; `GET /api/employees/3` → `paidLeaveBalance: 25` |
  | 2 | RG-12 | Sophie (6) approuve #4 (Thomas, FamilyEvent) | `200` ; `GET /api/employees/7` → `paidLeaveBalance: 30` |
  | 3 | RG-10 | Claire (1) approuve #3 (Hugo) | `200` ; solde de Hugo 25 |
  | 4 | RG-10 | Hugo (2) approuve #3 (sa propre demande) | `403 LEAVE_APPROVER_NOT_ALLOWED` ; #3 reste `Pending` |
  | 5 | RG-10 | Sophie (6) approuve #1 ; Claire (1) approuve #1 (N+2, pas de délégation) | `403 LEAVE_APPROVER_NOT_ALLOWED` les deux fois ; solde de Léa 30 |
  | 6 | RG-10 | Claire soumet une demande (FamilyEvent 2027-03-08 → 03-08), puis tente de l'approuver | `403 LEAVE_APPROVER_NOT_ALLOWED` (direction générale, FR-011) |
  | 7 | RG-11 | Sophie (6) approuve #5 (déjà `Approved`) ; Sophie approuve #6 (déjà `Rejected`) | `409 LEAVE_ALREADY_DECIDED` ; solde de Yasmine inchangé (25) |
  | 8 | RG-11 | Hugo approuve #1 deux fois de suite | `200`, puis `409 LEAVE_ALREADY_DECIDED` ; solde de Léa 25, sans double débit |
  | 9 | — | Hugo approuve 9999 | `404 LEAVE_NOT_FOUND` |
  | 10 | — | Sans en-tête `X-User-Id` | `401` |

### Implementation for User Story 2

- [x] T021 [US2] Dans `backend/src/Conges.Application/LeaveRequests/LeaveRequestCommands.cs`, ajouter
  `Task<LeaveRequestDto> ApproveAsync(int id, CancellationToken ct)` à `ILeaveRequestCommands`, puis
  l'implémenter. Ajouter aussi une méthode privée partagée `LoadForDecisionAsync(int id, CancellationToken ct)`,
  réutilisée par US3.

  `LoadForDecisionAsync` effectue les étapes suivantes, **dans cet ordre** :
  1. charger la demande, suivie, avec `Include(r => r.Employee!).ThenInclude(e => e.Team!).ThenInclude(t =>
     t.ParentTeam)`. Si elle est absente : `BusinessRuleException(LeaveErrors.NotFound, ...)` ;
  2. si `ApprovalCircuit.ApproverOf(request.Employee!) != currentUser.EmployeeId`, alors `LEAVE_APPROVER_NOT_ALLOWED`.
     Le résultat `null` est donc toujours refusé ;
  3. si `Status != Pending`, alors `LEAVE_ALREADY_DECIDED`.

  `ApproveAsync` effectue ensuite les étapes suivantes :
  1. `Status = Approved`, `DecidedAt = clock.GetUtcNow()` et `DecidedById = currentUser.EmployeeId` ;
  2. si `Type == PaidLeave`, alors `request.Employee!.PaidLeaveBalance -= request.Days` ;
  3. un **seul** `SaveChangesAsync`. Une `DbUpdateConcurrencyException` est traduite en
     `BusinessRuleException(LeaveErrors.AlreadyDecided, ...)` (research R6) ;
  4. retourner `ToDto()`.
- [x] T022 [US2] Dans `backend/src/Conges.Api/Controllers/LeaveRequestsController.cs`, ajouter l'action
  `[HttpPost("{id:int}/approve")]`, avec `ProducesResponseType` 200, 401, 403, 404 et 409 :
  `Task<LeaveRequestDto> Approve(int id, CancellationToken ct) => commands.ApproveAsync(id, ct);`.
- [x] T023 [US2] Lancer `docker compose run --rm backend-tests` : `ApproveLeaveRequestTests` et tous les tests
  précédents passent.

**Checkpoint** : US1 et US2 fonctionnent. Le solde est mis à jour à l'approbation.

---

## Phase 5: User Story 3 - Refuser une demande avec un motif (Priority: P2)

**Goal** : le valideur refuse une demande en attente avec un motif d'au moins 10 caractères. Le statut passe à
`Rejected`, le motif est conservé, et le solde n'est pas modifié.

**Independent Test** : en tant que Hugo, `POST /api/leave-requests/2/reject` avec `{"reason":"Période de forte
activité"}` renvoie `200` et `status: Rejected`. Le solde de Karim reste à 30.

### Tests for User Story 3 ⚠️

- [x] T024 [US3] Écrire `backend/tests/Conges.Tests/RejectLeaveRequestTests.cs`, avec une factory par test et
  `Today = 2027-02-01` :

  | # | RG | Action | Attendu |
  |---|---|---|---|
  | 1 | RG-10, RG-13, RG-14 | Hugo (2) refuse #2 (Karim) avec `{"reason":"Période de forte activité"}` | `200` ; `status: Rejected`, `rejectionReason: "Période de forte activité"`, `decidedById: 2`, `decidedAt` = 2027-02-01T09:00:00Z ; `GET /api/employees/4` → `paidLeaveBalance: 30` |
  | 2 | RG-13 | Hugo refuse #2 avec un corps vide (aucun contenu), puis `{}`, puis `{"reason":null}`, puis `{"reason":""}`, puis `{"reason":"   Non merci  "}` (9 caractères après suppression des espaces) | `422 LEAVE_REJECTION_REASON_REQUIRED` à chaque fois ; #2 reste `Pending` |
  | 3 | RG-13 | Hugo refuse #2 avec `{"reason":"  Pas validé  "}` (10 caractères après suppression des espaces) | `200` ; `rejectionReason: "Pas validé"` (valeur sans espaces de début et de fin) |
  | 4 | RG-11 | Sophie (6) refuse #5 (déjà `Approved`) avec un motif valide | `409 LEAVE_ALREADY_DECIDED` |
  | 5 | RG-10 | Sophie (6) refuse #2 avec un motif valide ; Hugo refuse #3 (sa propre demande) | `403 LEAVE_APPROVER_NOT_ALLOWED` les deux fois |
  | 6 | RG-10 | Sophie (6) refuse #2 avec `{"reason":"court"}` | `403 LEAVE_APPROVER_NOT_ALLOWED` (l'habilitation est contrôlée avant le motif, research R7) |
  | 7 | — | Hugo refuse 9999 avec un motif valide | `404 LEAVE_NOT_FOUND` |
  | 8 | — | Sans en-tête `X-User-Id` | `401` |

### Implementation for User Story 3

- [x] T025 [US3] Dans `backend/src/Conges.Application/LeaveRequests/LeaveRequestCommands.cs`, ajouter
  `public sealed record RejectLeaveRequest(string? Reason);` et `Task<LeaveRequestDto> RejectAsync(int id, string?
  reason, CancellationToken ct)` à l'interface, puis l'implémenter dans cet ordre :
  1. `LoadForDecisionAsync(id, ct)` (T021) ;
  2. `var trimmed = reason?.Trim() ?? ""`. Si `trimmed.Length < 10`, alors
     `BusinessRuleException(LeaveErrors.RejectionReasonRequired, ...)`. Ce n'est **pas** un validateur
     FluentValidation (ADR-003) ;
  3. `Status = Rejected`, `RejectionReason = trimmed`, `DecidedAt = clock.GetUtcNow()` et
     `DecidedById = currentUser.EmployeeId`. Le solde n'est pas touché ;
  4. un seul `SaveChangesAsync`, avec la même traduction de `DbUpdateConcurrencyException`, puis retourner `ToDto()`.
- [x] T026 [US3] Dans `backend/src/Conges.Api/Controllers/LeaveRequestsController.cs`, ajouter l'action
  `[HttpPost("{id:int}/reject")]`, avec `ProducesResponseType` 200, 401, 403, 404, 409 et 422 :
  `Task<LeaveRequestDto> Reject(int id, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)]
  RejectLeaveRequest? body, CancellationToken ct) => commands.RejectAsync(id, body?.Reason, ct);`.
  `EmptyBodyBehavior.Allow` garantit qu'un corps vide donne un 422 et non un 400 (research R2).
- [x] T027 [US3] Lancer `docker compose run --rm backend-tests` : `RejectLeaveRequestTests` et tous les tests
  précédents passent.

**Checkpoint** : soumission, approbation et refus fonctionnent.

---

## Phase 6: User Story 4 - Lister les demandes à décider (Priority: P2)

**Goal** : un utilisateur obtient la liste des demandes `Pending` dont il est le valideur, triée par date de début.

**Independent Test** : en tant que Hugo, `GET /api/leave-requests/pending` renvoie les demandes #1, #2 et #7, dans
cet ordre. Ce test s'appuie sur les données de démonstration et ne dépend d'aucune autre story.

### Tests for User Story 4 ⚠️

- [x] T028 [US4] Écrire `backend/tests/Conges.Tests/PendingLeaveRequestsTests.cs`. Les tests en lecture seule
  partagent une `IClassFixture<CongesApiFactory>` ; le test 6 crée sa propre factory.

  | # | RG | Action | Attendu |
  |---|---|---|---|
  | 1 | RG-10 | Hugo (2) : `GET /api/leave-requests/pending` | ids exactement `[1, 2, 7]`, dans cet ordre (tri par `startDate`) ; tous `Pending` ; la #3 (sa propre demande) n'y figure pas |
  | 2 | RG-10 | Claire (1) | exactement `[3]` |
  | 3 | RG-10 | Sophie (6) | exactement `[4, 8]` |
  | 4 | — | Léa (3) | `200` et `[]` |
  | 5 | — | Sans en-tête `X-User-Id` | `401` |
  | 6 | RG-11 | Factory dédiée : Hugo refuse #2 (via `AppDbContext` dans un scope de `factory.Services` pour rester indépendant d'US3, ou via `/reject`), puis consulte la liste | `[1, 7]` |

### Implementation for User Story 4

- [x] T029 [US4] Modifier `backend/src/Conges.Application/LeaveRequests/LeaveRequestQueries.cs`, dans cet ordre :
  1. ajouter `Task<IReadOnlyList<LeaveRequestDto>> ListPendingToDecideAsync(CancellationToken ct)` à
     `ILeaveRequestQueries` ;
  2. injecter `ICurrentUser currentUser` dans le constructeur primaire de `LeaveRequestQueries` (ADR-005) ;
  3. implémenter la méthode :
     - lire `var me = currentUser.EmployeeId` en premier, pour obtenir un 401 si l'en-tête est absent ;
     - charger `db.LeaveRequests.AsNoTracking().Include(r => r.Employee!).ThenInclude(e => e.Team!).ThenInclude(t
       => t.ParentTeam).Where(r => r.Status == LeaveStatus.Pending)` ;
     - filtrer en mémoire `ApprovalCircuit.ApproverOf(r.Employee!) == me`, trier par `StartDate` puis par `Id`, et
       mapper avec `ToDto()`.
- [x] T030 [US4] Dans `backend/src/Conges.Api/Controllers/LeaveRequestsController.cs`, ajouter l'action
  `[HttpGet("pending")]` avec `ProducesResponseType` 200 et 401 :
  `Task<IReadOnlyList<LeaveRequestDto>> Pending(CancellationToken ct) => leaveRequests.ListPendingToDecideAsync(ct);`.
- [x] T031 [US4] Lancer `docker compose run --rm backend-tests` : `PendingLeaveRequestsTests` et tous les tests
  précédents passent.

**Checkpoint** : les quatre user stories sont fonctionnelles et testées.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose** : vérifications transverses avant de déclarer la partie terminée
(`/superpowers:verification-before-completion`).

- [x] T032 [P] Vérifier la traçabilité (SC-001, ADR-006). Chaque scénario d'acceptation de
  `specs/001-leave-request-approval/spec.md` (US1 1-18, US2 1-6, US3 1-6, US4 1-6) doit avoir au moins un test
  correspondant dans `backend/tests/Conges.Tests/`, qui cite sa `RG-xx` quand le scénario en a une. Compléter les
  tests manquants.
- [x] T033 [P] Vérifier SC-005 (ADR-004) : `grep -rniE "easter|pâques|paques|DayOfWeek.Sunday|holiday"
  backend/src --include=*.cs`. Les seules occurrences liées aux jours fériés doivent se trouver dans
  `Conges.Infrastructure/Calendar/` ou appeler `IHolidayCalendarService`. `LeaveDayCounter` ne fait que lire le
  calendrier.
- [x] T034 Lancer la suite complète `docker compose run --rm backend-tests` et vérifier qu'elle est au vert. Copier
  le résumé (nombre de tests passés) dans le compte rendu.
- [x] T035 [P] Lancer `docker compose exec web npx ng build` : le front compile sans modification (SC-006).
- [x] T036 Dérouler le parcours manuel de `specs/001-leave-request-approval/quickstart.md`, § 2, étapes 1 à 11,
  après `docker compose restart api`, et vérifier chaque résultat attendu (statut et `code`).

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)** : T001 en premier, car sans lui aucune commande `docker compose` ne fonctionne. T002 vient
  ensuite.
- **Foundational (Phase 2)** : dépend du Setup et **bloque toutes les user stories**.
- **US1 à US4 (Phases 3 à 6)** : chacune ne dépend que de la phase 2. Elles partagent toutefois les fichiers
  `LeaveRequestCommands.cs` (US1, US2, US3) et `LeaveRequestsController.cs` (toutes). On les mène donc **en
  séquence** dans l'ordre US1 → US2 → US3 → US4, ou en parallèle uniquement en gérant les fusions de ces deux
  fichiers.
- **Polish (Phase 7)** : dépend de toutes les stories souhaitées.

### User Story Dependencies

- **US1 (P1)** : aucune dépendance sur une autre story.
- **US2 (P1)** : indépendante d'US1, car elle s'appuie sur les demandes en attente du seed. Elle crée
  `LoadForDecisionAsync`, que réutilise US3.
- **US3 (P2)** : fonctionnellement indépendante. Techniquement, elle réutilise `LoadForDecisionAsync` (T021). Si
  US3 est faite avant US2, il faut créer cette méthode dans T025.
- **US4 (P2)** : indépendante. Elle ne touche que `LeaveRequestQueries.cs` et une action du controller.

### Within Each User Story

- Le test est écrit et **échoue** avant l'implémentation (TDD).
- L'ordre est : service applicatif, puis enregistrement DI, puis action du controller, puis passage des tests.
- La story est terminée (checkpoint vert) avant de passer à la suivante.

### Parallel Opportunities

- **Phase 2** : T003, T005, T006, T008, T010 et T013 touchent des fichiers différents et sont parallélisables.
  T004 attend T003, T007 attend T006, T009 attend T008, T011 attend T010, et T012 attend T009.
- **US4 contre US1 à US3** : US4 modifie `LeaveRequestQueries.cs` plutôt que `LeaveRequestCommands.cs`. Seul le
  controller est commun.
- **Phase 7** : T032, T033 et T035 sont parallélisables.

---

## Parallel Example: Phase 2 (Foundational)

```bash
# En parallèle (fichiers distincts) :
Task: "T003 FixedTimeProvider dans backend/tests/Conges.Tests/FixedTimeProvider.cs"
Task: "T005 ProblemAssert dans backend/tests/Conges.Tests/ProblemAssert.cs"
Task: "T006 BusinessRuleException + LeaveErrors dans backend/src/Conges.Application/Common/BusinessRuleException.cs"
Task: "T008 LeaveDayCounterTests dans backend/tests/Conges.Tests/LeaveDayCounterTests.cs"
Task: "T010 ApprovalCircuitTests dans backend/tests/Conges.Tests/ApprovalCircuitTests.cs"
Task: "T013 IsConcurrencyToken dans backend/src/Conges.Infrastructure/Persistence/AppDbContext.cs"

# Puis, chacune après sa dépendance :
Task: "T004 CongesApiFactory.Today"         # après T003
Task: "T007 GlobalExceptionHandler"         # après T006
Task: "T009 LeaveDayCounter"                # après T008
Task: "T011 ApprovalCircuit"                # après T010
```

## Parallel Example: User Story 4 (en parallèle d'US2 ou d'US3)

```bash
Task: "T028 PendingLeaveRequestsTests dans backend/tests/Conges.Tests/PendingLeaveRequestsTests.cs"
Task: "T029 ListPendingToDecideAsync dans backend/src/Conges.Application/LeaveRequests/LeaveRequestQueries.cs"
# T030 (controller) est à fusionner avec les actions des autres stories.
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Phase 1 : réparer `docker-compose.yml` et obtenir une référence de tests verte.
2. Phase 2 : construire les fondations (bloquant).
3. Phase 3 : livrer US1, la soumission.
4. **STOP and VALIDATE** : `SubmitLeaveRequestTests` est vert, et les étapes 3 à 5 du quickstart passent.

### Incremental Delivery

1. Fondations, puis US1 (MVP : on peut soumettre).
2. US2 : on peut approuver, et le solde est débité. L'objectif métier principal est atteint.
3. US3 : on peut refuser avec un motif.
4. US4 : la liste « à décider » est disponible, prête pour l'écran de la partie 2.
5. Polish : traçabilité, build du front et quickstart. Ensuite, commit et push (SETUP.md, § 7).

---

## Notes

- **[P]** signifie des fichiers différents, sans dépendance. **[USx]** donne la traçabilité vers la spec.
- Chaque tâche de test doit échouer avant l'implémentation correspondante (`/superpowers:test-driven-development`).
- En cas d'échec inattendu, utiliser `/superpowers:systematic-debugging` : identifier la cause racine avant toute
  correction.
- Commit conseillé à chaque checkpoint.
- **Écart au plan** : `LeaveDayCounter` et `ApprovalCircuit` sont `public` et non `internal`, faute
  d'`InternalsVisibleTo`. C'est cohérent avec `HolidayCalendarService`, et aucune violation de la constitution
  n'en découle.
