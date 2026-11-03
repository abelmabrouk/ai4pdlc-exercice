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
- `docs/wiki/` : wiki de l'équipe (sous-module git, voir ci-dessous).

## Documentation de référence

Le wiki de l'équipe est dans `docs/wiki/` (sous-module de
https://github.com/abelmabrouk/ai4pdlc-exercice/wiki ; mise à jour : `git submodule update --remote docs/wiki`).
**Il fait foi** : en cas d'écart entre le wiki et une intuition « de bon sens », c'est le wiki qui l'emporte.
Le consulter avant toute spec, tout plan ou toute implémentation. Il contient notamment :

- Métier : `Glossaire.md`, `Regles-de-gestion.md` (RG-01 à RG-14), `Decompte-des-jours.md`,
  `Circuit-de-validation.md`.
- Technique : `ADR.md` et `ADR-00x-*.md`, `Codes-d-erreur.md` (liste fermée des codes `LEAVE_*`).
- Design system « Ardoise » : `Design-system.md`, `Design-tokens.md`, `Composants.md`, `Interactions.md`,
  `Messages.md`, `Conventions-data-testid.md`, `Maquette-demandes-en-attente.md` (image dans `images/`).

Pour toute spec : section « Références » obligatoire (ADR et RG-xx, voir ADR-006), et pour une spec front,
section « Composants utilisés » (voir `Design-system.md`).

Pages chargées en permanence :

- @docs/wiki/Regles-de-gestion.md
- @docs/wiki/Codes-d-erreur.md
- @docs/wiki/ADR-006-redaction-des-specifications.md

## Conventions

La constitution du projet est dans `constitution.md`. Une copie est placée dans `.specify/memory/` par
l'initialisation de Spec Kit.
Utilisateur courant : en-tête HTTP `X-User-Id` (sélecteur « Connecté en tant que » dans le front).
