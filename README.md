# Congés

Application interne de gestion des demandes de congés.

- **Back-end** : .NET 8 Web API, EF Core + SQLite, FluentValidation, xUnit.
- **Front-end** : Angular 20 (standalone components, signals).
- **Outillage** : Docker, Claude Code, GitHub Spec Kit.

## Démarrer

Prérequis et installation du poste : [SETUP.md](SETUP.md).

```bash
docker compose up -d
```

| Service | URL |
|---|---|
| Front | http://localhost:4200 |
| API | http://localhost:5080 |
| Swagger | http://localhost:5080/swagger |

La base SQLite est recréée avec des données de démonstration à chaque démarrage de l'API.

## Données de démonstration

| Id | Employé | Équipe | Rôle | Solde CP |
|---|---|---|---|---|
| 1 | Claire Martin | Direction | Manager de la Direction | 30 |
| 2 | Hugo Bernard | Produit | Manager de l'équipe Produit | 30 |
| 3 | Lea Petit | Produit | | 30 |
| 4 | Karim Benali | Produit | | 30 |
| 5 | Ines Moreau | Produit | | 3 |
| 6 | Sophie Laurent | Support | Manager de l'équipe Support | 30 |
| 7 | Thomas Roux | Support | | 30 |
| 8 | Yasmine Haddad | Support | | 25 |
| 9 | Nicolas Fournier | Support | | 30 |

Les équipes Produit et Support sont rattachées à la Direction.

## API existante

| Méthode | Route | Description |
|---|---|---|
| GET | `/api/employees` | Liste des employés |
| GET | `/api/employees/{id}` | Détail d'un employé (avec solde) |
| GET | `/api/me` | Employé courant (en-tête `X-User-Id`) |
| GET | `/api/teams` | Équipes |
| GET | `/api/leave-requests?employeeId=&teamId=&status=&from=&to=` | Demandes de congé |
| GET | `/api/leave-requests/{id}` | Détail d'une demande |

## Travail en cours

Voir [ENONCE.md](ENONCE.md).

## Contribuer

Fork + pull request, sans invitation : [CONTRIBUTING.md](CONTRIBUTING.md) (en anglais).
