# /push — Commit and push current changes

Commits staged changes with a conventional commit message and pushes to origin/main.

## Before pushing, always verify

```bash
# 1. Tests pass
cd backend && dotnet test
cd ../frontend && npm test

# 2. Build is clean
cd frontend && npm run build

# 3. No secrets accidentally staged
git diff --staged | grep -i "api.key\|apikey\|secret\|password" && echo "WARNING: possible secret!"

# 4. Status is clean (no untracked files that are needed)
git status
```

## Commit format

```
type(scope): short summary in imperative mood

- bullet describing what changed and why
- another bullet if needed
```

Valid types: feat · fix · test · refactor · docs · chore · perf · ci · build
Scopes: backend · frontend · docs · ci · deps · gemini · indicators

## Push

```bash
git push origin main
```

PRs via GitHub — never commit straight to main.
