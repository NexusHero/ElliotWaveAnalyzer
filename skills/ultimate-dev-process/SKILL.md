---
name: ultimate-dev-process
description: Vendor-neutral, stack-agnostic development governance that unifies architecture governance, TDD/testing discipline, implementation style, and commit/PR workflow into one Definition of Done for architects, engineers, and QA.
---
# Ultimate Development Process

Merged from two production processes (ElliotWaveAnalyzer, Résumé/myJob) and hardened with the
gaps neither covered on its own. One process, three roles, one Definition of Done — so an
architect, an engineer, and a QA/test engineer can sit down together and know exactly which of
them owns which gate, and where the seams between their work are.

Applies to humans and AI coding agents alike. An agent follows every section below automatically,
without being asked — the same way a senior engineer would not need reminding to write a test.

---

## Roles

| Role | Owns |
|------|------|
| **Software Architect** | Requirements Register, ADRs, Quality Goals, the Tech Radar, architecture conformance in review |
| **Software Engineer** | Implementation style (SOLID), TDD, the code itself, its unit/pure-logic tests |
| **QA / Test Engineer** | Test strategy shape (the pyramid), test *quality* (not just the coverage %), acceptance/e2e tests, the security checklist |
| **AI coding agent** | All of the above, applied on every change, without being asked |

No role is a gate-keeper of last resort — each gate below is owned by whoever is closest to it,
and checked by the *other* two roles in review (see **Reviewer Protocol**).

---

## 0. Definition of Ready — before work starts

A work item is ready to pick up when it has:

