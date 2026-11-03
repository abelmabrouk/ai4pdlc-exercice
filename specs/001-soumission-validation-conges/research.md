# Research — Soumission et validation des demandes de congé

Phase 0 du plan. Aucune inconnue technique ne subsistait dans le Technical Context : les décisions ci-dessous
sont prises à partir du code existant (`backend/`), de la constitution et du wiki (`docs/wiki/`).

## R1 — Erreurs métier : une exception applicative unique, traduite par le gestionnaire global

- **Decision** : ajouter dans `Conges.Application/Common/Exceptions.cs` une `BusinessRuleException` portant un
  `Code` (chaîne du référentiel), un `Kind` (`BusinessErrorKind` : `RuleViolation`, `Forbidden`, `NotFound`,
  `Conflict`), un `Title` et un `Detail` facultatif. Une classe statique `LeaveErrors`
  (`Conges.Application/LeaveRequests`) fabrique les huit erreurs du référentiel (`LeaveErrors.InvalidPeriod()`,
  `LeaveErrors.NoticeTooShort()`…), pour que les codes ne soient écrits qu'à un seul endroit.
  `GlobalExceptionHandler` traduit `Kind` en statut HTTP (422, 403, 404, 409) et ajoute l'extension `code`. Le
  champ `type` vaut `https://conges.example/errors/{code en minuscules, _ → -}`, comme dans l'exemple du wiki.
- **Rationale** : l'ADR-003 impose « une exception applicative dédiée, porteuse du code et du statut, que le
  gestionnaire global traduit en ProblemDetails ». L'énumération `Kind` évite de mettre des statuts HTTP dans la
  couche Application (ADR-001). La traduction se fait à un seul endroit, côté Api.
- **Alternatives considered** :
  - Un statut HTTP `int` dans l'exception : plus direct, mais il fait remonter un détail HTTP dans Application.
  - Une exception par code (`InsufficientBalanceException`…) : huit classes sans comportement propre, ce qui va à
    l'encontre du principe VII.
  - Réutiliser `NotFoundException` pour `LEAVE_NOT_FOUND` : elle ne porte pas de code. Elle reste utilisée pour les
    employés, pour lesquels le référentiel n'a pas de code.

## R2 — Les règles de gestion ne passent jamais par FluentValidation

- **Decision** : FluentValidation ne vérifie que la forme de `SubmitLeaveRequest` : `type` présent et dans
  l'énumération, `startDate` et `endDate` présentes. Les champs de la requête sont nullables (`LeaveType?`,
  `DateOnly?`), sinon un champ absent serait silencieusement désérialisé en valeur par défaut (`0001-01-01`).
  Toutes les règles RG-01 à RG-13 sont vérifiées dans le service applicatif et lèvent une
  `BusinessRuleException`, y compris « fin avant début » (RG-01) et « motif absent » (RG-13).
- **Rationale** : l'ADR-003 dit « Une règle de gestion ne doit pas être implémentée comme une règle
  FluentValidation : elle produirait un 400 sans code ». La spec (US1 sc. 11, US3 sc. 4) l'exige explicitement.
- **Alternatives considered** :
  - Valider `startDate ≤ endDate` dans FluentValidation, comme le fait déjà `LeaveRequestFilterValidator` : c'est
    correct pour un filtre de recherche (pure forme), mais interdit pour une demande (RG-01).
  - Laisser `[ApiController]` seul gérer les champs absents : sans nullabilité, l'absence n'est pas détectée.

## R3 — Ordre « 401 avant 400 » : un filtre d'autorisation sur les nouvelles actions

- **Decision** : un attribut `[RequireIdentifiedUser]` (`Conges.Api/Http`) implémente `IAsyncAuthorizationFilter`.
  Il appelle un service applicatif `ICurrentUserGuard.EnsureIdentifiedAsync()`, qui lit
  `ICurrentUser.EmployeeId` (en-tête absent ou mal formé → `UnauthenticatedException`) et vérifie que le
  collaborateur existe (sinon `UnauthenticatedException`). L'attribut est posé sur les quatre nouvelles actions
  uniquement.
- **Rationale** : dans ASP.NET Core, les filtres d'autorisation s'exécutent **avant** la liaison du modèle. Sans
  eux, `[ApiController]` renverrait un 400 (corps invalide) avant toute vérification d'identité, contrairement à
  FR-024. Les exceptions levées dans un filtre remontent au middleware `UseExceptionHandler`, donc au
  `GlobalExceptionHandler`, qui produit déjà le 401 en ProblemDetails. Le filtre ne touche pas au `DbContext` :
  il délègue à un service applicatif (principe I).
