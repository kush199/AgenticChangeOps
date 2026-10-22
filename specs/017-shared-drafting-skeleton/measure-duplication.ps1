# Measures production-code duplication between the Requirement, Architect and Developer agents.
# Method (extends specs/009-shared-agent-plumbing/measure-duplication.ps1): per agent, read every
# *.cs file outside bin and obj; trim lines; drop blank lines, comment lines, "using" and
# "namespace" lines and lines made only of braces, brackets, parentheses, commas or semicolons;
# remove the agent-specific words; then count the multiset intersection of each pair of agents'
# remaining lines. The headline is the sum of the three pairwise figures; the lines present in
# all three agents are reported as well. With -Agents Two only the Requirement and Architect
# agents are measured, which reproduces the slice 009 figures on its commit.
# Changes no file. With no arguments it measures production code under src of this checkout.
# -Scope Tests applies the same normalisation to the agents' test projects (reported
# separately); -Root measures another checkout of the repository, for example a worktree of an
# earlier commit.

param(
    [ValidateSet('Src', 'Tests')] [string] $Scope = 'Src',
    [ValidateSet('Two', 'Three')] [string] $Agents = 'Three',
    [string] $Root = (Join-Path $PSScriptRoot '..\..')
)

$ErrorActionPreference = 'Stop'
$repoRoot = Resolve-Path $Root
$words = 'Requirements', 'Requirement', 'Architect', 'Developer', 'Proposals', 'Proposal', 'Design', 'Draft', 'Basis'

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

function Get-Counts($lines) {
    $counts = @{}
    foreach ($line in $lines) {
        if ($counts.ContainsKey($line)) { $counts[$line]++ } else { $counts[$line] = 1 }
    }
    return $counts
}

function Get-SharedCount($lines, $otherCounts) {
    $remaining = @{}
    foreach ($key in $otherCounts.Keys) { $remaining[$key] = $otherCounts[$key] }
    $shared = 0
    foreach ($line in $lines) {
        if ($remaining.ContainsKey($line) -and $remaining[$line] -gt 0) {
            $shared++
            $remaining[$line]--
        }
    }
    return $shared
}

if ($Scope -eq 'Tests') {
    $folders = [ordered]@{
        requirement = 'tests\AgenticChangeOps.Agents.Requirement.Tests'
        architect   = 'tests\AgenticChangeOps.Agents.Architect.Tests'
        developer   = 'tests\AgenticChangeOps.Agents.Developer.Tests'
    }
} else {
    $folders = [ordered]@{
        requirement = 'src\AgenticChangeOps.Agents.Requirement'
        architect   = 'src\AgenticChangeOps.Agents.Architect'
        developer   = 'src\AgenticChangeOps.Agents.Developer'
    }
}
if ($Agents -eq 'Two') { $folders.Remove('developer') }

$lines = [ordered]@{}
foreach ($name in $folders.Keys) {
    $lines[$name] = Get-NormalisedLines (Join-Path $repoRoot $folders[$name])
    "$($name)_lines=$($lines[$name].Count)"
}

$names = @($lines.Keys)
$total = 0
for ($i = 0; $i -lt $names.Count; $i++) {
    for ($j = $i + 1; $j -lt $names.Count; $j++) {
        $a = $names[$i]; $b = $names[$j]
        $shared = Get-SharedCount $lines[$a] (Get-Counts $lines[$b])
        $smaller = [Math]::Min($lines[$a].Count, $lines[$b].Count)
        $percent = if ($smaller -gt 0) { [Math]::Round(100.0 * $shared / $smaller, 1) } else { 0 }
        "shared_$($a)_$($b)=$shared"
        "shared_percent_of_smaller_$($a)_$($b)=$percent"
        $total += $shared
    }
}
"shared_pairwise_total=$total"

if ($names.Count -eq 3) {
    # Lines present in all three: multiset intersection of the first two, then with the third.
    $first = Get-Counts $lines[$names[0]]
    $second = Get-Counts $lines[$names[1]]
    $third = Get-Counts $lines[$names[2]]
    $all = 0
    foreach ($key in $first.Keys) {
        if ($second.ContainsKey($key) -and $third.ContainsKey($key)) {
            $all += [Math]::Min($first[$key], [Math]::Min($second[$key], $third[$key]))
        }
    }
    "shared_all_three=$all"
}
