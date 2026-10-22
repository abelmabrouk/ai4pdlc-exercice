# CLAUDE.md

Application de gestion des congés : API .NET 8 + front Angular 20. Tout tourne dans Docker ;
aucun SDK .NET ni Node n'est nécessaire sur le poste.

## Commandes

```bash
docker compose up -d                          # API http://localhost:5080 (Swagger : /swagger) + front http://localhost:4200
docker compose logs -f api                    # logs de l'API (recompilée et redémarrée à chaque modification)
docker compose logs -f web                    # logs du front (ng serve)
docker compose run --rm backend-tests         # tests xUnit du back
docker compose exec web npx ng build          # vérifier que le front compile
docker compose exec web npm run lint:styles   # stylelint
docker compose restart api                    # recrée la base SQLite avec les données de démonstration
```

## Structure

- `backend/src/Conges.{Domain,Application,Infrastructure,Api}` : architecture en couches (voir la constitution).
- `backend/tests/Conges.Tests` : tests xUnit (`CongesApiFactory` pour les tests d'API).
- `frontend/src/app/core` : modèles, services HTTP, utilisateur courant.
- `frontend/src/app/shared` : composants partagés.
- `frontend/src/app/features` : écrans.
- `specs/` : spécifications produites avec Spec Kit.

## Conventions

La constitution du projet est dans `constitution.md`. Une copie est placée dans `.specify/memory/` par
l'initialisation de Spec Kit.
Utilisateur courant : en-tête HTTP `X-User-Id` (sélecteur « Connecté en tant que » dans le front).
