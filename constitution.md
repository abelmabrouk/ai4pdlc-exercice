# Constitution — Application Congés

Ce document fixe les principes d'architecture et de développement de l'application Congés.
Tout plan technique (`/speckit-plan`) doit vérifier sa conformité à cette constitution.

> Après `specify init`, copiez ce fichier dans `.specify/memory/constitution.md` (voir SETUP.md).

## Principes fondamentaux

### I. Architecture en couches (NON NÉGOCIABLE)

Le back-end suit une architecture en couches stricte :

- `Conges.Domain` : entités et énumérations, sans dépendance.
- `Conges.Application` : cas d'usage (services applicatifs), DTOs, validateurs, abstractions (`IAppDbContext`,
  `ICurrentUser`, services transverses). Dépend uniquement de `Domain`.
- `Conges.Infrastructure` : EF Core, implémentations des abstractions. Dépend d'`Application` et de `Domain`.
- `Conges.Api` : controllers HTTP minces, configuration. Aucune logique métier dans un controller : un
  controller traduit HTTP ↔ appel d'un service applicatif, rien d'autre.

Les dépendances vont toujours vers l'intérieur. Un controller n'accède jamais au `DbContext`.

### II. Contrats explicites

- Les entrées et sorties HTTP sont des DTOs (`record`), jamais des entités EF.
- JSON en camelCase, énumérations sérialisées en chaînes, dates au format `yyyy-MM-dd` (`DateOnly`).
- La validation de forme des entrées utilise FluentValidation (validateurs dans `Application`).
- Les erreurs sont renvoyées au format ProblemDetails (RFC 7807) via le gestionnaire d'exceptions global.

### III. Réutilisation avant création

Avant de créer un service, un composant ou un utilitaire, vérifier qu'il n'existe pas déjà dans le repo.
Les services transverses existants sont la source de vérité de leur domaine.

### IV. Tests

- Tout cas d'usage nouveau est couvert par des tests xUnit dans `backend/tests/Conges.Tests`.
- Les tests d'API utilisent `CongesApiFactory` (base SQLite dédiée, données de démonstration).
- Les tests doivent passer avec `docker compose run --rm backend-tests` avant de considérer une tâche terminée.

### V. Temps et données

- Le temps courant s'obtient via `TimeProvider` (injecté), jamais via `DateTime.Now` en dur.
- Le schéma de base suit le modèle EF Core (`EnsureCreated`, base recréée au démarrage) : pas de migrations.
- La clé de configuration `ConnectionStrings:Conges` ne doit pas être renommée.

### VI. Front-end Angular moderne

- Angular 20, composants standalone uniquement (pas de NgModules).
- État local avec les signals (`signal`, `computed`, `input()`, `output()`, `rxResource`).
- Control flow natif (`@if`, `@for`, `@switch`) ; pas de `*ngIf` / `*ngFor`.
- Styles des composants dans des fichiers `.css` séparés (`styleUrl`).
- Les appels HTTP passent par les services de `src/app/core/`.

### VII. Simplicité

Implémenter ce que la spec demande, rien de plus. Pas d'abstraction spéculative.

## Workflow

Spec (`/speckit-specify`) → plan (`/speckit-plan`) → tâches (`/speckit-tasks`) → implémentation
(`/speckit-implement`). Chaque étape est relue par un humain avant de passer à la suivante.

## Gouvernance

Cette constitution prévaut sur les habitudes individuelles. Toute dérogation est justifiée dans le plan
(section « Complexity Tracking »).

**Version** : 1.0.0 | **Ratifiée** : 2026-10-21 | **Dernière modification** : 2026-10-21
