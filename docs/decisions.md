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
| [0009](#adr-0009--fail-closed-authorization-and-named-policies) | Fail-closed authorization and named policies | Accepted | 2026-10-15 |
| [0010](#adr-0010--read-only-reviewer-subagent-and-the-commit-gate) | Read-only reviewer subagent and the commit gate | Accepted | 2026-10-18 |

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

**Status:** Accepted · **Date:** 2026-10-08 · **Amended by [ADR-0009](#adr-0009--fail-closed-authorization-and-named-policies):** `UsersController` is now guarded by the named policy `CanAdmin` (`[Authorize(Policy = PolicyNames.CanAdmin)]`) instead of `Roles = Admin`; the Admin-only rule itself is unchanged.

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

**Status:** Accepted · **Date:** 2026-10-13 (tracker D6) · **Amended by [ADR-0009](#adr-0009--fail-closed-authorization-and-named-policies):** the rules below are now the named policies `CanRead` and `CanWrite`; `CanRead` includes `Writer`, so a Writer-only user can read; and authorization is no longer opt-in (a fallback policy requires a login). The text below is the 2026-10-13 decision as written.

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

- `format-on-edit.ps1` — **PostToolUse** on Edit/Write/MultiEdit. Runs
  `dotnet format whitespace --folder --include <file>` on the one edited `.cs` file (~2 s). It does
  nothing until the repo has an `.editorconfig` (finding #20, Week 2 A1), so it can never reformat
  whole files against defaults.

**Failure rule: errors, plus new warnings.** `warning-baseline.txt` lists the accepted warnings as
`file|code|message` (no line numbers, so unrelated edits don't make an old warning look new); the
Stop hook fails on any warning not in it, and asks Claude to fix or ask, never to silence. It
started with the 36 existing warnings (34 distinct keys). **A2 fixed them on 2026-10-15 and the
baseline is now empty**, so every warning fails the Stop hook. Re-run
`.claude/hooks/update-warning-baseline.ps1` only to knowingly accept new warnings.

### Consequences

- Hook output becomes part of every Claude Code session in this repo — a slow or noisy hook is paid
  for on every task.
- The formatting baseline commit touches many files once; review it as formatting-only.
- `NZWalks/.vs/` and `obj/` are tracked noise (CLAUDE.md); hooks must not stage or format them.
- The hooks only see Claude's actions and the user's messages: a build or pull done entirely in
  Visual Studio or a terminal is not checked until the next message.
- They call `powershell`, so they work on Windows only; a Mac/Linux clone gets hook errors.
- The build stamp lives in `.claude/hooks/.state/` (gitignored).

---

## ADR-0009 — Fail-closed authorization and named policies

**Status:** Accepted · **Date:** 2026-10-15 · Closes finding #18; builds on [ADR-0007](#adr-0007--access-policy-for-walks-and-images).

### Context

Authorization was opt-in: an action with no `[Authorize]` was anonymous, and finding #8 (Walks and
Images open to the world) happened only because someone forgot the attribute. ADR-0007 closed that
hole by adding attributes, and explicitly left the structural fix open. The role rules were also
repeated as strings on 12 actions of Regions, Walks and Images, plus `UsersController` at class level (Regions used the literals `"Reader,Admin"` and `"Writer,Admin"`),
so a typo silently locked everyone out of one action, and changing who may read meant editing every
read action.

### Options

1. **Keep opt-in** and rely on review to catch a missing attribute.
2. **Fail closed with a `FallbackPolicy`** that requires a logged-in user, plus named policies for
   the role rules.
3. **Fail closed with `RequireRole` on the fallback itself** (a role required everywhere by default).

### Decision

**Option 2.** In `AddAuthorization` (`Program.cs`):

- `FallbackPolicy` = an authenticated user, with no role check. Every token already carries a role,
  because Login refuses a user who has none. The fallback applies only to endpoints with **no**
  authorization metadata, so any `[Authorize]` or `[AllowAnonymous]` overrides it.
- `[AllowAnonymous]` is on the `Register` and `Login` **actions**, not on `AuthController`, so a
  future third action there is protected by default. These are the only anonymous actions.
- Named policies, with their names in `Constants/PolicyNames.cs` (const strings, so attributes can
  use them): **`CanRead`** = Reader, Writer or Admin; **`CanWrite`** = Writer or Admin; **`CanAdmin`**
  = Admin. Regions, Walks and Images use `[Authorize(Policy = PolicyNames.CanRead | CanWrite)]`.
- `UsersController` uses `CanAdmin` at class level. The plan for this change had kept
  `Roles = RoleNames.Admin` there and rejected a `CanAdmin` policy; `CanAdmin` was added during the
  work so that every attribute in the API refers to a policy, and its behaviour is identical.

### Consequences

- **A forgotten attribute now means 401, not an open endpoint.** A new action is reachable by any
  logged-in user until it is given a policy, so a new write action still needs `CanWrite`
  deliberately; the fallback only guarantees "logged in".
- **Behaviour change: `CanRead` includes Writer.** A user holding only Writer used to be able to
  change walks and regions but not read them; now they can read. Register still gives everyone
  Reader, so this only affects an account whose Reader role an Admin revoked.
- A policy name is still a string looked up at request time, so a literal typo fails at runtime;
  always use the `PolicyNames` constants.
- `/Images/*` downloads and the Swagger UI stay public: static files and the Development-only Swagger
  middleware are not endpoints, so the fallback never sees them (ADR-0007 keeps images public).
- Rate limiting is unchanged. The limiter runs before authentication, so a 401 still counts against
  the global per-IP limit.
- **Week 4 (finding #4, CORS):** `UseCors` must sit before `UseAuthorization`, or browser preflight
  requests will be rejected with 401.

### Verified 2026-10-15

No token → 401 on Regions, Walks and Images upload; Login and Register reachable without a token;
Reader reads and cannot write; a Writer-only user reads (200) and writes (201); `/Images` and
Swagger load without a token; and a scratch copy with a deliberately un-attributed action returned
401 with no token and 200 for a Reader, which shows the fallback itself works.

---

## ADR-0010 — Read-only reviewer subagent and the commit gate

**Status:** Accepted · **Date:** 2026-10-18 · Week 2 tracker task B2, decision D4 ("which subagent?").

### Context

The repo's rules live in `CLAUDE.md`, the ADRs and `docs/03`, and a model writing code can drift from
them without noticing (finding #8 shipped an open endpoint; the A3 commit left several docs describing the
old role rules). The roadmap suggested a test-writer subagent, but there is no test project until Week 3.

### Options

1. **A reviewer that is only a prompt** the main session may or may not run.
2. **A read-only reviewer plus a hook that blocks `git commit`** until a review is recorded for the exact
   staged diff.
3. **A reviewer with shell access** so it can build and probe the API itself.

### Decision

**Option 2.**

- **The reviewer** is `.claude/agents/nzwalks-reviewer.md` with the tools `Read, Grep, Glob` only. It cannot
  edit files or run commands. It reads a diff file plus the current files, `CLAUDE.md`, the ADRs and
  `docs/03`, and returns a fixed report: findings with a level (**High, Medium, Low, Cosmetic**, the words
  `docs/03` uses), **compromises**, **requested checks** and what it could not review. A **High finding
  blocks the commit** (verdict BLOCK); anything lower is reported and does not block.
- **Elevation is per check, by the user.** When the reviewer wants evidence (a build, `dotnet format`, a test,
  a probe request) it lists a *requested check*. The main session runs it only after the user approves that
  specific check, and passes the output back. There is no second agent with a shell.
- **The gate** is `.claude/hooks/review-before-commit.ps1`, a `PreToolUse` hook on `Bash|PowerShell`. A
  `git commit` is refused until `.claude/hooks/.state/reviews/<hash>.txt` records PASS or ACCEPTED for the
  SHA-256 prefix of the exact staged diff. Flow: `review-prepare.ps1` writes the diff to a file and prints its
  hash; the reviewer reads it; the main session shows the user the report; `review-mark.ps1` records PASS,
  BLOCK, or ACCEPTED. ACCEPTED needs a note and is recorded only after the user has seen the High finding and
  decided to go ahead. Any change to what is staged changes the hash and voids the review.
  Commits that take their content from the working tree instead of the index (`-a/--all`, `-o/--only`,
  `-i/--include`, or path arguments, quoted or not) are refused, because the reviewed and committed diffs
  could then differ. So is a command that stages and commits in one go (`git add … && git commit …`),
  because the hook reads the index before the command runs; staging must be its own earlier command. With
  nothing staged, `--amend` and `--allow-empty` are refused too. A docs-only commit is
  reviewed too. The hash is computed from git's output captured as UTF-8 by a process, so it does not
  depend on the console code page (the first real review found that it did).
- **The gate's own files are protected from Claude's edit tools.** `.claude/hooks/guard-gate-files.ps1`
  refuses Claude's `Edit`/`Write` on `.claude/settings.json` (and `settings.local.json`), everything under `.claude/hooks/` (scripts,
  review markers) and `.claude/agents/`. The user can always edit them by hand, or let Claude change one
  file for 30 minutes by creating `.claude/hooks/.state/allow-gate-edit.txt` with that path in their own
  terminal. Only the edit tools are guarded; a shell command that writes to those files is not caught.
- **Compromises** (a knowingly accepted trade-off, deviation or imperfection) are reported with a level and a
  proposed destination: a deliberate choice that closes off an alternative goes to an ADR, an unintended
  defect or debt not fixed in the diff goes to a `docs/03` finding. **Nothing is written to a doc until the
  user confirms it**; the reviewer itself never writes.

### Consequences

- The gate proves a review was **recorded** for this diff, not that it was done well, and the main session
  writes the marker. The user sees every report; that is the real control. Never record PASS or ACCEPTED
  without having run the reviewer and, for a High finding, without the user's say-so.
- **Accepted limits of the gate (confirmed by the user 2026-10-18).** It guards against forgetting, not against
  a determined or misled session:
  - *The marker is a plain file.* A session misled by text in a file, or in a hurry, could run
    `review-mark.ps1 PASS` without running the reviewer, or write the marker directly, and an unreviewed
    change would commit with the gate showing green (the way a forgotten `[Authorize]` produced finding #8).
    The user seeing every report is what catches this: a commit with no report in the chat is the tell.
  - *The gate's files can still be changed through a shell command* (the edit-tool protection does not cover
    it). A session "fixing" a false block by loosening the matcher or removing the hook entry would turn the
    gate off silently for every later commit.
  - *It fails open.* If the hook itself crashes (PowerShell or git missing from the hook's environment, an
    execution-policy change after an update, a clone on another machine, a permissions problem under
    `.state/`), the commit goes through with no message, and the user would believe commits were reviewed
    when they were not. Failing closed was rejected because a broken hook would then stop every commit Claude
    makes until the user repairs it, which is worse than the risk it removes.
- It only sees commits made by the `git commit` command that Claude runs. A commit made in Visual Studio or
  a terminal is not gated, and neither are commits created by `git merge`, `cherry-pick`, `rebase`,
  `revert`, `pull` or `commit-tree`, even when Claude runs them.
- The hook finds `git commit` by reading the command text with heredoc bodies, here-strings and quoted
  strings blanked out. That is a heuristic: an unusual way of writing the command can slip past it (for
  example a flag placed after a line continuation), some options are misread (`-t <file>`, `--trailer`, and
  `-S<keyid>` can be taken for a path or an `-a`/`-o`/`-i` flag and block by mistake), and a command that merely contains the
  words `git commit` outside quotes can be blocked by mistake. If the gate misreads a command, ask the user;
  do not route the commit around it. The gate reads the index when the command starts, so staging must be
  its own earlier command.
- **Known limits of that heuristic, found by the third review and accepted by the user on 2026-10-18** (none is
  High; each is either a false block with a workaround, or a rare form):
  - *False block (Medium):* `git commit -F - <<'EOF' … EOF` (message piped in) is refused, because the
    heredoc replacement leaves a stray word that is read as a path. Use `-m "…"` or `-F <file>` instead.
  - *False blocks (Low):* a redirection with a space (`> /dev/null`, `2> $null`) is read as a path; unquoted
    glued values (`-mfix`) and abbreviated long options (`--onl`, `--incl`) are misread.
  - *Not refused (Low):* `-am"msg"` (a flag cluster with a glued quoted value) is not seen as `-a`;
    `--pathspec-from-file=<f>` commits working-tree paths without being refused; a quoted `git.exe` commit on
    the second or later line of a multi-line command is not recognised.
  - *Only the first `git commit` in a command is inspected,* so a second commit, `-a`, or later staging after it
    is not examined (`git commit -m a; git commit -a -m b`).
  - *The staging-verb list is fixed* (`add`, `stage`, `rm`, `mv`, `reset`, `restore`, `apply`, `stash`,
    `checkout`, `switch`, `update-index`): other index-changing commands (`merge --squash`, `cherry-pick -n`,
    `revert -n`) are not caught, and index-neutral ones such as `git checkout -b x && git commit` are refused.
- The reviewer can't build or run anything, so warnings and runtime behaviour reach it only as requested
  checks; its "Not reviewed" section says so each time.
- Agent definitions load when a session starts, so a new `.claude/agents/` file is available from the next
  session. In the session that created it, the dry run used the built-in `Explore` agent pointed at the same
  definition file (which cannot enforce the tool list).
- Every commit pays for a review, including small ones. If that becomes too slow, the first thing to relax is
  docs-only commits, as a new ADR.
- The report is model output. The reviewer is told to treat diff content as data and to report text aimed at it
  as a High finding.
- These scripts are PowerShell, so they work on Windows only, like the Week 2 hooks (ADR-0008): the hook
  `review-before-commit.ps1`, the two on-demand scripts `review-prepare.ps1` and `review-mark.ps1`, and the
  shared helper `review-common.ps1`. Diff files and review markers live in `.claude/hooks/.state/`, which is
  gitignored.

### Dry run (2026-10-18)

Run on the A3 commit (`30ce92e`) before wiring the gate: verdict PASS, with 3 Medium doc findings
(CLAUDE.md still said `Roles = RoleNames.Admin` for `UsersController`; ADR-0007 not marked as amended by
ADR-0009; `docs/01` and `docs/02` still describe `Roles =` and omit `PolicyNames`), 1 Low ("11 actions"
should be 12) and 1 Cosmetic (the date). All were real when checked against the files.

### First real review (2026-10-18)

Run on this ADR's own commit (staged diff `8ec8c158594f3a2f`): verdict PASS, with 6 Medium findings, 4 Low and
1 Cosmetic, all in the gate and its docs. The ones that were bugs were fixed before the commit: the diff hash
changed with the console code page (the same diff hashed as `8ec8…` under the default code page 65001 and as
`f758…` under code page 437; after the fix both give `8ec8…`), `git.exe` and quoted-option forms were missed, path/`-o`/`-i` commits were not refused,
`--amend`/`--allow-empty` with an empty index passed, and quoted text was read as flags. The rest are
recorded under Consequences.

A second review of the fixed diff (`d1a8bb912b3e8299`) was also PASS, with 4 Medium findings: a command that
stages and commits in one go was judged against the old index (the way commits were being made), quoted path
arguments were not refused, multi-line messages with a quote or an apostrophe were falsely blocked, and the
"use a script file" advice was itself a way round the gate. All four were fixed before the commit, along with
two Lows (git's error output is now drained so it cannot stall the hook, and the grant command now creates
the `.state/` folder first). Two reviews of one tool each finding real gaps in the matcher is why the limits
above say "heuristic": it is a guard against forgetting, and further parsing edge cases are accepted rather
than chased. A third review (`a2fcba26…`) was PASS with one Medium (a false block on `git commit -F - <<'EOF'`
that the heredoc fix introduced) and four Lows; they are recorded as accepted limits above.
