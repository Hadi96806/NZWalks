# 01 — Project Structure

This document maps the current folder/file structure of the NZWalks solution exactly as it exists today, explains what each piece is for, and argues why the structure is organized this way. It intentionally documents reality, including a few naming quirks — those are tracked separately in [03-FINDINGS-AND-TECH-DEBT.md](./03-FINDINGS-AND-TECH-DEBT.md) rather than silently "corrected" here.

## 1. Solution layout

```
NZ Walks/                              (git repo root)
└── NZWalks/                           (solution folder)
    ├── NZWalks.sln
    └── NZWalks.API/                   (the only project — net10.0 Web API)
```

There is currently **one project** in the solution. There is no separate `NZWalks.Domain`, `NZWalks.Data`, or `NZWalks.Repository` class library — every concern (controllers, EF Core contexts, repositories, DTOs, mapping profiles, middleware) lives inside `NZWalks.API`. This is a deliberate callout for scaling: see finding #7 in the tech-debt doc.

## 2. Full folder tree — `NZWalks.API/`

```
NZWalks.API/
├── Program.cs                     # Composition root: DI registration + HTTP pipeline (see 02-MIDDLEWARE-PIPELINE.md)
├── ConfigureSwaggerOptions.cs      # Wires up one Swagger doc per API version
├── NZWalks.API.csproj               # carries <UserSecretsId> — secrets live in user-secrets, not the repo
├── appsettings.json                 # Jwt:{Issuer,Audience,ExpiryMinutes}, Logging; ConnectionStrings and Jwt:Key are empty placeholders (ADR-0003)
├── appsettings.Development.json     # Dev-only Logging overrides
│
├── Constants/
│   ├── PolicyNames.cs              # CanRead / CanWrite / CanAdmin as consts, for [Authorize(Policy = …)] (ADR-0009)
│   ├── RoleNames.cs                # Reader / Writer / Admin as consts, used by the policies in Program.cs + RoleNames.All
│   └── RateLimitPolicies.cs        # Auth policy name, const so it works in [EnableRateLimiting(…)] (ADR-0006)
│
├── Controllers/                    # HTTP entry points — one controller per resource
│   ├── AuthController.cs           # api/Auth — Register (always Reader) / Login
│   ├── ImagesController.cs         # api/Images — Upload
│   ├── RegionsController.cs        # api/v{version}/Regions — versioned CRUD (v1.0 & v2.0)
│   ├── UsersController.cs          # api/Users — Admin-only GrantRole / RevokeRole (ADR-0004)
│   └── WalksController.cs          # api/Walks — filtered/sorted/paginated CRUD
│
├── Data/                           # EF Core DbContexts — the only place the API talks to SQL Server
│   ├── NZWalksDbContext.cs         # Regions, Walks, Difficulties, Images — seeds Difficulty/Region data
│   └── NZWalksAuthDbContext.cs     # IdentityDbContext — seeds Reader/Writer/Admin roles
│
├── Images/                         # Uploaded image files land here, served back out at /Images/*
│   └── Dot Net.png
│
├── Logs/                           # Serilog daily-rolling file sink output (generated at runtime, not source)
│   └── NZWalksLog*.txt
│
├── Mappings/
│   └── AutoMapperProfiles.cs       # Single profile — every Domain <-> DTO mapping in one place
│
├── Middlewares/
│   └── ExceptionHandlerMiddleware.cs  # Global try/catch around the whole pipeline, returns JSON 500s
│
├── Migrations/                     # EF Core migrations for NZWalksDbContext
│   ├── ..._First Migration.cs
│   ├── ..._Seeding data for difficulties and regions.cs
│   ├── ..._Adding Images table.cs
│   ├── NZWalksDbContextModelSnapshot.cs
│   └── NZWalksAuthDb/               # Separate sub-folder for the second DbContext's migrations
│       ├── ..._Creating Auth Db.cs
│       └── NZWalksAuthDbContextModelSnapshot.cs
│
├── Models/
│   ├── Domain/                     # Plain EF entities — no validation attributes, no wire format concerns
│   │   ├── Difficulty.cs
│   │   ├── Image.cs
│   │   ├── Region.cs
│   │   └── Walk.cs
│   ├── DTO/                        # Request/response shapes — this is what the API actually sends/receives
│   │   ├── AddRegionDto.cs / UpdateRegionDto.cs
│   │   ├── AddWalkDto.cs / UpdateWalkDto.cs / WalkDto.cs
│   │   ├── RegionDto.cs            # contains both RegionDtoV1 and RegionDtoV2
│   │   ├── DifficultyDto.cs
│   │   ├── ImageUploadDto.cs
│   │   ├── LogInRequestDto.cs / LoginResponseDto.cs
│   │   ├── RegisterRequestDto.cs   # email + password only; a "roles" member is rejected (400)
│   │   ├── UserRoleRequestDto.cs   # { email, role } body for GrantRole / RevokeRole
│   │   └── WalkQueryDto.cs         # GET /api/Walks filter, sort and paging query parameters
│   └── PaginationResult/           # Paging wrappers (namespace: NZWalks.API.Models.PaginationResult)
│       ├── WalkPageResult.cs       # wraps domain Walk list + paging metadata
│       └── WalkPageDtoResult.cs    # wraps WalkDto list + paging metadata
│
├── Properties/
│   └── launchSettings.json
│
├── Repositories/                   # Repository pattern — sits between controllers and DbContexts
│   ├── IImageRepository.cs         → LocalImageRepository.cs
│   ├── IRegionRepository.cs        → SQLRegionRepository.cs
│   ├── ITokenRepository.cs         → TokenRepository.cs
│   └── IWalkRepository.cs          → SQLWalkRepository.cs
│
└── Validators/                     # FluentValidation — one validator per request DTO (ADR-0001)
    ├── *Validator.cs               # AddRegionDto, UpdateRegionDto, AddWalkDto, UpdateWalkDto, ImageUploadDto,
    │                               # LogInRequestDto, RegisterRequestDto, UserRoleRequestDto, WalkQueryDto
    ├── AuthRules.cs                # MaxPasswordLength (128) — caps input before the password hasher; used by Login + Register
    └── RuleBuilderExtensions.cs    # MustBeOptionalHttpUrl() — optional image-URL rule (absolute http/https, ≤ 2048 chars)
```

