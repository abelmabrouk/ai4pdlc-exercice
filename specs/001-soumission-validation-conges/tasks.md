---

description: "Liste des tâches : soumission et validation des demandes de congé"
---

# Tasks: Soumission et validation des demandes de congé

**Input**: Design documents from `/specs/001-soumission-validation-conges/`

**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md), [data-model.md](data-model.md),
[contracts/leave-requests-api.md](contracts/leave-requests-api.md), [quickstart.md](quickstart.md)

**Tests** : ils sont **demandés**. L'énoncé impose le TDD (`/superpowers:test-driven-development`), la
constitution (principe IV) exige des tests xUnit, et le critère SC-001 de la spec veut que 100 % des scénarios
d'acceptation soient automatisés. Chaque tâche de test est écrite **avant** l'implémentation correspondante, et
on vérifie qu'elle **échoue** avant de passer à la suite.

**Organization** : les tâches sont regroupées par user story. À l'intérieur d'une story, elles alternent par
petits incréments « test rouge → implémentation verte », règle de gestion par règle de gestion.

## Format: `[ID] [P?] [Story] Description`

- **[P]** : parallélisable (fichier différent, aucune dépendance sur une tâche inachevée)
- **[Story]** : user story concernée (US1 à US4)
- Les chemins sont relatifs à la racine du dépôt.

## Conventions pour l'implémenteur

- **Lancer tous les tests** : `docker compose run --rm backend-tests`.
- **Lancer un sous-ensemble** :
  `docker compose run --rm backend-tests dotnet test --artifacts-path /artifacts --filter "FullyQualifiedName~<NomDeClasse>"`.
- **Nommage des tests (ADR-006)** : le nom de chaque test commence par la règle couverte, par exemple
  `RG03_paid_leave_submitted_monday_cannot_start_wednesday`. Pour un scénario sans RG, utiliser `ERR_` (erreur
  de forme ou d'identification) ou `NF_` (demande inexistante).
- **Assertions d'erreur** : vérifier le statut HTTP, le type de contenu `application/problem+json` et
  l'extension `code`, avec la valeur exacte du référentiel. Pour un 400 : présence de `errors` et absence de
  `code`. Les codes s'écrivent **en littéral** dans les tests (`"LEAVE_OVERLAP"`), comme le ferait un client.
- **Horloge par défaut des tests d'API** : lundi 2027-03-01 à 09:00 UTC. À cette date, les demandes seedées (voir
  `backend/src/Conges.Infrastructure/Persistence/DatabaseInitializer.cs`) sont les suivantes :

  | Id | Collaborateur (id) | Type | Période | Jours | Statut | Valideur |
  |---|---|---|---|---|---|---|
  | 1 | Lea Petit (3) | PaidLeave | 2027-03-29 → 2027-04-02 | 5 | Pending | Hugo (2) |
  | 2 | Karim Benali (4) | PaidLeave | 2027-04-12 → 2027-04-14 | 3 | Pending | Hugo (2) |
  | 3 | Hugo Bernard (2) | PaidLeave | 2027-04-05 → 2027-04-09 | 5 | Pending | Claire (1) |
  | 4 | Thomas Roux (7) | FamilyEvent | 2027-03-18 → 2027-03-19 | 2 | Pending | Sophie (6) |
  | 5 | Yasmine Haddad (8) | PaidLeave | 2027-01-25 → 2027-01-29 | 5 | Approved | Sophie (6) |
  | 6 | Nicolas Fournier (9) | PaidLeave | 2027-02-08 → 2027-02-09 | 2 | Rejected | Sophie (6) |
  | 7 | Ines Moreau (5) | PaidLeave | 2027-04-26 → 2027-04-27 | 2 | Pending | Hugo (2) |
  | 8 | Yasmine Haddad (8) | PaidLeave | 2027-04-19 → 2027-04-22 | 4 | Pending | Sophie (6) |

  Soldes CP : 30 jours pour tous, sauf Ines Moreau (3) et Yasmine Haddad (25). Équipes : Direction (1, manager
  Claire, sans parente), Produit (2, manager Hugo, parente Direction), Support (3, manager Sophie, parente
  Direction).

---

## Phase 1: Setup (outillage de test partagé)

**Purpose** : rendre les tests d'API déterministes (horloge fixe), isolés (une base par test) et concis.

- [X] T001 [P] Créer `TestClock` dans `backend/tests/Conges.Tests/TestClock.cs` : `public sealed class TestClock : TimeProvider` avec une propriété `public DateTimeOffset Now { get; set; }` modifiable, `GetUtcNow() => Now`, `LocalTimeZone => TimeZoneInfo.Utc`, et une fabrique `public static TestClock At(string isoDate, int hour = 9)` qui renvoie ce jour à `hour`:00 UTC (research R7, sans nouveau paquet NuGet).
- [X] T002 Modifier `backend/tests/Conges.Tests/CongesApiFactory.cs` :
  - ajouter une propriété `public TimeProvider? Clock { get; init; }`. Dans `ConfigureWebHost`, si elle est renseignée, appeler `builder.ConfigureTestServices(s => s.AddSingleton<TimeProvider>(Clock))` (`using Microsoft.AspNetCore.TestHost;`) ;
  - ajouter `public async Task ArrangeAsync(Func<AppDbContext, Task> arrange)`, qui crée un scope, résout `AppDbContext`, exécute `arrange` puis `SaveChangesAsync` ;
  - ajouter `public IInterceptor[] DbInterceptors { get; init; } = [];` (`using Microsoft.EntityFrameworkCore.Diagnostics;`). S'il n'est pas vide, dans `ConfigureTestServices`, retirer le descripteur `DbContextOptions<AppDbContext>`, puis réenregistrer `services.AddDbContext<AppDbContext>(o => o.UseSqlite($"Data Source={_databasePath};Pooling=False").AddInterceptors(DbInterceptors))`.

  Garder le constructeur sans paramètre, nécessaire à `IClassFixture` dans `ReadApiTests` (research R7, R8).
