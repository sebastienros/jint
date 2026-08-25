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
brought back in line, and a `*` glob can never widen into a blanket. A test *name* may itself contain a `*`,
which is what the `\*` escape is for; without it there was no pattern at all that named
`fetch/api/basic/accept-header.any.js`'s then-failing `…with value '*/*'` row and not its passing sibling
`…with value 'custom/*'` — both pass now, so the escape's only user today is `WptCorpusTests`.
**`NeedsTriage` is the debt**: those are genuine defects the harness found, recorded
rather than fixed so that the change which first ran a suite is not also the change that moved the engine — a
non-zero count there means the corpus has found something and somebody still owes the engine a fix. It was
empty until [#3260](https://github.com/sebastienros/jint/issues/3260) gave the fetch corpus a server, which
found five things the moment it could make a real request; they were filed as #3279–#3283, they are all fixed,
and the category is **empty again**, which is the state that makes the next non-zero count mean something.
A row that turns out not to be a defect at all leaves for `AssertsWhatNothingRequires`, the
analogue of test262's `PERMANENT EXCLUSIONS` banner, earned the same way: a normative citation and an argued
decision, never a to-do. **The engine supplies its own `setTimeout`** — unlike the test262 harness, which has no web APIs to
enable and shims one onto the event loop, this driver enables
`WebApiFeatures.Timers` and pumps with `Tasks.ProcessTasks()` bounded by `TimeUntilNextPumpScheduledWork()`,
so a suite that schedules a timer exercises the shipped `TimerQueue`. **`// META: variant=` sharding is
ignored**: the shim leaves `location.search` empty, so `subsetTest`/`subsetTestByKey` run everything and one
run of a file is the union of all of its variants. And **every engine carries the fetch object model**:
`WptHarness.BuildEngine` installs `Headers`, `Request` and `Response` on top of `Default` — and, for all but
the files `WptHarness._serverBackedFiles` names, pointedly not `fetch`, which no feature flag names the model
without. `url/urlencoded-parser.any.js` reaches the urlencoded parser through `Request.formData()` and
`Response.formData()` as well as `URLSearchParams`, and the `headers/` and `response/` suites are about those
three interfaces and nothing else, which is what let that half of the fetch corpus be vendored years before
there was anything for the other half to talk to. **That other half is `WptServer`** — an in-process HTTP/1.1
origin on the loopback interface, a raw `TcpListener` on port 0, serving the *vendored* corpus plus a C# port
of six wptserve `.py` handlers, which `WptServerTests` holds to the upstream source at the pin. Only the
seventeen files in that list get `WebApiFeatures.Fetch`, their `Options.WebApi.Fetch.UrlFilter` is the
server's own port re-checked on every redirect hop, and `TheServerLaneHoldsExactlyTheFilesItNames` pins the
list in both directions — so *no suite can reach the network*, which is the promise the driver has always
made, while the twenty files that could not produce a test report at all now do. Two things about that lane
are worth knowing before touching it: the shim supplies the API base URL the engine deliberately has not got
(`new URL(relative, base).href`, which is what `RequestConstructor` tells a host to do, and it pointedly does
*not* wrap `Request`), and the drive loop polls there rather than treating "nothing scheduled" as stalled,
because a request in flight is a thread-pool completion `TimeUntilNextScheduledWork` cannot report. Last,
**every engine carries a `DiagnosticsSink`**, because that is what makes an
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

An eighth, because it is the one somebody reaches for the wrong tool on. `Vendor/README.md`'s inventory table
is generated by `WptCensusTests`, and **its "Not passing" column is a ceiling, not a baseline**: a rise fails
as a regression naming the suite and the size of it, a fall fails as staleness, and `JINT_WPT_CENSUS=update`
lowers that figure and *refuses to raise it*. If the census says the corpus fails more than the table allows,
the answer is the regression — an engine defect, or a corpus entry whose outcome depends on the machine — and
never re-censusing, which is what
[#3339](https://github.com/sebastienros/jint/issues/3339) records three unrelated pull requests being invited
to do. The other four columns are equalities in both directions, because none of them counts an outcome.
