<!--
Thanks for contributing to Elliott Wave Analyzer!
Please fill in the sections below. Keep the PR focused — one logical change per PR.
-->

## Summary

<!-- What does this PR do, and why? Link the motivating issue. -->

Closes #

## Type of change

<!-- Mark all that apply with an "x". Type should match your Conventional Commit prefix. -->

- [ ] `feat` — new feature
- [ ] `fix` — bug fix
- [ ] `docs` — documentation only
- [ ] `refactor` — code change that neither fixes a bug nor adds a feature
- [ ] `test` — adding or correcting tests
- [ ] `chore` / `ci` — tooling, dependencies, or pipeline

## How was this tested?

<!-- Commands you ran, scenarios you covered, and anything reviewers should reproduce. -->

## Checklist

- [ ] `dotnet test` passes locally (no skipped tests)
- [ ] `npm test` passes locally
- [ ] `npm run build` succeeds (`tsc --noEmit` + `vite build`)
- [ ] Commit messages follow [Conventional Commits](https://www.conventionalcommits.org)
- [ ] New tests use the `Subject_StateUnderTest_ExpectedBehaviour` naming convention
- [ ] **SOLID holds**: no god classes (SRP); new services depend on interfaces, not concrete types (DIP); interfaces stay narrow (ISP); extension over modification (OCP)
- [ ] **TDD**: tests were written before the implementation (Red → Green → Refactor)
- [ ] **New API endpoints** are exercised by a test **and** carry OpenAPI metadata (`WithSummary`/`WithDescription`/`Produces`/`ProducesProblem`), visible in the Scalar UI
- [ ] Line coverage stays ≥ 90% (business logic in pure, testable classes)
- [ ] No API keys or secrets are committed
- [ ] **Architecture Governance** (for architecturally-relevant changes): ADR added to `docs/architecture.md` §9 · Requirements Register (§1) updated · sequence diagram added/updated in the Runtime View (§6) for a fulfilled requirement · affected §5/§6/§8 prose corrected
- [ ] Any unrelated bug/defect found while working this task got its own issue — not silently fixed inline, not silently skipped
- [ ] I have read and agree to the [Code of Conduct](../CODE_OF_CONDUCT.md)

## Screenshots / notes

<!-- For UI changes, add before/after screenshots. Otherwise delete this section. -->
