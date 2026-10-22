# Contributing: fork and pull request

This repository is public and read-only for participants. **No invitation is needed.** You work in your own fork
and hand in your work through a pull request to `main`. A GitHub account is all you need.

## 1. Fork and clone

**With the GitHub CLI** (recommended):

```bash
gh auth login                                                   # once
gh repo fork abelmabrouk/ai4pdlc-exercice --clone --remote      # fork + clone; origin = your fork, upstream = this repo
cd ai4pdlc-exercice
git checkout -b <your-first-name>
```

**Without the GitHub CLI:**

1. Open https://github.com/abelmabrouk/ai4pdlc-exercice and click **Fork**.
2. Clone your fork:

```bash
git clone https://github.com/<your-account>/ai4pdlc-exercice.git
cd ai4pdlc-exercice
git checkout -b <your-first-name>
```

To push over HTTPS, use `gh auth login` or a GitHub personal access token. GitHub rejects account passwords.

### Already cloned this repository without forking?

Nothing is lost: your commits are local. Commit your work, fork, point `origin` to your fork, and push.

```bash
git add -A && git commit -m "Part 1"         # if you have uncommitted changes
git checkout -b <your-first-name>            # if you are still on main

# With the GitHub CLI: creates the fork, renames the current origin to upstream, adds your fork as origin
gh repo fork --remote

# Or without it, after clicking Fork on GitHub:
git remote rename origin upstream
git remote add origin https://github.com/<your-account>/ai4pdlc-exercice.git

git push -u origin HEAD                      # then open your pull request (section 2)
```

## 2. Open your pull request (end of part 1)

```bash
git add -A && git commit -m "Part 1" && git push -u origin HEAD
gh pr create --repo abelmabrouk/ai4pdlc-exercice --base main --title "<your-first-name>" --body "SDD workshop"
```

Without the CLI, use the "Create a pull request" link printed by `git push`, or the **Contribute → Open pull
request** button on your fork.

## 3. Keep it up to date (part 2 and beyond)

Just push: the pull request updates itself.

```bash
git add -A && git commit -m "Part 2" && git push
```

If Spec Kit switched you to a new branch for part 2, push that branch and open a new pull request from it. The
most recent pull request of each participant is the one that counts.

## Rules

- **Never merge.** Pull requests exist only to hand your work to the facilitator; they are never merged.
- **One person, one fork.** Work in your own fork, never in someone else's.
- **Public by design.** Your pull request is visible to everyone, like any contribution to a public repository.

## Troubleshooting

| Symptom | Fix |
|---|---|
| `git push`: 403 on `abelmabrouk/ai4pdlc-exercice` | You are pushing to the workshop repository instead of your fork. Check with `git remote -v`: `origin` must point to `github.com/<your-account>/ai4pdlc-exercice`. Fix it with `git remote set-url origin https://github.com/<your-account>/ai4pdlc-exercice.git`. |
| `git push`: 403 "denied to <other-account>" | Git is using another GitHub account's credentials. Run `gh auth login` then `gh auth setup-git`. |
| `gh pr create` lists the wrong base repository | Pass `--repo abelmabrouk/ai4pdlc-exercice --base main` explicitly. |
