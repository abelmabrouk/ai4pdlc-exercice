# Implementation Plan: Soumission et validation des demandes de congé

**Branch**: `001-leave-request-approval` | **Date**: 2026-11-03 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/001-leave-request-approval/spec.md`

## Summary

Il s'agit d'ajouter au back-end les trois cas d'usage d'écriture (soumettre, approuver, refuser) et la liste « à
décider ». Le contrat d'API d'ENONCE.md est respecté à l'identique, de même que les règles RG-01 à RG-14 du wiki.

Approche technique :

- **Services.**
  - Un nouveau service applicatif `LeaveRequestCommands`, pour la soumission et les décisions.
  - Une méthode `ListPendingToDecideAsync` ajoutée à `LeaveRequestQueries`, existant.
  - Deux briques internes et testables unitairement :
    - `LeaveDayCounter` : jours ouvrables et jours ouvrés de prévenance. Il s'appuie uniquement sur
      `IHolidayCalendarService` (ADR-004).
    - `ApprovalCircuit` : l'algorithme du valideur, identique au wiki.
- **Erreurs.** Les erreurs métier passent par une `BusinessRuleException` porteuse d'un code issu d'un catalogue
  fermé. Le `GlobalExceptionHandler` existant les traduit en ProblemDetails, avec le statut et l'extension `code`
  (ADR-003).
- **Points d'entrée.** Le controller existant reçoit quatre actions minces.
- **Base de données.** Aucune colonne n'est ajoutée. Seul un jeton de concurrence est posé sur `Status`.

## Technical Context

**Language/Version**: C# 12 / .NET 8 (`net8.0`)

**Primary Dependencies**: ASP.NET Core 8 (controllers), EF Core 8.0.11 (SQLite), FluentValidation 11.11.0. Aucun
nouveau package.

**Storage**: SQLite, avec un schéma créé par `EnsureCreated` et une base recréée au démarrage (ADR-002). Pas de
migration.

**Testing**: xUnit 2.5.3, `Microsoft.AspNetCore.Mvc.Testing` (`CongesApiFactory`), exécutés par
`docker compose run --rm backend-tests`.

**Target Platform**: conteneur Linux `mcr.microsoft.com/dotnet/sdk:8.0` (atelier, bac à sable).

**Project Type**: service web (API REST), consommé par un front Angular existant et par des applications
partenaires.

**Performance Goals**: aucun objectif spécifique. Le volume est celui d'une organisation de démonstration
(9 collaborateurs, 3 équipes).

**Constraints**:

- Le contrat d'API d'ENONCE.md n'est pas négociable : routes, formats et statuts de succès.
- Les codes d'erreur et leurs statuts sont fermés (wiki).
- Les jours fériés proviennent exclusivement de `IHolidayCalendarService`.
- Le temps est obtenu via `TimeProvider`.

**Scale/Scope**: quatre routes HTTP, deux services applicatifs (un nouveau, un étendu) et environ six classes de
test.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principe | Vérification | Statut |
|---|---|---|
| **I. Architecture en couches** | Les règles sont dans `Application` (`LeaveRequestCommands`, `LeaveDayCounter`, `ApprovalCircuit`). Les controllers traduisent HTTP vers le service, sans logique, et ne touchent jamais au `DbContext`. Les erreurs sont classées par `ErrorKind` (sans HTTP) dans `Application`, et le statut HTTP est choisi dans `Api`. | ✅ |
| **II. Contrats explicites** | Les entrées `SubmitLeaveRequest` et `RejectLeaveRequest` sont des `record`. La sortie est `LeaveRequestDto`, existant. Le JSON est en camelCase, les énumérations en chaînes et les dates en `DateOnly`, comme dans la configuration existante. La forme est validée par FluentValidation, et les règles métier sont des exceptions codées (ADR-003). Les erreurs sont des ProblemDetails, via `GlobalExceptionHandler`. | ✅ |
| **III. Réutilisation avant création** | Ce qui est réutilisé : `IHolidayCalendarService`, `ICurrentUser`, `TimeProvider`, `LeaveRequestDto`/`ToDto()`, `GlobalExceptionHandler`, `NotFoundException` (employé inconnu), `CongesApiFactory`, `LeaveRequestsController` et `LeaveRequestQueries`. Ce qui est nouveau, faute d'équivalent dans le dépôt (vérifié) : le décompte des jours, le circuit de validation et les erreurs codées. | ✅ |
| **IV. Tests** | Des tests xUnit couvrent chaque scénario d'acceptation, avec la référence `RG-xx` (ADR-006). Les tests d'API passent par `CongesApiFactory`. Le seuil d'acceptation est `docker compose run --rm backend-tests` au vert. | ✅ |
| **V. Temps et données** | `TimeProvider` est injecté, et un `FixedTimeProvider` sert dans les tests. Pas de migration : le jeton de concurrence est une configuration du modèle, sans colonne. `ConnectionStrings:Conges` est inchangée. | ✅ |
| **VI. Front-end Angular** | Hors périmètre : le front n'est pas modifié. `LeaveRequestService` couvre déjà le contrat. | ✅ N/A |
| **VII. Simplicité** | Pas de MediatR ni de repository (ADR-001). Pas de package de test supplémentaire. Le filtrage de la liste « à décider » se fait en mémoire, avec une seule implémentation de la règle. `GET /{id}` n'est pas modifié. | ✅ |

**Résultat** : aucune violation. La section Complexity Tracking est vide.

**Re-check post-design (Phase 1)** : la conception de [data-model.md](data-model.md) et de
[contracts/](contracts/leave-requests-api.md) n'introduit ni dépendance, ni projet, ni abstraction supplémentaire.
✅

## Project Structure

### Documentation (this feature)

```text
specs/001-leave-request-approval/
├── spec.md
├── plan.md                         # ce fichier
├── research.md                     # Phase 0 : décisions R1 à R9
├── data-model.md                   # Phase 1 : entités, valeurs calculées, transitions, invariants
├── quickstart.md                   # Phase 1 : validation (tests + parcours curl)
├── contracts/
│   └── leave-requests-api.md       # Phase 1 : contrat HTTP et catalogue des codes
├── checklists/
│   └── requirements.md
└── tasks.md                        # Phase 2 (/speckit-tasks)
```

### Source Code (repository root)

```text
backend/
├── src/
│   ├── Conges.Domain/                            # inchangé
│   ├── Conges.Application/
│   │   ├── Common/
│   │   │   └── BusinessRuleException.cs          # NOUVEAU : ErrorKind, LeaveError, LeaveErrors, BusinessRuleException
│   │   ├── LeaveRequests/
│   │   │   ├── LeaveRequestQueries.cs            # MODIFIÉ : + ListPendingToDecideAsync
│   │   │   ├── LeaveRequestCommands.cs           # NOUVEAU : SubmitAsync, ApproveAsync, RejectAsync, DTOs d'entrée, validateur
│   │   │   ├── LeaveDayCounter.cs                # NOUVEAU : jours ouvrables et jours ouvrés (via IHolidayCalendarService)
│   │   │   └── ApprovalCircuit.cs                # NOUVEAU : ApproverOf(employee)
│   │   └── DependencyInjection.cs                # MODIFIÉ : enregistrement des nouveaux services
│   ├── Conges.Infrastructure/
│   │   └── Persistence/AppDbContext.cs           # MODIFIÉ : Status.IsConcurrencyToken()
│   └── Conges.Api/
│       ├── Controllers/LeaveRequestsController.cs  # MODIFIÉ : POST, GET pending, POST approve, POST reject
│       └── Http/GlobalExceptionHandler.cs          # MODIFIÉ : BusinessRuleException → ProblemDetails + code
└── tests/Conges.Tests/
    ├── CongesApiFactory.cs                       # MODIFIÉ : date du jour optionnelle (FixedTimeProvider)
    ├── FixedTimeProvider.cs                      # NOUVEAU
    ├── LeaveDayCounterTests.cs                   # NOUVEAU (unitaires)
    ├── ApprovalCircuitTests.cs                   # NOUVEAU (unitaires)
    ├── SubmitLeaveRequestTests.cs                # NOUVEAU (API, US1)
    ├── ApproveLeaveRequestTests.cs               # NOUVEAU (API, US2)
    ├── RejectLeaveRequestTests.cs                # NOUVEAU (API, US3)
    └── PendingLeaveRequestsTests.cs              # NOUVEAU (API, US4)