There is no `wwwroot/` folder — static image serving is configured explicitly against the `Images/` folder in `Program.cs` (see 02-MIDDLEWARE-PIPELINE.md).

## 3. Controllers

| Controller | Route(s) | Actions | Injected dependencies |
|---|---|---|---|
| `AuthController` | `api/Auth` (only the `Register` and `Login` actions are `[AllowAnonymous]`; class-level `[EnableRateLimiting(RateLimitPolicies.Auth)]`, 5 req/min per IP) | `POST /Register`, `POST /Login` (lockout-aware) | `UserManager<IdentityUser>`, `SignInManager<IdentityUser>`, `ITokenRepository`, `ILogger<AuthController>` |
| `UsersController` | `api/Users` (class-level `[Authorize(Policy = PolicyNames.CanAdmin)]`) | `POST /GrantRole`, `POST /RevokeRole` | `UserManager<IdentityUser>`, `ILogger<UsersController>` |
| `ImagesController` | `api/Images` (`Upload`: `CanWrite`) | `POST /Upload` | `IImageRepository` |
| `RegionsController` | `api/v{version}/Regions` (v1.0, v2.0; reads `CanRead`, writes `CanWrite`) | `GetAllV1`, `GetAllV2`, `GetById`, `Create`, `Update`, `Delete` | `IRegionRepository`, `IMapper`, `ILogger<RegionsController>` |
| `WalksController` | `api/Walks` (reads `CanRead`, writes `CanWrite` — ADR-0007, ADR-0009) | `GetAll` (filter + sort + pagination), `GetById`, `Create`, `Update`, `Delete` | `IMapper`, `IWalkRepository` |

Controllers are deliberately thin: their request DTOs are validated before the action runs (a FluentValidation validator in `Validators/`, wired by `AddFluentValidationAutoValidation()`), they call a repository, map the result with AutoMapper, and return it. No EF Core or SQL appears in a controller file.

## 4. Models: Domain vs DTO

- **Domain** (`Models/Domain/`) mirrors the database schema exactly — these are the types EF Core tracks and persists. They carry no validation attributes and no concern for what a client should see.
- **DTO** (`Models/DTO/`) is the wire contract — what actually goes over HTTP. Naming convention:
  - Requests: `<Verb><Entity>Dto` — `AddRegionDto`, `UpdateRegionDto`, `AddWalkDto`, `UpdateWalkDto`, `RegisterRequestDto`, `LogInRequestDto`, `ImageUploadDto`. These carry `[Required]`, `[MinLength]`, `[MaxLength]`, `[Range]`, `[DataType]` validation attributes.
  - Responses: `<Entity>Dto` (`WalkDto`, `DifficultyDto`) or `<Entity>DtoVn` when a resource is API-versioned (`RegionDtoV1`, `RegionDtoV2` — v2 adds a computed `HasImage` field).