- **Alternatives considered** :
  - Vérifier l'identité au début de chaque service : simple, mais un corps invalide passerait avant le 401.
  - Un schéma d'authentification ASP.NET complet : disproportionné pour un en-tête de bac à sable (principe VII).
  - Un filtre global : il casserait les routes de lecture existantes, appelées sans en-tête (`ReadApiTests`).
  - Modifier `HeaderCurrentUser` pour vérifier l'existence en base : la couche Api accéderait alors aux données.

## R4 — Décompte des jours et délai de prévenance : un service unique au-dessus du calendrier

- **Decision** : créer `LeaveDayCounter` (`Conges.Application/LeaveRequests`), qui reçoit
  `IHolidayCalendarService` par injection et expose deux opérations :
  - `CountLeaveDays(start, end)` : jours ouvrables (lundi à samedi), bornes incluses, hors jours renvoyés par le
    calendrier (RG-02) ;
  - `CountNoticeDays(submissionDate, startDate)` : jours ouvrés (lundi à vendredi), strictement entre les deux
    dates, hors jours du calendrier (RG-03).

  Chaque opération appelle `GetHolidays(from, to)` une seule fois sur l'intervalle. La classe est publique et
  concrète, enregistrée en singleton (le calendrier l'est aussi), et testable unitairement avec un
  `HolidayCalendarService` réel, comme `HolidayCalendarServiceTests`.
- **Rationale** : l'ADR-004 impose que tout décompte injecte `IHolidayCalendarService`, sans liste locale ni calcul
  de Pâques. Réunir les deux calculs évite deux définitions divergentes de « jour non travaillé ». La
  distinction ouvrable/ouvré est le piège principal de la spec : elle doit être testée isolément.
- **Alternatives considered** :
  - Ajouter ces méthodes à `IHolidayCalendarService` : ce service a une responsabilité claire (le calendrier),
    et l'implémentation est en Infrastructure. Le décompte est une règle métier, il va dans Application.
  - Une interface `ILeaveDayCounter` : aucune deuxième implémentation n'est envisagée (principe VII).
  - Appeler `IsHoliday` jour par jour : correct, mais `GetHolidays` sur l'intervalle est plus direct.

## R5 — Circuit de validation : une règle pure dans la couche Application

- **Decision** : créer `ApprovalCircuit.ApproverIdOf(Employee requester)` (`Conges.Application/LeaveRequests`), une
  fonction statique pure. Elle exige `requester.Team` et `Team.ParentTeam` chargés et renvoie :
  - le manager de l'équipe, si le demandeur n'est pas ce manager ;
  - sinon, le manager de l'équipe parente ;
  - `null` si l'équipe n'a pas d'équipe parente (direction générale, clarification Q1).

  Le service de décision l'utilise pour RG-10. La requête « à décider » charge les demandes `Pending` avec
  employé, équipe et équipe parente, puis filtre en mémoire `ApproverIdOf(r.Employee) == utilisateur courant`.
- **Rationale** : une seule définition du valideur sert à la liste et à la décision, ce qui garantit la cohérence
  exigée par SC-004. La fonction est testable unitairement sans base. Le volume (application de démonstration,
  quelques dizaines de demandes) rend le filtrage en mémoire acceptable.
- **Alternatives considered** :
  - Un prédicat traduit en SQL : plus efficace à grande échelle, mais il duplique l'algorithme (une expression
    LINQ plus la fonction de décision). Ce n'est pas justifié à ce volume.
  - Une propriété calculée sur `Employee` : moins explicite sur le chargement nécessaire des équipes.
  - Placer la fonction dans `Conges.Domain` : exclu. Le principe I de la constitution réserve ce projet aux
    entités et aux énumérations (relevé par `/speckit-analyze`, C1).

## R6 — Décisions concurrentes et atomicité

- **Decision** : déclarer `LeaveRequest.Status` comme jeton de concurrence (`IsConcurrencyToken()` dans
  `AppDbContext`). Une décision modifie la demande (et le solde du collaborateur pour des congés payés approuvés),
  puis appelle `SaveChangesAsync` **une seule fois**. Une `DbUpdateConcurrencyException` est traduite en
  `LEAVE_ALREADY_DECIDED`.
- **Rationale** : EF Core exécute un `SaveChanges` dans une transaction. La demande et le solde sont donc écrits
  ensemble ou pas du tout (FR-020). Avec le jeton, l'`UPDATE` porte `WHERE Status = 'Pending'` : si une autre
  décision est passée entre-temps, aucune ligne n'est modifiée, et la seconde décision échoue proprement (Edge
  Cases). Il n'y a pas de changement de schéma (ADR-002).
