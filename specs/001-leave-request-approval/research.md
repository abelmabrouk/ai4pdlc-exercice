# Research — Soumission et validation des demandes de congé

> **Révision du 2026-11-03 — alignement sur le wiki de l'équipe.** Les décisions R1, R2, R3, R4, R5 et R7 ci-dessous
> sont remplacées par la section « Révision wiki » en fin de document, qui fait foi.

Aucune inconnue technique bloquante : la pile est fixée par la constitution et le code existant. Ce document
consigne les décisions de conception et leur justification.

## R1 — Décompte des jours ouvrés

- **Decision** : ajouter `int CountWorkingDays(DateOnly from, DateOnly to)` à l'interface existante
  `IHolidayCalendarService` (Application) et l'implémenter dans `HolidayCalendarService` (Infrastructure) :
  jours du lundi au vendredi entre `from` et `to` inclus, moins les jours renvoyés par `GetHolidays`.
- **Rationale** : principe III (réutilisation) — ce service est la source de vérité du calendrier (fériés,
  fermetures, lundi de Pentecôte travaillé). Le décompte appartient à ce domaine.
- **Alternatives** : calcul dans le service de commandes (dupliquerait la connaissance du calendrier) ; nouveau
  service `WorkingDaysCalculator` (abstraction supplémentaire sans besoin, principe VII).

## R2 — Résolution de la personne habilitée