**Why split them:** the database shape and the API shape are allowed to diverge without breaking each other. A column can be renamed, split, or added on the `Region` entity without touching every client that consumes `RegionDtoV1`; conversely, `RegionDtoV2` can evolve (e.g. add `HasImage`) without an EF Core migration. This is also what makes API versioning workable — `RegionDtoV1` and `RegionDtoV2` are two different DTOs mapped from the *same* `Region` entity.

**PaginationResult** (`Models/PaginationResult/`) follows the same Domain/DTO split one level up: `WalkPageResult` wraps a list of domain `Walk` (used internally between repository and controller), `WalkPageDtoResult` wraps a list of `WalkDto` (what the controller actually returns), each carrying paging metadata (page number, page size, total count).

## 5. Repositories

| Interface | Implementation | Responsibility |
|---|---|---|
| `IRegionRepository` | `SQLRegionRepository` | CRUD for `Region` via `NZWalksDbContext` |
| `IWalkRepository` | `SQLWalkRepository` | CRUD for `Walk`, plus filtering (by name), sorting (name/length, asc/desc), and pagination, eager-loading `Difficulty`/`Region` |
| `IImageRepository` | `LocalImageRepository` | Writes an uploaded file to local disk, builds its public URL, persists an `Image` row |
| `ITokenRepository` | `TokenRepository` | Builds a signed JWT (email + role claims) from `Jwt:Key/Issuer/Audience` |

**Why the repository pattern here:** controllers depend on interfaces (`IWalkRepository`), not on `NZWalksDbContext` or `LocalImageRepository` directly. That means:
- The actual data access technology (SQL Server via EF Core, local disk for images) can be swapped — e.g. `LocalImageRepository` could become `BlobImageRepository` — without touching a single controller.
- Controllers become trivially testable by mocking the interface; no in-memory database or file system needed for controller-level tests.
- Query logic (filtering/sorting/pagination in `SQLWalkRepository`) lives in one place instead of being duplicated per endpoint.

## 6. AutoMapper

`Mappings/AutoMapperProfiles.cs` is a single profile that owns every Domain ↔ DTO mapping:
- `Region ↔ RegionDtoV1`, `Region ↔ UpdateRegionDto`, `Region ↔ AddRegionDto`
- `Region → RegionDtoV2` (custom: `HasImage` computed from whether `RegionImageUrl` is empty), with reverse map
- `Walk ↔ AddWalkDto`, `Walk ↔ WalkDto`, `Walk ↔ UpdateWalkDto`
- `Difficulty ↔ DifficultyDto`
- `WalkPageDtoResult ↔ WalkPageResult`

**Why centralize mapping:** without AutoMapper, every controller action would hand-write `new WalkDto { Name = walk.Name, ... }` style code, which drifts silently when a field is added to `Walk` but a developer forgets one of the six places it's mapped. One profile means one place to update when the shape of `Walk` changes, and it's trivially unit-testable in isolation (`profile.AssertConfigurationIsValid()`).

## 7. Why this structure, overall

- **A new developer's first question is "where do I look for X?"** — this layout answers it consistently: HTTP-facing code is always in `Controllers/`, persistence is always in `Data/` + `Repositories/`, wire contracts are always in `Models/DTO/`, and translation between the two is always in `Mappings/`. There's exactly one place each concern lives.
- **Change isolation:** a request-shape change touches `Models/DTO/` and `Mappings/`; a schema change touches `Models/Domain/`, `Data/`, and a new file in `Migrations/`; a query-behavior change touches only `Repositories/`. These rarely overlap, which keeps diffs small and reviewable.
- **What's easy to change today:** DTO shapes, mapping rules, adding a new endpoint/action, adding a new repository method.
- **What's hard to change today** (see [03-FINDINGS-AND-TECH-DEBT.md](./03-FINDINGS-AND-TECH-DEBT.md) for the full list): splitting the project into class libraries after the fact (everything currently references everything else within one assembly), and swapping image storage away from local disk (the path is hardcoded relative to content root).
