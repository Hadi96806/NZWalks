# 03 — Findings & Tech Debt

This is a snapshot of real issues found while mapping the current codebase (see [01-PROJECT-STRUCTURE.md](./01-PROJECT-STRUCTURE.md) and [02-MIDDLEWARE-PIPELINE.md](./02-MIDDLEWARE-PIPELINE.md)). None of these are production incidents — the app isn't in production yet — but they should be addressed **before or while** scaling up, since several of them get more expensive to fix the longer they're left (naming, project layout) and others are silent correctness bugs a new feature could easily trip over (the error-id bug, the role-assignment bug).

This is a punch list for upcoming work, not a changelog. Entries that have since been fixed are marked **RESOLVED** with the date and a pointer to what replaced them; the text beneath such a heading is kept as the record of what was found.

## Bugs

### 1. `ExceptionHandlerMiddleware` error id is always `Guid.Empty` — **RESOLVED** (found already fixed 2026-10-15)
**Resolution:** the id is now `Guid.NewGuid()` at `Middlewares/ExceptionHandlerMiddleware.cs:103`. The text below describes the state when the finding was written.
**File:** `NZWalks.API/Middlewares/ExceptionHandlerMiddleware.cs`
**Current behavior:** the error id is generated with `new Guid()` — the parameterless constructor, which always produces `00000000-0000-0000-0000-000000000000`, not a random value.
**Why it matters:** the whole point of returning an error id to the client is so they can quote it back for support/debugging and you can grep logs for that exact id. Right now every single error response has the *same* id, so it's useless as a correlation key.
**Suggested direction:** change to `Guid.NewGuid()`.

