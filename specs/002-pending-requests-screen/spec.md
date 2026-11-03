# Feature Specification: Écran « Demandes en attente »

**Feature Branch**: `julien-guillaume-damien`

**Created**: 2026-11-03

**Status**: Draft

**Input**: User description: "Un manager dispose d'un écran « Demandes en attente » qui liste les demandes qu'il doit décider. Il peut les filtrer, puis les approuver ou les refuser. Le tout en respectant les pratiques de l'équipe."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Voir les demandes à décider (Priority: P1)

Un manager ouvre l'écran « Demandes en attente » depuis le menu et voit la liste des demandes en attente qu'il est
habilité à décider : collaborateur, équipe, type de congé, période, nombre de jours et commentaire.

**Why this priority**: C'est le point d'entrée de l'écran ; sans liste, rien ne peut être filtré ni décidé.

**Independent Test**: Se connecter en tant que Hugo Bernard, ouvrir « Demandes en attente » et vérifier que les
demandes de Lea Petit, Karim Benali et Ines Moreau sont listées, et aucune autre.

**Acceptance Scenarios**:

1. **Given** Hugo Bernard connecté, **When** il ouvre l'écran, **Then** il voit les 3 demandes en attente de son
   équipe (Lea, Karim, Ines), triées par date de début, avec pour chacune le collaborateur, l'équipe, le type, la
   période, le nombre de jours et le commentaire.
2. **Given** Claire Martin connectée, **When** elle ouvre l'écran, **Then** elle voit la demande de Hugo Bernard.
3. **Given** un manager sans demande à décider, **When** il ouvre l'écran, **Then** un message indique qu'il n'y a
   aucune demande en attente.
4. **Given** le chargement en cours, **When** l'écran s'affiche, **Then** un indicateur de chargement est visible ;
   **Given** un échec de chargement, **Then** un message d'erreur et un bouton « Réessayer » sont proposés.
5. **Given** l'écran ouvert en tant que Hugo, **When** l'utilisateur change l'identité « Connecté en tant que »
   pour Sophie Laurent, **Then** la liste se recharge et affiche les demandes de l'équipe Support.

---

### User Story 2 - Approuver une demande (Priority: P1)

Le manager approuve une demande depuis la liste, après confirmation. La demande disparaît de la liste et une
notification confirme l'approbation.

**Why this priority**: C'est l'action principale attendue de l'écran.

**Independent Test**: En tant que Hugo, approuver la demande de Lea, confirmer, puis vérifier qu'elle a quitté la
liste et que « Mes demandes » de Lea l'affiche « Approuvée ».

**Acceptance Scenarios**:

1. **Given** la demande de Lea dans la liste, **When** Hugo clique sur « Approuver », **Then** une fenêtre de
   confirmation rappelle le collaborateur, la période et le nombre de jours.
2. **Given** la fenêtre de confirmation ouverte, **When** Hugo confirme, **Then** la demande est approuvée, elle
   disparaît de la liste et une notification de succès s'affiche.
3. **Given** la fenêtre de confirmation ouverte, **When** Hugo annule (bouton ou touche Échap), **Then** rien n'est
   modifié.
4. **Given** une décision en cours d'envoi, **When** elle n'est pas terminée, **Then** les boutons de la fenêtre
   sont désactivés pour empêcher un double envoi.
5. **Given** une approbation refusée par le système (solde insuffisant, demande déjà décidée, droits), **When**
   Hugo confirme, **Then** une notification d'erreur affiche le message explicatif et la liste est rechargée.

---

### User Story 3 - Refuser une demande avec un motif (Priority: P2)

Le manager refuse une demande en saisissant un motif obligatoire. La demande disparaît de la liste et une
notification confirme le refus.

**Why this priority**: Indispensable pour clore les demandes non acceptées, mais moins fréquent que l'approbation.

**Independent Test**: En tant que Hugo, refuser la demande de Karim avec le motif « Période de forte activité »,
puis vérifier qu'elle a quitté la liste et que « Mes demandes » de Karim l'affiche « Refusée » avec ce motif.

**Acceptance Scenarios**:

1. **Given** la demande de Karim dans la liste, **When** Hugo clique sur « Refuser », **Then** une fenêtre demande
   un motif de refus.
2. **Given** la fenêtre de refus ouverte, **When** le motif est vide ou ne contient que des espaces, **Then** le
   bouton de confirmation est désactivé.
3. **Given** un motif saisi, **When** Hugo confirme, **Then** la demande est refusée avec ce motif, disparaît de la
   liste et une notification de succès s'affiche.
4. **Given** un refus rejeté par le système, **When** Hugo confirme, **Then** une notification d'erreur affiche le
   message explicatif et la liste est rechargée.

---

### User Story 4 - Filtrer la liste (Priority: P2)

Le manager affine la liste par collaborateur, par type de congé et par période, pour traiter les demandes par lot.

**Why this priority**: Utile dès que la liste grandit ; l'écran reste utilisable sans filtre.

**Independent Test**: En tant que Hugo, filtrer sur le collaborateur « Karim » : seule la demande de Karim reste ;
effacer le filtre : les 3 demandes réapparaissent.

**Acceptance Scenarios**:

1. **Given** les 3 demandes de l'équipe Produit, **When** Hugo saisit « kar » dans le filtre collaborateur, **Then**
   seule la demande de Karim Benali reste affichée (recherche insensible à la casse et aux accents, sur le nom
   complet).
2. **Given** des demandes de types différents, **When** le manager choisit le type « Événement familial », **Then**
   seules les demandes de ce type restent affichées ; « Tous les types » les réaffiche toutes.
3. **Given** une période « du » / « au » saisie, **When** le filtre s'applique, **Then** seules les demandes dont la
   période chevauche cet intervalle restent affichées ; une borne seule filtre d'un seul côté.
