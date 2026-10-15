# PostToolUse (Edit|Write|MultiEdit): whitespace-format the one .cs file that was just edited.
# Inactive until the repo has an .editorconfig (Week 2 A1, finding #20): formatting against
# defaults would rewrite whole files and bury the real change in the diff.
$ErrorActionPreference = 'Continue'  # native tools write warnings to stderr; don't treat them as fatal
$payload = [Console]::In.ReadToEnd() | ConvertFrom-Json
$path = [string]$payload.tool_input.file_path
if ($path -notmatch '\.cs$' -or -not (Test-Path $path)) { exit 0 }

$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
if (-not (Test-Path (Join-Path $root '.editorconfig'))) { exit 0 }

$full = (Resolve-Path $path).Path
if ($full -notlike "$root*" -or $full -match '\\(obj|bin)\\') { exit 0 }
$relative = $full.Substring($root.Length).TrimStart('\', '/')

$before = Get-FileHash $full
# --folder skips loading the solution, so this takes ~1-2 s instead of a full workspace load.
$output = & dotnet format whitespace $root --folder --include $relative 2>&1 | Out-String
if ($LASTEXITCODE -ne 0) {
    [Console]::Error.WriteLine("format-on-edit.ps1: dotnet format failed on $relative (exit $LASTEXITCODE). Tell the user; the edit itself stands.`n$output")
    exit 2
}
if ((Get-FileHash $full).Hash -ne $before.Hash) {
    # Claude's view of the file is now stale; say so, so the next Edit re-reads it.
    Write-Output (@{ hookSpecificOutput = @{ hookEventName = 'PostToolUse'; additionalContext = "format-on-edit.ps1 reformatted whitespace in $relative. Re-read it before editing it again, and mention the reformat to the user." } } | ConvertTo-Json -Compress -Depth 4)
}
exit 0
