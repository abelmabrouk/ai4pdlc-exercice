# Implementation Plan: Soumission et validation des demandes de congé

**Branch**: `romain` (dossier de spec : `001-soumission-validation-conges`) | **Date**: 2026-11-03 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/001-soumission-validation-conges/spec.md`

## Summary

Il s'agit d'ajouter à l'API Congés les quatre opérations du contrat de `ENONCE.md` : soumettre une demande,
lister les demandes à décider, approuver et refuser. Toutes les règles de gestion RG-01 à RG-14 sont appliquées,
et les erreurs suivent le référentiel des codes de l'ADR-003.

Approche technique, détaillée dans [research.md](research.md) :

- **Un service de commandes** `ILeaveRequestCommands` (soumettre, approuver, refuser) dans `Conges.Application`.
  La requête « à décider » est ajoutée au service de requêtes existant `ILeaveRequestQueries`. Les controllers
  restent minces (ADR-001).
- **Décompte des jours et délai de prévenance** : `LeaveDayCounter`, au-dessus du calendrier existant
  `IHolidayCalendarService` (ADR-004). Il couvre les jours ouvrables pour le décompte et les jours ouvrés pour la
  prévenance.
- **Valideur** : la fonction pure `ApprovalCircuit.ApproverIdOf` (`Conges.Application`), partagée par la liste et la
  décision.
- **Erreurs métier** : une `BusinessRuleException` (code + nature), fabriquée par `LeaveErrors` et traduite en
  ProblemDetails avec l'extension `code` par le `GlobalExceptionHandler` existant. Les règles de gestion ne
  passent jamais par FluentValidation, qui reste limité à la forme.
- **Identification (401) avant la forme (400)** : un filtre d'autorisation `[RequireIdentifiedUser]` est posé sur
  les nouvelles actions (FR-021, FR-024).
- **Atomicité et concurrence** : un seul `SaveChanges` par décision, et `Status` comme jeton de concurrence
  (FR-020, Edge Cases).
- **Aucun changement de schéma**. Les tests d'API utilisent une horloge fixe (`TestClock`) et une fabrique par test.

## Technical Context

**Language/Version**: C# 12 / .NET 8 (`net8.0`)

**Primary Dependencies**: ASP.NET Core 8 (controllers), EF Core 8.0.11 (SQLite), FluentValidation 11.11. Aucune
nouvelle dépendance.

**Storage**: SQLite (`ConnectionStrings:Conges`), schéma créé par `EnsureCreated`, base recréée et seedée au
démarrage (ADR-002). Aucune colonne ajoutée.

**Testing**: xUnit 2.5.3 et `Microsoft.AspNetCore.Mvc.Testing` (`CongesApiFactory`), exécutés par
`docker compose run --rm backend-tests`

**Target Platform**: conteneur Linux (`mcr.microsoft.com/dotnet/sdk:8.0`), via Docker Compose

**Project Type**: web service, le back-end d'une application web (le front Angular est hors périmètre)

**Performance Goals**: aucun objectif spécifique. C'est un bac à sable avec un faible volume ; les réponses
doivent être immédiates à l'échelle humaine.

**Constraints**:

- Le contrat d'API est imposé (routes, camelCase, énumérations en chaînes, dates `yyyy-MM-dd`).
- Les codes d'erreur viennent du référentiel fermé.
- Les jours non travaillés viennent exclusivement de `IHolidayCalendarService`.
- Le temps vient exclusivement de `TimeProvider`.

**Scale/Scope**: 9 collaborateurs, 3 équipes, une dizaine de demandes seedées ; 4 routes nouvelles et 1 route
modifiée

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principe / décision | Exigence | Conformité du plan | Statut |
|---|---|---|---|
| **I. Architecture en couches** | Dépendances vers l'intérieur, controllers minces, pas de `DbContext` dans un controller | Règles dans des services `Application`. `ApprovalCircuit` (règle pure) dans `Application`, car `Domain` ne contient que des entités et des énumérations. Controller : HTTP ↔ service. Le filtre `[RequireIdentifiedUser]` délègue à `ICurrentUserGuard` (`Application`). | ✅ |
| **II. Contrats explicites** | DTO `record`, camelCase, énumérations en chaînes, `DateOnly`, FluentValidation pour la forme, ProblemDetails via le gestionnaire global | `SubmitLeaveRequest` et `RejectLeaveRequest` en `record`. `LeaveRequestDto` réutilisé. Validateur de forme uniquement. `BusinessRuleException` traduite par `GlobalExceptionHandler`. Les 400 de liaison du modèle restent produits par `[ApiController]` (voir Complexity Tracking). | ✅ (dérogation documentée) |
| **III. Réutilisation avant création** | Vérifier l'existant | Réutilisés : `IHolidayCalendarService`, `ICurrentUser`, `TimeProvider`, `LeaveRequestDto`/`ToDto`, `ILeaveRequestQueries`, `GlobalExceptionHandler`, `UnauthenticatedException`, `CongesApiFactory`. Seul ajout : le décompte, qui n'existe nulle part. | ✅ |
| **IV. Tests** | xUnit, `CongesApiFactory`, `docker compose run --rm backend-tests` vert | Tests unitaires (`LeaveDayCounter`, `ApprovalCircuit`) et tests d'API par user story, avec chaque scénario d'acceptation tracé par son `RG-xx` | ✅ |
| **V. Temps et données** | `TimeProvider`, `EnsureCreated` sans migrations, clé `ConnectionStrings:Conges` inchangée | `TimeProvider` injecté, `TestClock` en test. Seule évolution du modèle : un jeton de concurrence, sans colonne. Configuration inchangée. | ✅ |
| **VI. Front-end Angular** | — | Hors périmètre (partie 2) | N/A |
| **VII. Simplicité** | Pas d'abstraction spéculative | Pas de MediatR ni de repository. Pas d'interface pour `LeaveDayCounter` (une seule implémentation). Une exception métier unique plutôt qu'une par code. Filtrage du valideur en mémoire à ce volume. | ✅ |
| **ADR-003** (wiki) | Code stable, 422/403/404/409, 400 réservé à la forme | Couvert par R1, R2 et R9 | ✅ |
| **ADR-004** (wiki) | Jours non travaillés via `IHolidayCalendarService` uniquement | `LeaveDayCounter` l'injecte. Aucun calcul de férié ajouté. | ✅ |
| **ADR-005** (wiki) | `X-User-Id` via `ICurrentUser`, jamais `HttpContext` dans les services, 401 en ProblemDetails | `ICurrentUserGuard` lit `ICurrentUser` et lève `UnauthenticatedException` | ✅ |
| **ADR-006** (wiki) | Spec avec « Références » et traçabilité des RG-xx | Spec conforme. Le nom des tests reprend le `RG-xx` couvert. | ✅ |

**Résultat avant la Phase 0** : PASS. Une dérogation mineure au principe II est documentée dans Complexity Tracking
(relevée par `/speckit-analyze`).
**Re-check après la Phase 1** (data model, contrat, quickstart) : PASS. Le design n'introduit ni projet, ni
dépendance, ni changement de schéma. Le filtre d'autorisation n'est pas une dérogation : il reste dans la couche
Api et ne fait qu'appeler un service applicatif.

## Project Structure

### Documentation (this feature)

```text
specs/001-soumission-validation-conges/
├── spec.md              # Spécification (/speckit-specify + /speckit-clarify)
├── plan.md              # Ce fichier
├── research.md          # Phase 0 : décisions techniques R1 à R10
├── data-model.md        # Phase 1 : entités, valeurs dérivées, cycle de vie, erreurs
├── quickstart.md        # Phase 1 : guide de validation
├── contracts/
│   └── leave-requests-api.md   # Phase 1 : contrat HTTP des 4 routes, plus GET /{id} modifiée
├── checklists/
│   └── requirements.md
└── tasks.md             # Phase 2 (/speckit-tasks), pas encore créé
```

### Source Code (repository root)

```text
backend/
├── src/
│   ├── Conges.Application/
│   │   ├── Common/
│   │   │   ├── Exceptions.cs                   # MODIFIÉ : + BusinessRuleException, BusinessErrorKind
│   │   │   └── CurrentUserGuard.cs             # NOUVEAU : ICurrentUserGuard (en-tête + existence → 401)
│   │   ├── LeaveRequests/
│   │   │   ├── ApprovalCircuit.cs              # NOUVEAU : ApproverIdOf(Employee) (circuit de validation)
│   │   │   ├── LeaveErrors.cs                  # NOUVEAU : fabrique des 8 erreurs du référentiel
│   │   │   ├── LeaveDayCounter.cs              # NOUVEAU : jours ouvrables (RG-02) et jours ouvrés (RG-03)
│   │   │   ├── LeaveRequestCommands.cs         # NOUVEAU : DTO, validateur de forme, ILeaveRequestCommands
│   │   │   └── LeaveRequestQueries.cs          # MODIFIÉ : + ListPendingToDecideAsync ; GetAsync → LEAVE_NOT_FOUND
│   │   └── DependencyInjection.cs              # MODIFIÉ : enregistrement des nouveaux services
│   ├── Conges.Infrastructure/
│   │   └── Persistence/AppDbContext.cs         # MODIFIÉ : Status.IsConcurrencyToken()
│   └── Conges.Api/
│       ├── Controllers/LeaveRequestsController.cs  # MODIFIÉ : + 4 actions
│       └── Http/
│           ├── GlobalExceptionHandler.cs       # MODIFIÉ : BusinessRuleException → ProblemDetails + code
│           └── RequireIdentifiedUserAttribute.cs   # NOUVEAU : filtre d'autorisation (401 avant 400)
└── tests/Conges.Tests/
    ├── TestClock.cs                            # NOUVEAU : TimeProvider figé
    ├── CongesApiFactory.cs                     # MODIFIÉ : horloge et intercepteurs EF facultatifs (ConfigureTestServices)
    ├── ConcurrentDecisionInterceptor.cs        # NOUVEAU : simule une décision concurrente (FR-020, Edge Cases)
    ├── LeaveDayCounterTests.cs                 # NOUVEAU : unitaires RG-02, RG-03
    ├── ApprovalCircuitTests.cs                 # NOUVEAU : unitaires du circuit de validation
    ├── SubmitLeaveRequestApiTests.cs           # NOUVEAU : US1
    ├── ApproveLeaveRequestApiTests.cs          # NOUVEAU : US2
    ├── RejectLeaveRequestApiTests.cs           # NOUVEAU : US3
    └── PendingLeaveRequestsApiTests.cs         # NOUVEAU : US4
