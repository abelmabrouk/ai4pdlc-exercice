# Feature Specification: Écran « Demandes en attente »

**Feature Branch**: `julien-guillaume-damien`

**Created**: 2026-11-03 · **Révisée** : 2026-11-03 (alignement sur le wiki et le design system « Ardoise »)

**Status**: Draft

**Input**: User description: "Un manager dispose d'un écran « Demandes en attente » qui liste les demandes qu'il doit décider. Il peut les filtrer, puis les approuver ou les refuser. Le tout en respectant les pratiques de l'équipe."

## Références

Documentation de l'équipe : https://github.com/abelmabrouk/ai4pdlc-exercice/wiki (elle fait foi, ADR-006).

| Référence | Objet |
|---|---|
| ADR-003 | Le front se fonde uniquement sur `code` ; jamais sur `title` ni `detail` |
| ADR-005 | Utilisateur courant (`X-User-Id`, sélecteur « Connecté en tant que ») |
| ADR-006 | Rédaction des specs (Références, traçabilité RG, codes d'erreur) |
| RG-10, RG-11 | Seul le valideur décide ; une demande décidée ne l'est qu'une fois |
| RG-12 | Approbation |
| RG-13 | Refus : motif d'au moins 10 caractères |
| Circuit de validation | La liste ne contient que les demandes dont l'utilisateur est le valideur |
| Codes d'erreur / Messages | Libellé affiché pour chaque code |
| Design system, Composants, Interactions, Design tokens | Règles de l'écran |
| Conventions data-testid | Attributs de recette QA |
| Maquette « Demandes en attente » | Rendu attendu |
| `specs/001-leave-request-approval` | API consommée (inchangée) |

## Composants utilisés

| Composant / classe | Usage |
|---|---|
| `app-data-table` (`DataTable`, `DataTableCell`) | Tableau des demandes, `rowTestId` = `leave-row-{id}` |
| `app-status-badge` | Colonne « Statut » |
| `app-confirm-dialog` | Refus uniquement : `tone="danger"`, `reasonLabel`, `[reasonMinLength]="10"`, `busy` |
| `ToastService` (`app-toast` déjà dans le shell) | Retours de succès et d'erreur |
| `.toolbar`, `.field`, `.field__label`, `.select`, `.input` | Barre de filtres |
| `.btn--primary` / `.btn--danger` + `.btn--sm` ; `.btn--secondary` | Actions de ligne ; « Réessayer » |
| `.skeleton` (3 × `.skeleton__row`), `.state-panel` (+ `--error`), `.state-panel__icon` | États d'écran |

**Règles d'interaction appliquées** (page *Interactions*) : un seul état d'écran à la fois ; approbation en un clic
sans confirmation ; refus jamais en un clic ; boutons de la ligne désactivés pendant l'appel ; après un succès,
toast et disparition de la ligne ; après un échec, toast du libellé du `code` puis rechargement ; filtres dans la
`.toolbar`, appliqués immédiatement sur la liste chargée, recherche insensible à la casse ; jamais de message
technique.

**Libellés** : page *Messages*, section « Écran Demandes en attente » et table des codes d'erreur, repris à
l'identique.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Voir les demandes à décider (Priority: P1)

Le manager ouvre « Demandes en attente » et voit les demandes dont il est le valideur.

**Why this priority**: point d'entrée de l'écran.

**Independent Test**: en tant que Hugo Bernard, 3 lignes : Lea Petit, Karim Benali, Ines Moreau.

**Acceptance Scenarios**:

1. **[RG-10, Circuit de validation]** **Given** Hugo connecté, **When** il ouvre l'écran, **Then** il voit les 3
   demandes de son équipe avec les colonnes Collaborateur, Équipe, Type, Période (« du jj/mm/aaaa au jj/mm/aaaa »),
   Jours, Statut (pastille « En attente ») et Actions.
2. **Given** Claire Martin connectée, **When** elle ouvre l'écran, **Then** elle voit la demande de Hugo.
3. **Given** le chargement en cours, **Then** seul le squelette (3 lignes, `pending-loading`) est affiché.
4. **Given** une liste vide, **Then** seul le panneau ✅ « Aucune demande en attente. » (`pending-empty`) est affiché.
5. **Given** un échec de chargement, **Then** seul le panneau d'erreur « Impossible de charger les demandes en
   attente. » (`pending-error`) est affiché, avec « Réessayer » (`btn-retry`) qui relance le chargement.
6. **Given** l'écran ouvert, **When** l'utilisateur change d'identité, **Then** la liste se recharge.

---

### User Story 2 - Approuver une demande (Priority: P1)

**Why this priority**: action principale, en un seul clic.

**Independent Test**: Hugo clique « Approuver » sur Lea : toast « Demande approuvée. », la ligne disparaît.

**Acceptance Scenarios**:

1. **[RG-12]** **Given** la ligne de Lea, **When** Hugo clique « Approuver » (`btn-approve-{id}`), **Then** la demande
   est approuvée sans confirmation, un toast de succès « Demande approuvée. » s'affiche et la ligne disparaît.
2. **Given** l'appel en cours, **Then** les boutons « Approuver » et « Refuser » de cette ligne sont désactivés.
3. **[RG-11]** **Given** une demande déjà traitée ailleurs, **When** Hugo l'approuve, **Then** un toast d'erreur
   « Cette demande a déjà été traitée. » (`LEAVE_ALREADY_DECIDED`) s'affiche et la liste est rechargée.
4. **[RG-12]** **Given** un solde devenu insuffisant, **Then** le toast d'erreur est « Le solde de congés est
   insuffisant. » (`LEAVE_INSUFFICIENT_BALANCE`).

---

### User Story 3 - Refuser une demande avec un motif (Priority: P2)

**Why this priority**: clôt les demandes non acceptées ; jamais en un clic.

**Independent Test**: Hugo refuse Karim avec « Période de forte activité » : toast « Demande refusée. », la ligne
disparaît.

**Acceptance Scenarios**:

1. **Given** la ligne de Karim, **When** Hugo clique « Refuser » (`btn-reject-{id}`), **Then** le dialogue « Refuser la
   demande » s'ouvre avec le message « Demande de Karim Benali du 14/12/2026 au 16/12/2026. » et le champ « Motif du
   refus ».
2. **[RG-13]** **Given** le dialogue ouvert, **When** le motif fait moins de 10 caractères (espaces de bord exclus),
   **Then** le bouton « Refuser » reste désactivé et l'indicateur « n / 10 caractères minimum » est affiché.
3. **[RG-13, RG-14]** **Given** un motif valide, **When** Hugo confirme, **Then** les boutons du dialogue sont
   désactivés pendant l'appel, puis le dialogue se ferme, un toast « Demande refusée. » s'affiche et la ligne disparaît.
4. **Given** un refus rejeté par l'API, **Then** le dialogue se ferme, un toast d'erreur affiche le libellé du `code`
   et la liste est rechargée.
5. **Given** le dialogue ouvert, **When** Hugo annule (bouton, Échap, clic sur le fond), **Then** rien n'est envoyé.

---

### User Story 4 - Filtrer la liste (Priority: P2)

**Why this priority**: affiner la liste ; l'écran reste utilisable sans filtre.

**Independent Test**: Hugo saisit « KAR » : seule la ligne de Karim reste.

**Acceptance Scenarios**:

1. **Given** les 3 demandes de Produit, **When** Hugo saisit « KAR » dans « Collaborateur » (`filter-search`,
   placeholder « Rechercher un collaborateur »), **Then** seule la ligne de Karim reste (recherche insensible à la casse).
2. **Given** Sophie Laurent et des demandes de types différents, **When** elle choisit « Événement familial » dans
   « Type » (`filter-type`), **Then** seule la demande de Thomas Roux reste ; « Tous les types » les réaffiche toutes.
3. **Given** des filtres ne retenant aucune demande, **Then** seul le panneau 🔍 « Aucune demande ne correspond aux
   filtres. » (`pending-no-match`) est affiché.
4. **Given** des filtres actifs, **When** une demande est décidée, **Then** les filtres sont conservés.

---

### Edge Cases

- Collaborateur qui n'est valideur de rien : le lien « Demandes en attente » n'apparaît que pour un manager ; par
  l'URL directe, il voit l'état vide.
- Erreur réseau, 500 ou code inconnu : toast « Une erreur inattendue est survenue. Veuillez réessayer. »
- Changement d'identité pendant que le dialogue de refus est ouvert : le dialogue se ferme sans rien envoyer.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001** *(Circuit de validation)*: L'écran affiche les demandes en attente renvoyées par l'API pour l'utilisateur
  courant, sans recalculer l'habilitation.
- **FR-002** *(Messages)*: Colonnes, dans l'ordre : Collaborateur, Équipe, Type (Congés payés / Événement familial),
  Période (« du {début} au {fin} », jj/mm/aaaa), Jours, Statut (`app-status-badge`), Actions.
- **FR-003** *(Interactions)*: Exactement un état est affiché parmi chargement, vide, aucun résultat, erreur et données,
  avec les visuels, libellés et `data-testid` du wiki.
- **FR-004**: La liste se recharge quand l'utilisateur courant change.
- **FR-005** *(Interactions)*: Filtres dans une `.toolbar` au-dessus du tableau : « Type » (Tous les types, Congés payés,
  Événement familial) puis « Collaborateur » (recherche sur le nom, insensible à la casse) ; combinés, appliqués
  immédiatement sur la liste chargée, conservés après une décision.
- **FR-006** *(Interactions, RG-12)*: « Approuver » décide en un clic, sans confirmation.
- **FR-007** *(Interactions, RG-13)*: « Refuser » ouvre `app-confirm-dialog` (`tone="danger"`, motif d'au moins
  10 caractères) ; le dialogue se ferme après la réponse de l'API, succès ou échec.
- **FR-008** *(Interactions)*: Pendant un appel, les boutons de la ligne concernée sont désactivés (et ceux du dialogue
  pour un refus).
- **FR-009** *(Interactions, Messages)*: Après un succès : toast « Demande approuvée. » ou « Demande refusée. » et
  disparition de la ligne.
- **FR-010** *(ADR-003, Messages)*: Après un échec : toast d'erreur du libellé associé au `code` (table de *Messages*),
  ou libellé générique si le code est absent ou inconnu ; jamais `title`, `detail` ni message HTTP ; puis rechargement.
- **FR-011** *(Conventions data-testid)*: `pending-requests-page`, `leave-row-{id}`, `btn-approve-{id}`,
  `btn-reject-{id}`, `filter-type`, `filter-search`, `pending-loading`, `pending-empty`, `pending-no-match`,
  `pending-error`, `btn-retry` ; les attributs des composants partagés ne sont pas redéfinis.
- **FR-012** *(Composants, Design tokens)*: Uniquement les composants partagés et classes globales ; aucune librairie
  UI ; aucune valeur de couleur, d'espacement ou de taille en dur.

### Key Entities

- **Demande à décider**: collaborateur, équipe, type, période, jours, statut.
- **Filtres**: type de congé, texte de recherche sur le collaborateur.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Approuver = 1 clic ; refuser = 1 clic, saisie du motif, 1 clic.
- **SC-002**: 100 % des `data-testid` de la page *Conventions data-testid* sont présents (recette QA).
- **SC-003**: 100 % des libellés affichés correspondent à l'identique à la page *Messages*.
- **SC-004**: Aucun appel en double pour une même action, y compris en cas de clics répétés.
- **SC-005**: `ng build` et `lint:styles` sans erreur ; rendu conforme à la maquette.

## Assumptions

- L'API de la partie 1 (révisée selon le wiki) fournit `code` sur toute erreur métier ; aucune modification du back.
- Hors périmètre : filtre de période, décision par lot, pagination, mémorisation des filtres, bouton « Effacer les
  filtres » (absents de la maquette et du wiki).
