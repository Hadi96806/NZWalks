---
name: task-implement
description: Implements exactly ONE subtask from an NZWalks plan in docs/plans/, then teaches it — explains the syntax of every piece of code written and the business reason behind it. Invoked by the "Implement" button on a plan's artifact page (it opens a new desktop Code session pre-typed with "Use the task-implement skill on <plan file>, subtask <n>"), by "/task-implement <plan file> subtask <n>", or when the user says "implement subtask N of <plan>". Only use it when the user explicitly asks for a subtask to be implemented; for planning use task-plan instead.
---

# Task implement

The user is learning by building. He normally writes the code himself and uses `task-plan` for
plans. When he presses **Implement**, he's handing one subtask to you, so the explanation is as
important as the code. A correct diff he doesn't understand counts as a failure.

Arguments: `<plan file> subtask <n>`, e.g. `docs/plans/fluentvalidation.md subtask 2`.

## Step 0 — File this session under the NZWalks group

The Implement button opens a new Claude Code session in the desktop app
(`claude://code/new?folder=<repo>&q=<prompt>`), and that link can't choose a sidebar group. So
first, move this session into the **NZWalks** group: `move_sessions` with `["self"]`. Find the
group's id with `list_groups` (it was `cg-298ae979-f559-477c-b508-3e1f0fc322c0` on 2026-10-12).
If it doesn't exist, create it with `create_group`. If the sidebar tools aren't available
(a terminal CLI session), skip this step and say so in one line.

## Step 1 — Load exactly one subtask

- Read the plan file and find subtask `<n>`. Restate it in one line, along with its tracker id
  if the plan gives one.
- Read the **Decisions**, **Owned elsewhere** and **Order** sections too. They constrain what you
  may change.
- If the subtask has a **Decide:** item the plan doesn't answer, ask with AskUserQuestion before
  writing code. Put the recommended option first, and describe the concrete consequence of each
  option.

## Step 2 — Check preconditions in the code, not the plan

- `git status` (ignore `.vs/`, `obj/`, `bin/`). If there are uncommitted changes in files this
  subtask touches, stop and say so. Don't mix two pieces of work in one diff.
- Dependencies: if the Order section says this subtask needs another one first, confirm that one
  is done in the code. If it isn't, say so and stop.
- Already done? If the code already does what the subtask asks, say so and change nothing.

## Step 3 — Implement only this subtask

- Change only the files the subtask names. Anything else you notice, list it at the end as
  "spotted, not changed".
- Follow `CLAUDE.md` conventions: the `Repositories` folder and namespace, comment style, and
  match the surrounding code.
- Never write secrets into files. The repo is public.
- Build from `NZWalks/`: `dotnet build`. Report the errors (0 expected) and the warning count
  (compare it with the plan's baseline).
- Don't commit unless he asks.

## Step 4 — Teach it

For each change, in this order:
1. **What changed:** `file:line`, and the code.
2. **Syntax:** explain each construct that isn't obvious: generics, lambdas, fluent chains,
   attributes, the DI registration, the extension methods. Say what the compiler or framework does
   with it, not only what it's called. Assume he knows C#; explain the framework-specific parts.
3. **Business reason:** why the API should behave this way. What a client sees, what risk it
   closes, what rule it enforces, and what would happen without it.

## Step 5 — Verify and hand back

- List the plan's **Verify** steps for this subtask as a checklist he runs in Visual Studio.
  Visual Studio has his real user-secrets; a session-launched app may not.
- If you run checks yourself, show the real output, not a summary.
- Don't tick anything here. A tick means "reviewed and on the remote", so ticking (the plan file,
  the plan's artifact page and the week tracker) belongs to the `task-close` skill, after review,
  commit and push. When he confirms the checks pass, tell him to run `/task-close <plan file>`.
- End by naming the next subtask in the plan's order. Don't start it.
