# Installation du poste

Comptez une quinzaine de minutes, à faire **avant** l'atelier.

## 1. Prérequis

| Outil | Version | Vérification |
|---|---|---|
| Git | 2.40+ | `git --version` |
| Compte GitHub | — | — |
| GitHub CLI (recommandé) | 2.40+ | `gh --version` |
| Docker Desktop (ou Docker Engine + Compose v2) | Compose 2.20+ | `docker compose version` |
| Node.js (uniquement pour installer Claude Code) | 18+ | `node --version` |
| Claude Code | dernière version | `claude --version` |
| uv (pour installer Spec Kit) | 0.5+ | `uv --version` |
| Python (installé par uv si absent) | 3.11+ | — |

Systèmes supportés : macOS, Linux ou Windows **avec WSL2**. Sous Windows, clonez le repo et lancez toutes les
commandes depuis le terminal WSL, pas depuis PowerShell.

Aucun SDK .NET ni Angular n'est nécessaire : la compilation, les tests et les serveurs de développement tournent
dans Docker.

## 2. Claude Code

```bash
npm install -g @anthropic-ai/claude-code
claude          # au premier lancement : connexion à votre compte
```

## 3. Superpowers (plugin Claude Code)

Superpowers ajoute à Claude Code des skills de méthode. Pendant l'atelier, vous utiliserez ceux de
l'implémentation : TDD, débogage systématique et vérification avant de déclarer une tâche terminée. Il est publié
sur le marketplace officiel d'Anthropic :

```bash
claude plugin install superpowers@claude-plugins-official
```

Vous pouvez aussi l'installer depuis une session `claude`, avec `/plugin install superpowers@claude-plugins-official`.

Si le marketplace officiel n'est pas disponible sur votre poste, passez par le marketplace de l'auteur :

```text
/plugin marketplace add obra/superpowers-marketplace
/plugin install superpowers@superpowers-marketplace
```

Redémarrez ensuite Claude Code, puis vérifiez l'installation :

```text
/plugin            # superpowers doit apparaître dans la liste des plugins installés
```

## 4. Récupérer le projet : fork du dépôt

> English version of the fork and pull request process: [CONTRIBUTING.md](CONTRIBUTING.md).

Le dépôt de l'atelier est public : https://github.com/abelmabrouk/ai4pdlc-exercice. Vous n'avez pas besoin
d'invitation. Vous travaillez dans **votre fork**, et vous rendez votre travail par une **pull request**.
Un compte GitHub suffit.

**Avec le CLI GitHub** (recommandé) :

```bash
gh auth login                                                   # une seule fois
gh repo fork abelmabrouk/ai4pdlc-exercice --clone --remote      # fork + clone ; origin = votre fork
cd ai4pdlc-exercice
git checkout -b <votre-prenom>
```

**Sans le CLI GitHub :**

1. Ouvrez https://github.com/abelmabrouk/ai4pdlc-exercice et cliquez sur **Fork**.
2. Clonez votre fork :

```bash
git clone https://github.com/<votre-compte>/ai4pdlc-exercice.git
cd ai4pdlc-exercice
git checkout -b <votre-prenom>
```

Pour pousser sur votre fork en HTTPS, utilisez `gh auth login` ou un jeton personnel GitHub : le mot de passe du
compte est refusé par GitHub.

## 5. Démarrer la stack

```bash
docker compose pull                 # télécharge les images .NET 8 et Node 22 (~1,5 Go, à faire avant l'atelier)
docker compose up -d
docker compose ps                   # api doit être "healthy"
```

Vérifiez ensuite que tout répond :

- http://localhost:5080/swagger affiche l'API ;
- http://localhost:4200 affiche l'écran « Mes demandes » ;
- `docker compose run --rm backend-tests` passe au vert.

Le premier démarrage du front prend 1 à 2 minutes, le temps de `npm install`.

Si les ports 5080 ou 4200 sont déjà occupés, changez-les :

```bash
API_PORT=5081 WEB_PORT=4201 docker compose up -d
```