```

**Structure Decision** : les quatre projets existants de `backend/` (ADR-001) suffisent. Aucun projet n'est
créé. Le front (`frontend/`) n'est pas modifié.

## Flux des cas d'usage

L'ordre des étapes est celui de FR-024. Les détails sont dans [contracts/leave-requests-api.md](contracts/leave-requests-api.md).

**Soumettre** (`ILeaveRequestCommands.SubmitAsync`) :

1. [filtre] Utilisateur identifié et existant, sinon 401.
2. [liaison du modèle + FluentValidation] `type`, `startDate`, `endDate` présents et bien formés, sinon 400.
3. RG-01 : `start ≤ end` et `start ≥ aujourd'hui` (`TimeProvider`), sinon `LEAVE_INVALID_PERIOD`.
4. RG-02 : `days = CountLeaveDays(start, end)` ; si `days = 0`, `LEAVE_INVALID_PERIOD`.
5. RG-03, pour `PaidLeave` seulement : `CountNoticeDays(aujourd'hui, start) ≥ 2`, sinon
   `LEAVE_NOTICE_TOO_SHORT` (RG-04 : aucun contrôle pour `FamilyEvent`).
6. RG-05, pour `PaidLeave` seulement : `days ≤ solde − Σ days(PaidLeave Pending)`, sinon
   `LEAVE_INSUFFICIENT_BALANCE`.
