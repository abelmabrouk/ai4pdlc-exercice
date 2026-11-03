# Feature Specification: Soumission et validation des demandes de congé

**Feature Branch**: `julien-guillaume-damien`

**Created**: 2026-11-03

**Status**: Draft

**Input**: User description: "Un collaborateur peut soumettre une demande de congé ; la personne habilitée peut l'approuver ou la refuser ; le solde est mis à jour. Le tout en respectant les pratiques de l'équipe."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Soumettre une demande de congé (Priority: P1)

Un collaborateur saisit le type de congé (congés payés ou événement familial), une date de début, une date de fin
et un commentaire facultatif. La demande est enregistrée « en attente » avec le nombre de jours ouvrés décomptés,
et elle devient visible dans la liste des demandes existante.

**Why this priority**: Sans soumission, il n'y a rien à décider ni à décompter : c'est le point d'entrée de tout
le processus.

**Independent Test**: Se connecter en tant que Lea Petit, soumettre une demande de congés payés du lundi au
vendredi d'une semaine sans jour férié, puis consulter la demande : elle est « en attente » et décompte 5 jours.

**Acceptance Scenarios**:

1. **Given** Lea Petit (solde 30 jours) sans demande sur la période, **When** elle soumet une demande de congés
   payés du lundi 8 au vendredi 12 mars 2027 avec le commentaire « Vacances », **Then** la demande est créée avec
   le statut « en attente », 5 jours décomptés, sa date de soumission, et son solde reste à 30 jours.
