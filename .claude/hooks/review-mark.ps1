# Step 3 of a review (ADR-0010): record the outcome for the exact staged diff, which is what unlocks the commit gate.
#   powershell -NoProfile -ExecutionPolicy Bypass -File .claude/hooks/review-mark.ps1 -Hash <hash> -Verdict PASS|BLOCK|ACCEPTED -Note "<text>"
# PASS     the reviewer reported no High finding.
# BLOCK    the reviewer reported a High finding that is still unresolved (the commit stays blocked).
# ACCEPTED the user looked at the High finding(s) and decided to commit anyway; -Note must say what they decided.
# The hash must match what is staged right now, so a stale review can't be recorded for a changed diff.
param(
    [Parameter(Mandatory)][string]$Hash,
    [Parameter(Mandatory)][ValidateSet('PASS', 'BLOCK', 'ACCEPTED')][string]$Verdict,
    [string]$Note = ''
)
$ErrorActionPreference = 'Continue'
$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
. (Join-Path $PSScriptRoot 'review-common.ps1')

if ($Verdict -eq 'ACCEPTED' -and -not $Note.Trim()) { Write-Error "ACCEPTED needs -Note: record what the user decided."; exit 1 }
if (-not (Test-Path (Join-Path $script:StateDir "review-$Hash.diff"))) { Write-Error "No prepared diff for $Hash. Run review-prepare.ps1 first."; exit 1 }
$staged = Get-StagedDiff $root
if ($staged.Hash -ne $Hash) { Write-Error "The staged diff is now $($staged.Hash), not $Hash. Re-run review-prepare.ps1 and review the current diff."; exit 1 }

New-Item -ItemType Directory -Force $script:ReviewDir | Out-Null
$lines = @("verdict: $Verdict", "hash: $Hash", "recorded: $((Get-Date).ToUniversalTime().ToString('o'))")
if ($Note.Trim()) { $lines += "note: $($Note.Trim())" }
[IO.File]::WriteAllText((Join-Path $script:ReviewDir "$Hash.txt"), (($lines -join "`n") + "`n"), (New-Object Text.UTF8Encoding $false))
Write-Output "Recorded $Verdict for diff $Hash. $(if ($Verdict -eq 'BLOCK') { 'The commit stays blocked until a High finding is fixed (re-review) or the user accepts it.' } else { 'git commit is now allowed for exactly this staged diff.' })"
