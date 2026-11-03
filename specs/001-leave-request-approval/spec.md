# Feature Specification: Soumission et validation des demandes de congé

**Feature Branch**: `001-leave-request-approval`

**Created**: 2026-11-03

**Status**: Draft

**Input**: User description: "Partie 1 de ENONCE.md — Un collaborateur peut soumettre une demande de congé ; la personne habilitée peut l'approuver ou la refuser ; le solde est mis à jour. Le tout en respectant les pratiques de l'équipe." Spec reprise en s'appuyant sur la documentation de l'équipe (wiki du projet).

## Références

*Section obligatoire (ADR-006). La documentation de l'équipe fait foi : elle prime sur toute interprétation de
bon sens.*

**Règles de gestion** ([Règles de gestion](https://github.com/abelmabrouk/ai4pdlc-exercice/wiki/Regles-de-gestion)) :

| Règle | Objet | Couverte par |
|---|---|---|
| RG-01 | Période continue, début non passé | FR-003, US1 |
| RG-02 | Décompte en jours ouvrables, période sans jour décompté invalide | FR-002, FR-003, US1 |
| RG-03 | Délai de prévenance de 2 jours ouvrés pour les congés payés | FR-004, US1 |
| RG-04 | Pas de délai de prévenance pour les événements familiaux | FR-004, US1 |
| RG-05 | Solde disponible = solde CP − demandes CP en attente | FR-005, FR-006, US1 |
| RG-06 | Pas de chevauchement avec une demande en attente ou approuvée | FR-007, US1 |
| RG-07 | Statut initial `Pending`, horodatage de soumission | FR-008, US1 |
| RG-10 | Seul le valideur décide | FR-009 à FR-011, US2, US3, US4 |
| RG-11 | Seule une demande en attente peut être décidée, décision définitive | FR-014, US2, US3 |
| RG-12 | Approbation : débit du solde CP à ce moment-là | FR-012, US2 |
| RG-13 | Refus : motif obligatoire d'au moins 10 caractères | FR-013, US3 |
| RG-14 | Traçabilité de la décision (date, auteur) | FR-015, US2, US3 |

**Documentation métier** :
[Décompte des jours](https://github.com/abelmabrouk/ai4pdlc-exercice/wiki/Decompte-des-jours),
[Circuit de validation](https://github.com/abelmabrouk/ai4pdlc-exercice/wiki/Circuit-de-validation),
[Glossaire](https://github.com/abelmabrouk/ai4pdlc-exercice/wiki/Glossaire),
[Codes d'erreur](https://github.com/abelmabrouk/ai4pdlc-exercice/wiki/Codes-d-erreur).

**Décisions d'architecture applicables** :

- **ADR-001**, architecture en couches : chaque cas d'usage est un service applicatif, et les points d'entrée
  restent minces.
- **ADR-002**, persistance SQLite sans migrations : le schéma suit le modèle, et la base est recréée au démarrage.
- **ADR-003**, format des erreurs : chaque erreur métier porte un `code` stable et un statut normalisé. Une règle de
  gestion n'est jamais traitée comme une erreur de forme.
- **ADR-004**, calendrier des jours non travaillés : il est la source unique des jours fériés et des fermetures,
  pour le décompte comme pour le délai de prévenance.
- **ADR-005**, identification de l'utilisateur : l'utilisateur courant est porté par l'identification existante.
  S'il est absent ou invalide, l'opération est refusée.
- **ADR-006**, rédaction des spécifications : la spec contient cette section, les scénarios citent les `RG-xx` et
  les erreurs citent leur code.

**Contrat d'API** : il est imposé par ENONCE.md (Partie 1) et n'est pas négociable.

## Clarifications

### Session 2026-11-03

- Q: Qui décide des demandes du plus haut responsable (Claire, manager de la Direction, sans équipe parente) ?
  → A (initiale) : elle décide elle-même de ses propres demandes. **Remplacée** par la documentation
  ([Circuit de validation](https://github.com/abelmabrouk/ai4pdlc-exercice/wiki/Circuit-de-validation)), qui fait
  foi : personne ne valide ses propres congés, et les demandes de la direction générale sont validées hors
  application. Dans l'application, ces demandes n'ont donc pas de valideur (voir FR-011).
- Q: Le contrôle du solde à la soumission tient-il compte des congés payés en attente ? → A: Oui. Cette réponse est
  confirmée par RG-05 et par le glossaire (« solde disponible »).

## User Scenarios & Testing *(mandatory)*

*Sauf mention contraire, les exemples supposent la date du jour assez éloignée de la période demandée pour que le
délai de prévenance soit respecté. Les dates de 2027 tiennent compte du calendrier de l'entreprise : Ascension le
jeudi 6 mai, fermeture le vendredi 7 mai, Victoire 1945 le samedi 8 mai, et lundi de Pentecôte (17 mai)
travaillé.*

### User Story 1 - Soumettre une demande de congé (Priority: P1)

Un collaborateur choisit un type de congé (congés payés ou événement familial), une date de début, une date de fin
et, s'il le souhaite, un commentaire. La demande est enregistrée en attente de décision, avec le nombre de jours
ouvrables qu'elle décompte.

**Why this priority**: Sans soumission, il n'y a rien à valider ni de solde à mettre à jour. C'est le point
d'entrée de tout le processus.

**Independent Test**: Se connecter en tant que Léa (solde 30 jours), soumettre une demande de congés payés du lundi
8 au vendredi 12 mars 2027, puis consulter la demande : elle est en attente et décompte 5 jours.

**Acceptance Scenarios**:

*Création et décompte*

1. **[RG-02, RG-07]** **Given** Léa avec un solde de 30 jours, **When** elle soumet une demande de congés payés du
   2027-03-08 (lundi) au 2027-03-12 (vendredi) avec le commentaire « Vacances », **Then** la demande est créée au
   statut `Pending` avec 5 jours décomptés, le commentaire est conservé, et la date de soumission est l'instant de
   la requête. La demande est restituée au même format qu'une consultation.
2. **[RG-02]** **Given** une période du lundi 2027-03-08 au samedi 2027-03-13, **When** elle est soumise, **Then**
   la demande décompte 6 jours (le samedi est un jour ouvrable).
3. **[RG-02]** **Given** une période du vendredi 2027-03-12 au lundi 2027-03-15, **When** elle est soumise,
   **Then** la demande décompte 3 jours (vendredi, samedi et lundi ; le dimanche n'est jamais décompté).
4. **[RG-02]** **Given** une période du lundi 2027-05-03 au samedi 2027-05-08, **When** elle est soumise, **Then**
   la demande décompte 3 jours : le jeudi 6 mai (Ascension), le vendredi 7 mai (fermeture) et le samedi 8 mai
   (Victoire 1945) ne sont pas décomptés.
5. **[RG-02]** **Given** une demande pour le seul lundi de Pentecôte, 2027-05-17, **When** elle est soumise,
   **Then** la demande décompte 1 jour (journée de solidarité travaillée).
6. **[RG-02]** **Given** une demande pour événement familial du 2027-03-08 au 2027-03-13, **When** elle est
   soumise, **Then** elle décompte 6 jours, comme des congés payés.

*Période invalide*

7. **[RG-01]** **Given** une date de fin antérieure à la date de début, **When** la demande est soumise, **Then**
   elle est refusée avec le code `LEAVE_INVALID_PERIOD` et rien n'est enregistré.
8. **[RG-01]** **Given** une date de début antérieure à la date du jour, **When** la demande est soumise, **Then**
   elle est refusée avec le code `LEAVE_INVALID_PERIOD`.
9. **[RG-02]** **Given** une période sans aucun jour ouvrable (le seul dimanche 2027-03-14, ou le seul jeudi
   2027-05-06, férié), **When** elle est soumise, **Then** elle est refusée avec le code `LEAVE_INVALID_PERIOD`.

*Délai de prévenance*

10. **[RG-03]** **Given** la date du jour est un lundi d'une semaine sans jour férié ni fermeture, **When** un
    collaborateur soumet une demande de congés payés commençant le mercredi, **Then** elle est refusée avec le code
    `LEAVE_NOTICE_TOO_SHORT`. **When** la demande commence le jeudi, **Then** elle est acceptée (mardi et mercredi
    sont les deux jours ouvrés de prévenance).
11. **[RG-03]** **Given** la date du jour est le mardi 2027-05-04, **When** un collaborateur soumet une demande de
    congés payés commençant le lundi 2027-05-10, **Then** elle est refusée avec le code `LEAVE_NOTICE_TOO_SHORT` :
    seul le mercredi 5 mai est un jour ouvré, car l'Ascension, la fermeture, le samedi et le dimanche ne comptent
    pas. **When** la demande commence le mardi 2027-05-11, **Then** elle est acceptée.
12. **[RG-04]** **Given** la date du jour, **When** un collaborateur soumet une demande pour événement familial
    commençant le jour même ou le lendemain, **Then** elle est acceptée.

*Solde*

13. **[RG-05]** **Given** Inès avec un solde de 3 jours et une demande de congés payés de 2 jours déjà en attente
    (solde disponible : 1 jour), **When** elle soumet une demande de congés payés de 2 jours, **Then** elle est
    refusée avec le code `LEAVE_INSUFFICIENT_BALANCE`. **When** elle soumet une demande de congés payés de 1 jour,
    **Then** elle est acceptée.
14. **[RG-05]** **Given** Inès avec un solde de 3 jours, **When** elle soumet une demande pour événement familial de
    5 jours, **Then** la demande est acceptée (le solde n'est pas concerné).

*Chevauchement*

15. **[RG-06]** **Given** Léa a une demande en attente ou approuvée du 2027-03-08 au 2027-03-12, **When** elle
    soumet une demande du 2027-03-12 au 2027-03-15, **Then** elle est refusée avec le code `LEAVE_OVERLAP` (un seul
    jour commun suffit).
16. **[RG-06]** **Given** Léa a une demande refusée du 2027-03-08 au 2027-03-12, **When** elle soumet une demande
    sur la même période, **Then** la demande est acceptée.

*Forme et identification*

17. **Given** un type de congé inconnu, ou une date absente ou mal formée, **When** la demande est soumise,
    **Then** elle est refusée comme requête mal formée, avec le détail des champs en erreur et sans code métier.
18. **Given** aucun utilisateur identifié, **When** une demande est soumise, **Then** elle est refusée pour
    défaut d'identification.

---

### User Story 2 - Approuver une demande (Priority: P1)

Le valideur d'une demande en attente l'approuve. La demande passe au statut `Approved`, la décision est tracée
(qui, quand) et, pour des congés payés, le solde CP du demandeur est débité du nombre de jours décomptés.

**Why this priority**: L'approbation donne sa valeur à la demande et déclenche la mise à jour du solde, qui est
l'objectif métier explicite de la fonctionnalité.

**Independent Test**: Se connecter en tant que Hugo (manager de l'équipe Produit), approuver la demande en attente
de Léa (5 jours de congés payés), puis vérifier que la demande est approuvée et que le solde de Léa est passé de
30 à 25 jours.

**Acceptance Scenarios**:

1. **[RG-10, RG-12, RG-14]** **Given** une demande de congés payés de 5 jours de Léa, en attente, et Léa avec un
   solde de 30 jours, **When** Hugo, son valideur, l'approuve, **Then** la demande passe au statut `Approved`,
   Hugo est enregistré comme auteur de la décision avec sa date, le solde de Léa passe à 25 jours, et la demande
   mise à jour est restituée.
2. **[RG-12]** **Given** une demande pour événement familial de Thomas, en attente, **When** Sophie, sa valideuse,
   l'approuve, **Then** la demande est approuvée et le solde de Thomas est inchangé.
3. **[RG-10]** **Given** la demande en attente de Hugo (manager de Produit), **When** Claire (manager de
   l'équipe parente) l'approuve, **Then** l'approbation est acceptée. **When** Hugo tente d'approuver sa propre
   demande, **Then** il est refusé avec le code `LEAVE_APPROVER_NOT_ALLOWED`.
4. **[RG-10]** **Given** une demande en attente de Léa, **When** Sophie (manager d'une autre équipe) ou Claire
   (N+2, sans délégation) tente de l'approuver, **Then** elle est refusée avec le code
   `LEAVE_APPROVER_NOT_ALLOWED` et rien ne change.
5. **[RG-11]** **Given** une demande déjà approuvée ou refusée, **When** son valideur tente de l'approuver,
   **Then** il est refusé avec le code `LEAVE_ALREADY_DECIDED` et rien ne change.
6. **Given** une demande inexistante, **When** quelqu'un tente de l'approuver, **Then** il est refusé avec le code
   `LEAVE_NOT_FOUND`.

---

### User Story 3 - Refuser une demande avec un motif (Priority: P2)

Le valideur refuse une demande en attente en indiquant un motif d'au moins 10 caractères. La demande passe au
statut `Rejected`, le motif et la décision sont tracés, et le solde n'est pas modifié.

**Why this priority**: Indispensable pour clore une demande, mais elle n'a pas d'effet sur le solde. Le processus
reste utilisable sans elle, en mode dégradé.

**Independent Test**: Se connecter en tant que Hugo, refuser la demande en attente de Karim avec le motif
« Période de forte activité », puis vérifier que la demande est refusée, que le motif est conservé et que le solde
de Karim est inchangé.

**Acceptance Scenarios**:

1. **[RG-10, RG-13, RG-14]** **Given** une demande en attente de Karim, **When** Hugo la refuse avec le motif
   « Période de forte activité », **Then** la demande passe au statut `Rejected`, le motif est conservé, Hugo est
   enregistré comme auteur de la décision avec sa date, le solde de Karim est inchangé, et la demande mise à jour
   est restituée.
2. **[RG-13]** **Given** une demande en attente, **When** son valideur la refuse sans motif, avec un motif vide,
   ou avec un motif de moins de 10 caractères une fois les espaces de début et de fin retirés (par exemple
   « `   Non merci  ` », soit 9 caractères), **Then** le refus est rejeté avec le code
   `LEAVE_REJECTION_REASON_REQUIRED` et rien ne change.
3. **[RG-13]** **Given** une demande en attente, **When** son valideur la refuse avec un motif d'exactement
   10 caractères après suppression des espaces de début et de fin, **Then** le refus est accepté.
4. **[RG-11]** **Given** une demande déjà décidée, **When** son valideur tente de la refuser, **Then** il est
   refusé avec le code `LEAVE_ALREADY_DECIDED`.
5. **[RG-10]** **Given** une demande en attente, **When** une personne qui n'est pas son valideur tente de la
   refuser, **Then** elle est refusée avec le code `LEAVE_APPROVER_NOT_ALLOWED`.
6. **Given** une demande inexistante, **When** quelqu'un tente de la refuser, **Then** il est refusé avec le code
   `LEAVE_NOT_FOUND`.

---

### User Story 4 - Lister les demandes à décider (Priority: P2)

Un valideur consulte la liste des demandes en attente dont il est le valideur, pour les traiter.

**Why this priority**: Elle facilite le travail du manager et sert de base à l'écran de la partie 2. On peut
cependant décider d'une demande connue sans cette liste.

**Independent Test**: Se connecter en tant que Hugo et vérifier que la liste contient exactement les demandes en
attente de Léa, Karim et Inès.

**Acceptance Scenarios**:

1. **[RG-10]** **Given** les données de démonstration, **When** Hugo consulte ses demandes à décider, **Then** il
   obtient exactement les demandes en attente de Léa, Karim et Inès. Sa propre demande, les demandes de Support et
   les demandes déjà décidées n'y figurent pas.
2. **[RG-10]** **Given** les données de démonstration, **When** Claire consulte ses demandes à décider, **Then**
   elle obtient uniquement la demande en attente de Hugo, et aucune demande des membres de Produit ou de Support,
   car il n'y a pas de délégation.
3. **[RG-10]** **Given** les données de démonstration, **When** Sophie consulte ses demandes à décider, **Then**
   elle obtient les demandes en attente de Thomas et de Yasmine.
4. **Given** un collaborateur qui n'est valideur d'aucune demande (par exemple Léa), **When** il consulte ses
   demandes à décider, **Then** il obtient une liste vide, et non une erreur.
5. **[RG-11]** **Given** une demande qui vient d'être approuvée ou refusée, **When** son valideur consulte de
   nouveau la liste, **Then** elle n'y figure plus.
6. **Given** plusieurs demandes à décider, **When** la liste est consultée, **Then** elle est triée par date de
   début croissante.

---

### Edge Cases

- **Les demandes de la direction générale** (demandeur manager d'une équipe sans équipe parente, comme Claire) :
  elles sont validées hors application. Elles peuvent être soumises, mais personne, pas même leur auteur, ne peut
  les décider dans l'application (`LEAVE_APPROVER_NOT_ALLOWED`). Elles n'apparaissent dans aucune liste « à
  décider ».
- **Une équipe sans manager** : l'algorithme du circuit de validation ne désigne aucun valideur, et le cas se
  traite comme le précédent. Les données de démonstration ne contiennent pas ce cas.
- **Le valideur décide de sa propre demande** : c'est impossible, car par construction le valideur n'est jamais
  le demandeur.
- **Un congé payé commençant aujourd'hui** : il est refusé (`LEAVE_NOTICE_TOO_SHORT`), car le délai de prévenance
  ne peut pas être respecté. Un événement familial commençant aujourd'hui est accepté.
- **Plusieurs règles enfreintes en même temps à la soumission** : un seul code est renvoyé. Les contrôles se font
  dans l'ordre suivant : forme, `LEAVE_INVALID_PERIOD`, `LEAVE_NOTICE_TOO_SHORT`, `LEAVE_OVERLAP`, puis
  `LEAVE_INSUFFICIENT_BALANCE`.
- **Plusieurs refus possibles pour une décision** : les contrôles se font dans l'ordre suivant :
  `LEAVE_NOT_FOUND`, `LEAVE_APPROVER_NOT_ALLOWED`, `LEAVE_ALREADY_DECIDED`, puis (pour un refus)
  `LEAVE_REJECTION_REASON_REQUIRED`.
- **Deux décisions simultanées sur la même demande** : une seule est retenue. La seconde reçoit
  `LEAVE_ALREADY_DECIDED`.

## Requirements *(mandatory)*

### Functional Requirements

**Soumission**

- **FR-001**: Le système DOIT permettre au collaborateur identifié de soumettre une demande de congé pour
  lui-même, avec un type (`PaidLeave` ou `FamilyEvent`), une date de début, une date de fin et un commentaire
  facultatif.
- **FR-002** *(RG-02)*: Le système DOIT calculer le nombre de jours décomptés comme le nombre de jours ouvrables de
  la période, bornes incluses : du lundi au samedi, hors jours fériés et fermetures. Les jours non travaillés
  proviennent exclusivement du calendrier de l'entreprise (ADR-004). Le calcul est le même pour tous les types.
- **FR-003** *(RG-01, RG-02)*: Le système DOIT refuser avec le code `LEAVE_INVALID_PERIOD` une demande dont la date
  de fin précède la date de début, dont la date de début est antérieure à la date du jour, ou qui ne décompte aucun
  jour.
- **FR-004** *(RG-03, RG-04)*: Pour les congés payés, le système DOIT exiger au moins 2 jours ouvrés (du lundi au
  vendredi, hors jours fériés et fermetures du calendrier de l'entreprise) entre le jour de la soumission (exclu)
  et le premier jour du congé (exclu). Sinon, il refuse avec le code `LEAVE_NOTICE_TOO_SHORT`. Les événements
  familiaux ne sont pas soumis à ce délai.
- **FR-005** *(RG-05)*: Pour les congés payés, le système DOIT refuser avec le code `LEAVE_INSUFFICIENT_BALANCE`
  une demande dont le nombre de jours dépasse le solde disponible. Le solde disponible est le solde CP diminué des
  jours des autres demandes CP du collaborateur encore en attente.
- **FR-006** *(RG-05)*: Une demande pour événement familial NE DOIT PAS être soumise au contrôle du solde.
- **FR-007** *(RG-06)*: Le système DOIT refuser avec le code `LEAVE_OVERLAP` une demande qui chevauche, même d'un
  seul jour, une autre demande du même collaborateur au statut `Pending` ou `Approved`. Les demandes `Rejected`
  sont ignorées.
- **FR-008** *(RG-07)*: Une demande acceptée DOIT être enregistrée au statut `Pending`, avec comme date de
  soumission l'instant de la requête. Elle est restituée au même format qu'une consultation de demande.

**Circuit de validation**

- **FR-009** *(RG-10)*: Le valideur d'une demande DOIT être déterminé ainsi. Si le demandeur est le manager de son
  équipe, le valideur est le manager de l'équipe parente. Sinon, c'est le manager de l'équipe du demandeur.
- **FR-010** *(RG-10)*: Seul le valideur d'une demande DOIT pouvoir la décider. Toute autre personne, y compris le
  demandeur et le N+2 d'un collaborateur (pas de délégation), est refusée avec le code
  `LEAVE_APPROVER_NOT_ALLOWED`.
- **FR-011**: Une demande sans valideur selon FR-009 (direction générale, équipe sans manager) NE DOIT pouvoir
  être décidée par personne dans l'application.

**Décision**

- **FR-012** *(RG-12)*: Le système DOIT permettre au valideur d'approuver une demande en attente : le statut passe
  à `Approved`. Pour les congés payés, le solde CP du demandeur est débité du nombre de jours de la demande dans la
  même opération. Les événements familiaux ne débitent pas le solde.
- **FR-013** *(RG-13)*: Le système DOIT permettre au valideur de refuser une demande en attente avec un motif d'au
  moins 10 caractères, espaces de début et de fin exclus. Le statut passe à `Rejected`, le motif est conservé et
  le solde n'est pas modifié. Un motif absent ou trop court est refusé avec le code
  `LEAVE_REJECTION_REASON_REQUIRED`.
- **FR-014** *(RG-11)*: Le système DOIT refuser avec le code `LEAVE_ALREADY_DECIDED` toute décision sur une demande
  qui n'est pas en attente. Une décision est définitive.
- **FR-015** *(RG-14)*: Toute décision DOIT enregistrer sa date et son auteur. La demande mise à jour est
  restituée.
- **FR-016**: Une décision sur une demande inexistante DOIT être refusée avec le code `LEAVE_NOT_FOUND`.

**Consultation**

- **FR-017** *(RG-10)*: Le système DOIT fournir à l'utilisateur identifié la liste des demandes en attente dont il
  est le valideur, triée par date de début croissante. La liste peut être vide.

**Erreurs, identification et contrat**

- **FR-018** *(ADR-003)*: Chaque refus pour règle de gestion DOIT porter son code et le statut associé dans le
  référentiel des codes d'erreur. Ces règles ne doivent pas être traitées comme des erreurs de forme.

  | Code | Statut |
  |---|---|
  | `LEAVE_INVALID_PERIOD`, `LEAVE_NOTICE_TOO_SHORT`, `LEAVE_INSUFFICIENT_BALANCE`, `LEAVE_OVERLAP`, `LEAVE_REJECTION_REASON_REQUIRED` | 422 |
  | `LEAVE_APPROVER_NOT_ALLOWED` | 403 |
  | `LEAVE_ALREADY_DECIDED` | 409 |
  | `LEAVE_NOT_FOUND` | 404 |

- **FR-019** *(ADR-003)*: Les erreurs de forme (type inconnu, date absente ou mal formée) DOIVENT être refusées
  comme requêtes mal formées (400), avec le détail des champs et sans code métier.
- **FR-020** *(ADR-005)*: Toute opération de cette fonctionnalité DOIT exiger un utilisateur identifié. Sinon, elle
  est refusée (401).
- **FR-021**: Les échanges DOIVENT respecter à l'identique le contrat d'API imposé par ENONCE.md (routes, formats,
  valeurs des types et des statuts, format des dates, statuts de succès).

### Key Entities

- **Demande de congé** : elle porte le demandeur, le type (`PaidLeave` ou `FamilyEvent`), les dates de début et de
  fin, le nombre de jours décomptés (en jours ouvrables), le statut (`Pending`, `Approved` ou `Rejected`), le
  commentaire facultatif, le motif de refus, la date de soumission, la date de décision et l'auteur de la décision.
- **Collaborateur** : il appartient à une équipe et dispose d'un solde CP, en jours ouvrables.
- **Équipe** : elle a un manager (lui-même membre de l'équipe) et une équipe parente facultative. Le manager et
  l'équipe parente déterminent le valideur.
- **Calendrier de l'entreprise** : il est la source unique des jours fériés et des fermetures. Il sert au
  décompte (jours ouvrables) et au délai de prévenance (jours ouvrés).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100 % des scénarios d'acceptation sont couverts par des tests automatisés qui passent, et chaque
  test cite la règle `RG-xx` qu'il couvre.
- **SC-002**: Pour chaque collaborateur, le solde CP après une série d'opérations est toujours égal au solde
  initial moins la somme des jours de ses congés payés approuvés.
- **SC-003**: Aucun solde CP ne devient négatif, et la somme des jours CP en attente d'un collaborateur ne dépasse
  jamais son solde CP, quel que soit l'enchaînement des opérations.
- **SC-004**: 100 % des refus pour règle de gestion portent un code du référentiel et le statut associé, sans
  modifier aucune donnée.
- **SC-005**: Aucun calcul de jours fériés ou de fermetures n'existe en dehors du calendrier de l'entreprise.
- **SC-006**: L'écran de la partie 2 peut s'appuyer sur ces opérations sans aucune adaptation du contrat.

## Assumptions

- Le contrat d'API de la partie 1 d'ENONCE.md est imposé. Les codes d'erreur et leurs statuts sont ceux du
  référentiel du wiki. Aucun nouveau code n'est créé.
- Les demandes de la direction générale peuvent être soumises, car aucun code du référentiel ne permet de les
  refuser. En revanche, elles ne peuvent pas être décidées dans l'application.
- Le délai de prévenance et le début non passé s'apprécient par rapport à la date du jour de l'horloge du système
  (paramétrable dans les tests).
- Une approbation ne peut pas rendre le solde négatif, car RG-05 garantit à la soumission que les jours CP en
  attente ne dépassent pas le solde. RG-12 ne prévoit donc pas de nouveau contrôle du solde à l'approbation.
- Le commentaire est libre et facultatif. Aucune règle de gestion ne le contraint.
- L'ordre des contrôles (voir les cas limites) est un choix de cette spec, faute de règle dans la documentation.
- Hors périmètre (documentation) : l'annulation d'une demande, les demi-journées, le report de solde et
  l'acquisition mensuelle. Sont aussi hors périmètre : la modification d'une demande soumise, les notifications et
  la délégation de validation.
- Les messages affichés à l'utilisateur (page Messages du wiki) relèvent du front (partie 2). L'API ne garantit
  que le `code`.
