# Numeric materialization diagnostics

Read `docs/benchmarks/numeric-materialization-2026-09/README.md` for scope and limitations.
**The runtime candidate is retained; `build_pair.py` refuses identical arms.**
These helpers run from the repository root. They do not replace the hosted Linux comparison gate.
The driver is adapted from the component investigation's browser-profile text helper and links the
unchanged campaign workloads directly.

- `python3 tools/numeric-materialization/locked.py COMMAND...` holds the campaign-wide advisory lock
  until the command exits. Never delete or replace the lock file.
- `...locked.py python3 tools/numeric-materialization/build_pair.py` builds preserved baseline and
  candidate browser binaries, helpers, benchmarks, and focused correctness suites. **It temporarily
  restores the one candidate engine source file while building baseline, and restores it in `finally`.**
  Do not edit that source concurrently with this command. The baseline SHA is explicit in the helper.
- Place the verified official macOS arm64 Lightpanda binary at
  `artifacts/numeric-materialization/lightpanda-aarch64-macos`. Verify its digest against the report.
- `...locked.py python3 tools/numeric-materialization/measure.py pairs` runs six balanced three-arm
  rounds on interpreter-control, event-form and mutate-query, cold and original 5+10 warm boundaries.
- `...locked.py python3 tools/numeric-materialization/measure.py profiles` collects separate CPU and
  allocation samples for 5+300 warm repetitions. Requires `dotnet-trace`. Interpret instrumented
  windows separately from timings; tracing overhead can be large.
- `python3 tools/numeric-materialization/summarize.py` exports complete six-round comparisons and
  per-launch compact rows. Only rows with successful pre/post blocking idle checks are admitted.

All generated binaries, downloads and raw profiles are under ignored `artifacts/`. Keep production tiering/PGO. Idle rejection may be bypassed only for the explicitly requested diagnostic runs described below. Both process launch and server/client cleanup are bounded. CPU
is root-process CPU; process-tree memory is not measured. A failed idle check aborts the batch, leaving
its refusal evidence, rather than producing an apparently accepted speedup.

An optional attempt tag follows `pairs` or `profiles`; pass the same tag to `summarize.py` or `profile_summary.py`. Incomplete collections fail rather than reporting a performance verdict.

`verify_browsers.py` uses the stock campaign correctness-only smoke mode on all three preserved binaries, with `Measurements: null`. Run it under the lock; it is not a timing fallback.

The user subsequently authorized bypassing the idle guard. `measure.py pairs <tag> --skip-idle-check`
and `profiles` with the same flag record the bypass explicitly, retaining the shared CPU lock and
production runtime settings. Pass `--skip-idle-check` to the timing summarizer to include those
**noisy local diagnostic** rows. They are never marked idle-accepted. `run_requested.py` performs the
fresh-build, six-round comparison and profile batch; `run_bdn.py` runs both default-job microbenchmark
arms with three launches each. That single BDN arm pair supplies exploratory timing/allocation evidence,
not a six-pair speed verdict.