7. RG-06 : aucune demande `Pending` ou `Approved` du collaborateur ne vérifie `s ≤ end ∧ e ≥ start`, sinon
   `LEAVE_OVERLAP`.
8. RG-07 : création en `Pending`, `SubmittedAt = GetUtcNow()`. Réponse 201 avec `Location`.

**Approuver / refuser** (`ApproveAsync`, `RejectAsync`) :

1. [filtre] 401.
2. [liaison du modèle, refus uniquement] JSON invalide → 400.
3. Chargement de la demande avec employé, équipe et équipe parente, sinon `LEAVE_NOT_FOUND`.
4. RG-10 : `ApproverIdOf(employé) == utilisateur courant`, sinon `LEAVE_APPROVER_NOT_ALLOWED`. Une valeur `null`
   (direction générale) échoue toujours.
5. RG-11 : statut `Pending`, sinon `LEAVE_ALREADY_DECIDED`.
6. Refus seulement, RG-13 : `reason?.Trim()` d'au moins 10 caractères, sinon `LEAVE_REJECTION_REASON_REQUIRED`.
7. RG-12, RG-13, RG-14 : statut, motif, `DecidedAt`, `DecidedById`, et le débit du solde pour une approbation
   `PaidLeave`. Ensuite un seul `SaveChangesAsync` ; une `DbUpdateConcurrencyException` donne
   `LEAVE_ALREADY_DECIDED`.

