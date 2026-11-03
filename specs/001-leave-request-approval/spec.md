# Feature Specification: Soumission et validation des demandes de congé

**Feature Branch**: `julien-guillaume-damien`

**Created**: 2026-11-03 · **Révisée** : 2026-11-03 (alignement sur le wiki de l'équipe)

**Status**: Draft

**Input**: User description: "Un collaborateur peut soumettre une demande de congé ; la personne habilitée peut l'approuver ou la refuser ; le solde est mis à jour. Le tout en respectant les pratiques de l'équipe."

## Références

Documentation de l'équipe : https://github.com/abelmabrouk/ai4pdlc-exercice/wiki (elle fait foi, ADR-006).

| Référence | Objet |
|---|---|
| ADR-001 | Architecture en couches, services applicatifs, controllers minces |
| ADR-002 | SQLite, schéma sans migrations |
| ADR-003 | Format des erreurs : ProblemDetails + extension `code`, statuts 422 / 403 / 404 / 409 ; règles de gestion hors FluentValidation |
| ADR-004 | Jours fériés et fermetures exclusivement via `IHolidayCalendarService` |
| ADR-005 | Utilisateur courant via `X-User-Id` / `ICurrentUser` |
| ADR-006 | Rédaction des spécifications (cette section, traçabilité RG, codes d'erreur) |
| RG-01 à RG-07 | Règles de soumission |
| RG-10 à RG-14 | Règles de décision |
| Décompte des jours | Jours **ouvrables** (lundi → samedi, hors fériés et fermetures) |
| Circuit de validation | Valideur = N+1, ou N+2 pour un manager ; Direction validée hors application |
| Codes d'erreur | `LEAVE_INVALID_PERIOD`, `LEAVE_NOTICE_TOO_SHORT`, `LEAVE_INSUFFICIENT_BALANCE`, `LEAVE_OVERLAP`, `LEAVE_REJECTION_REASON_REQUIRED`, `LEAVE_APPROVER_NOT_ALLOWED`, `LEAVE_ALREADY_DECIDED`, `LEAVE_NOT_FOUND` |
| `ENONCE.md`, partie 1 | Contrat d'API (routes et formats non négociables) |

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Soumettre une demande de congé (Priority: P1)

Un collaborateur saisit le type (congés payés ou événement familial), une date de début, une date de fin et un
commentaire facultatif. La demande est enregistrée « en attente » avec le nombre de jours ouvrables décomptés.

**Why this priority**: point d'entrée de tout le processus.

**Independent Test**: en tant que Lea Petit, soumettre des congés payés du lundi 8 au vendredi 12 mars 2027 :
demande « en attente », 5 jours, solde inchangé.

**Acceptance Scenarios**:

1. **[RG-02, RG-07]** **Given** Lea (solde 30) sans demande sur la période, **When** elle soumet des congés payés du
   lundi 8 au vendredi 12 mars 2027 avec le commentaire « Vacances », **Then** la demande est créée « en attente »,
   5 jours décomptés, avec sa date de soumission, et le solde reste à 30.
2. **[RG-02]** **Given** une période du lundi au samedi sans jour férié, **When** elle est soumise, **Then** 6 jours
   sont décomptés (le samedi est ouvrable, le dimanche jamais ; fériés et fermetures exclus).
3. **[RG-01]** **Given** une date de fin antérieure à la date de début, ou une date de début passée, **When** la
   demande est soumise, **Then** elle est refusée avec `LEAVE_INVALID_PERIOD` et rien n'est enregistré.
4. **[RG-02]** **Given** une période sans jour décompté (un dimanche seul, un férié seul), **When** elle est soumise,
   **Then** elle est refusée avec `LEAVE_INVALID_PERIOD`.
5. **[RG-03]** **Given** une demande de congés payés soumise un lundi, **When** elle commence le mercredi, **Then** elle
   est refusée avec `LEAVE_NOTICE_TOO_SHORT` ; **When** elle commence le jeudi, **Then** elle est acceptée.
6. **[RG-04]** **Given** une demande « événement familial », **When** elle commence le jour même ou le lendemain de la
   soumission, **Then** elle est acceptée.
7. **[RG-05]** **Given** Ines Moreau (solde 3, dont 2 jours engagés par une demande en attente), **When** elle soumet
   2 jours de congés payés, **Then** elle est refusée avec `LEAVE_INSUFFICIENT_BALANCE` ; une demande « événement
   familial » de même durée est acceptée.
8. **[RG-06]** **Given** une demande en attente ou approuvée du 8 au 12 mars, **When** le même collaborateur soumet une
   demande qui la chevauche d'au moins un jour, **Then** elle est refusée avec `LEAVE_OVERLAP` ; une demande refusée
   sur la même période ne bloque pas.
9. **[Circuit de validation]** **Given** Claire Martin (Direction, équipe sans équipe parente, validée hors
   application), **When** elle soumet une demande, **Then** elle est refusée avec `LEAVE_APPROVER_NOT_ALLOWED`.

---

### User Story 2 - Consulter les demandes à décider (Priority: P2)

Le valideur consulte la liste des demandes en attente dont il est le valideur, et uniquement celles-là.

**Why this priority**: support de l'écran « Demandes en attente » (partie 2).

**Independent Test**: en tant que Hugo Bernard, la liste contient les demandes de Lea, Karim et Ines, ni la sienne
ni celles du Support.

**Acceptance Scenarios**:

1. **[RG-10]** **Given** les données de démonstration, **When** Hugo consulte sa liste, **Then** il voit uniquement les
   demandes en attente des membres de Produit autres que lui.
2. **[RG-10]** **Given** les données de démonstration, **When** Claire consulte sa liste, **Then** elle voit la demande
   de Hugo (N+2), et jamais une demande dont elle serait l'auteur.
3. **Given** un collaborateur qui n'est valideur d'aucune demande, **When** il consulte sa liste, **Then** elle est vide.
4. **[RG-11]** **Given** des demandes approuvées ou refusées, **When** un manager consulte sa liste, **Then** elles
   n'apparaissent pas.

---

### User Story 3 - Approuver une demande et mettre à jour le solde (Priority: P1)

Le valideur approuve une demande en attente ; pour des congés payés, le solde est débité à ce moment-là.

**Why this priority**: finalité du processus.

**Independent Test**: Hugo approuve la demande de 5 jours de Lea : statut « approuvée », solde de Lea à 25.

**Acceptance Scenarios**:

1. **[RG-12, RG-14]** **Given** la demande de congés payés de 5 jours de Lea en attente (solde 30), **When** Hugo
   l'approuve, **Then** elle passe « approuvée », Hugo et la date de décision sont enregistrés, et le solde passe à 25.
2. **[RG-12]** **Given** une demande « événement familial » en attente, **When** son valideur l'approuve, **Then** le
   solde est inchangé.
3. **[RG-10]** **Given** une demande de Karim, **When** Sophie Laurent tente de l'approuver, **Then** elle est refusée
   avec `LEAVE_APPROVER_NOT_ALLOWED` et la demande est inchangée.
4. **[RG-10]** **Given** sa propre demande, **When** Hugo tente de l'approuver, **Then** elle est refusée avec
   `LEAVE_APPROVER_NOT_ALLOWED`.
5. **[RG-11]** **Given** une demande déjà approuvée ou refusée, **When** son valideur tente de l'approuver, **Then**
   elle est refusée avec `LEAVE_ALREADY_DECIDED`, sans aucune modification du solde.
6. **[RG-12]** **Given** une demande de congés payés dont la durée dépasse le solde au moment de la décision, **When**
   son valideur l'approuve, **Then** elle est refusée avec `LEAVE_INSUFFICIENT_BALANCE` ; le solde n'est jamais négatif.
7. **Given** une demande inexistante, **When** une décision est tentée, **Then** la réponse est `LEAVE_NOT_FOUND`.

---

### User Story 4 - Refuser une demande avec un motif (Priority: P2)

Le valideur refuse une demande en attente avec un motif d'au moins 10 caractères ; le solde n'est pas modifié.

**Why this priority**: clôt les demandes non acceptées, sans effet sur le solde.

**Independent Test**: Hugo refuse la demande de Karim avec « Période de forte activité » : statut « refusée »,
motif enregistré, solde inchangé.

**Acceptance Scenarios**:

1. **[RG-13, RG-14]** **Given** une demande de Karim en attente, **When** Hugo la refuse avec « Période de forte
   activité », **Then** elle passe « refusée » avec le motif, l'auteur et la date ; le solde de Karim est inchangé.
2. **[RG-13]** **Given** un motif absent, ou de moins de 10 caractères espaces de début et de fin exclus (« Trop tôt »,
   « ␣␣court␣␣ »), **When** le refus est envoyé, **Then** il est rejeté avec `LEAVE_REJECTION_REASON_REQUIRED`.
3. **[RG-10, RG-11]** **Given** un utilisateur qui n'est pas le valideur, ou une demande déjà décidée, **When** un refus
   est tenté, **Then** il est rejeté avec `LEAVE_APPROVER_NOT_ALLOWED` ou `LEAVE_ALREADY_DECIDED`.

---

### Edge Cases

- **[RG-02]** Lundi de Pentecôte : travaillé dans l'entreprise, donc décompté. Fermetures (ex. 24 et 31 décembre)
  exclues, y compris à cheval sur deux années.
- **[RG-03]** Le délai se compte en jours **ouvrés** (lundi → vendredi hors fériés et fermetures), jour de soumission
  et premier jour du congé exclus. Exemple : soumission un vendredi, début au plus tôt le mercredi suivant.
- **[RG-11]** Deux décisions simultanées sur la même demande : une seule est appliquée, l'autre reçoit
  `LEAVE_ALREADY_DECIDED` ; le solde n'est débité qu'une fois.
- **[ADR-005]** Utilisateur courant absent ou invalide : 401.
- Type de congé inconnu, date mal formée, commentaire de plus de 500 caractères : erreur de forme (400, `errors`).

## Requirements *(mandatory)*

### Functional Requirements

**Soumission**

- **FR-001**: Le système DOIT permettre à l'utilisateur courant de soumettre une demande pour lui-même (type, date
  de début, date de fin, commentaire facultatif).
- **FR-002** *(RG-07)*: Toute demande créée a le statut « en attente » et l'instant de la requête comme date de
  soumission ; elle est restituée au format de la consultation d'une demande.
- **FR-003** *(RG-02, ADR-004)*: Le nombre de jours est le nombre de jours **ouvrables** entre les deux dates incluses
  (lundi → samedi, hors fériés et fermetures fournis par le calendrier de l'entreprise).
- **FR-004** *(RG-01, RG-02)*: Une demande dont la fin précède le début, dont le début est passé, ou sans jour
  décompté, DOIT être refusée avec `LEAVE_INVALID_PERIOD`.
- **FR-005** *(RG-03, RG-04)*: Une demande de congés payés DOIT laisser au moins 2 jours ouvrés entre le jour de
  soumission (exclu) et le premier jour (exclu), sinon `LEAVE_NOTICE_TOO_SHORT` ; les événements familiaux en sont
  exemptés.
- **FR-006** *(RG-05)*: Une demande de congés payés DOIT être refusée avec `LEAVE_INSUFFICIENT_BALANCE` si ses jours
  dépassent le solde disponible (solde CP − jours des autres demandes CP en attente).
- **FR-007** *(RG-06)*: Une demande qui chevauche une demande en attente ou approuvée du même collaborateur DOIT être
  refusée avec `LEAVE_OVERLAP`.
- **FR-008** *(RG-12)*: La soumission NE DOIT PAS modifier le solde.
- **FR-009** *(Circuit de validation)*: Un demandeur sans valideur dans l'application (manager d'une équipe sans
  équipe parente) NE PEUT PAS soumettre : `LEAVE_APPROVER_NOT_ALLOWED`.

**Valideur**

- **FR-010** *(RG-10, Circuit de validation)*: Le valideur d'une demande est le manager de l'équipe du demandeur ; si
  le demandeur est ce manager, c'est le manager de l'équipe parente. Personne ne décide sa propre demande ; pas de
  délégation.

**Consultation**

- **FR-011**: Le système DOIT fournir à l'utilisateur courant la liste des demandes en attente dont il est le
  valideur, et uniquement celles-ci.

**Décision**

- **FR-012** *(RG-10, RG-11)*: Seul le valideur peut décider (`LEAVE_APPROVER_NOT_ALLOWED`), et seulement une demande en
  attente (`LEAVE_ALREADY_DECIDED`) ; une décision est définitive.
- **FR-013** *(RG-12)*: L'approbation passe la demande « approuvée » et, pour des congés payés, débite le solde du
  nombre de jours dans la même opération ; elle est refusée (`LEAVE_INSUFFICIENT_BALANCE`) si le solde devenait négatif.
- **FR-014** *(RG-13)*: Le refus exige un motif d'au moins 10 caractères (espaces de début et de fin exclus), sinon
  `LEAVE_REJECTION_REASON_REQUIRED` ; la demande passe « refusée » et le solde n'est pas modifié.
- **FR-015** *(RG-14)*: La date et l'auteur de la décision sont enregistrés ; les dates proviennent de l'horloge du système.

**Erreurs**

- **FR-016** *(ADR-003, Codes d'erreur)*: Toute erreur métier est un ProblemDetails avec l'extension `code` du
  référentiel et le statut associé : 422 (règle de gestion), 403 (`LEAVE_APPROVER_NOT_ALLOWED`), 409
  (`LEAVE_ALREADY_DECIDED`), 404 (`LEAVE_NOT_FOUND`). Les erreurs de forme restent en 400 avec `errors`, sans `code`.

### Key Entities

- **Demande de congé**: demandeur, type, dates, jours décomptés, statut, commentaire, motif de refus, date de
  soumission, date et auteur de la décision.
- **Collaborateur**: équipe, solde CP (en jours ouvrables).
- **Équipe**: manager, équipe parente éventuelle ; détermine le valideur.
- **Calendrier de l'entreprise**: jours fériés et fermetures (source unique, ADR-004).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100 % des scénarios d'acceptation sont couverts par des tests automatisés qui passent, chacun citant
  les `RG-xx` qu'il couvre.
- **SC-002**: Après toute séquence de décisions, chaque solde vaut le solde initial moins les jours des congés payés
  approuvés, et n'est jamais négatif.
- **SC-003**: 0 décision possible par une autre personne que le valideur, sur toutes les combinaisons des données de
  démonstration.
- **SC-004**: 100 % des erreurs métier portent un `code` du référentiel avec le statut HTTP attendu.
- **SC-005**: Le front et les applications partenaires utilisent la fonctionnalité sans adaptation du contrat d'API.

## Assumptions

- Le contrat d'API (routes, formats, codes de succès) est celui d'`ENONCE.md` ; les statuts d'erreur suivent l'ADR-003.
- **Écart à faire valider par la DRH** : le référentiel des codes étant fermé, le refus de soumission pour la
  Direction (FR-009) réutilise `LEAVE_APPROVER_NOT_ALLOWED` (403), faute de code dédié.
- Hors périmètre (wiki) : annulation, demi-journées, report de solde, acquisition mensuelle ; délégation de décision.
