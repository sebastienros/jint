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

    READING BENCHMARKDOTNET'S CSV. Two details of that format have silently destroyed whole runs,
    so both are now pinned by -SelfTest:

      * The Mean cell's unit is BDN's own TimeUnit name, and the microsecond one is a GREEK SMALL
        LETTER MU (U+03BC), not an ASCII "u" - a regex accepting only ns|us|ms|s parses ZERO rows
        and prints an empty table. Both mu spellings (U+03BC and the U+00B5 MICRO SIGN) are
        accepted, and deliberately NOT through a PowerShell 'switch': switch compares
        case-insensitively and ToUpper folds both characters to GREEK CAPITAL MU, so listing them
        as two branches makes BOTH fire and the "value" becomes an Object[] that explodes on the
        next arithmetic. The unit table is an ordinal dictionary for exactly that reason.
      * A row's identity is Class.Method plus its [Params]. Every other CSV column is a job
        characteristic (~40 of them: Affinity, PowerPlanMode, InvocationCount, ...) or a
        baseline-relative statistic (Ratio, RatioSD, Alloc Ratio). Those vary between processes -
        InvocationCount is auto-selected per run, ratios move with every round - so folding them
        into the key silently unpairs rows. Only columns outside NonParameterColumns are treated
        as parameters.

    FAILING LOUDLY. A measurement that reports nothing must never exit 0. A round whose side parsed
    no rows aborts immediately naming the artifacts directory and log; at the end each row that
    paired in fewer than every round is warned about, and the script exits non-zero when any row
    paired in fewer than half the rounds, when a row was seen on only one side, or when the report
    table is empty.

.PARAMETER Baseline
    Path to the baseline worktree (the branch being compared against).

.PARAMETER Candidate
    Path to the candidate worktree (the change under test).

.PARAMETER Filter
    BenchmarkDotNet filter(s), e.g. '*SunSpiderBenchmark*'.

.PARAMETER Rounds
    Number of A/B pairs. 8 is a reasonable floor; the paired CI narrows roughly as 1/sqrt(Rounds).

.PARAMETER SelfTest
    Run the parser and the pairing/statistics pipeline against inline CSV fixtures that mirror
    today's BenchmarkDotNet output, print the results and exit non-zero on any failure. Runs no
    benchmarks and needs no worktrees - use it after touching this script.

.EXAMPLE
    ./measure-paired.ps1 -Baseline D:\Work\jint -Candidate D:\Work\jint.myfix -Filter '*ForOfArrayBenchmark*'

.EXAMPLE
    ./measure-paired.ps1 -SelfTest
#>
[CmdletBinding(DefaultParameterSetName = 'Measure')]
param(
    [Parameter(Mandatory, ParameterSetName = 'Measure')] [string]   $Baseline,
    [Parameter(Mandatory, ParameterSetName = 'Measure')] [string]   $Candidate,
    [Parameter(Mandatory, ParameterSetName = 'Measure')] [string[]] $Filter,
    [Parameter(ParameterSetName = 'Measure')] [int]    $Rounds = 8,
    [Parameter(ParameterSetName = 'Measure')] [string] $OutputRoot = (Join-Path ([IO.Path]::GetTempPath()) "jint-paired-$(Get-Date -Format yyyyMMdd-HHmmss)"),
    [Parameter(Mandatory, ParameterSetName = 'SelfTest')] [switch] $SelfTest
)

$ErrorActionPreference = 'Stop'

# --- BenchmarkDotNet CSV parsing -----------------------------------------------------------

# BDN's TimeUnit names, ordinal-keyed. Ordinal is load-bearing: a case-insensitive lookup (which
# is what a plain hashtable and 'switch' both do) folds U+00B5 and U+03BC onto one another.
$script:UnitToNanoseconds = [System.Collections.Generic.Dictionary[string, double]]::new([StringComparer]::Ordinal)
$script:UnitToNanoseconds['ns'] = 1.0
$script:UnitToNanoseconds['us'] = 1e3          # both mu spellings are normalised to 'us' first
$script:UnitToNanoseconds['ms'] = 1e6
$script:UnitToNanoseconds['s']  = 1e9
$script:UnitToNanoseconds['m']  = 6e10
$script:UnitToNanoseconds['h']  = 3.6e12
$script:UnitToNanoseconds['d']  = 8.64e13