- **Test** : un intercepteur EF de test (`ConcurrentDecisionInterceptor`) modifie le statut en base juste avant
  l'enregistrement. Cela vérifie la traduction en `LEAVE_ALREADY_DECIDED` et l'annulation du débit du solde
  (FR-020).
- **Alternatives considered** :
  - Une transaction explicite avec un `UPDATE` conditionnel : il faudrait exposer `Database` dans
    `IAppDbContext`. C'est plus de code pour le même résultat.
  - Ne rien faire : SQLite sérialise les écritures, mais la lecture du statut précède l'écriture. Deux décisions
    simultanées pourraient toutes deux réussir.
- **Risque accepté** : deux approbations **simultanées** de deux demandes différentes du même collaborateur
  pourraient perdre une mise à jour du solde. Ce cas n'est pas couvert par la spec (SC-005 parle d'une
  « suite » de décisions) et il est improbable, car un même valideur décide une demande à la fois.

## R7 — Le temps : TimeProvider, et une horloge fixe pour les tests

- **Decision** : les services reçoivent `TimeProvider`, déjà enregistré en singleton. La date du jour vaut
  `DateOnly.FromDateTime(clock.GetLocalNow().DateTime)`, et les horodatages (`SubmittedAt`, `DecidedAt`) valent
  `clock.GetUtcNow()`. Côté tests, une classe `TestClock : TimeProvider` (environ 10 lignes, sans nouveau
  paquet) fige l'instant et le fuseau UTC. `CongesApiFactory` accepte une horloge facultative et la substitue via
  `ConfigureTestServices`.
- **Rationale** : la constitution (principe V) interdit `DateTime.Now`. La spec date ses exemples (le lundi
  2027-03-01, le jeudi 2027-03-04, le mardi 2027-05-04) et exige une horloge maîtrisable (Assumptions). Les
  données de démonstration étant calculées à partir de la même horloge, elles restent cohérentes.
- **Alternatives considered** : le paquet `Microsoft.Extensions.TimeProvider.Testing` (`FakeTimeProvider`) :
  très bien, mais c'est une dépendance de plus pour dix lignes de code.

## R8 — Isolation des tests d'API

- **Decision** : les classes de tests qui modifient l'état (soumission, décision) créent **une fabrique par test**
  : xUnit instancie une classe de test par test, donc la fabrique est créée dans le constructeur et libérée dans
  `Dispose`. Chaque test a ainsi sa base SQLite fraîche, seedée à la date de son horloge. Les tests en lecture
  seule existants gardent `IClassFixture<CongesApiFactory>`.
- **Rationale** : une approbation dans un test (le solde de Lea passe à 25 jours) ne doit pas fausser le suivant.
  Le coût de démarrage d'une fabrique est acceptable pour quelques dizaines de tests.
- **Alternatives considered** :
  - Une fixture partagée avec des données créées par chaque test : les tests de décision reposent sur les
    demandes seedées (n° 1 à 8), et la fixture serait fragile selon l'ordre d'exécution.
  - Des transactions annulées en fin de test : c'est incompatible avec le client HTTP en mémoire.

## R9 — Corps de requête absent pour le refus

- **Decision** : l'action « refuser » lit son corps avec
  `[FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] RejectLeaveRequest? body`. Un corps absent, un
  `reason` absent, vide ou composé uniquement d'espaces donnent tous `LEAVE_REJECTION_REASON_REQUIRED` (422). Le
  motif est réduit (`Trim`) avant le contrôle de longueur et avant l'enregistrement.
- **Rationale** : par défaut, `[ApiController]` renvoie un 400 pour un corps vide, alors que RG-13 et le
  référentiel classent « motif absent » en 422 avec code (US3 sc. 4).
- **Alternatives considered** : rendre le corps obligatoire (400 si absent). C'est contraire à la spec.

## R10 — `LEAVE_NOT_FOUND` sur la consultation existante

- **Decision** : `LeaveRequestQueries.GetAsync` lève `LeaveErrors.NotFound()` au lieu de
  `NotFoundException("Demande de congé", id)`.
- **Rationale** : clarification Q2. Le test existant `Unknown_leave_request_returns_problem_details_404` ne vérifie
  que le statut 404 et le type de contenu : il reste vert.
- **Alternatives considered** : aucune, la décision vient de la clarification.