```

**Structure Decision**: on garde l'architecture en couches existante (`backend/src/Conges.{Domain,Application,
Infrastructure,Api}` et `backend/tests/Conges.Tests`). On n'ajoute aucun projet. Le front (`frontend/`) n'est
pas touché par cette partie.

### Points de conception clés

Le détail et les alternatives sont dans [research.md](research.md).

- **Soumission** (`SubmitAsync`) :
  1. validation de forme (400) ;
  2. chargement de l'employé courant (404 « Employé » s'il est inconnu) ;
  3. RG-01 et RG-02 (`LEAVE_INVALID_PERIOD`) ;
  4. RG-03 pour un `PaidLeave` (`LEAVE_NOTICE_TOO_SHORT`) ;
  5. RG-06 (`LEAVE_OVERLAP`) ;
  6. RG-05 pour un `PaidLeave` (`LEAVE_INSUFFICIENT_BALANCE`) ;
  7. création au statut `Pending` avec `SubmittedAt = clock.GetUtcNow()`.

  La réponse est un `201 CreatedAtAction(Get)`.
- **Décision** (`ApproveAsync` / `RejectAsync`) :
  1. chargement de la demande avec `Employee → Team → ParentTeam` (`LEAVE_NOT_FOUND`) ;
  2. `ApprovalCircuit.ApproverOf(...) == currentUser` (`LEAVE_APPROVER_NOT_ALLOWED`) ;
  3. `Status == Pending` (`LEAVE_ALREADY_DECIDED`) ;
  4. pour un refus, motif ≥ 10 caractères après `Trim()` (`LEAVE_REJECTION_REASON_REQUIRED`) ;
  5. mise à jour du statut, de `DecidedAt` et de `DecidedById`, puis débit du solde pour une approbation de
     `PaidLeave` ;
  6. un seul `SaveChangesAsync`. Une `DbUpdateConcurrencyException` est traduite en `LEAVE_ALREADY_DECIDED`.
- **Liste « à décider »** : les demandes `Pending` sont filtrées par `ApproverOf(...) == currentUser` et triées
  par `StartDate` puis par `Id`.
- **Routes** : `GET pending` est déclarée avant ou à côté de `GET {id:int}`. La contrainte `int` évite toute
  ambiguïté.

## Complexity Tracking

Aucune violation de la constitution : rien à justifier.