# Everything BDN can emit that is NOT a [Params] value: the descriptor, the job characteristics
# (Job/Run/Env/Gc/Infrastructure/Accuracy modes), the statistics, the diagnoser metrics and the
# baseline-relative columns. Enumerated generously - a name that BDN never emits costs nothing,
# a name that is missing silently unpairs rows. Over-exclusion cannot pass silently either: two
# CSV rows collapsing onto one key is a hard error (see Read-Means).
$script:NonParameterColumns = [System.Collections.Generic.HashSet[string]]::new(
    [string[]] @(
        # descriptor
        'Method', 'Job', 'Type', 'Namespace', 'Categories', 'Description', 'Id', 'Baseline', 'IsDefault',
        # run mode
        'RunStrategy', 'LaunchCount', 'WarmupCount', 'MinWarmupIterationCount', 'MaxWarmupIterationCount',
        'IterationTime', 'IterationCount', 'MinIterationCount', 'MaxIterationCount', 'InvocationCount',
        'UnrollFactor', 'MemoryRandomization',
        # environment mode
        'Affinity', 'EnvironmentVariables', 'Jit', 'LargeAddressAware', 'Platform', 'PowerPlanMode', 'Runtime',
        # GC mode
        'AllowVeryLargeObjects', 'Concurrent', 'CpuGroups', 'Force', 'HeapAffinitizeMask', 'HeapCount',
        'NoAffinitize', 'RetainVm', 'Server',
        # infrastructure mode
        'Arguments', 'BuildConfiguration', 'Clock', 'EngineFactory', 'NuGetReferences', 'Toolchain',
        'IsMutator', 'ArtifactsPath',
        # accuracy mode
        'AnalyzeLaunchVariance', 'EvaluateOverhead', 'MaxAbsoluteError', 'MaxRelativeError',
        'MinInvokeCount', 'MinIterationTime', 'OutlierMode',
        # statistics
        'Mean', 'Error', 'StdDev', 'StdErr', 'Median', 'Min', 'Q1', 'Q3', 'Max', 'Op/s', 'MValue',
        'Iterations', 'Skewness', 'Kurtosis', 'ConfidenceInterval', 'Rank',
        # diagnoser metrics
        'Gen0', 'Gen1', 'Gen2', 'Allocated', 'Allocated native memory', 'Native memory leak',
        'Code Size', 'Method Size', 'Completed Work Items', 'Lock Contentions', 'Exceptions'
    ),
    [StringComparer]::OrdinalIgnoreCase)

# A CSV column carries a [Params] value only if it is none of the above and does not match one of
# the shapes BDN generates for derived columns: anything baseline-relative ("Ratio", "RatioSD",
# "Alloc Ratio", "<metric> Ratio"), the legacy "Gen 0" spelling, and the per-operation columns the
# hardware-counter diagnosers add ("BranchInstructions/Op", "Op/s", ...).
function Test-IsParameterColumn([string] $name) {
    if ([string]::IsNullOrWhiteSpace($name)) { return $false }
    if ($script:NonParameterColumns.Contains($name)) { return $false }
    if ($name -match '(?i)ratio')      { return $false }
    if ($name -match '(?i)/\s*(op|s)$'){ return $false }
    if ($name -match '(?i)^gen\s*\d+$'){ return $false }
    return $true
}

# "20.68 ms" / "2.204 <mu>s" / "987.6 ns" / "1,234.5 ns" -> nanoseconds; $null when the cell is not
# a measurement at all (BDN writes "NA" for a benchmark that failed to run).
function ConvertTo-Nanoseconds([string] $mean) {
    if ($mean -notmatch '^\s*([\d.,]+)\s*([a-zA-Z\u00B5\u03BC]+)\s*$') { return $null }
    $number = $Matches[1] -replace ',', ''
    $unit   = $Matches[2] -replace '[\u00B5\u03BC]', 'u'      # MICRO SIGN / GREEK SMALL LETTER MU
    if (-not $script:UnitToNanoseconds.ContainsKey($unit)) { return $null }
    $value = 0.0
    if (-not [double]::TryParse($number, [Globalization.NumberStyles]::Float,
                                [Globalization.CultureInfo]::InvariantCulture, [ref] $value)) { return $null }
    return $value * $script:UnitToNanoseconds[$unit]
}

