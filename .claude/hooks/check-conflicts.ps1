# Detects an unfinished merge/rebase with conflicted files.
#   UserPromptSubmit: catches pulls the user ran in their own terminal; stdout becomes context.
#   PostToolUse (Bash): catches git pull/merge/rebase that Claude ran; exit 2 feeds stderr back.
$ErrorActionPreference = 'Continue'  # native tools write warnings to stderr; don't treat them as fatal
$payload = [Console]::In.ReadToEnd() | ConvertFrom-Json
$event = $payload.hook_event_name

if ($event -eq 'PostToolUse') {
    $cmd = [string]$payload.tool_input.command
    if ($cmd -notmatch '\bgit\s+(pull|merge|rebase|cherry-pick|stash\s+(pop|apply))\b') { exit 0 }
}

$root = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
Set-Location $root
$conflicted = @(git diff --name-only --diff-filter=U 2>$null | Where-Object { $_ })
if ($conflicted.Count -eq 0) { exit 0 }

$gitDir = (git rev-parse --git-dir).Trim()
$op = if ((Test-Path "$gitDir/rebase-merge") -or (Test-Path "$gitDir/rebase-apply")) { 'rebase' }
      elseif (Test-Path "$gitDir/MERGE_HEAD") { 'merge (pull)' }
      elseif (Test-Path "$gitDir/CHERRY_PICK_HEAD") { 'cherry-pick' }
      else { 'stash apply' }

$msg = @"
GIT CONFLICT detected by .claude/hooks/check-conflicts.ps1: an unfinished $op has $($conflicted.Count) conflicted file(s):
$($conflicted | ForEach-Object { "  - $_" } | Out-String)
Handle it before anything else, and report to the user as you go:
1. Say plainly that there is a conflict, which operation caused it and which files.
2. For each file, show both sides briefly and classify it:
   - mechanical (noise under .vs/, obj/, bin/; whitespace only; both sides added different, independent lines): resolve it
     and say exactly what you kept;
   - anything touching behaviour, config or docs where the two sides disagree: do NOT pick a side. Explain both versions
     and ask the user which to keep (or how to combine them).
3. After resolving, check no conflict markers remain and run dotnet build.
4. Never commit, continue the rebase, or push without the user's explicit ok. Never resolve by discarding a whole side
   (checkout --ours/--theirs) on a source file without asking.
"@

if ($event -eq 'PostToolUse') { [Console]::Error.WriteLine($msg); exit 2 }
Write-Output $msg
exit 0
