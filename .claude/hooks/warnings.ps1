# Shared by build-on-stop.ps1 and update-warning-baseline.ps1.
# A warning's key is "relative/file.cs|CODE|message": no line numbers, so editing elsewhere in a
# file doesn't make an old warning look new.
function Get-WarningKeys([string]$output, [string]$root) {
    $output -split "`r?`n" |
        Where-Object { $_ -match '^(?<file>.+?)\(\d+,\d+\): warning (?<code>[A-Z]+\d+): (?<msg>.+?)( \[[^\]]+\])?$' } |
        ForEach-Object {
            $file = $Matches.file.Replace($root, '').TrimStart('\', '/').Replace('\', '/')
            "$file|$($Matches.code)|$($Matches.msg.Trim())"
        } |
        Sort-Object -Unique
}
