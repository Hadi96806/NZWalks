---
name: task-plan
description: Default planner for the NZWalks project. Plans ONE action the user wants to do — a subtask, fix, feature, or decision — by checking it against the real code, the docs (findings, structure, decisions), and the weekly plan of the hardening roadmap the user says to rely on. The user writes the code himself; this skill produces the plan only. Use it whenever the user asks to plan, scope, break down, or understand what's needed for a change in this project, whenever plan mode is active, or when they name a subtask from a week tracker (e.g. "B3s2", "JWT hardening", "D1") — even if they don't say "plan".
---

# Task plan

The user is learning by building. He writes every line himself. Your job is to make the next
hour of his work **correct and unsurprising**: what to change, where, why, the decisions inside
it, and how to know it worked. A plan that just restates the subtask is worthless. A plan that
says "this is already half done", "this collides with something already scheduled", or "as
written this fixes the wrong problem" is worth the whole session.

Be fast: read only what this action needs. Be smart: verify in the code, never from memory.

## Constants

| Thing | Where |
|---|---|
| Conventions, commands | `CLAUDE.md` (repo root) |
| Structure | `docs/01-PROJECT-STRUCTURE.md` |
| Pipeline / DI order | `docs/02-MIDDLEWARE-PIPELINE.md` |
| Findings #1–#25 | `docs/03-FINDINGS-AND-TECH-DEBT.md` — a 2026-08-03 snapshot, re-check every finding |
| Glossary | `docs/04-appendix-glossary.md` |
| Decision log | `docs/decisions.md` |
| Scheduled plans | `docs/plans/*.md` (local, gitignored) + Notion: Claude Mastery → 14 weeks Program → Week N |
| Week trackers | Week 1: `https://claude.ai/artifact/UcgFcrcwRmYXsvadtbsi9Z`, progress in db `progress/week1`. Later weeks: ask the user for the link. |
| Roadmap | Notion "Claude Mastery", page id `3af62584db3280278821fe2480b8d8d3` |

## Step 1 — Pin down the action and the weekly plan

- Restate the action in one line. If it maps to a tracker subtask, name its id (e.g. B3s1).
- **Started from a tracker's "Make a plan" button?** The prompt names the task id, its title and
  the tracker URL, and says to rely on that tracker. Then don't ask which weekly plan: read that
  tracker's task card (its subtasks, the decisions above it, and the footer's findings and
  collisions) as the brief. Still ask about anything the card leaves open. When the plan is
  published, add it to that week's Notion `## Interactive plans` list. Also ask him whether the
  tracker's card should switch from Make a plan to Open plan; that's a tracker republish.
- Otherwise, **always ask which weekly plan to rely on** before comparing against one — never infer it from
  the date or the conversation. Offer the options you can see (the known week trackers, the
  Notion roadmap week, a `docs/plans/` file, or "none — plan against the code only"), and say in
  each option's description what that source currently says about this action.
- **Trap: tracker decision ids ≠ ADR numbers.** Week 1: D1 → ADR-0002, D2 → ADR-0001,
  D3 → ADR-0003. Always map before editing the decision log.

## Step 2 — Read only the docs this action needs

Pick by topic, don't read everything:
- Touches a controller, repo, or folder → `01`
- Touches `Program.cs`, middleware, auth, ordering → `02`
- Fixes a bug or debt → `03` (find the finding number)
- Closes off an alternative → `decisions.md`
- Secrets, keys, config → `docs/plans/secrets-remediation.md`

## Step 3 — Audit the code against the action

Verify, every time:
- **Already done?** Grep the csproj, `Program.cs`, the attributes. Planning finished work wastes
  his hours.
- **Half done?** e.g. an ADR that exists but still says "Open / Not yet taken".
- **Uncommitted work?** `git status` (ignore `.vs/`, `obj/`, `bin/`).
- **Does the subtask as written fix the right problem?** e.g. "use AddToRolesAsync" would keep
  the self-assigned-Admin hole open. Say so plainly.
- **Will it compile/run?** Name the gotcha before he hits it cold.

## Step 4 — Compare with the weekly plan he chose

- Where does this action sit in that week, and what in this week or the next two depends on it?
- **Collisions:** is part of this already owned by another subtask or a `docs/plans/` step? Never
  plan the same change in two places — reference the owner instead (e.g. Jwt:Key rotation belongs
  to secrets plan step 4; ADR-0003 acceptance belongs to its step 8).
- Fit check against the 5-hour week. If it doesn't fit, say so.

## Step 5 — Ask whatever makes the plan more accurate

Ask freely — any question whose answer makes the plan more accurate is worth asking: scope,
intent, preferences, environment facts you can't read from the repo, trade-offs only he can
weigh. Don't ask what you can find in the code or docs yourself.

Use AskUserQuestion (up to 4 questions per call; call it again if you need more), and make every
option **informative**:
- Label: short and concrete.
- Description: what concretely happens if he picks it — the files touched, what it costs (time,
  breaking change, workflow change), what it leaves open, and the consequence if it goes wrong.
- Put the recommended option first with "(Recommended)" and say *why* in its description.

Example of a good option description:
> "Tokens live 15 min, ClockSkew 0. You re-login in Swagger more often; a stolen token is useful
> for 15 min instead of ~25 today. No refresh tokens yet, so this is the only lever."

## Step 6 — Write the plan

