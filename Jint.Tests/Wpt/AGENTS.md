# Agent instructions: the web-platform-tests corpus

> **Read this when:** You are touching `Jint.Tests/Wpt/` — the vendored corpus, the harness shim, the driver, or the exclusion table.
>
> This is one of the co-located instruction files indexed from the repository-root
> [`AGENTS.md`](../../AGENTS.md). Read that first — it carries the build and test commands, the branch to
> target, and the conventions that apply to every file in the repository. Nothing below is
> repeated there.

### Web platform tests

The web APIs have a conformance suite the way the language has test262: `Jint.Tests/Wpt/` runs vendored
web-platform-tests `.any.js` files, one test case per file, against an engine built with
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
and the category went **empty again**, which is the state that makes the next non-zero count mean something.
It went non-zero once more and is empty again: vendoring `fetch/api/request/` found that
`new Request(anotherRequest)` left the input undisturbed, because `FetchBodyObject.ProxyBody` teed the stream
(and shared the source for a buffered body) where the standard *proxies* it, and a proxy disturbs what it
reads. Three rows of `request-disturbed.any.js` named it; [#3618](https://github.com/sebastienros/jint/issues/3618)
fixed two and moved the third — which asks for a disturbance the constructor's own step makes conditional on
there being no init body — to `AssertsWhatNothingRequires`, which is the shape the pair of categories is for.
A row that turns out not to be a defect at all leaves for `AssertsWhatNothingRequires`, the
analogue of test262's `PERMANENT EXCLUSIONS` banner, earned the same way: a normative citation and an argued
decision, never a to-do. **The engine supplies its own `setTimeout`** — unlike the test262 harness, which has no web APIs to
enable and shims one onto the event loop, this driver enables
`WebApiFeatures.Timers` and pumps with `Tasks.ProcessTasks()` bounded by `Tasks.TimeUntilNextScheduledWork`,
so a suite that schedules a timer exercises the shipped `TimerQueue`. **`// META: variant=` sharding is
ignored**: the shim leaves `location.search` empty, so `subsetTest`/`subsetTestByKey` run everything and one
run of a file is the union of all of its variants. And **every engine carries the fetch object model**:
`WptHarness.BuildEngine` installs `Headers`, `Request` and `Response` on top of `Default` — and, for all but
the files `WptHarness.IsServerBacked` names, pointedly not `fetch`, which no feature flag names the model
without. `url/urlencoded-parser.any.js` reaches the urlencoded parser through `Request.formData()` and
`Response.formData()` as well as `URLSearchParams`, and the `headers/` and `response/` suites are about those
three interfaces and nothing else, which is what let that half of the fetch corpus be vendored years before
there was anything for the other half to talk to. **That other half is `WptServer`** — an in-process HTTP/1.1
origin on the loopback interface, a raw `TcpListener` on port 0, serving the *vendored* corpus plus a C# port
of the wptserve `.py` handlers those suites name, which `WptServerTests` holds to the upstream source at the pin. Only the files
`WptHarness.IsServerBacked` names — the thirty in that list plus the whole vendored `xhr/` directory —
get `WebApiFeatures.Fetch`, their `Options.WebApi.Fetch.UrlFilter` is the
server's own port re-checked on every redirect hop, and `TheServerLaneHoldsExactlyTheFilesItNames` pins the
list in both directions — so *no suite can reach the network*, which is the promise the driver has always
made, while the files that could not produce a test report at all now do. **A third lane grants the same two
features and no reach at all**: `WptHarness._blobUrlBackedFiles` is the two `FileAPI/url` files whose only
requests are of `blob:` URLs, which scheme fetch answers from the engine's own store before a filter is
consulted, so their `UrlFilter` refuses *every* URL — pinned, in both directions and against double
membership, by `TheBlobUrlLaneHoldsExactlyTheFilesItNames`. Two things about the server lane
are worth knowing before touching it: the driver gives each of its engines the API base URL, the referrer,
the origin and the cookie jar a browsing environment would supply — `Options.WebApi.Fetch.BaseUrl` is the URL
the server really serves that file at, and it is what let the whole of `fetch/api/request/` be vendored,
since the shim's older `fetch` wrapper could resolve a relative url for `fetch()` and never for
`new Request()` — and the drive loop polls there rather than treating "nothing scheduled" as stalled,
because a request in flight is a thread-pool completion `TimeUntilNextScheduledWork` cannot report. Last,
**every engine carries a `DiagnosticsSink`**, because that is what makes an
exception escaping a timer callback, a `queueMicrotask` callback or an event listener report-and-continue
rather than erupt from the pump — the environment the corpus was written for, and the same choice
`WorkerRequest.CreateDefaultOptions` already makes for a worker engine. It is a *recorder* rather than
`DiagnosticsSink.Null` on purpose: before it existed such an exception erupted and the driver reported a
harness error for the whole file, so `WptHarness` turns any recorded uncaught callback error into that same
harness error unless the file declared `setup({allow_uncaught_exception: true})` — which is upstream's own
rule, and the second and last member of the properties bag the shim acts on. That rule stops at the file's
own completion boundary, which is upstream's too: once a `setup({single_test: true})` file's one test has a
result (`tests.tests[0].phase >= HAS_RESULT` upstream, `__wpt.fileTestComplete` here) a callback that throws
afterwards is ignored, because the four such files arm a guard timer a browser lets fire. The predicate is
"the file's one test has a result" and never "nothing is outstanding" — the latter would silence a file whose
tests are all synchronous, which has an empty outstanding list from its first line.

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
The same file's **drift-verification recipe is generated by the same tests**, between the two markers under
"Verifying that nothing has drifted": the extensions its `find` names, the number of `gh api` calls it makes
and the size of the tree it walks are read off `WptCorpus.Paths`, and `JINT_WPT_CENSUS=update` rewrites them
like the table. That is [#3647](https://github.com/sebastienros/jint/issues/3647): a *verification statement*
had been typed, so it was four vendored suites out of date and naming five file extensions where the corpus
has nine — the `.asis` responses and the `.headers` sidecars had never been compared with upstream at all.
The outcome sentence below the block stays prose, because reproducing it needs the network; what the figures
give it is a subject, so a bump that moves them says the run has to be done again.

A ninth, which nothing in the `.any.js` lane can reach and which every `.html` test will. `WptServer` has a
second half, `WptServerFiles`: wptserve's content-type table, its `.headers` sidecars and its `.sub.` template
language, ported from `tools/wptserve/wptserve/` at the pin and held to it by `WptServerTests` the same way
the `.py` handlers are. Three things about it decide where a divergence goes. **There is one origin** — one
loopback host, one port, no TLS — so every host-shaped token (`{{host}}`, `{{domains[www2]}}`,
`{{hosts[alt][]}}`) answers the same address and every `{{ports[…][…]}}` the same port; a file whose subject
is a *second* origin therefore reads as same-origin, which makes it un-runnable rather than merely different
and puts it in `_notVendored` or the exclusion table, never in a green run. **Anything unresolvable is a 500,
never a placeholder left in place**: an undefined variable, a `?pipe=` other than `sub`, a `{{ host }}` whose
spaces wptserve's own tokenizer rejects. Serving the file as though the query were not there is how a server
limitation becomes a failing assertion about the engine three layers away — which is exactly what
`xhr/abort-after-timeout.any.js`'s `?pipe=trickle(d1)` was. And **`resources/` and `common/` are helper roots,
never suites**: `WptCorpus.TestFiles` refuses to be asked about them, because a `.any.js` under one would be
the harness testing itself. `Vendor/resources/testharness.js` is upstream's real harness for a page to load,
`Prelude/testharness-shim.js` remains Jint's own for the `.any.js` lane, and `resources/testharnessreport.js`
is deliberately **not** vendored — upstream's is a stub that exists to be replaced, so Jint's lives in
`Prelude/` and `WptServer` takes an overlay for it per instance.

A tenth, because the file that used to be about one driver is now the corpus two of them share.
**`Jint.Tests.Browser/Wpt/` is the browser lane**, and everything above applies to it — one corpus, one pin,
the exclusion table as the artefact, `NeedsTriage` as the debt, the census as a ceiling — while it lives in
another project because it loads a `.html` into a real `Page` and this one may not reference `Jint.Browser`.
That project references *this* one and reaches `WptCorpus`, `WptServer` and `WptExclusion` through
`InternalsVisibleTo`; the dependency is one way, and copying any of the three would give the two lanes two
pins that nothing would say had drifted. Three consequences reach back into this directory and are the reason
this paragraph is here rather than only there. A vendored **document** — `.html`, `.htm`, `.xhtml`, `.xht` —
must be under a directory `WptCorpus.BrowserSuites` names or under a shared helper root, and
`EveryVendoredFileIsAccountedFor` fails otherwise, because a document no lane claims would be embedded,
byte-verified and run by nothing. `WptServer` **synthesizes** `<name>.any.html` for a `.any.js` file, which is
`WptServerWrappers`, a port of upstream's `AnyHtmlHandler` held to `tools/serve/serve.py` at the pin the same
way `WptServerFiles` is held to `tools/wptserve/`; the dedicated-worker wrapper is deliberately not generated,
for the reason `workers/*.worker.js` is a not-vendored row. And `_notVendored` here covers `.any.js` while the
lane's own table covers documents, so a directory both lanes touch has a row in each and neither may name a
file the other vendors. [`Jint.Tests.Browser/Wpt/AGENTS.md`](../../Jint.Tests.Browser/Wpt/AGENTS.md) is the
rest of it.
