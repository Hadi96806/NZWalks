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
| [0001](#adr-0001--validation-strategy) | Validation strategy: FluentValidation or data annotations | Accepted | 2026-09-30 |
| [0002](#adr-0002--solution-layout-for-testability) | Solution layout for testability | Accepted | 2026-10-04 |
| [0003](#adr-0003--handling-of-secrets-already-in-git-history) | Handling of secrets already in git history | Accepted | 2026-09-30 |
| [0004](#adr-0004--roles-are-granted-only-by-an-admin) | Roles are granted only by an Admin | Accepted | 2026-10-08 |
| [0005](#adr-0005--token-lifetime-and-clock-skew) | Token lifetime and clock skew | Accepted | 2026-10-08 |
| [0006](#adr-0006--rate-limiting-and-account-lockout) | Rate limiting and account lockout | Accepted | 2026-10-13 |
| [0007](#adr-0007--access-policy-for-walks-and-images) | Access policy for Walks and Images | Accepted | 2026-10-13 |
| [0008](#adr-0008--week-2-hooks-what-runs-when-and-what-fails) | Week 2 hooks: what runs when, and what fails | Accepted | 2026-10-15 |

---

## ADR-0001 — Validation strategy

**Status:** Accepted · **Date:** 2026-09-30 · **Accepted:** 2026-10-11

### Context

Request validation lived as data annotations on the write DTOs, enforced by `[ApiController]`
(the `[ValidateModel]` filter never ran — `ModelStateInvalidFilter` at order −2000 always
short-circuited first). Some inputs had no validation at all: `UpdateWalkDto`, the Walks GetAll
query string (`pageSize=0` / `pageNumber=0` returned 500), and the image upload, whose checks were
hand-written in the controller. The Week 1 hardening work introduces FluentValidation, which raised
the question of whether the two engines coexist.

### Options

1. **Replace** the annotations with FluentValidation.
2. **Coexist** — annotations for simple presence and length rules, FluentValidation for anything
   conditional or cross-field.
3. **Keep annotations** and skip FluentValidation entirely.

### Decision

**Option 1, for every request input**, not only the four write DTOs: Region and Walk add/update,
a new `WalkQueryDto` for the Walks query string, `ImageUploadDto`, and the three auth DTOs. Every
`AbstractValidator<T>` lives in `Validators/` and is registered with
`AddValidatorsFromAssemblyContaining<Program>()` + `AddFluentValidationAutoValidation()`.

Supporting choices:
- `SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true` — FluentValidation alone
  owns "required", so a missing field reports one message, not two.
- `DefaultRuleLevelCascadeMode = CascadeMode.Stop` — a field reports its first failing rule only.
- Strict 400s, no silent clamping (e.g. `pageSize` 1–100).
- Identity keeps password complexity; validators only cap the length (128). Login has no format
  rules so its errors stay generic. Foreign-key existence stays in the repository (validators are
  synchronous under auto-validation).
- **One 400 contract:** every 400 with field errors is `ValidationProblemDetails`
  (`errors: { "<Field>": [...] }`), whether it comes from auto-validation, a hand-called
  `ValidateAndThrow` (`ValidationException`), or `InvalidReferenceException` — the last two are
  mapped in `ExceptionHandlerMiddleware`.

Auto-validation is no longer recommended by the FluentValidation authors for new projects because
it can't run async rules. Accepted here because every rule is synchronous; if a database-backed
rule is ever needed, switch to a global action filter that calls `ValidateAsync`.

### Consequences

- Validation rules live in `Validators/`, not on the DTO — a place newcomers won't guess without
  this note. A new request DTO needs a validator, or it is accepted unchecked.
- Stricter than before: PUT Walk, lowercase Region codes, Add Region names under 3 characters,
  non-http image URLs and `pageSize > 100` now return 400. `.JPG` uploads are now accepted.
- Query string names changed to `filterOn` / `filterQuery` (were `filtertOn` / `filrQuery`).
- `ValidateModelAttribute` was deleted; `UsersController.InvalidRole` was removed.
- Rules become unit-testable without hosting — relevant once a test project exists in Week 3.

---

## ADR-0002 — Solution layout for testability

**Status:** Accepted · **Date:** 2026-10-04

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

**Option 2 — test through the API assembly.** Testing this way works fine at this size, and the
split is a deliberate structural step that deserves its own slot rather than becoming a detour
inside a week already budgeted for writing tests.

Option 3 was rejected as a non-answer: deferring the decision is what leaves Week 3 without one.

### Consequences

- No compiler-enforced boundary stopping a controller from touching `NZWalksDbContext` directly;
  that stays a convention, enforced by review.
- Test projects reference the web assembly, so test startup carries the hosting stack.
- The split stays available later, and gets no harder for having waited — unlike the
  `Respositries` rename (finding #5), which does.
- Acting on this needs one line in `Program.cs`: `public partial class Program { }`. Top-level
  statements generate an `internal` `Program`, which a test assembly cannot name as the generic
  argument to `WebApplicationFactory<Program>`. Without it the decision is unimplementable, and
  Week 3 opens with a compiler error whose cause is not obvious. Tracked as Week 1 subtask B5s2.

---

## ADR-0003 — Handling of secrets already in git history

**Status:** Accepted · **Date:** 2026-09-30 (accepted 2026-10-05)

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

**Option 1 — rotate and move to user-secrets.** Applied 2026-10-05: connection strings and
`Jwt:Key` now live in user-secrets (`UserSecretsId` in the csproj); `appsettings.json` keeps empty
placeholders so the config contract stays readable, and `Program.cs` fails at startup with the
fix-it command when any of the three is null or empty. The treatment of the old values:

- **`Jwt:Key` — rotated, old value dead.** Tokens signed with the committed key are rejected.
- **App DB credential — rotated for NZWalks.** The app connects as a dedicated `nzwalks_app` login
  with `db_owner` on `NZWalksDb` and `NZWalksAuthDb` only.
- **`sa` password — NOT yet rotated.** Other projects on the same SQL Server instance still
  connect as `sa`, so changing it would break them. Until it is changed, the committed `sa`
  password is a live credential. Closing this is outstanding, not optional; update this entry when
  it is done.

Option 3 is not viable — it leaves live credentials exposed for five more weeks.

### Consequences

- The old signing key is public and dead. The old `sa` password is public and **still live** until
  rotated on the instance; any environment still using it needs changing, not just this repo.
- A fresh clone has no secrets and will not start until they are set; the startup guard names
  each missing key. user-secrets only load in `Development`, so other environments must supply
  the values another way (Key Vault in Week 5).
- `db_owner` is wider than runtime needs but is what `dotnet ef database update` requires.
- History keeps the old values. Anyone cloning the repo can read them, so the rotation has to be
  real rather than cosmetic.
- A history rewrite stays possible later, but it rewrites every commit hash and needs
  coordinating with any clone — worth its own decision rather than being folded into this one.

---

## ADR-0004 — Roles are granted only by an Admin

**Status:** Accepted · **Date:** 2026-10-08

### Context

`POST /api/Auth/Register` is anonymous and used to take a `Roles` array from the request body,
assigning `Roles[0]`. Anyone could register as Admin, log in, and pass every
`[Authorize(Roles=…)]` check (finding #9, which understated this). The question is who is allowed
to decide a user's role.

### Options

1. **Keep client-supplied roles** and validate them against a list — still lets the caller pick.
2. **Register assigns the lowest role; an Admin grants more** through an authenticated endpoint.
3. **Seed one Admin at startup** from configuration, and grant the rest from there.

### Decision

**Option 2.** Register always assigns Reader and rejects a request that still sends `roles`
(`[JsonUnmappedMemberHandling(Disallow)]`). `UsersController` is guarded at class level by
`[Authorize(Roles = Admin)]` and exposes `GrantRole` and `RevokeRole`; both are idempotent,
validate the role against `RoleNames.All`, return 404 for an unknown user, and write an audit line
(who changed whose role). `RevokeRole` refuses to remove a user's last role (Login rejects a user
with none) and the last Admin, both with 409.

The **first Admin comes from SQL**, not code ([runbook](./05-RUNBOOK-FIRST-ADMIN.md)): one
`AspNetUserRoles` row using the seeded Admin role id. Option 3 was rejected because it puts an admin credential in configuration or in a
startup path that runs on every launch.

### Consequences

- There is no way to create an Admin through the API from nothing. Recovering from "no Admin
  left" is a SQL insert; the last-Admin check exists to make that rare, but it is not atomic, so
  two simultaneous revokes could still remove both.
- A role change takes effect at the user's **next login**. Roles are baked into the JWT, so a
  demoted user keeps the old role until the token expires — at most 15 minutes (ADR-0005). There
  is no revocation list; `jti` is in the token as the hook for one.
- The role list lives in `RoleNames` and must match the roles seeded in `NZWalksAuthDbContext`.
  Adding a role means touching both.
- Roles still only protect `RegionsController` and `UsersController` until finding #8 is closed.

---

## ADR-0005 — Token lifetime and clock skew

**Status:** Accepted · **Date:** 2026-10-08

### Context

Tokens lived a hard-coded 20 minutes, `Jwt:ExpiryMinutes` was never read (finding #11), and
the validator's default `ClockSkew` of 5 minutes let an expired token keep working for ~25
minutes in total. With no revocation, token lifetime is the only bound on a stolen or stale token.

### Options

1. **Short access token, no refresh token** — simple; the user logs in again every 15 minutes.
2. **Short access token plus refresh tokens** — better experience, but needs a hashed token table,
   rotation with reuse detection and a revoke endpoint.
3. **Keep the longer lifetime.**

### Decision

**Option 1: 15 minutes, `ClockSkew = TimeSpan.Zero`.** The lifetime comes from `Jwt:ExpiryMinutes`
(default 15) and `Program.cs` refuses to start if it is missing or not positive. Skew can be zero
because one process issues and validates against one clock. Issued tokens carry `sub`, `jti` and
`iat`, and the validator accepts only HS256.

**Refresh tokens are deferred**, not rejected: there is no client yet to hold one, and doing it
properly is larger than the rest of this work combined.

### Consequences

- A user must log in again every 15 minutes. Fine for Swagger and tests; it will need refresh
  tokens once a real client exists.
- If tokens are ever validated by a different service, zero skew will reject tokens from a slightly
  fast issuer; revisit then.
- Role and account changes lag by up to 15 minutes (ADR-0004).

---

## ADR-0006 — Rate limiting and account lockout

**Status:** Accepted · **Date:** 2026-10-13 (tracker D4)

### Context

Nothing capped how fast a client could call the API. Login could be tried without limit, so a
password could be guessed as fast as the server answered, and every attempt costs a deliberately
slow password hash. Register could create accounts in bulk. Login and register are anonymous, so
the only key available there is the client IP.

### Options

**Algorithm**
1. **Fixed window** — N requests per window, counter resets at the window edge. Simplest; allows a
   burst of up to 2N across a window boundary, and its `Retry-After` is always the full window.
2. **Sliding window** — the window is split into segments; smooths the boundary burst and gives a
   closer `Retry-After`. A little more configuration.
3. **Token bucket** — allows short bursts, caps the steady rate. Best fit for bursty real clients.

**Partition key**
1. **Per IP** — works for anonymous endpoints; breaks behind a proxy (everyone shares the proxy IP)
   and can't stop a distributed attacker.
2. **Per user** — fairer for authenticated traffic, useless for login/register (no user yet).

**Where the state lives**
1. **In memory** (built-in limiter) — no dependency; resets on restart; each instance counts alone.
2. **Distributed** (e.g. Redis, or the gateway) — one count across instances; adds infrastructure.

**Brute force against one account**
1. **IP limits only** — a botnet gets the per-IP budget from every address.
2. **Plus Identity lockout** — counts failures per account, wherever they come from.

**Counting failed logins**
1. **`SignInManager.CheckPasswordSignInAsync(..., lockoutOnFailure: true)`** — one call checks
   lockout first, counts failures, resets on success. Needs `.AddSignInManager()`; issues no cookie.
2. **By hand** — `IsLockedOutAsync`, `CheckPasswordAsync`, `AccessFailedAsync`,
   `ResetAccessFailedCountAsync`. Same result, but checking the password before the lockout lets a
   locked account still confirm correct guesses — an easy ordering mistake.

### Decision

- Built-in .NET 8 rate limiter, no package. **Global fixed window: 100 requests/min per IP**,
  `QueueLimit = 0` (reject, never delay).
- **Named policy `RateLimitPolicies.Auth`: 5 requests/min per IP** on the whole `AuthController`
  (class-level, so any future credential endpoint is covered; `[DisableRateLimiting]` opts a cheap
  action out). Login and register share this budget and also count against the global 100.
- `UseRouting()` then `UseRateLimiter()`, after static files (image downloads aren't counted) and
  before authentication (rejected requests never reach the JWT handler). Explicit `UseRouting()` is
  required so the limiter can read `[EnableRateLimiting]` from the matched endpoint.
- Rejections are **429** problem+json (`type`, `detail`, `traceId`, written through
  `IProblemDetailsService`) with a `Retry-After` header, and are logged as warnings with IP and path.
- **Identity lockout: 5 consecutive failures, locked 5 minutes**, counted with option 1 above. A
  locked account gets the **same generic 401** as a wrong password — saying "locked" would confirm
  the account exists and that guessing is working. Lockouts are logged instead.
- Fixed window, per IP, in memory: chosen for simplicity at this stage. The alternatives are
  deferred, not rejected; each is tracked in docs/03 (findings #14–#17).

### Consequences

- Brute force is bounded: about 5 guesses/min per IP, and about 5 guesses per 5 minutes per account
  whatever the number of IPs.
- **Week 4 blocker:** behind Azure App Service every request arrives from the proxy, so all users
  would share one 100/min bucket and one 5/min login budget — effectively a login outage. Forwarded
  headers with known proxies must be configured before deploying (finding #14).
- `Retry-After` is always 60 (the full window), not the time actually left — safe but imprecise. A
  client can burst up to 200 requests across a window boundary (finding #15).
- Limiter counters are per process and reset on restart (finding #16). Lockout state is in the
  database, so it survives restarts.
- Limits and lockout settings are hard-coded in `Program.cs`; lockout can be abused to keep a known
  user locked out, and that user is never told why login fails (finding #17).
- In development every local tool shares `::1`, so Swagger, Postman and scripts share one budget —
  developers will hit the 5/min auth limit themselves. Register uses one of the same 5 permits.

---

## ADR-0007 — Access policy for Walks and Images

**Status:** Accepted · **Date:** 2026-10-13 (tracker D6)

### Context

`RegionsController` required `Reader`/`Admin` to read and `Writer`/`Admin` to write.
`WalksController` and `ImagesController` had no `[Authorize]` at all, so anyone could create,
change or delete walks and write files to the server's disk (finding #8). Nothing suggested the
open surface was intentional.

### Options

1. **Mirror Regions** — reads `Reader`/`Admin`, writes `Writer`/`Admin`.
2. **Public reads, protected writes** — walks browsable anonymously, like a public catalogue.
3. **Login required, no role split** — any authenticated user may read and write.

### Decision

**Option 1.** Walks `GetAll`/`GetById` require `Reader` or `Admin`; Walks `Create`/`Update`/`Delete`
and Images `Upload` require `Writer` or `Admin`. Role names come from `RoleNames` constants
(`$"{RoleNames.Writer},{RoleNames.Admin}"` is a compile-time constant), so a typo is a compile
error. Image **downloads** under `/Images` stay public: they are static files served before
routing and authorization, and an `<img>` tag cannot send a JWT.

Option 2 is a reasonable product choice, not a security fix; it was not taken because nothing in
the project defines a public audience yet. Revisit if a public frontend appears.

### Consequences

- Every Walks call now needs a token; any registered user (always `Reader`, ADR-0004) can read,
  only users an Admin made `Writer` can change data or upload.
- A user holding only `Writer` cannot read, the same as on Regions (finding #18).
- Authorization is still opt-in per action, so the next controller is public unless its author adds
  the attribute — the structural fix (fallback policy, named policies) is finding #18.
- Uploaded files remain publicly readable, which makes the SVG and file-naming gaps (finding #19)
  worth closing before real users upload.

---

## ADR-0008 — Week 2 hooks: what runs when, and what fails

**Status:** Accepted · **Date:** 2026-10-15 (opened 2026-10-13)

### Context

Week 2 adds Claude Code hooks that run `dotnet format` and analyzers after edits (tests join in
Week 3, when a test project exists). The repo has no `.editorconfig`, no `.gitattributes`, and 36
existing nullable warnings (finding #20). A full `dotnet build` takes ~25 s here.

### Options

**When the hook runs**
1. **After every edit** (`PostToolUse` on Edit/Write) — immediate feedback, but a build per edit is
   slow and a multi-file change is checked while half-finished.
2. **Once when Claude finishes a turn** (`Stop`) — one build per task, checks the finished state;
   feedback arrives later.
3. **Format after every edit, build/analyzers on `Stop`** — formatting is fast and per-file; the
   expensive check runs once.

**What fails the hook**
1. **Any warning** — strictest; fails immediately on the 36 existing warnings.
2. **New warnings only** — compare against a baseline count/list; lets the 36 be paid down separately.
3. **Errors only** — never blocks on warnings; easiest, catches least.

### Decision

**Option 3 for timing, errors-only for now.** Scripts live in `.claude/hooks/` (PowerShell),
wired in the committed `.claude/settings.json`:

- `guard-secrets.ps1` — **PreToolUse** on Edit/Write/MultiEdit. Blocks content that looks like a
  secret (connection-string password, `sa` login, a JSON `Key` value, private key, long key-like
  string outside source files). Added beyond the original options because the repo is public and
  secrets leaked once already (ADR-0003).
- `check-conflicts.ps1` — **PostToolUse** on Bash/PowerShell when the command is a pull, merge,
  rebase, cherry-pick or stash pop, and on **UserPromptSubmit** so a pull run in the user's own
  terminal is caught on their next message. Claude reports the conflict, resolves only mechanical
  ones, asks about the rest, and never commits or pushes without an explicit ok.
- `build-on-stop.ps1` — **Stop**. Refuses to finish while a conflict is open; otherwise runs
  `dotnet build` when a `.cs`/`.csproj`/`.json` changed since the last good build, and on errors
  makes Claude report them, then fix mechanical ones or ask. `stop_hook_active` lets the second
  pass through, so it never loops; locked DLLs (app running) are reported as unverified, not as
  errors.

Still to add: `dotnet format whitespace --include <file>` on PostToolUse once the `.editorconfig` +
format commit lands (finding #20, Week 2 A1), and failing on warnings once the 36 are fixed (A2).

Why errors-only: the 36 existing warnings would fail every turn today, and a baseline comparison
is throwaway work when A2 removes them this week.

### Consequences

- Hook output becomes part of every Claude Code session in this repo — a slow or noisy hook is paid
  for on every task.
- The formatting baseline commit touches many files once; review it as formatting-only.
- `NZWalks/.vs/` and `obj/` are tracked noise (CLAUDE.md); hooks must not stage or format them.
- The hooks only see Claude's actions and the user's messages: a build or pull done entirely in
  Visual Studio or a terminal is not checked until the next message.
- They call `powershell`, so they work on Windows only; a Mac/Linux clone gets hook errors.
- The build stamp lives in `.claude/hooks/.state/` (gitignored).
