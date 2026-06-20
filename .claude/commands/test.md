# /test — Run the full test suite

Runs all backend (NUnit) and frontend (Vitest) tests.

## Usage

```
/test              # all tests
/test --backend    # backend only
/test --frontend   # frontend only
/test --coverage   # backend with code coverage report
/test --watch      # frontend in watch mode
```

## What this runs

```bash
# All tests (default)
cd backend && dotnet test --logger "console;verbosity=detailed"
cd ../frontend && npm test

# Backend only (--backend)
cd backend && dotnet test --logger "console;verbosity=detailed"

# Frontend only (--frontend)
cd frontend && npm test -- --reporter=verbose

# Coverage (--coverage)
cd backend && dotnet test --collect:"XPlat Code Coverage"
cd ../frontend && npm run test:coverage

# Frontend watch (--watch)
cd frontend && npm run test:watch
```

## Quality gates before a PR

- All NUnit tests green, none skipped
- All Vitest tests green
- `tsc --noEmit` reports no errors
- `npm run build` succeeds
