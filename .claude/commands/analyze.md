# /analyze — Static analysis and type checking

Runs all static analysis checks without running tests.

## What this runs

```bash
# Backend — build check
cd backend && dotnet build --configuration Release

# Backend — find vulnerable packages
cd backend && dotnet list package --vulnerable --include-transitive

# Frontend — TypeScript strict check
cd frontend && npx tsc --noEmit

# Frontend — production build check
cd frontend && npm run build
```

## Use this to

- Quickly verify a change compiles before running the full test suite
- Check for NuGet vulnerabilities before opening a PR
- Confirm TypeScript types are correct after editing `src/api/types.ts`
