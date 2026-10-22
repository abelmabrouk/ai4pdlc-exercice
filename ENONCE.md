# Énoncé de l'exercice

Vous travaillez avec Claude Code et deux outils installés pendant le setup ([SETUP.md](SETUP.md)). Chacun a son
rôle :

- **Spec Kit** structure le travail : spécification, plan, tâches. C'est l'ossature de chaque partie.
- **Superpowers** n'intervient qu'à l'implémentation, pour la discipline : TDD, débogage, vérification.

## Méthode de travail (pour chaque partie)

| Étape | Outil | Ce que vous obtenez |
|---|---|---|
| 1. Spécifier | `/speckit-specify` (puis `/speckit-clarify` si des zones restent floues) | `specs/<n>-<nom>/spec.md` : user stories et critères d'acceptation Given/When/Then |
| 2. Planifier | `/speckit-plan` | `plan.md`, conforme à la constitution |
| 3. Découper | `/speckit-tasks` | `tasks.md` : des tâches petites et testables |
| 4. Implémenter | `/speckit-implement`, avec `/superpowers:test-driven-development` | Un test qui échoue d'abord, puis le code qui le fait passer |
| 5. Déboguer si besoin | `/superpowers:systematic-debugging` | Une cause racine identifiée avant toute correction |
| 6. Vérifier | `/superpowers:verification-before-completion` | Tests et build réellement passés avant de déclarer la tâche terminée |

Les étapes 1 à 3 se font uniquement avec Spec Kit. N'utilisez pas les skills de cadrage ou de planification de
Superpowers (`brainstorming`, `writing-plans`) : ils feraient doublon avec le cycle Spec Kit.

Relisez chaque artefact avant de passer à l'étape suivante : c'est vous qui validez, pas l'IA.

Travaillez dans votre fork, sur votre propre branche. À la fin de chaque partie, poussez-la : votre pull request
vers le dépôt de l'atelier se met à jour (voir [SETUP.md](SETUP.md), § 7).

---

## Partie 1 — Soumission et validation des demandes de congé (back-end, 40 min)

> Un collaborateur peut soumettre une demande de congé ; la personne habilitée peut l'approuver ou la
> refuser ; le solde est mis à jour. Le tout en respectant les pratiques de l'équipe.

### Contrat d'API à respecter

Le front et les autres équipes s'appuient sur ce contrat : les routes et les formats ne sont pas négociables.

L'utilisateur courant est identifié par l'en-tête `X-User-Id`. Le JSON est en camelCase, les énumérations sont
des chaînes et les dates sont au format `yyyy-MM-dd`.

**Soumettre une demande**

```http
POST /api/leave-requests
X-User-Id: 3
Content-Type: application/json

{ "type": "PaidLeave", "startDate": "2027-03-08", "endDate": "2027-03-12", "comment": "Vacances" }
```

`type` vaut `PaidLeave` ou `FamilyEvent`.

En cas de succès, l'API renvoie `201 Created` avec la demande créée, au même format que
`GET /api/leave-requests/{id}` et avec le statut `Pending`.

**Lister les demandes à décider**

```http
GET /api/leave-requests/pending
X-User-Id: 2
```

L'API renvoie `200 OK` avec la liste des demandes `Pending` que l'utilisateur courant est habilité à décider.

**Approuver**

```http
POST /api/leave-requests/{id}/approve
X-User-Id: 2
```

L'API renvoie `200 OK` avec la demande mise à jour (statut `Approved`).

**Refuser**

```http
POST /api/leave-requests/{id}/reject
X-User-Id: 2
Content-Type: application/json

{ "reason": "Période de forte activité" }
```

L'API renvoie `200 OK` avec la demande mise à jour (statut `Rejected`).

---

## Partie 2 — Écran « Demandes en attente » (front-end, 30 min)

> Un manager dispose d'un écran « Demandes en attente » qui liste les demandes qu'il doit décider. Il peut
> les filtrer, puis les approuver ou les refuser. Le tout en respectant les pratiques de l'équipe.

Ce qui est déjà en place :

- **La route** `/manager/demandes` existe et pointe vers le composant vide `PendingRequestsPage`, dans
  `frontend/src/app/features/pending-requests/`.
- **Le service HTTP** `LeaveRequestService` (`frontend/src/app/core/`) couvre déjà le contrat d'API de la
  partie 1 : `pending()`, `approve(id)` et `reject(id, reason)`.
- **L'utilisateur courant** se choisit dans le sélecteur « Connecté en tant que » de l'en-tête.

Vous ne construisez que l'écran.
