# PreToolUse (Bash|PowerShell): the commit gate (ADR-0010). Blocks `git commit` until the read-only reviewer's
# outcome has been recorded for exactly the staged diff. Exit 2 blocks and sends stderr back to Claude.
# It proves a review was recorded for this diff, not that the review was done well; the user sees every report.
# It fails open: a crash or a missing tool lets the commit through rather than locking the user out.
$ErrorActionPreference = 'Continue'  # native tools write warnings to stderr; don't treat them as fatal
$payload = [Console]::In.ReadToEnd() | ConvertFrom-Json
$cmd = [string]$payload.tool_input.command
if (-not $cmd) { exit 0 }

# Quoted text (commit messages, -Note text, echo strings) is not part of the command: blank it out so words
# inside it are never read as git options or as a commit command. Heredoc bodies (bash <<'EOF' ... EOF) and
# PowerShell here-strings (@' ... '@) are multi-line messages that can hold any quote character, so they go first.
$noDocs = [regex]::Replace($cmd, '(?s)<<-?\s*([''"]?)(\w+)\1[^\r\n]*\r?\n.*?\r?\n[ \t]*\2(?=[\s)]|$)', 'HEREDOC')
$noDocs = [regex]::Replace($noDocs, '(?s)@''\r?\n.*?\r?\n''@|@"\r?\n.*?\r?\n"@', '""')
$bare = [regex]::Replace($noDocs, '"[^"]*"|''[^'']*''', '""')   # same pattern as $quoted below

# `git commit`, also as git.exe, with options before it (git -C <dir> commit, git -c k=v commit), or called by a quoted path.
$commitPattern = '(?i)(?<![\w.\-/\\])git(?:\.exe)?\s+(?:-\S+(?:\s+[^-\s]\S*)?\s+)*commit(?=\s|$)'
$quoted = '"[^"]*"|''[^'']*'''
$source = $bare
$m = [regex]::Match($bare, $commitPattern)
# A quoted path to git (& "C:\...\git.exe" commit) only counts when it starts a statement, so text that merely contains it is not a commit.
if (-not $m.Success) { $source = $noDocs; $m = [regex]::Match($noDocs, '(?i)(?:^|[;&|(])\s*&?\s*["''][^"'']*git(?:\.exe)?["'']\s+(?:-\S+\s+)*commit(?=\s|$)') }
if (-not $m.Success) { exit 0 }

$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
. (Join-Path $PSScriptRoot 'review-common.ps1')

# This hook reads the index when the command starts. A command that also changes the index before committing
# (git add x && git commit) would be judged against the old index, so staging must be its own earlier command.
$before = $source.Substring(0, $m.Index)
if ($before -match '(?i)(?<![\w.\-/\\])git(?:\.exe)?\s+(?:-\S+(?:\s+[^-\s]\S*)?\s+)*(add|stage|rm|mv|reset|restore|apply|stash|checkout|switch|update-index)(?=\s|$)') {
    [Console]::Error.WriteLine("BLOCKED by .claude/hooks/review-before-commit.ps1 (ADR-0010): this command changes what is staged and commits in one go, so the reviewer cannot have seen the diff that will be committed. Run the staging (git add <files>) as its own command first, then review that staged diff, then run the commit as a separate command.")
    exit 2
}

# What follows `commit` on the same command (up to the next ; | & or line end), as tokens.
$rest = [regex]::Replace($source.Substring($m.Index + $m.Length), $quoted, '""')
$rest = ($rest -split '[|;&\r\n]', 2)[0]
$tokens = @($rest -split '\s+' | Where-Object { $_ })

# Options that take a value in the next token, so that token is not a path.
$valueOptions = '-m', '-F', '-C', '-c', '--message', '--file', '--author', '--date', '--template', '--cleanup', '--reuse-message', '--reedit-message', '--fixup', '--squash'
$unsafe = $null
for ($i = 0; $i -lt $tokens.Count; $i++) {
    $t = $tokens[$i]
    if ($t -match '^\d*[<>]') { continue }                                       # a redirection (2>&1, > out.txt)
    if ($valueOptions -contains $t) { $i++; continue }                           # its value is the next token, even a blanked quoted one
    if ($t -eq '--') { $unsafe = "'--' (paths follow)"; break }
    if ($t -match '^--(all|only|include)$') { $unsafe = $t; break }
    if ($t -match '^--') { continue }
    if ($t -match '^-' -and $t -notmatch '^-[A-Za-z]+$') { continue }            # an option glued to a value, e.g. -m"text"
    if ($t -match '^-[A-Za-z]+$') {
        $letters = $t.Substring(1)
        if ($letters -match '[aoi]') { $unsafe = $t; break }
        if ($letters.EndsWith('m') -or $letters.EndsWith('F') -or $letters.EndsWith('C')) { $i++ }
        continue
    }
    $unsafe = "the path '$t'"; break
}
if ($unsafe) {
    [Console]::Error.WriteLine("BLOCKED by .claude/hooks/review-before-commit.ps1 (ADR-0010): this commit uses $unsafe, which commits files by looking at the working tree instead of the staged diff, so the reviewer could not have seen exactly what is committed. Stage the files by name (git add <files>), review the staged diff, then run a plain 'git commit -m ...' with no -a/-o/-i or path arguments.")
    exit 2
}

$staged = Get-StagedDiff $root
if ($staged.Empty) {
    # With nothing staged git refuses a normal commit by itself, but --amend and --allow-empty still succeed and would rewrite history unreviewed.
    if ($tokens -contains '--amend' -or $tokens -contains '--allow-empty') {
        [Console]::Error.WriteLine("BLOCKED by .claude/hooks/review-before-commit.ps1 (ADR-0010): 'git commit --amend/--allow-empty' with nothing staged would rewrite or add history that no reviewer has seen. Ask the user before doing this.")
        exit 2
    }
    exit 0
}

$marker = Get-ReviewMarker $staged.Hash
if ($marker -and $marker.Verdict -in @('PASS', 'ACCEPTED')) { exit 0 }

$why = if (-not $marker) { "No review is recorded for the staged diff ($($staged.Hash))." }
       else { "The recorded review for this diff ($($staged.Hash)) is BLOCK: a High finding is unresolved." }
[Console]::Error.WriteLine(@"
BLOCKED by .claude/hooks/review-before-commit.ps1 (ADR-0010): $why
Before committing:
1. powershell -NoProfile -ExecutionPolicy Bypass -File .claude/hooks/review-prepare.ps1      (writes the staged diff to a file and prints its hash)
2. Run the nzwalks-reviewer subagent with the diff file path and the hash. It is read-only; it can only Read, Grep and Glob.
3. Show the user the report. If it lists compromises, ask the user to confirm each one before anything is written to docs.
4. powershell -NoProfile -ExecutionPolicy Bypass -File .claude/hooks/review-mark.ps1 -Hash <hash> -Verdict PASS|BLOCK|ACCEPTED -Note "<text>"
   - PASS only if the report has no High finding.
   - A High finding blocks the commit: fix it and review again, or ask the user. Record ACCEPTED only after the user has seen the
     finding and said to go ahead, and put what they decided in -Note.
Do not run review-mark.ps1 without having run the reviewer, and never record PASS or ACCEPTED on your own judgement.
If you change what is staged, the hash changes and the review must be repeated.
"@)
exit 2