- a stable id (`REQ-NNN` or equivalent) and a one-paragraph acceptance criterion,
- the quality attribute(s) it affects named (correctness / security / performance / …), and
- a rough test approach (what kind of test will prove it's done — not the test code itself).

If it crosses a trust boundary (auth, money, PII, a new external integration) it is flagged for a
security pass **before** implementation starts, not discovered in review.

Work without an id is fine for spikes/experiments; it does not enter the Requirements Register and
does not get an ADR — but it also does not merge to the main line as a delivered feature.

---

## 1. Architecture Governance

Architecture documentation is part of the change, not a follow-up. A PR that changes architecture
without the matching documentation is **not done** — exactly like a PR with a failing test.

### 1.1 Requirements Register

Every feature starts from a requirement with a stable id (`REQ-NNN`), tracked in a living register
(`docs/architecture.md` §1 or equivalent). A ticket/issue is where the requirement is *discussed*;
the register is where it is *tracked*. Each row carries: id, short statement, the issue/PR that
delivers it, status (`Proposed` → `In Progress` → `Fulfilled`).

### 1.2 ADRs — one decision, one file, never edited after acceptance

Any of the following **requires an ADR**, in the **same PR**:

- adding, removing, or swapping a technology/library/external service,
- introducing or changing a cross-layer boundary, an abstraction, or a major algorithm,
- any decision a future maintainer would ask *"why was it done this way?"* about.

Routine dependency bumps and plain bug fixes don't need one.

Format is lightweight [MADR](https://adr.github.io/madr/): **Context → Decision → Consequences**
(+ **Alternatives considered** when the choice was close), plus a status. Numbering is sequential
and immutable. A decision that replaces another **supersedes** it — mark the old one
`Superseded by ADR-NNN` — never rewrite history.

### 1.3 Sequence diagrams for fulfilled requirements

When a requirement is fulfilled, add or update a sequence diagram (Mermaid or PlantUML — pick one
per project and stay consistent) in the Runtime View showing the actual call flow across the real
building blocks. It documents reality: it must match the code shipping in the same PR. Link it to
its `REQ-NNN`.

### 1.4 Tech Radar — so the ADR log doesn't become the only way to see the shape of the stack

Once a project accumulates a couple dozen ADRs, "what's our stance on X" stops being answerable by
skimming. Keep a short **Tech Radar** table (Adopt / Trial / Assess / Hold) next to the ADR index —
one line per technology, linking back to the ADR that decided it. This is a index, not a process:
it costs one line per ADR and pays for itself the first time someone asks "can I use gRPC here."

### 1.5 Docs staleness is a CI gate, not a review nitpick

Manual review catches stale docs unreliably. Add a deterministic, dependency-free check (no LLM,
no network) to CI that fails the build on structural drift:

- a diagram source (`.puml`/`.mmd`) with no rendered output, or an orphaned render with no source,
- a relative Markdown link that points at a file that doesn't exist,
- an `ADR-NNNN` reference with no matching ADR file.

This is cheap, deterministic, and catches the exact rot manual review misses under deadline
pressure.

---

## 2. Implementation Style

### 2.1 SOLID is a quality gate, not an aspiration

Reviewed on every PR, weighted the same as the tests. The two violations to watch hardest:

- **No god classes (SRP).** If a class fetches *and* calculates *and* orchestrates *and*
  persists, split it. If you cannot state its single reason to change in one sentence, it's wrong.
  Business logic lives in small, pure, dependency-free classes; orchestration/glue stays thin.
- **Depend on interfaces, not concretes, across a boundary (DIP).** Consumers depend on
  abstractions; infrastructure implementations (a specific SDK, a specific DB driver) never leak
  into domain/application code. New behavior is a new implementation + one wiring line — existing
  classes are not modified (OCP).

A PR that introduces a god class, or a concrete dependency where an interface should stand, is
**not done** — same as a PR with a failing test.

### 2.2 Isolate volatile third-party surface

Any SDK/library that changes fast, or that you might swap later (an LLM SDK, a payment provider, a
charting library) is wrapped behind one narrow interface, with its types confined to a single
adapter file. Nothing upstream of that file ever imports the vendor's types.

### 2.3 Config over hardcoding

API keys, model/version names, endpoints, feature toggles — all configuration, never literals in
source. A vendor deprecating a model version or rotating an endpoint should be a config change, not
a deploy.

### 2.4 Language-appropriate strictness, always on

Whatever the stack's strict mode is (nullable reference types, TypeScript `strict`, a linter's
strictest preset) — it's on project-wide, with no blanket suppressions. A suppression needs a
comment explaining why, not just `// eslint-disable`.

---

## 3. Testing Behavior

### 3.1 TDD: Red → Green → Refactor

Tests are written **before** the implementation. New behavior arrives with the test that specifies
it, in the same commit/PR — never code first, tests bolted on after (or never).

### 3.2 Naming and structure

```
Subject_StateUnderTest_ExpectedBehaviour
```

Examples: `CalculateRsi_AllGains_RsiApproachesOneHundred`,
`ValidateAsync_InvalidLabel_ThrowsArgumentException`.

Bodies follow **Arrange–Act–Assert** with a blank line between each phase. Never abbreviate a shown
test with `// ...` — show the whole thing.

### 3.3 The pyramid, and what each layer is for

| Level | What it tests | Rule |
|-------|---------------|------|
| **Pure-logic / unit** | Deterministic cores — rule checkers, calculators, parsers, evaluators | No mocks needed — exhaustive fixtures, no I/O |
| **Service / orchestration** | Delegation, provider selection, input validation | Mock the ports (interfaces), not the concretes |
| **Acceptance / integration** | The real API against a real datastore | Only genuinely external systems (LLM, third-party API) are faked |
| **Component / e2e** | UI against the real DOM/browser | Cover the golden path + empty/loading/error states |

**Never call a real external system (network, LLM, paid API) from a unit test.** Use seeded,
deterministic fixtures — one shared fixture module, not ad-hoc arrays scattered across test files.

### 3.4 Coverage is architectural, not brute-forced

Target **≥ 90% line coverage on core/business logic**, enforced as a CI-blocking gate — not an
aspiration in a doc nobody reads. The way to hit it: keep business logic in pure, dependency-free
classes so it is trivially and exhaustively testable; orchestration/glue stays thin and needs fewer
tests. Shallow tests that chase the percentage without exercising real behavior don't count, and a
reviewer should say so.

---

## 4. Security & NFR Gate

Keep a short, concrete checklist (adapt the specifics to the stack, keep the shape):

- **AuthN/AuthZ** — every business endpoint requires auth explicitly; public endpoints are
  explicitly marked public, not accidentally open.
- **Rate limiting** — every endpoint has an explicit policy; tighter limits on anything that calls
  a paid/LLM API or handles login.
- **Input validation** — every write endpoint validates; free-form strings that reach an external
  system go through an allowlist; numeric ranges are explicitly bounded.
- **CORS** — no wildcard origins; explicit allowlist.
- **Secrets** — never in source or committed config; read from environment/secret manager; local
  override files are gitignored.
- **Headers** — security headers and HSTS on in non-dev environments; a CSP that names only known
  external domains.
- **Logging** — no PII/secrets in log lines; security-relevant events (auth failures, rate-limit
  hits) are logged at a level that alerts.

**Lightweight threat-model trigger:** any change that adds a new external integration or a new
trust boundary gets a security-checklist pass *before* merge — not as an afterthought once
something breaks in production.

---

## 5. Commit & Branching

Conventional Commits, in English:

```
type(scope): short summary in imperative mood
```

Types: `feat` · `fix` · `docs` · `test` · `refactor` · `chore` · `ci` · `build` · `perf` ·
`style` · `revert`.

Branches: `feat/`, `fix/`, `docs/`, `chore/`, `refactor/`, `test/`, `ci/` + short description.

**Automate the enforcement, don't rely on memory.** Install local git hooks:

- `pre-commit` runs the full local gate (format + lint + tests) — the same thing CI runs.
- `commit-msg` rejects a commit whose subject doesn't match Conventional Commits.

Provide **one script** that *is* the local gate and *is* what CI runs — `./test.sh` or equivalent —
so "did you run the checks" is never a review question.

---

## 6. Pull Request Workflow

- **`main` is protected. Merge only via PR — no direct pushes.**
- **One logical change per PR.** Larger changes get an issue first to discuss the approach.
- **Every task has a tracked issue; every PR links it** (`Closes #123`). This is what keeps the
  history traceable after the fact — a PR with no linked issue is missing context a reviewer has
  no other way to recover.
- All CI checks green, all review threads resolved, branch up to date with base — all three,
  before merge.

### Reviewer Protocol — the gap most process docs leave implicit

The PR template checklist is the *author's* self-check. The reviewer's job is a distinct pass, not
a re-read of the same checklist:

1. **Architecture conformance** — does this match the ADRs and the Requirements Register, or does
   it quietly introduce a new pattern that needs its own ADR?
2. **Test quality, not test quantity** — do the tests assert behavior, or just execute lines? Would
   they fail if the logic were subtly wrong? A green coverage number with weak assertions is a
   finding, not a pass.
3. **Security checklist** — applied by the reviewer independently, not trusted from the author's
   checkbox, for anything touching a trust boundary.
4. **Docs currency** — does the changed behavior show up in the docs a future reader would consult?

---

## 7. Definition of Done — the master checklist

A change is done, and its PR mergeable, only when **all** hold:

- [ ] Build succeeds; strict/lint checks pass with zero suppressions added without justification
- [ ] All tests green, none skipped; new behavior has a test written before the implementation
- [ ] Line coverage on core logic stays **≥ 90%**, achieved architecturally, not by shallow tests
- [ ] **SOLID holds**: no god classes, dependencies through interfaces across boundaries
- [ ] Every new endpoint/entry point is exercised by a test **and** documented (OpenAPI/equivalent)
- [ ] No secrets, API keys, or PII committed
- [ ] Security checklist applied if the change crosses a trust boundary
- [ ] **Architecture Governance**, if architecturally relevant: ADR added/updated · Requirements
      Register updated · sequence diagram added/updated for a fulfilled requirement · affected
      docs sections corrected — all in the same PR
- [ ] Commit messages are Conventional Commits; branch follows the naming convention
- [ ] PR links its issue; one logical change; all CI green; review threads resolved
- [ ] `git status` clean — no file the change depends on is left untracked

---

## 8. Observability & Rollback

For anything architecturally relevant, answer *before* merging: **how will we know, in production,
if this breaks?** — a log line, a metric, or an alert. Not every PR needs new dashboards; every PR
that changes a cross-cutting concern or a user-facing critical path needs an answer to this
question, not silence.

- Structured logging for anything architecturally significant; never log PII or secrets.
- Prefer changes that degrade gracefully (a failed optional enrichment shows a clear "unavailable"
  state) over ones that take the whole request down.
- If a change is risky enough to want a fast rollback path, say so in the PR description — that's
  a fact for the reviewer and the on-call, not an implementation detail to bury in the diff.

---

## 9. AI Agent Guidance

This skill is vendor-neutral and stack-agnostic on purpose. A project should pair it with:

- a **stack appendix** (below) filling in the actual commands, and
- an optional **vendor-specific overlay** (e.g. a `<project>-claude` skill) for model defaults,
  thinking-effort settings, and terse-style preferences — the same split ElliotWaveAnalyzer uses
  between its vendor-neutral `elliottwave-agents` skill and the `elliottwave-claude` overlay.

An agent applies Sections 0–8 automatically, on every change, without being asked — the same way it
would not need reminding to write a test.

---

## Appendix: Stack Adaptation (this repo)

| Gate | Command |
|------|---------|
| Build (backend) | `cd backend && dotnet build --configuration Release` |
| Build (frontend) | `cd frontend && npm run build` |
| Lint / format check | `.editorconfig` enforced by the IDE; `npx tsc --noEmit` for type-level checks |
| Unit / pure-logic tests | `cd backend && dotnet test` |
| Frontend tests | `cd frontend && npm test` |
| Integration / acceptance tests | Included in `dotnet test` (Testcontainers-backed) |
| Coverage report | `dotnet test --collect:"XPlat Code Coverage"` · `npm run test:coverage` |
| Type check | `cd frontend && npx tsc --noEmit` |

See `skills/elliottwave-agents/SKILL.md` for how these gates are already wired into this repo's CI
and `.claude/commands/` (`/test`, `/analyze`, `/push`, `/security-check`).

## Recommended Upgrades for This Repo

This repo already implements most of Sections 1–8 in depth (Requirements Register, per-requirement
sequence diagrams, ADRs embedded in `docs/architecture.md`, ≥90% coverage gate, SOLID as a hard
gate, TDD). Two gaps worth closing, ported from the sibling process (Résumé/myJob):

1. **Issue-linking discipline (§6).** The PR template doesn't yet require `Closes #NNN`. Add it —
   it's what makes the Requirements Register cross-checkable against GitHub history after the fact.
2. **Local git hooks (§5).** Quality gates are documented and run in CI, but nothing stops an
   uncommitted local check from being skipped before a push. Add a `pre-commit` hook running the
   fast subset (`dotnet build` + `tsc --noEmit`) and a `commit-msg` hook enforcing Conventional
   Commits, mirroring `scripts/hooks/` in the Résumé/myJob repo.
