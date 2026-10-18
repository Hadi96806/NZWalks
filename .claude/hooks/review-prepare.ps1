# Step 1 of a review (ADR-0010): write the diff to review into a file the read-only reviewer can Read.
#   Staged diff (what the next commit will contain):
#     powershell -NoProfile -ExecutionPolicy Bypass -File .claude/hooks/review-prepare.ps1
#   An existing commit (a dry run; no marker is needed or recorded):
#     powershell -NoProfile -ExecutionPolicy Bypass -File .claude/hooks/review-prepare.ps1 -Commit <sha>
param([string]$Commit)
$ErrorActionPreference = 'Continue'
$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
. (Join-Path $PSScriptRoot 'review-common.ps1')

if ($Commit) {
    $text = Invoke-GitUtf8 $root @('show', '--no-color', '--no-ext-diff', $Commit)
    if (-not $text.Trim()) { Write-Error "No such commit: $Commit"; exit 1 }
    $hash = Get-DiffHash $text
    $label = "commit $Commit"
} else {
    $info = Get-StagedDiff $root
    if ($info.Empty) { Write-Output "Nothing is staged. Stage the files for this commit by name (git add <files>), then run this again."; exit 1 }
    $text = $info.Text; $hash = $info.Hash; $label = 'staged diff'
}

New-Item -ItemType Directory -Force $script:StateDir | Out-Null
$diffFile = Join-Path $script:StateDir "review-$hash.diff"
[IO.File]::WriteAllText($diffFile, $text, (New-Object Text.UTF8Encoding $false))

$files = @(if ($Commit) { git -C $root show --name-only --format= $Commit } else { git -C $root diff --cached --name-only }) | Where-Object { $_ }
Write-Output "Review target : $label"
Write-Output "Diff hash     : $hash"
Write-Output "Diff file     : $diffFile"
Write-Output "Files ($($files.Count)):"
$files | ForEach-Object { Write-Output "  $_" }
Write-Output ""
Write-Output "Next: run the nzwalks-reviewer subagent, giving it the diff file path and the hash above."