### 2. Response `ContentType` typo: `"Aplication/json"` — **RESOLVED** (found already fixed 2026-10-15)
**Resolution:** the explicit `ContentType` assignment is gone; the response is written with `WriteAsJsonAsync(..., contentType: "application/problem+json")` at `Middlewares/ExceptionHandlerMiddleware.cs:153`, so there is no misspelt MIME type left. The text below describes the state when the finding was written.
**File:** `NZWalks.API/Middlewares/ExceptionHandlerMiddleware.cs`
**Current behavior:** the 500 response sets `ContentType = "Aplication/json"` (missing a "p").
**Why it matters:** some HTTP clients / API gateways sniff or validate the `Content-Type` header; a misspelled MIME type can cause clients to fail to parse the body as JSON even though the body itself is valid JSON.
**Suggested direction:** fix to `"application/json"` (or just use `WriteAsJsonAsync`, which sets this correctly on its own — it's already being called, so the explicit `ContentType` assignment may be redundant).

### 3. Error message typo: "adminstrator" — **RESOLVED**
**Resolution:** the 500 message in `ExceptionHandlerMiddleware` no longer contains the typo (verified 2026-10-13; tracker B5s1). The text below describes the state when the finding was written.
**File:** `NZWalks.API/Middlewares/ExceptionHandlerMiddleware.cs`
**Current behavior:** the client-facing error message reads "Something went wrong, please contact adminstrator".
**Why it matters:** cosmetic, but it's user (developer/support)-facing text that will get quoted in bug reports and screenshots.
**Suggested direction:** fix the spelling; consider whether this message should be configurable/localizable long-term.

### 9. `AuthController.Register` silently drops all but the first role — **RESOLVED 2026-10-08**
**File:** `NZWalks.API/Controllers/AuthController.cs`
**Current behavior (as found):** `registerRequestDto.Roles` accepted a list, but only `Roles[0]` was ever passed to `userManager.AddToRoleAsync`.
**Why it mattered:** the dropped roles were the smaller problem. The caller chose their own role, so `"roles":["Admin"]` produced an Admin token and every `[Authorize(Roles=…)]` was decorative.
**The suggested fix was wrong:** looping over `Roles` or using `AddToRolesAsync` would still have honoured client-supplied roles — it fixes "only the first role is applied" and keeps "the caller picks their roles".
**Resolution:** `Roles` was removed from `RegisterRequestDto`; Register always assigns Reader, and an unknown JSON member such as `roles` is a 400. Roles are granted and revoked only by an Admin through `UsersController` (see [ADR-0004](./decisions.md#adr-0004--roles-are-granted-only-by-an-admin)). Register now also returns Identity's own error messages as a 400 problem+json instead of the plain string "Something went wrong!".

### 12. `RegionsController.GetById` route is missing its braces — **RESOLVED 2026-10-11**
**File:** `NZWalks.API/Controllers/RegionsController.cs`
**Current behavior (as found):** `[Route("id:Guid")]` declared a literal path segment, so the route was `/api/v1/Regions/id:Guid`. `GET /api/v1/Regions/{id}` returned 404 even for an existing region, and the `Location` header from `CreatedAtAction(nameof(GetById), …)` in `Create` pointed at a URL that did not resolve.
**Why it mattered:** a wrong 404 tells the client a record does not exist when it does, and every create handed back a broken link.
**Resolution:** `[Route("{id:Guid}")]`, matching `Update` and `Delete`. Fixed as part of the consistent-error-responses work, which also moved the Images upload 400 to `ValidationProblem`, removed the unreachable `else { return BadRequest(); }` in `Regions.Create`, and gave the Regions/Walks 404s a `detail` naming the id.

### 13. Bad client input could cause a 500, and reference errors lost their field list — **RESOLVED 2026-10-11**
**Files:** `NZWalks.API/Controllers/WalksController.cs`, `Controllers/ImagesController.cs`, `Middlewares/ExceptionHandlerMiddleware.cs`
**Current behavior (as found):**
- `GET /api/Walks?pageSize=0` divided by zero and `?pageNumber=0` sent a negative OFFSET to SQL — both 500. `pageSize` had no upper bound.
- `POST /api/Images/Upload` with no file threw a `NullReferenceException` in the hand-written check — 500. The extension check was case-sensitive (`.JPG` rejected).
- `InvalidReferenceException` built a `ValidationProblemDetails`, but `WriteAsJsonAsync` serialized it as its static type `ProblemDetails`, so the `errors` list naming the missing `RegionId`/`DifficultyId` never reached the client.
**Why it mattered:** a 500 for a client mistake tells the caller the server is broken and hides what to fix; a 400 without `errors` gives no field to fix.
**Resolution:** FluentValidation (ADR-0001) — `WalkQueryDto` + validator (`pageNumber ≥ 1`, `pageSize` 1–100, allowed filter/sort names) and `ImageUploadDtoValidator` (file required, any-case extension). The middleware now serializes with the runtime type, and maps `ValidationException` to the same 400 shape.



### 4. No CORS policy configured
**Files:** `NZWalks.API/Program.cs` (absence of `AddCors`/`UseCors`)
**Current behavior:** there is no CORS configuration anywhere in the project.
**Why it matters:** as soon as a browser-based frontend (a separate origin — different port/host) tries to call this API directly from JavaScript, every request will be blocked by the browser's same-origin policy. This is invisible today because nothing browser-based is calling the API yet.
**Suggested direction:** decide the allowed origin(s) for the eventual frontend and add an explicit named CORS policy (avoid `AllowAnyOrigin()` combined with credentials/auth headers).

### 8. `WalksController` and `ImagesController` have no role-based authorization — **RESOLVED 2026-10-13**
**Resolution:** Walks reads require `Reader`/`Admin`, Walks writes and Images upload require `Writer`/`Admin` — the same split as Regions ([ADR-0007](./decisions.md#adr-0007--access-policy-for-walks-and-images)). Image downloads under `/Images` stay public (static files run before authorization). Follow-up debt is in #18–#20. The text below describes the state when the finding was written.
**Files:** `NZWalks.API/Controllers/WalksController.cs`, `NZWalks.API/Controllers/ImagesController.cs`
**Current behavior:** `RegionsController` consistently applies `[Authorize(Roles="Reader,Admin")]` (reads) / `[Authorize(Roles="Writer,Admin")]` (writes). `WalksController` and `ImagesController` have no `[Authorize]` attributes at all — every action on them is currently reachable anonymously.
**Why it matters:** this looks like an oversight rather than an intentional public API surface, given the pattern established on `RegionsController`. As-is, anyone can create/update/delete walks or upload images without authenticating.
**Suggested direction:** decide the intended access policy per action (likely mirroring `RegionsController`'s Reader/Writer/Admin split) and apply it consistently.

### 11. `Jwt:ExpiryMinutes` is configured but never read — **RESOLVED 2026-10-08**
**Resolution:** `TokenRepository` now reads `Jwt:ExpiryMinutes` (set to 15) and uses `DateTime.UtcNow`; `Program.cs` refuses to start when the value is missing or not positive; `ClockSkew` is zero. See [ADR-0005](./decisions.md#adr-0005--token-lifetime-and-clock-skew). The text below describes the state when the finding was written.
**Files:** `NZWalks.API/appsettings.json`, `NZWalks.API/Respositries/TokenRepository.cs`, `NZWalks.API/Program.cs`
**Current behavior:** `appsettings.json` defines a `Jwt:ExpiryMinutes` key, but neither `Program.cs` nor `TokenRepository` reads it — token expiry is therefore either hardcoded elsewhere or not being set from configuration at all.
**Why it matters:** dead configuration is misleading — anyone tuning token lifetime by editing `appsettings.json` will see no effect and won't know why.
**Suggested direction:** either wire `Jwt:ExpiryMinutes` into the token's `expires` claim in `TokenRepository.CreateToken`, or remove the unused key.

## Naming / Consistency

### 5. Repository layer is misspelled throughout: `Respositries`, `IRegionRespository`
**Files:** `NZWalks.API/Respositries/` (folder), `IRegionRespository.cs`, all references to `IRegionRespository`
**Current behavior:** the folder name and one interface name are consistently misspelled across the codebase (not just a one-off typo — it's the actual, referenced identifier).
**Why it matters:** harmless functionally, but it's the kind of thing that gets *harder* to fix the more code references it (every `using NZWalks.API.Respositries;` and every `IRegionRespository` usage). Doing it now, while the surface area is small, is far cheaper than doing it after the project has grown.
**Suggested direction:** a rename pass (folder, namespace, interface name) while the codebase is still small — this is exactly the kind of change that should happen *before* scaling, not after.

### 6. `Models/Pagination Result/` has a literal space in the folder path
**Files:** `NZWalks.API/Models/Pagination Result/` (folder), namespace `Pagination_Result`
**Current behavior:** the folder name contains a literal space, while the C# namespace uses an underscore (`Pagination_Result`) — the two don't match, which is a mismatch some tooling (and some CI/build systems, particularly on non-Windows runners or shell scripts that split on whitespace) can trip over.
**Why it matters:** works today on this machine, but is fragile — a script `find`-ing or globbing paths without proper quoting will break on this folder.
**Suggested direction:** rename the folder to `PaginationResult` (no space) to match common .NET convention, and consider whether the underscore in the namespace should also become PascalCase (`NZWalks.API.PaginationResult`) for consistency with the rest of the codebase's namespaces.

## Structural / Scaling

### 7. Everything lives in one project — no Domain/Data/Repository class library split
**Files:** whole solution — `NZWalks.sln` only references `NZWalks.API.csproj`
**Current behavior:** controllers, DbContexts, repositories, DTOs, and mapping profiles are all compiled into a single assembly.
**Why it matters:** this is fine at the current size, but it will start to hurt once: (a) you want to unit-test repository/domain logic without pulling in ASP.NET Core hosting; (b) another host (a background worker, a console tool, a second API) wants to reuse the domain/data layer; (c) you want to enforce that, say, controllers can never directly reference `NZWalksDbContext` — a compiler-enforced boundary that only a separate project can give you.
**Suggested direction:** when the time comes, extract `Models/Domain`, `Data/`, and `Respositries/` into a `NZWalks.Data` (or similarly named) class library that `NZWalks.API` references. This is a bigger, deliberate step — not a quick fix — and is exactly the kind of change this documentation set exists to set up for.

### 10. Uploaded images are stored on local disk, tied to a single instance
**Files:** `NZWalks.API/Respositries/LocalImageRepository.cs`, `Program.cs` (`StaticFileOptions` pointed at `ContentRoot/Images`)
**Current behavior:** `LocalImageRepository` writes uploaded files to `{ContentRoot}/Images` on the machine running the app, and files are served back out via a `PhysicalFileProvider` pointed at that same local folder.
**Why it matters:** this works for a single instance on a single machine. It does not survive: horizontal scaling (a second instance won't see files uploaded to the first), container redeploys (an ephemeral container filesystem loses all uploaded images on restart), or most cloud hosting models (App Service/containers typically don't guarantee persistent local disk).
**Suggested direction:** when scaling beyond a single instance, replace `LocalImageRepository` with an implementation backed by shared/durable storage (e.g. Azure Blob Storage, S3-compatible storage) behind the same `IImageRepository` interface — this is precisely the kind of swap the repository pattern (see [01-PROJECT-STRUCTURE.md](./01-PROJECT-STRUCTURE.md), section 5) is designed to make painless.

### 14. Rate limits are keyed by client IP, which breaks behind a proxy — **OPEN, blocks the Week 4 deploy**
**Files:** `NZWalks.API/Program.cs` (`AddRateLimiter`, `RemoteIpAddress` partition key)
**Current behavior:** both limiters partition by `HttpContext.Connection.RemoteIpAddress`. There is no `UseForwardedHeaders`.
**Why it matters:** behind Azure App Service (or any reverse proxy or load balancer) every request arrives from the proxy's address, so all users share one 100/min bucket and one 5/min login budget — a handful of users would lock everyone out of login. In the other direction, a distributed attacker (many IPs) gets the full per-IP budget from every address; only account lockout bounds that.
**Suggested direction:** before deploying, configure `ForwardedHeadersOptions` with `ForwardedHeaders.XForwardedFor` and `KnownProxies`/`KnownNetworks` set to the real proxy — never trust `X-Forwarded-For` from anyone, or clients can spoof their IP and dodge the limit — and call `UseForwardedHeaders()` first in the pipeline. See [ADR-0006](./decisions.md#adr-0006--rate-limiting-and-account-lockout).

### 15. Fixed-window limiter allows boundary bursts and reports an imprecise `Retry-After` — **OPEN (accepted trade-off)**
**Files:** `NZWalks.API/Program.cs` (`GetFixedWindowLimiter`, `OnRejected`)
**Current behavior:** each window resets its counter at the edge, so a client can send 100 requests at 0:59 and 100 more at 1:00. The fixed-window limiter's `MetadataName.RetryAfter` is the full window length, so `Retry-After` is always `60`, even 20 seconds into the window.
**Why it matters:** the effective peak is twice the stated limit, and well-behaved clients wait longer than needed. Acceptable at the current scale.
**Suggested direction:** switch to `GetSlidingWindowLimiter` (e.g. 6 segments of 10 s) or `GetTokenBucketLimiter` if burstiness or retry precision start to matter. The `OnRejected` code stays the same.

### 16. Rate-limit state is in memory, per instance — **OPEN (accepted trade-off)**
**Files:** `NZWalks.API/Program.cs` (built-in `PartitionedRateLimiter`)
**Current behavior:** counters live in the process. A restart resets every counter; two instances each allow the full limit (2 × 100/min, 2 × 5/min logins).
**Why it matters:** irrelevant on one instance. Once scaled out (see #10), limits multiply by the instance count, and every redeploy gives an attacker a fresh budget. Identity lockout is unaffected — it is stored in the database.
**Suggested direction:** when running more than one instance, move to a distributed limiter (e.g. Redis-backed), or enforce limits at the gateway (Azure Front Door / API Management) instead of in the app.

### 17. Rate-limit and lockout settings are hard-coded; lockout abuse and user feedback are unaddressed — **OPEN**
**Files:** `NZWalks.API/Program.cs` (`AddRateLimiter`, `Configure<IdentityOptions>`), `NZWalks.API/Controllers/AuthController.cs`
**Current behavior:**
- `PermitLimit`/`Window` (100/min, 5/min) and lockout (5 failures, 5 minutes) are literals in `Program.cs`; the two `GetFixedWindowLimiter` blocks differ only in the limit, and the `AddRateLimiter` block is ~60 lines of `Program.cs`.
- Login and Register share one 5/min budget.
- A locked-out user gets the generic 401 and is never told the account was locked. Anyone who knows an email can keep that account locked by sending 5 wrong passwords every 5 minutes. Lockout duration is fixed, not escalating.
**Why it matters:** changing a limit per environment needs a rebuild. Register abuse is slow and spread out, so a per-minute budget fits it poorly. The generic 401 is deliberate (it hides whether the account exists and whether guessing works) but leaves a real user confused, and a short fixed lockout is a mild denial-of-service lever against a known user.
**Suggested direction:**
- Bind limits and lockout from configuration (`RateLimiting:` and `Identity:Lockout` sections); extract a `PerIpFixedWindow(int permitLimit)` helper if a third policy appears.
- Split the policies if needed (e.g. Login 5/min, Register 3/hour).
- Notify the user out-of-band (email) when their account locks, rather than revealing it in the API; consider escalating lockout (5 min, then 15, then 1 h) via a custom `UserManager` if the API ever guards anything valuable.
- The warnings now logged (`Rate limit hit by…`, `Login refused for locked-out user…`) are the data to base these decisions on.

### 18. New endpoints are public by default; role rules are repeated strings — **RESOLVED 2026-10-15**
**Resolution:** `Program.cs` now has `AddAuthorization` with a `FallbackPolicy` that requires an authenticated user, so an endpoint with no authorization metadata fails closed (401). `[AllowAnonymous]` is on the `Register` and `Login` actions only. The role rules are the named policies `CanRead` (Reader, Writer, Admin), `CanWrite` (Writer, Admin) and `CanAdmin` (Admin) in `Constants/PolicyNames.cs`, and every controller attribute uses `[Authorize(Policy = …)]`. Behaviour change: `CanRead` includes Writer. See [ADR-0009](./decisions.md#adr-0009--fail-closed-authorization-and-named-policies). The text below describes the state when the finding was written.
**Files:** every controller; `NZWalks.API/Program.cs` (no `AddAuthorization` policies, no `FallbackPolicy`)
**Current behavior:**
- Authorization is opt-in: an action with no `[Authorize]` is anonymous. Finding #8 existed only because someone forgot the attribute.
- The role split is written as attribute strings on 12 actions across three controllers. Walks, Images and Users use `RoleNames` constants; `RegionsController` still uses the literals `"Reader,Admin"` / `"Writer,Admin"`.
- Reads require `Reader` or `Admin`, so a user holding only `Writer` can change walks and regions but not read them. Rare today (Register always grants `Reader`, and RevokeRole refuses to remove a user's last role) but possible if an Admin revokes `Reader` from a Writer.
**Why it matters:** the next controller ships open unless its author remembers; a typo in a literal role string silently locks everyone out of that action; changing who may read means editing every read action.
**Suggested direction:** set `FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build()` so endpoints fail closed, with `[AllowAnonymous]` on Register and Login. Define named policies (`CanRead` = Reader/Writer/Admin, `CanWrite` = Writer/Admin) in `AddAuthorization` and use `[Authorize(Policy = …)]`. Switch `RegionsController` to the constants or the policies.

### 19. Image upload accepts SVG and stores files under the client's name — **OPEN**
**Files:** `NZWalks.API/Validators/ImageUploadDtoValidator.cs`, `NZWalks.API/Respositries/LocalImageRepository.cs`, `NZWalks.API/Program.cs` (`UseStaticFiles` for `/Images`)
**Current behavior:** `.svg` is an allowed extension, and uploads are served back publicly from `/Images` with no `Content-Security-Policy` or `X-Content-Type-Options` header. Files are saved as `{Name}{extension}` from the request.
**Why it matters:** an SVG can carry script; opened directly from `/Images` it runs on the API's origin (stored XSS). Upload is now Writer-only (#8), which narrows who can plant one, but doesn't remove the risk. Two uploads with the same `Name` overwrite each other, and predictable names let anyone enumerate files.
**Suggested direction:** drop `.svg` (or sanitise it), serve `/Images` with `Content-Security-Policy: default-src 'none'` and `X-Content-Type-Options: nosniff`, and store files under a generated id (`{Guid}{ext}`) with the display name kept only in the database. Do it together with the Blob Storage move (#10), since both change how files are named and served.

### 20. No `.editorconfig` / `.gitattributes`; 36 nullable warnings — **RESOLVED 2026-10-15 (Week 2 A1 + A2)**
**Resolved (A2):** `dotnet build` now reports **0 warnings**. 31 non-nullable strings on DTOs and domain models got `= string.Empty` (validators' `NotEmpty()` rejects `""` and `null` identically, and Swagger is unchanged); the four non-string properties (`File` on `Image` and `ImageUploadDto`, `Difficulty` and `Region` on `WalkDto`) got `= null!` with a comment saying who fills them; the one `CS8602` in `LocalImageRepository` now throws a clear `InvalidOperationException` when there is no `HttpContext`. `.claude/hooks/warning-baseline.txt` is empty, so the Stop hook fails on any warning.
**Resolved (A1):** `.editorconfig` (whitespace and layout rules only, CRLF, no `charset`) and `.gitattributes` (`* text=auto`, CRLF working copies for source file types) are at the repo root, and one formatting-only commit ran `dotnet format whitespace` over the solution. `dotnet format whitespace NZWalks/NZWalks.sln --verify-no-changes` exits 0, and the format hook is now active. The bullets below describe the state before A1 and A2.
**Files:** repo root (absence), `NZWalks/NZWalks.API/Models/DTO/*` and other models (CS8618)
**Current behavior:**
- There is no `.editorconfig`, so `dotnet format` applies its built-in defaults; there is no `.gitattributes`, so line endings depend on each machine (git already warns "LF will be replaced by CRLF" on files edited here).
- `dotnet build` reports 36 warnings: 35 × CS8618 (non-nullable properties not initialised — mostly DTOs and domain models) and 1 × CS8602 (possible null dereference). The plans have been holding the line at "no more than 36".
**Why it matters:** Week 2 adds hooks that run `dotnet format` and analyzers after every edit. Without a committed style, the first run reformats whole files, and every later diff mixes real changes with formatting churn; without fixed line endings, the same file can flip between machines. A hook that fails on warnings would fail on all 36 from day one.
**Suggested direction:** before wiring the hooks, add a `.editorconfig` that matches the current code style and a `.gitattributes` (`* text=auto`, `*.cs eol=crlf`), run `dotnet format` once and commit that as a formatting-only commit. Then either fix the 36 warnings (`required` members or `= default!`/`string.Empty` on DTOs) or make the hook fail only on *new* warnings. See [ADR-0008](./decisions.md#adr-0008--week-2-hooks-what-runs-when-and-what-fails).

### 21. Nothing enforces that `[AllowAnonymous]` stays on Register and Login only — **OPEN (Low)**
**Files:** `NZWalks.API/Controllers/AuthController.cs`, `NZWalks.API/Program.cs` (the fallback policy)
**Current behavior:** the fail-closed rule ([ADR-0009](./decisions.md#adr-0009--fail-closed-authorization-and-named-policies)) makes every endpoint require a login unless it carries `[AllowAnonymous]`, and only `Register` and `Login` do. Nothing in the code or the build stops someone adding a second `[AllowAnonymous]`, or putting it on a whole controller; the rule is held by convention, by `CLAUDE.md`, and by the reviewer subagent (ADR-0010).
**Why it matters:** an unintended public endpoint is the exact failure the fallback policy exists to prevent (finding #8, finding #18). A review catches it only if the review happens and the reviewer notices.
**Suggested direction:** once a test project exists (Week 3), add a test that lists every endpoint carrying `[AllowAnonymous]` metadata and fails unless the set is exactly `Register` and `Login`. Raised by the reviewer's dry run on the A3 commit and recorded here on the user's confirmation, 2026-10-18.

## Summary table

| # | Category | Severity | File(s) |
|---|---|---|---|
| 1 | Bug | Medium — **resolved** | `Middlewares/ExceptionHandlerMiddleware.cs` |
| 2 | Bug | Low — **resolved** | `Middlewares/ExceptionHandlerMiddleware.cs` |
| 3 | Bug | Cosmetic — **resolved** | `Middlewares/ExceptionHandlerMiddleware.cs` |
| 4 | Security/Config | Medium (blocking, once a frontend exists) | `Program.cs` |
| 5 | Naming | Low (grows costlier over time) | `Respositries/` |
| 6 | Naming | Low | `Models/Pagination Result/` |
| 7 | Structural | High (design decision, not a quick fix) | whole solution |
| 8 | Security | High — **resolved 2026-10-13** | `WalksController.cs`, `ImagesController.cs` |
| 9 | Bug | Medium — **resolved 2026-10-08** | `Controllers/AuthController.cs` |
| 10 | Structural | High (only matters once scaling horizontally) | `Respositries/LocalImageRepository.cs` |
| 11 | Config | Low — **resolved 2026-10-08** | `appsettings.json`, `Respositries/TokenRepository.cs` |
| 12 | Bug | Medium — **resolved 2026-10-11** | `Controllers/RegionsController.cs` |
| 13 | Bug | Medium — **resolved 2026-10-11** | `WalksController.cs`, `ImagesController.cs`, `ExceptionHandlerMiddleware.cs` |
| 14 | Security/Config | High (blocks the Week 4 deploy) | `Program.cs` |
| 15 | Structural | Low (accepted trade-off) | `Program.cs` |
| 16 | Structural | Medium (only matters once scaled out) | `Program.cs` |
| 17 | Security/Config | Low | `Program.cs`, `Controllers/AuthController.cs` |
| 18 | Security | Medium — **resolved 2026-10-15** | all controllers, `Program.cs` |
| 19 | Security | Medium | `Validators/ImageUploadDtoValidator.cs`, `Respositries/LocalImageRepository.cs`, `Program.cs` |
| 20 | Tooling | Medium — **resolved 2026-10-15** (style baseline A1, 0 warnings A2) | repo root, `Models/DTO/*` |
| 21 | Security | Low | `Controllers/AuthController.cs`, `Program.cs` |
