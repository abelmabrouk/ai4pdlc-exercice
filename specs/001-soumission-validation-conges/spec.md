# Feature Specification: Soumission et validation des demandes de congé

**Feature Branch**: `romain`

**Created**: 2026-11-03

**Status**: Draft

**Input**: User description: "Partie 1 de ENONCE.md : soumission et validation des demandes de congé (back-end), en respectant le contrat d'API de l'énoncé et le wiki docs/wiki"

## Clarifications

### Session 2026-11-03

- Q: Que doit faire le système quand Claire Martin (manager de la Direction, équipe sans équipe parente) soumet
  une demande de congé ? → A: Elle est acceptée si elle respecte les règles. Elle n'apparaît dans aucune liste de
  demandes à décider, et toute tentative de décision est refusée avec `LEAVE_APPROVER_NOT_ALLOWED`.
- Q: La consultation existante d'une demande inexistante doit-elle aussi renvoyer le code `LEAVE_NOT_FOUND` ?
  → A: Oui. Toute réponse « demande inexistante » porte `LEAVE_NOT_FOUND`, qu'il s'agisse de consulter, d'approuver
  ou de refuser.
- Q: Quand une demande enfreint plusieurs règles à la fois, quelle erreur le système doit-il renvoyer ? → A: La
  première dans un ordre fixe. À la soumission : 401, 400, puis RG-01, RG-02, RG-03, RG-05, RG-06. À la décision :
  401, 404, 403, 409, puis motif (422). Précisé à l'analyse : pour le refus, le 400 (JSON invalide) se place
  juste après le 401.

## User Scenarios & Testing *(mandatory)*

Ces exemples utilisent les données de démonstration :

