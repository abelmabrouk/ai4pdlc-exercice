# Quickstart — validation de l'écran « Demandes en attente »

> Révisé le 2026-11-03 (wiki). Références : [contrat d'interface](contracts/ui-contract.md).

## Prérequis

```bash
docker compose up -d
docker compose restart api        # données de démonstration
```

## 1. Contrôles automatiques

```bash
docker compose exec web npm run test:unit     # node --test : codes → libellés, filtres, messages
docker compose exec web npx ng build
docker compose exec web npm run lint:styles
docker compose run --rm backend-tests
```

## 2. Scénarios navigateur (http://localhost:4200/manager/demandes)

| # | Action | Attendu |
|---|---|---|
| 1 | Hugo Bernard | 3 lignes (Lea, Karim, Ines) ; colonnes Collaborateur, Équipe, Type, Période, Jours, Statut (« En attente »), Actions |
| 2 | Claire Martin | 1 ligne : Hugo Bernard |
| 3 | Lea Petit, URL directe | panneau ✅ « Aucune demande en attente. » |
| 4 | Hugo, « Collaborateur » = « KAR » | seule la ligne Karim |
| 5 | Sophie, « Type » = Événement familial | seule la ligne Thomas Roux |
| 6 | Hugo, recherche « zzz » | panneau 🔍 « Aucune demande ne correspond aux filtres. » |
| 7 | Hugo, « Approuver » sur Lea | pas de dialogue ; boutons de la ligne grisés pendant l'appel ; toast « Demande approuvée. » ; ligne retirée |
| 8 | Hugo, « Refuser » sur Karim | dialogue « Refuser la demande », « Demande de Karim Benali du … au …. » ; « Refuser » désactivé avec « Trop tôt » (8 / 10 caractères minimum) |
| 9 | Motif « Période de forte activité » → Refuser | dialogue fermé, toast « Demande refusée. », ligne retirée |
| 10 | Deux onglets Hugo, approuver la même demande | second onglet : toast « Cette demande a déjà été traitée. », liste rechargée |
| 11 | `docker compose stop api`, recharger | panneau d'erreur + « Réessayer » ; `docker compose start api` puis « Réessayer » → liste |
