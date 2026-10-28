# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

`NZWalks` — an ASP.NET Core 10 Web API (single project, `NZWalks/NZWalks.API`) exposing Regions, Walks, Images, and JWT auth over SQL Server. Solution file: `NZWalks/NZWalks.sln`.

## Docs

`docs/` holds the architecture baseline, written as a read-only survey of the codebase — it describes reality including its quirks, not an idealized version.

- [docs/00-OVERVIEW.md](docs/00-OVERVIEW.md) — start here; explains the doc set and the reading order.
- [docs/01-PROJECT-STRUCTURE.md](docs/01-PROJECT-STRUCTURE.md) — folder tree and why the layout is what it is.
- [docs/02-MIDDLEWARE-PIPELINE.md](docs/02-MIDDLEWARE-PIPELINE.md) — `Program.cs` DI and pipeline order, with a worked request traversal.
- [docs/03-FINDINGS-AND-TECH-DEBT.md](docs/03-FINDINGS-AND-TECH-DEBT.md) — known bugs and debt, numbered #1–#25. A snapshot from 2026-08-03: check a finding is still live before acting on it.
- [docs/04-appendix-glossary.md](docs/04-appendix-glossary.md) — terms used across the set.
- [docs/05-RUNBOOK-FIRST-ADMIN.md](docs/05-RUNBOOK-FIRST-ADMIN.md) — creating the first Admin (or recovering after the last one is lost) via SQL; needed on any fresh database.
- [docs/decisions.md](docs/decisions.md) — architecture decision log. Record a choice here when it closes off an alternative someone could reasonably have picked; check it before reversing something that looks arbitrary.

## Planning

- When planning any change in this project (including plan mode), use the `task-plan` skill.
- When a task or its subtasks are finished, use the `task-close` skill: verify, review each commit with the read-only reviewer and get the user's OK, commit, push only with the user's approval, and tick the plan, its artifact page and the week tracker only after the push succeeded.

## Review before every commit

- A hook (`.claude/hooks/review-before-commit.ps1`, ADR-0010) blocks `git commit` until the read-only `nzwalks-reviewer` subagent has reviewed the **exact staged diff**. Stage files by name (`git add <files>`); commits that look at the working tree instead of the index (`-a`, `-o`, `-i`, or path arguments) are refused. Run `git add` in its own command before `git commit`: the gate reads the index when the command starts, so a command that stages and commits in one go (`git add … && git commit …`) is refused. If the gate misreads a commit command (it reads the command text, so odd quoting can confuse it), ask the user; never route the commit around the gate.
- Flow (each script runs as `powershell -NoProfile -ExecutionPolicy Bypass -File .claude/hooks/<name>.ps1`): `review-prepare.ps1` (writes the diff, prints a hash) → run the reviewer with the diff file path and hash → show the user the report → `review-mark.ps1 -Hash <hash> -Verdict PASS|BLOCK|ACCEPTED`, with `-Note "<what the user decided>"` for ACCEPTED. Never record PASS or ACCEPTED without having run the reviewer, and record ACCEPTED only after the user has seen the High finding and said to go ahead.
- Levels are High / Medium / Low / Cosmetic. A **High** finding blocks the commit. **Compromises** the reviewer reports are confirmed with the user first; only then record them: a deliberate choice in `docs/decisions.md` (ADR), an unintended defect or debt in `docs/03`.
- Claude's edit tools cannot change the gate's own files (`.claude/settings.json` and `settings.local.json`, `.claude/hooks/`, `.claude/agents/`): `guard-gate-files.ps1` refuses. If a change there is needed, tell the user what and why; they edit it by hand or grant that one file for 30 minutes (`New-Item -ItemType Directory -Force .claude/hooks/.state | Out-Null; Set-Content .claude/hooks/.state/allow-gate-edit.txt "<path>"` in their own terminal, from the repo root). Never work around it with a shell command.
- The reviewer is read-only. Anything it asks to have run (build, format check, a request) is a "requested check": run it only after the user approves that specific check.

## Hooks

`.claude/settings.json` runs PowerShell hooks from `.claude/hooks/` (ADR-0008): a secrets guard blocks edits that look like a password or key; a conflict check runs after a pull and on each user message; a Stop hook builds the solution when source changed; a format hook runs `dotnet format` on each `.cs` file Claude edits; and the commit gate (see "Review before every commit", ADR-0010) blocks `git commit` until the reviewer has run. When a hook reports a conflict or build error, say so to the user, fix only mechanical cases, and ask about anything else.