| Collaborateur | Id | Équipe | Rôle | Solde CP |
|---|---|---|---|---|
| Claire Martin | 1 | Direction (pas d'équipe parente) | manager de Direction | 30 |
| Hugo Bernard | 2 | Produit (parente : Direction) | manager de Produit | 30 |
| Lea Petit | 3 | Produit | — | 30 |
| Karim Benali | 4 | Produit | — | 30 |
| Ines Moreau | 5 | Produit | — | 3 |
| Sophie Laurent | 6 | Support (parente : Direction) | manager de Support | 30 |
| Thomas Roux | 7 | Support | — | 30 |
| Yasmine Haddad | 8 | Support | — | 25 |

Sauf mention contraire, « aujourd'hui » est le **lundi 2027-03-01**, une semaine sans jour férié ni fermeture.
Le calendrier de l'entreprise compte notamment l'Ascension le jeudi 2027-05-06, une fermeture le vendredi
2027-05-07 et le 8 mai (samedi 2027-05-08) comme fériés. Le lundi de Pentecôte (2027-05-17) est **travaillé**.

Chaque scénario porte entre crochets l'identifiant de la règle de gestion qu'il couvre (ADR-006).

### User Story 1 - Soumettre une demande de congé (Priority: P1)

Un collaborateur soumet une demande de congés payés ou d'événement familial pour une période donnée. Le système
calcule le nombre de jours décomptés, vérifie les règles de gestion et enregistre la demande « en attente »,
ou la refuse avec un code d'erreur qui explique pourquoi.

**Why this priority**: sans soumission, il n'y a rien à valider. C'est le point d'entrée de tout le processus
et la partie la plus riche en règles de gestion (période, décompte, prévenance, solde, chevauchement).

**Independent Test**: on soumet des demandes au nom de différents collaborateurs, puis on vérifie la réponse
(demande créée avec son nombre de jours, ou code d'erreur) et la consultation de la demande créée.

**Acceptance Scenarios**:

*Cas nominal*

1. **[RG-02, RG-07]** **Given** Lea Petit, **When** elle soumet une demande de congés payés du lundi 2027-03-08
   au vendredi 2027-03-12 avec le commentaire « Vacances », **Then** la demande est créée au statut `Pending`
   avec 5 jours décomptés, la date de soumission est l'instant de la requête, et la réponse a le même format
   que la consultation d'une demande.
2. **[RG-07]** **Given** une demande venant d'être créée, **When** on la consulte par son identifiant,
   **Then** on retrouve exactement les informations renvoyées à la création.

*Décompte en jours ouvrables (RG-02)*

3. **[RG-02]** **Given** Lea Petit, **When** elle demande du lundi 2027-03-08 au samedi 2027-03-13,
   **Then** 6 jours sont décomptés (le samedi est un jour ouvrable).
4. **[RG-02]** **Given** Lea Petit, **When** elle demande du jeudi 2027-03-11 au mardi 2027-03-16,
   **Then** 5 jours sont décomptés (jeu, ven, sam, lun, mar ; le dimanche n'est jamais décompté).
5. **[RG-02]** **Given** Lea Petit, **When** elle demande du lundi 2027-03-08 au dimanche 2027-03-14,
   **Then** 6 jours sont décomptés.
6. **[RG-02]** **Given** Lea Petit, **When** elle demande du lundi 2027-05-03 au samedi 2027-05-08,
   **Then** 3 jours sont décomptés (l'Ascension, la fermeture du 7 mai et le 8 mai sont exclus).
7. **[RG-02]** **Given** Lea Petit, **When** elle demande le seul lundi de Pentecôte 2027-05-17,
   **Then** la demande est acceptée avec 1 jour décompté (journée de solidarité, travaillée).
8. **[RG-02]** **Given** Lea Petit, **When** elle demande le seul dimanche 2027-03-14, **Then** la demande est
   refusée avec `LEAVE_INVALID_PERIOD` (aucun jour décompté).
9. **[RG-02]** **Given** Lea Petit, **When** elle demande le seul jeudi de l'Ascension 2027-05-06,
   **Then** la demande est refusée avec `LEAVE_INVALID_PERIOD`.
10. **[RG-02]** **Given** Lea Petit, **When** elle soumet un événement familial du jeudi 2027-03-11 au mardi
    2027-03-16, **Then** 5 jours sont décomptés, comme pour des congés payés.

*Période (RG-01)*

11. **[RG-01]** **Given** Lea Petit, **When** elle demande une période dont la date de fin (2027-03-08) précède
    la date de début (2027-03-12), **Then** la demande est refusée avec `LEAVE_INVALID_PERIOD` (422), et non avec
    une erreur de format.
12. **[RG-01]** **Given** Lea Petit, **When** elle soumet une demande (de n'importe quel type) qui commence hier
    (2027-02-28), **Then** elle est refusée avec `LEAVE_INVALID_PERIOD`.

*Délai de prévenance (RG-03, RG-04)*

13. **[RG-03]** **Given** une soumission le lundi 2027-03-01, **When** Lea Petit demande des congés payés à
    partir du jeudi 2027-03-04, **Then** la demande est acceptée (mardi et mercredi sont les deux jours ouvrés
    de prévenance).
14. **[RG-03]** **Given** une soumission le lundi 2027-03-01, **When** Lea Petit demande des congés payés à
    partir du mercredi 2027-03-03, **Then** la demande est refusée avec `LEAVE_NOTICE_TOO_SHORT`.
15. **[RG-03]** **Given** une soumission le jeudi 2027-03-04, **When** Lea Petit demande des congés payés à partir
    du lundi 2027-03-08, **Then** la demande est refusée avec `LEAVE_NOTICE_TOO_SHORT` (seul le vendredi est un jour
    ouvré de prévenance : le samedi ne compte pas). À partir du mardi 2027-03-09 (vendredi et lundi), elle est
    acceptée.
16. **[RG-03]** **Given** une soumission le mardi 2027-05-04, **When** Lea Petit demande des congés payés à partir
    du lundi 2027-05-10, **Then** la demande est refusée avec `LEAVE_NOTICE_TOO_SHORT` (seul le mercredi 5 est
    ouvré : l'Ascension et la fermeture du 7 mai ne comptent pas). À partir du mardi 2027-05-11, elle est
    acceptée.
17. **[RG-04]** **Given** une soumission le lundi 2027-03-01, **When** Lea Petit soumet un événement familial qui
    commence le jour même, **Then** la demande est acceptée.

*Solde (RG-05)*

18. **[RG-05]** **Given** Ines Moreau (solde CP de 3 jours), qui a déjà une demande de congés payés de 2 jours en
    attente, **When** elle demande 2 jours de congés payés, **Then** la demande est refusée avec
    `LEAVE_INSUFFICIENT_BALANCE` (solde disponible : 1 jour).
19. **[RG-05]** **Given** la même situation, **When** Ines Moreau demande 1 jour de congés payés, **Then** la
    demande est acceptée (un nombre de jours égal au solde disponible est autorisé).
20. **[RG-05]** **Given** la même situation, **When** Ines Moreau soumet un événement familial de 3 jours,
    **Then** la demande est acceptée (le solde ne s'applique pas aux événements familiaux).

*Chevauchement (RG-06)*

21. **[RG-06]** **Given** Lea Petit a une demande en attente du 2027-03-08 au 2027-03-12, **When** elle demande du
    vendredi 2027-03-12 au samedi 2027-03-13, **Then** la demande est refusée avec `LEAVE_OVERLAP`.
22. **[RG-06]** **Given** la même situation, **When** elle demande le seul samedi 2027-03-13, **Then** la demande
    est acceptée (périodes contiguës, sans jour commun).
23. **[RG-06]** **Given** la même situation, **When** elle soumet un événement familial le mercredi 2027-03-10,
    **Then** la demande est refusée avec `LEAVE_OVERLAP` (le chevauchement vaut quel que soit le type).
24. **[RG-06]** **Given** Lea Petit a une demande approuvée sur une période, **When** elle soumet une demande qui
    la chevauche, **Then** la demande est refusée avec `LEAVE_OVERLAP`.
25. **[RG-06]** **Given** Lea Petit a une demande **refusée** sur une période, **When** elle soumet une demande sur
    la même période, **Then** la demande est acceptée.
26. **[RG-06]** **Given** Karim Benali a une demande en attente du 2027-03-08 au 2027-03-12, **When** Lea Petit
    demande la même période, **Then** sa demande est acceptée (seules les demandes du même collaborateur comptent).

*Erreurs de forme et d'identification*

27. **Given** une soumission sans type, avec un type inconnu, sans date ou avec une date mal formée, **When** elle
    est envoyée, **Then** elle est refusée avec une erreur de validation de forme (400), qui détaille les champs
    en erreur et ne porte pas de code métier.
28. **Given** une requête sans identification de l'utilisateur, avec une identification mal formée ou avec un
    identifiant qui ne correspond à aucun collaborateur, **When** elle est envoyée, **Then** elle est refusée
    comme « utilisateur non identifié » (401).
29. **Given** un identifiant de demande inexistant, **When** on consulte cette demande, **Then** la consultation est
    refusée avec `LEAVE_NOT_FOUND` (404).

*Ordre des vérifications*

30. **[RG-03, RG-05]** **Given** une soumission le lundi 2027-03-01 et Ines Moreau, dont le solde disponible est
    de 1 jour, **When** elle demande 2 jours de congés payés à partir du mercredi 2027-03-03, **Then** la demande
    est refusée avec `LEAVE_NOTICE_TOO_SHORT` (RG-03 est vérifiée avant RG-05), et non avec
    `LEAVE_INSUFFICIENT_BALANCE`.

---

### User Story 2 - Approuver une demande (Priority: P2)

Le valideur d'une demande en attente l'approuve. La demande passe au statut « approuvée ». S'il s'agit de
congés payés, le solde du collaborateur est débité à ce moment-là.

**Why this priority**: c'est l'issue la plus fréquente d'une demande, et c'est elle qui met à jour le solde.

**Independent Test**: sur les demandes en attente des données de démonstration, on approuve au nom de différents
utilisateurs, puis on vérifie le statut, les informations de décision et le solde du collaborateur.

**Acceptance Scenarios**:

1. **[RG-10, RG-12, RG-14]** **Given** la demande n°1 de Lea Petit (congés payés, 5 jours, en attente) et un
   solde de 30 jours, **When** Hugo Bernard (son N+1) l'approuve, **Then** la demande passe au statut `Approved`,
   le solde de Lea passe à 25 jours, et la date de décision ainsi que l'auteur (Hugo Bernard) sont enregistrés.
2. **[RG-12]** **Given** la demande n°4 de Thomas Roux (événement familial, en attente), **When** Sophie Laurent
   l'approuve, **Then** la demande passe au statut `Approved` et le solde de Thomas reste inchangé.
3. **[RG-10]** **Given** la demande n°3 de Hugo Bernard, manager de Produit, **When** Claire Martin (manager de
   l'équipe parente Direction) l'approuve, **Then** l'approbation est acceptée.
4. **[RG-10]** **Given** la demande n°3 de Hugo Bernard, **When** Hugo Bernard tente de l'approuver lui-même,
   **Then** l'action est refusée avec `LEAVE_APPROVER_NOT_ALLOWED` (403).
5. **[RG-10]** **Given** la demande n°1 de Lea Petit, **When** Claire Martin (N+2 de Lea) tente de l'approuver,
   **Then** l'action est refusée avec `LEAVE_APPROVER_NOT_ALLOWED` (pas de délégation).
6. **[RG-10]** **Given** la demande n°1 de Lea Petit, **When** Sophie Laurent (manager d'une autre équipe) ou
   Karim Benali (simple collègue) tente de l'approuver, **Then** l'action est refusée avec
   `LEAVE_APPROVER_NOT_ALLOWED`.
7. **[RG-11]** **Given** une demande déjà approuvée ou déjà refusée, **When** son valideur tente de l'approuver,
   **Then** l'action est refusée avec `LEAVE_ALREADY_DECIDED` (409), et ni la demande ni le solde ne changent.
8. **Given** un identifiant de demande inexistant, **When** un utilisateur tente de l'approuver, **Then** l'action
   est refusée avec `LEAVE_NOT_FOUND` (404).
9. **[RG-10]** **Given** Claire Martin (direction générale) vient de soumettre une demande de congés payés valide,
   **When** elle consulte la réponse, **Then** la demande est créée au statut `Pending`. **And when** Claire Martin
   ou tout autre utilisateur tente de l'approuver ou de la refuser, **Then** l'action est refusée avec
   `LEAVE_APPROVER_NOT_ALLOWED`, et la demande n'apparaît dans la liste des demandes à décider d'aucun
   utilisateur.

---

### User Story 3 - Refuser une demande avec un motif (Priority: P2)

Le valideur d'une demande en attente la refuse en indiquant un motif. La demande passe au statut « refusée » et
le solde du collaborateur n'est pas modifié.

**Why this priority**: c'est l'autre issue d'une décision. Elle est aussi indispensable que l'approbation, mais
ne touche pas au solde.

**Independent Test**: sur les demandes en attente des données de démonstration, on refuse avec différents motifs,
puis on vérifie le statut, le motif enregistré, les informations de décision et le solde.

**Acceptance Scenarios**:

1. **[RG-13, RG-14]** **Given** la demande n°2 de Karim Benali (en attente), **When** Hugo Bernard la refuse avec
   le motif « Période de forte activité », **Then** la demande passe au statut `Rejected`, le motif est enregistré,
   le solde de Karim est inchangé, et la date ainsi que l'auteur de la décision sont enregistrés.
2. **[RG-13]** **Given** la demande n°2, **When** Hugo Bernard la refuse avec le motif « Trop tôt »
   (8 caractères), **Then** l'action est refusée avec `LEAVE_REJECTION_REASON_REQUIRED` (422) et la demande reste
   en attente.
3. **[RG-13]** **Given** la demande n°2, **When** Hugo Bernard la refuse avec le motif « ␣␣␣Trop tôt␣␣␣ »
   (8 caractères une fois les espaces de début et de fin retirés), **Then** l'action est refusée avec
   `LEAVE_REJECTION_REASON_REQUIRED`.
4. **[RG-13]** **Given** la demande n°2, **When** Hugo Bernard la refuse sans motif, avec un motif vide ou avec un
   motif composé uniquement d'espaces, **Then** l'action est refusée avec `LEAVE_REJECTION_REASON_REQUIRED` (422),
   et non avec une erreur de format (400).
5. **[RG-13]** **Given** la demande n°2, **When** Hugo Bernard la refuse avec le motif « Trop court »
   (exactement 10 caractères), **Then** le refus est accepté.
6. **[RG-10]** **Given** la demande n°1 de Lea Petit, **When** un utilisateur qui n'en est pas le valideur tente
   de la refuser, **Then** l'action est refusée avec `LEAVE_APPROVER_NOT_ALLOWED` (403).
7. **[RG-11]** **Given** une demande déjà approuvée ou déjà refusée, **When** son valideur tente de la refuser
   avec un motif valide, **Then** l'action est refusée avec `LEAVE_ALREADY_DECIDED` (409).
8. **Given** un identifiant de demande inexistant, **When** un utilisateur tente de la refuser, **Then** l'action
   est refusée avec `LEAVE_NOT_FOUND` (404).
9. **[RG-11, RG-13]** **Given** une demande déjà refusée, **When** son valideur la refuse de nouveau avec le motif
   « Trop tôt », **Then** l'action est refusée avec `LEAVE_ALREADY_DECIDED` (le statut est vérifié avant le motif).
10. **[RG-10, RG-11]** **Given** une demande déjà approuvée, **When** un utilisateur qui n'en est pas le valideur
    tente de la refuser, **Then** l'action est refusée avec `LEAVE_APPROVER_NOT_ALLOWED` (l'habilitation est
    vérifiée avant le statut).

---

### User Story 4 - Lister les demandes à décider (Priority: P3)

Un utilisateur consulte la liste des demandes en attente dont il est le valideur. C'est la liste qui alimentera
l'écran « Demandes en attente » du manager (partie 2).

**Why this priority**: elle facilite la décision, mais l'approbation et le refus fonctionnent déjà par
identifiant de demande sans elle.

**Independent Test**: on consulte la liste au nom de chaque utilisateur des données de démonstration et on la
compare au circuit de validation.

**Acceptance Scenarios**:

1. **[RG-10]** **Given** les données de démonstration, **When** Hugo Bernard consulte ses demandes à décider,
   **Then** il obtient les demandes en attente de Lea Petit, Karim Benali et Ines Moreau, **mais pas la sienne**.
2. **[RG-10]** **Given** les données de démonstration, **When** Claire Martin consulte ses demandes à décider,
   **Then** elle obtient la demande en attente de Hugo Bernard (manager de Produit), et aucune demande des
   membres de Produit ou de Support qui ne sont pas managers.
3. **[RG-10]** **Given** les données de démonstration, **When** Sophie Laurent consulte ses demandes à décider,
   **Then** elle obtient les demandes en attente de Thomas Roux et de Yasmine Haddad, mais aucune demande
   approuvée ou refusée.
4. **Given** Thomas Roux, qui n'est valideur d'aucune demande, **When** il consulte ses demandes à décider,
   **Then** il obtient une liste vide (et non une erreur).
5. **[RG-11, RG-12]** **Given** Hugo Bernard vient d'approuver la demande n°1, **When** il consulte de nouveau ses
   demandes à décider, **Then** la demande n°1 n'y figure plus.
6. **Given** une liste de demandes à décider, **When** on la consulte, **Then** chaque élément a le même format que
   la consultation d'une demande, et les éléments sont triés par date de début croissante.

---

### Edge Cases

- **Le valideur est le manager de l'équipe parente.** Une demande d'un manager (Hugo Bernard, Sophie Laurent)
  remonte au manager de l'équipe parente (Claire Martin). Elle n'apparaît jamais dans la liste de son auteur.
- **La demande vient de la direction générale.** Une demande de Claire Martin, manager d'une équipe sans équipe
  parente, n'a pas de valideur dans l'application. Elle est acceptée à la soumission si elle respecte les règles,
  n'apparaît dans aucune liste de demandes à décider, et toute tentative de décision est refusée avec
  `LEAVE_APPROVER_NOT_ALLOWED`.
- **Le début est aujourd'hui.** C'est autorisé pour un événement familial (RG-04). Pour des congés payés, c'est
  refusé avec `LEAVE_NOTICE_TOO_SHORT`, puisque le délai de prévenance ne peut pas être respecté (RG-03).
- **Le délai de prévenance compte en jours ouvrés, pas en jours ouvrables.** Le samedi compte dans le décompte
  des jours de congé, mais pas dans le délai de prévenance.
- **Les jours fériés et les fermetures viennent d'une source unique.** Ils proviennent exclusivement du calendrier
  de l'entreprise (ADR-004), pour le décompte comme pour le délai de prévenance. Une fermeture ajoutée à la
  configuration est prise en compte sans autre modification.
- **Plusieurs règles sont violées.** Seule la première violation est renvoyée, dans l'ordre fixé par FR-024
  (voir US1 sc. 30, US3 sc. 9 et 10).
- **Le solde est entièrement consommé.** Une demande dont le nombre de jours est égal au solde disponible est
  acceptée. Une demande qui le dépasse d'un jour est refusée.
- **Approbation et solde disponible.** Le solde disponible tient déjà compte des demandes en attente (RG-05).
  L'approbation d'une demande en attente ne peut donc pas rendre le solde négatif. Elle ne refait pas le contrôle
  de solde.
- **Deux décisions concurrentes.** Si deux décisions portent sur la même demande, une seule l'emporte. L'autre
  reçoit `LEAVE_ALREADY_DECIDED`.

## Requirements *(mandatory)*

### Functional Requirements

**Soumission**

- **FR-001**: Le système DOIT permettre à un collaborateur identifié de soumettre une demande de congé pour
  lui-même, de type congés payés ou événement familial, avec une date de début, une date de fin et un commentaire
  facultatif.
- **FR-002**: Le système DOIT refuser avec `LEAVE_INVALID_PERIOD` une demande dont la date de fin précède la date
  de début, ou dont la date de début est antérieure à la date du jour (RG-01).
- **FR-003**: Le système DOIT calculer le nombre de jours d'une demande en jours ouvrables (du lundi au samedi,
  bornes incluses, hors jours fériés et fermetures de l'entreprise), de la même façon pour tous les types
  (RG-02).
- **FR-004**: Le système DOIT obtenir les jours fériés et les fermetures exclusivement auprès du calendrier
  des jours non travaillés de l'entreprise, sans liste ni calcul propre (ADR-004).
- **FR-005**: Le système DOIT refuser avec `LEAVE_INVALID_PERIOD` une demande qui ne contient aucun jour
  décompté (RG-02).
- **FR-006**: Pour les congés payés, le système DOIT refuser avec `LEAVE_NOTICE_TOO_SHORT` une demande qui ne
  laisse pas au moins 2 jours ouvrés (du lundi au vendredi, hors jours fériés et fermetures) entre le jour de la
  soumission et le premier jour du congé, ces deux jours étant exclus (RG-03).
- **FR-007**: Le système NE DOIT PAS appliquer de délai de prévenance aux événements familiaux, qui peuvent
  commencer dès le jour de la soumission (RG-04).
- **FR-008**: Pour les congés payés, le système DOIT refuser avec `LEAVE_INSUFFICIENT_BALANCE` une demande dont le
  nombre de jours dépasse le solde disponible, c'est-à-dire le solde CP du collaborateur moins les jours de ses
  autres demandes de congés payés en attente (RG-05). Les événements familiaux ne sont pas concernés.
- **FR-009**: Le système DOIT refuser avec `LEAVE_OVERLAP` une demande qui partage au moins un jour calendaire
  avec une autre demande du même collaborateur, en attente ou approuvée, quel que soit son type. Les demandes
  refusées sont ignorées (RG-06).
- **FR-010**: Le système DOIT enregistrer une demande acceptée au statut « en attente », avec comme date de
  soumission l'instant de la requête, et la renvoyer au même format que la consultation d'une demande (RG-07).
- **FR-011**: Le système NE DOIT PAS modifier le solde CP à la soumission.

**Valideur et liste à décider**

- **FR-012**: Le système DOIT déterminer le valideur d'une demande ainsi : le manager de l'équipe du demandeur ;
  si le demandeur est lui-même ce manager, le manager de l'équipe parente ; si cette équipe n'a pas d'équipe
  parente, aucun valideur dans l'application (circuit de validation).
- **FR-013**: Le système DOIT permettre à un utilisateur identifié d'obtenir la liste des demandes en attente dont
  il est le valideur, et uniquement celles-là, triées par date de début croissante. Une liste vide est une
  réponse normale.

**Décision**

- **FR-014**: Le système DOIT refuser avec `LEAVE_APPROVER_NOT_ALLOWED` toute décision prise par un utilisateur qui
  n'est pas le valideur de la demande, y compris son auteur et son N+2 (RG-10, pas de délégation).
- **FR-015**: Le système DOIT refuser avec `LEAVE_ALREADY_DECIDED` toute décision portant sur une demande qui n'est
  plus en attente. Une décision est définitive (RG-11).
- **FR-016**: Le système DOIT refuser avec `LEAVE_NOT_FOUND` toute décision portant sur une demande inexistante.
  La consultation d'une demande inexistante DOIT renvoyer le même code.
- **FR-017**: À l'approbation, le système DOIT passer la demande au statut « approuvée » et, pour les congés
  payés uniquement, débiter le solde CP du collaborateur du nombre de jours de la demande (RG-12).
- **FR-018**: Au refus, le système DOIT exiger un motif d'au moins 10 caractères, espaces de début et de fin
  exclus, et refuser sinon avec `LEAVE_REJECTION_REASON_REQUIRED`, y compris si le motif est absent. Une demande
  acceptée passe au statut « refusée », son motif est enregistré sans les espaces de début et de fin, et le solde
  n'est pas modifié (RG-13).
- **FR-019**: À chaque décision, le système DOIT enregistrer la date et l'heure de la décision ainsi que son
  auteur (RG-14), et renvoyer la demande mise à jour au même format que la consultation.
- **FR-020**: Une décision et la mise à jour du solde qui l'accompagne DOIVENT être appliquées ensemble ou pas
  du tout.

**Identification et erreurs**

- **FR-021**: Le système DOIT identifier l'utilisateur courant pour chaque opération et refuser comme
  « utilisateur non identifié » (401) une requête sans identification, avec une identification invalide ou avec
  un identifiant qui ne correspond à aucun collaborateur (ADR-005).
- **FR-022**: Le système DOIT signaler chaque violation d'une règle de gestion par une erreur structurée portant le
  code stable du référentiel et le statut associé : 422 pour une règle non respectée, 403 pour une action
  interdite, 404 pour une ressource absente, 409 pour un conflit d'état (ADR-003). Il n'utilise aucun code absent
  du référentiel.
- **FR-023**: Le système DOIT signaler les erreurs de forme (champ obligatoire absent, valeur mal formée, type
  inconnu) par une erreur de validation (400), qui détaille les champs en erreur sans code métier. Une règle de
  gestion n'est jamais signalée comme une erreur de forme (ADR-003).
- **FR-024**: Lorsque plusieurs vérifications échouent, le système DOIT renvoyer uniquement la première selon un
  ordre fixe :
  - à la soumission : identification (401), forme (400), puis RG-01, RG-02, RG-03, RG-05 et RG-06 ;
  - à la décision : identification (401), forme (400, refus uniquement : JSON invalide), existence (404),
    habilitation (403), statut (409), puis motif de refus (422).

### Contrat d'interface (imposé)

Le contrat d'API de `ENONCE.md` n'est pas négociable : le front et deux applications partenaires (paie, planning)
s'appuient dessus. Il est rappelé ici comme contrainte externe, et non comme choix de conception.

| Opération | Route | Succès |
|---|---|---|
| Soumettre une demande | `POST /api/leave-requests` — corps `{ type, startDate, endDate, comment }` | `201 Created`, demande créée (statut `Pending`) |
| Lister les demandes à décider | `GET /api/leave-requests/pending` | `200 OK`, liste des demandes `Pending` dont l'utilisateur est le valideur |
| Approuver | `POST /api/leave-requests/{id}/approve` | `200 OK`, demande mise à jour (statut `Approved`) |
| Refuser | `POST /api/leave-requests/{id}/reject` — corps `{ reason }` | `200 OK`, demande mise à jour (statut `Rejected`) |

- **Identification.** L'utilisateur courant est porté par l'en-tête `X-User-Id`.
- **Format des données.** Le JSON est en camelCase, les énumérations sont des chaînes (`type` : `PaidLeave` ou
  `FamilyEvent`) et les dates sont au format `yyyy-MM-dd`.
- **Format de réponse.** Les demandes renvoyées ont le même format que `GET /api/leave-requests/{id}` (existant).

### Cas d'erreur et codes

Les codes sont tirés du référentiel [Codes d'erreur](../../docs/wiki/Codes-d-erreur.md).

| Code | Statut | Cas | Règle |
|---|---|---|---|
| `LEAVE_INVALID_PERIOD` | 422 | Fin avant début, début dans le passé ou aucun jour décompté | RG-01, RG-02 |
| `LEAVE_NOTICE_TOO_SHORT` | 422 | Délai de prévenance des congés payés non respecté | RG-03 |
| `LEAVE_INSUFFICIENT_BALANCE` | 422 | Solde disponible insuffisant (congés payés) | RG-05 |
| `LEAVE_OVERLAP` | 422 | Chevauchement avec une demande en attente ou approuvée du même collaborateur | RG-06 |
| `LEAVE_REJECTION_REASON_REQUIRED` | 422 | Motif de refus absent ou de moins de 10 caractères | RG-13 |
| `LEAVE_APPROVER_NOT_ALLOWED` | 403 | L'utilisateur n'est pas le valideur de la demande | RG-10 |
| `LEAVE_ALREADY_DECIDED` | 409 | La demande n'est plus en attente | RG-11 |
| `LEAVE_NOT_FOUND` | 404 | Demande inexistante | — |
| *(sans code)* | 400 | Erreur de forme : champ manquant, format invalide, type inconnu | — |
| *(sans code)* | 401 | Utilisateur non identifié | ADR-005 |

### Key Entities *(include if feature involves data)*

- **Demande de congé** : demande d'un collaborateur. Elle porte un type (congés payés ou événement familial), une
  période (date de début et date de fin, incluses), un nombre de jours décomptés, un statut (en attente,
  approuvée, refusée), un commentaire facultatif, la date de soumission et, une fois décidée, la date et l'auteur
  de la décision ainsi que, en cas de refus, le motif.
- **Collaborateur** : employé membre d'une équipe, avec un solde de congés payés exprimé en jours ouvrables.
- **Équipe** : unité de l'organigramme, avec un manager (lui-même membre de l'équipe) et éventuellement une équipe
  parente. Elle détermine le valideur des demandes de ses membres.
- **Calendrier des jours non travaillés** : source unique des jours fériés légaux et des fermetures de
  l'entreprise. Il existe déjà, et cette fonctionnalité le consulte sans le modifier.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100 % des scénarios d'acceptation de cette spécification sont couverts par des tests automatisés,
  et ces tests passent.
- **SC-002**: 100 % des refus liés à une règle de gestion portent un code du référentiel et le statut associé.
  Aucun refus métier n'est signalé comme une erreur de forme, et aucun code hors référentiel n'apparaît.
- **SC-003**: Le nombre de jours décomptés correspond aux exemples de la page « Décompte des jours » dans 100 %
  des cas, y compris avec un jour férié, une fermeture d'entreprise ou le lundi de Pentecôte.
- **SC-004**: Pour chaque utilisateur des données de démonstration, la liste des demandes à décider correspond
  exactement au circuit de validation : aucune demande de l'utilisateur lui-même, aucune demande dont il n'est pas
  le valideur, aucune demande déjà décidée.
- **SC-005**: Après n'importe quelle suite de soumissions et de décisions, le solde CP de chaque collaborateur est
  égal à son solde initial moins la somme des jours de ses demandes de congés payés approuvées. Aucun solde ne
  devient négatif.
- **SC-006**: Le front (partie 2) et les applications partenaires peuvent consommer les quatre opérations sans
  adaptation du contrat d'interface. Tout écart de route, de format ou de statut de réponse est un échec.

## Assumptions

- **Fuseau horaire.** « Aujourd'hui » et « le jour de la soumission » sont la date locale de l'horloge de
  référence de l'application, au moment de la requête. Les tests doivent pouvoir fixer cette horloge.
- **Commentaire.** Le commentaire de soumission est facultatif, sans contrainte de contenu.
- **Motif.** Il est enregistré sans les espaces de début et de fin.
- **Direction générale.** Les demandes de la direction générale (manager d'une équipe sans équipe parente) sont
  acceptées à la soumission, mais ne sont décidables par personne dans l'application. C'est confirmé en
  clarification : le référentiel des codes étant fermé, aucun code ne permet de les refuser à la soumission.
- **Ordre des vérifications.** Il suit la numérotation des règles de gestion (FR-024, confirmé en clarification),
  pour que la réponse soit déterministe lorsque plusieurs règles sont violées.
- **Consultation existante.** La consultation d'une demande inexistante est alignée sur le code `LEAVE_NOT_FOUND`,
  par cohérence avec le référentiel (confirmé en clarification). Le reste de son comportement ne change pas.
- **Tri de la liste.** La liste des demandes à décider est triée par date de début croissante, puis par
  identifiant, comme la liste existante des demandes.
- **Utilisateur.** L'identification de l'utilisateur est celle déjà en place (ADR-005). Aucune authentification
  supplémentaire n'est introduite.
- **Hors périmètre** : annulation d'une demande, modification d'une demande soumise, demi-journées, report de
  solde, acquisition mensuelle des congés, délégation de validation, notifications, écran front (partie 2).

## Références

Exigées par l'[ADR-006](../../docs/wiki/ADR-006-redaction-des-specifications.md).

**Décisions d'architecture**

- [ADR-001 — Architecture en couches](../../docs/wiki/ADR-001-architecture-en-couches.md) : les cas d'usage sont
  des services applicatifs, et les controllers sont minces.
- [ADR-002 — Persistance SQLite](../../docs/wiki/ADR-002-persistance-sqlite.md) : la base est recréée avec les
  données de démonstration, et les dates sont stockées sans heure.
- [ADR-003 — Format des erreurs](../../docs/wiki/ADR-003-format-des-erreurs.md) : ProblemDetails, extension `code`,
  statuts 422, 403, 404 et 409, 400 réservé à la forme (FR-022, FR-023).
- [ADR-004 — Calendrier des jours non travaillés](../../docs/wiki/ADR-004-calendrier-des-jours-non-travailles.md) :
  source unique des jours fériés et des fermetures (FR-003, FR-004, FR-006).
- [ADR-005 — Identification de l'utilisateur](../../docs/wiki/ADR-005-identification-utilisateur.md) : en-tête
  `X-User-Id`, 401 si absent ou invalide (FR-021).
- [ADR-006 — Rédaction des spécifications](../../docs/wiki/ADR-006-redaction-des-specifications.md) : section
  « Références », traçabilité des RG-xx, codes d'erreur cités.

**Règles de gestion** ([référentiel](../../docs/wiki/Regles-de-gestion.md))

| Règle | Objet | Couverte par |
|---|---|---|
| RG-01 | Période continue, début non passé | FR-002 ; US1 sc. 11, 12 |
| RG-02 | Décompte en jours ouvrables, période sans jour décompté invalide | FR-003, FR-005 ; US1 sc. 1, 3 à 10 |
| RG-03 | Délai de prévenance de 2 jours ouvrés (congés payés) | FR-006 ; US1 sc. 13 à 16, 30 |
| RG-04 | Pas de prévenance pour les événements familiaux | FR-007 ; US1 sc. 17 |
| RG-05 | Solde disponible (congés payés) | FR-008 ; US1 sc. 18 à 20, 30 |
| RG-06 | Pas de chevauchement avec une demande en attente ou approuvée | FR-009 ; US1 sc. 21 à 26 |
| RG-07 | Statut `Pending` et date de soumission | FR-010 ; US1 sc. 1, 2 |
| RG-10 | Seul le valideur décide | FR-012 à FR-014 ; US2 sc. 3 à 6, 9 ; US3 sc. 6 ; US4 |
| RG-11 | Seule une demande en attente se décide, décision définitive | FR-015, FR-024 ; US2 sc. 7 ; US3 sc. 7, 9, 10 ; US4 sc. 5 |
| RG-12 | Approbation, débit du solde à l'approbation (congés payés) | FR-017 ; US2 sc. 1, 2 |
| RG-13 | Refus avec un motif d'au moins 10 caractères, solde inchangé | FR-018 ; US3 sc. 1 à 5 |
| RG-14 | Date et auteur de la décision | FR-019 ; US2 sc. 1 ; US3 sc. 1 |

**Autres pages du wiki**

- [Glossaire](../../docs/wiki/Glossaire.md) : jour ouvrable, jour ouvré, solde disponible, valideur, N+1, N+2.
- [Décompte des jours](../../docs/wiki/Decompte-des-jours.md) : exemples de décompte repris dans US1.
- [Circuit de validation](../../docs/wiki/Circuit-de-validation.md) : algorithme du valideur (FR-012).
- [Codes d'erreur](../../docs/wiki/Codes-d-erreur.md) : référentiel fermé des codes.
- **Énoncé** : `ENONCE.md`, partie 1 (contrat d'API).
- **Design system** : non applicable, cette fonctionnalité n'a pas d'interface utilisateur.
