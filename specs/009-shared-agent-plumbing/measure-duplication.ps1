# Measures production-code duplication between the Requirement and Architect agents.
# Method (research.md R6): per agent, read every *.cs file outside bin and obj; trim lines;
# drop blank lines, comment lines, "using" and "namespace" lines and lines made only of
# braces, brackets, parentheses, commas or semicolons; remove the agent-specific words; then
# count the multiset intersection of the two agents' remaining lines.
# Changes no file. With no arguments it measures production code under src of this checkout.
# -Scope Tests applies the same normalisation to the two agents' test projects (reported
# separately); -Root measures another checkout of the repository, for example a worktree of an
# earlier commit.

param(
    [ValidateSet('Src', 'Tests')] [string] $Scope = 'Src',
    [string] $Root = (Join-Path $PSScriptRoot '..\..')
)

$ErrorActionPreference = 'Stop'
$repoRoot = Resolve-Path $Root
$words = 'Requirements', 'Requirement', 'Architect', 'Design', 'Draft', 'Basis'

function Get-NormalisedLines([string] $folder) {
    $lines = New-Object System.Collections.Generic.List[string]
    $files = Get-ChildItem -Path $folder -Filter '*.cs' -Recurse |
        Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' } |
        Sort-Object FullName
    foreach ($file in $files) {
        foreach ($raw in Get-Content -LiteralPath $file.FullName) {
            $line = $raw.Trim()
            if ($line.Length -eq 0) { continue }
            if ($line.StartsWith('//')) { continue }
            if ($line.StartsWith('using ') -or $line.StartsWith('namespace ')) { continue }
            if ($line -match '^[\{\}\[\]\(\),;\s]+$') { continue }
            foreach ($word in $words) { $line = $line.Replace($word, '') }
            $lines.Add($line)
        }
    }
    return , $lines
}

if ($Scope -eq 'Tests') {
    $requirementFolder = 'tests\AgenticChangeOps.Agents.Requirement.Tests'
    $architectFolder = 'tests\AgenticChangeOps.Agents.Architect.Tests'
} else {
    $requirementFolder = 'src\AgenticChangeOps.Agents.Requirement'
    $architectFolder = 'src\AgenticChangeOps.Agents.Architect'
}

$requirement = Get-NormalisedLines (Join-Path $repoRoot $requirementFolder)
$architect = Get-NormalisedLines (Join-Path $repoRoot $architectFolder)

$counts = @{}
foreach ($line in $architect) {
    if ($counts.ContainsKey($line)) { $counts[$line]++ } else { $counts[$line] = 1 }
}

$shared = 0
foreach ($line in $requirement) {
    if ($counts.ContainsKey($line) -and $counts[$line] -gt 0) {
        $shared++
        $counts[$line]--
    }
}

$smaller = [Math]::Min($requirement.Count, $architect.Count)
$percent = if ($smaller -gt 0) { [Math]::Round(100.0 * $shared / $smaller, 1) } else { 0 }

"requirement_lines=$($requirement.Count)"
"architect_lines=$($architect.Count)"
"shared_lines=$shared"
"shared_percent_of_smaller=$percent"
