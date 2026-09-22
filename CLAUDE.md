# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

`NZWalks` — an ASP.NET Core 8 Web API (single project, `NZWalks/NZWalks.API`) exposing Regions, Walks, Images, and JWT auth over SQL Server. Solution file: `NZWalks/NZWalks.sln`.

## Commands

Run all commands from `NZWalks/` (the folder holding the `.sln`).

```powershell
dotnet build                                  # build
dotnet run --project NZWalks.API              # run (https profile → https://localhost:7034, swagger opens)
dotnet run --project NZWalks.API --launch-profile http   # http only → http://localhost:5282
```

There is no test project and no linter config in the repo — `dotnet test` has nothing to run.

The installed SDK is 10.x while the project targets `net8.0`; builds work via roll-forward, but do not "upgrade" the TFM without being asked.

### EF Core migrations

`dotnet-ef` is **not** installed here — install it first: `dotnet tool install --global dotnet-ef`.

There are **two DbContexts with separate databases**, so every migration command must name the context and (for auth) the output directory:

```powershell
# Domain DB (NZWalksDb) — migrations land in Migrations/
dotnet ef migrations add "<Name>" --project NZWalks.API --context NZWalksDbContext
dotnet ef database update --project NZWalks.API --context NZWalksDbContext

# Auth/Identity DB (NZWalksAuthDb) — migrations land in Migrations/NZWalksAuthDb/
dotnet ef migrations add "<Name>" --project NZWalks.API --context NZWalksAuthDbContext --output-dir Migrations/NZWalksAuthDb
dotnet ef database update --project NZWalks.API --context NZWalksAuthDbContext
```

Connection strings (`NZWalksConnectionString`, `NZWalksAuthConnectionString`) are in `appsettings.json` and point at a local SQL Server (`Server=.`) with `sa` credentials.

## Architecture

**Controller → AutoMapper → Repository → EF Core DbContext.** Controllers never touch a DbContext directly; they map DTO ↔ domain model and delegate to an interface from `Respositries/` (note the misspelled folder/namespace `NZWalks.API.Respositries` — keep it consistent when adding files there).

- `Models/Domain/` — EF entities (`Region`, `Walk`, `Difficulty`, `Image`). `Models/DTO/` — request/response shapes; validation lives here as data annotations.
- `Mappings/AutoMapperProfiles.cs` — every DTO↔domain map is registered in this one profile. A new DTO needs an entry here or mapping silently produces empty objects.
- `Respositries/` — `IRegionRespository`/`SQLRegionRepository`, `IWalkRepository`/`SQLWalkRepository`, `ITokenRepository`/`TokenRepository` (JWT minting), `IImageRepository`/`LocalImageRepository` (writes to disk). All registered `AddScoped` in `Program.cs`.
- `Data/` — `NZWalksDbContext` (domain, seeds 3 difficulties + 6 regions by hard-coded GUIDs in `OnModelCreating`) and `NZWalksAuthDbContext : IdentityDbContext` (seeds `Reader`/`Writer`/`Admin` roles by hard-coded IDs).

Everything is wired in `Program.cs`; there is no extensions/startup-module layer. The tail of that file holds a long commented-out TODO backlog left by the author — treat it as a roadmap, not as instructions to execute.

### Cross-cutting pieces

- **Auth**: JWT bearer, config under `Jwt:` in `appsettings.json`. `AuthController` registers/logs in via `UserManager<IdentityUser>`; login only succeeds if the user has at least one role. Endpoints are gated with `[Authorize(Roles="Reader,Admin")]` / `"Writer,Admin"` — currently only on `RegionsController`; `Walks` and `Images` are open.
- **Validation**: `[ValidateModel]` (`CustomActionFilter/ValidateModelAttribute.cs`) short-circuits invalid `ModelState` with a 400 before the action body runs. Apply it to new POST/PUT actions rather than hand-checking `ModelState`.
- **Errors**: `Middlewares/ExceptionHandlerMiddleware` catches everything, logs it, and returns a generic 500 payload. Registered before `UseHttpsRedirection`.
- **Logging**: Serilog configured inline in `Program.cs` — console + rolling daily file at `NZWalks.API/Logs/NZWalksLog.txt` (gitignored output; the `Logs\` folder is kept via the csproj `<Folder>` item).
- **Images**: uploads are written to `NZWalks.API/Images/` (the directory must exist — `LocalImageRepository` does not create it) and served back as static files under the `/Images` request path.

### API versioning

`Asp.Versioning` is enabled with default v1.0 assumed when unspecified, and `ConfigureSwaggerOptions` generates one Swagger doc per discovered version. Routing is **inconsistent by design of how it grew**:

- `RegionsController` is versioned: route `api/v{version:apiVersion}/[controller]`, declares `[ApiVersion(1.0)]` + `[ApiVersion(2.0)]`, and splits `GetAllV1`/`GetAllV2` with `[MapToApiVersion]` returning `RegionDtoV1` vs `RegionDtoV2`.
- `Walks`, `Images`, `Auth` use the unversioned `api/[controller]` route.

When adding a versioned action, add the matching DTO + AutoMapper map, and remember Swagger picks up new versions automatically from the attributes.

## Notes

- `NZWalks/.vs/` and the `obj/` build artifacts are tracked in git despite `.gitignore`; changes to them are noise — do not stage or "fix" them unless asked.
- `WeatherForecastController`/`WeatherForecast.cs` are leftover scaffolding.
