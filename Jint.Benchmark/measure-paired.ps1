<#
.SYNOPSIS
    Interleaved paired A/B measurement. The gating protocol for comparing two Jint builds.

.DESCRIPTION
    The problem this solves is not noise inside a run - BenchmarkDotNet already averages that away.
    It is the offset BETWEEN processes, which with LaunchCount=1 never reaches the reported error at
    all. Measured on the gating machine, that offset is ~3% at the median and ~10% at p90 even with
    the process pinned and the clock fixed, and it is dominated by a bistable ~1.9x slow mode that
    tiered compilation produces per process (TieredCompilation=0 collapses it from StdDev 1.95 to
    0.37 - at the cost of measuring code that never ships).

    Rather than disabling the runtime features that cause it, this measures around them. Each round
    runs BOTH builds once, and the order alternates every round so that whatever penalises the
    second run in a pair - thermal state, machine drift, run order - lands on each build equally
    often. The statistic is then the per-round DIFFERENCE, not two independently estimated means.

    This is the "duet"/paired design from the benchmarking literature (Bulej et al., Duet
    Benchmarking, arXiv:2001.05811), which reports 2.3-12.5x accuracy improvements on JVM workloads
    over measuring the two variants separately. It is adapted to interleave in time rather than run
    concurrently, because on a dedicated machine concurrency would introduce contention instead of
    cancelling it.

    Every runtime feature stays production-default: tiered compilation on, dynamic PGO on. The
    per-process lottery is sampled rather than suppressed.

.PARAMETER Baseline
    Path to the baseline worktree (the branch being compared against).

.PARAMETER Candidate
    Path to the candidate worktree (the change under test).

.PARAMETER Filter
    BenchmarkDotNet filter(s), e.g. '*SunSpiderBenchmark*'.

.PARAMETER Rounds
    Number of A/B pairs. 8 is a reasonable floor; the paired CI narrows roughly as 1/sqrt(Rounds).

.EXAMPLE
    ./measure-paired.ps1 -Baseline D:\Work\jint -Candidate D:\Work\jint.myfix -Filter '*ForOfArrayBenchmark*'
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string]   $Baseline,
    [Parameter(Mandatory)] [string]   $Candidate,
    [Parameter(Mandatory)] [string[]] $Filter,
    [int]    $Rounds = 8,
    [string] $OutputRoot = (Join-Path ([IO.Path]::GetTempPath()) "jint-paired-$(Get-Date -Format yyyyMMdd-HHmmss)")
)

$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Force -Path $OutputRoot | Out-Null

$env:JINT_BENCH_MODE      = 'stable'   # pinned + fixed clock; LaunchCount stays 1 per round
$env:JINT_BENCH_POWERPLAN = [Environment]::GetEnvironmentVariable('JINT_BENCH_POWERPLAN', 'User')

function Invoke-Side([string] $root, [string] $tag, [int] $round) {
    $proj = Join-Path $root 'Jint.Benchmark'
    $art  = Join-Path $OutputRoot "$tag-r$round"
    Push-Location $proj
    try {
        $a = @('run','-c','Release','--project','.','--','--filter') + $Filter +
             @('--artifacts', $art, '--launchCount','1')
        & dotnet @a *> (Join-Path $OutputRoot "$tag-r$round.log")
    } finally { Pop-Location }
    $art
}

