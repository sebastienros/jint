<#
.SYNOPSIS
    Prepares a Windows machine to produce reproducible BenchmarkDotNet numbers for Jint.

.DESCRIPTION
    Creates (once) a dedicated "Jint Benchmark (fixed clock)" power plan and records its GUID in the
    user environment variable JINT_BENCH_POWERPLAN, which JintBenchmarkConfig reads. BenchmarkDotNet
    then applies that plan for the duration of a run and restores the previous one afterwards, so the
    machine is never left in benchmark mode.

    Why a fixed clock. On the Ryzen 9 5950X this suite is gated on, the stock High performance plan
    leaves PERFBOOSTMODE at Aggressive, and the delivered frequency then depends on package
    temperature, on what the other 15 cores are doing, and on per-core silicon binning. Measured under
    a single-threaded load: High performance peaks at 4,607 MHz and averages 3,810 MHz across cores,
    while this plan holds every core at 3,375 MHz with peak equal to mean. Absolute numbers drop
    11-27%; run-to-run comparability is what is bought with that.

    -Check runs the verification only and changes nothing.

    -Restore puts the machine back on High performance and kills any orphaned benchmark process. Run
    it after any interrupted run: BenchmarkDotNet restores the previous power plan when it exits
    normally, but a run killed part-way through leaves the fixed-clock plan active, and the next thing
    to use the machine — including an unrelated benchmark — then silently runs at nominal frequency.

.EXAMPLE
    ./setup-benchmark-machine.ps1              # create or update the plan (needs an elevated shell)

.EXAMPLE
    ./setup-benchmark-machine.ps1 -Check       # report the current state, change nothing

.EXAMPLE
    ./setup-benchmark-machine.ps1 -Restore     # after an interrupted run
#>
[CmdletBinding()]
param(
    [switch] $Check,
    [switch] $Restore
)

$ErrorActionPreference = 'Stop'

$PlanName        = 'Jint Benchmark (fixed clock)'
$HighPerformance = '8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c'
$PerfBoostMode   = 'be337238-0d82-4146-a960-4f3749d470c7'   # hidden by default
$IdleDisable     = '5d76a2ca-e8c0-402f-a133-2158492d58ad'   # hidden by default

function Test-Elevated {
    $id = [Security.Principal.WindowsIdentity]::GetCurrent()
    (New-Object Security.Principal.WindowsPrincipal($id)).IsInRole(
        [Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Get-JintPlanGuid {
    $line = powercfg /L | Select-String -SimpleMatch $PlanName | Select-Object -First 1
    if (-not $line) { return $null }
    [regex]::Match($line.ToString(), '[0-9a-f]{8}(-[0-9a-f]{4}){3}-[0-9a-f]{12}').Value
}

function Show-PlanSettings([string] $guid) {
    $dump = powercfg /q $guid SUB_PROCESSOR | Out-String
    foreach ($alias in 'PERFBOOSTMODE', 'PROCTHROTTLEMIN', 'PROCTHROTTLEMAX', 'CPMINCORES', 'IDLEDISABLE') {
        $m = [regex]::Match($dump, "GUID Alias: $alias[\s\S]*?Current AC Power Setting Index: (0x[0-9a-f]+)")
        $value = if ($m.Success) { [Convert]::ToInt32($m.Groups[1].Value, 16) } else { '?' }
        '{0,-16} = {1}' -f $alias, $value
    }
}

# --- restore after an interrupted run -----------------------------------------------------------

if ($Restore) {
    $stray = Get-Process -Name 'Jint.Benchmark' -ErrorAction SilentlyContinue
    if ($stray) {
        # Kill first: a live host process re-applies its own plan and would undo the restore below.
        $stray | Stop-Process -Force
        Start-Sleep -Milliseconds 500
        "Killed $($stray.Count) orphaned benchmark process(es)."
    }

    powercfg /setactive $HighPerformance
    "Active scheme: $((powercfg /getactivescheme))"
    exit 0
}

# --- report-only ------------------------------------------------------------------------------

if ($Check) {
    $guid = Get-JintPlanGuid
    if (-not $guid) {
        Write-Warning "Plan '$PlanName' does not exist. Run this script elevated, without -Check."
        exit 1
    }

    "Plan GUID          : $guid"
    "JINT_BENCH_POWERPLAN : $([Environment]::GetEnvironmentVariable('JINT_BENCH_POWERPLAN', 'User'))"
    ''
    Show-PlanSettings $guid
    exit 0
}

# --- create or update -------------------------------------------------------------------------

if (-not (Test-Elevated)) {
    throw 'This script must run in an elevated shell (powercfg needs administrator rights to create a scheme).'
}

$guid = Get-JintPlanGuid
if ($guid) {
    "Reusing existing plan $guid"
} else {
    $out  = powercfg -duplicatescheme $HighPerformance | Out-String
    $guid = [regex]::Match($out, '[0-9a-f]{8}(-[0-9a-f]{4}){3}-[0-9a-f]{12}').Value
    if (-not $guid) { throw "Could not duplicate the High performance scheme: $out" }
    powercfg -changename $guid $PlanName 'Fixed-frequency plan for reproducible BenchmarkDotNet runs.'
    "Created plan $guid"
}

# PERFBOOSTMODE and IDLEDISABLE are hidden attributes; powercfg refuses to set them until unhidden.
powercfg -attributes SUB_PROCESSOR $PerfBoostMode -ATTRIB_HIDE
powercfg -attributes SUB_PROCESSOR $IdleDisable   -ATTRIB_HIDE

powercfg -setacvalueindex $guid SUB_PROCESSOR PERFBOOSTMODE   0     # no opportunistic boost
powercfg -setacvalueindex $guid SUB_PROCESSOR PROCTHROTTLEMIN 100   # every core at nominal
powercfg -setacvalueindex $guid SUB_PROCESSOR PROCTHROTTLEMAX 100
powercfg -setacvalueindex $guid SUB_PROCESSOR CPMINCORES      100   # no core parking
powercfg -setacvalueindex $guid SUB_PROCESSOR IDLEDISABLE     1     # no C-state entry
powercfg -setacvalueindex $guid SUB_PROCESSOR PERFEPP         0     # favour performance over efficiency

[Environment]::SetEnvironmentVariable('JINT_BENCH_POWERPLAN', $guid, 'User')
$env:JINT_BENCH_POWERPLAN = $guid

''
"Plan GUID            : $guid"
'JINT_BENCH_POWERPLAN : set for the current user (restart open shells to pick it up)'
''
Show-PlanSettings $guid
''
'Next: run a gating measurement with'
'  $env:JINT_BENCH_MODE = "gate"'
'  dotnet run -c Release --project Jint.Benchmark -- --filter "*YourBenchmark*"'
