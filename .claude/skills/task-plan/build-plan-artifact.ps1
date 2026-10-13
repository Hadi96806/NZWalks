# Builds a plan artifact page from the shared template and a plan's parts file.
# Usage: powershell -File build-plan-artifact.ps1 -Parts docs/plans/<name>.parts.html -Out docs/plans/<name>.html
# The parts file holds five sections, each wrapped in <!--NAME--> ... <!--/NAME-->:
#   TITLE, HEADER, NOTES_TOP, NOTES_BOTTOM, DATA (the JS consts PLAN, KEY, CONTEXT, STEPS).
param([Parameter(Mandatory)][string]$Parts, [Parameter(Mandatory)][string]$Out)
$ErrorActionPreference = 'Stop'
$template = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'plan-artifact-template.html'))
$src = [IO.File]::ReadAllText((Resolve-Path $Parts))
foreach ($name in 'TITLE', 'HEADER', 'NOTES_TOP', 'NOTES_BOTTOM', 'DATA') {
    $m = [regex]::Match($src, "(?s)<!--$name-->(.*?)<!--/$name-->")
    if (-not $m.Success) { throw "Parts file is missing the $name section." }
    $template = $template.Replace("__${name}__", $m.Groups[1].Value.Trim())
}
if ($template -match '__[A-Z_]+__') { throw "Unfilled placeholder: $($Matches[0])" }
[IO.File]::WriteAllText((Join-Path (Get-Location) $Out), $template, (New-Object Text.UTF8Encoding $false))
"Built $Out"