## 6. Spec Kit

[GitHub Spec Kit](https://github.com/github/spec-kit) outille le cycle spec → plan → tâches → implémentation.
Il s'installe avec `uv`.

```bash
# 1. uv (si absent) — https://docs.astral.sh/uv/
curl -LsSf https://astral.sh/uv/install.sh | sh

# 2. CLI Spec Kit
uv tool install specify-cli --from git+https://github.com/github/spec-kit.git
specify --help

# 3. Initialisation dans le repo, pour Claude Code et avec les scripts bash (macOS, Linux, WSL)
specify init --here --force --integration claude --script sh

# 4. Constitution de l'équipe : remplace le modèle vide créé par l'initialisation
cp constitution.md .specify/memory/constitution.md
```

Commitez le résultat de l'initialisation sur votre branche :

```bash
git add -A && git commit -m "Initialisation Spec Kit"
```

Redémarrez ensuite `claude` à la racine du projet. Les commandes suivantes sont alors disponibles :

| Commande | Rôle |
|---|---|
| `/speckit-specify` | Rédiger la spécification fonctionnelle |
| `/speckit-clarify` | Lever les ambiguïtés (optionnel) |
| `/speckit-plan` | Produire le plan technique |
| `/speckit-tasks` | Découper en tâches |
| `/speckit-implement` | Implémenter les tâches |

Les specs sont écrites dans `specs/<numéro>-<nom>/`.

Si une commande ouvre une nouvelle branche git, c'est normal : travaillez sur cette branche et poussez-la (voir
ci-dessous).

## 7. Pendant l'atelier : rendre son travail

À la fin de la partie 1, poussez votre branche sur votre fork et ouvrez une pull request vers le dépôt de
l'atelier :

```bash
git add -A && git commit -m "Partie 1" && git push -u origin HEAD
gh pr create --repo abelmabrouk/ai4pdlc-exercice --base main --title "<votre-prénom>" --body "Atelier SDD"
```

Sans le CLI GitHub, utilisez le lien « Create a pull request » affiché par `git push`, ou le bouton
**Contribute → Open pull request** de votre fork.

Ensuite, il suffit de pousser : la pull request se met à jour toute seule.

```bash
git add -A && git commit -m "Partie 2" && git push
```

Si Spec Kit vous a fait changer de branche pour la partie 2, poussez cette nouvelle branche et ouvrez une
nouvelle pull request depuis celle-ci. Pour chaque participant, c'est la pull request la plus récente qui est
évaluée.

La pull request ne sera jamais fusionnée : elle sert uniquement à transmettre votre travail au facilitateur.

## Dépannage

| Symptôme | Solution |
|---|---|
| `api` reste « starting » | `docker compose logs api` : une erreur de compilation empêche le démarrage |
| Le front ne voit pas les modifications | `docker compose restart web` |
| Base dans un état incohérent | `docker compose restart api` (la base est recréée) |
| `npm install` échoue derrière un proxy | Définissez `HTTP_PROXY` et `HTTPS_PROXY` dans le service `web` de `docker-compose.yml` |
| `git push` : 403 sur `abelmabrouk/ai4pdlc-exercice` | Vous poussez vers le dépôt de l'atelier au lieu de votre fork. Vérifiez avec `git remote -v` : `origin` doit pointer vers `github.com/<votre-compte>/ai4pdlc-exercice`. Corrigez avec `git remote set-url origin https://github.com/<votre-compte>/ai4pdlc-exercice.git`. |
| Projet cloné directement, sans fork, et travail déjà commencé | Rien n'est perdu. Commitez, puis lancez `gh repo fork --remote` (ou forkez sur GitHub, puis `git remote rename origin upstream` et `git remote add origin https://github.com/<votre-compte>/ai4pdlc-exercice.git`). Poussez ensuite avec `git push -u origin HEAD` et ouvrez la PR. |
| `git push` : 403 « denied to <autre-compte> » | Git utilise les identifiants d'un autre compte GitHub. Relancez `gh auth login` puis `gh auth setup-git`. |