- [X] T003 [P] Créer `ProblemAssert` dans `backend/tests/Conges.Tests/ProblemAssert.cs`, avec deux méthodes :
  - `static Task HasCodeAsync(HttpResponseMessage response, HttpStatusCode status, string code)` : vérifie le statut, le type de contenu `application/problem+json` et la propriété JSON `code` ;
  - `static Task IsValidationProblemAsync(HttpResponseMessage response)` : vérifie un 400, `application/problem+json`, la présence de `errors` et l'absence de `code`.
- [X] T004 Créer la classe de base `ApiTestBase` dans `backend/tests/Conges.Tests/ApiTestBase.cs` : `public abstract class ApiTestBase : IDisposable`, qui expose :
  - `protected TestClock Clock { get; } = TestClock.At("2027-03-01")` ;
  - `protected CongesApiFactory Factory`, créée paresseusement avec `Clock = Clock` (une fabrique, donc une base fraîche, par test) ;
  - `protected HttpClient As(int employeeId)`, qui délègue à `Factory.CreateClientAs` ;
  - `protected HttpClient Anonymous()` ;
  - `protected static Task<LeaveRequestDto> ReadLeaveAsync(HttpResponseMessage)`, qui désérialise avec `Json.Options` ;
  - `Dispose()`, qui libère la fabrique.

  Dépend de T001 et T002.

**Checkpoint** : `docker compose run --rm backend-tests` reste vert, les tests existants étant inchangés.

---

## Phase 2: Foundational (prérequis bloquants)

**Purpose** : briques partagées par toutes les stories : le circuit de validation, la chaîne des erreurs métier
(ADR-003), `LEAVE_NOT_FOUND` et l'identification (401 avant 400).

**⚠️ CRITICAL** : aucune story ne commence avant la fin de cette phase.

- [X] T005 [P] Écrire les tests unitaires (rouges) du circuit de validation dans `backend/tests/Conges.Tests/ApprovalCircuitTests.cs`. Construire en mémoire des `Team` et `Employee` reproduisant le seed (Direction sans parente, Produit et Support rattachées à Direction), et vérifier :
  - Lea (membre de Produit) → 2 ;
  - Hugo (manager de Produit) → 1 ;
  - Sophie (manager de Support) → 1 ;
  - Thomas (membre de Support) → 6 ;
  - Claire (manager de Direction, sans parente) → `null` ;
  - un membre non manager de Direction → 1 ;
  - une `InvalidOperationException` si `Team` n'est pas chargé.
- [X] T006 Implémenter `ApprovalCircuit` dans `backend/src/Conges.Application/LeaveRequests/ApprovalCircuit.cs` (namespace `Conges.Application.LeaveRequests`, **pas** dans `Conges.Domain`, qui ne contient que des entités et des énumérations selon le principe I) : `public static class ApprovalCircuit` avec `public static int? ApproverIdOf(Employee requester)`. Elle renvoie `team.ManagerId` si `team.ManagerId != requester.Id`, sinon `team.ParentTeam?.ManagerId`. Elle lève une `InvalidOperationException` si `requester.Team` est null, ou si `Team.ParentTeamId` est renseigné alors que `ParentTeam` n'est pas chargé (research R5). T005 doit passer au vert.
- [X] T007 [P] Ajouter à `backend/src/Conges.Application/Common/Exceptions.cs` :
  - `public enum BusinessErrorKind { RuleViolation, Forbidden, NotFound, Conflict }` ;
  - `public class BusinessRuleException(string code, BusinessErrorKind kind, string title, string? detail = null) : Exception(title)`, qui expose `Code`, `Kind`, `Title` et `Detail` (research R1, ADR-003).
- [X] T008 Créer `LeaveErrors` dans `backend/src/Conges.Application/LeaveRequests/LeaveErrors.cs` : `public static class LeaveErrors`, avec une méthode par code du référentiel `docs/wiki/Codes-d-erreur.md` (voir le tableau « Erreurs » de `data-model.md`) :

  | Méthode | Code | Nature | Titre |
  |---|---|---|---|
  | `InvalidPeriod()` | `LEAVE_INVALID_PERIOD` | `RuleViolation` | « Période de congé invalide. » |
  | `NoticeTooShort()` | `LEAVE_NOTICE_TOO_SHORT` | `RuleViolation` | « Délai de prévenance non respecté. » |
  | `InsufficientBalance(int requested, int available)` | `LEAVE_INSUFFICIENT_BALANCE` | `RuleViolation` | « Solde de congés insuffisant. » |
  | `Overlap()` | `LEAVE_OVERLAP` | `RuleViolation` | « Chevauchement avec une autre demande. » |
  | `RejectionReasonRequired()` | `LEAVE_REJECTION_REASON_REQUIRED` | `RuleViolation` | « Motif de refus absent ou trop court. » |
  | `ApproverNotAllowed()` | `LEAVE_APPROVER_NOT_ALLOWED` | `Forbidden` | « Vous n'êtes pas le valideur de cette demande. » |
  | `AlreadyDecided()` | `LEAVE_ALREADY_DECIDED` | `Conflict` | « La demande a déjà été décidée. » |
  | `NotFound(int id)` | `LEAVE_NOT_FOUND` | `NotFound` | « Demande de congé introuvable. » |

  `InsufficientBalance` a pour détail « {requested} jours demandés, {available} jours disponibles. ». Dépend de T007.
- [X] T009 Étendre `backend/src/Conges.Api/Http/GlobalExceptionHandler.cs` avec un cas `BusinessRuleException`, qui produit un `ProblemDetails` :
  - `Status` selon `Kind` : `RuleViolation` → 422, `Forbidden` → 403, `NotFound` → 404, `Conflict` → 409 ;
  - `Title` et `Detail` repris de l'exception ;
  - `Type = $"https://conges.example/errors/{code.ToLowerInvariant().Replace('_', '-')}"` ;
  - `Extensions["code"] = code`.

  Ne pas modifier les cas existants (400 FluentValidation, 404 `NotFoundException`, 401, 500). Dépend de T007.
