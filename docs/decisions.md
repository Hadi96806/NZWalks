# Decisions

Architecture decisions for NZWalks, newest last. This log exists so that a choice made once
stays made — the reasoning is recorded here rather than re-derived from the code months later,
and a decision that turns out badly can be reversed deliberately instead of drifted away from.

Record a decision when it closes off an alternative someone could reasonably have picked.
Routine implementation choices don't belong here; if the answer was obvious, there was no
decision.

Companion documents: [00-OVERVIEW.md](./00-OVERVIEW.md) describes the architecture as it stands,
and [03-FINDINGS-AND-TECH-DEBT.md](./03-FINDINGS-AND-TECH-DEBT.md) lists known defects and debt.
This file is for choices; those are for state.

## Format

Each entry is numbered, dated, and carries a status:

- **Open** — the trade-off is framed and a direction is recommended, but nobody has committed.
- **Accepted** — decided and in force.
- **Superseded by ADR-NNNN** — replaced; kept because the reasoning still explains the code.

An entry states the context, the options, the decision, and the consequences — including the
ones that are inconvenient. An entry that lists only upsides is not finished.

## Index

| # | Decision | Status | Date |
|---|---|---|---|
| [0001](#adr-0001--validation-strategy) | Validation strategy: FluentValidation or data annotations | Open | 2026-09-30 |
| [0002](#adr-0002--solution-layout-for-testability) | Solution layout for testability | Open | 2026-09-30 |
| [0003](#adr-0003--handling-of-secrets-already-in-git-history) | Handling of secrets already in git history | Open | 2026-09-30 |

---

## ADR-0001 — Validation strategy

**Status:** Open · **Date:** 2026-09-30

### Context

Request validation currently lives as data annotations on the write DTOs (`AddRegionDto`,
`UpdateRegionDto`, `AddWalkDto`, `UpdateWalkDto`), enforced by the `[ValidateModel]` action
filter. The Week 1 hardening work introduces FluentValidation, which raises the question of
whether the two coexist.

Running both engines means two places to look when a 400 response is wrong, and two places to
change when a rule changes.

### Options

1. **Replace** the annotations with FluentValidation on the write DTOs.
2. **Coexist** — annotations for simple presence and length rules, FluentValidation for anything
   conditional or cross-field.
3. **Keep annotations** and skip FluentValidation entirely.

### Decision

_Not yet taken._ Recommended direction: **option 1, scoped to the four write DTOs.** Strip each
DTO's annotations as its validator lands, so there is exactly one place a validation rule can
live.

### Consequences

- Validation rules move out of the DTO and into a class beside it, which is a place to look that
  newcomers won't guess without this note.
- Rules become unit-testable without hosting — relevant once a test project exists in Week 3.
- Read DTOs and query parameters are unaffected and keep whatever validation they have.

---

## ADR-0002 — Solution layout for testability

**Status:** Open · **Date:** 2026-09-30

### Context

Everything compiles into a single assembly, `NZWalks.API`. Finding #7 in
[03-FINDINGS-AND-TECH-DEBT.md](./03-FINDINGS-AND-TECH-DEBT.md) notes that this hurts once
repository and domain logic needs unit-testing without pulling in ASP.NET Core hosting — which
is exactly what Week 3 of the roadmap asks for. No week schedules the split.

### Options

1. **Split now** — extract `Models/Domain`, `Data/` and `Respositries/` into a class library
   that `NZWalks.API` references.
2. **Test through the API assembly** — add a test project that references `NZWalks.API` directly.
3. **Defer** and decide when the pain is concrete.

### Decision

_Not yet taken._ Recommended direction: **option 2.** Testing through the API assembly works
fine at this size, and the split is a deliberate structural step that deserves its own slot
rather than becoming a detour inside a week already budgeted for writing tests.

### Consequences

- No compiler-enforced boundary stopping a controller from touching `NZWalksDbContext` directly;
  that stays a convention, enforced by review.
- Test projects reference the web assembly, so test startup carries the hosting stack.
- The split stays available later, and gets no harder for having waited — unlike the
  `Respositries` rename (finding #5), which does.

---

## ADR-0003 — Handling of secrets already in git history

**Status:** Open · **Date:** 2026-09-30

### Context

`appsettings.json` was committed in `13ca524` containing the SQL Server `sa` password and
`Jwt:Key` in plaintext. Both values remain retrievable from git history. Azure Key Vault is
scheduled for Week 5 of the roadmap, but Key Vault protects future secrets only — it does
nothing about values already published to the repository.

### Options

1. **Rotate and move to user-secrets**, accepting that the old values stay in history.
2. **Rewrite history** to purge the values, then rotate.
3. **Wait for Key Vault** in Week 5.

### Decision

_Not yet taken._ Recommended direction: **option 1.** Rotate both values now and move
configuration to user-secrets locally; treat the committed values as permanently compromised.
Option 3 is not viable — it leaves live credentials exposed for five more weeks.

### Consequences

- The old `sa` password and signing key must be considered public; any environment still using
  them needs changing, not just this repo.
- History keeps the old values. Anyone cloning the repo can read them, so the rotation has to be
  real rather than cosmetic.
- A history rewrite stays possible later, but it rewrites every commit hash and needs
  coordinating with any clone — worth its own decision rather than being folded into this one.
