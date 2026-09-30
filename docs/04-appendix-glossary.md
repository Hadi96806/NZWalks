# 04 — Appendix: Glossary

Short definitions of terms used across this documentation set, for anyone reading it as onboarding material.

- **DTO (Data Transfer Object):** a class whose only job is to define the shape of data going over the wire (a request body or a response body). Distinct from a *domain model*, which defines the shape of data in the database. See [01-PROJECT-STRUCTURE.md](./01-PROJECT-STRUCTURE.md), section 4.

- **Domain model:** a class that mirrors a database table/entity, tracked and persisted by EF Core. In this project, these live in `Models/Domain/`.

- **Middleware:** a piece of code that sits in the HTTP request pipeline and gets a chance to inspect/modify every request and response passing through it, before deciding whether to call the next middleware in the chain. Order matters — see [02-MIDDLEWARE-PIPELINE.md](./02-MIDDLEWARE-PIPELINE.md).

- **Repository pattern:** an interface (e.g. `IWalkRepository`) that abstracts away *how* data is fetched/stored, so the code calling it doesn't need to know whether the implementation talks to SQL Server, an in-memory list, or a remote API. See [01-PROJECT-STRUCTURE.md](./01-PROJECT-STRUCTURE.md), section 5.

- **Dependency Injection (DI) / DI container:** the mechanism (`builder.Services.Add...`) that lets a class declare "I need an `IWalkRepository`" in its constructor without knowing or caring which concrete class actually implements it. ASP.NET Core's built-in container resolves this at runtime.

- **Claim:** a single piece of information embedded in a JWT (e.g. the user's email, or a role like "Admin"). `HttpContext.User` is populated with claims once a token is validated by the Authentication middleware.

- **Role-based authorization:** restricting an endpoint to users whose token contains a specific role claim, via `[Authorize(Roles = "...")]`. Different from *authentication*, which only confirms *who* the caller is, not what they're allowed to do.

- **EF Core Migration:** a generated C# file describing a change to the database schema (e.g. "add an Images table"), plus a snapshot of what the full schema looks like after applying it. Running `dotnet ef database update` applies any migrations not yet applied to the target database.

- **AutoMapper Profile:** a class that declares mapping rules between two types (e.g. `Region` ↔ `RegionDtoV1`) in one place, instead of hand-writing property-by-property assignment in every controller action.

- **API Versioning:** allowing multiple versions of the same resource's contract to exist side by side (e.g. `RegionDtoV1` vs `RegionDtoV2`), so existing clients don't break when the API's shape evolves.

- **CORS (Cross-Origin Resource Sharing):** a browser security mechanism that blocks JavaScript running on one origin (e.g. `https://frontend.example.com`) from calling an API on a different origin (e.g. `https://api.example.com`) unless the API explicitly allows it. Not yet configured in this project — see [03-FINDINGS-AND-TECH-DEBT.md](./03-FINDINGS-AND-TECH-DEBT.md), finding #4.

- **Composition root:** the single place in an application where all the pieces (DI registrations, middleware pipeline) are wired together. In this project, that's `Program.cs`.