- [X] T010 [P] Test rouge (clarification Q2, US1 sc. 29) dans `backend/tests/Conges.Tests/ReadApiTests.cs` : compléter `Unknown_leave_request_returns_problem_details_404` pour vérifier aussi `code == "LEAVE_NOT_FOUND"` via `ProblemAssert.HasCodeAsync`. Dépend de T003.
- [X] T011 Dans `backend/src/Conges.Application/LeaveRequests/LeaveRequestQueries.cs`, remplacer dans `GetAsync` le `throw new NotFoundException("Demande de congé", id)` par `throw LeaveErrors.NotFound(id)` (research R10). T010 doit passer au vert. Dépend de T008, T009 et T010.
- [X] T012 Créer `ICurrentUserGuard` dans `backend/src/Conges.Application/Common/CurrentUserGuard.cs` : `public interface ICurrentUserGuard { Task EnsureIdentifiedAsync(CancellationToken ct); }` et une implémentation `internal sealed class CurrentUserGuard(ICurrentUser currentUser, IAppDbContext db)`. Elle lit `currentUser.EmployeeId`, qui lève déjà `UnauthenticatedException` si l'en-tête est absent ou invalide, puis lève `UnauthenticatedException` si `!await db.Employees.AnyAsync(e => e.Id == id, ct)`. L'enregistrer en scoped dans `backend/src/Conges.Application/DependencyInjection.cs` (research R3, FR-021).
- [X] T013 Créer `RequireIdentifiedUserAttribute` dans `backend/src/Conges.Api/Http/RequireIdentifiedUserAttribute.cs` : `public sealed class RequireIdentifiedUserAttribute : Attribute, IAsyncAuthorizationFilter`, dont `OnAuthorizationAsync` appelle `context.HttpContext.RequestServices.GetRequiredService<ICurrentUserGuard>().EnsureIdentifiedAsync(context.HttpContext.RequestAborted)`. Les exceptions remontent au `GlobalExceptionHandler`. Le filtre s'exécute avant la liaison du modèle, donc le 401 passe avant le 400 (FR-024). Il n'accède jamais au `DbContext` directement (principe I). Ce comportement est testé par les tests 401 de chaque story. Dépend de T012.

**Checkpoint** : tous les tests sont verts, `ApprovalCircuitTests` et la 404 avec code compris.

---

## Phase 3: User Story 1 - Soumettre une demande de congé (Priority: P1) 🎯 MVP

**Goal** : `POST /api/leave-requests` crée une demande `Pending`, avec un nombre de jours ouvrables correct, ou la
refuse avec le bon code (RG-01 à RG-07).