4. **Given** des filtres actifs ne retenant aucune demande, **When** la liste est vide, **Then** un message indique
   qu'aucune demande ne correspond aux filtres, avec une action « Effacer les filtres ».
5. **Given** des filtres actifs, **When** le manager décide une demande, **Then** les filtres sont conservés.
6. **Given** des filtres actifs, **When** le manager clique sur « Effacer les filtres », **Then** toutes les
   demandes à décider réapparaissent.

---

### Edge Cases

- Collaborateur qui n'est manager d'aucune équipe : le lien « Demandes en attente » n'apparaît pas dans le menu ;
  s'il ouvre l'adresse de l'écran directement, il voit la liste vide (aucune demande à décider).
- Demande décidée entre-temps par quelqu'un d'autre (ou dans un autre onglet) : l'action échoue avec le message du
  système et la liste est rechargée, la demande n'y figurant plus.
- Date de fin de filtre antérieure à la date de début : aucune demande ne correspond ; message « aucune demande ne
  correspond aux filtres ».
- Commentaire absent : la cellule reste vide, sans texte de remplacement trompeur.
- Changement d'identité pendant qu'une fenêtre de décision est ouverte : la fenêtre se ferme sans rien envoyer.
- Claire Martin : sa propre demande en attente apparaît dans sa liste et elle peut la décider (règle de la partie 1).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: L'écran DOIT afficher les demandes en attente que l'utilisateur courant est habilité à décider, telles
  que fournies par le système (aucune règle d'habilitation n'est recalculée par l'écran).
- **FR-002**: Chaque demande DOIT afficher : collaborateur, équipe, type de congé (libellé lisible), période
  (jj/mm/aaaa), nombre de jours et commentaire.
- **FR-003**: L'écran DOIT présenter des états distincts : chargement, erreur avec « Réessayer », liste vide, aucun
  résultat après filtrage.
- **FR-004**: La liste DOIT se recharger quand l'utilisateur courant change.
- **FR-005**: Le manager DOIT pouvoir filtrer par collaborateur (texte libre, insensible à la casse et aux accents,
  sur le nom complet), par type de congé (tous / congés payés / événement familial) et par période (du / au,
  bornes facultatives, critère de chevauchement). Les filtres se combinent (ET) et s'appliquent immédiatement.
- **FR-006**: Le manager DOIT pouvoir effacer tous les filtres en une action.
- **FR-007**: Le manager DOIT pouvoir approuver une demande après une confirmation rappelant collaborateur, période et
  nombre de jours.
- **FR-008**: Le manager DOIT pouvoir refuser une demande en saisissant un motif obligatoire (non vide après suppression
  des espaces) ; la confirmation est impossible sans motif.
- **FR-009**: Pendant l'envoi d'une décision, l'écran DOIT empêcher un second envoi.
- **FR-010**: Après une décision réussie, la demande DOIT disparaître de la liste et une notification de succès DOIT
  nommer le collaborateur concerné ; les filtres actifs sont conservés.
- **FR-011**: Après une décision échouée, une notification d'erreur DOIT afficher le message explicatif fourni par le
  système (ou un message générique s'il est absent), et la liste DOIT être rechargée.
- **FR-012**: L'écran DOIT être utilisable au clavier : boutons accessibles par tabulation, fermeture des fenêtres par
  Échap, libellés explicites sur les boutons d'action (ex. « Approuver la demande de Lea Petit »).
- **FR-013**: L'écran DOIT réutiliser les composants partagés de l'application (tableau, fenêtre de confirmation,
  notifications) pour rester cohérent avec « Mes demandes ».

### Key Entities

- **Demande à décider**: demande en attente vue par le manager : collaborateur, équipe, type, période, nombre de
  jours, commentaire.
- **Filtres**: collaborateur (texte), type de congé, date de début et date de fin de la période recherchée.
- **Décision**: approbation, ou refus accompagné d'un motif.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Un manager approuve ou refuse une demande en 3 actions au plus depuis l'ouverture de l'écran (cliquer,
  [saisir le motif], confirmer).
- **SC-002**: Après une décision, la liste et la notification reflètent le résultat en moins d'une seconde dans les
  conditions normales d'usage.
- **SC-003**: 100 % des scénarios d'acceptation ci-dessus sont vérifiés sur l'application lancée avec les données de
  démonstration.
- **SC-004**: Aucune décision n'est envoyée deux fois pour un même clic, y compris en cas de clics répétés.
- **SC-005**: L'écran est visuellement cohérent avec « Mes demandes » (mêmes composants et styles) et passe les
  contrôles de qualité du front (compilation et règles de style) sans erreur.

## Assumptions

- Le back-end de la partie 1 est livré : liste des demandes à décider, approbation et refus, avec messages d'erreur
  explicites. Le service d'accès existant du front couvre déjà ces opérations ; aucune modification du back-end
  n'est prévue.
- La liste des demandes à décider est de petite taille (une équipe) : le filtrage se fait sur la liste déjà
  chargée, sans nouvelle demande au système.
- Le motif de refus n'a pas de longueur minimale au-delà de « non vide », comme la règle du système (partie 1).
- Le solde du collaborateur n'est pas affiché sur cet écran (non fourni dans la liste) ; le système refuse lui-même
  une approbation qui rendrait le solde négatif.
- Hors périmètre : décision par lot (plusieurs demandes à la fois), historique des décisions, pagination, tri
  personnalisé, mémorisation des filtres entre deux visites.
- Les pratiques de l'équipe s'appliquent : constitution (composants autonomes, signals, styles dans des fichiers
  séparés, appels via les services du cœur), composants partagés existants, attributs de test (`data-testid`)
  comme sur les autres écrans.