- **Decision** : règle unique exprimée dans `Conges.Application/LeaveRequests/LeaveApprovers.cs` :
  - si `employee.Id != employee.Team.ManagerId` → approbateur = `Team.ManagerId` ;
  - sinon, s'il existe une équipe parente → approbateur = `Team.ParentTeam.ManagerId` ;
  - sinon (manager de l'équipe racine) → approbateur = le demandeur lui-même (FR-018).
  Elle est exposée sous deux formes : une méthode `int? ApproverOf(Employee)` (pour approuver/refuser/soumettre)
  et un prédicat traduisible en SQL `Expression<Func<LeaveRequest,bool>> DecidableBy(int userId)` (pour la liste
  des demandes à décider), testés ensemble pour rester cohérents.
- **Rationale** : une seule définition de l'habilitation, utilisée par la liste et par les décisions (SC-003).
- **Alternatives** : charger toutes les demandes en attente puis filtrer en mémoire (simple mais ne passe pas à
  l'échelle et duplique la logique) ; table d'habilitations (hors périmètre).

## R3 — Codes HTTP et format d'erreur

- **Decision** : ajouter dans `Conges.Application/Common/Exceptions.cs` :
  - `ForbiddenException` → **403** (non habilité, décision de sa propre demande) ;
  - `ConflictException(code, message)` → **409** (demande plus en attente, chevauchement, solde insuffisant).
  Les mapper dans `GlobalExceptionHandler` en ProblemDetails, avec une extension `code`
  (`NotPending`, `Overlap`, `InsufficientBalance`, `ConcurrentDecision`) déjà prévue par `ApiProblem.code` côté front.
  Les erreurs de forme (dates, type, motif vide) restent des `ValidationException` FluentValidation → **400**.
  Demande introuvable → `NotFoundException` existante → **404** ; en-tête absent → **401** existant.
- **Rationale** : principe II (ProblemDetails via le gestionnaire global), FR-017 (catégories distinctes).
- **Alternatives** : 422 pour les règles métier (moins usuel dans le projet, aucun précédent) ; 400 pour tout
  (ne distingue pas les catégories).

## R4 — Validation des entrées

- **Decision** : validateurs FluentValidation dans `Application` :
  - `SubmitLeaveRequestValidator` : `type` défini (enum), `endDate >= startDate`, `startDate >= aujourd'hui`
    (via `TimeProvider` injecté), `comment` ≤ 500 caractères ;
  - `RejectLeaveRequestValidator` : `reason` non vide (après trim) et ≤ 500 caractères.
  Les règles qui dépendent des données (0 jour ouvré, chevauchement, solde) sont vérifiées dans le service.
  « 0 jour ouvré » renvoie une `ValidationException` (400, champ `endDate`) car c'est une saisie invalide.
- **Rationale** : principe II (validation de forme par FluentValidation), principe V (`TimeProvider`).
- **Note** : un `type` inconnu est rejeté en 400 par la désérialisation (comportement ASP.NET existant).

## R5 — Atomicité et décisions concurrentes

- **Decision** : le changement de statut et le débit du solde sont enregistrés dans un même `SaveChangesAsync`
  (transaction implicite EF Core). `LeaveRequest.Status` est déclaré jeton de concurrence
  (`IsConcurrencyToken()` dans `AppDbContext`) : une seconde décision sur une demande déjà décidée échoue avec
  `DbUpdateConcurrencyException`, traduite en `ConflictException("ConcurrentDecision")` dans le service.
- **Rationale** : garantit « une seule décision appliquée, solde débité une seule fois » (cas limite de la spec)
  sans verrou ni migration (principe V : le schéma suit le modèle, `EnsureCreated`).
- **Alternatives** : transaction explicite sérialisable (non exposée par `IAppDbContext`, plus lourde) ;
  ignorer la concurrence (contredit la spec).

## R6 — Solde disponible à la soumission

- **Decision** : disponible = `PaidLeaveBalance` − somme des `Days` des demandes `PaidLeave` `Pending` du
  collaborateur. Contrôle uniquement pour `PaidLeave`. À l'approbation, contrôle contre `PaidLeaveBalance` seul.
- **Rationale** : FR-006 et FR-012 ; évite d'engager plus de jours que le solde.

## R7 — Demandes du manager de l'équipe racine (FR-018)

- **Decision** : aucune règle spéciale à la soumission : la demande est créée `Pending`. L'habilitation (R2)
  désigne le demandeur lui-même comme approbateur ; la demande apparaît donc dans sa liste et il la décide via les
  mêmes routes et règles (solde, statut, concurrence).
- **Rationale** : choix du métier ; respecte strictement le contrat d'`ENONCE.md` (`Pending` à la création).
- **Alternatives** : approbation automatique à la soumission (renvoyait `Approved`, écart au contrat) ; demande
  bloquée sans décideur ; soumission interdite.

## R8 — Tests

- **Decision** : tests d'API xUnit avec `CongesApiFactory` (une base par classe de test via `IClassFixture`) :
  `SubmitLeaveRequestTests`, `PendingLeaveRequestsTests`, `DecideLeaveRequestTests`. Tests unitaires de
  `CountWorkingDays` ajoutés à `HolidayCalendarServiceTests` existant.
- **Point d'attention** : les données de démonstration sont relatives à la date du jour ; les tests qui créent des
  demandes utilisent des dates futures fixes (2027) choisies hors des demandes seedées, et des tests qui modifient
  l'état utilisent leur propre fixture pour ne pas interférer.

## Révision wiki (fait foi)

- **W1 — Décompte (RG-02, ADR-004).** `IHolidayCalendarService.CountWorkableDays` : jours **ouvrables** (lundi →
  samedi, hors fériés et fermetures). `CountWorkingDays` (jours ouvrés, lundi → vendredi) est conservé pour le délai
  de prévenance. Les deux partagent la même boucle ; aucun jour férié n'est calculé ailleurs.
- **W2 — Prévenance (RG-03, RG-04).** Congés payés : `CountWorkingDays(aujourd'hui + 1, début − 1) ≥ 2`. Événement
  familial : exempté. « Aujourd'hui » vient de `TimeProvider` ; les tests figent l'horloge via
  `CongesApiFactory { Now = … }` (`FixedTimeProvider`).
- **W3 — Erreurs (ADR-003, « Codes d'erreur »).** Une seule exception applicative `BusinessException(code, kind,
  title, detail)` ; `BusinessErrorKind` (RuleViolation, Forbidden, NotFound, Conflict) est traduit par
  `GlobalExceptionHandler` en 422 / 403 / 404 / 409, avec l'extension `code` et un `type` stable. Les codes et leurs
  fabriques sont dans `LeaveErrors` (liste fermée du wiki). `ForbiddenException`, `ConflictException` et
  `ConflictCodes` sont supprimés.
- **W4 — Validation.** FluentValidation ne porte plus que la forme (type connu, longueurs maximales de 500). RG-01,
  RG-02 et RG-13 sont vérifiées dans `LeaveRequestCommands` et produisent un code métier.
- **W5 — Concurrence (RG-11).** La décision concurrente (jeton de concurrence sur `Status`) renvoie
  `LEAVE_ALREADY_DECIDED`, le code `ConcurrentDecision` n'existant pas dans le référentiel.
- **W6 — Circuit de validation.** `LeaveApprovers.ApproverOf` renvoie `null` pour le manager d'une équipe sans équipe
  parente (Direction, validée hors application) ; `DecidableBy` ne renvoie jamais une demande de l'utilisateur. Une
  soumission sans valideur est refusée en 403 `LEAVE_APPROVER_NOT_ALLOWED` : **code réutilisé faute de code dédié,
  écart à remonter à la DRH** (le référentiel est fermé et maintenu dans le wiki).
- **W7 — Refus (RG-13).** Motif `Trim()` d'au moins 10 caractères, sinon 422 `LEAVE_REJECTION_REASON_REQUIRED` ;
  évalué après 404 / 403 / 409.