2. **Given** une période contenant un week-end ou un jour férié, **When** un collaborateur soumet une demande
   couvrant cette période, **Then** seuls les jours ouvrés (hors samedis, dimanches, jours fériés et fermetures
   de l'entreprise) sont décomptés.
3. **Given** Ines Moreau (solde 3 jours, dont 2 déjà engagés par une demande en attente), **When** elle soumet
   une demande de congés payés de 2 jours ouvrés, **Then** la demande est refusée à la saisie avec un message
   indiquant que le solde disponible est insuffisant.
4. **Given** une demande dont la date de fin précède la date de début, ou dont la date de début est passée,
   **When** elle est soumise, **Then** elle est rejetée avec un message d'erreur explicite et rien n'est enregistré.
5. **Given** un collaborateur ayant déjà une demande en attente ou approuvée du 8 au 12 mars, **When** il soumet
   une demande qui chevauche cette période, **Then** elle est rejetée avec un message signalant le chevauchement.
6. **Given** une demande de type « événement familial », **When** elle est soumise, **Then** elle est acceptée
   quel que soit le solde de congés payés.
7. **Given** Claire Martin (manager de la Direction, équipe sans équipe parente), **When** elle soumet une demande
   valide, **Then** la demande est créée « en attente », comme pour tout collaborateur, et son solde est inchangé.

---

### User Story 2 - Consulter les demandes à décider (Priority: P2)

La personne habilitée (le manager de l'équipe du demandeur) consulte la liste des demandes en attente qu'elle
est autorisée à décider, et uniquement celles-là.

**Why this priority**: Le décideur doit pouvoir retrouver ce qui l'attend avant de décider ; c'est aussi le
support de l'écran « Demandes en attente » de la partie 2.

**Independent Test**: Avec les données de démonstration, se connecter en tant que Hugo Bernard et vérifier que
la liste contient les demandes en attente de Lea, Karim et Ines, mais ni la sienne ni celles de l'équipe Support.

**Acceptance Scenarios**:

1. **Given** les données de démonstration, **When** Hugo Bernard (manager Produit) consulte ses demandes à
   décider, **Then** il voit uniquement les demandes en attente des membres de l'équipe Produit autres que lui.
2. **Given** les données de démonstration, **When** Claire Martin (manager de la Direction) consulte ses demandes
   à décider, **Then** elle voit la demande en attente de Hugo Bernard (manager d'une équipe rattachée à la
   Direction).
3. **Given** un collaborateur qui ne manage aucune équipe, **When** il consulte ses demandes à décider, **Then**
   la liste est vide.
4. **Given** des demandes approuvées ou refusées, **When** un manager consulte ses demandes à décider, **Then**
   elles n'apparaissent pas.
5. **Given** une demande en attente de Claire Martin, **When** Claire consulte ses demandes à décider, **Then** sa
   propre demande y figure, avec celles de Hugo Bernard et Sophie Laurent.

---

### User Story 3 - Approuver une demande et mettre à jour le solde (Priority: P1)

La personne habilitée approuve une demande en attente. La demande passe « approuvée », la date et l'auteur de la
décision sont conservés et, pour des congés payés, le solde du demandeur est diminué du nombre de jours décomptés.

**Why this priority**: C'est la finalité du processus : l'approbation et la mise à jour du solde.

**Independent Test**: Se connecter en tant que Hugo Bernard, approuver la demande de 5 jours de Lea Petit, puis
vérifier le statut de la demande et le solde de Lea (25 jours).

**Acceptance Scenarios**:

1. **Given** la demande de congés payés de 5 jours de Lea Petit en attente et un solde de 30 jours, **When** Hugo
   Bernard l'approuve, **Then** la demande est « approuvée », Hugo est enregistré comme décideur avec la date de
   décision, et le solde de Lea passe à 25 jours.
2. **Given** une demande « événement familial » en attente, **When** le manager habilité l'approuve, **Then** la
   demande est « approuvée » et le solde de congés payés du demandeur est inchangé.
3. **Given** une demande en attente de Karim Benali, **When** Sophie Laurent (manager Support) tente de
   l'approuver, **Then** l'action est interdite et la demande reste inchangée.
4. **Given** sa propre demande en attente, **When** Hugo Bernard tente de l'approuver, **Then** l'action est
   interdite.
5. **Given** une demande déjà approuvée ou refusée, **When** le manager habilité tente de l'approuver, **Then**
   l'action est refusée pour conflit d'état et ni la demande ni le solde ne changent.
6. **Given** une demande de congés payés dont le nombre de jours dépasse le solde actuel du demandeur au moment
   de la décision, **When** le manager tente de l'approuver, **Then** l'approbation est refusée et le solde ne
   devient jamais négatif.
7. **Given** une demande de congés payés de 5 jours de Claire Martin en attente et un solde de 30 jours, **When**
   Claire l'approuve elle-même, **Then** la demande est « approuvée », Claire est enregistrée comme décideur et son
   solde passe à 25 jours (elle peut de même la refuser).

---

### User Story 4 - Refuser une demande avec un motif (Priority: P2)

La personne habilitée refuse une demande en attente en indiquant obligatoirement un motif. Le solde n'est pas
modifié.

**Why this priority**: Indispensable pour clore une demande non acceptée, mais sans effet sur le solde.

**Independent Test**: Se connecter en tant que Hugo Bernard, refuser la demande de Karim Benali avec le motif
« Période de forte activité », puis vérifier le statut, le motif et le solde de Karim (inchangé).

**Acceptance Scenarios**:

1. **Given** une demande en attente de Karim Benali, **When** Hugo Bernard la refuse avec le motif « Période de
   forte activité », **Then** la demande est « refusée », le motif, le décideur et la date de décision sont
   enregistrés, et le solde de Karim est inchangé.
2. **Given** une demande en attente, **When** le manager habilité la refuse sans motif ou avec un motif vide,
   **Then** le refus est rejeté avec un message indiquant que le motif est obligatoire.
3. **Given** une demande déjà décidée, ou un utilisateur non habilité, **When** un refus est tenté, **Then** il
   est rejeté selon les mêmes règles que l'approbation.

---

### Edge Cases

- Demande ne contenant aucun jour ouvré (week-end seul, jour férié seul) : rejetée, car elle décompterait 0 jour.
- Demande d'un jour (début = fin) sur un jour ouvré : acceptée, 1 jour décompté.
- Demande à cheval sur deux années ou contenant une fermeture d'entreprise (ex. 24 décembre) : les jours fériés
  et fermetures des deux années sont exclus du décompte.
- Lundi de Pentecôte : jour travaillé dans l'entreprise, donc décompté.
- Demande d'un manager d'équipe : décidée par le manager de l'équipe parente (Hugo → Claire).
- Demande de Claire Martin (manager de l'équipe racine, sans équipe parente) : elle reste « en attente » et c'est
  Claire qui la décide elle-même (voir FR-018) ; c'est la seule exception à l'interdiction de décider sa propre demande.
- Utilisateur courant absent ou inconnu : toute action est refusée comme non authentifiée.
- Demande inexistante : l'approbation ou le refus signale que la demande est introuvable.
- Deux décisions simultanées sur la même demande : une seule est appliquée, le solde n'est débité qu'une fois.
- Type de congé inconnu ou dates mal formées : rejet avec message explicite.

## Requirements *(mandatory)*

### Functional Requirements

**Soumission**

- **FR-001**: Le système DOIT permettre à l'utilisateur courant de soumettre une demande de congé pour lui-même,
  avec un type (congés payés ou événement familial), une date de début, une date de fin et un commentaire facultatif.
- **FR-002**: Le système DOIT enregistrer toute nouvelle demande avec le statut « en attente » et sa date de soumission, et la restituer dans le même format que la consultation d'une demande existante.
- **FR-003**: Le système DOIT calculer le nombre de jours décomptés comme le nombre de jours ouvrés entre les dates
  de début et de fin incluses, en excluant samedis, dimanches, jours fériés et fermetures de l'entreprise, selon le
  calendrier de l'entreprise déjà en place.
- **FR-004**: Le système DOIT rejeter une demande dont la date de fin précède la date de début, dont la date de
  début est antérieure à la date du jour, ou qui ne décompte aucun jour ouvré.
- **FR-005**: Le système DOIT rejeter une demande qui chevauche une autre demande en attente ou approuvée du même
  collaborateur.
- **FR-006**: Pour les congés payés, le système DOIT rejeter une demande dont le nombre de jours dépasse le solde
  disponible, c'est-à-dire le solde actuel diminué des jours des demandes de congés payés encore en attente du
  même collaborateur.
- **FR-007**: Le système NE DOIT PAS modifier le solde lors de la soumission.

**Habilitation**

- **FR-008**: La personne habilitée à décider une demande DOIT être le manager de l'équipe du demandeur ; si le
  demandeur est lui-même ce manager, c'est le manager de l'équipe parente ; s'il n'y a pas d'équipe parente, c'est
  le demandeur lui-même (FR-018).
- **FR-009**: Le système DOIT interdire à quiconque de décider sa propre demande (sauf cas de FR-018), et à tout
  utilisateur non habilité de décider une demande.
- **FR-018**: Le manager d'une équipe sans équipe parente (Direction) DOIT être la personne habilitée à décider ses
  propres demandes : elles apparaissent dans sa liste de demandes à décider et il peut les approuver ou les refuser
  selon les mêmes règles que toute autre demande.

**Consultation des demandes à décider**

- **FR-010**: Le système DOIT fournir à l'utilisateur courant la liste des demandes en attente qu'il est habilité
  à décider, et uniquement celles-ci.

**Décision**

- **FR-011**: Le système DOIT permettre à la personne habilitée d'approuver une demande en attente ; la demande
  passe « approuvée » avec l'auteur et la date de la décision.
- **FR-012**: À l'approbation d'une demande de congés payés, le système DOIT diminuer le solde du demandeur du
  nombre de jours décomptés, dans la même opération que le changement de statut ; l'approbation DOIT être refusée
  si elle rendait le solde négatif.
- **FR-013**: L'approbation d'une demande « événement familial » NE DOIT PAS modifier le solde.
- **FR-014**: Le système DOIT permettre à la personne habilitée de refuser une demande en attente avec un motif
  obligatoire et non vide ; la demande passe « refusée » avec le motif, l'auteur et la date de la décision, sans
  effet sur le solde.
- **FR-015**: Le système DOIT refuser toute décision sur une demande qui n'est plus en attente, sans aucune
  modification.
- **FR-016**: La date de décision et la date de soumission DOIVENT provenir de l'horloge du système.

**Erreurs**

- **FR-017**: Chaque refus DOIT être signalé avec une catégorie distincte et un message compréhensible : entrée
  invalide, non authentifié, action interdite, demande introuvable, conflit d'état ou de solde.

### Key Entities

- **Demande de congé**: demandeur, type, dates de début et de fin, nombre de jours décomptés, statut (en attente,
  approuvée, refusée), commentaire, motif de refus, date de soumission, date et auteur de la décision.
- **Collaborateur**: appartient à une équipe, possède un solde de congés payés en jours.
- **Équipe**: a un manager (membre de l'équipe) et éventuellement une équipe parente ; détermine qui est habilité
  à décider.
- **Calendrier de l'entreprise**: jours fériés et fermetures, utilisé pour le décompte des jours.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100 % des scénarios d'acceptation ci-dessus sont couverts par des tests automatisés qui passent.
- **SC-002**: Après toute séquence d'approbations et de refus, le solde de chaque collaborateur est égal à son
  solde initial moins la somme des jours des demandes de congés payés approuvées dans cette séquence, et n'est
  jamais négatif.
- **SC-003**: Aucune demande ne peut être décidée par une personne autre que la personne habilitée (0 cas sur
  l'ensemble des combinaisons demandeur/décideur des données de démonstration).
- **SC-004**: Un collaborateur soumet une demande, et son manager la décide, en une seule action chacun, avec un
  résultat visible immédiatement.
- **SC-005**: Les clients existants (front et autres équipes) utilisent la fonctionnalité sans adaptation, le
  contrat d'échange imposé étant respecté à l'identique.

## Assumptions

- Le contrat d'échange (routes, formats, codes de succès) est imposé par l'énoncé de l'exercice (`ENONCE.md`,
  partie 1) et n'est pas négociable.
- L'utilisateur courant est identifié par le mécanisme existant ; aucune authentification nouvelle n'est requise.
- Le solde n'existe que pour les congés payés ; les congés « événement familial » ne sont pas plafonnés ni
  décomptés du solde.
- Le solde est débité à l'approbation et non à la soumission, comme le montrent les données de démonstration
  (Yasmine Haddad : 25 jours après une demande de 5 jours approuvée).
- Seules les journées entières sont gérées (pas de demi-journées).
- Sont hors périmètre : annulation ou modification d'une demande, délégation de décision, notifications,
  demandes saisies pour le compte d'un tiers, écran front (partie 2).
- Les pratiques de l'équipe s'appliquent : constitution du projet, réutilisation des services existants
  (notamment le calendrier des jours fériés) et format d'erreur standard du projet.
