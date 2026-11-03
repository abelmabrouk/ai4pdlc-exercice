# Quickstart — validation de l'écran « Demandes en attente »

Références : [contrat d'interface](contracts/ui-contract.md), [modèle](data-model.md).

## Prérequis

```bash
docker compose up -d              # front http://localhost:4200, API http://localhost:5080
docker compose restart api        # repartir des données de démonstration
```

## 1. Contrôles automatiques

```bash
docker compose exec web npx ng build          # compile sans erreur
docker compose exec web npm run lint:styles   # aucune erreur stylelint
docker compose run --rm backend-tests         # non-régression du back (inchangé)
```

## 2. Scénarios dans le navigateur (http://localhost:4200/manager/demandes)

| # | Action | Attendu |
|---|---|---|
| 1 | « Connecté en tant que » Hugo Bernard | 3 lignes : Lea, Karim, Ines, triées par date de début |
| 2 | Basculer sur Claire Martin | 1 ligne : Hugo Bernard |
| 3 | Basculer sur Sophie Laurent | Thomas Roux et Yasmine Haddad |
| 4 | Basculer sur Lea Petit | lien « Demandes en attente » absent ; URL directe → panneau « Aucune demande en attente » |
| 5 | Hugo, filtre collaborateur « KAR » | seule la ligne Karim |
| 6 | Hugo, filtre collaborateur « inès » (accent) | seule la ligne Ines |
| 7 | Sophie, type « Événement familial » | seule la ligne Thomas |
| 8 | Hugo, « Du » après toutes les périodes | panneau « Aucune demande ne correspond aux filtres » ; « Effacer les filtres » → 3 lignes |
| 9 | Hugo, « Approuver » sur Lea → dialogue → Échap | dialogue fermé, 3 lignes |
| 10 | Hugo, « Approuver » sur Lea → Confirmer | toast succès « Demande de Lea Petit approuvée. », 2 lignes ; « Mes demandes » en tant que Lea : solde 25, demande « Approuvée » |
| 11 | Hugo, « Refuser » sur Karim | bouton « Refuser » désactivé tant que le motif est vide ou blanc |
| 12 | Saisir « Période de forte activité » → Refuser | toast succès, 1 ligne ; « Mes demandes » de Karim : « Refusée » avec le motif |
| 13 | Hugo, filtre « Ines » actif, approuver Ines (2 jours, solde 3) | toast succès ; le filtre reste rempli et le panneau « Aucune demande ne correspond aux filtres » s'affiche |
| 14 | Ouvrir deux onglets en tant que Hugo, approuver la même demande dans les deux | second onglet : toast d'erreur « La demande a déjà été décidée. », liste rechargée sans la demande |
| 15 | Arrêter l'API (`docker compose stop api`) puis recharger l'écran | panneau d'erreur + « Réessayer » ; `docker compose start api` puis « Réessayer » → liste |
