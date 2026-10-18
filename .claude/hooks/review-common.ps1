# Shared by review-prepare.ps1, review-mark.ps1 and review-before-commit.ps1 (ADR-0010).
# A review is tied to the exact staged diff by a short SHA-256 of its text, so a review of one diff
# can never unlock the commit of a different one.
$script:StateDir = Join-Path $PSScriptRoot '.state'
$script:ReviewDir = Join-Path $script:StateDir 'reviews'

function Get-DiffHash([string]$text) {
    $bytes = [Text.Encoding]::UTF8.GetBytes($text)
    $sha = [Security.Cryptography.SHA256]::Create()
    (([BitConverter]::ToString($sha.ComputeHash($bytes))) -replace '-', '').ToLower().Substring(0, 16)
}

# Runs git and returns its stdout decoded as UTF-8. PowerShell 5.1 would decode a native command's output with
# the console code page, so the same diff could hash differently under the Bash/PowerShell tool and the hook
# runner; a process with an explicit encoding does not depend on the console at all.
function Invoke-GitUtf8([string]$root, [string[]]$gitArgs) {
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = 'git'
    $psi.Arguments = (@('-C', "`"$root`"", '-c', 'core.quotepath=false') + ($gitArgs | ForEach-Object { if ($_ -match '\s') { "`"$_`"" } else { $_ } })) -join ' '
    $psi.UseShellExecute = $false
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.StandardOutputEncoding = New-Object Text.UTF8Encoding $false
    $psi.CreateNoWindow = $true
    $proc = [System.Diagnostics.Process]::Start($psi)
    $errTask = $proc.StandardError.ReadToEndAsync()   # drained at the same time: a full stderr pipe would otherwise stall git and time the hook out
    $text = $proc.StandardOutput.ReadToEnd()
    $proc.WaitForExit()
    $null = $errTask.Result
    $text.Replace("`r`n", "`n").TrimEnd("`n")
}

# Text of what a commit would record. --no-color and --no-ext-diff keep it stable; git normalises line endings
# in the index, so the same staged content always gives the same hash.
function Get-StagedDiff([string]$root) {
    $text = Invoke-GitUtf8 $root @('diff', '--cached', '--no-color', '--no-ext-diff')
    [pscustomobject]@{ Text = $text; Hash = (Get-DiffHash $text); Empty = ($text.Trim().Length -eq 0) }
}

function Get-ReviewMarker([string]$hash) {
    $file = Join-Path $script:ReviewDir "$hash.txt"
    if (-not (Test-Path $file)) { return $null }
    $lines = Get-Content $file
    [pscustomobject]@{ Verdict = ($lines | Where-Object { $_ -like 'verdict:*' } | Select-Object -First 1) -replace '^verdict:\s*', ''; File = $file; Lines = $lines }
}
