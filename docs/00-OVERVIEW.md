# NZWalks — Architecture Documentation

This is the reference baseline for the NZWalks ASP.NET Core Web API, written before further feature work so that upcoming changes build on an accurate, agreed-upon understanding of the current codebase — not an idealized version of it.

**What NZWalks is:** an ASP.NET Core 8 Web API for browsing walking trails ("Walks") grouped by geographic "Regions" and difficulty level, with JWT-based authentication, Identity-backed role authorization (Reader/Writer/Admin), image upload for regions, and versioned Region endpoints (v1/v2).

## How to read this doc set

Read in this order:

1. **[01-PROJECT-STRUCTURE.md](./01-PROJECT-STRUCTURE.md)** — the folder/file tree, what each folder is for, the Controllers/Models/Repositories/Mappings layout, and why it's organized this way.
2. **[02-MIDDLEWARE-PIPELINE.md](./02-MIDDLEWARE-PIPELINE.md)** — `Program.cs` broken into DI registration order and HTTP pipeline order, plus a worked example of a request traversing every middleware layer in sequence (with a Mermaid diagram).
3. **[03-FINDINGS-AND-TECH-DEBT.md](./03-FINDINGS-AND-TECH-DEBT.md)** — real bugs, gaps, and structural issues found while writing docs 1–2, framed as a punch list for upcoming work rather than urgent incidents.
4. **[04-appendix-glossary.md](./04-appendix-glossary.md)** — short definitions of terms used throughout (DTO, middleware, repository pattern, claims, migrations, etc.) for anyone using this as onboarding material.

## Ground rules for this documentation set

- **Describes reality, not aspiration.** Where the current code has a quirk (a misspelled folder, a missing CORS policy, a bug), it's documented as-is in 01/02 and then called out explicitly in 03 — nothing is silently "corrected" in the description.
- **No code was changed to produce this.** This is a read-only survey of the codebase as it existed on 2026-08-03.
- **This is the foundation, not the plan.** Decisions about *what* to fix first, and in what order, are intentionally left to a future planning pass — 03-FINDINGS-AND-TECH-DEBT.md lists what's wrong and why it matters, not a prioritized roadmap.
