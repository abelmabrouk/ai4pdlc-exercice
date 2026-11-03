# Research: Soumission et validation des demandes de congé

**Feature**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md) | **Date**: 2026-11-03

Le contexte technique ne contenait pas d'inconnue bloquante : la stack, l'architecture et les conventions sont
fixées par la constitution et par les ADR du wiki. Cette phase tranche donc les choix de conception que la spec
laisse ouverts.

---

## R1. Erreurs métier : exception porteuse d'un code

**Decision** : ajouter dans `Conges.Application/Common` les éléments suivants.

- Une exception `BusinessRuleException(LeaveError error, string detail)`.
- Un catalogue fermé `LeaveErrors`, qui reprend un à un les codes du wiki. Chaque entrée porte le code (par exemple
  `LEAVE_OVERLAP`), son titre en français et sa catégorie `ErrorKind`.
- L'énumération `ErrorKind` : `RuleViolation`, `Forbidden`, `NotFound` et `Conflict`.

Dans `GlobalExceptionHandler`, un nouveau cas traduit la catégorie en statut (422, 403, 404 ou 409). Il produit un
ProblemDetails avec :

- `type` : `https://conges.example/errors/<code-en-kebab-case>` ;
- `title` : le titre de l'entrée du catalogue ;
- `detail` : le détail fourni à l'exception ;
- l'extension `code`.

**Rationale** :

- ADR-003 demande « une exception applicative dédiée, porteuse du code et du statut ». Passer par une catégorie
  plutôt que par un entier HTTP évite à la couche Application de dépendre de notions HTTP (principe I).
- Le catalogue fermé reflète la règle du wiki : « un code absent de ce fichier n'existe pas ».
- Le gestionnaire global existant reste le seul point de traduction (principe III).

**Alternatives considered** :

- *Une exception par code* (`OverlapException`, etc.) : huit classes pour un seul comportement.
- *Le statut HTTP en entier dans l'exception* : l'Application connaîtrait HTTP.
- *Des règles FluentValidation avec un `ErrorCode`* : elles produiraient un 400 sans extension `code`. ADR-003
  l'interdit explicitement.
- *Réutiliser `NotFoundException` pour `LEAVE_NOT_FOUND`* : elle ne porte pas de code. Elle reste utilisée par la
  consultation existante (`GET /{id}`), dont le comportement n'est pas modifié, car hors périmètre.

## R2. Forme des entrées : FluentValidation et liaison du modèle

**Decision** :

