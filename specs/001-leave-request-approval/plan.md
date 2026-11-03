# Implementation Plan: Soumission et validation des demandes de congé

**Branch**: `julien-guillaume-damien` | **Date**: 2026-11-03 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/001-leave-request-approval/spec.md`

## Summary

Ajouter au back-end la soumission d'une demande de congé, la liste des demandes à décider, l'approbation (avec
débit du solde de congés payés) et le refus motivé, selon le contrat imposé par `ENONCE.md`. Approche : un service
applicatif de commandes `LeaveRequestCommands` et une requête `PendingAsync` dans `LeaveRequestQueries`, une règle
d'habilitation unique (`LeaveApprovers`), le décompte des jours ouvrés ajouté au service calendrier existant, deux
exceptions métier (403/409) mappées par le gestionnaire global, et un jeton de concurrence sur le statut.

## Technical Context

**Language/Version**: C# / .NET 8

**Primary Dependencies**: ASP.NET Core Web API, EF Core 8 (SQLite), FluentValidation 11

**Storage**: SQLite, schéma via `EnsureCreated`, base recréée et seedée au démarrage

**Testing**: xUnit + `Microsoft.AspNetCore.Mvc.Testing` (`CongesApiFactory`), lancé par `docker compose run --rm backend-tests`

**Target Platform**: conteneur Docker Linux

**Project Type**: web-service (back-end d'une application web ; le front n'est pas modifié dans cette partie)

**Performance Goals**: usage interne, quelques dizaines d'utilisateurs ; réponse perçue immédiate

**Constraints**: contrat d'API non négociable ; constitution v1.0.0 ; pas de migration

**Scale/Scope**: 4 endpoints, ~6 fichiers modifiés, ~4 fichiers créés côté source, 3 classes de tests

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principe | Vérification | Statut |
|---|---|---|
| I. Couches | Logique dans `Application` (`LeaveRequestCommands`, `LeaveApprovers`, validateurs) ; controller limité à HTTP ↔ service ; aucun accès `DbContext` depuis l'API ; calendrier implémenté en `Infrastructure` | ✅ |
| II. Contrats | DTOs `record` (`SubmitLeaveRequestDto`, `RejectLeaveRequestDto`, `LeaveRequestDto` existant) ; camelCase, enums en chaînes, `DateOnly` ; FluentValidation ; erreurs ProblemDetails via `GlobalExceptionHandler` | ✅ |
| III. Réutilisation | Réutilise `IHolidayCalendarService` (étendu), `LeaveRequestDto`/`ToDto`, `ICurrentUser`, `NotFoundException`, `CongesApiFactory`, `LeaveRequestService` front (inchangé) | ✅ |
| IV. Tests | Tests xUnit d'API pour chaque cas d'usage + tests unitaires du décompte ; `backend-tests` doit passer | ✅ |
| V. Temps et données | `TimeProvider` injecté pour `SubmittedAt`, `DecidedAt` et « date du jour » ; pas de migration ; `ConnectionStrings:Conges` inchangé | ✅ |
| VI. Angular | Non concerné (aucune modification front) | ✅ N/A |
| VII. Simplicité | Pas de nouvelle entité, pas de repository, pas de bus ; une seule classe de commandes | ✅ |

**Re-check post-design** : ✅ conforme, sans écart au contrat d'API.

## Project Structure

### Documentation (this feature)

```text
specs/001-leave-request-approval/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── leave-requests-api.md
├── checklists/
│   └── requirements.md
└── tasks.md             # créé par /speckit-tasks
```

### Source Code (repository root)

```text
backend/src/
├── Conges.Domain/                                   # inchangé
├── Conges.Application/
│   ├── Common/
│   │   ├── Exceptions.cs                            # + ForbiddenException, ConflictException(code, message)
│   │   └── IHolidayCalendarService.cs               # + CountWorkingDays(from, to)
│   ├── LeaveRequests/
│   │   ├── LeaveRequestQueries.cs                   # + PendingAsync(ct) (demandes décidables par l'utilisateur courant)
│   │   ├── LeaveApprovers.cs                        # NOUVEAU : ApproverOf(Employee), DecidableBy(userId)
│   │   ├── LeaveRequestCommands.cs                  # NOUVEAU : SubmitAsync, ApproveAsync, RejectAsync + DTOs d'entrée
│   │   └── LeaveRequestCommandValidators.cs         # NOUVEAU : SubmitLeaveRequestValidator, RejectLeaveRequestValidator
│   └── DependencyInjection.cs                       # + ILeaveRequestCommands
├── Conges.Infrastructure/
│   ├── Calendar/HolidayCalendarService.cs           # + CountWorkingDays
│   └── Persistence/AppDbContext.cs                  # Status.IsConcurrencyToken()
└── Conges.Api/
    ├── Controllers/LeaveRequestsController.cs       # + POST /, GET /pending, POST /{id}/approve, POST /{id}/reject
    └── Http/GlobalExceptionHandler.cs               # + 403 Forbidden, 409 Conflict (extension `code`)

backend/tests/Conges.Tests/
├── HolidayCalendarServiceTests.cs                   # + cas CountWorkingDays
├── SubmitLeaveRequestTests.cs                       # NOUVEAU (US1)
├── PendingLeaveRequestsTests.cs                     # NOUVEAU (US2)
└── DecideLeaveRequestTests.cs                       # NOUVEAU (US3, US4)
```

**Structure Decision**: application web existante (`backend/` + `frontend/`) ; seul `backend/` est modifié,
en respectant les quatre couches de la constitution.

## Design notes

- **Soumission** (`SubmitAsync`) : valider la forme → charger l'employé courant avec équipe et équipe parente
  (404 s'il n'existe pas) → `Days = CountWorkingDays` (0 → 400) → chevauchement (409 `Overlap`) → solde
  disponible si `PaidLeave` (409 `InsufficientBalance`) → créer `Pending` → `SaveChanges` →
  recharger avec `Employee.Team` et renvoyer `ToDto()`. Le controller renvoie `CreatedAtAction(Get, …)`.
- **Décision** (`ApproveAsync` / `RejectAsync`) : charger la demande + employé + équipe + équipe parente
  (404) → `ApproverOf(employee) == currentUser` sinon 403 (couvre l'auto-décision du manager racine, FR-018) → `Status == Pending` sinon 409 `NotPending` →
  appliquer la transition (et le débit du solde si `PaidLeave` approuvée, 409 si solde < 0) → `SaveChanges`
  (`DbUpdateConcurrencyException` → 409 `ConcurrentDecision`).
- **Liste** (`PendingAsync`) : `Where(Status == Pending).Where(LeaveApprovers.DecidableBy(userId))`, tri
  `StartDate`, `Id`.
- Le controller reste mince : chaque action est une ligne d'appel au service.

## Complexity Tracking

Aucune violation : la conception respecte la constitution et le contrat d'API.
