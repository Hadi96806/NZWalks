# 03 — Findings & Tech Debt

This is a snapshot of real issues found while mapping the current codebase (see [01-PROJECT-STRUCTURE.md](./01-PROJECT-STRUCTURE.md) and [02-MIDDLEWARE-PIPELINE.md](./02-MIDDLEWARE-PIPELINE.md)). None of these are production incidents — the app isn't in production yet — but they should be addressed **before or while** scaling up, since several of them get more expensive to fix the longer they're left (naming, project layout) and others are silent correctness bugs a new feature could easily trip over (the error-id bug, the role-assignment bug).

Nothing in this document has been fixed yet — it's a punch list for upcoming work, not a changelog.

## Bugs

### 1. `ExceptionHandlerMiddleware` error id is always `Guid.Empty`
**File:** `NZWalks.API/Middlewares/ExceptionHandlerMiddleware.cs`
**Current behavior:** the error id is generated with `new Guid()` — the parameterless constructor, which always produces `00000000-0000-0000-0000-000000000000`, not a random value.
**Why it matters:** the whole point of returning an error id to the client is so they can quote it back for support/debugging and you can grep logs for that exact id. Right now every single error response has the *same* id, so it's useless as a correlation key.
**Suggested direction:** change to `Guid.NewGuid()`.

### 2. Response `ContentType` typo: `"Aplication/json"`
**File:** `NZWalks.API/Middlewares/ExceptionHandlerMiddleware.cs`
**Current behavior:** the 500 response sets `ContentType = "Aplication/json"` (missing a "p").
**Why it matters:** some HTTP clients / API gateways sniff or validate the `Content-Type` header; a misspelled MIME type can cause clients to fail to parse the body as JSON even though the body itself is valid JSON.
**Suggested direction:** fix to `"application/json"` (or just use `WriteAsJsonAsync`, which sets this correctly on its own — it's already being called, so the explicit `ContentType` assignment may be redundant).

### 3. Error message typo: "adminstrator"
**File:** `NZWalks.API/Middlewares/ExceptionHandlerMiddleware.cs`
**Current behavior:** the client-facing error message reads "Something went wrong, please contact adminstrator".
**Why it matters:** cosmetic, but it's user (developer/support)-facing text that will get quoted in bug reports and screenshots.
**Suggested direction:** fix the spelling; consider whether this message should be configurable/localizable long-term.

### 9. `AuthController.Register` silently drops all but the first role
**File:** `NZWalks.API/Controllers/AuthController.cs`
**Current behavior:** `registerRequestDto.Roles` accepts a list, but only `Roles[0]` is ever passed to `userManager.AddToRoleAsync`.
**Why it matters:** if a client submits `["Writer", "Admin"]` expecting both roles, `Admin` (or whichever isn't index 0) is silently discarded with no error — the caller has no way to know the request was only partially honored.
**Suggested direction:** loop over `Roles` and call `AddToRoleAsync` for each, or use `AddToRolesAsync(user, roles)`.

## Security / Config Gaps

### 4. No CORS policy configured
**Files:** `NZWalks.API/Program.cs` (absence of `AddCors`/`UseCors`)
**Current behavior:** there is no CORS configuration anywhere in the project.
**Why it matters:** as soon as a browser-based frontend (a separate origin — different port/host) tries to call this API directly from JavaScript, every request will be blocked by the browser's same-origin policy. This is invisible today because nothing browser-based is calling the API yet.
**Suggested direction:** decide the allowed origin(s) for the eventual frontend and add an explicit named CORS policy (avoid `AllowAnyOrigin()` combined with credentials/auth headers).

### 8. `WalksController` and `ImagesController` have no role-based authorization
**Files:** `NZWalks.API/Controllers/WalksController.cs`, `NZWalks.API/Controllers/ImagesController.cs`
**Current behavior:** `RegionsController` consistently applies `[Authorize(Roles="Reader,Admin")]` (reads) / `[Authorize(Roles="Writer,Admin")]` (writes). `WalksController` and `ImagesController` have no `[Authorize]` attributes at all — every action on them is currently reachable anonymously.
**Why it matters:** this looks like an oversight rather than an intentional public API surface, given the pattern established on `RegionsController`. As-is, anyone can create/update/delete walks or upload images without authenticating.
**Suggested direction:** decide the intended access policy per action (likely mirroring `RegionsController`'s Reader/Writer/Admin split) and apply it consistently.

### 11. `Jwt:ExpiryMinutes` is configured but never read
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

## Summary table

| # | Category | Severity | File(s) |
|---|---|---|---|
| 1 | Bug | Medium | `Middlewares/ExceptionHandlerMiddleware.cs` |
| 2 | Bug | Low | `Middlewares/ExceptionHandlerMiddleware.cs` |
| 3 | Bug | Cosmetic | `Middlewares/ExceptionHandlerMiddleware.cs` |
| 4 | Security/Config | Medium (blocking, once a frontend exists) | `Program.cs` |
| 5 | Naming | Low (grows costlier over time) | `Respositries/` |
| 6 | Naming | Low | `Models/Pagination Result/` |
| 7 | Structural | High (design decision, not a quick fix) | whole solution |
| 8 | Security | High | `WalksController.cs`, `ImagesController.cs` |
| 9 | Bug | Medium | `Controllers/AuthController.cs` |
| 10 | Structural | High (only matters once scaling horizontally) | `Respositries/LocalImageRepository.cs` |
| 11 | Config | Low | `appsettings.json`, `Respositries/TokenRepository.cs` |
