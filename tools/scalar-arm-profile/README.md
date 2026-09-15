# Scalar ARM budget diagnostics

Temporary investigation workflow for issue #3882, based on `main` at
`0b0d64d87b1c334ab7149f26c03db7d2a7b3df1a`. The `Scalar ARM profile` workflow runs
on two independent `ubuntu-24.04-arm` workers. Pushes now validate the fixes with
three full Release solution runs per worker, without profiling. `verify.py` retains
every attempt's log and browser TRX, and fails for any suite failure or missing/non-passing
Scalar result on either framework. It changes only the browser TRX destination.

Manual dispatch with `profile=true` selects diagnostic capture instead. Each worker repeats the ordinary
full Release solution test command up to six times, stopping when the Scalar
error-sink assertion reports the known page-task budget error and a readable
browser trace has been captured. Capture changes no existing test source, fixture asset, or budget.

The script temporarily supplies browser-only runsettings using a generated
`Jint.Tests.Browser/Directory.Build.targets`, which it refuses to overwrite and
removes afterward. An external EventPipe collector connects as each browser testhost starts;
other suites retain their normal scheduling and are not profiled. Traces contain
managed stack samples, GC events, and runtime method information. Using an external
writer and a generated NUnit assembly teardown finalize the trace before vstest
terminates its testhost. The teardown runs after all tests and waits at most 60 seconds
for the collector; this diagnostic wait is outside every page-task budget. Diagnostic ports
use `nosuspend`, so inherited settings cannot suspend a child process. Profiling adds overhead, so a profiled
failure is diagnostic evidence, not a measurement of unprofiled failure frequency.

Artifacts include the source SHA, runtime and worker information, full test logs,
TRX results (with Scalar start/end timestamps), `vmstat`, raw `.nettrace` files,
Speedscope conversions, and inclusive stack reports. Raw traces retain GC and
timing information that the Speedscope view does not expose. The first iteration
must produce readable samples with zero dropped events; missing tests or unusable
capture stop the batch. A separate TraceEvent reader records the session timestamps
and event-loss count. The 256 MB capture buffer accommodates runtime event bursts.
Subsequent passing traces are discarded except for the final iteration. Failures
and the first baseline are retained. Exhausting a batch exits with code 2 and
explicitly does not declare the issue fixed.

Local capture smoke check (one isolated test; not a reproduction of CI load):

```sh
python3 tools/scalar-arm-profile/run.py --smoke --attempts 1 --output /tmp/scalar-smoke
```

`TRACE_TOOL` can select a `dotnet-trace` executable. The workflow pins the reader
to 9.0.652701. See the official [EventPipe environment settings](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/eventpipe#trace-using-environment-variables).
