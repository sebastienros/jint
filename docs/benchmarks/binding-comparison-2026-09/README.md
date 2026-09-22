# D0.3: reproduced DOM binding spike comparison

Two independent complete hosted runs reproduce lower measured costs for the small hand-shaped binding
spike than for AngleSharp.Js reflection on the three DOM workloads. This completes the measurement and
scoped conclusion for [campaign #3575 D0.3](https://github.com/sebastienros/jint/issues/3575), whose harness
was tracked in [#3898](https://github.com/sebastienros/jint/issues/3898).

This is an end-to-end comparison of two explicitly different embedding paths. It does **not** isolate the
causal benefit of shapes, measure the production generated browser bindings, or establish equivalent-browser
startup savings. The interpreter versions and binding surfaces differ. The cold interpreter control makes
the setup difference especially visible; controls are reported without subtraction.

## Provenance and acceptance

Both runs measured committed source `c0725301810ad1454d7d1fbc0757659af1542f9b`, with a clean checkout,
.NET SDK 10.0.401, runtime .NET 10.0.12, and Ubuntu 24.04.5 LTS x64. The runner image was
`ubuntu24 / 20260907.300.1`. Each job ran serially on its own GitHub-hosted VM.

| Run | Workflow evidence | Guest CPU model | Maximum recorded idle-check load |
| --- | --- | --- | ---: |
| 1 | [35671058293, attempt 1](https://github.com/sebastienros/jint/actions/runs/35671058293) | AMD EPYC 9V74 80-Core Processor | 1.0% of one core |
| 2 | [35677667091, attempt 1](https://github.com/sebastienros/jint/actions/runs/35677667091) | AMD EPYC 7763 64-Core Processor | 5.0% of one core |

These CPU model names describe the guest-visible processor, not a reservation of all its named cores.
The VMs expose four logical processors. The unchanged idle gate allows at most 40% of one core; every
pre-arm check passed. Guest observations do not establish physical-host isolation or continuous idleness
during each benchmark. The different CPU models are another reason to keep the runs' estimates separate.

| Component | Baseline | Candidate |
| --- | --- | --- |
| Binding | AngleSharp.Js 1.1.0 reflection, complete Window/DOM setup | Small hand-shaped Node/Element/Document prototypes |
| Interpreter | NuGet Jint 4.15.3 | Jint 5.0.0-beta at the measured source revision |
| Parser | Acornima 1.6.2 | Acornima 1.8.0 |
| DOM | AngleSharp 1.8.0 | Identical AngleSharp 1.8.0 assembly |
| BenchmarkDotNet | 0.15.8 | Identical 0.15.8 assembly |

The candidate covers only the members used by the spike; it is not the complete generated WebIDL surface.
Exact assembly informational versions and SHA256 digests are in each `raw/verification.json`; both arms'
provenance and workload checksums match exactly between the two runs. The shared workload SHA256 is
`14D52412A48DF26C52A656E8163B5F50644AC2C52EBD9962CFD5147C1DEB6AC6`.

Each run completed six alternating baseline/candidate pairs, three BDN launches per row, all eight
workload/lane rows, and 24 independent correctness checks per arm. Production tiering/PGO and the gate
configuration remained enabled. No short job, idle override, control subtraction, or post-hoc row selection
was used. Reanalysis with the measured revision's analyzer exactly reproduces all eight retained summaries
in both runs. All 28 analyzer-recorded file digests per run and all 132 archived files were independently
verified after extracting the checked-in archives.

## Results

Values are the median of six paired `100 * (candidate - baseline) / baseline` time differences. Negative
means lower candidate time. Brackets are pointwise 95% percentile bootstrap intervals: 5,000 resamples,
seed 20260816 reset per row, Python Mersenne Twister, sorted indices 125 and 4875. Launches are not treated
as independent rounds. These exploratory intervals have no multiple-comparison adjustment and are not
pooled across the two VMs. Full precision, round means, allocation diagnostics, and raw BDN output remain
in the archives.

| Workload / lane | Run 1 median Δ [95% CI], % | Run 2 median Δ [95% CI], % |
| --- | ---: | ---: |
| DocumentLookup / cold | −70.76 [−71.45, −70.38] | −69.16 [−71.82, −66.40] |
| ElementReadWrite / cold | −56.68 [−57.42, −56.28] | −53.06 [−54.48, −52.30] |
| NodeRead / cold | −74.07 [−74.49, −73.85] | −73.57 [−73.85, −72.79] |
| InterpreterControl / cold | −81.46 [−81.69, −80.98] | −80.43 [−80.66, −80.20] |
| DocumentLookup / warm | −60.70 [−61.03, −60.24] | −60.20 [−62.89, −58.78] |
| ElementReadWrite / warm | −35.07 [−36.83, −34.23] | −31.37 [−33.39, −29.38] |
| NodeRead / warm | −61.47 [−62.14, −61.21] | −63.20 [−63.55, −59.83] |
| InterpreterControl / warm | −2.17 [−4.24, +0.0003] | −2.97 [−4.58, −2.61] |

All six DOM rows improve in every paired round of both runs. Warm DOM improvements reproduce in direction
and remain much larger than the warm interpreter-control differences. The warm control's direction is
unresolved in run 1 and lower in run 2; it is not evidence of equal interpreter costs and cannot be used to
correct the DOM rows. The different interpreter versions remain a confounder.

Cold includes DOM parsing, engine/binding setup, first prepared-script execution, and disposal. It excludes
script parsing and process startup; BDN has already warmed process-wide initialization. The cold control
improves by roughly 80–81% even without DOM calls, demonstrating that unequal setup costs dominate an
important part of this comparison. Warm excludes DOM/engine setup, script parsing, and disposal and warms
each engine only with its own workload.

The supported conclusion is that the small binding spike has reproducibly lower costs for these embedding
paths under the two recorded hosted environments. Attribution to shape storage alone, equivalent production
browser performance, or generalization to dedicated physical hardware requires a different matched experiment.

## Durable evidence and reanalysis

Both gzip archives contain the complete downloaded Actions artifact directories, including driver logs,
host metadata, verification logs/JSON, per-round CSV/Markdown/HTML reports, all BDN logs, and `analysis.json`.
No file contents were rewritten. Packaging normalizes tar ownership, permissions and timestamps and uses
an empty gzip filename and timestamp zero, so identical inputs produce identical archives. The archives
are approximately 0.6 MB each and remain in git rather than depending on Actions artifact retention.

`SHA256SUMS` covers both archives and `files.sha256`; the latter covers every extracted artifact file.
From the repository root, verify and reanalyze without running any benchmarks:

```sh
evidence="$PWD/docs/benchmarks/binding-comparison-2026-09"
(cd "$evidence" && sha256sum -c SHA256SUMS)
extracted=$(mktemp -d)
for run in 35671058293 35677667091; do
  tar -xzf "$evidence/binding-measurement-$run-1.tar.gz" -C "$extracted"
done
(cd "$extracted" && sha256sum -c "$evidence/files.sha256")
git show c0725301810ad1454d7d1fbc0757659af1542f9b:Jint.Benchmark/BindingComparison/analyze.py > "$extracted/analyze.py"
for run in 35671058293 35677667091; do
  python3 "$extracted/analyze.py" \
    --artifacts "$extracted/binding-measurement-$run-1/raw" \
    --driver-log "$extracted/binding-measurement-$run-1/driver.log" \
    --expected-head c0725301810ad1454d7d1fbc0757659af1542f9b \
    > "$extracted/reanalysis-$run.json"
done
```

On macOS, replace `sha256sum -c` with `shasum -a 256 -c`. Reanalysis changes machine-local file paths and
Python version metadata; its `summary` must equal the retained analysis exactly. To collect new measurements,
use the [harness instructions](../../../Jint.Benchmark/BindingComparison/README.md) and the
[benchmark requirements](../../../Jint.Benchmark/AGENTS.md) at the measured revision. Use a clean isolated
checkout and a new output directory, retain refused attempts, and do not weaken the idle guard to obtain a run.
