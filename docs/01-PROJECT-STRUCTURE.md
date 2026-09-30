# 01 — Project Structure

This document maps the current folder/file structure of the NZWalks solution exactly as it exists today, explains what each piece is for, and argues why the structure is organized this way. It intentionally documents reality, including a few naming quirks — those are tracked separately in [03-FINDINGS-AND-TECH-DEBT.md](./03-FINDINGS-AND-TECH-DEBT.md) rather than silently "corrected" here.

## 1. Solution layout

```
NZ Walks/                              (git repo root)
└── NZWalks/                           (solution folder)
    ├── NZWalks.sln
    └── NZWalks.API/                   (the only project — net8.0 Web API)
```

There is currently **one project** in the solution. There is no separate `NZWalks.Domain`, `NZWalks.Data`, or `NZWalks.Repository` class library — every concern (controllers, EF Core contexts, repositories, DTOs, mapping profiles, middleware) lives inside `NZWalks.API`. This is a deliberate callout for scaling: see finding #7 in the tech-debt doc.

## 2. Full folder tree — `NZWalks.API/`

```
NZWalks.API/
├── Program.cs                     # Composition root: DI registration + HTTP pipeline (see 02-MIDDLEWARE-PIPELINE.md)
├── ConfigureSwaggerOptions.cs      # Wires up one Swagger doc per API version
├── WeatherForecast.cs              # Leftover from the dotnet new webapi template — unused
├── NZWalks.API.csproj
├── appsettings.json                 # ConnectionStrings, Jwt:{Key,Issuer,Audience,ExpiryMinutes}, Logging
├── appsettings.Development.json     # Dev-only Logging overrides
│
├── Controllers/                    # HTTP entry points — one controller per resource
│   ├── AuthController.cs           # api/Auth — Register / Login
│   ├── ImagesController.cs         # api/Images — Upload
│   ├── RegionsController.cs        # api/v{version}/Regions — versioned CRUD (v1.0 & v2.0)
│   ├── WalksController.cs          # api/Walks — filtered/sorted/paginated CRUD
│   └── WeatherForecastController.cs # template leftover, unused
│
├── CustomActionFilter/
│   └── ValidateModelAttribute.cs   # ActionFilterAttribute — short-circuits to 400 on invalid ModelState
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
│   │   └── RegisterRequestDto.cs
│   └── Pagination Result/          # Paging wrappers (namespace: Pagination_Result — see findings doc)
│       ├── WalkPageResult.cs       # wraps domain Walk list + paging metadata
│       └── WalkPageDtoResult.cs    # wraps WalkDto list + paging metadata
│
├── Properties/
│   └── launchSettings.json
│
└── Respositries/                   # Repository pattern — sits between controllers and DbContexts
    ├── IImageRepository.cs         → LocalImageRepository.cs
    ├── IRegionRespository.cs       → SQLRegionRepository.cs
    ├── ITokenRepository.cs         → TokenRepository.cs
    └── IWalkRepository.cs          → SQLWalkRepository.cs
```

There is no `wwwroot/` folder — static image serving is configured explicitly against the `Images/` folder in `Program.cs` (see 02-MIDDLEWARE-PIPELINE.md).

## 3. Controllers

| Controller | Route(s) | Actions | Injected dependencies |
|---|---|---|---|
| `AuthController` | `api/Auth` | `POST /Register`, `POST /Login` | `UserManager<IdentityUser>`, `ITokenRepository` |
| `ImagesController` | `api/Images` | `POST /Upload` | `IImageRepository` |
| `RegionsController` | `api/v{version}/Regions` (v1.0, v2.0) | `GetAllV1`, `GetAllV2`, `GetById`, `Create`, `Update`, `Delete` | `IRegionRespository`, `IMapper`, `ILogger<RegionsController>` |
| `WalksController` | `api/Walks` | `GetAll` (filter + sort + pagination), `GetById`, `Create`, `Update`, `Delete` | `IMapper`, `IWalkRepository` |
| `WeatherForecastController` | template default | unused | — |

Controllers are deliberately thin: they validate input (via `[ValidateModel]`), call a repository, map the result with AutoMapper, and return it. No EF Core or SQL appears in a controller file.

## 4. Models: Domain vs DTO

- **Domain** (`Models/Domain/`) mirrors the database schema exactly — these are the types EF Core tracks and persists. They carry no validation attributes and no concern for what a client should see.
- **DTO** (`Models/DTO/`) is the wire contract — what actually goes over HTTP. Naming convention:
  - Requests: `<Verb><Entity>Dto` — `AddRegionDto`, `UpdateRegionDto`, `AddWalkDto`, `UpdateWalkDto`, `RegisterRequestDto`, `LogInRequestDto`, `ImageUploadDto`. These carry `[Required]`, `[MinLength]`, `[MaxLength]`, `[Range]`, `[DataType]` validation attributes.
  - Responses: `<Entity>Dto` (`WalkDto`, `DifficultyDto`) or `<Entity>DtoVn` when a resource is API-versioned (`RegionDtoV1`, `RegionDtoV2` — v2 adds a computed `HasImage` field).

**Why split them:** the database shape and the API shape are allowed to diverge without breaking each other. A column can be renamed, split, or added on the `Region` entity without touching every client that consumes `RegionDtoV1`; conversely, `RegionDtoV2` can evolve (e.g. add `HasImage`) without an EF Core migration. This is also what makes API versioning workable — `RegionDtoV1` and `RegionDtoV2` are two different DTOs mapped from the *same* `Region` entity.

**Pagination Result** (`Models/Pagination Result/`) follows the same Domain/DTO split one level up: `WalkPageResult` wraps a list of domain `Walk` (used internally between repository and controller), `WalkPageDtoResult` wraps a list of `WalkDto` (what the controller actually returns), each carrying paging metadata (page number, page size, total count).

## 5. Repositories

| Interface | Implementation | Responsibility |
|---|---|---|
| `IRegionRespository` | `SQLRegionRepository` | CRUD for `Region` via `NZWalksDbContext` |
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

- **A new developer's first question is "where do I look for X?"** — this layout answers it consistently: HTTP-facing code is always in `Controllers/`, persistence is always in `Data/` + `Respositries/`, wire contracts are always in `Models/DTO/`, and translation between the two is always in `Mappings/`. There's exactly one place each concern lives.
- **Change isolation:** a request-shape change touches `Models/DTO/` and `Mappings/`; a schema change touches `Models/Domain/`, `Data/`, and a new file in `Migrations/`; a query-behavior change touches only `Respositries/`. These rarely overlap, which keeps diffs small and reviewable.
- **What's easy to change today:** DTO shapes, mapping rules, adding a new endpoint/action, adding a new repository method.
- **What's hard to change today** (see [03-FINDINGS-AND-TECH-DEBT.md](./03-FINDINGS-AND-TECH-DEBT.md) for the full list): splitting the project into class libraries after the fact (everything currently references everything else within one assembly), swapping image storage away from local disk (the path is hardcoded relative to content root), and fixing the `Respositries` / `Pagination Result` naming without a rename pass across the whole codebase.