function Get-RowKey($row, [string] $class) {
    $paramCols = $row.PSObject.Properties.Name | Where-Object {
        (Test-IsParameterColumn $_) -and $row.$_ -and $row.$_ -ne 'Default'
    }
    $suffix = if ($paramCols) { '[' + (($paramCols | ForEach-Object { $row.$_ }) -join ',') + ']' } else { '' }
    return "$class.$($row.Method)$suffix"
}

<#
    Parse every *-report.csv BenchmarkDotNet wrote under $artifactDir, keyed by
    "Class.Method[params]". Returns an object rather than a bare hashtable so the caller can tell
    "nothing ran" from "everything ran and nothing parsed":

        .Rows      hashtable key -> mean in nanoseconds
        .Unparsed  distinct Mean cells that were not measurements (diagnostics for the abort)
        .Files     number of report CSVs found
#>
function Read-Means([string] $artifactDir) {
    $rows      = @{}
    $unparsed  = [System.Collections.Generic.List[string]]::new()
    $files     = 0
    $resultDir = Join-Path $artifactDir 'results'

    if (Test-Path -LiteralPath $resultDir) {
        Get-ChildItem $resultDir -Filter '*-report.csv' | ForEach-Object {
            $files++
            $file = $_.FullName
            $cls  = ($_.BaseName -replace '-report$', '') -replace '^Jint\.Benchmark\.', ''
            Import-Csv $file | ForEach-Object {
                $r  = $_
                $ns = ConvertTo-Nanoseconds $r.Mean
                if ($null -eq $ns) {
                    if (-not $unparsed.Contains([string] $r.Mean)) { $unparsed.Add([string] $r.Mean) }
                    return
                }
                $key = Get-RowKey $r $cls
                if ($rows.ContainsKey($key)) {
                    throw "two CSV rows map to the same key '$key' in $file. Either the run used more " +
                          'than one job, or a [Params] property is named like a column in ' +
                          '$script:NonParameterColumns and was excluded from the key.'
                }
                $rows[$key] = $ns
            }
        }
    }

    return [pscustomobject] @{ Rows = $rows; Unparsed = $unparsed.ToArray(); Files = $files }
}

