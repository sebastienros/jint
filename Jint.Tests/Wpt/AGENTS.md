# Agent instructions: the web-platform-tests corpus

> **Read this when:** You are touching `Jint.Tests/Wpt/` — the vendored corpus, the harness shim, the driver, or the exclusion table.
>
> This is one of the co-located instruction files indexed from the repository-root
> [`AGENTS.md`](../../AGENTS.md). Read that first — it carries the build and test commands, the branch to
> target, and the conventions that apply to every file in the repository. Nothing below is
> repeated there.

### Web platform tests

The web APIs have a conformance suite the way the language has test262: `Jint.Tests/Wpt/` runs vendored
web-platform-tests `.any.js` files, one xUnit theory case per file, against an engine built with
`UseWebApis(WebApiFeatures.Default)`. `Vendor/` is copied verbatim from a pinned upstream commit — provenance,
the pin, and the table of files deliberately *not* vendored are in `Vendor/README.md`, and the driver enforces
that table. `Prelude/testharness-shim.js` is Jint's own file, not a vendored one: it implements the slice of
upstream's `testharness.js` these suites use, and `WptHarnessTests` exercises every assertion in it from both
sides, because a shim that quietly passed everything would make five thousand cases green and mean nothing.

Six things to know before touching it. **The exclusion table is the artefact**: a test that does not pass is
named in `WptTestRunner._exclusions` with a `WptDivergence` category, and an entry must match at least one
failing test and no passing one — so a fix, a rename, or a corpus bump makes the run fail until the table is
brought back in line, and a `*` glob can never widen into a blanket. **`NeedsTriage` is the debt, and it is
empty**: those are genuine defects the harness found, recorded rather than fixed so that the change which
first ran a suite is not also the change that moved the engine — every one filed so far has been paid, and the
emptiness is the signal, so a non-zero count there means the corpus has found something. A row that turns out
not to be a defect at all leaves for `AssertsWhatNothingRequires`, the analogue of test262's
`PERMANENT EXCLUSIONS` banner, earned the same way: a normative citation and an argued decision, never a
to-do. **The engine supplies its own `setTimeout`** — unlike the test262 harness, which has no web APIs to
enable and shims one onto the event loop, this driver enables
`WebApiFeatures.Timers` and pumps with `Advanced.ProcessTasks()` bounded by `TimeUntilNextPumpScheduledWork()`,
so a suite that schedules a timer exercises the shipped `TimerQueue`. **`// META: variant=` sharding is
ignored**: the shim leaves `location.search` empty, so `subsetTest`/`subsetTestByKey` run everything and one
run of a file is the union of all of its variants. And **every engine carries the fetch object model**:
`WptHarness.BuildEngine` installs `Headers`, `Request` and `Response` on top of `Default` — and pointedly not
`fetch`, which no feature flag names the model without, so no suite can open a socket either way. Two suite
groups need it: `url/urlencoded-parser.any.js` reaches the urlencoded parser through `Request.formData()` and
`Response.formData()` as well as `URLSearchParams`, and the two vendored `fetch/api/` suites are about those
three interfaces and nothing else, which is what let half of that corpus be vendored while the half that
talks to a server could not. Last, **every engine carries a `DiagnosticsSink`**, because that is what makes an
exception escaping a timer callback, a `queueMicrotask` callback or an event listener report-and-continue
rather than erupt from the pump — the environment the corpus was written for, and the same choice
`WorkerRequest.CreateDefaultOptions` already makes for a worker engine. It is a *recorder* rather than
`DiagnosticsSink.Null` on purpose: before it existed such an exception erupted and the driver reported a
harness error for the whole file, so `WptHarness` turns any recorded uncaught callback error into that same
harness error unless the file declared `setup({allow_uncaught_exception: true})` — which is upstream's own
rule, and the second and last member of the properties bag the shim acts on.

A seventh thing is worth knowing because it decides *where* a divergence gets recorded. The driver's unit of
report is a test, so a file that cannot produce one — a throw at file scope, a run that **stalls**, or a file
whose tests are all registered *without a name* — is a harness error covering the whole file and has to go in
`_notVendored` with its reason rather than in the exclusion table. The clearest three are opposite ends of
one idea: `streams/readable-byte-streams/construct-byob-request.any.js` throws before registering a case,
`compression/decompression-extra-input.any.js` registers four and then waits forever on a stream that a
conforming implementation would have errored, and
`html/webappapis/microtask-queuing/queue-microtask-exceptions.any.js` lets its throw erupt past the pump and
takes the file with it. Prefer a sibling file that asserts the same property by *comparing a result* —
`decompression-corrupt-input.any.js` does, in six rows — and say in the not-vendored row which one it is, so
the divergence is still pinned somewhere. Where no sibling does, the divergence lives in `Vendor/README.md`
and in the triage issue, which is the shape `queue-microtask-exceptions` has.
