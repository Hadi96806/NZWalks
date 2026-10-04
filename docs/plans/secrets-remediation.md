# Secrets remediation plan

**Status:** not started · **Written:** 2026-09-30 · **Scheduled:** 2026-10-01

Closes out [ADR-0003](../decisions.md#adr-0003--handling-of-secrets-already-in-git-history) and
finding #11's neighbours in [03-FINDINGS-AND-TECH-DEBT.md](../03-FINDINGS-AND-TECH-DEBT.md).

---

Repo is public (`github.com/Hadi96806/NZWalks`); `appsettings.json` carries the live SQL `sa`
password and `Jwt:Key`, committed in `13ca524`. Runtime exposure is nil (local-only API), but
the repo exposure is already real and grows with every commit. Steps in order:

| # | Step | Why |
|---|------|-----|
| 1 | `dotnet user-secrets init` in `NZWalks/NZWalks.API`, then set the **current** values into user-secrets unchanged | Creates a safe destination before removing anything, so the app keeps running through the migration. Pure plumbing — exposure unchanged. |
| 2 | Strip `appsettings.json` to empty placeholders (keep the keys) | Stops the bleeding: future commits stop republishing the values. Empty keys keep the config contract readable for a fresh clone. |
| 3 | Add null-or-empty startup guards in `Program.cs` naming the missing key and the `user-secrets set` command | Load-bearing and easy to skip. `Program.cs:111` uses `?? throw`, which catches null — the empty string from step 2 passes through and dies later inside `SymmetricSecurityKey`. Connection strings would surface as an obscure driver error. |
| 4 | Rotate `Jwt:Key` — fresh value, user-secrets only | First step that actually changes an attacker's position; steps 1–3 are hygiene. The old key is public forever, and until replaced anyone reading the repo can mint an `Admin` token. Invalidates existing tokens (fine — only holder is us). |
| 5 | Rotate the DB credential **on the SQL Server instance**, preferably as a least-privilege login scoped to the two DBs instead of `sa` | Most-skipped step because it lives outside the codebase. Until done, `p@ssw0rd` is a live credential on a public repo. Scoping to the two DBs costs the same and contains the next leak. |
| 6 | Verify: `dotnet user-secrets list`, run the app, hit login and confirm a token returns | A half-applied rotation is worse than none — app down, or silently still on the old credential while we think otherwise. |
| 7 | Grep the tree for both old values, then commit and push | Last cheap check that no tracked file still holds a live value, before the fix itself goes public. |
| 8 | Update ADR-0003 in `docs/decisions.md` — Status `Open` → `Accepted`, fill Decision + Consequences | The values stay in history permanently. Without a record that they were rotated and are dead, week 5 finds them and can't tell if they're live. |

**Explicitly not doing: a history rewrite.** After steps 4–5 the values in history are dead, so a
rewrite buys tidiness rather than security, at the cost of rewriting every commit hash. If it
ever happens it gets its own ADR covering coordination with existing clones.

Note: the `.gitignore` secrets section (from `883839e`) survived the revert and already says
connection strings and `Jwt:Key` belong in user-secrets — steps 1–3 make that true.

---

## Addendum — state left behind on 2026-09-30

Steps 1–4 were applied once on 2026-09-30 and then reverted at the user's request. The revert
was `git checkout`, so it undid the tracked files but **not** the user-secrets store, which
lives outside the repo. What that leaves for step 1:

- A user-secrets store exists under id `1208e6b5-260f-4946-b7ab-de4c5be51a6c`, holding a rotated
  `Jwt:Key` and both connection strings.
- The revert removed `UserSecretsId` from `NZWalks.API.csproj`, so **nothing reads that store**.
  It is orphaned, not active.
- Running `dotnet user-secrets init` fresh will mint a *new* id and point at an *empty* store.
  The values from 2026-09-30 will not appear in `dotnet user-secrets list`, which looks like
  step 1 silently failing. It hasn't — it's a different store.

Simplest path: run `init` fresh and re-set all three values, treating the orphan as dead. The
`Jwt:Key` wants rotating in step 4 regardless, so nothing is lost. Optionally delete the
orphaned store afterwards — on Windows it sits at
`%APPDATA%\Microsoft\UserSecrets\1208e6b5-260f-4946-b7ab-de4c5be51a6c\`.

Also settled on 2026-09-30, worth not re-deriving: the `sa` password was **not** rotated on the
SQL Server instance. Step 5 is still fully outstanding and is the only step that changes the
live-credential situation.