The user writes the code, so: **name APIs, files, properties and decisions; no ready-to-paste
implementations.** Structure:

1. **Context** — why this change, what's true today (with `file:line`), consequences of leaving it.
2. **Already done / out of scope / owned elsewhere** — each with one-line reason. Silent omissions
   read as oversights.
3. **Steps** — small, each independently verifiable, cheapest-and-safest first, dependencies named.
   Per step: what changes, where, the decision inside it, the gotcha.
4. **Verify** — concrete manual checks (Swagger/curl; no test project exists). e.g. decode the
   token and compare `exp − iat`, a curl that must now 401/403.
5. **Docs to update** — finding to mark fixed in `03`, ADR to write/accept (map D↔ADR), tracker
   subtask to tick.

### The interactive plan artifact — buttons live here, and only here

Every plan also gets an **artifact**, and that's the only place the buttons go: **not** on the
Notion page and **not** in the `.md` (he decided this 2026-10-08). Never hand-write the page.
Every plan uses the same layout:

1. Write `docs/plans/<name>.parts.html` with five sections, each wrapped in `<!--NAME-->…<!--/NAME-->`:
   `TITLE` (a short name), `HEADER` (eyebrow, h1, lede, decision chips), `NOTES_TOP` ("Owned
   elsewhere"), `NOTES_BOTTOM` ("Done when"), and `DATA`. `DATA` holds the JS consts `PLAN`
   (the `.md` path), `KEY` (a localStorage prefix), `CONTEXT` (the brief for Ask Claude: who he
   is, the stack, the decisions, what's out of scope), and `STEPS`
   (`[{t, tag, why?, items:[html…], decide?}]`). Copy `docs/plans/rate-limiting.parts.html`.
2. Build: `powershell -File .claude/skills/task-plan/build-plan-artifact.ps1 -Parts docs/plans/<name>.parts.html -Out docs/plans/<name>.html`
   (from the repo root). It fills `plan-artifact-template.html`, next to this file.
3. Publish `docs/plans/<name>.html` with `capabilities: {sample: {}}`. Put the URL in the `.md`.

What the template gives each subtask, so don't rebuild these:
- **Ask Claude**: a chat through the `sample` capability. The first turn sends `CONTEXT`,
  every subtask title and this subtask's full text. It docks on the right with a draggable,
  keyboard-resizable edge, or switches to a **floating window** that moves by its header and
  resizes from its corner. The layout is remembered per viewer. On phones it's a bottom sheet.
  That Claude can't read his files, and the brief tells it so.
- **Implement**: **Start implement session** opens a new desktop Claude Code session through
  `claude://code/new?folder=<repo>&q=<prompt>`. The prompt is "Use the task-implement skill on
  <plan>, subtask <n>. Before starting, file this session under the "NZWalks" sidebar group."
  It must **not** start with `/`, because the app refuses deep-link prompts that start with a
  slash. The link has no group parameter, so `task-implement` Step 0 moves the session into
  the group. A **Copy command** fallback gives `/task-implement <plan> subtask <n>` for when the
  link doesn't open the app.
- A per-viewer Done tick and a progress bar.

To change the layout for every plan, edit the template, rebuild each plan's `.html` from its
parts file, and republish each one.

## Step 7 — Save and publish the plan

Every plan lives in these places:

1. **Local files:** `docs/plans/<short-kebab-name>.md`, plus its `.parts.html` and built
   `.html`. Plan mode already writes there via `plansDirectory` in `.claude/settings.json`;
   rename an auto-generated filename to a readable one. `docs/plans/` is **gitignored**: never
   `git add` it or commit a plan, and never move a plan anywhere tracked.
2. **The artifact** (Step 6): the interactive, durable copy of the plan. Its versions are kept
   by the artifact service, so it's the copy that survives if the local files are lost.
3. **Notion holds a link, not a copy** (he decided this 2026-10-12: "I wanted the artifact to be
   under Week 1, not a Notion file"). **Don't create a Notion page for the plan.** On the Week N
   page (Claude Mastery → 14 weeks Program → Week N), add one line under its
   `## Interactive plans` heading: an emoji, the plan's name linked to the artifact URL, and
   "Ask Claude chat and Implement on every subtask". Create that heading if it's missing.
   If the Week N page doesn't exist, create it under 14 weeks Program
   (page `3f062584db32817f8ab9ead496f2d755`) and record its id here.
   - Known week pages: Week 1 = `3f062584db32815e917fcbbdb3ceadeb`,
     Week 2 = `3f962584db328128829cfa63cf7ea5c6`.
   - Notion can't embed an artifact (it only embeds uploaded HTML files), so a link is the
     closest thing to putting the artifact under the week.
4. Give him the artifact link and the local path.
5. Before publishing, grep the plan for the `Jwt:Key` value, the `sa` password and real emails.
   The artifact is private until he shares it, but can be shared.

Plan one action at a time. When done, stop and offer the next subtask — don't batch.

## Things that have burned us

- Implementing instead of planning — he discarded a fully working Week 1 diff because he wants
  to write it himself.
- Trusting `docs/03` or memory over the code.
- Two plans owning the same step.
- Severity claims that aren't true — he's learning from these, so pressure-test them
  (e.g. `DateTime.Now` in a JWT `expires` is a latent hazard, not a live bug).
- Putting secret values in committed docs (the repo is public).
