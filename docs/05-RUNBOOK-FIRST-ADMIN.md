# 05 — Runbook: creating the first Admin

Register always creates a **Reader** and no API call can promote someone without already being an
Admin ([ADR-0004](./decisions.md#adr-0004--roles-are-granted-only-by-an-admin)). So the **first**
Admin, and any recovery after the last one is lost, comes from SQL. Once one Admin can log in,
use `POST /api/Users/GrantRole` for everyone else.

Use `<email>` as a placeholder below. Never write a real email or a password into this file.

## Before you start

- The user must exist. Create them with `POST /api/Auth/Register` first — that makes a Reader.
- Connect to `NZWalksAuthDb` (not `NZWalksDb`) as a login with write access. `nzwalks_app`
  works; its connection string is in user-secrets.
- With `sqlcmd`: pass `-C` (trust the self-signed server certificate). If a write fails with
  `Msg 1934 … QUOTED_IDENTIFIER`, add `-I`.
- The role ids are the ones seeded in `Data/NZWalksAuthDbContext.cs`:

| Role | Id |
|---|---|
| Reader | `74f9477e-7d94-4060-8930-7739c951ab88` |
| Writer | `53e8188d-da64-4947-8719-335ae316952e` |
| Admin | `6293730a-ab48-452b-b9b8-c06858ba016c` |

## 1. Read first — does an Admin already exist?

```sql
SELECT u.Email, ISNULL(STRING_AGG(r.Name, ','), '(no roles)') AS Roles
FROM AspNetUsers u
LEFT JOIN AspNetUserRoles ur ON ur.UserId = u.Id
LEFT JOIN AspNetRoles r      ON r.Id = ur.RoleId
GROUP BY u.Email
ORDER BY u.Email;
```

If someone already has `Admin` and you can log in as them, **stop** — no write is needed.

## 2. Grant Admin to one user

```sql
INSERT INTO AspNetUserRoles (UserId, RoleId)
SELECT u.Id, '6293730a-ab48-452b-b9b8-c06858ba016c'
FROM AspNetUsers u
WHERE u.NormalizedEmail = UPPER('<email>')
  AND NOT EXISTS (SELECT 1 FROM AspNetUserRoles x
                  WHERE x.UserId = u.Id
                    AND x.RoleId = '6293730a-ab48-452b-b9b8-c06858ba016c');
```

`(1 rows affected)` means it worked. `(0 rows affected)` means the email doesn't exist (it is
matched on `NormalizedEmail`, which is upper case) or the user is already an Admin. The
`NOT EXISTS` makes it safe to run twice.

## 3. Tidy: nobody should have zero roles

Login rejects a user with no roles. List them with the query in step 1 (`(no roles)`), then give
them Reader:

```sql
INSERT INTO AspNetUserRoles (UserId, RoleId)
SELECT u.Id, '74f9477e-7d94-4060-8930-7739c951ab88'
FROM AspNetUsers u
WHERE NOT EXISTS (SELECT 1 FROM AspNetUserRoles x WHERE x.UserId = u.Id);
```

To demote an extra Admin, prefer `POST /api/Users/RevokeRole` over SQL; it refuses to remove the
last Admin.

## 4. Verify

1. Log in as that user (`POST /api/Auth/Login`) — **after** the insert. A role is baked into the
   token when it is issued, so a token from before the grant still says Reader.
2. Decode the token locally (never on a website) and confirm the role claim contains `Admin`.
3. Call an Admin-only action, e.g. `POST /api/Users/GrantRole` for an unknown email: 404 means you
   got past authorization; 403 means the token still lacks Admin.
4. Re-run the step 1 query: no `(no roles)` rows.
