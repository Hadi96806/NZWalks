# PreToolUse (Edit|Write|MultiEdit): refuse content that looks like a secret.
# The repo is public and secrets live in user-secrets (ADR-0003). Exit 2 blocks the edit
# and sends stderr back to Claude.
$ErrorActionPreference = 'Continue'  # native tools write warnings to stderr; don't treat them as fatal
$payload = [Console]::In.ReadToEnd() | ConvertFrom-Json
$tool = $payload.tool_input
$path = [string]$tool.file_path

$texts = @()
if ($tool.content)    { $texts += [string]$tool.content }
if ($tool.new_string) { $texts += [string]$tool.new_string }
if ($tool.edits)      { $texts += $tool.edits | ForEach-Object { [string]$_.new_string } }
$text = $texts -join "`n"
if (-not $text) { exit 0 }

$rules = @(
    @{ Name = 'connection-string password'; Pattern = '(?i)\b(password|pwd)=[^;"''\s<>{}]+' },
    @{ Name = 'sa login';                   Pattern = '(?i)\buser\s?id\s*=\s*sa\b' },
    @{ Name = 'JWT signing key value';      Pattern = '(?i)"key"\s*:\s*"[^"]{16,}"' },
    @{ Name = 'private key block';          Pattern = '-----BEGIN [A-Z ]*PRIVATE KEY-----' }
)
# Long base64-looking strings are only suspicious outside source files (config, docs, scripts).
if ($path -notmatch '\.(cs|csproj|sln)$') {
    $rules += @{ Name = 'long key-like string'; Pattern = '(?<![A-Za-z0-9+/])[A-Za-z0-9+/]{40,}={0,2}(?![A-Za-z0-9+/])' }
}

$hits = $rules | Where-Object { $text -match $_.Pattern } | ForEach-Object { $_.Name }
if (-not $hits) { exit 0 }

[Console]::Error.WriteLine(@"
BLOCKED by .claude/hooks/guard-secrets.ps1: the edit to $path looks like it contains a secret ($($hits -join ', ')).
This repo is public. Connection strings and Jwt:Key live in user-secrets (ADR-0003); committed files keep empty placeholders.
Tell the user what was blocked and why. Do not retry with the value disguised. If it is a real secret, give them the
'dotnet user-secrets set "<key>" "<value>" --project NZWalks.API' command to run themselves. If it is a false positive,
say so and ask them how to proceed.
"@)
exit 2