- **Soumission.** Le DTO d'entrée `SubmitLeaveRequest(LeaveType? Type, DateOnly? StartDate, DateOnly? EndDate,
  string? Comment)` a des champs nullables. Un validateur FluentValidation impose `NotNull` sur le type et sur les
  deux dates, et `IsInEnum` sur le type.
- **Valeurs mal formées.** Une date ou un type mal formés sont déjà rejetés en 400 `ValidationProblemDetails` par
  la liaison du modèle de `[ApiController]`.
- **Ce qui n'est pas de la forme.** L'ordre des dates, la date du jour et le décompte nul sont des règles de
  gestion (`LEAVE_INVALID_PERIOD`). Elles sont contrôlées dans le service, pas dans le validateur.
- **Refus.** Le corps `RejectLeaveRequest(string? Reason)` est lié avec
  `[FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)]`. Un corps absent ou un motif nul aboutissent ainsi à
  `LEAVE_REJECTION_REASON_REQUIRED` (422) et non à un 400 générique. Le contrôle de longueur (≥ 10 caractères après
  `Trim()`) est fait dans le service.

**Rationale** :

- Avec des `DateOnly` non nullables, une date absente serait silencieusement remplacée par `0001-01-01`.
- ADR-003 sépare la forme (400 sans code) des règles (422 avec code).
- RG-13 classe un motif « absent » sous `LEAVE_REJECTION_REASON_REQUIRED`.

**Alternatives considered** : contrôler l'absence de motif dans un validateur, ce qui donnerait 400 au lieu
de 422 et serait contraire au référentiel.

## R3. Décompte des jours et délai de prévenance

**Decision** : créer un service applicatif `LeaveDayCounter` (interne, singleton, dans
`Conges.Application/LeaveRequests`). Il dépend uniquement de `IHolidayCalendarService` et expose deux méthodes.

- `CountLeaveDays(start, end)` compte les jours ouvrables, bornes incluses : jour ≠ dimanche et
  `!IsHoliday`. Un seul appel à `GetHolidays(start, end)` évite un appel par jour.
- `CountWorkingDaysBetween(after, before)` compte les jours ouvrés strictement entre deux dates : du lundi au
  vendredi, hors jours fériés et fermetures. Elle sert au délai de prévenance (RG-03).

**Rationale** : ADR-004 fait du calendrier la source unique des jours non travaillés, pour les congés comme pour
les délais. Aucun service de décompte n'existe encore (principe III, vérifié dans le dépôt). Le service est pur et
déterministe, donc testable unitairement avec la même construction que `HolidayCalendarServiceTests`.

**Alternatives considered** :

- *Mettre le décompte dans `HolidayCalendarService`* : ce service gère le calendrier, pas les règles de décompte
  des congés.
- *Une méthode d'extension statique* : elle ne peut pas recevoir le calendrier par injection.

## R4. Circuit de validation

**Decision** : une fonction pure `ApprovalCircuit.ApproverOf(Employee requester)` (classe statique interne,
dans `Conges.Application/LeaveRequests`). Elle reprend l'algorithme du wiki à l'identique.

```text
équipe := requester.Team
si équipe.ManagerId = requester.Id alors valideur := équipe.ParentTeam?.ManagerId
sinon valideur := équipe.ManagerId
```

Elle retourne `int?` : `null` signifie qu'il n'y a pas de valideur (FR-011). La décision et la liste « à décider »
appellent toutes deux cette fonction, avec l'employé chargé avec `Team` puis `ParentTeam`.

**Liste « à décider »** : on charge les demandes `Pending` (avec `Employee → Team → ParentTeam`), puis on filtre en
mémoire `ApproverOf(r.Employee) == currentUser`. Le tri se fait par `StartDate`, puis par `Id`.

**Rationale** : une seule implémentation de la règle garantit que la liste et la décision ne peuvent jamais
diverger (FR-010, FR-017). Le volume, une organisation de démonstration, rend le filtrage en mémoire négligeable
(principe VII).

**Alternatives considered** : traduire la règle en prédicat EF côté base. Ce serait plus efficace à grande
échelle, mais la règle existerait en deux exemplaires (expression LINQ et code C#). Ce n'est pas justifié ici.

## R5. Date du jour et horodatages

**Decision** :

- `TimeProvider` est injecté (principe V).
- La date du jour vaut `DateOnly.FromDateTime(clock.GetLocalNow().DateTime)`, de façon cohérente avec
  `DatabaseInitializer`.
- `SubmittedAt` et `DecidedAt` valent `clock.GetUtcNow()`.

**Tests** : `CongesApiFactory` accepte une date « aujourd'hui » optionnelle. Elle remplace alors le `TimeProvider`
via `ConfigureTestServices` par un `FixedTimeProvider`, une petite classe de test qui surcharge `GetUtcNow` et
`LocalTimeZone => Utc`. Les données de démonstration se recalent sur cette date, car le seed utilise la même
horloge.

**Rationale** : les scénarios RG-03 (par exemple la soumission le mardi 2027-05-04) exigent une date du jour
maîtrisée. Une classe de dix lignes évite d'ajouter le package `Microsoft.Extensions.TimeProvider.Testing`
(principe VII).

**Alternatives considered** : `FakeTimeProvider`, qui demande une dépendance de plus pour un seul usage.

## R6. Atomicité de la décision et décisions concurrentes

**Decision** :

- Le changement de statut, l'horodatage, le décideur et le débit du solde sont modifiés sur les entités suivies,
  puis persistés par un **seul** `SaveChangesAsync`. EF Core l'exécute dans une transaction.
- `LeaveRequest.Status` est déclaré **jeton de concurrence** (`IsConcurrencyToken()` dans `AppDbContext`). Si deux
  décisions partent de la même lecture, la seconde lève `DbUpdateConcurrencyException`, que le service traduit en
  `LEAVE_ALREADY_DECIDED`.

**Rationale** : cela couvre le cas limite des « décisions simultanées » et FR-012 (« dans la même opération »)
avec une ligne de configuration du modèle. Il n'y a pas de colonne ajoutée, donc pas de migration (ADR-002).

**Alternatives considered** :

- *Une colonne `RowVersion`* : SQLite ne la gère pas nativement.
- *Une transaction explicite avec `ExecuteUpdateAsync` conditionnel* : `IAppDbContext` n'expose pas `Database`, et
  le code serait plus verbeux.

**Risque accepté** : deux **soumissions** simultanées du même collaborateur pourraient chacune passer le contrôle
du solde ou du chevauchement. Ce cas n'est pas couvert par la spec, et l'usage interactif le rend improbable.

## R7. Ordre des contrôles

**Decision** : l'ordre suit les cas limites de la spec.

- **Soumission** : forme (400), puis `LEAVE_INVALID_PERIOD`, `LEAVE_NOTICE_TOO_SHORT` (congés payés seulement),
  `LEAVE_OVERLAP` et `LEAVE_INSUFFICIENT_BALANCE` (congés payés seulement).
- **Décision** : `LEAVE_NOT_FOUND`, puis `LEAVE_APPROVER_NOT_ALLOWED` et `LEAVE_ALREADY_DECIDED`, puis, pour un
  refus, `LEAVE_REJECTION_REASON_REQUIRED`.

**Rationale** :

- On contrôle d'abord ce qui rend les contrôles suivants sans objet : une période invalide n'a pas de décompte.
- Contrôler l'habilitation avant l'état évite de révéler l'état d'une demande à une personne non habilitée.

## R8. Utilisateur identifié mais inconnu

**Decision** : si `X-User-Id` désigne un employé inexistant, la soumission lève la `NotFoundException`
(« Employé ») existante, soit un 404 sans code. La liste « à décider » renvoie une liste vide. La décision renvoie
`LEAVE_APPROVER_NOT_ALLOWED`, puisque cet utilisateur n'est le valideur de personne.

**Rationale** : ce cas n'est pas spécifié. On réutilise le comportement existant de `GET /api/me`, sans créer de
nouveau code d'erreur (le référentiel est fermé).

## R9. Stratégie de test

**Decision** :

- **Tests unitaires** : `LeaveDayCounterTests` et `ApprovalCircuitTests`, sans HTTP.
- **Tests d'API** par user story (`SubmitLeaveRequestTests`, `ApproveLeaveRequestTests`,
  `RejectLeaveRequestTests`, `PendingLeaveRequestsTests`), via `CongesApiFactory`. Chaque test cite la règle
  `RG-xx` qu'il couvre, dans son nom ou en commentaire (SC-001).
- **Isolation** : les tests qui modifient l'état créent leur propre `CongesApiFactory`, c'est-à-dire une base dédiée.
  Les tests en lecture seule partagent une fixture de classe.
- **Assertions d'erreur** : elles vérifient le statut, `application/problem+json` et l'extension `code`.

**Rationale** : c'est la constitution, principe IV, et ADR-006 (traçabilité des RG). Les soldes sont sensibles à
l'ordre d'exécution, d'où une base par test qui écrit.
