---
name: task-close
description: Closes out a finished NZWalks task or set of subtasks, in order — verify the work, review every commit with the read-only nzwalks-reviewer agent and get the user's OK, commit with proper messages, push only after the user approves, and only then tick the finished steps in the plan file, the plan's artifact page and the week tracker. Use it when the user says "close this task", "finish and push", "commit, push and tick", or types /task-close <plan file>. Not for planning (use task-plan) or for implementing a subtask (use task-implement).
---

# Task close

The user wants a finished piece of work landed the same way every time: checked, reviewed, committed,
pushed with his approval, and **only then** ticked everywhere it is tracked. A tick means "this is on
the remote and was reviewed", so nothing is ticked early.

Arguments: `<plan file>` and optionally the subtask ids being closed, e.g.
`docs/plans/rename-repositories-pagination.md b3s1 b3s2 b3s3`. If the plan file is missing, ask. The
plan's header names its tracker task and subtask ids; the week tracker URLs are in the constants table of
`.claude/skills/weekly-plan/SKILL.md` and the "Active trackers" table of
`.claude/skills/plan-queue/SKILL.md` (for a week not listed there, ask the user for the link).

Run every script and command from the **repo root**, and use `NZWalks/NZWalks.sln`-style paths: the shell's
directory resets between calls, and `build-plan-artifact.ps1` writes `-Out` relative to where it runs.

## Step 0 — Scope what is being closed

- Read the plan file. Note the **steps** (the `### n.` headings), which tracker subtask ids they map to,
  and the plan's **Verify / Done when** items.
- `git fetch origin` (read-only), then `git status` and `git log origin/<branch>..HEAD`. List what is
  uncommitted and what is committed but unpushed. Separate the work that belongs to this plan from anything
  unrelated; unrelated files are never staged. Say plainly what you found.
- **Unpushed commits you did not make and gate in this run** (made in Visual Studio or a terminal, or by
  merge, cherry-pick, rebase or pull) were never reviewed: the gate only sees `git commit` run by Claude
  (ADR-0010). List them separately, offer a dry-run review of each with
  `review-prepare.ps1 -Commit <sha>` and the reviewer, and put them to the user as their **own**
  acknowledgement question in Step 4, separate from "push now?". A step that rests only on such a commit is
  ticked only if the user says so explicitly, because a tick means "reviewed and on the remote".
- Decide which steps and subtasks are **actually finished**. A step that is only partly done is not
  closed: leave it unticked and say why.

## Step 1 — Verify it is really done

Run what can be run and show the **real output**, not a summary:
- `dotnet build NZWalks/NZWalks.sln --no-incremental`: 0 errors and 0 warnings (the Stop hook fails on any
  warning, ADR-0008; an incremental build that is up to date prints no warnings and can hide one). If a
  running app locks the output, build the project file into the scratch folder instead and say so:
  `dotnet build NZWalks/NZWalks.API/NZWalks.API.csproj --no-incremental -o <scratch folder>/build`
  (a project, not the solution, accepts `-o`). Never kill a process you did not start.
- `dotnet format whitespace NZWalks/NZWalks.sln --verify-no-changes` exits 0.
- The plan's own Verify / Done-when items that a command can prove. List the ones only the user can check
  (Visual Studio, a browser) as a checklist.
- If a check fails, stop and fix or ask. Do not continue to review with a red check.

## Step 2 — Group the work into commits

- One logical change per commit, as the plan says (for example renames, then docs, then the blame-ignore
  entry). Docs follow code in their own commit.
- Stage **by name**, in **its own command**, never in the same command as `git commit`: the gate
  (ADR-0010) reads the index when a command starts and refuses `git add … && git commit …`, `-a`, `-o`,
  `-i` and path arguments. Never `git add -A` on the repo root. Stage each commit's files right before
  that commit's review (Step 3), not all commits at once, so the staged set and its hash are exactly one
  commit.
- Never stage `.vs/`, `obj/`, `bin/`, `docs/plans/` (untracked by design) or another person's unrelated
  changes.

## Step 3 — For each commit: review, approve, commit

0. Start with an **empty index** (`git diff --cached --stat` prints nothing), then stage only this
   commit's files, in their own command. If something is already staged, tell the user what it is and ask
   before unstaging it: it may be their own work.
1. `powershell -NoProfile -ExecutionPolicy Bypass -File .claude/hooks/review-prepare.ps1` writes the
   staged diff to a file and prints its hash.
2. Run the **`nzwalks-reviewer`** agent (Agent tool, `subagent_type: nzwalks-reviewer`) with the diff file
   path, the hash, and a short context: what the commit is, what you already verified, and what is
   intentionally left for another commit. Tell it not to re-report accepted limits in ADR-0010.