# A side that parsed nothing is a total failure of the run, not a row to skip.
function Assert-ParsedRows($result, [string] $tag, [int] $round, [string] $artifactDir, [string] $logPath) {
    if ($result.Rows.Count -gt 0) { return }
    $detail = "round $round, side '$tag': parsed 0 benchmark rows from $($result.Files) report CSV(s)."
    if ($result.Unparsed.Count -gt 0) {
        $detail += " Mean cells that did not parse: '" + (($result.Unparsed | Select-Object -First 5) -join "', '") + "'."
    }
    throw "$detail Artifacts: $artifactDir. Log: $logPath"
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

<#
    Turn the per-round differences into the report table plus the two diagnostic lists that decide
    the exit code. Every row either produces a table line or an explanation of why it did not.
#>
function Invoke-PairedAnalysis {
    param(
        [hashtable] $Paired,
        [System.Collections.Generic.HashSet[string]] $SeenBase,
        [System.Collections.Generic.HashSet[string]] $SeenCandidate,
        [int] $Rounds
    )

    $lines    = [System.Collections.Generic.List[string]]::new()
    $warnings = [System.Collections.Generic.List[string]]::new()
    $failures = [System.Collections.Generic.List[string]]::new()

    $expected = [System.Collections.Generic.HashSet[string]]::new($SeenBase, [StringComparer]::Ordinal)
    $expected.UnionWith($SeenCandidate)

    foreach ($k in ($expected | Sort-Object)) {
        # Plain assignment, never an if-expression: the pipeline unrolls an empty array to $null.
        $d = [double[]] @()
        if ($Paired.ContainsKey($k)) { $d = [double[]] $Paired[$k] }
        $pairs = $d.Count

        if (-not ($SeenBase.Contains($k) -and $SeenCandidate.Contains($k))) {
            $side = if ($SeenBase.Contains($k)) { 'baseline' } else { 'candidate' }
            $failures.Add("$k was only ever seen on the $side side - the two runs do not agree on this row's key")
            continue
        }
        if ($pairs -lt $Rounds) {
            $warnings.Add("$k paired in $pairs of $Rounds rounds")
        }
        if ($pairs * 2 -lt $Rounds) {
            $failures.Add("$k paired in only $pairs of $Rounds rounds (fewer than half)")
        }
        if ($pairs -lt 3) {
            if ($pairs * 2 -ge $Rounds) { $failures.Add("$k has only $pairs pairs - too few to bootstrap") }
            continue
        }

        $med = Get-Median $d
        $ci  = Get-BootstrapCi $d
        $pos = ($d | Where-Object { $_ -gt 0 }).Count

        # A result counts only when the whole interval sits on one side of zero.
        $verdict = if ($ci[0] -gt 0) { 'SLOWER' } elseif ($ci[1] -lt 0) { 'FASTER' } else { 'no change' }

        $lines.Add(('{0,-52} {1,9:+0.00;-0.00} {2,20} {3,8} {4}' -f
            $k.Substring(0, [Math]::Min(52, $k.Length)), $med,
            ("[{0:+0.00;-0.00}, {1:+0.00;-0.00}]" -f $ci[0], $ci[1]), "$pos/$pairs", $verdict))
    }

    if ($lines.Count -eq 0) {
        $failures.Add('the report table is empty - no row produced a usable paired comparison')
    }

    return [pscustomobject] @{
        Lines    = $lines.ToArray()
        Warnings = $warnings.ToArray()
        Failures = $failures.ToArray()
    }
}

# --- self test -----------------------------------------------------------------------------

<#
    Exercises Read-Means and the pairing/statistics pipeline against CSV fixtures copied from real
    BenchmarkDotNet output (the ~40 job-characteristic columns, a Ratio block, Greek-mu units).
    Returns the number of failed assertions.
#>
function Invoke-SelfTest {
    $script:SelfTestFailed = 0

    function Assert-Equal($expected, $actual, [string] $what) {
        $ok = if ($null -eq $expected) { $null -eq $actual } else { ($null -ne $actual) -and ($expected -eq $actual) }
        if ($ok) {
            Write-Host "  ok   $what"
        } else {
            Write-Host "  FAIL $what : expected '$expected', got '$actual'"
            $script:SelfTestFailed++
        }
    }
    function Assert-True($condition, [string] $what) {
        if ($condition) { Write-Host "  ok   $what" }
        else { Write-Host "  FAIL $what"; $script:SelfTestFailed++ }
    }
    function Assert-Throws([scriptblock] $action, [string] $what) {
        try { & $action | Out-Null; Write-Host "  FAIL $what : no error was thrown"; $script:SelfTestFailed++ }
        catch { Write-Host "  ok   $what : $($_.Exception.Message.Split([Environment]::NewLine)[0])" }
    }

    # The mu characters never appear literally in this file: the source stays ASCII so no editor,
    # BOM or checkout setting can quietly turn the fixture into something else.
    $mu    = [char]0x03BC   # GREEK SMALL LETTER MU - what BDN actually writes
    $micro = [char]0x00B5   # MICRO SIGN - the other codepoint that renders identically

    # Verbatim header from a real Jint.Benchmark run (BDN + MemoryDiagnoser + a [Benchmark(Baseline)]).
    $headerWithRatio = 'Method,Job,AnalyzeLaunchVariance,EvaluateOverhead,MaxAbsoluteError,MaxRelativeError,MinInvokeCount,MinIterationTime,OutlierMode,Affinity,EnvironmentVariables,Jit,LargeAddressAware,Platform,PowerPlanMode,Runtime,AllowVeryLargeObjects,Concurrent,CpuGroups,Force,HeapAffinitizeMask,HeapCount,NoAffinitize,RetainVm,Server,Arguments,BuildConfiguration,Clock,EngineFactory,NuGetReferences,Toolchain,IsMutator,InvocationCount,IterationCount,IterationTime,LaunchCount,MaxIterationCount,MaxWarmupIterationCount,MemoryRandomization,MinIterationCount,MinWarmupIterationCount,RunStrategy,UnrollFactor,WarmupCount,Mean,Error,StdDev,Median,MValue,Ratio,RatioSD,Gen0,Gen1,Allocated,Alloc Ratio'
    $headerPlain     = 'Method,Job,AnalyzeLaunchVariance,EvaluateOverhead,MaxAbsoluteError,MaxRelativeError,MinInvokeCount,MinIterationTime,OutlierMode,Affinity,EnvironmentVariables,Jit,LargeAddressAware,Platform,PowerPlanMode,Runtime,AllowVeryLargeObjects,Concurrent,CpuGroups,Force,HeapAffinitizeMask,HeapCount,NoAffinitize,RetainVm,Server,Arguments,BuildConfiguration,Clock,EngineFactory,NuGetReferences,Toolchain,IsMutator,InvocationCount,IterationCount,IterationTime,LaunchCount,MaxIterationCount,MaxWarmupIterationCount,MemoryRandomization,MinIterationCount,MinWarmupIterationCount,RunStrategy,UnrollFactor,WarmupCount,Mean,Error,StdDev,Median,MValue,Gen0,Gen1,Allocated'
    $headerParams    = $headerPlain -replace ',Mean,', ',N,Mode,Mean,'

    # The job-characteristic block of a real data row; {0} is InvocationCount, which BDN
    # auto-selects per process and which therefore must never reach the key.
    $jobFields = 'Job-BWTMIA,False,Default,Default,Default,Default,Default,Default,00000000000000001111111111111100,Empty,RyuJit,Default,X64,8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c,.NET 10.0,False,False,False,True,Default,Default,False,False,False,Default,Default,Default,Default,Default,Default,Default,{0},Default,Default,1,Default,Default,Default,Default,Default,Default,16,Default'

    function New-RatioRow([string] $method, [string] $mean, [string] $ratio, [string] $invocationCount = 'Default') {
        "$method,$($jobFields -f $invocationCount),$mean,0.17 $($mu)s,0.15 $($mu)s,$mean,2.000,$ratio,0.00,0.0000,0.0000,1.22 KB,1.00"
    }
    function New-PlainRow([string] $method, [string] $mean, [string] $invocationCount = 'Default') {
        "$method,$($jobFields -f $invocationCount),$mean,0.0147 $($mu)s,0.0130 $($mu)s,$mean,2.000,0.7248,0.0267,11.88 KB"
    }
    function New-ParamsRow([string] $method, [string] $n, [string] $mode, [string] $mean) {
        "$method,$($jobFields -f 'Default'),$n,$mode,$mean,0.0147 $($mu)s,0.0130 $($mu)s,$mean,2.000,0.7248,0.0267,11.88 KB"
    }

    $dir = Join-Path ([IO.Path]::GetTempPath()) "jint-paired-selftest-$([Guid]::NewGuid().ToString('N').Substring(0, 8))"
    try {
        $results = Join-Path $dir 'results'
        New-Item -ItemType Directory -Force -Path $results | Out-Null
        function Write-Fixture([string] $name, [string[]] $lines) {
            [IO.File]::WriteAllText((Join-Path $results $name), ($lines -join "`r`n") + "`r`n",
                                    [Text.UTF8Encoding]::new($false))
        }

        Write-Host 'fixtures: field counts line up with the header'
        Assert-Equal ($headerWithRatio -split ',').Count ((New-RatioRow 'SyncCallLoop' "147.3 $($mu)s" '1.00') -split ',').Count 'ratio fixture row width'
        Assert-Equal ($headerPlain     -split ',').Count ((New-PlainRow 'Execute'      "2.204 $($mu)s")        -split ',').Count 'plain fixture row width'
        Assert-Equal ($headerParams    -split ',').Count ((New-ParamsRow 'Run' '1000' 'Fast' '20.68 ms')       -split ',').Count 'params fixture row width'

        Write-Fixture 'Jint.Benchmark.MinimalScriptBenchmark-report.csv' @(
            $headerPlain,
            (New-PlainRow 'Execute'              "2.204 $($mu)s"),
            (New-PlainRow 'Execute_ParsedScript' '987.6 ns'),
            (New-PlainRow 'ExecuteMicro'         "1.500 $($micro)s"),
            (New-PlainRow 'ExecuteBig'           '20.68 ms'),
            (New-PlainRow 'ExecuteSeparated'     '"1,234.5 ns"'))   # BDN quotes a thousands separator
        Write-Fixture 'Jint.Benchmark.AsyncAwaitBenchmark-report.csv' @(
            $headerWithRatio,
            (New-RatioRow 'SyncCallLoop'      "147.3 $($mu)s" '1.00' '4096'),
            (New-RatioRow 'AwaitResolvedLoop' "424.8 $($mu)s" '2.88' '4096'))
        Write-Fixture 'Jint.Benchmark.ParamsBenchmark-report.csv' @(
            $headerParams,
            (New-ParamsRow 'Run' '1000' 'Fast' '20.68 ms'),
            (New-ParamsRow 'Run' '2000' 'Slow' '41.36 ms'))

        $r = Read-Means $dir

        Write-Host ''
        Write-Host 'Read-Means: units'
        Assert-Equal 3 $r.Files 'report CSVs found'
        Assert-Equal 2204.0    $r.Rows['MinimalScriptBenchmark.Execute']              'GREEK SMALL LETTER MU microseconds -> ns'
        Assert-Equal 987.6     $r.Rows['MinimalScriptBenchmark.Execute_ParsedScript'] 'nanoseconds pass through'
        Assert-Equal 1500.0    $r.Rows['MinimalScriptBenchmark.ExecuteMicro']         'MICRO SIGN microseconds -> ns'
        Assert-Equal 20680000.0 $r.Rows['MinimalScriptBenchmark.ExecuteBig']          'milliseconds -> ns'
        Assert-Equal 1234.5    $r.Rows['MinimalScriptBenchmark.ExecuteSeparated']     'thousands separator stripped'
        Assert-True ($r.Rows['MinimalScriptBenchmark.Execute'] -is [double]) 'a mean is ONE double, not an Object[] (the switch/ToUpper trap)'
        Assert-Equal 2204.0 (ConvertTo-Nanoseconds "2.204 $($micro)s") 'both mu codepoints agree'
        Assert-Equal $null  (ConvertTo-Nanoseconds 'NA') 'a failed benchmark ("NA") is not a measurement'
        Assert-Equal $null  (ConvertTo-Nanoseconds '2.204 zz') 'an unknown unit is not a measurement'

        Write-Host ''
        Write-Host 'Read-Means: keys'
        Assert-Equal 9 $r.Rows.Count 'every fixture row parsed'
        Assert-True ($r.Rows.ContainsKey('AsyncAwaitBenchmark.SyncCallLoop')) 'job characteristics and Ratio columns stay out of the key'
        Assert-True ($r.Rows.ContainsKey('ParamsBenchmark.Run[1000,Fast]'))   '[Params] values stay in the key'
        Assert-True ($r.Rows.ContainsKey('ParamsBenchmark.Run[2000,Slow]'))   'the second [Params] row keeps its own key'
        Assert-Equal 0 (@($r.Rows.Keys | Where-Object { $_ -match '4096|RyuJit|\.NET|0000' }).Count) 'no key carries a job characteristic'

        # The same benchmarks re-run: InvocationCount auto-selects differently and every ratio moves.
        # The keys must be identical or the rows unpair, which is exactly what used to happen.
        $dir2 = Join-Path $dir 'round2'
        $results2 = Join-Path $dir2 'results'
        New-Item -ItemType Directory -Force -Path $results2 | Out-Null
        [IO.File]::WriteAllText((Join-Path $results2 'Jint.Benchmark.AsyncAwaitBenchmark-report.csv'),
            (@($headerWithRatio,
               (New-RatioRow 'SyncCallLoop'      "149.1 $($mu)s" '1.00' '8192'),
               (New-RatioRow 'AwaitResolvedLoop' "430.2 $($mu)s" '2.89' '8192')) -join "`r`n") + "`r`n",
            [Text.UTF8Encoding]::new($false))
        $r2 = Read-Means $dir2
        Assert-True ($r2.Rows.ContainsKey('AsyncAwaitBenchmark.SyncCallLoop')) 'a second run with a different InvocationCount and Ratio produces the same key'

        Write-Host ''
        Write-Host 'Read-Means: loud failures'
        $empty = Join-Path $dir 'emptyrun'
        New-Item -ItemType Directory -Force -Path (Join-Path $empty 'results') | Out-Null
        [IO.File]::WriteAllText((Join-Path $empty 'results\Jint.Benchmark.BrokenBenchmark-report.csv'),
            (@($headerPlain, (New-PlainRow 'Execute' '2.204 qs')) -join "`r`n") + "`r`n",
            [Text.UTF8Encoding]::new($false))
        $broken = Read-Means $empty
        Assert-Equal 0 $broken.Rows.Count 'an unrecognised unit parses no rows'
        Assert-Equal '2.204 qs' $broken.Unparsed[0] 'the offending Mean cell is reported'
        Assert-Throws { Assert-ParsedRows $broken 'base' 1 $empty 'base-r1.log' } 'a side that parsed nothing aborts'
        Assert-Throws { Assert-ParsedRows (Read-Means (Join-Path $dir 'does-not-exist')) 'cand' 2 'nowhere' 'cand-r2.log' } 'a missing artifacts directory aborts'

        $dup = Join-Path $dir 'duplicate'
        New-Item -ItemType Directory -Force -Path (Join-Path $dup 'results') | Out-Null
        [IO.File]::WriteAllText((Join-Path $dup 'results\Jint.Benchmark.DupBenchmark-report.csv'),
            (@($headerPlain, (New-PlainRow 'Execute' '10.0 ns'), (New-PlainRow 'Execute' '11.0 ns')) -join "`r`n") + "`r`n",
            [Text.UTF8Encoding]::new($false))
        Assert-Throws { Read-Means $dup } 'two rows collapsing onto one key is an error, not a silent overwrite'
    }
    finally {
        Remove-Item -LiteralPath $dir -Recurse -Force -ErrorAction SilentlyContinue
    }

    Write-Host ''
    Write-Host 'statistics'
    Assert-Equal 2.0 (Get-Median ([double[]] @(1.0, 2.0, 3.0))) 'odd-length median'
    Assert-Equal 2.5 (Get-Median ([double[]] @(1.0, 2.0, 3.0, 4.0))) 'even-length median'
    $ci = Get-BootstrapCi ([double[]] @(5.0, 5.0, 5.0, 5.0, 5.0, 5.0))
    Assert-True ($ci[0] -eq 5.0 -and $ci[1] -eq 5.0) 'a constant sample bootstraps to a point interval'

    Write-Host ''
    Write-Host 'Invoke-PairedAnalysis'
    $base = [System.Collections.Generic.HashSet[string]]::new([string[]] @('A.Full', 'A.Half', 'A.BaseOnly', 'A.Never'), [StringComparer]::Ordinal)
    $cand = [System.Collections.Generic.HashSet[string]]::new([string[]] @('A.Full', 'A.Half', 'A.Never'), [StringComparer]::Ordinal)
    $paired = @{
        'A.Full' = @(-2.0, -1.5, -2.4, -1.9, -2.1, -2.2, -1.7, -2.0)
        'A.Half' = @(0.4, -0.2, 0.1)
    }
    $analysis = Invoke-PairedAnalysis -Paired $paired -SeenBase $base -SeenCandidate $cand -Rounds 8
    Assert-Equal 2 $analysis.Lines.Count 'an under-paired row is still shown in the table, not hidden'
    Assert-True ($analysis.Lines[0] -match 'FASTER') 'a consistent improvement reads FASTER'
    Assert-True (@($analysis.Warnings | Where-Object { $_ -match 'A\.Half paired in 3 of 8' }).Count -eq 1) 'an under-paired row is warned about'
    Assert-True (@($analysis.Failures | Where-Object { $_ -match 'A\.Half paired in only 3 of 8' }).Count -eq 1) 'fewer than half the rounds fails the run'
    Assert-True (@($analysis.Failures | Where-Object { $_ -match 'A\.BaseOnly.*baseline side' }).Count -eq 1) 'a row seen on one side only fails the run'
    Assert-True (@($analysis.Failures | Where-Object { $_ -match 'A\.Never paired in only 0 of 8' }).Count -eq 1) 'a row seen on both sides that never paired fails the run'

    $emptyAnalysis = Invoke-PairedAnalysis -Paired @{} `
        -SeenBase ([System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)) `
        -SeenCandidate ([System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)) -Rounds 8
    Assert-True (@($emptyAnalysis.Failures | Where-Object { $_ -match 'report table is empty' }).Count -eq 1) 'an empty table fails the run'

    $good = Invoke-PairedAnalysis -Paired @{ 'A.Full' = @(0.1, -0.2, 0.3, 0.0, -0.1, 0.2, 0.1, -0.3) } `
        -SeenBase ([System.Collections.Generic.HashSet[string]]::new([string[]] @('A.Full'), [StringComparer]::Ordinal)) `
        -SeenCandidate ([System.Collections.Generic.HashSet[string]]::new([string[]] @('A.Full'), [StringComparer]::Ordinal)) -Rounds 8
    Assert-Equal 0 $good.Failures.Count 'a complete run reports no failures'
    Assert-Equal 0 $good.Warnings.Count 'a complete run reports no warnings'
    Assert-True ($good.Lines[0] -match 'no change') 'noise around zero reads no change'

    Write-Host ''
    if ($script:SelfTestFailed -eq 0) { Write-Host 'SELF-TEST PASSED' } else { Write-Host "SELF-TEST FAILED: $($script:SelfTestFailed) assertion(s)" }
    return $script:SelfTestFailed
}

if ($SelfTest) {
    exit (Invoke-SelfTest)
}

# --- measurement ---------------------------------------------------------------------------

New-Item -ItemType Directory -Force -Path $OutputRoot | Out-Null

$env:JINT_BENCH_MODE      = 'stable'   # pinned + fixed clock; LaunchCount stays 1 per round
$env:JINT_BENCH_POWERPLAN = [Environment]::GetEnvironmentVariable('JINT_BENCH_POWERPLAN', 'User')

function Invoke-Side([string] $root, [string] $tag, [int] $round) {
    $proj = Join-Path $root 'Jint.Benchmark'
    $art  = Join-Path $OutputRoot "$tag-r$round"
    $log  = Join-Path $OutputRoot "$tag-r$round.log"
    Push-Location $proj
    try {
        $a = @('run','-c','Release','--project','.','--','--filter') + $Filter +
             @('--artifacts', $art, '--launchCount','1')
        & dotnet @a *> $log
        if ($LASTEXITCODE -ne 0) {
            throw "round $round, side '$tag': dotnet exited with $LASTEXITCODE. Log: $log"
        }
    } finally { Pop-Location }

    $result = Read-Means $art
    Assert-ParsedRows $result $tag $round $art $log
    $result.Rows
}

$paired   = @{}   # key -> list of percentage differences, one per round
$seenBase = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
$seenCand = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)