# Parse every *-report.csv BenchmarkDotNet wrote, keyed by "Class.Method[params]".
function Read-Means([string] $artifactDir) {
    $rows = @{}
    Get-ChildItem (Join-Path $artifactDir 'results') -Filter '*-report.csv' -EA SilentlyContinue | ForEach-Object {
        $cls = ($_.BaseName -replace '-report$','') -replace '^Jint\.Benchmark\.',''
        Import-Csv $_.FullName | ForEach-Object {
            $r = $_
            $paramCols = $r.PSObject.Properties.Name | Where-Object {
                $_ -notin 'Method','Job','Mean','Error','StdDev','Median','Min','Max','Gen0','Gen1','Gen2','Allocated','Rank','StdErr','MValue' -and
                $r.$_ -and $r.$_ -ne 'Default'
            }
            $suffix = if ($paramCols) { '[' + (($paramCols | ForEach-Object { $r.$_ }) -join ',') + ']' } else { '' }
            $key = "$cls.$($r.Method)$suffix"

            # "20.68 ms" / "1.234 us" / "987.6 ns" -> nanoseconds
            if ($r.Mean -match '^\s*([\d.,]+)\s*(ns|us|ms|s)\s*$') {
                $v = [double]($Matches[1] -replace ',','')
                $ns = switch ($Matches[2]) { 'ns' {$v} 'us' {$v*1e3} 'ms' {$v*1e6} 's' {$v*1e9} }
                $rows[$key] = $ns
            }
        }
    }
    $rows
}

$paired = @{}   # key -> list of percentage differences, one per round

for ($r = 1; $r -le $Rounds; $r++) {
    # Alternate which side runs first so run-order penalty is shared equally.
    $baseFirst = ($r % 2) -eq 1
    Write-Host "round $r/$Rounds ($(if($baseFirst){'base,cand'}else{'cand,base'}))"

    if ($baseFirst) {
        $b = Read-Means (Invoke-Side $Baseline  'base' $r)
        $c = Read-Means (Invoke-Side $Candidate 'cand' $r)
    } else {
        $c = Read-Means (Invoke-Side $Candidate 'cand' $r)
        $b = Read-Means (Invoke-Side $Baseline  'base' $r)
    }

    foreach ($k in $b.Keys) {
        if (-not $c.ContainsKey($k)) { continue }
        if (-not $paired.ContainsKey($k)) { $paired[$k] = @() }
        $paired[$k] += 100.0 * ($c[$k] - $b[$k]) / $b[$k]
    }
}

# --- paired statistics ---------------------------------------------------------------------
# The per-round difference is the unit of observation. Reporting its median plus a percentile
# bootstrap CI avoids assuming normality, which a bimodal per-process distribution violates.

function Get-Median([double[]] $v) {
    $s = $v | Sort-Object; $n = $s.Count
    if ($n % 2) { $s[[int](($n-1)/2)] } else { ($s[$n/2-1] + $s[$n/2]) / 2 }
}

function Get-BootstrapCi([double[]] $v, [int] $iter = 5000) {
    $rng = [Random]::new(20260816)   # fixed seed: the report must be reproducible from the same data
    $meds = for ($i = 0; $i -lt $iter; $i++) {
        $s = for ($j = 0; $j -lt $v.Count; $j++) { $v[$rng.Next($v.Count)] }
        Get-Median ([double[]] $s)
    }
    $sorted = $meds | Sort-Object
    @($sorted[[int](0.025 * $iter)], $sorted[[int](0.975 * $iter)])
}

Write-Host ''
'{0,-52} {1,9} {2,20} {3,8} {4}' -f 'row', 'median %', '95% CI', 'sign', 'verdict'
'-' * 108

$report = foreach ($k in ($paired.Keys | Sort-Object)) {
    $d = [double[]] $paired[$k]
    if ($d.Count -lt 3) { continue }
    $med = Get-Median $d
    $ci  = Get-BootstrapCi $d
    $pos = ($d | Where-Object { $_ -gt 0 }).Count

    # A result counts only when the whole interval sits on one side of zero.
    $verdict = if ($ci[0] -gt 0) { 'SLOWER' } elseif ($ci[1] -lt 0) { 'FASTER' } else { 'no change' }

    '{0,-52} {1,9:+0.00;-0.00} {2,20} {3,8} {4}' -f
        $k.Substring(0, [Math]::Min(52, $k.Length)), $med,
        ("[{0:+0.00;-0.00}, {1:+0.00;-0.00}]" -f $ci[0], $ci[1]), "$pos/$($d.Count)", $verdict
}
$report

Write-Host ''
"rounds: $Rounds   raw data: $OutputRoot"
'A row is only a regression when its 95% CI excludes zero. A wide CI means the measurement was'
'not precise enough to decide - add rounds rather than reading the median on its own.'