**Lister les demandes à décider** (`ILeaveRequestQueries.ListPendingToDecideAsync`) : [filtre] 401 ; demandes
`Pending` avec employé, équipe et équipe parente ; filtre `ApproverIdOf == utilisateur courant` ; tri par
`StartDate` puis `Id`.

## Test Strategy

- **Unitaires, rapides** :
  - `LeaveDayCounterTests` : tous les exemples de décompte et de prévenance de la spec (US1 sc. 3 à 10 et 13 à
    16), avec un `HolidayCalendarService` réel et la fermeture du 2027-05-07.
  - `ApprovalCircuitTests` : N+1, manager → N+2, direction générale → `null`.
- **API, une fabrique par test** et une horloge fixée par défaut au lundi 2027-03-01 : un fichier par user story.
  - **Nommage** : le nom de chaque test reprend la règle couverte (par exemple
    `RG03_paid_leave_submitted_monday_cannot_start_wednesday`), ce qui assure la traçabilité de l'ADR-006.
  - **Contrôles** : statut HTTP, type de contenu `application/problem+json` et extension `code` pour chaque
    erreur. Pour les 400 : présence de `errors` et absence de `code`.
- **Non-régression** : `ReadApiTests` et `HolidayCalendarServiceTests` doivent rester verts.
  `Unknown_leave_request_returns_problem_details_404` reste valide avec `LEAVE_NOT_FOUND`.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| Principe II, « ProblemDetails via le gestionnaire d'exceptions global » : les 400 de liaison du modèle (JSON invalide, date mal formée, énumération inconnue) ne passent pas par `GlobalExceptionHandler`. | ASP.NET Core les produit avant l'exécution de l'action, au format ProblemDetails (`ValidationProblemDetails`, `application/problem+json`, `errors`, sans `code`), exactement comme sur les routes existantes. Le format exigé est donc respecté. | Les faire passer par le gestionnaire (`InvalidModelStateResponseFactory` qui lève une `ValidationException`) changerait le comportement de toutes les routes existantes, sans gain pour les clients : le statut et le format sont identiques, et les clients ne lisent que `code` (ADR-003). |