## Commands

Run all commands from `NZWalks/` (the folder holding the `.sln`).

```powershell
dotnet build                                  # build
dotnet run --project NZWalks.API              # run (https profile → https://localhost:7034, swagger opens)
dotnet run --project NZWalks.API --launch-profile http   # http only → http://localhost:5282
```

There is no test project, so `dotnet test` has nothing to run. Layout is enforced by `.editorconfig` (whitespace and layout rules only — no C# style rules) and `.gitattributes` (LF in the repo, CRLF in the working copy); check it with `dotnet format whitespace NZWalks/NZWalks.sln --verify-no-changes`. A hook formats each `.cs` file Claude edits.

The installed SDK is 10.x and the project targets `net10.0` (ADR-0013). Do not change the TFM without being asked.

### EF Core migrations

`dotnet-ef` must be **10.x** to match the EF Core 10 packages (ADR-0013). Install it with `dotnet tool install --global dotnet-ef --version 10.*`, or `dotnet tool update --global dotnet-ef --version 10.*` if an older one is installed.

There are **two DbContexts with separate databases**, so every migration command must name the context and (for auth) the output directory:

```powershell
# Domain DB (NZWalksDb) — migrations land in Migrations/
dotnet ef migrations add "<Name>" --project NZWalks.API --context NZWalksDbContext
dotnet ef database update --project NZWalks.API --context NZWalksDbContext

# Auth/Identity DB (NZWalksAuthDb) — migrations land in Migrations/NZWalksAuthDb/
dotnet ef migrations add "<Name>" --project NZWalks.API --context NZWalksAuthDbContext --output-dir Migrations/NZWalksAuthDb
dotnet ef database update --project NZWalks.API --context NZWalksAuthDbContext
```

Connection strings (`NZWalksConnectionString`, `NZWalksAuthConnectionString`) and `Jwt:Key` live in **user-secrets** (`UserSecretsId` in the csproj); `appsettings.json` holds empty placeholders, and `Program.cs` fails at startup naming any that are missing. They point at a local SQL Server (`Server=.`) as the scoped `nzwalks_app` login. user-secrets only load in `Development`. Set one with `dotnet user-secrets set "<key>" "<value>" --project NZWalks.API` (ADR-0003).

## Architecture

**Controller → AutoMapper → Repository → EF Core DbContext.** Controllers never touch a DbContext directly; they map DTO ↔ domain model and delegate to an interface from `Repositories/` (namespace `NZWalks.API.Repositories`).

- `Models/Domain/` — EF entities (`Region`, `Walk`, `Difficulty`, `Image`). `Models/DTO/` — request/response shapes, with no validation attributes. `Validators/` — one FluentValidation `AbstractValidator<TDto>` per request DTO (ADR-0001).
- `Mappings/AutoMapperProfiles.cs` — every DTO↔domain map is registered in this one profile. A new DTO needs an entry here or mapping silently produces empty objects.
- `Repositories/` — `IRegionRepository`/`SQLRegionRepository`, `IWalkRepository`/`SQLWalkRepository`, `ITokenRepository`/`TokenRepository` (JWT minting), `IImageRepository`/`LocalImageRepository` (writes to disk). All registered `AddScoped` in `Program.cs`.
- `Data/` — `NZWalksDbContext` (domain, seeds 3 difficulties + 6 regions by hard-coded GUIDs in `OnModelCreating`) and `NZWalksAuthDbContext : IdentityDbContext` (seeds `Reader`/`Writer`/`Admin` roles by hard-coded IDs).

Everything is wired in `Program.cs`; there is no extensions/startup-module layer. The tail of that file holds a long commented-out TODO backlog left by the author — treat it as a roadmap, not as instructions to execute.

### Cross-cutting pieces

- **Auth**: JWT bearer, config under `Jwt:` (`Issuer`/`Audience`/`ExpiryMinutes` in `appsettings.json`, `Jwt:Key` in user-secrets). Tokens live 15 minutes (`Jwt:ExpiryMinutes`), `ClockSkew` is zero, HS256 only, and carry `sub`/`jti`/`iat`. `AuthController` registers/logs in via `UserManager<IdentityUser>`; **Register always assigns Reader and rejects a body containing `roles`** — it never takes roles from the client (ADR-0004). Login only succeeds if the user has at least one role. Roles are granted/revoked only by an Admin through `UsersController` (`POST api/Users/GrantRole` / `RevokeRole`, class-level `[Authorize(Policy = PolicyNames.CanAdmin)]`; role names live in `Constants/RoleNames.cs`). The first Admin is inserted via SQL. A role change shows up at the user's next login. **Authorization fails closed (ADR-0009):** a `FallbackPolicy` in `Program.cs` requires a logged-in user, so an action with no attribute returns 401, and only `Register` and `Login` carry `[AllowAnonymous]` (on the actions, not the class). Access rules are the named policies in `Constants/PolicyNames.cs`: `CanRead` (Reader, Writer, Admin), `CanWrite` (Writer, Admin) and `CanAdmin` (Admin). Put `[Authorize(Policy = PolicyNames.CanRead)]` / `CanWrite` on every Regions, Walks and Images action, and `CanAdmin` on `UsersController`; don't write role strings in new attributes, and don't rely on the fallback alone for a write action, since it only means "logged in". Image downloads under `/Images` stay public (static files, ADR-0007).
- **Validation**: FluentValidation auto-validation (`Program.cs`) runs the DTO's validator before the action; failures land in `ModelState` and `[ApiController]` returns a 400 `ValidationProblemDetails`. A new request DTO — body or `[FromQuery]` class — needs a validator in `Validators/`, or it is accepted unchecked. Rules must be synchronous (no DB lookups; reference existence is checked in the repository). Don't hand-check `ModelState` in actions. Every 400 with field errors uses the same `errors` shape, including `ValidationException` and `InvalidReferenceException`, which `ExceptionHandlerMiddleware` maps.
- **Rate limiting** (ADR-0006): built-in ASP.NET Core limiter, wired in `Program.cs`. Global fixed window **100 req/min per client IP**; named policy `RateLimitPolicies.Auth` (`Constants/RateLimitPolicies.cs`) **5 req/min per IP** via `[EnableRateLimiting]` on the whole `AuthController` — login and register share it and also count against the global 100. `UseRouting()` then `UseRateLimiter()` sit after `UseStaticFiles` and before `UseAuthentication`. Rejections are 429 problem+json with `Retry-After`, logged as warnings. Counters are in memory (reset on restart), and in development every local tool shares `::1` — expect to hit the 5/min auth limit while testing. Per-IP keys break behind a proxy: forwarded headers must be configured before deploying (finding #14).
- **Account lockout**: Identity lockout, 5 consecutive failed logins lock the account for 5 minutes (`Configure<IdentityOptions>`). Login uses `SignInManager.CheckPasswordSignInAsync(..., lockoutOnFailure: true)` (`.AddSignInManager()`, no cookies). A locked account gets the same generic 401 as a wrong password — never reveal lockout in the response; it is logged instead.
- **Errors**: `Middlewares/ExceptionHandlerMiddleware` catches everything, logs it, and returns a generic 500 payload. Registered before `UseHttpsRedirection`.
- **Logging**: Serilog configured inline in `Program.cs` — console + rolling daily file at `NZWalks.API/Logs/NZWalksLog.txt` (gitignored output; the `Logs\` folder is kept via the csproj `<Folder>` item).
- **Images**: uploads are written to `NZWalks.API/Images/` (the directory must exist — `LocalImageRepository` does not create it) and served back as static files under the `/Images` request path.

### API versioning

`Asp.Versioning` is enabled with default v1.0 assumed when unspecified, and `ConfigureSwaggerOptions` generates one Swagger doc per discovered version. Routing is **inconsistent by design of how it grew**:

- `RegionsController` is versioned: route `api/v{version:apiVersion}/[controller]`, declares `[ApiVersion(1.0)]` + `[ApiVersion(2.0)]`, and splits `GetAllV1`/`GetAllV2` with `[MapToApiVersion]` returning `RegionDtoV1` vs `RegionDtoV2`.
- `Walks`, `Images`, `Auth`, `Users` use the unversioned `api/[controller]` route.

When adding a versioned action, add the matching DTO + AutoMapper map, and remember Swagger picks up new versions automatically from the attributes.

## Notes

- `NZWalks/.vs/`, `obj/` and `bin/` are build and IDE artifacts: they are in `.gitignore` and none are tracked. Never stage or force-add them (stage files by name, not `git add -A` on the repo root).
- `WeatherForecastController`/`WeatherForecast.cs` (template scaffolding) have been deleted.