3. **Show the user the report** with its levels (High, Medium, Low, Cosmetic) and say which findings you
   verified as real and which were false alarms.
   - **High:** the commit stays blocked. Fix it and review again, or ask the user. Record `ACCEPTED` only
     after the user has seen the finding and said to go ahead, with what they decided in `-Note`.
   - **Compromises:** ask the user to confirm each one before it is written to a doc: a deliberate choice
     goes to an ADR in `docs/decisions.md`, an unintended defect or debt goes to a finding in `docs/03`.
   - **Requested checks:** list each one the reviewer asked for and run it **only after the user approves
     that specific check**; pass the output back to the reviewer if it could change the verdict.
   - **Medium and below:** fix cheap ones that are real; any change to what is staged changes the hash and
     needs a new review. Do not chase cosmetics endlessly: say what you left and why.
4. **Ask for the user's OK** on the report before recording anything.
5. `review-mark.ps1 -Hash <hash> -Verdict PASS|BLOCK|ACCEPTED -Note "<text>"` (run as
   `powershell -NoProfile -ExecutionPolicy Bypass -File .claude/hooks/review-mark.ps1 …`). Never record PASS
   or ACCEPTED without having run the reviewer, and never on your own judgement.
6. Commit in a separate command with a proper message: a subject that says what changed, a body that says
   why and what was verified, `Reviewed by the read-only reviewer (diff <hash first 8>): PASS.`, and the
   attribution line from the session's reminder. Pass the message with `-m "…"` or `-F <file>`; never
   `-F -` with a heredoc (ADR-0010 lists it as a false block). Confirm with `git log --oneline` and
   `git status`.

Things that will come up:
- Edits to `.claude/settings.json`, `.claude/hooks/` or `.claude/agents/` are refused for Claude's edit tools
  (ADR-0010). Tell the user what and why, and ask for the one-file grant (the command is in the hook's
  message); do not work around it with a shell command.
- The secrets guard flags a 40-character commit hash written with the edit tools (the
  `.git-blame-ignore-revs` case). It is a false positive, but the guard's own message says to tell the
  user and ask: **ask each time**, and offer appending the hash to that one file with a shell command. An
  approval given in an earlier conversation is not standing permission.

## Step 4 — Push, only with the user's approval

- After the last commit, `git fetch origin` and compare. If the remote has commits you don't have, stop
  and tell the user; do not merge or rebase on your own.
- If Step 0 found unreviewed commits, ask the acknowledgement question for them first, on its own line.
- List the commits that would be pushed and **ask once: "push now?"**. Push on a clear yes only
  (`git push origin <branch>`), never force, never to another branch. Report the result of the push.
- Do not tick anything yet. The user's "yes" to "push now?" is also the go-ahead to tick in Step 5, once the
  push has succeeded; say so in the question.

## Step 5 — Tick, only after the push succeeded

Confirm the push first (`git status -sb` shows the branch level with its remote). Then, for the steps and
subtasks that are finished **and** pushed:

1. **Plan file:** add or set `- [x] Done` under each finished `### n.` step (the plan files are untracked
   by design, so this is local).
2. **Plan artifact page:** in `<plan>.parts.html` set `done:true` on each finished step object, bump the
   storage `KEY` suffix (so ticks a browser already saved under the old key don't override the new flags;
   the side effect is that the page's saved chat layout and any ticks made by hand in a browser reset too),
   rebuild with `powershell -NoProfile -ExecutionPolicy Bypass -File .claude/skills/task-plan/build-plan-artifact.ps1 -Parts <plan>.parts.html -Out <plan>.html`, then `Artifact` `action: "read"` the
   plan's URL and publish the rebuilt file to the same URL, leaving `capabilities` out; the publish result
   should say the declaration was "carried forward", which keeps the page's Ask Claude chat. If it does not,
   republish with `capabilities: {sample: {}}` as `task-plan` does.
3. **Tracker:** `ArtifactData` `get` the week's progress document (for example collection `progress`, doc
   `week2`), then `update` with the **full** `done` map plus the finished subtask ids, `updatedAt` set to the
   current ISO time (the tracker writes both fields), and `if_version` set to the version you read. Keep
   every existing tick; never untick anything.
4. **Read back** the tracker document and the plan file, and report the actual states.

A task whose subtasks are not all finished keeps its unfinished ones unticked.

## Step 6 — Report

Say, briefly: what was committed and pushed (hashes and subjects), the reviewer verdict for each commit,
what was ticked where, and what is still open or left on purpose (cosmetic notes, anything deferred).

## Rules

- Never tick before the push, never push without the user's approval, never force-push.
- Never skip the gate or the reviewer, and never record a verdict you did not get.
- Never write secrets into files; the repo is public.
- Do not commit plan files, scratch files or hook state (`.claude/hooks/.state/` is gitignored).
