# Reproduced X4 browser comparison: independent audit

Both complete independent runs pass the pinned analyzer and independent provenance checks. The evidence supports completing X4's measurement/reproduction requirement with the scoped conclusions below, with the locally retained raw results and the complete text tables published here. This is not a universal browser ranking or a claim about arbitrary websites, rendering, network latency, or dedicated physical hardware.

## Runs and validation

- First run: [35738112188](https://github.com/sebastienros/jint/actions/runs/35738112188)
- Independent repeat: [35764021366](https://github.com/sebastienros/jint/actions/runs/35764021366)
- Both measured source `6363436848365979b2201ed4ec9892ac140c13bc`.
- Both used Ubuntu image `20260907.300.1`, Linux `6.17.0-1022-azure`, SDK 10.0.401, and four guest-visible processors identifying as AMD EPYC 7763. Separate hosted jobs do not establish separate physical hosts or physical-host idleness.
- Both used the same measured executable hashes, harness binary, 18 source hashes, Chrome for Testing 153.0.8010.52 and Lightpanda 0.4.1 asset 565737341. No Lightpanda source was read.

Each run completed 360 Jint A/A rows followed by 540 comparison rows on the same VM: six rounds, three nested independent launches, five workloads, two lanes. Each exact pinned analyzer rerun matched all retained 110 A/A and 330 cross-browser records, including manifest digests. Kernel accounting and all four actual-adapter cleanup tests passed per phase, with no skips.

All 1800 before/after host-idle checks per run passed, alongside the shared idle validator. Maximum loads, as percentages of one core, were 33.499/10.499 for first calibration/comparison and 13.999/9.000 for repeat calibration/comparison, below the unchanged 40% ceiling. These are bracketing guest observations, not continuous or physical-host measurements.

All 900 lifecycle/collector cleanups per run passed, including unpopulated terminal scopes and consistent retained CPU/memory accounting. Forced termination is explicitly recorded throughout; successful cleanup does not mean exclusively graceful browser exit.

Before/after union snapshots were unchanged within every phase: 160593 dependency files for calibration and 160901 for comparison. Every calibration dependency remained unchanged into its comparison. Across runs, dependency path sets are identical and only two setup-record files differ: the Chromium AppArmor policy's run-specific profile name, and the setup manifest recording its hash/name. Policy permissions and executable path are otherwise identical. The browser sandbox remained enabled. All binary and runtime library contents match; the declared private-key-directory exclusion remains explicit.

The full comparison has 328 of 330 metric rows with the same resolved interval direction in both runs. There are no opposite resolved directions. Exactly two rows change uncertainty status, listed below. The A/A tables are not folded into these counts or treated as corrections.

## Calibration limits

The first calibration has seven of 110 intervals excluding zero on identical arms; the repeat has fourteen. Only one A/A metric has the same resolved direction in both; nineteen change between resolved and unresolved, and ninety remain unresolved. This is evidence against treating a nonzero pointwise interval as sufficient proof of an implementation difference.

Repeat warm parse-extract has an identical-arm wall-time offset of −11.40% [−13.58%, −6.91%] and CPU offset of −13.08% [−17.38%, −7.58%]. Repeat warm fetch-update has a −4.64% wall-time offset [−5.87%, −2.30%]. These must remain visible when interpreting browser comparisons; there is no universal noise floor.

The warm interpreter control remains particularly noisy: wall-time A/A is +14.76% [−7.02%, +69.10%] in the first run and −5.58% [−34.74%, +99.96%] in the repeat. CPU A/A is +18.53% [−7.56%, +68.10%] and +1.51% [−36.42%, +101.68%]. Neither supports a precise control correction. No control is subtracted and no observations or confidence intervals are pooled across runs.

## Page-work wall time, including controls

Each value is the median paired percentage difference `100*(other browser - Jint)/Jint`; negative means less time for the other browser. Brackets are separate per-run pointwise 95% percentile bootstrap intervals over six paired rounds (10,000 resamples, seed 3575), after taking the median of three launches per arm/round. These are exploratory intervals without multiple-comparison correction.

### Chromium relative to Jint

| Workload / lane | First median % [95% CI] | Repeat median % [95% CI] |
| --- | ---: | ---: |
| parse-extract / cold | -61.87 [-63.78, -60.17] | -62.19 [-62.66, -61.83] |
| parse-extract / warm | -37.61 [-44.02, -30.45] | -43.44 [-48.39, -36.24] |
| mutate-query / cold | -74.38 [-75.40, -73.24] | -75.35 [-75.93, -73.63] |
| mutate-query / warm | -68.12 [-72.14, -66.59] | -70.45 [-70.79, -65.55] |
| event-form / cold | -63.33 [-64.47, -61.88] | -63.85 [-64.89, -63.12] |
| event-form / warm | -27.99 [-32.10, -24.51] | -30.98 [-32.81, -26.43] |
| fetch-update / cold | -64.17 [-65.57, -61.91] | -63.65 [-64.91, -63.17] |
| fetch-update / warm | -18.33 [-21.57, -13.30] | -22.00 [-24.50, -20.15] |
| interpreter-control / cold | -64.02 [-65.82, -62.65] | -64.05 [-66.02, -63.05] |
| interpreter-control / warm | -53.09 [-61.98, -46.71] | -60.25 [-80.15, -56.47] |

### Lightpanda relative to Jint

| Workload / lane | First median % [95% CI] | Repeat median % [95% CI] |
| --- | ---: | ---: |
| parse-extract / cold | -71.13 [-71.84, -70.61] | -72.52 [-72.78, -71.35] |
| parse-extract / warm | -82.56 [-84.37, -81.96] | -84.21 [-86.05, -82.99] |
| mutate-query / cold | -79.90 [-80.32, -79.45] | -80.18 [-80.61, -79.70] |
| mutate-query / warm | -91.13 [-92.24, -91.01] | -91.55 [-91.74, -91.30] |
| event-form / cold | -72.31 [-72.66, -71.81] | -72.29 [-73.17, -71.54] |
| event-form / warm | -79.86 [-81.32, -78.96] | -79.78 [-80.38, -79.52] |
| fetch-update / cold | -71.30 [-71.87, -70.72] | -71.38 [-71.80, -71.18] |
| fetch-update / warm | -80.72 [-81.43, -78.51] | -79.48 [-80.02, -78.95] |
| interpreter-control / cold | -71.21 [-71.44, -69.05] | -71.19 [-71.91, -70.45] |
| interpreter-control / warm | -85.75 [-88.36, -84.48] | -87.20 [-93.60, -86.56] |

All four DOM workloads have lower page-work wall time for both other browsers in both cold and warm lanes. This large directional result reproduces. Cold results include navigation and first-use work inside a new process lifecycle; warm rows use the declared warmups and repeated navigation. They do not isolate a DOM binding mechanism or imply equivalence of rendering work.

## CPU and memory tradeoffs

Chromium's lower wall time is not a universal CPU improvement. Its warm event-form and fetch-update page-work CPU are higher than Jint in both runs, and most lifecycle CPU rows are higher. Warm mutate-query lifecycle CPU is lower in both. The warm interpreter-control lifecycle CPU difference does not reproduce as resolved. Lightpanda page-work and lifecycle CPU are lower across all recorded rows in both runs; retain the controls and calibration limitations rather than attributing the difference to one subsystem.

Kernel lifetime peak charged memory is higher for Chromium and lower for Lightpanda in every row in both runs. Across DOM rows, Chromium median increases range approximately 156–290% first run and 149–285% repeat; Lightpanda median reductions range approximately 87–93% and 89–93%. This is kernel-accounted cgroup usage, including applicable cache/kernel charges, not portable RSS. It must not be merged with sampled stage peaks, whose sampling can miss transients.

### All comparison rows whose interval direction changes

| Metric, other browser relative to Jint | First median % [95% CI] | Repeat median % [95% CI] | Conclusion |
| --- | ---: | ---: | --- |
| Chromium / parse-extract / warm / page-work CPU | −6.09 [−10.47, +11.31] | −14.77 [−23.70, −6.09] | Unresolved first, lower repeat; no claim of a resolved improvement in both |
| Chromium / interpreter-control / warm / lifecycle CPU | +5.21 [+0.68, +11.32] | +2.21 [−22.66, +5.60] | Higher first, unresolved repeat; no reproduced CPU regression claim |

Every other metric retains the same resolved direction. This count describes the measured intervals; it does not erase A/A offsets or establish universality.

## Conclusion

Two independent complete hosted runs reproduce lower page-work wall time for Chromium and Lightpanda than Jint on the four fixed offline DOM workloads in both declared lanes. Chromium trades higher charged peak memory and some higher CPU costs for lower wall time. Lightpanda uses less charged peak memory and CPU in these measurements. These findings are specific to the pinned builds, workloads, lifecycle boundaries and hosted environments. The controls expose substantial warm-run uncertainty; two CPU rows do not have a resolved direction in both runs. The experiment does not establish arbitrary-site performance, rendering equivalence or a causal explanation for the differences.

## Complete tables and audit records

- [All 330 cross-browser comparisons](https://github.com/sebastienros/jint/blob/main/docs/benchmarks/browser-comparison-2026-09/comparison-cross-run.csv), including Chromium versus Lightpanda and all controls.
- [All 110 identical-arm comparisons](https://github.com/sebastienros/jint/blob/main/docs/benchmarks/browser-comparison-2026-09/calibration-cross-run.csv).
- [Cross-run summary](https://github.com/sebastienros/jint/blob/main/docs/benchmarks/browser-comparison-2026-09/cross-run-summary.json), naming every interval-status change in both phases.
- Independent audit summaries for the [first run](https://github.com/sebastienros/jint/blob/main/docs/benchmarks/browser-comparison-2026-09/audit-35738112188.json) and [repeat](https://github.com/sebastienros/jint/blob/main/docs/benchmarks/browser-comparison-2026-09/audit-35764021366.json).

CSV columns `run1` and `run2` identify the first run and repeat above. All numeric differences and interval bounds are percentages, calculated as `100*(right-left)/left`; the metric name identifies the underlying quantity. A negative memory difference means lower charged usage, not a negative byte count. Each CSV retains every comparison, including both unchanged and changed interval directions. The Markdown wall-time tables are rounded to two decimals from these full-precision values.

## Retention and reanalysis

The campaign owner accepted local retention of the complete raw results. No binary files or downloadable compressed archives accompany this report, and external archive publication is not a completion prerequisite. The original [first artifact](https://github.com/sebastienros/jint/actions/runs/35738112188/artifacts/10709031789) and [repeat artifact](https://github.com/sebastienros/jint/actions/runs/35764021366/artifacts/10718274168) are available while GitHub retains them; those artifacts expire. The text tables and audit summaries here are retained in the repository, but do not substitute for the raw observations needed to independently rerun the analyzer.

The audit used these local artifact directories under the campaign worktree (they are ignored by Git):

```text
artifacts/campaign-evidence/35738112188/
artifacts/campaign-evidence/35764021366/
```

Each contains `calibration/`, `comparison/`, `calibration-analysis.json`, `comparison-analysis.json`, and setup evidence. These are local paths, not publicly available downloads. The downloaded ZIPs were SHA-256 verified before extraction:

| Run | Original ZIP SHA-256 |
| --- | --- |
| 35738112188 | `3a61aa63c2d77546aac419ff82009e57c0bc438e08a51edac7d0abe851f5e45d` |
| 35764021366 | `ac1ccf962396eb882d792faea5cace7ce9d714fc969edd2a4e589d281c00bde6` |

Reanalyze each complete local artifact with the exact measured revision's analyzer, without running another benchmark:

```sh
analysis_dir=$(mktemp -d)
for script in analyze.py measure.py; do
  git show "6363436848365979b2201ed4ec9892ac140c13bc:tools/browser-comparison/$script" > "$analysis_dir/$script"
done
for run in 35738112188 35764021366; do
  for phase in calibration comparison; do
    python3 "$analysis_dir/analyze.py" "artifacts/campaign-evidence/$run/$phase" > "/tmp/$run-$phase-analysis.json"
  done
done
```

The independent audit compared the resulting JSON objects with each retained analysis in full, including manifest hashes, and then checked dependency continuity between phases and across jobs. The [pinned harness instructions](https://github.com/sebastienros/jint/blob/6363436848365979b2201ed4ec9892ac140c13bc/tools/browser-comparison/README.md) describe all metrics, fixtures, lane boundaries and collection commands. The [pinned workflow](https://github.com/sebastienros/jint/blob/6363436848365979b2201ed4ec9892ac140c13bc/.github/workflows/browser-comparison-hosted.yml) records the hosted setup. A fresh collection must repeat verification and same-VM calibration, retain all controls and meet the unchanged gates; the numbers here are not a substitute for that validation.

## Earlier refusals and setup failures

Only the two complete validated runs above contribute accepted comparison numbers. Earlier failures remain part of the campaign's feasibility history:

- Preflight found dangling LLDB package links in the hosted image; setup was repaired through a bounded, recorded package-manager operation before measurement. Missing or unreadable dependencies were never silently skipped.
- Browser installation and sandbox feasibility required verified pins and a scoped Chromium AppArmor profile. The browser sandbox was not disabled.
- [Calibration 35686847316](https://github.com/sebastienros/jint/actions/runs/35686847316) refused a 44.498% idle observation after 151 rows; partial observations were not accepted as a calibration.
- [Comparison 35711999339](https://github.com/sebastienros/jint/actions/runs/35711999339) completed its calibration but refused a 50.998% idle observation after 63 comparison rows. The partial comparison was excluded.
- [Comparison 35729896926](https://github.com/sebastienros/jint/actions/runs/35729896926) reached the 1800-second dependency-scan setup limit before collecting rows. The later bounded four-worker hashing change retained full coverage and fail-closed behavior; it did not raise that limit or change timing/accounting gates.

These records document setup feasibility and refusal behavior, not browser performance. No claim that every attempted hosted run succeeds is implied by the two accepted results.
