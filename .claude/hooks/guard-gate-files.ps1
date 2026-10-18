# PreToolUse (Edit|Write|MultiEdit): refuse Claude's edits to the commit gate's own files (ADR-0010):
# .claude/settings.json, everything under .claude/hooks/ (scripts, review markers, the allow-file) and .claude/agents/.
# The user can always edit these by hand. To let Claude change one, the user creates a short-lived allow-file in a terminal,
# run from the repo root (the first command creates the folder on a fresh clone):
#   New-Item -ItemType Directory -Force .claude/hooks/.state | Out-Null
#   Set-Content .claude/hooks/.state/allow-gate-edit.txt ".claude/hooks/review-before-commit.ps1"   (one repo-relative path per line, or *)
# It expires after 30 minutes. Exit 2 blocks the edit and sends stderr back to Claude.
# Only the edit tools are guarded; a shell command that writes to these files is not caught.
$ErrorActionPreference = 'Continue'  # native tools write warnings to stderr; don't treat them as fatal
$payload = [Console]::In.ReadToEnd() | ConvertFrom-Json
$path = [string]$payload.tool_input.file_path
if (-not $path) { exit 0 }

$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
try { $full = [IO.Path]::GetFullPath($path) } catch { exit 0 }
if (-not $full.StartsWith($root, [StringComparison]::OrdinalIgnoreCase)) { exit 0 }
$rel = $full.Substring($root.Length).TrimStart('\', '/').Replace('\', '/')

$protected = ($rel -match '(?i)^\.claude/settings(\.local)?\.json$') -or ($rel -match '(?i)^\.claude/(hooks|agents)/')
if (-not $protected) { exit 0 }

$allow = Join-Path $PSScriptRoot '.state/allow-gate-edit.txt'
if ((Test-Path $allow) -and (((Get-Date) - (Get-Item $allow).LastWriteTime).TotalMinutes -le 30)) {
    $granted = @(Get-Content $allow | ForEach-Object { $_.Trim().Replace('\', '/') } | Where-Object { $_ })
    if (($granted -contains '*') -or ($granted | Where-Object { $_ -ieq $rel })) { exit 0 }
}

[Console]::Error.WriteLine(@"
BLOCKED by .claude/hooks/guard-gate-files.ps1 (ADR-0010): $rel is part of the commit gate (settings, hooks or the reviewer agent), and Claude may not edit it unless the user grants that specific change.
Tell the user what you wanted to change and why. Do not work around this with a shell command. The user can edit the file themselves, or let you do it for the next 30 minutes by running in their own terminal, from the repo root:
  New-Item -ItemType Directory -Force .claude/hooks/.state | Out-Null; Set-Content .claude/hooks/.state/allow-gate-edit.txt "$rel"
"@)
exit 2
