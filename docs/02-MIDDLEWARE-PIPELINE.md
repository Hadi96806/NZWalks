# 02 — Program.cs & Middleware Pipeline

`NZWalks.API/Program.cs` is the composition root: it has two distinct halves, in this order:

1. **DI container configuration** — `builder.Services.Add...` calls, which register *what exists* (but don't run yet).
2. **HTTP pipeline configuration** — `app.Use...` / `app.Map...` calls, which define *what runs, in what order,* for every incoming request.

Both halves are documented below in the exact order they appear in the file, followed by a concrete request walkthrough.

## 1. DI container configuration (`builder.Services.Add...`)

| # | Call | What it does | Why it's here |
|---|---|---|---|
| — | `LoggerConfiguration()` → `builder.Logging.ClearProviders()` + `AddSerilog(logger)` | Serilog sinks: Console + daily-rolling file (`Logs/NZWalksLog.txt`), minimum level Information | Runs *before* `builder.Services.Add...` because logging needs to be wired into the host before anything else starts logging during startup |
| — | `RequireSecret(...)` for both connection strings, and a check that `Jwt:ExpiryMinutes` > 0 | Fails at startup, naming the missing key | Secrets live in user-secrets (ADR-0003); a missing one should stop the app, not surface later as a confusing runtime error |
| 1 | `AddControllers(o => o.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true)` | Registers MVC controller support | Baseline. The option stops MVC adding an implicit "required" error for non-nullable strings, so FluentValidation alone owns required-field messages (ADR-0001) |
| 1a | `AddProblemDetails()` | Registers `IProblemDetailsService`; empty error responses get an RFC 7807 body | Used by `UseStatusCodePages`, `NotFound()`/`Problem()` and the rate limiter's 429 writer |
| 1b | `AddValidatorsFromAssemblyContaining<Program>()` + `AddFluentValidationAutoValidation()`; `ValidatorOptions.Global.DefaultRuleLevelCascadeMode = CascadeMode.Stop` | Registers every validator in `Validators/` and runs it before each action; one message per field | Failures land in `ModelState`, so `[ApiController]` returns the usual 400 `ValidationProblemDetails` (ADR-0001) |
| 2 | `AddApiVersioning(...).AddMvc().AddApiExplorer(...)` | Default version 1.0, assumes default when unspecified, reports supported versions in response headers, groups Swagger docs as `'v'VVV` | Needed before Swagger generation, since Swagger needs to know about versions to generate one doc per version |
| 3 | `AddHttpContextAccessor()` | Registers `IHttpContextAccessor` | Required by `LocalImageRepository` to build the public URL of an uploaded image outside of a controller context |
| 4 | `AddEndpointsApiExplorer()` | Minimal-API-style endpoint metadata for Swagger | Paired with `AddSwaggerGen` |
| 5 | `AddSwaggerGen(...)` | Adds a JWT Bearer security definition + requirement so Swagger UI can send `Authorization: Bearer <token>` | Lets Swagger UI test authenticated endpoints directly |
| 6 | `ConfigureOptions<ConfigureSwaggerOptions>()` | Registers the class that creates one Swagger doc per discovered API version | Depends on `AddApiVersioning` having run first |
| 7 | `AddDbContext<NZWalksDbContext>(...)` | SQL Server via `NZWalksConnectionString` | Main data (Regions, Walks, Difficulties, Images) |
| 8 | `AddDbContext<NZWalksAuthDbContext>(...)` | SQL Server via `NZWalksAuthConnectionString` | Separate database for Identity (users/roles), isolated from app data |
| 9–12 | `AddScoped<IRegionRepository, SQLRegionRepository>()`, `AddScoped<IWalkRepository, SQLWalkRepository>()`, `AddScoped<ITokenRepository, TokenRepository>()`, `AddScoped<IImageRepository, LocalImageRepository>()` | Wires each repository interface to its concrete implementation | `Scoped` matches the DbContext lifetime — one instance per HTTP request |
| 13 | `AddAutoMapper(cfg => cfg.AddProfile<AutoMapperProfiles>())` | Registers the single mapping profile | Used by controllers via `IMapper` |
| 14 | `AddIdentityCore<IdentityUser>().AddRoles<IdentityRole>().AddTokenProvider<DataProtectorTokenProvider<IdentityUser>>("NZWalks").AddEntityFrameworkStores<NZWalksAuthDbContext>().AddSignInManager().AddDefaultTokenProviders()` | Registers ASP.NET Core Identity (users + roles), backed by `NZWalksAuthDbContext`. `AddSignInManager()` is only for the lockout-aware `CheckPasswordSignInAsync` in Login — no cookie scheme is registered | Needed before `AddAuthentication`, since the JWT bearer scheme validates against Identity-issued claims |
| 15 | `Configure<IdentityOptions>(...)` | Password policy: non-alphanumeric required, digit required, min length 8, min unique chars 1. Lockout: 5 consecutive failures lock the account for 5 minutes, enabled for new users | Password rules govern `UserManager.CreateAsync` in Register; lockout governs `CheckPasswordSignInAsync` in Login (ADR-0006) |
| 16 | `AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(...)` | Sets JWT Bearer as the default auth scheme; configures `TokenValidationParameters` (validate issuer/audience/lifetime/signing key, using `Jwt:Issuer`/`Jwt:Audience`/`Jwt:Key`; HS256 only; `ClockSkew = TimeSpan.Zero`) | Depends on Identity being registered above (ADR-0005) |
| 16a | `AddAuthorization(options => …)` | Named policies `CanRead` (Reader, Writer, Admin), `CanWrite` (Writer, Admin) and `CanAdmin` (Admin), plus a `FallbackPolicy` requiring an authenticated user, so an endpoint with no attribute fails closed (ADR-0009) | Names live in `Constants/PolicyNames.cs`; `[AllowAnonymous]` only on Register and Login |
| 17 | `AddRateLimiter(...)` | Global fixed window 100 req/min per client IP; named policy `RateLimitPolicies.Auth` 5 req/min per IP; `RejectionStatusCode = 429`; `OnRejected` sets `Retry-After`, logs a warning and writes a problem+json body through `IProblemDetailsService` | Registered here, applied by `UseRateLimiter()` in the pipeline (ADR-0006) |

**Note:** `Jwt:ExpiryMinutes` (15) is read by `TokenRepository` for the token lifetime, and the app refuses to start if it is missing or not positive (finding #11, resolved).

## 2. HTTP pipeline configuration (`app.Use...` / `app.Map...`)

In exact order:

| # | Middleware | What it does | Why it's positioned here |
|---|---|---|---|
| 1 | `if (Environment.IsDevelopment()) { UseSwagger(); UseSwaggerUI(...); }` | Serves `/swagger/{version}/swagger.json` and the Swagger UI, one endpoint per API version | Dev-only — never exposed in production; must run before routing since it serves its own endpoints outside `MapControllers` |
| 2 | `UseMiddleware<ExceptionHandlerMiddleware>()` | Wraps everything downstream in a try/catch; maps `InvalidReferenceException` and FluentValidation's `ValidationException` to a 400 `ValidationProblemDetails`, `RegionInUseException` to 409, `BadHttpRequestException` to its own status, and anything else to a 500 problem+json | Placed as early as possible (right after the dev-only Swagger block) so it can catch exceptions thrown by *every* middleware and controller below it |
| 2a | `UseStatusCodePages()` | Gives empty 4xx/5xx responses (e.g. a bare 401/403/404) a problem+json body | Right after the exception handler, so every error response leaves in the same shape |
| 3 | `UseHttpsRedirection()` | Redirects HTTP → HTTPS | Runs before anything that would otherwise process a plaintext request |
| 4 | `UseStaticFiles(new StaticFileOptions { FileProvider = PhysicalFileProvider(.../Images), RequestPath = "/Images" })` | Serves uploaded images directly from disk at `/Images/*` | Deliberately placed **before** `UseAuthentication`/`UseAuthorization` — uploaded images are publicly viewable without a token, by design (e.g. an `<img src>` tag doesn't carry an `Authorization` header) |
| 4a | `UseRouting()` | Matches the request to an endpoint, so later middleware can read its metadata | Explicit (instead of implicit at the start) so it sits after static files and before the rate limiter, which needs the matched endpoint's `[EnableRateLimiting]` |
| 4b | `UseRateLimiter()` | Applies the global limiter and any named policy on the endpoint; rejects with 429 | After static files (image downloads aren't counted) and **before** authentication, so a flood is turned away before any JWT validation work |
| 5 | `UseAuthentication()` | Reads the `Authorization: Bearer <token>` header, validates it against the `TokenValidationParameters` configured above, and populates `HttpContext.User` if valid | Must run **before** `UseAuthorization` — authorization needs `HttpContext.User` to already be populated to evaluate role checks |
| 6 | `UseAuthorization()` | Evaluates the matched endpoint's `[Authorize(Policy = …)]` / `[AllowAnonymous]` metadata, or the fallback policy (authenticated user) when it has none, against `HttpContext.User` | Must run **after** `UseAuthentication` and **before** the request reaches a controller action |
| 7 | `MapControllers()` | Maps controller actions as endpoints (matched earlier by `UseRouting`) and executes the selected action | Last — by this point the request has passed through exception wrapping, HTTPS enforcement, static file short-circuit (if applicable), rate limiting, authentication, and authorization |
| 8 | `Run()` | Starts the Kestrel server | — |

**Notable gap:** there is no `AddCors()` / `UseCors()` anywhere in the pipeline — see finding #4 in the tech-debt doc.

## 3. Request traversal — worked example

Example request: `POST /api/Walks` with header `Authorization: Bearer <jwt>`.

```mermaid
sequenceDiagram
    participant C as Client
    participant K as Kestrel
    participant EX as ExceptionHandlerMiddleware
    participant HR as HttpsRedirection
    participant SF as StaticFiles (/Images)
    participant RL as Routing + RateLimiter
    participant AN as Authentication
    participant AZ as Authorization
    participant R as Routing / WalksController
    participant DB as SQLWalkRepository + DbContext

    C->>K: POST /api/Walks (Bearer token, JSON body)
    K->>EX: enter pipeline
    EX->>HR: try { next() }
    HR->>SF: already HTTPS, pass through
    SF->>SF: path is /api/Walks, not /Images -> pass through
    SF->>RL: pass through
    RL->>RL: match WalksController.Create; under 100/min for this IP
    RL->>AN: permit granted
    AN->>AN: validate JWT signature, issuer, audience, lifetime
    AN->>AZ: HttpContext.User populated
    AZ->>AZ: check [Authorize(Policy = CanWrite)] (Writer or Admin) against the token's role claims
    AZ->>R: authorized, run WalksController.Create
    R->>R: FluentValidation validates AddWalkDto (invalid → 400 ProblemDetails)
    R->>DB: IWalkRepository.CreateAsync(walk)
    DB-->>R: created Walk entity
    R->>R: IMapper maps Walk -> WalkDto
    R-->>AZ: 201 Created + WalkDto
    AZ-->>AN: response bubbles back up
    AN-->>RL: response bubbles back up
    RL-->>SF: response bubbles back up
    SF-->>HR: response bubbles back up
    HR-->>EX: response bubbles back up (no exception thrown)
    EX-->>K: response bubbles back up
    K-->>C: 201 Created (WalkDto JSON)
```

### Step-by-step narrative

1. **Kestrel** accepts the TCP connection and hands the request to the ASP.NET Core middleware pipeline.
2. **`ExceptionHandlerMiddleware`** wraps everything that follows in a try/catch. It calls `next()` and waits — if anything downstream throws, control returns here (see failure path below).
3. **`UseHttpsRedirection`** confirms the request is already HTTPS and passes through unchanged.
4. **`UseStaticFiles`** checks whether the request path starts with `/Images`. It doesn't (`/api/Walks`), so this middleware is a no-op pass-through.
4a. **`UseRouting`** matches the request to `WalksController.Create`; **`UseRateLimiter`** takes a permit from this IP's global 100/min window (the action has no named policy). If the window is used up, the request ends here with a 429 and `Retry-After`.
5. **`UseAuthentication`** reads the `Authorization` header, validates the JWT (issuer, audience, lifetime, signing key against `Jwt:Key`), and — if valid — populates `HttpContext.User` with the claims embedded in the token (email + role, per `TokenRepository`).
6. **`UseAuthorization`** checks the endpoint's `[Authorize(Policy = PolicyNames.CanWrite)]` (Writer or Admin) against `HttpContext.User`: no valid token → 401, a token without Writer or Admin → 403 (ADR-0007, ADR-0009). An endpoint with no attribute at all gets the fallback policy, so it is 401 without a token too.
7. **`MapControllers`** runs `WalksController.Create`. FluentValidation's auto-validation (`AddWalkDtoValidator`) runs during model binding and records any failures in `ModelState`; `[ApiController]`'s built-in filter then short-circuits with a `400` `ValidationProblemDetails` (`application/problem+json`) if the `AddWalkDto` is invalid.
8. The controller calls `IWalkRepository.CreateAsync`, which `SQLWalkRepository` fulfills against `NZWalksDbContext`.
9. `IMapper` maps the resulting `Walk` entity to a `WalkDto`, and the controller returns `201 Created`.
10. The response bubbles back up through the same middleware stack in reverse — each middleware gets a chance to inspect/modify the outgoing response — until Kestrel sends it to the client.

### Failure paths

- **Missing/invalid/expired JWT:** `UseAuthentication` simply fails to populate `HttpContext.User` (or populates it as unauthenticated) — the request still proceeds to `UseAuthorization`. Authorization then rejects it with `401 Unauthorized` (no token) or `403 Forbidden` (valid token, wrong role) *before* the controller action ever runs; `UseStatusCodePages` gives the empty response a problem+json body.
- **Too many requests:** `UseRateLimiter` ends the request with `429 Too Many Requests`, a `Retry-After` header and a problem+json body, before authentication runs. Login and register hit this after 5 calls per minute per IP.
- **Controller throws an unhandled exception** (e.g. a database connectivity failure in `SQLWalkRepository`): the exception propagates up through routing, authorization, authentication, and static files, and is caught by `ExceptionHandlerMiddleware`'s `catch` block. It logs the exception with a fresh `errorId` and the `traceId`, and writes a `500` problem+json response carrying both (finding #1, resolved). In Development the `detail` is the exception message; otherwise a generic message.
- **`ModelState` invalid** (e.g. missing required field on `AddWalkDto`): the `[ApiController]` model-state filter short-circuits with a `400` `ValidationProblemDetails` (listing each failed field) before the repository is ever called — this happens *inside* the controller/action-filter stage, after all pipeline middleware has already passed the request through.

## 4. JWT / Identity configuration summary

- **Scheme:** `JwtBearerDefaults.AuthenticationScheme` ("Bearer") is the sole/default authentication scheme.
- **Token validation:** issuer, audience, lifetime, and signing key are all validated (`ValidateIssuer/Audience/Lifetime/IssuerSigningKey = true`), sourced from `Jwt:Issuer`, `Jwt:Audience`, `Jwt:Key` in configuration.
- **Password policy:** non-alphanumeric required, digit required, minimum length 8, minimum unique characters 1 — enforced by `IdentityOptions` against `UserManager.CreateAsync`.
- **Role seeding:** roles are **not** seeded at runtime by a `DbInitializer`/seeder class. They're inserted via an EF Core migration (`Migrations/NZWalksAuthDb/..._Creating Auth Db.cs`), which applies `HasData` on fixed GUIDs for `Reader`, `Writer`, `Admin` roles defined in `NZWalksAuthDbContext.OnModelCreating`. This means the roles exist in the database the moment `dotnet ef database update` is run — there is no code path that creates them lazily at app startup.
- **Role assignment:** Register always assigns `Reader` and rejects a body containing `roles` (ADR-0004). Further roles are granted or revoked only by an Admin through `UsersController`; the first Admin comes from SQL ([05-RUNBOOK-FIRST-ADMIN.md](./05-RUNBOOK-FIRST-ADMIN.md)).
- **Login:** `SignInManager.CheckPasswordSignInAsync(..., lockoutOnFailure: true)`. Unknown email, wrong password, locked-out account and a user with no roles all get the same generic 401; lockouts are logged (ADR-0006).
- **Tokens:** 15-minute lifetime from `Jwt:ExpiryMinutes`, `ClockSkew` zero, HS256, claims `sub`/`jti`/`iat` plus email and roles (ADR-0005).