for ($r = 1; $r -le $Rounds; $r++) {
    # Alternate which side runs first so run-order penalty is shared equally.
    $baseFirst = ($r % 2) -eq 1
    Write-Host "round $r/$Rounds ($(if($baseFirst){'base,cand'}else{'cand,base'}))"

    if ($baseFirst) {
        $b = Invoke-Side $Baseline  'base' $r
        $c = Invoke-Side $Candidate 'cand' $r
    } else {
        $c = Invoke-Side $Candidate 'cand' $r
        $b = Invoke-Side $Baseline  'base' $r
    }

    foreach ($k in $b.Keys) { $seenBase.Add($k) | Out-Null }
    foreach ($k in $c.Keys) { $seenCand.Add($k) | Out-Null }

    foreach ($k in $b.Keys) {
        if (-not $c.ContainsKey($k)) { continue }
        if (-not $paired.ContainsKey($k)) { $paired[$k] = @() }
        $paired[$k] += 100.0 * ($c[$k] - $b[$k]) / $b[$k]
    }
}

$analysis = Invoke-PairedAnalysis -Paired $paired -SeenBase $seenBase -SeenCandidate $seenCand -Rounds $Rounds

Write-Host ''
'{0,-52} {1,9} {2,20} {3,8} {4}' -f 'row', 'median %', '95% CI', 'sign', 'verdict'
'-' * 108
$analysis.Lines

Write-Host ''
"rounds: $Rounds   raw data: $OutputRoot"
'A row is only a regression when its 95% CI excludes zero. A wide CI means the measurement was'
'not precise enough to decide - add rounds rather than reading the median on its own.'

foreach ($w in $analysis.Warnings) { Write-Warning $w }

if ($analysis.Failures.Count -gt 0) {
    Write-Host ''
    Write-Host ('=' * 108)
    Write-Host "MEASUREMENT FAILED - $($analysis.Failures.Count) problem(s); the table above is NOT a result:"
    foreach ($f in $analysis.Failures) { Write-Host "  * $f" }
    Write-Host "Raw data: $OutputRoot"
    Write-Host ('=' * 108)
    exit 1
}