**Independent Test** : on soumet des demandes au nom de différents collaborateurs, puis on vérifie la réponse
(201 avec `days`, ou code d'erreur) et la consultation `GET /api/leave-requests/{id}` (spec, US1).

- [X] T014 [P] [US1] Écrire les tests unitaires (rouges) de `LeaveDayCounter` dans `backend/tests/Conges.Tests/LeaveDayCounterTests.cs`, avec un `HolidayCalendarService` réel configuré avec `CompanyClosures = ["2027-05-07"]` (comme `HolidayCalendarServiceTests`).
  - `CountLeaveDays` (`[Theory]`) :

    | Période | Jours attendus |
    |---|---|
    | 03-08 → 03-12 | 5 |
    | 03-08 → 03-13 | 6 |
    | 03-11 → 03-16 | 5 |
    | 03-08 → 03-14 | 6 |
    | 05-03 → 05-08 | 3 |
    | 05-17 → 05-17 | 1 |
    | 03-14 → 03-14 | 0 |
    | 05-06 → 05-06 | 0 |

  - `CountNoticeDays(submission, start)` :

    | Soumission → début | Jours attendus |
    |---|---|
    | 03-01 → 03-04 | 2 |
    | 03-01 → 03-03 | 1 |
    | 03-04 → 03-08 | 1 |
    | 03-04 → 03-09 | 2 |
    | 05-04 → 05-10 | 1 |
    | 05-04 → 05-11 | 2 |
    | 03-01 → 03-01 | 0 |
    | 03-01 → 02-28 | 0 |

  Toutes les dates sont en 2027 (spec US1 sc. 3 à 10 et 13 à 16).
- [X] T015 [US1] Implémenter `LeaveDayCounter` dans `backend/src/Conges.Application/LeaveRequests/LeaveDayCounter.cs` : `public sealed class LeaveDayCounter(IHolidayCalendarService calendar)`, avec deux méthodes.
  - `int CountLeaveDays(DateOnly start, DateOnly end)` : jours **du lundi au samedi**, bornes incluses, hors dates de `calendar.GetHolidays(start, end)` ; 0 si `end < start`.
  - `int CountNoticeDays(DateOnly submissionDate, DateOnly startDate)` : jours **du lundi au vendredi** strictement entre les deux dates, hors dates de `calendar.GetHolidays(...)` ; 0 si l'intervalle est vide.

  Un seul appel à `GetHolidays` par méthode. **Aucun** calcul de jour férié local (ADR-004, research R4). L'enregistrer en singleton dans `backend/src/Conges.Application/DependencyInjection.cs`. T014 doit passer au vert.
- [X] T016 [P] [US1] Créer `backend/tests/Conges.Tests/SubmitLeaveRequestApiTests.cs` (`: ApiTestBase`) et y écrire les tests rouges du cas nominal, de la forme et de l'identification :
  - **RG02/RG07, sc. 1** : Lea (3) soumet `PaidLeave` du 2027-03-08 au 2027-03-12 avec `comment` « Vacances » → 201, en-tête `Location`, `status` `Pending`, `days` 5, `submittedAt == Clock.Now`, et le solde de Lea reste à 30 (`GET /api/employees/3`, FR-011).
  - **RG07, sc. 2** : `GET` sur `Location` renvoie la même demande.
  - **US2 sc. 9, partie soumission** : Claire (1) soumet `PaidLeave` du 2027-03-08 au 2027-03-12 → 201 `Pending`.
  - **ERR, sc. 27** : 400 via `ProblemAssert.IsValidationProblemAsync` pour un `type` absent, un `type` « Holiday », un `startDate` absent, un `endDate` absent, un `startDate` « 08/03/2027 » et un JSON invalide.
  - **ERR, sc. 28** : 401 sans `X-User-Id`, avec `X-User-Id: abc` et avec `X-User-Id: 999`. 401 aussi sans en-tête **et** avec un corps invalide (401 avant 400, FR-024).
- [X] T017 [US1] Créer `backend/src/Conges.Application/LeaveRequests/LeaveRequestCommands.cs` avec :
  - `public sealed record SubmitLeaveRequest(LeaveType? Type, DateOnly? StartDate, DateOnly? EndDate, string? Comment)`. Selon `data-model.md` : « Champs nullables pour détecter l'absence. Validateur FluentValidation de **forme** uniquement » ;
  - `public sealed class SubmitLeaveRequestValidator : AbstractValidator<SubmitLeaveRequest>` : `Type` `NotNull().IsInEnum()`, `StartDate` `NotNull()`, `EndDate` `NotNull()`. **Aucune** comparaison de dates ni règle métier (ADR-003, research R2) ;
  - `public interface ILeaveRequestCommands { Task<LeaveRequestDto> SubmitAsync(SubmitLeaveRequest request, CancellationToken ct); }` ;
  - l'implémentation `internal sealed class LeaveRequestCommands(IAppDbContext db, ICurrentUser currentUser, LeaveDayCounter days, TimeProvider clock, IValidator<SubmitLeaveRequest> validator)`. `SubmitAsync` valide la forme (`ValidateAndThrowAsync`), calcule `Days` avec `CountLeaveDays`, puis crée la `LeaveRequest` pour `currentUser.EmployeeId` avec `Status = Pending`, `Comment` et `SubmittedAt = clock.GetUtcNow()`. Il appelle `SaveChangesAsync`, recharge la demande avec `Employee` et `Team`, et renvoie `ToDto()`.

  Enregistrer `ILeaveRequestCommands` en scoped dans `backend/src/Conges.Application/DependencyInjection.cs`.
- [X] T018 [US1] Dans `backend/src/Conges.Api/Controllers/LeaveRequestsController.cs`, injecter `ILeaveRequestCommands`. Ajouter `[HttpPost] [RequireIdentifiedUser] public async Task<ActionResult<LeaveRequestDto>> Submit([FromBody] SubmitLeaveRequest request, CancellationToken ct)`, qui renvoie `CreatedAtAction(nameof(Get), new { id = dto.Id }, dto)`. Ajouter `[ProducesResponseType]` pour 201, 400, 401 et 422. Le controller reste mince : aucune règle. T016 doit passer au vert.
- [X] T019 [US1] Ajouter à `backend/tests/Conges.Tests/SubmitLeaveRequestApiTests.cs` les tests rouges de RG-01 et RG-02 :
  - **sc. 3 à 7 et 10** : `days` attendu de 6, 5, 6, 3, 1 et 5 (sc. 10 : `FamilyEvent` du 03-11 au 03-16 → 5) ;
  - **sc. 8** : dimanche 2027-03-14 seul → 422 `LEAVE_INVALID_PERIOD` ;
  - **sc. 9** : Ascension 2027-05-06 seule → 422 `LEAVE_INVALID_PERIOD` ;
  - **sc. 11** : `startDate` 2027-03-12 et `endDate` 2027-03-08 → 422 `LEAVE_INVALID_PERIOD`, **pas** 400 ;
  - **sc. 12** : début le 2027-02-28 (hier), en `PaidLeave` puis en `FamilyEvent` → 422 `LEAVE_INVALID_PERIOD`.
- [X] T020 [US1] Dans `SubmitAsync` (`backend/src/Conges.Application/LeaveRequests/LeaveRequestCommands.cs`), après la validation de forme :
  - **RG-01** : lever `LeaveErrors.InvalidPeriod()` si `start > end`, ou si `start < today`, avec `today = DateOnly.FromDateTime(clock.GetLocalNow().DateTime)` ;
  - **RG-02** : lever `LeaveErrors.InvalidPeriod()` si `days == 0`. Selon `data-model.md`, `Days` est « toujours `≥ 1` ».

  T019 doit passer au vert.
- [X] T021 [US1] Ajouter à `backend/tests/Conges.Tests/SubmitLeaveRequestApiTests.cs` les tests rouges de RG-03 et RG-04 :
  - **sc. 13** : début le 2027-03-04 → 201 ;
  - **sc. 14** : début le 2027-03-03 → 422 `LEAVE_NOTICE_TOO_SHORT` ;
  - **sc. 15** : `Clock.Now = TestClock.At("2027-03-04").Now`, régler avant le premier appel ; début le 2027-03-08 → 422, début le 2027-03-09 → 201 ;
  - **sc. 16** : `Clock` au 2027-05-04 ; début le 2027-05-10 → 422, début le 2027-05-11 → 201 ;
  - **sc. 17** : `FamilyEvent` commençant le 2027-03-01 (aujourd'hui) → 201.

  Utiliser des demandes d'un jour pour éviter les chevauchements.
- [X] T022 [US1] Dans `SubmitAsync` (`backend/src/Conges.Application/LeaveRequests/LeaveRequestCommands.cs`), pour `Type == PaidLeave` uniquement (**RG-03**) : lever `LeaveErrors.NoticeTooShort()` si `days.CountNoticeDays(today, start) < 2`. Aucun contrôle pour `FamilyEvent` (**RG-04**). Placer ce contrôle après RG-02 (FR-024). T021 doit passer au vert.
- [X] T023 [US1] Ajouter à `backend/tests/Conges.Tests/SubmitLeaveRequestApiTests.cs` les tests rouges de RG-05, en tant qu'Ines (5), dont le solde est de 3 jours avec 2 jours `Pending` (demande seedée n° 7), soit 1 jour disponible :
  - **sc. 18** : `PaidLeave` du 2027-03-08 au 2027-03-09 → 422 `LEAVE_INSUFFICIENT_BALANCE` ;
  - **sc. 19** : `PaidLeave` du 2027-03-08 au 2027-03-08 → 201 ;
  - **sc. 20** : `FamilyEvent` du 2027-03-08 au 2027-03-10 → 201.
- [X] T024 [US1] Dans `SubmitAsync` (`backend/src/Conges.Application/LeaveRequests/LeaveRequestCommands.cs`), pour `PaidLeave` uniquement (**RG-05**) : `available = employee.PaidLeaveBalance − Σ Days` des demandes du même collaborateur avec `Type == PaidLeave && Status == Pending`. Lever `LeaveErrors.InsufficientBalance(days, available)` si `days > available` (l'égalité est acceptée). Le solde n'est **pas** modifié à la soumission (FR-011). Placer ce contrôle après RG-03. T023 doit passer au vert.
- [X] T025 [US1] Ajouter à `backend/tests/Conges.Tests/SubmitLeaveRequestApiTests.cs` les tests rouges de RG-06. Lea crée d'abord, via l'API, une demande du 2027-03-08 au 2027-03-12, puis :
  - **sc. 21** : du 03-12 au 03-13 → 422 `LEAVE_OVERLAP` ;
  - **sc. 22** : le 03-13 seul → 201 ;
  - **sc. 23** : `FamilyEvent` le 03-10 → 422 `LEAVE_OVERLAP` ;
  - **sc. 24** : la première demande passée en `Approved` via `Factory.ArrangeAsync`, puis du 03-10 au 03-10 → 422 `LEAVE_OVERLAP` ;
  - **sc. 25** : la première demande passée en `Rejected` via `ArrangeAsync`, puis la même période → 201 ;
  - **sc. 26** : Karim (4) a une demande du 03-08 au 03-12 (créée via l'API), puis Lea demande la même période → 201.
- [X] T026 [US1] Dans `SubmitAsync` (`backend/src/Conges.Application/LeaveRequests/LeaveRequestCommands.cs`, **RG-06**) : lever `LeaveErrors.Overlap()` s'il existe une demande du même collaborateur avec `Status` `Pending` ou `Approved`, quel que soit son type, telle que `r.StartDate <= end && r.EndDate >= start`. Les demandes `Rejected` sont ignorées. C'est le dernier contrôle avant la création (FR-024). T025 doit passer au vert.
- [X] T027 [US1] Ajouter à `backend/tests/Conges.Tests/SubmitLeaveRequestApiTests.cs` le test **RG03_RG05, sc. 30** : Ines demande `PaidLeave` du 2027-03-03 au 2027-03-04 → 422 `LEAVE_NOTICE_TOO_SHORT`, et non `LEAVE_INSUFFICIENT_BALANCE`. Lancer `docker compose run --rm backend-tests` : toute la suite doit être verte.

**Checkpoint** : US1 est livrable seule, et c'est le MVP. Tous les scénarios US1 (sc. 1 à 30) sont automatisés et verts.

---

## Phase 4: User Story 2 - Approuver une demande (Priority: P2)

**Goal** : `POST /api/leave-requests/{id}/approve` passe la demande en `Approved`, enregistre la décision et débite
le solde CP. Seul le valideur peut le faire (RG-10, RG-11, RG-12, RG-14).

**Independent Test** : on approuve des demandes seedées au nom de différents utilisateurs, puis on vérifie le
statut, `decidedAt`, `decidedById` et le solde via `GET /api/employees/{id}` (spec, US2). Aucune dépendance à US1 :
la demande de Claire est créée via `Factory.ArrangeAsync`.

- [X] T028 [P] [US2] Créer `backend/tests/Conges.Tests/ApproveLeaveRequestApiTests.cs` (`: ApiTestBase`) et y écrire les tests rouges :
  - **sc. 1** : Hugo (2) approuve la n° 1 → 200 `Approved`, `decidedById` 2, `decidedAt == Clock.Now`, et le solde de Lea (3) passe de 30 à 25 ;
  - **sc. 2** : Sophie (6) approuve la n° 4 (`FamilyEvent`) → 200, et le solde de Thomas (7) reste à 30 ;
  - **sc. 3** : Claire (1) approuve la n° 3 de Hugo → 200 ;
  - **sc. 4** : Hugo approuve la n° 3 → 403 `LEAVE_APPROVER_NOT_ALLOWED` ;
  - **sc. 5** : Claire approuve la n° 1 → 403 ;
  - **sc. 6** : Sophie, puis Karim (4), approuvent la n° 1 → 403 ;
  - **sc. 7** : Sophie approuve la n° 5 (`Approved`) puis la n° 6 (`Rejected`) → 409 `LEAVE_ALREADY_DECIDED`, et le solde de Yasmine reste à 25 ;
  - **sc. 8** : la n° 9999 → 404 `LEAVE_NOT_FOUND` ;
  - **sc. 9** : une demande `Pending` de Claire insérée via `ArrangeAsync` ; Claire puis Hugo l'approuvent → 403 ;
  - **ERR** : 401 sans `X-User-Id` ;
  - **ordre** : un non-valideur qui approuve une demande déjà décidée → 403, pas 409 ; un utilisateur inconnu sur une demande inexistante → 401, pas 404.
- [X] T029 [US2] Ajouter à `backend/tests/Conges.Tests/ApproveLeaveRequestApiTests.cs` un test rouge de concurrence (Edge Case « deux décisions concurrentes », research R6) :
  - via `Factory.Services`, ouvrir deux scopes, donc deux `AppDbContext`, et charger la demande n° 1 dans chacun ;
  - dans le premier, passer `Status` à `Approved` et sauvegarder ;
  - dans le second, passer `Status` à `Rejected` : `SaveChangesAsync` doit lever `DbUpdateConcurrencyException`.

  Ajouter un second test, qui vérifie la traduction en code et l'atomicité (FR-020) :
  - créer `backend/tests/Conges.Tests/ConcurrentDecisionInterceptor.cs`, un `SaveChangesInterceptor` construit avec un identifiant de demande. Au premier `SavingChangesAsync` où une `LeaveRequest` de cet identifiant est modifiée, il exécute `eventData.Context!.Database.ExecuteSqlRawAsync("UPDATE LeaveRequests SET Status = 'Rejected' WHERE Id = {0}", id)`, ce qui simule une décision concurrente ;
  - avec une fabrique dédiée, `new CongesApiFactory { Clock = TestClock.At("2027-03-01"), DbInterceptors = [new ConcurrentDecisionInterceptor(1)] }`, Hugo (2) approuve la n° 1 → 409 `LEAVE_ALREADY_DECIDED`, et le solde de Lea reste à 30 : le débit est annulé avec la mise à jour de la demande.

  Ce second test reste rouge jusqu'à T032.
- [X] T030 [US2] Dans `backend/src/Conges.Infrastructure/Persistence/AppDbContext.cs`, configurer `request.Property(r => r.Status).HasConversion<string>().HasMaxLength(30).IsConcurrencyToken()` : il s'agit d'ajouter `.IsConcurrencyToken()`, sans changement de schéma (ADR-002). Le premier test de T029 doit passer au vert.
- [X] T031 [US2] Dans `backend/src/Conges.Application/LeaveRequests/LeaveRequestCommands.cs`, ajouter `Task<LeaveRequestDto> ApproveAsync(int id, CancellationToken ct)` à `ILeaveRequestCommands`. Écrire d'abord une méthode privée réutilisable `LoadDecidableAsync(int id, CancellationToken ct)`. Elle charge la demande **avec suivi**, avec `Include(r => r.Employee!).ThenInclude(e => e.Team!).ThenInclude(t => t.ParentTeam)`, puis applique dans cet ordre (FR-024) :
  - lever `LeaveErrors.NotFound(id)` si la demande est absente ;
  - lever `LeaveErrors.ApproverNotAllowed()` si `ApprovalCircuit.ApproverIdOf(request.Employee) != currentUser.EmployeeId`. Une valeur `null` échoue toujours (RG-10) ;
  - lever `LeaveErrors.AlreadyDecided()` si `Status != Pending` (RG-11).

  `ApproveAsync` applique ensuite :
  - `Status = Approved`, `DecidedAt = clock.GetUtcNow()`, `DecidedById = currentUser.EmployeeId` (RG-14) ;
  - si `Type == PaidLeave`, `request.Employee.PaidLeaveBalance -= request.Days` (RG-12). Pas de nouveau contrôle de solde (Edge Cases) ;
  - **un seul** `SaveChangesAsync` (FR-020). Intercepter `DbUpdateConcurrencyException` et lever `LeaveErrors.AlreadyDecided()` ;
  - renvoyer `ToDto()`.
- [X] T032 [US2] Dans `backend/src/Conges.Api/Controllers/LeaveRequestsController.cs`, ajouter `[HttpPost("{id:int}/approve")] [RequireIdentifiedUser] public Task<LeaveRequestDto> Approve(int id, CancellationToken ct)`, qui délègue à `ApproveAsync`. Ajouter `[ProducesResponseType]` pour 200, 401, 403, 404 et 409. T028 et le second test de T029 doivent passer au vert, et toute la suite aussi.

**Checkpoint** : US1 et US2 fonctionnent chacune indépendamment, et le solde n'est débité qu'à l'approbation.

---

## Phase 5: User Story 3 - Refuser une demande avec un motif (Priority: P2)

**Goal** : `POST /api/leave-requests/{id}/reject` passe la demande en `Rejected`, avec un motif d'au moins 10
caractères après réduction. Le solde est inchangé (RG-10, RG-11, RG-13, RG-14).

**Independent Test** : on refuse des demandes seedées avec différents motifs, puis on vérifie le statut,
`rejectionReason`, les informations de décision et le solde (spec, US3).

**Dépendance** : réutilise `LoadDecidableAsync` (T031) et le jeton de concurrence (T030) de US2.

- [X] T033 [P] [US3] Créer `backend/tests/Conges.Tests/RejectLeaveRequestApiTests.cs` (`: ApiTestBase`) et y écrire les tests rouges :
  - **sc. 1** : Hugo (2) refuse la n° 2 avec « Période de forte activité » → 200 `Rejected`, `rejectionReason` identique, `decidedById` 2, `decidedAt == Clock.Now`, et le solde de Karim (4) reste à 30 ;
  - **sc. 2** : motif « Trop tôt » → 422 `LEAVE_REJECTION_REASON_REQUIRED`, et un `GET` montre que la demande est toujours `Pending` ;
  - **sc. 3** : « ␣␣␣Trop tôt␣␣␣ » → 422 ;
  - **sc. 4** : corps `{}`, `{"reason":null}`, `{"reason":""}`, `{"reason":"     "}` **et corps absent** → 422, jamais 400 ;
  - **sc. 5** : « Trop court » (10 caractères) → 200 ;
  - **sc. 5 bis** : « ␣␣Trop court␣␣ » → 200, avec `rejectionReason` « Trop court » (motif réduit) ;
  - **sc. 6** : Sophie (6) refuse la n° 1 → 403 `LEAVE_APPROVER_NOT_ALLOWED` ;
  - **sc. 7** : Sophie refuse la n° 6 (`Rejected`) avec un motif valide → 409 `LEAVE_ALREADY_DECIDED` ;
  - **sc. 8** : la n° 9999 → 404 `LEAVE_NOT_FOUND` ;
  - **sc. 9** : Sophie refuse la n° 6 avec « Trop tôt » → 409, pas 422 ;
  - **sc. 10** : Hugo refuse la n° 5 (`Approved`, valideur Sophie) → 403, pas 409 ;
  - **ERR** : 401 sans `X-User-Id` ; JSON invalide → 400, y compris sur la demande n° 9999 (le 400 passe avant le 404, FR-024).
- [X] T034 [US3] Dans `backend/src/Conges.Application/LeaveRequests/LeaveRequestCommands.cs`, ajouter `public sealed record RejectLeaveRequest(string? Reason)`. Selon `data-model.md` : « Aucun validateur de forme : l'absence du motif relève de RG-13 (422) ». Ajouter `Task<LeaveRequestDto> RejectAsync(int id, RejectLeaveRequest? request, CancellationToken ct)` à `ILeaveRequestCommands`. Son implémentation :
  - appelle `LoadDecidableAsync(id, ct)`, donc 404, puis 403, puis 409 ;
  - calcule `reason = request?.Reason?.Trim()`, et lève `LeaveErrors.RejectionReasonRequired()` si `string.IsNullOrEmpty(reason) || reason.Length < 10` (RG-13, contrôlé **après** le statut) ;
  - applique `Status = Rejected`, `RejectionReason = reason` (réduit), `DecidedAt = clock.GetUtcNow()` et `DecidedById = currentUser.EmployeeId`, sans toucher au solde ;
  - fait un seul `SaveChangesAsync`, intercepte `DbUpdateConcurrencyException` et lève `AlreadyDecided`, puis renvoie `ToDto()`.
- [X] T035 [US3] Dans `backend/src/Conges.Api/Controllers/LeaveRequestsController.cs`, ajouter `[HttpPost("{id:int}/reject")] [RequireIdentifiedUser] public Task<LeaveRequestDto> Reject(int id, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] RejectLeaveRequest? request, CancellationToken ct)`, qui délègue à `RejectAsync` (research R9). Ajouter `[ProducesResponseType]` pour 200, 400, 401, 403, 404, 409 et 422. T033 doit passer au vert, et toute la suite aussi.

**Checkpoint** : approbation et refus sont complets, et les décisions sont définitives et atomiques.

---

## Phase 6: User Story 4 - Lister les demandes à décider (Priority: P3)

**Goal** : `GET /api/leave-requests/pending` renvoie exactement les demandes `Pending` dont l'utilisateur courant est
le valideur, triées par date de début (RG-10, RG-11).

**Independent Test** : on consulte la liste au nom de chaque utilisateur seedé et on la compare au circuit de
validation (spec, US4). Aucune dépendance à US2 : l'état « approuvé » est préparé via `Factory.ArrangeAsync`.

- [X] T036 [P] [US4] Créer `backend/tests/Conges.Tests/PendingLeaveRequestsApiTests.cs` (`: ApiTestBase`) et y écrire les tests rouges :
  - **sc. 1** : Hugo (2) → ids [1, 2, 7], sans la n° 3 ;
  - **sc. 2** : Claire (1) → [3] ;
  - **sc. 3** : Sophie (6) → [4, 8], sans les n° 5 et 6 ;
  - **sc. 4** : Thomas (7) → 200 avec `[]` ;
  - **sc. 5** : la n° 1 passée en `Approved` via `ArrangeAsync`, puis Hugo → [2, 7] ;
  - **sc. 6** : chaque élément a le format de `GET /{id}` (comparer à `GET /api/leave-requests/{id}`), tri par `startDate` croissante puis `id` : pour Hugo, l'ordre attendu est [1, 2, 7] (03-29, 04-12, 04-26) ;
  - **direction générale** : une demande `Pending` de Claire insérée via `ArrangeAsync` n'apparaît chez aucun des utilisateurs 1 à 9 ;
  - **ERR** : 401 sans `X-User-Id` et avec `X-User-Id: 999`.
- [X] T037 [US4] Dans `backend/src/Conges.Application/LeaveRequests/LeaveRequestQueries.cs`, ajouter `Task<IReadOnlyList<LeaveRequestDto>> ListPendingToDecideAsync(CancellationToken ct)` à `ILeaveRequestQueries`. Injecter `ICurrentUser` dans `LeaveRequestQueries`. L'implémentation :
  - charge `AsNoTracking()` les demandes `Status == Pending`, avec `Include(r => r.Employee!).ThenInclude(e => e.Team!).ThenInclude(t => t.ParentTeam)` ;
  - filtre **en mémoire** `ApprovalCircuit.ApproverIdOf(r.Employee!) == currentUser.EmployeeId` (research R5) ;
  - trie par `StartDate` puis `Id`, et projette en `ToDto()`.
- [X] T038 [US4] Dans `backend/src/Conges.Api/Controllers/LeaveRequestsController.cs`, ajouter `[HttpGet("pending")] [RequireIdentifiedUser] public Task<IReadOnlyList<LeaveRequestDto>> Pending(CancellationToken ct)`, avec `[ProducesResponseType]` pour 200 et 401. Vérifier que la route ne capture pas `{id:int}`. T036 doit passer au vert, et toute la suite aussi.

**Checkpoint** : les quatre opérations du contrat sont disponibles, et le front (partie 2) peut s'y brancher.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose** : vérifications finales de conformité et de bout en bout.

- [X] T039 Lancer `docker compose run --rm backend-tests` (projet `backend/tests/Conges.Tests`) et vérifier que **toute** la suite est verte, y compris les tests existants `ReadApiTests` et `HolidayCalendarServiceTests` (constitution IV, SC-001). Joindre la sortie à la fin de la tâche (`/superpowers:verification-before-completion`).
- [X] T040 [P] Faire la revue de conformité décrite dans `specs/001-soumission-validation-conges/quickstart.md`, § 3 :
  - `grep -rniE "easter|p[aâ]ques|pentec" backend/src --include=*.cs` ne trouve rien hors de `backend/src/Conges.Infrastructure/Calendar` (ADR-004) ;
  - aucun `AppDbContext` ni `IAppDbContext` dans `backend/src/Conges.Api` (principe I) ;
  - `SubmitLeaveRequestValidator` ne contient que des contrôles de présence et de format (ADR-003) ;
  - aucun `DateTime.Now` ni `DateTime.UtcNow` dans `backend/src` (principe V).
- [X] T041 [P] Contrôler la traçabilité : chaque scénario d'acceptation de `specs/001-soumission-validation-conges/spec.md` (US1 sc. 1 à 30, US2 sc. 1 à 9, US3 sc. 1 à 10, US4 sc. 1 à 6) correspond à au moins un test de `backend/tests/Conges.Tests/` dont le nom porte le `RG-xx` concerné (ADR-006). Compléter les tests manquants.
- [X] T042 Dérouler le parcours manuel de `specs/001-soumission-validation-conges/quickstart.md`, § 2, après `docker compose restart api`, avec `curl` sur http://localhost:5080. Vérifier chaque statut et chaque `code` attendus.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)** : aucune dépendance. T004 dépend de T001 et T002.
- **Foundational (Phase 2)** : dépend de la Phase 1 et **bloque** toutes les stories.
  - T008 et T009 dépendent de T007.
  - T011 dépend de T008, T009 et T010.
  - T013 dépend de T012.
  - T006 dépend de T005.
- **US1 (Phase 3)** : dépend de la Phase 2.
- **US2 (Phase 4)** : dépend de la Phase 2. Ses tests n'utilisent pas l'API de soumission (US1).
- **US3 (Phase 5)** : dépend de la Phase 2 **et** de T030 et T031 (le cœur de décision de US2).
- **US4 (Phase 6)** : dépend de la Phase 2. Ses tests n'utilisent pas l'API d'approbation (US2).
- **Polish (Phase 7)** : dépend de toutes les stories.

### User Story Dependencies

```text
Setup → Foundational ─┬─ US1 (P1, MVP)
                      ├─ US2 (P2) ── US3 (P2)
                      └─ US4 (P3)
```

### Within Each User Story

- Écrire le test et le voir **échouer**, puis implémenter jusqu'au vert. Ne jamais écrire le code avant le test.
- Ordre dans une story : service applicatif, puis action du controller.
- **Fichiers partagés entre stories**, à modifier séquentiellement et non en parallèle :
  - `LeaveRequestCommands.cs` (US1, US2, US3) ;
  - `LeaveRequestsController.cs` (US1 à US4) ;
  - `DependencyInjection.cs` (T012, T015, T017) ;
  - `LeaveRequestQueries.cs` (T011, T037).

### Parallel Opportunities

- **Phase 1** : T001 et T003.
- **Phase 2** : T005, T007 et T010 (trois fichiers différents). Ensuite T006 ; T008 et T009 en parallèle ; T012.
- **US1** : T014 (tests unitaires) et T016 (tests d'API) en parallèle.
- **Écriture des tests** : une fois la Phase 2 terminée, les fichiers de tests T028 (US2), T033 (US3) et T036 (US4) peuvent être rédigés en parallèle de US1, car ce sont des fichiers distincts. Leur implémentation reste séquentielle, à cause des fichiers partagés.
- **Phase 7** : T040 et T041.

---

## Parallel Example: User Story 1

```bash
# Rédiger en parallèle les deux fichiers de tests rouges de US1 :
Task: "T014 Tests unitaires LeaveDayCounter dans backend/tests/Conges.Tests/LeaveDayCounterTests.cs"
Task: "T016 Tests d'API nominal, forme et identification dans backend/tests/Conges.Tests/SubmitLeaveRequestApiTests.cs"

# Puis, séquentiellement : T015 (LeaveDayCounter) → T017 (SubmitAsync) → T018 (action POST) → T019/T020 … T027
```

## Parallel Example: après la Phase 2

```bash
# Rédiger en parallèle les fichiers de tests des stories suivantes, pendant l'implémentation de US1 :
Task: "T028 Tests d'approbation dans backend/tests/Conges.Tests/ApproveLeaveRequestApiTests.cs"
Task: "T033 Tests de refus dans backend/tests/Conges.Tests/RejectLeaveRequestApiTests.cs"
Task: "T036 Tests de la liste à décider dans backend/tests/Conges.Tests/PendingLeaveRequestsApiTests.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Phase 1 : Setup (T001 à T004).
2. Phase 2 : Foundational (T005 à T013). Elle est bloquante.
3. Phase 3 : US1 (T014 à T027), règle par règle.
4. **STOP and VALIDATE** : suite verte, puis soumission manuelle via `quickstart.md` (§ 2, « Soumettre » et
   « Quelques refus métier »).
5. Commit et push de la branche : la pull request de l'atelier se met à jour.

### Incremental Delivery

1. Setup + Foundational : socle prêt (erreurs avec code, 401, circuit de validation).
2. US1 : soumission (MVP). Commit.
3. US2 : approbation et débit du solde. Commit.
4. US3 : refus avec motif. Commit.
5. US4 : liste à décider, dont le front de la partie 2 a besoin. Commit.
6. Polish : conformité, traçabilité, quickstart. Commit et push.

---

## Notes

- 42 tâches. Chacune est petite et vérifiable par un test ou une commande.
- Écart toléré : les tests 401 du filtre `[RequireIdentifiedUser]` (T013) sont écrits avec la première story qui
  l'utilise (T016), car le filtre n'a pas de route propre à tester en Phase 2.
- Ne jamais implémenter une règle de gestion dans `SubmitLeaveRequestValidator` : elle donnerait un 400 sans code
  (ADR-003).
- Ne jamais calculer de jour férié hors de `IHolidayCalendarService` (ADR-004). Le lundi de Pentecôte est
  travaillé.
- En cas de test rouge inexpliqué : `/superpowers:systematic-debugging` avant toute correction.
