# Stop: before Claude hands back, make sure there is no conflict left and the solution builds.
# Exit 2 + stderr keeps Claude working with the errors in hand. Builds only when source changed
# since the last successful build, so plain chat turns stay fast.
$ErrorActionPreference = 'Continue'  # native tools write warnings to stderr; don't treat them as fatal
$payload = [Console]::In.ReadToEnd() | ConvertFrom-Json

# Second pass after Claude already reacted (fixed, or asked the user): let it stop, or it loops.
if ($payload.stop_hook_active) { exit 0 }

$root = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
Set-Location $root

$conflicted = @(git diff --name-only --diff-filter=U 2>$null | Where-Object { $_ })
if ($conflicted.Count -gt 0) {
    [Console]::Error.WriteLine("GIT CONFLICT still open in: $($conflicted -join ', '). Report it to the user and resolve or ask, as check-conflicts.ps1 describes. Skipping the build until it is resolved.")
    exit 2
}

$api = Join-Path $root 'NZWalks/NZWalks.API'
$stateDir = Join-Path $root '.claude/hooks/.state'
$stamp = Join-Path $stateDir 'last-good-build.txt'
New-Item -ItemType Directory -Force $stateDir | Out-Null

$latest = Get-ChildItem $api -Recurse -File -Include *.cs, *.csproj, *.json |
    Where-Object { $_.FullName -notmatch '\\(obj|bin|Logs|Properties\\PublishProfiles)\\' } |
    Measure-Object -Property LastWriteTimeUtc -Maximum
$latestTicks = $latest.Maximum.Ticks
if ((Test-Path $stamp) -and [long](Get-Content $stamp) -ge $latestTicks) { exit 0 }

$output = & dotnet build (Join-Path $root 'NZWalks/NZWalks.sln') -nologo -v q 2>&1 | Out-String
$code = $LASTEXITCODE

if ($code -eq 0) {
    Set-Content $stamp $latestTicks
    exit 0
}

$errors = @($output -split "`r?`n" | Where-Object { $_ -match ': error ' } | Select-Object -Unique)

# The app running in Visual Studio locks the DLLs: that's not a code error, just tell the user.
if ($errors.Count -gt 0 -and -not ($errors | Where-Object { $_ -notmatch 'MSB3021|MSB3026|MSB3027' })) {
    $note = 'Build hook: could not verify the build because NZWalks.API is running (output DLLs locked). Stop the app and build to check.'
    Write-Output (@{ systemMessage = $note } | ConvertTo-Json -Compress)
    exit 0
}

$shown = ($errors | Select-Object -First 25) -join "`n"
if (-not $shown) { $shown = ($output -split "`r?`n" | Select-Object -Last 25) -join "`n" }
[Console]::Error.WriteLine(@"
BUILD FAILED (.claude/hooks/build-on-stop.ps1): dotnet build NZWalks.sln exited $code with $($errors.Count) error(s):
$shown

Report this to the user first: which errors, in which files, and what caused them.
- If the cause is clear and the fix is mechanical (a typo, missing using, wrong overload you introduced), fix it,
  rebuild, and say exactly what you changed.
- If fixing it means a design choice, touches code the user wrote and is still working on, or comes from a pull,
  do not guess: explain the options and ask the user how to resolve it.
"@)
exit 2
