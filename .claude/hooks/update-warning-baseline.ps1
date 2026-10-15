# Run by hand: records today's build warnings as the accepted baseline for build-on-stop.ps1.
#   powershell -NoProfile -ExecutionPolicy Bypass -File .claude/hooks/update-warning-baseline.ps1
# After Week 2 A2 fixes the warnings, run it again: the baseline becomes empty and any warning fails.
$ErrorActionPreference = 'Continue'
$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
. (Join-Path $PSScriptRoot 'warnings.ps1')

$output = & dotnet build (Join-Path $root 'NZWalks/NZWalks.sln') -nologo -v q --no-incremental 2>&1 | Out-String
if ($LASTEXITCODE -ne 0) { Write-Error "Build failed; fix the errors before taking a baseline."; exit 1 }

$keys = @(Get-WarningKeys $output $root)
[IO.File]::WriteAllText((Join-Path $PSScriptRoot 'warning-baseline.txt'), ($keys -join "`n"), (New-Object Text.UTF8Encoding $false))
Write-Output "Baseline: $($keys.Count) warning(s) written to .claude/hooks/warning-baseline.txt"
