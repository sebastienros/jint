# Vendored web-platform-tests

Everything in this directory is copied verbatim from
[web-platform-tests](https://github.com/web-platform-tests/wpt), at commit

    6c7127bdd9f2cc6a3668fd9791757843e09d5a9e

`wpt-LICENSE.md` is that commit's `LICENSE.md`; the corpus is redistributed here under the 3-Clause BSD
License it carries. Paths under this directory mirror paths in the wpt tree, which is what the harness's
`// META: script=` and `fetch()` resolution rely on — see `Jint.Tests.csproj` for the `LogicalName` that
keeps them intact through embedding.

One directory carries a second licence, because upstream vendors third-party code of its own:
`compression/third_party/pako/` is [pako](https://github.com/nodeca/pako) under the MIT License, with wpt's
own copy of that licence and its `README` beside the minified inflater. Three compression files check their
output by inflating it with pako rather than with the engine's own `DecompressionStream`, which is the point
— a round trip through one implementation proves nothing about the bytes.

## What runs this

`Jint.Tests/Wpt/WptTestRunner.cs`, one xUnit theory case per `.any.js` file, on a fresh engine built with
`UseWebApis(WebApiFeatures.Default)` plus the fetch object model — `Headers`, `Request` and `Response`, and,
for every file but the seventeen the [server lane](#the-server-lane) names, pointedly not `fetch`, so no suite
gets outbound network access — and a `DiagnosticsSink`, which is what makes
an exception escaping a timer callback, an event listener or a `queueMicrotask` callback report-and-continue
rather than erupt from the pump. `WptDiagnosticsSink` says why the driver needs one and why it records the
reports instead of discarding them; the timers-and-microtask section below has the same account in prose.
`Jint.Tests/Wpt/Prelude/testharness-shim.js`
— *not* vendored — stands in for upstream's `testharness.js`; its header says what it implements and where it
deliberately differs. `WptHarness.cs` documents the three decisions a reader is most likely to want: the
engine supplies its own `setTimeout` — the shim's `step_timeout` is a forwarder onto it, so the streams
suites' 45 timer sites are decided by the shipped `TimerQueue` — `// META: variant=` sharding is ignored
because one unsharded run is the union of every variant, and why the object model is there at all
(`url/urlencoded-parser.any.js` runs each of its 35 inputs through `URLSearchParams`, `Request.formData()`
and `Response.formData()`, one algorithm reached three ways; and the `headers/` and `response/` suites are
about those three interfaces and nothing else, which is what let that half of the corpus be vendored years
before there was anything for the other half to talk to).

Twelve standards are vendored: `url/`, `encoding/`, `compression/`, `urlpattern/`, `hr-time/` and
`user-timing/` as one suite each, `FileAPI/` as **three** (its root, `blob/` and `file/`), `workers/` as
**four**, `html/webappapis/` as **three** (timers, microtask-queuing, structured-clone), `dom/` as **two**
(events, abort), `fetch/api/` as **five** (basic, body, headers, redirect, response), `WebCryptoAPI/` as
**eight** and `streams/` as **seven** — their root files plus one suite per sub-directory, because
`WptCorpus.TestFiles` lists a directory's own files and never descends. That is 293 theory cases over 40,983
assertions, of which 2,982 do not pass and every one is named in the driver's table; the whole driver runs in
about two minutes.

Those three figures are a census taken at the pin rather than a running tally, so they are restated whenever a
change moves them; the counts before
[#3260](https://github.com/sebastienros/jint/issues/3260) stood a wpt server up were 273 / 40,657 / 2,889,
before [#3195](https://github.com/sebastienros/jint/issues/3195)'s interface-object exposure 270 / 40,631 /
2,907, and the paragraph they replaced had been left at 269 / 40,617 / 2,980 by an earlier change.

The corpora arrived a group at a time, most of them under
[issue #3185](https://github.com/sebastienros/jint/issues/3185); this file records what each of them says
about the engine, and the [inventory](#the-whole-corpus-standard-by-standard) at the end has the figures
corpus by corpus and lists what remains deliberately unvendored.

## Two lanes: the top-level engine, and a real worker

Every suite but one runs its file in the driver's own engine. The `workers/` corpus mostly does not, and
cannot: nearly every `.any.js` file in it that is reachable at all carries `// META: global=worker` or
`global=dedicatedworker`, because the file's whole subject *is* the worker global. Run in the driver's
top-level engine, `workers/Worker-custom-event.any.js` would test that engine's own `addEventListener`, pass,
and prove nothing — the same emptiness that keeps `urlpattern.https.any.js` out of the tree.

So the driver has a second lane. `WptWorkerProvider` is a `WorkerProvider` — the shipped host extension point
— and the file becomes the body of a **real module worker**, constructed by a real `new Worker(…, { type:
'module' })` from a parent engine, with the parent and the worker pumped cooperatively on the test's own
thread. Nothing here starts a thread: `OnWorkerStarted` starts nothing and the drive loop calls
`ProcessTasks` on the parent and on each live worker in turn, so a suite's outcome cannot depend on how two
schedulers interleaved. Results are read straight off the worker engine between turns rather than posted back,
so a defect in the serializer would present as a failing assertion rather than as every worker suite reporting
nothing.

Three consequences worth knowing:

* **`WptHarness.RunsInAWorker` picks the lane from the directory *and* the `// META: global=` key**, and needs
  both. The directory alone would force the lane on a `workers/` file that is really about a window creating a
  worker — which most of upstream's `workers/` tree is. The key alone would let a corpus bump move a settled
  suite by editing one comment, and would move the wrong ones: `global=window,worker` is true of both lanes and
  says nothing about which is worth running. `EveryWorkerLaneFileIsAWorkersFile` pins both directions.
* **The file is a module, where a browser's `.any.js` for a worker is a classic script.** Jint runs module
  workers only, so there is no classic script to evaluate and no `importScripts` to pull the harness in with;
  the loader serves one module whose source is the shim, then the file's `// META: script=` helpers, then the
  file — the same three-step composition the top-level lane performs with three `Execute` calls, and sound
  because the shim assigns everything it exports onto `globalThis` explicitly. The one behavioural difference
  is that module code is strict, and **two vendored rows now depend on sloppy mode**:
  `interfaces/WorkerGlobalScope/self.any.js`'s `self = 1` and `Worker-replace-self.any.js` both assign to
  `self` and assert the assignment was refused *silently*, which is what a read-only attribute does in sloppy
  mode and not what it does here. They were writable-either-way when the lane was built, and became the lane's
  own divergence the day [#3224](https://github.com/sebastienros/jint/issues/3224) made
  `WorkerGlobalScope.self` read-only; they are `NeedsClassicWorkerScript` below.
* **The worker gets the same environment the top-level engine gets**: `WebApiFeatures.Default` minus the
  grants a worker never inherits, plus the fetch object model, plus the shim's resource reader. A file's
  outcome therefore does not depend on which lane ran it.

There is one file the rule sends the other way, and it is the rule's second half rather than an exemption
from it. `workers/modules/dedicated-worker-import.any.js` carries no `// META: global=` key at all, because
its subject is a *page* creating workers — nine of them, each running a vendored corpus module whose subject
is `import`. So it runs in the top-level lane, and `WptHarness` gives a `workers/` directory a
`WptWorkerProvider` there too: the workers it creates are real ones, and the provider's second shape serves
them modules out of this tree, resolving a specifier exactly as the shim's `fetch` does. A specifier the
corpus does not hold is a startup failure the parent hears as an `error` event rather than a harness error,
which is what lets the two cases needing a wpt server be per-test exclusions. `_topLevelWorkersFiles` names
which files are meant to take this route, so a corpus bump that adds another has to say so.

The shim changed in one place for this. It used to install `self` unconditionally; it now does so only when
the engine has not, because `WorkerGlobalScope.self` is read-only in HTML and an unconditional assignment
would both overwrite what is under test and — the day the engine makes it read-only — throw out of a
strict-mode function and take every suite with it. That day has come:
[#3224](https://github.com/sebastienros/jint/issues/3224) landed, so `global.self !== global` is false on
every worker engine and the guard is the only thing keeping the shim off a read-only attribute.

One thing deliberately did **not** change: `GLOBAL.isWorker()` still answers `false` in the worker lane. The
shim cannot tell the lanes apart and nothing in the corpus asks — `isWindow()` is the only one of the three
ever called, and `false` is the right answer for it in both lanes. A file that guards a worker-specific branch
on `isWorker()` would skip it silently, so vendoring one means giving the shim a way to know first. That is
recorded rather than fixed because an unused mechanism is one nothing proves right.

Tests that do not pass are named in the driver's exclusion table with the category they belong to. An entry
there must match at least one failing test and no passing one, so a fix, a rename or a corpus bump cannot
leave a permanent exemption behind — the run fails until the table is brought back in line.

## The server lane

A third lane, added by [#3260](https://github.com/sebastienros/jint/issues/3260). Seventeen files —
`WptHarness._serverBackedFiles` names every one — run on an engine that has the shipped `fetch`, against
`WptServer`: an in-process HTTP/1.1 origin on the loopback interface, started once per test run.

**Why there is one.** Thirteen rows of the not-vendored table below used to read *"needs a wpt server"*. That
was roughly a quarter of everything parked, and it understated itself twice over: a file needing a server
usually cannot produce a test report at all, so it landed here rather than in the exclusion table, and the
per-test `NeedsWptServer` category was a single row. What those files want turned out to be narrow — a static
file, a header echo, a status code, a redirect, a trickled body. Not a browser, not a DOM.

**Why it is not `wptserve`.** Deno's runner drives the real Python server against an upstream checkout. That
would put a Python dependency in a .NET test suite's CI and give up the vendored-and-byte-verified model this
file is about. `WptServer` serves the *vendored* corpus instead — `WptCorpus`, the same bytes every other
suite reads — so nothing about provenance changes.

**What is a divergence, and how it is checked.** The `.py` handlers cannot run, so six of them are
reimplemented in C# and that is the one part of the corpus that is not vendored bytes. `WptServerTests` is
what stops a reimplementation quietly answering something else: thirteen tests, each written from the
upstream handler's source at the pin and named after it, so a corpus bump that changes one has something to
fail. What is ported and what is not:

| Upstream handler | What the C# does | What it deliberately does not |
| --- | --- | --- |
| `inspect-headers.py` | echoes the named request headers back as `x-request-<name>` | the `?cors` branch — Jint has no CORS model, so every file that passes it is parked for that instead |
| `status.py` | the status code, reason phrase, content type and body all out of the query | — |
| `method.py` | echoes the method, the four content headers (or `NO`) and the body | the `?cors` branch |
| `redirect.py` | `redirect_status`, `location`, the `simple` flag, the query-preserving location rewrite with its `&count=` hop counter, and the `token`/`max_count` stash | the `Access-Control-*` headers, the `OPTIONS` preflight branch, `redirect_referrerpolicy`, `delay` (no vendored file passes one) |
| `redirect-empty-location.py` | a 302 with an empty `Location` | — |
| `clean-stash.py` | drops one token's stash entry | — |
| `trickle.py` | `count` lines of `TEST_TRICKLE`, one every `ms` ms, with the same delay before the headers | — |

Everything else is a file out of the vendored tree, served with a content type from its extension; a path the
corpus does not hold is a 404 rather than a CLR exception, because unlike the shim's resource reader this one
is answering a request the corpus itself composed.

**Three things about the lane are load-bearing.**

* **It is a list, not a rule.** Giving the whole `fetch/api/` corpus the lane would turn thirty already-green
  rows into network-dependent ones for nothing: they build their own `Response` and never ask for a URL.
  `TheServerLaneHoldsExactlyTheFilesItNames` holds the list to the corpus in both directions, because the
  lane is the only place in the driver that grants `WebApiFeatures.Fetch` and is therefore the boundary of
  the promise that no suite can open a socket. The engine's `Options.WebApi.Fetch.UrlFilter` is
  `WptServer.Owns`, re-run on every redirect hop, so even inside the lane nothing can reach the network.
* **The shim supplies the API base URL the engine deliberately has not got.** Every file in the fetch corpus
  writes `fetch("../resources/status.py?…")`, and `RequestConstructor` documents declining to resolve a
  relative URL — there is no document, and a host is told to "resolve it yourself with
  `new URL(relative, base).href`". The shim does exactly that, against a `location.href` that is the URL the
  server really serves the file at. It is the harness playing the part of the browsing environment, as it
  already does by supplying `location` and `GLOBAL` at all. It pointedly does **not** wrap `Request`: a file
  constructing one from a relative url is asserting about the constructor, and those rows stay excluded under
  `NeedsApiBaseUrl` rather than being fixed up by the harness.
* **The drive loop had to learn to wait for something with no due time.** Every other lane treats "nothing
  queued and nothing scheduled" as proof that pumping cannot change the answer. A request in flight is
  neither — it is a completion on a thread-pool thread, and `Tasks.TimeUntilNextScheduledWork` has nothing
  to report about it, which is the case `Tasks.WaitForScheduledWork` documents as findable only by
  polling. So the server lane polls, with an idle timer reset by *progress* (a test settling) rather than by
  the wall clock, and a grace period deliberately longer than the lane's own `fetch` timeout so that a
  request the server never answers becomes a failing test rather than a stalled file.

**What it cost and what it bought.** The lane added 20 files and 326 assertions, and cost about **5 s**:
medians of five `--filter Jint.Tests.Wpt` runs each side on one Windows machine, 68 s before and 73 s after,
with the count identical on every run of both. Of those 326 assertions 233 passed the day the lane landed;
the 93 that did not were named in the exclusion table under five categories, four of which were engine defects
filed separately (see `WptDivergence.NeedsTriage`) and the fifth of which is not an engine matter at all — the
.NET HTTP stack does not carry a header value above ASCII, which
`WptServerTests.AHeaderValueAboveAsciiDoesNotSurviveTheHttpStack` measures with no engine in the picture.
**Every one of those defects is now fixed**, so 257 assertions pass and the 69 that do not are decisions and
that transport limit — see [what the fetch *network* corpus says](#what-the-fetch-network-corpus-says-about-this-engine)
below for the arithmetic.

## Deliberately not vendored

The driver enforces this list (`WptTestRunner._notVendored`): a re-vendor that brings one of these back
without revisiting the reason fails rather than quietly adding a red suite.

| Upstream path | Why not |
| --- | --- |
| `url/idlharness.any.js`, `encoding/idlharness.any.js` | Need `/resources/idlharness.js` and `/resources/WebIDLParser.js` — a WebIDL conformance framework an order of magnitude larger than the shim, testing a layer Jint's source-generated built-ins do not have. |
| `url/IdnaTestV2.any.js`, `url/IdnaTestV2-removed.any.js` | A 314 kB UTS-46 conformance corpus. `Jint.WebApi.Url.Parsing.Idna` builds on `IdnMapping` and documents where that diverges (VerifyDnsLength, CheckHyphens, ICU version skew); running it is an IDNA triage of its own rather than part of standing the harness up. |
| `encoding/legacy-mb-*`, `encoding/iso-2022-jp-decoder.any.js`, `encoding/replacement-encodings.any.js` | The Encoding Standard's legacy multi-byte decoders and the replacement encoding, which [issue #3106](https://github.com/sebastienros/jint/issues/3106) implements. `replacement-encodings` additionally needs `XMLHttpRequest`. The single-byte families that used to share this row are implemented, and their two files are vendored — see [Encoding, the single-byte half](#encoding-the-single-byte-half). |
| `encoding/unsupported-encodings.any.js` | Decodes through `XMLHttpRequest` and `data:` URLs (`encoding/resources/decoding-helpers.js`). |
| `WebCryptoAPI/*.tentative.https.any.js` (and the helpers only they use) | Upstream marks a file `.tentative` when it tests a proposal the specification has not adopted: ML-KEM, ML-DSA, KMAC, cSHAKE, SHA-3, TurboSHAKE, KangarooTwelve, AES-OCB, ChaCha20-Poly1305, Argon2, Ed448/X448, `getPublicKey`, `supports`. Jint registers what [the algorithm overview](https://w3c.github.io/webcrypto/#algorithm-overview) lists and nothing else. |
| `WebCryptoAPI/**/*_Ed25519.https.any.js`, `*_X25519.https.any.js`, `*/eddsa*`, `*/cfrg_curves*`, `*/okp_importKey*` | Curve25519. The BCL ships no X25519 or Ed25519 primitive — there is no `ECCurve` for either — so the whole family is out of scope for a crypto layer written against it. The campaign that built `crypto.subtle` excluded them for the same reason. Rows that sit *inside* a file which is otherwise about something else (the X25519 rows of `derived_bits_length`) are excluded one by one under `NeedsCurve25519` instead. |
| `WebCryptoAPI/serialization/*` | Round-trips a `CryptoKey` through `structuredClone`, which is HTML's serialization steps for a platform object rather than anything `crypto.subtle` does. |
| `WebCryptoAPI/encap_decap/*` | ML-KEM key encapsulation, a proposal. |
| `WebCryptoAPI/secure_context/*` | A `.sub.html` test for a browsing context. |
| `WebCryptoAPI/import_export/crashtests/*` | A crashtest — a regression reproduction rather than an assertion. |
| `WebCryptoAPI/tools/*` | The corpus's own Python generator. |
| `streams/transferable/*.window.js`, `streams/transferable/resources/*` | The directory itself **is** vendored now that transferring a stream works ([issue #3199](https://github.com/sebastienros/jint/issues/3199)) — but it holds exactly one `.any.js` file. The two `.window.js` tests drive an iframe and a `MessagePort` helper page, and `resources/` is the iframe, worker, shared-worker and service-worker plumbing those and the directory's `.html` files load. The blanket "`.any.js` only" rule below already excludes all of it; these rows say so by name, because a half-vendored directory should not look like an oversight. |
| `streams/readable-streams/owning-type*.tentative.any.js` | Upstream's `.tentative` marker again: owning-type readable streams are a proposal the Streams Standard has not adopted. |
| `streams/*/crashtests/*` | A crashtest — a regression reproduction rather than an assertion. |
| `compression/compression-output-length.any.js`, `compression/compression-stream.any.js` | Both fetch a binary fixture out of wpt's `/media/` directory — a 384 kB WebM, and for the second a WebVTT file as well — and read it back with `response.arrayBuffer()` / `response.bytes()`. The shim's `fetch` is a *text* reader over the vendored tree, so neither the transport nor the accessor exists here, and vendoring a third of a megabyte of video in order to compress it would be a strange thing for this corpus to carry. `compression-stream.any.js` additionally calls `fetch` at file scope, so its failure could not even be a per-test one. |
| `compression/decompression-extra-input.any.js` | Writes a member plus one trailing byte and never closes the writer, so its second `reader.read()` settles only if the trailing byte errors the stream. It does not here — that is the second of `DecompressionCodec`'s two documented divergences — so the read waits for input that cannot arrive and the *file* stalls rather than any test failing, which is a harness error no per-test exclusion can name. The divergence is still asserted, by the four excluded rows of `decompression-corrupt-input.any.js`. |
| `urlpattern/urlpattern.https.any.js` | Byte-identical to `urlpattern.any.js` (both are two `// META:` lines). Upstream ships both so a browser runs the corpus over http and over https; Jint has no scheme to be served over, so the second copy would run the same 370 cases again and assert nothing the first did not. |
| `urlpattern/*.tentative.any.js`, `urlpattern/*.tentative.https.any.js` | Upstream's `.tentative` marker: `compare()` and `generate()` are proposals the URL Pattern Standard has not adopted. Covered by the two existing `*.tentative.*` rows. |
| `FileAPI/file/send-file-formdata*.any.js` | All four POST a `FormData` to wptserve's `/fetch/api/resources/echo-content.py` and assert on the multipart body that comes back, so they need the fetch object model, an outbound request and the server's own Python handler. Serializing a `FormData` as `multipart/form-data` is a fetch body's job and arrives with that feature, which `WebApiFeatures.Default` never includes. |
| `FileAPI/fileReader.any.js` | Jint has no `FileReader` — a `Blob` is read here through `text()`, `arrayBuffer()`, `bytes()` and `stream()` — and this file is about that reader's state machine (`readyState`, `abort()`, the progress events) rather than about a `Blob`. It is the only `.any.js` in the File API's root that is not vendored; its sibling `unicode.any.js` needs no reader and is. |
| `workers/*.worker.js` | Twenty-one files that look like the most runnable thing in the directory and are the least: **every one opens with `importScripts("/resources/testharness.js")` at file scope.** A `.worker.js` is a *classic* worker's top-level script, and Jint runs module workers only — `importScripts` is present and throws a `TypeError`, which is the module-worker step [the standard itself prescribes](https://html.spec.whatwg.org/multipage/workers.html#dom-workerglobalscope-importscripts). So the file throws before registering a test, and a harness error is for the whole file rather than anything a per-test exclusion can name. What they assert about the worker global is asserted instead by the `.any.js` files beside them and by `Jint.Tests/Runtime/WebApi/WorkerMechanismTests.cs`. |
| `workers/modules/*.sub.js`, `workers/modules/resources/*.py`, `workers/modules/resources/static-import-redirect-worker.js` | What a wpt server would have produced: a `.sub.js` worker importing from a second origin wptserve substitutes a host into, and the `redirect.py` / credentials / referrer handlers. Two of the nine cases of `dedicated-worker-import.any.js` name one of these; they are excluded per test under `NeedsWptServer` rather than being a reason not to vendor the file, because the worker's module loader refuses the specifier and the parent hears the startup failure as an `error` event — the test's own reject path. |
| `workers/modules/*.window.js`, `workers/modules/shared-worker-*` | A browsing context, and a `SharedWorker`. |
| `workers/modules/dedicated-worker-import-blob-url.any.js`, `workers/modules/dedicated-worker-import-data-url.any.js`, `workers/modules/resources/*data-url*`, `workers/modules/resources/*block-cross-origin*` | Need `URL.createObjectURL`, a `data:` module loader, and a second origin. |
| `workers/SharedWorker-*.any.js`, `workers/semantics/interface-objects/*` | `SharedWorker` and `SharedWorkerGlobalScope`, which Jint does not have — the design records it as still open, needing a cross-engine name registry of the shape `BroadcastChannelBroker` has. A `global=sharedworker` file cannot even be run in the worker lane: there is no shared worker to be the global of. |
| `workers/examples/*` | Upstream's own tutorial for writing worker tests, and it teaches wpt rather than testing an engine: `general.any.js` is two tests, the second asserting `location.pathname === "/workers/examples/general.any.worker.js"` — the path of the glue script the wpt server generates for a `.any.js` file. There is no server here to generate one. `onconnect.any.js` beside it is `global=sharedworker`. |
| `workers/Worker-location.sub.any.js`, `workers/interfaces/WorkerUtils/importScripts/*`, `workers/importscripts_mime*.any.js` | `.sub.` is wptserve's server-side substitution: it rewrites `{{host}}` and `{{ports[…]}}` into a real origin before serving the file, so a vendored copy carries the placeholders verbatim. The `importScripts` families are classic-worker script loading on top of that, over server-chosen MIME types and cross-origin redirects. `Worker-location.sub.any.js` additionally asserts every member of a `WorkerLocation`, which is declined below. |
| `workers/interfaces/WorkerGlobalScope/location/*` | The whole assertion of `returns-same-object.any.js` is `location === location`. The harness shim installs a stub `location` of its own — `/common/subset-tests.js` reads `location.search` to pick a shard — so a vendored copy would pass **against the shim** while Jint deliberately has no `WorkerLocation` at all. A test that can only assert the harness is worse than no test, which is why this is a row here and not an exclusion. |
| `user-timing/supported-usertiming-types.any.js` | Reads `PerformanceObserver.supportedEntryTypes` at *file scope* to decide which promise tests to register, so on an engine with no `PerformanceObserver` it throws before the first test exists. The rows that reach for an observer from inside a test body are excluded one by one instead, under `NeedsPerformanceObserver`. |
| `html/webappapis/timers/evil-spec-example.any.js` | `setTimeout`'s string handler, which `TimerFunctions` documents declining: compiling the string is `eval` by another name and reachable even where a host disabled string compilation, so it is a `TypeError` here as it is in Node. The file's whole subject is that form, and it uses it at file scope. |
| `dom/events/*.window.js`, `dom/events/*.html`, `dom/abort/*.html` | For a browsing context, like every non-`.any.js` file. |
| `fetch/api/request/*` | Every file builds its `Request` from a **relative** url — `""`, `"./"`, `"../resources/…"` — and `RequestConstructor` documents why that cannot work: the specification resolves such a string against "the entry settings object's API base URL", which is a document's url, and an embedded engine has no document. Most of them do it at file scope, so there is not even a test to exclude. A host that wants a relative url resolves it itself with `new URL(relative, base).href`. |
| `fetch/api/crashtests/*` | A crashtest — a regression reproduction rather than an assertion. |
| `fetch/api/cors/*` | All 21 files are about the CORS request mode: `mode: "cors"`, a preflight, the `Access-Control-*` response headers, an opaque filtered response, and a second origin to be cross-origin *to*. `RequestConstructor` documents that this implementation neither reads nor validates `mode`, `credentials`, `integrity`, `referrer` or `referrerPolicy` — "nothing here pretends to honour a same-origin policy that does not exist" — so there is no origin model for these files to assert against, and [the server](#the-server-lane) would only let them fail more slowly. |
| `fetch/api/credentials/*` | A cookie jar (`SocketsHttpHandler`'s `UseCookies` is deliberately off — a jar shared by every engine in the process would be a cross-tenant channel), HTTP authentication, and the `credentials` init member above. |
| `fetch/api/policies/*` | Not one `.any.js` file: the directory is `.html` documents, their `.headers` sidecars and the `.js` they load. It is Content-Security-Policy and Referrer-Policy applied to a *document*. This row is the clearest example of how much the old "needs a wpt server" glob overstated itself — it was parking nothing runnable at all. |
| `fetch/api/abort/*` | `general.any.js` is 20 kB of `AbortSignal` against a server, and it opens by including `/common/get-host-info.sub.js` — wptserve's server-side substitution — and `../request/request-error.js`, out of the one fetch directory that has no API base URL. Both are file-scope requirements, so it cannot register a test here at all. `request.any.js` and `cache.https.any.js` construct their `Request`s from relative urls, which is that same story. |
| `fetch/api/basic/conditional-get.any.js` | An HTTP cache: `ETag` revalidation through `cache.py`. |
| `fetch/api/basic/error-after-response.any.js` | `bad-chunk-encoding.py`: a deliberately malformed chunked body, which needs a server writing bytes no framing layer would emit. |
| `fetch/api/basic/header-value-combining.any.js`, `header-value-null-byte.any.js`, `request-headers-case.any.js` | `/xhr/resources/`: `.asis` files served byte for byte, and two `.py` handlers in a directory nothing else here reaches. |
| `fetch/api/basic/http-response-code.any.js` | The connection-pool partition-key handler, and `get-host-info` substitution. |
| `fetch/api/basic/request-upload.any.js` | `echo-content.py` plus a streaming upload and a second origin. |
| `fetch/api/basic/gc.any.js` | `/common/gc.js` and a `garbageCollect()` the engine does not expose. |
| `fetch/api/basic/mode-same-origin.any.js` | The CORS request mode and a second origin. |
| `fetch/api/basic/referrer.any.js`, `request-referrer.any.js`, `fetch/api/redirect/redirect-referrer.any.js`, `redirect-referrer-override.any.js` | A `Referer` header, which an engine with no document never sends. |
| `fetch/api/basic/request-forbidden-headers.any.js` | The forbidden-header names, which `HeadersGuard` documents declining — the same reason the `headers-forbidden-override` rows are excluded one by one. Here it is the whole file. |
| `fetch/api/basic/request-headers.any.js` | Asserts the `Accept` and `Accept-Language` a browser adds, over most of its table. The `Accept` half is a real defect and is filed; see `NeedsBrowserRequestHeaders` for why the other half is not. |
| `fetch/api/basic/scheme-data.any.js`, `fetch/api/redirect/redirect-to-dataurl.any.js` | A `data:` URL fetch, and a `data:` redirect target. |
| `fetch/api/body/textstream.any.js` | `Response.prototype.textStream`, a Fetch pull the standard has not merged. The directory's other two files are vendored: they build their own `Request` and `Response` and never ask for a URL, which is what made the old glob wrong about them. |
| `fetch/api/redirect/redirect-back-to-original-origin.any.js`, `redirect-mode.any.js`, `redirect-origin.any.js` | A second origin, and for `redirect-mode` the opaque filtered response as well. |
| `fetch/api/redirect/redirect-schemes.any.js` | Redirects to `blob:` and other schemes, and `get-host-info` substitution. |
| `fetch/api/*/*.sub.any.js` | `.sub.` is wptserve rewriting `{{host}}` and `{{ports[…]}}` into a real origin before serving the file, so a vendored copy carries the placeholders verbatim. |
| `fetch/api/*/*.h2.any.js` | Needs an HTTP/2 server. `WptServer` speaks HTTP/1.1 on a raw socket, which is what lets it trickle a body. |
| `fetch/api/*/*keepalive*` | The `keepalive` init member, and a window creating iframes. |
| `fetch/api/headers/headers-no-cors.any.js` | The `"no-cors"` request mode. The rest of that directory is vendored. |
| `fetch/api/response/json.any.js` | Fetches a `data:` url and `/xhr/resources/utf16-bom.json`. |
| `fetch/api/response/response-blob-realm.any.js` | Needs a document and a second realm: it builds an `iframe` to obtain one. |

Every vendored file was timed at the pin; the slowest is
`derive_bits_keys/pbkdf2.https.any.js` at ~20 s for 8,632 cases (it is `// META: timeout=long` and sharded
nine ways upstream), then `generateKey/successes_RSA-OAEP.https.any.js` at ~7 s, which really does generate
156 RSA key pairs. Everything else is under 3 s. The whole streams corpus is 6.4 s for its 65 files, the
slowest being `readable-byte-streams/templated.any.js` at ~2.1 s and `readable-streams/templated.any.js` at
~1.0 s — both are `rs-test-templates.js` run over every stream shape — so nothing there is near the bar
either. `transferable/transform-stream-members.any.js`, the file the transferable-streams change added, is four
assertions and does not register.

The compression, urlpattern and File API corpora measure 2.6 s (15 files), 3.1 s (3 files) and 0.2 s
(14 files), run one after another on one thread. Two files carry almost all of that: `urlpattern.any.js`
at ~2.9 s, which is 369 patterns each compiled and then matched, and `compression/compression-large-flush-output.any.js`
at ~1.5 s, which compresses half a megabyte and inflates it again with pako. Both are `// META: timeout=long`
upstream. Everything else in the three is under 120 ms. The workers corpus added after them measures 0.46 s
for its 11 files — a whole engine is constructed, entangled and pumped per file, and that is still what it
costs.

Nothing was left out for being slow, but three of the files the timing and DOM corpora added *do* wait on a
real clock, so their figures are worth recording: `hr-time/basic.any.js` **2.4 s** (a deliberate 2,000 ms
sample, without which its `performance.now()`-against-`Date.now()` correlation would mean nothing),
`dom/abort/AbortSignal.any.js` **2.0 s** (a 2,000 ms window in which an already-aborted signal must fire no
event) and `html/webappapis/timers/clearinterval-from-callback.any.js` **1.3 s** (a 500 ms interval, then a
750 ms timer proving it was cleared). Measured file by file at the pin, the 74 files those two groups added
come to **7.6 s** of the driver's run; the two encoding files are the largest single contribution at 0.9 s for
7,504 assertions. `dom/events/Event-constructors.any.js`, added afterwards, is fourteen synchronous
constructor assertions and does not register.

Everything else upstream that is not a `.any.js` file is out of scope by construction: `.window.js`, `.html`,
`.worker.js` and `.xhtml` tests are for a browsing context or a worker — which is what excludes
`WebCryptoAPI/algorithm-discards-context.https.window.js`, `FileAPI/blob/Blob-constructor-dom.window.js`,
`FileAPI/blob/Blob-in-worker.worker.js` and `FileAPI/file/Worker-read-file-constructor.worker.js`. Jint now
*has* a worker, so `.worker.js` needed its own row above rather than resting on that sentence: those files are
out for what they are — classic-worker scripts — and not for want of somewhere to run them.

## Two copies of `urltestdata.json`

`Jint.Tests/Runtime/WebApi/Resources/` holds its own copy of `urltestdata.json` and `setters_tests.json`,
pinned to an earlier commit (`67456344…`) and run row-by-row against the parser with no engine at all by
`UrlCorpusTests`. This directory's copy is at the pin above and is read by the suites through their own
`fetch()`, on a real engine, through the real `URL` bindings — so the two exercise different layers and are
deliberately not merged in this change. Unifying them onto one pin is worth doing once the harness has
settled; it is a change to `UrlCorpusTests` as well as to this directory, and it belongs in its own commit.

## What the WebCryptoAPI corpus says about this engine

2,449 of its 24,136 assertions do not pass, and every one is named in the driver's table under one of five
categories, whose own documentation in `WptExclusions.cs` carries the citation. Three are the platform:
`NeedsPlatformCryptoParameters` (AES-GCM's 96-bit-only iv and 96-to-128-bit tag, RSA-OAEP's empty-only label,
RSA-PSS's hash-length-only salt — all four are limits of the BCL primitives, documented on the classes that
hit them), `NeedsCompressedEcPointImport` and `NeedsCurve25519`. One is the corpus running ahead of the
specification: `NeedsKeyEncapsulation` (ML-KEM's `encapsulateKey`/`decapsulateKey` `KeyUsage` values, which
the current `KeyUsage` enumeration does not declare). One is the corpus meeting an environment it was not
written for: `NeedsSecureContextModel`, which is all three tests of `historical.any.js` — the file asserts what
a **non-secure** context sees, and Jint has no scheme, no origin and therefore no secure-context bit. Its
`SubtleCrypto` row used to pass for a reason that was not a merit: there was no `SubtleCrypto` interface object
at all. [#3195](https://github.com/sebastienros/jint/issues/3195) installed it, because WinterTC's Minimum
Common API §5.1 lists it, and the row joined its two siblings — the one assertion in this corpus that this
change moved.

A sixth category was `NeedsQuotaExceededErrorInterface`, the nine `Large length: *` rows of
`getRandomValues.any.js`. They pass since
[#3189](https://github.com/sebastienros/jint/issues/3189) implemented WebIDL's
[`QuotaExceededError`](https://webidl.spec.whatwg.org/#quotaexceedederror) interface, and the entry and the
category are gone with them.

That figure is Windows and Linux, whose AES-GCM takes a 96- to 128-bit tag. **macOS has 216 more**, all in
`aes_gcm.https.any.js`: Apple's implementation takes a 128-bit tag and nothing else, so the four tag lengths
between 96 and 120 bits are refused there and their rows are scoped to that platform in the table.

A seventh was `NeedsTriage`, this corpus's **debt**, and it is paid. It held two genuine defects the corpus
found, recorded rather than fixed so that the change which first ran these suites was not also the change
that moved the engine. `SubtleCrypto` copied its caller's bytes before normalizing the algorithm where the
specification copies them after, which was every "… during call" row, and
[#3179](https://github.com/sebastienros/jint/issues/3179) moved each copy to its numbered step. ECDH's two
mismatched-curve rows answered the `OperationError` of the prose where every browser answers
`InvalidAccessError`, and [#3180](https://github.com/sebastienros/jint/issues/3180) made
`EcAlgorithm.DeriveBits` run the key-agreement checks before the *maximumLength* ceiling — the divergence
from the prose documented on `DeriveBits` itself and raised upstream as
[w3c/webcrypto#560](https://github.com/w3c/webcrypto/issues/560). Both entries left the table when they
started passing, which is the driver's own rule enforcing itself, so this corpus names no debt at all now.

## What the streams corpus says about this engine

1,170 assertions across 66 files, of which **4 do not pass** — 99.7%, which is what one expects of an
implementation written operation by operation against the standard, and also why the four are worth naming
individually. (Only the URL Pattern corpus below beats it, at 100%.)

`transferable/transform-stream-members.any.js` is the newest of the 65 and all four of its assertions pass.
It is the whole `.any.js` surface of transferable streams, and it asks one thing four ways: naming a
`TransformStream` *and* one of its two sides in a single transfer list must be a `DataCloneError`, in either
order. It passes because the transform stream's own transfer steps transfer each side in turn, so by the time
the list's other entry is reached that side is both locked and `[[Detached]]` — which is what makes the
refusal fall out of the steps rather than needing a rule of its own. The file took 0.4 s at the pin,
including the runner's start-up.

All four are a decision already taken: `readable-byte-streams/non-transferable-buffers.any.js`
(`NeedsWebAssembly`), which needs a `WebAssembly.Memory` buffer because that is the only `ArrayBuffer` a
script can obtain that cannot be transferred.

**Seven more used to be a decision and are not any more.** They were the rows of
`readable-streams/default-reader.any.js` that reach for the `ReadableStreamDefaultReader` **global**, under a
category — `NeedsStreamInterfaceGlobals` — that no longer exists:
[#3195](https://github.com/sebastienros/jint/issues/3195) installs all thirteen of the Streams Standard's
interface objects, as a browser does. The same ruling made `readable-byte-streams/construct-byob-request.any.js`
vendorable, which is the 66th file and the extra 16 assertions: it reads `ReadableByteStreamController.prototype`
and calls `new ReadableStreamBYOBRequest(…)` at *file scope*, so it used to throw a `ReferenceError` before
registering a single test — a harness error for the whole file that no per-test exclusion could name. All 16
of its rows pass, which is worth stating on its own: every one of them asserts that the constructor
whatwg/streams#870 took away is still gone, and it took a widening of the exposure to be able to ask.

**Five more used to be `NeedsTriage`, and [#3195](https://github.com/sebastienros/jint/issues/3195) fixed all
three defects behind them.** They are recorded here because two of the three turned out to be something other
than the triage note predicted, and because the third changes an ordering that shipped.

1. **The async iterator's methods were not enumerable.**
   `readable-streams/async-iterator.any.js`, "Async iterator instances should have the correct list of
   properties".
   [WebIDL's asynchronous iterator prototype object](https://webidl.spec.whatwg.org/#es-asynchronous-iterator-prototype-object)
   gives `next` and `return` `{ [[Writable]]: true, [[Enumerable]]: true, [[Configurable]]: true }`, and both
   reported `enumerable: false`. The triage note called it "one attribute on two `[JsFunction]` declarations",
   but no such attribute existed: `JsFunctionAttribute` had no `Flags` property at all, so no host could
   express WebIDL's rule — while the generator's *parser* already read a `Flags` named argument, which nothing
   could supply. The fix declares the property, defaulting to exactly the value the parser already applied for
   the omitted case (`PropertyFlag.NonEnumerable`, ECMA-262's rule for a
   [built-in function property](https://tc39.es/ecma262/#sec-ecmascript-standard-built-in-objects)), so it is
   a per-declaration opt-in and not a change of default: across the 224 files the generator emits for the
   `Jint` assembly, exactly two lines move, both in
   `ReadableStreamAsyncIteratorPrototype`. The `@@toStringTag` on that object was already right — WebIDL
   §3.7.10.2 gives an asynchronous iterator prototype object the class string
   "*interface* AsyncIterator" — and so is `ReadableStream.prototype[@@asyncIterator]`, which is
   non-enumerable because [WebIDL's iterable declarations](https://webidl.spec.whatwg.org/#es-iterable) give
   that symbol `{ [[Writable]]: true, [[Enumerable]]: false, [[Configurable]]: true }`. (The wider sweep that
   gave every operation in `Jint/WebApi/` WebIDL's attributes is
   [#3239](https://github.com/sebastienros/jint/issues/3239); it moved no corpus row in either direction, and
   that symbol is one of the few members it deliberately left alone.)

2. **`readable.cancel()` on a `TransformStream` rejected where it must fulfil** — three rows, one defect seen
   from three angles: `transform-streams/errors.any.js`'s "abort should set the close reason for the writable
   when it happens before cancel during start, and cancel should reject" (through `writer.abort()`) and
   "controller.error() should close writable immediately after readable.cancel()" (through
   `controller.error()`), and `transform-streams/general.any.js`'s "terminate() should abort writable
   immediately after readable.cancel()" (through `controller.terminate()`).
   `TransformStreamOperations.SourceCancelAlgorithm` was a faithful transcription of
   [TransformStreamDefaultSourceCancelAlgorithm](https://streams.spec.whatwg.org/#transform-stream-default-source-cancel)
   and stayed one, and `StartErroring`/`FinishErroring` were faithful too. What was wrong was the phrase they
   all hang off. Every "let *p* be **a promise resolved with** *x*" in the Streams Standard links to
   [WebIDL's operation](https://webidl.spec.whatwg.org/#a-promise-resolved-with), which is
   `NewPromiseCapability(%Promise%)` followed by calling its resolve function — always a *new* promise, so a
   thenable *x* is adopted through `NewPromiseResolveThenableJob` and costs two microtasks.
   `StreamPromises.ResolvedWith` used `PromiseResolve(%Promise%, x)` instead, which hands back *x itself*
   when *x* is already an ordinary promise. The reference implementation warns about precisely this in
   `lib/helpers/webidl.js`: "Cannot use original Promise.resolve since that will return value itself
   sometimes, unlike Web IDL."
   A `TransformStream` hands both of its controllers the *same* start promise, so the short-circuit flipped
   both `[[started]]` flags two microtasks early — early enough for `WritableStreamFinishErroring` to overtake
   the reaction `TransformStreamDefaultSourceCancelAlgorithm` registers, which then read `"errored"` where the
   standard leaves the writable `"erroring"`, taking the reject branch instead of the resolve one.

3. **`pipeTo()` reached the sink's `write` synchronously with an `enqueue()` on the source.**
   `piping/general-addition.any.js`, "enqueue() must not synchronously call write algorithm".
   `ReadableStreamPipe` reads through a raw `ReadRequest`, and a `ReadRequest`'s *chunk steps* are run
   synchronously by `ReadableStreamFulfillReadRequest` — so the write was started on the `enqueue()` call's own
   stack. [ReadableStreamPipeTo](https://streams.spec.whatwg.org/#readable-stream-pipe-to) leaves the mechanism
   flexible because "the exact manner in which this happens is not observable to author code", which is exactly
   the property a synchronous re-entry into author code destroys. The fix defers the *whole* of
   `PipeReadRequest.ChunkSteps` by one microtask, the same `AddToEventLoop` shape the tee already uses. Not
   less: deferring only the write would let the next step consult the writer's `ready` promise before the chunk
   just read had been charged to the destination's queue, and backpressure is the one thing the standard says
   must throttle the reads. Not more: "reads or writes should not be delayed for reasons other than these
   backpressure signals". `_currentWrite` is therefore assigned a microtask later than the chunk arrives, which
   `waitForWritesToFinish`'s existing re-check already covers — every shutdown that reaches it is itself a
   promise reaction queued behind that microtask — and a writer the pipe has already released is guarded
   against explicitly.

`readable-streams/garbage-collection.any.js` and `writable-streams/garbage-collection.any.js` pass, and it is
worth knowing why they can. They call `garbageCollect()` from `/common/gc.js`, whose fallback — reached in any
environment without `TestUtils.gc()`, `gc()` or `GCController`, browsers included — merely allocates a lot of
garbage and warns on the console. So these files assert that a stream and its reader keep working *across* a
point where a collection may have happened, never that one did; they are vendored on the same terms a browser
runs them without `--expose-gc`.

## What the compression corpus says about this engine

297 assertions across 15 files, of which **22 do not pass**.

**It was 84, and 68 of those were one word.** The Compression Standard's `CompressionFormat` enumeration
lists `brotli` alongside `deflate`, `deflate-raw` and `gzip`, and the corpus's `resources/formats.js` loops
over all four, so every per-format family in every file has a brotli row. Refusing the format was
*conforming* — `new CompressionStream(format)` step 1 is "if *format* is unsupported in `CompressionStream`,
then throw a `TypeError`" — but the corpus does not ask that question, it assumes support, and this was the
one exclusion category in this directory that was neither a platform limit nor a deliberate reduction: .NET
ships `BrotliStream`, on the same pull-stream shape `CompressionCodec`/`DecompressionCodec` already drove for
the other three formats. https://github.com/sebastienros/jint/issues/3210 wired it in, and **62 of the 68
rows went green with it**. The other six did not vanish, they moved: four to `NeedsWebAssembly` and two to
`NeedsIncrementalInflater`, joining their deflate and gzip siblings for reasons that have nothing to do with
brotli.

**16 are `NeedsWebAssembly`**: the `SharedArrayBuffer` and shared-`Uint8Array` rows of the two bad-chunk
files, which build their SAB through `WebAssembly.Memory` inline rather than through `common/sab.js`. All
four formats, both files, both chunk types — eight rows per file, and now one glob per chunk type rather than
one entry per format, which is a shape the table could not have while brotli's row of each pair failed for a
different reason.

**The remaining 6 are the two lenient-decompression divergences, and they are the reason this corpus was
worth running.** `DecompressionCodec` documents both on itself: the standard makes it an error for a stream
to end before its member is complete and an error for anything to follow the member, and detecting either
needs to know how many of the bytes handed over the decompressor actually *consumed* — which .NET's pull
streams do not report, `BrotliStream` no more than the deflate family.
`decompression-corrupt-input.any.js` is the only file in the corpus that asserts them, in six rows
(`truncating the input` and `trailing junk`, for each of that file's three formats — `deflate`, `gzip` and
`brotli`), and they are excluded under `NeedsIncrementalInflater` with that citation. Everything else that
file checks passes: a bad CMF or gzip ID, a bad FCHECK, an FHCRC flag, a corrupted last data byte, a wrong
ADLER32, CRC32 or ISIZE, a dictionary-compressed stream, every field the formats say may hold anything, and
all three unchanged inputs. So the divergence is exactly "a corrupt member is still rejected; an *incomplete*
or *over-long* one is not", which is what the class says — and adding a fourth format did not widen it,
because a brotli stream that cannot be parsed is still an error and the two that pass through are still only
these two.

A fifth file, `decompression-extra-input.any.js`, asserts the trailing-byte half by *waiting* for the error
rather than by comparing a result, and therefore stalls instead of failing — see the not-vendored table.

## What the URL Pattern corpus says about this engine

**373 assertions across 3 files, all of them passing** — the first corpus vendored here with no exclusion at
all. 369 of those are `resources/urlpatterntestdata.json` driven through `runTests`, which for every entry
compiles the pattern, compares all nine component pattern strings against what the constructor should have
canonicalized them to, and then checks `test()` and the whole `exec()` result object including the group
captures. `urlpattern-hasregexpgroups.any.js` guards its body with `assert_implements`, which is the one
assertion this change added to the shim.

## What the File API corpus says about this engine

342 assertions across 14 files (11 in `blob/`, 2 in `file/`, 1 in the root), **all of them passing**. Both
groups that used to be red are worth an account, because between them they are everything this corpus has
caught.

The eight that used to be red were the whole of `Blob-textStream.any.js`, under a `NeedsBlobTextStream`
category, because [the File API added](https://w3c.github.io/FileAPI/#dom-blob-textstream) `textStream()`
after this `Blob` was written. They pass since
[#3211](https://github.com/sebastienros/jint/issues/3211) implemented it — the blob's stream piped through a
UTF-8 `TextDecoderStream`, which is four steps over pieces the engine already had — and the two entries and
the category are gone with them.

### The three `Blob-constructor.any.js` rows this corpus found, and the engine defect behind them

The other three sat under `NeedsTriage`, failing with `TypeError: cannot construct iterator` thrown by
`Array.prototype.values` — and the defect was not in `Blob` at all. It is fixed
([#3209](https://github.com/sebastienros/jint/issues/3209)); the account is kept because it is the
best-documented thing this corpus has caught so far, and because the deleted entries are what now enforce the
fix.

```js
[...Array.prototype.values.call({})]                    // spec: []           was: TypeError
[...Array.prototype.values.call({length: '3', 0: 'a', 1: 'b', 2: 'c'})]
                                                        // spec: [a, b, c]    was: TypeError
[...Array.prototype.values.call({length: -1})]          // spec: []           was: TypeError
[...Array.prototype.values.call({length: null})]        // spec: []           was: TypeError
[...Array.prototype.values.call({length: true, 0: 'a'})]// spec: ['a']        was: TypeError
[...{[Symbol.iterator]: Array.prototype.values}]        // spec: []           was: TypeError
Array.prototype.values.call({get length() { throw e; }})// spec: throws at the first next()
                                                        // was: threw at values()
```

[`Array.prototype.values`](https://tc39.es/ecma262/#sec-array.prototype.values) is *ToObject(this)* followed
by *CreateArrayIterator(O, value)*, and neither step reads `length`; the iterator's own closure does
*LengthOfArrayLike* — a `Get` and a `ToLength` — on each `next()`. `ArrayPrototype.Values` instead gated on
`ObjectInstance.IsArrayLike`, which demands a `length` that is *present*, already a `JsNumber`, and
non-negative, and threw when it was not. That was wrong in six ways at once: absent means 0, a string or a
boolean is coerced, a negative clamps to 0, and the read belongs to `next()` rather than to `values()`.
`keys` and `entries` had the same three-line body and the same defect. The fix drops the gate from all three —
the per-`next()` read the specification asks for was already what the array-like iterator did — and
`ArrayIteratorReceiverTests` pins every shape above plus growth and shrink between two steps.

The corpus reached it because `new Blob(x)` converts `x` to a WebIDL `sequence`, and the file deliberately
hands it plain objects whose `@@iterator` *is* `Array.prototype[Symbol.iterator]` — which is how a browser's
`Blob` sees `{length: 1, 0: 'PASS'}` as a one-element sequence. It reproduced with no web API enabled at all,
which is what made it an engine finding rather than a `Blob` one. `Blob-constructor.any.js` is 73-for-73 now.

`Blob-stream.any.js` passes, and it is worth knowing why it can: it calls `garbageCollect()` from
`/common/gc.js`, whose fallback merely allocates a lot of garbage, so it asserts that a blob's stream keeps
working *across* a point where a collection may have happened — the same terms a browser runs it on without
`--expose-gc`. `Blob-constructor-detached-buffer.any.js` passes on the strength of
`Engine.Advanced`'s message ports: it detaches its buffer with
`new MessageChannel().port1.postMessage(buffer, [buffer])`.

## What the workers corpus says about this engine

**24 assertions across 12 files, of which 16 pass and 8 do not** — and the interesting figure is not the ratio
but that **six of the eight were decided in writing before a line of this corpus was run**, in the divergence
ledger of [issue #3167](https://github.com/sebastienros/jint/issues/3167): the two names the worker global
declines (three rows, #6 and #7), the nesting grant it withholds (one row), and module-workers-only (#2 — the
two `self` rows, which joined this count when the defect under them was fixed). The other two need a wpt
server. This is the smallest corpus here and the one that most nearly assays a *design* rather than an
implementation.

What passes is the worker global doing its job. `Worker-custom-event.any.js` adds a listener for a custom
event on `self` and dispatches one, so the worker global really is an event target.
`Worker-replace-event-handler.any.js` assigns `onmessage` eight times over.
`Worker-replace-global-constructor.any.js` replaces `self.MessageEvent`. `Worker-base64.any.js` finds `atob`
and `btoa`. `Worker-formdata.any.js` is the best of them: it builds a `FormData`, appends a `Blob` to it, and
then asserts that `postMessage(formData)` is a `DataCloneError` — so the worker global's `postMessage` is the
port's, running the real serializer, refusing the right value. The second row of
`semantics/multiple-workers/exposure.any.js` asserts `SharedWorker` is absent outside a window, and three of
the four rows of `interfaces/WorkerGlobalScope/self.any.js` pass on `self === self`, `'self' in self` and
`self instanceof WorkerGlobalScope`.

**Three of the eight failures are one decision family** (`NeedsDeclinedWorkerGlobals`): the worker global is
the global the engine already builds plus the worker names, and there are two names it deliberately does not
add. `WorkerLocation` (divergence #6) — a worker's script name is its `ModuleRecord.Location`, and there is no URL
for the other eight members to be parts of. `WorkerNavigator`, and `hardwareConcurrency` in particular (#7) —
in Jint the *host* owns every thread, so an engine answering a number would be guessing at a resource it does
not allocate. **The fourth** is `exposure.any.js`'s "Worker exposure" (`NeedsWorkerNesting`): nesting is off by
default, so `Worker` is `undefined` inside a worker until a provider grants it — a grant withheld rather than
a name declined, which is why it is a category of its own.

**Divergence #5 used to head that list and is closed.**
[#3195](https://github.com/sebastienros/jint/issues/3195) gave the worker global a real prototype chain —
`DedicatedWorkerGlobalScope.prototype`, then `WorkerGlobalScope.prototype` — and installed both interface
objects on the worker's global and on no other, so `self instanceof WorkerGlobalScope` is answered by walking
the chain rather than by a brand shim, and passes. The ledger's original reasoning was not wrong about the
danger, only about the remedy: what would have lied is an interface object with *no* chain behind it. The one
link the chain still declines is `EventTarget`, for that very reason — the worker global genuinely is not one,
so `WorkerGlobalScope.prototype` inherits from `%Object.prototype%` and `self instanceof EventTarget` stays
false.

**That ruling is also what made this the corpus's most expensive divergence stop being one.**
`workers/modules/dedicated-worker-import.any.js` — nine `promise_test`s over static, nested and dynamic
`import` in a module worker, exactly the feature Jint has — could not be vendored at all, because the
canonical "am I in a dedicated worker" sniff,
`'DedicatedWorkerGlobalScope' in self && self instanceof DedicatedWorkerGlobalScope`, is how every one of its
fixtures decides where to install its `onmessage` handler. With no interface object none of them installed
one, the worker never answered, and the file *hung* rather than failing — a harness error for the whole file
that no per-test exclusion could name. It is vendored now, with twelve fixture files beside it, and **seven of
the nine cases pass**: static, nested static, dynamic, nested dynamic, both orders of the mixed pair, and
`eval(import())`. The remaining two need a wpt server — a `.sub.js` fixture importing from a second origin,
and one importing through `redirect.py` — and they fail as *tests* rather than stalling, because the worker's
module loader refuses a specifier the vendored corpus does not hold and the parent hears the startup failure
as an `error` event, which is the file's own reject path. They are `NeedsWptServer`.

**The last two were the corpus's one `NeedsTriage` defect, it is fixed, and the rows still do not pass —
which is the more interesting half.** `interfaces/WorkerGlobalScope/self.any.js`'s "self = 1" assigns to
`self` and asserts it did not change; `Worker-replace-self.any.js` does the same and then asserts
`self instanceof WorkerGlobalScope`.

`WebApiRegistration` installed `self` once, for every global, as an ordinary writable data property, and its
own comment said which definition that was written against: "HTML exposes `self` through a `[Replaceable]`
accessor pair on **Window**" — which was the only global there was at the time.
[`WorkerGlobalScope`'s](https://html.spec.whatwg.org/multipage/workers.html#dom-workerglobalscope-self) is a
plain `readonly attribute` with no `[Replaceable]`, so the worker inherited the window's semantics along with
the property. [#3224](https://github.com/sebastienros/jint/issues/3224) fixed it the way two different IDL
definitions ask to be fixed, rather than by picking one: `WorkerGlobalScope.Install` replaces that one
descriptor with a non-writable one on the worker's global and on no other, so the top-level `[Replaceable]`
shape — where assignment *shadows*, and a script may rely on it — is untouched. The replacement is
non-clobbering by descriptor identity, so a `self` the host installed is left exactly as the host left it,
unread and therefore unmaterialized.

What the two rows report now is the **lane**, not the engine. A read-only attribute refuses an assignment two
different ways — silently in sloppy mode, with a `TypeError` in strict — and the corpus was written for the
first. This lane's file is the body of a module, so before the fix both failed because the assignment
*succeeded* — `self.any.js` with `expected object "DedicatedWorkerGlobalScope" but got 1`, and
`Worker-replace-self.any.js` because `self instanceof WorkerGlobalScope` had become false — and they now fail
with the `TypeError` strict mode owes (`FAIL: unexpected exception (TypeError) received while replacing
self.`). Nothing short of a classic-script worker would move either, so they are
`NeedsClassicWorkerScript` rather than debt, and the
engine's half is pinned in both modes by `Jint.Tests/Runtime/WebApi/WorkerMechanismTests.cs` — the module body
for strict, an indirect `eval` for the sloppy code this lane cannot otherwise reach.

The sibling the triage note called "the same shape and the same question",
`DedicatedWorkerGlobalScope.name`, turned out to be neither: it is
[`[Replaceable] readonly attribute DOMString name`](https://html.spec.whatwg.org/multipage/workers.html#dom-dedicatedworkerglobalscope-name),
so the writable data property it already had is what its own IDL asks for. It has a pin of its own beside
`self`'s, because the contrast is the whole point: HTML decided the two attributes separately, and so does
this engine.

## What the hr-time and user-timing corpora say about this engine

85 assertions, of which **6 do not pass**, and the split is almost exactly the one
`PerformancePrototype` predicts in its own documentation.

Five are `NeedsPerformanceObserver` and one is `NeedsPerformanceEventTarget`: the class lists
"`PerformanceObserver` and everything that reports to one, `toJSON`, `setResourceTimingBufferSize` and the
resource-timing surface, and the `EventTarget` this interface inherits from" as what it does not implement, and
says why each is *absent* rather than present-and-throwing — so that a script's own feature detection sees the
truth. What the corpus adds to that is a measurement of the cost: **only four files** of the user-timing suite
depend on an observer at all, and one more (`supported-usertiming-types.any.js`, not vendored) reads it at file
scope. The other fifteen read the timeline through `getEntries()` and pass, including the whole of
`mark.any.js`, `measure-l3.any.js` and `measure_syntax_err.any.js`.

**The four that used to sit beside them were one genuine defect, and it is fixed.**
`mark-errors.any.js` runs each of its five cases twice — once through `performance.mark(name, x)` and once
through `new PerformanceMark(name, x)` — and the constructor's half of four of them failed. The reading that
`performance.mark` "refused correctly" was the trap: the file calls it **unbound**, as
`testInfo.testFunction(self.performance.mark)`, so its `TypeError` came from the brand check on a `this` of
`undefined` and not from any conversion at all. `performance.mark('m', 123)` called properly returned a mark.
Both halves share one conversion — `UserTiming.ReadMarkOptions`, reached through
`PerformanceMarkConstructor.ReadArguments`, which `performance.mark` runs as its own step 1 — and it treated
every non-object as the empty dictionary where step 1 of
[WebIDL's dictionary conversion](https://webidl.spec.whatwg.org/#es-dictionary) says "if *jsDict* is not an
Object and *jsDict* is neither undefined nor null, then throw a TypeError". Fixing the shared conversion fixed
both halves at once, and `Jint.Tests/Runtime/WebApi/PerformanceTimelineTests.cs` pins the bound call the corpus
cannot make.

## What the timers and microtask corpora say about this engine

17 assertions across ten files, and **every one of them passes** — which is the answer that matters, because
these are the files that test the `TimerQueue` from the outside: `setTimeout` and `setInterval` sharing one id
space so `clearInterval` cancels a timeout, an interval cleared from its own callback staying cleared,
`setInterval(0)` and `setTimeout(0)` firing in registration order, `setInterval(fn)` with no interval at all
behaving as `0`, a negative delay clamping to `0`, and `2**32` wrapping to `0` through WebIDL's `long`
conversion. Four of them are `setup({single_test: true})` files, which the shim did not previously implement.

**The tenth file was a defect rather than a decline, and it is fixed.**
`queue-microtask-exceptions.any.js` throws from a `queueMicrotask` callback and expects to observe it as an
`error` event at the global scope. [HTML's `queueMicrotask`](https://html.spec.whatwg.org/multipage/timers-and-user-prompts.html#dom-queuemicrotask)
says "queue a microtask to invoke *callback*, given null and **"report"**", and WebIDL's
[*report the exception*](https://webidl.spec.whatwg.org/#report-the-exception) is HTML's *report an
exception* — fire `error` at the global scope, then tell the console. Jint instead let the throw erupt from
whatever was pumping the event loop: `TimerFunctions.QueueMicrotaskCallback` enqueued a bare
`callback.Call(...)`, where `TimerEntry.Fire` and `JsEventTarget.InvokePass` both carry the
`catch (JavaScriptException) when (… is { } diagnostics)` filter that reaches
`WebApiEngineState.FireGlobalErrorEvent`. So the exception left the engine before any listener could see it,
the file was a harness error rather than a failing test, and there was no test left for an exclusion to name —
which is why it lived here rather than in the table. Item 11 of
[issue #3212](https://github.com/sebastienros/jint/issues/3212) gave the callback the third instance of that
same filter, and the file is vendored and green. Its copy is byte-identical to upstream's at the pin —
`git hash-object` gives `01f32ac9ba14962fa99d4b263a8ca0f5a0daa161`, which is the blob id GitHub reports for
that path at commit `6c7127bdd9`.

Vendoring it needed one thing of the **driver**, and it is worth stating plainly because it is a decision
about the environment rather than about the engine. Jint reports an exception escaping an engine-invoked
callback — and carries on — only where the host installed a `DiagnosticsSink`; with none, the throw erupts,
which is the documented contract an embedder who configured nothing keeps. A conformance corpus is written
against the other half of it, so the driver's engines now carry a sink, exactly as the shipped
`WorkerRequest.CreateDefaultOptions` already installs `DiagnosticsSink.Null` on every worker engine and for
the same stated reason. The sink is a **recorder** rather than `Null`, because the report must not simply
disappear: before it existed, such an exception erupted and the driver reported a harness error for the whole
file, and upstream's own `testharness.js` does the same thing through a global `onerror` unless the file
declared `setup({allow_uncaught_exception: true})`. So the shim now honours that one flag — it is the second
member of the properties bag it acts on, beside `single_test` — and `WptHarness` turns any recorded uncaught
callback error the file did not declare itself ready for into the same harness error the eruption used to be.
Nothing else in the corpus moved: the sink changed no assertion's outcome anywhere, which the exclusion table
proves for itself, since a row that stopped matching a failing test would fail its suite.

## Encoding, the single-byte half

The two files that used to sit in the not-vendored table for
[issue #3106](https://github.com/sebastienros/jint/issues/3106) are vendored now that the single-byte decoders
are implemented. `textdecoder-fatal-single-byte.any.js` is 7,168 assertions and **passes entirely** — every
byte of every single-byte encoding, decoded with `fatal: true` and checked against the table.
`single-byte-decoder.any.js` is 336, and its 168 `(TextDecoder)` rows pass while its 168 `(XMLHttpRequest)`
rows are excluded under `NeedsWptServer`: that half asks `resources/single-byte-raw.py`, a wptserve handler,
to generate the bytes `0x00..0xFE` labelled with the charset from its query string.

The shim grew an `XMLHttpRequest` for it — GET only, read synchronously out of the vendored tree, `load`
dispatched on the engine's own timer — whose real job is the refusal: a request the corpus cannot serve throws
inside the test body naming what was asked for, so "there is no server here" arrives as a failing test rather
than as a missing global or a dead file. `WptHarnessTests` exercises it from both sides, against a real
vendored resource and against every shape it declines.

## What the DOM events and abort corpora say about this engine

76 assertions across thirteen files, and **every one of them passes**. Four of them did not, and all four
were `Event`'s legacy members: two named tests, below, and the two unnamed ones that kept
`Event-constructors.any.js` out of the corpus altogether, which is the thirteenth file and the end of this
section.

1. **`Event.isTrusted` was on the prototype, where WebIDL makes it an own property.**
   `dom/events/Event-isTrusted.any.js` takes `Object.getOwnPropertyDescriptor(new Event("x"), "isTrusted")`
   from two separate events and requires both to be an accessor and to be the *same* getter.
   [The DOM Standard](https://dom.spec.whatwg.org/#dom-event-istrusted) declares it
   `[LegacyUnforgeable]`, which [WebIDL](https://webidl.spec.whatwg.org/#LegacyUnforgeable) defines as
   "non-configurable and … exist[ing] as an own property on the object itself rather than on its prototype",
   with the attributes [§3.7.6](https://webidl.spec.whatwg.org/#es-attributes) gives it —
   `{ [[Set]]: undefined, [[Enumerable]]: true, [[Configurable]]: false }` — and which the same section
   *removes* from the interface prototype object's attribute set. `EventPrototype` declared it beside `type`
   and `bubbles` as an ordinary configurable prototype accessor.

   It is an own property of every event now, and it is **free**. `JsEvent.GetOwnProperty` answers it from the
   realm's one shared descriptor and `GetInitialOwnStringPropertyKeys` lists the name ahead of anything script
   adds — the shape `Function` uses for `length`/`name`/`prototype` and `JsError` for `message`. Measured on
   `net10.0` with `GC.GetAllocatedBytesForCurrentThread`, over 20,000 iterations, before and after the change:
   `new Event('x')` is **112 bytes** either way, and `new Event('x').isTrusted` is **112 bytes** either way.
   That is worth stating precisely because the obvious implementation is not free: giving an event one stored
   own property costs a `PropertyDictionary`, a list node and a descriptor, which the same probe measures at
   **+184 bytes**, more than doubling an event — and the engine creates one per `dispatchEvent` and more
   throughout the abort, message, worker and fetch paths. Two things did change for every event, both of them
   the point: `Object.keys(new Event('x'))` is `["isTrusted"]` and `JSON.stringify(new Event('x'))` is
   `{"isTrusted":false}`, which is what a browser answers.
2. **`Event` had no `srcElement` and no `returnValue`.** Both are in the DOM Standard's own interface —
   [`srcElement`](https://dom.spec.whatwg.org/#dom-event-srcelement), a plain `readonly attribute` whose
   "getter steps are to return this's target", and
   [`returnValue`](https://dom.spec.whatwg.org/#dom-event-returnvalue), whose getter is "false if this's
   canceled flag is set; otherwise true" and whose setter runs *set the canceled flag* — so they were missing
   members rather than a legacy extension nobody requires. `returnValue` in particular is not a property over
   a field: its setter is `preventDefault()` under another name, so a non-cancelable event and a passive
   listener both ignore it and assigning `true` can never clear a flag already set. The test that finds it is
   `AddEventListenerOptions-passive.any.js`'s "returnValue should be ignored if-and-only-if the passive option
   is true", which was the only one of that file's five rows to fail.

**`dom/events/Event-constructors.any.js` is vendored now, and getting it there needed a third member.** The
file used to sit in the not-vendored table because all fourteen of its tests are registered **without a
name**, so the driver reports every one of them under the same one and no per-test exclusion can single out
the two that fail. The reason recorded for those two was `srcElement` and `returnValue` — and that reading was
incomplete. Each of them asserts the whole initial state of a new event, `assert_true("initEvent" in ev)`
included, so implementing the two members above left the file exactly as red as it was, failing one assertion
later. [`initEvent()`](https://dom.spec.whatwg.org/#dom-event-initevent) is the third legacy member of the
same interface, and `initCustomEvent()` its `CustomEvent` counterpart; both are implemented, both are
*initialize an event* ([step by step](https://dom.spec.whatwg.org/#concept-event-initialize)) behind a
dispatch-flag guard, and the file now runs with all fourteen of its tests passing. Its copy is byte-identical
to upstream's at the pin — `git hash-object` gives `faa623ea92991b72742477a18471449f5382f1a8`, which is the
blob id GitHub reports for that path at commit `6c7127bdd9`.

One step of *initialize an event* has nothing to do here. "Set event's initialized flag" is read by exactly
one algorithm in the standard — `dispatchEvent`'s `InvalidStateError` guard — and unset by exactly one,
`document.createEvent()`. There is no `document` here, so every event Jint can build has the flag set from
birth and no observation can tell a stored flag from an assumed one.

Everything else passes, including all sixteen rows of `dom/abort/event.any.js`, all fourteen of
`AbortSignal.any()`'s composition and ordering rules, and `AbortSignal.timeout()` firing in registration order
— the last of which is a `TimerQueue` result as much as an abort one.

## What the structured-clone battery says about this engine

137 assertions, of which **3 do not pass**, and all three are the environment: a `WebAssembly.Memory`-backed
growable `SharedArrayBuffer`, an `ImageBitmap` and an `OffscreenCanvas`. Two rows that this corpus arrived
excluding are green on the branch it landed on, which is worth recording because it is the table working as
designed — "a non-serializable platform object fails" needed a `Response` to be non-serializable *with*, and
every engine the driver builds now carries the object model; and "a subclass instance will be received as its
closest transferable superclass" needed `ReadableStream` transfer, which
[issue #3199](https://github.com/sebastienros/jint/issues/3199) implemented.

**The other 30 were three defects**, filed as `NeedsTriage` when the corpus landed and fixed by
[issue #3212](https://github.com/sebastienros/jint/issues/3212). All three are kept written down here rather
than deleted with their rows, because in the first two the fix deliberately follows web-platform-tests past
what the prose of the owning standard currently says — a decision, not an oversight, and one the next person
to read those steps should not have to rediscover.

1. **An `Error`'s `cause` was not carried.** Seven rows, one per error type: `compare_Error` asserts
   `assert_equals(actual.cause, input.cause)` and Jint answered `undefined`. **The HTML Standard as published
   does not require this.**
   [Step 17 of StructuredSerializeInternal](https://html.spec.whatwg.org/multipage/structured-data.html#structuredserializeinternal)
   records the name, the message and the stack; the word *cause* does not appear in the section at all, and
   step 17.6's licence to attach "any interesting accompanying data" is explicitly scoped to data *"which are
   not yet specified"* — which the editor reads as excluding `cause`, since ECMA-262 specifies it
   ([whatwg/html#11321](https://github.com/whatwg/html/issues/11321): "Chromium and Firefox are actually not
   conformant to the current spec, which does not allow copying `cause`"). The change that would specify it,
   [whatwg/html#5749](https://github.com/whatwg/html/pull/5749), has been open since 2020 and is stalled on
   the larger plan to move structured cloning into ECMA-262. Two of the three engines carry `cause` today and
   the corpus asserts it, so Jint carries it: `SerializedError` gained `HasCause`/`Cause`, the error arm
   became the one deep arm that is not a container (registered in the memory map first, so an error that is
   its own cause terminates), and the clone gets it back through
   [CreateNonEnumerableDataPropertyOrThrow](https://tc39.es/ecma262/#sec-installerrorcause), the same
   writable/non-enumerable/configurable shape the language gives it.
   What was deliberately **not** taken from that pull request is its rewrite of `message`, which would `Get`
   the property rather than read an own data descriptor and install the result unconditionally: the same
   battery asserts the published semantics with
   `assert_equals(actual.hasOwnProperty("message"), input.hasOwnProperty("message"))`, so half-adopting the
   proposal would have turned a passing row red. `cause` is therefore read exactly as `message` is — an own
   *data* property, absent when the source has none.
2. **`Blob` and `File` were not serializable at all** — 22 rows, every one a `DataCloneError`. (The issue
   said 23; enumerating what the three globs and three named rows actually cover gives 6 + 7 + 6 + 1 + 1 + 1.)
   Both are `[Serializable]` in [the File API](https://w3c.github.io/FileAPI/#dfn-Blob), and Jint has both
   interfaces, so what was missing was only their serialization steps. Four things about the fix are worth
   knowing.
   The File API's `Blob` steps carry `[[SnapshotState]]` and `[[ByteSequence]]` and **nothing else** — `type`
   is not in them, which would make `structuredClone(new Blob(['x'], { type: 'text/plain' })).type` the empty
   string. `compare_Blob` asserts `assert_equals(actual.type, input.type)` and every engine carries it, so the
   media type is carried here too; that is a gap in the prose rather than a decision anyone made.
   `[[SnapshotState]]` has no representation, and that is not an omission: snapshot state is the state of the
   *underlying storage*, and a `JsBlob` is always built from bytes already in memory.
   The byte sequence is **shared with the clone, not copied**: a `Blob` is immutable by specification and
   `JsBlob` never hands its array out, so the source, the record and every clone can hold one array with
   nothing able to observe it — which is the opposite of an `ArrayBuffer`, whose storage script can write to
   and which therefore copies. It is also why a `Blob` record is exempt from the copy a `sharedRecord`
   deserialization otherwise makes.
   `SerializedFile` derives from `SerializedBlob` and is matched first in both switches, for the reason
   `SerializedQuotaExceededError` derives from `SerializedDomException`: it makes "and also the Blob fields"
   literal and keeps a `File` from flattening into a `Blob`. Because the deserializer builds a fresh instance
   from the realm's intrinsic, the two rows that look at the problem from a different angle fall out for free
   — a `Blob` whose interface object was deleted from the global still deserializes (the intrinsic is not the
   global property), and a `File` subclass comes back as a plain `File` (only the primary interface takes
   part). The deserializer also implements the step those two rows sit next to: *"if the interface identified
   by interfaceName is not exposed in targetRealm, throw a `DataCloneError`"*, which is reachable in Jint
   because a `MessagePort`, a `BroadcastChannel` or a `Worker` can carry a blob to an engine that never
   enabled `WebApiFeatures.Files`.
3. **`%Object.prototype%` was refused**, and the cause was not a check aimed at platform objects catching
   something by accident: the ordinary-object arm was a **type test on Jint's ordinary-object storage class**,
   `case JsObject`, where the specification's test is about internal slots and exoticness. `Object.prototype`
   is an `ObjectPrototype`, built by the source generator in builtin-shape mode, so it never matched and fell
   through to the blanket refusal. HTML's
   [step 23](https://html.spec.whatwg.org/multipage/structured-data.html#structuredserializeinternal) refuses
   "an exotic object [that] is not the `%Object.prototype%` intrinsic object associated with **any** realm" —
   `%Object.prototype%` *is* exotic (immutable prototype) and is carved out by name — and step 24's own note
   says the result "will be an empty object (not an immutable prototype exotic object)", which is exactly what
   the test checks by calling `Object.setPrototypeOf` on the clone. The arm is now
   `case JsObject or ObjectPrototype`, and matching the type is that predicate rather than an identity check
   standing in for it: `ObjectPrototype` is sealed and `Intrinsics` builds exactly one per realm, so
   `is ObjectPrototype` is true of every realm's `%Object.prototype%` and of nothing else.
   The refusal stays conservative in the direction it was designed to be — everything the recognized steps
   name is serialized and anything else is refused, which is what keeps a host's own `ObjectInstance`
   subclass, and every platform object Jint has not declared `[Serializable]`, refused rather than silently
   flattened into a plain object that has lost its state. Two shapes are still stricter than a browser for
   that reason and are recorded on `ThrowUncloneable`: a namespace object (`Math`, `JSON`, `console`) and an
   unmapped `arguments` object, both of which a browser clones as `{}`.

## What the fetch object model says about this engine

The figures below are the corpus as it stood before there was a server — 29 files, all of them about
`Headers`, `Request` and `Response` as objects. What the twenty files behind
[the server lane](#the-server-lane) added is the section after it.

388 assertions across 29 files, of which **75 do not pass** — and every one of those is a decision rather
than a defect. 73 are one documented decline: `HeadersGuard` refuses to enforce the *forbidden header name*
and *forbidden response header name* lists, because they are a browser's protection of headers the user agent
alone controls and Jint runs server-side, where those headers are exactly what a script legitimately needs to
set — the same choice Node and Deno make. That is the whole of `headers-forbidden-override.any.js`'s 72
forbidden rows (its 18 "is allowed to use" rows pass) and one row of `header-setcookie.any.js`. One more row
is `NeedsApiBaseUrl`: `Response.redirect("/")` with no document to resolve against.

**The seventy-fifth is a row no implementation passes**, and the three genuine defects that used to sit
beside it are fixed under [#3212](https://github.com/sebastienros/jint/issues/3212).

### The one row that is not debt

**An empty `FormData` body does not serialize to an empty body** — one row of `response-consume-empty.any.js`,
which asks that `await new Response(new FormData()).text()` have length 0 and gets the 50-byte closing
boundary `MultipartFormData` writes.

The triage note said to read
[the multipart/form-data encoding algorithm](https://html.spec.whatwg.org/multipage/form-control-infrastructure.html#multipart/form-data-encoding-algorithm)
before changing anything. Reading it settles the question the other way. HTML's algorithm normalizes the
newlines in each entry's name and value and then says to "return the byte sequence resulting from encoding
the entry list using the rules described by RFC 7578" — it defines the *escaping* and delegates the *framing*
entirely. RFC 7578 defers in turn to RFC 2046, whose section 5.1.1 grammar is

```
multipart-body := [preamble CRLF]
                  dash-boundary transport-padding CRLF
                  body-part *encapsulation
                  close-delimiter transport-padding
                  [CRLF epilogue]
```

The `close-delimiter` is not optional there, so an empty entry list has no shorter conforming encoding than
the one Jint writes. Nothing in either document licenses the empty body the row asks for.

The empirical half agrees, decisively. Measured on wpt.fyi against the four aligned stable runs at the time
of writing (chrome 152, edge 151, firefox 154, safari 26.6), that row is **0/1 in all four**, while the
file's thirteen other rows are 1/1 in all four. No browser produces an empty body for an empty `FormData`
either, which is what one would expect of a serializer that writes its close delimiter after the loop.

So this row is not debt. It keeps its exclusion, but `NeedsTriage` — "a bug or a specification detail to
chase" — was the wrong category for it, and it no longer carries it.
[#3261](https://github.com/sebastienros/jint/issues/3261) gave it one of its own,
`AssertsWhatNothingRequires`: the corpus's analogue of test262's `=== PERMANENT EXCLUSIONS ===` banner, for a
test that asserts what nothing requires of anybody. It is the only entry there, and moving it left
`NeedsTriage` **empty**, which is the state that makes a future non-zero count in it mean something. The
category's own documentation in `WptExclusions.cs` carries the rule for what may join it — a normative
citation and an argued decision, never a to-do — and the rule that age alone never promotes an entry into it.

### The three that are fixed

1. **The `Headers` iterator prototype's `next` was not enumerable** — three rows of `headers-basic.any.js`
   (`keys`, `values`, `entries`), each through the file's own `checkIteratorProperties`, which reads the
   descriptor and asserts all three attributes.
   [WebIDL's iterator prototype object](https://webidl.spec.whatwg.org/#es-iterator-prototype-object) gives
   `next` `{ [[Writable]]: true, [[Enumerable]]: true, [[Configurable]]: true }`; writable and configurable
   were already right and enumerable was the whole of it, because a built-in function property is
   non-enumerable everywhere in ECMA-262
   ([standard built-in objects](https://tc39.es/ecma262/#sec-ecmascript-standard-built-in-objects)) and that
   is what `[JsFunction]` defaults to. The fix is the `Flags` the streams corpus's async-iterator defect
   introduced, spelled on three declarations rather than one: **no vendored row reaches
   `FormDataIteratorPrototype` or `UrlSearchParamsIteratorPrototype`**, and both carried exactly the same
   defect with nothing to find it. The `@@toStringTag` on all three was already right — WebIDL gives a class
   string `{ [[Writable]]: false, [[Enumerable]]: false, [[Configurable]]: true }`.

2. **A consumed byte-source body handed back a stream that was not locked** — eight of the twelve rows of
   `response-stream-disturbed-5.any.js`. After `response.blob()` (or `text`/`json`/`arrayBuffer`) has been
   called, `response.body.getReader()` must throw a `TypeError`; here it succeeded.
   The triage note put this down to the stream `.body` exposes being "not the one the consume path locked",
   and the wrong half of that is the instructive one: the consume path locked nothing, and *disturbed* is not
   what `getReader()` refuses. It refuses a **locked** stream and only a locked one —
   [SetUpReadableStreamDefaultReader](https://streams.spec.whatwg.org/#set-up-readable-stream-default-reader)
   step 1 — so the disturbed-but-unlocked stream Jint handed back was given a reader quite correctly.
   What makes a browser refuse it is that consuming a body runs
   [fully read](https://fetch.spec.whatwg.org/#concept-body-fully-read), whose step 3 is "let reader be the
   result of [getting a reader](https://streams.spec.whatwg.org/#readablestream-get-a-reader) for body's
   stream" — which locks it — and whose read-all-bytes step never releases that reader. The stream a consumed
   body exposes is therefore disturbed, closed *and locked for good*. That is also exactly why the four rows
   whose body source is a `ReadableStream` **passed**: those go through `FetchBody.FullyRead`, which acquires
   the reader and holds it. `GetOrCreateStream` defers building the stream for a bytes-source body, so what
   it has to reproduce on first ask is the state fully read would have left — and it was reproducing only the
   `disturbed` half.

3. **A `record<ByteString, ByteString>` conversion performed one operation too many** — two rows of
   `headers-record.any.js`, "Correct operation ordering with two properties one of which has an invalid name"
   (6 where 5 are allowed) and "Basic operation with Symbol keys" (8 where 7 are). Both count the operations a
   proxy records while [WebIDL's record conversion](https://webidl.spec.whatwg.org/#es-record) walks the
   object, so what they pin is the order of `[[OwnPropertyKeys]]`, `[[GetOwnProperty]]` and `Get` rather than
   the header list behind it — and measured through the file's own logging proxy, the extra operation was the
   same one on both rows: a `[[Get]]` of the very key that ends the conversion. Step 4.2 is 4.2.1 "let
   *typedKey* be *key* converted to an IDL value of type *K*" and *then* 4.2.2 "let *value* be
   `? Get(O, key)`"; `FillFromRecord` did the `Get` first and converted the key inside `append`.
   Writing step 3 as the unfiltered `? O.[[OwnPropertyKeys]]()` the specification asks for also removed an
   inconsistency that predated the rows: `JsProxy.GetOwnPropertyKeys` honours Jint's `types` filter only when
   there is no `ownKeys` trap — correctly, since the spec's internal method has no filter — so a Symbol key
   already reached the walk through a proxy while `new Headers({ [Symbol.toStringTag]: 'x' })` on a plain
   object was silently accepted where a browser throws.

## What the fetch *network* corpus says about this engine

326 assertions across the twenty files [the server lane](#the-server-lane) added, of which **69 do not pass**,
and every one of those 69 is a decision or the environment rather than a defect. It did not start that way.
Unlike the object-model half above, most of what the lane first found were not decisions — the corpus was
making a real request for the first time, and five things it asserts turned out not to hold. Each was filed as
its own issue and deliberately **not** fixed by the change that first ran the suite: the change that first runs
a suite must not also be the change that moves the engine, or nobody can tell which of the two a number came
from. All five are fixed, and `WptDivergence.NeedsTriage` is empty again — which is the state that makes a
future non-zero count in it mean something.

1. **No `Accept: */*` was appended** ([#3279](https://github.com/sebastienros/jint/issues/3279)). Step 12 of
   [fetch](https://fetch.spec.whatwg.org/#concept-fetch) says to append it when the header list has no
   `Accept`, and nothing did. One row of `basic/accept-header.any.js`; the row that sets `Accept` explicitly
   always passed, which is what said the header list itself was fine. `FetchTransport` appends it to the
   transport's own copy of the list, so the header goes out and the `Request` the script holds still answers
   null for it, exactly as in a browser. The `Accept-Language` step beside it stays unimplemented on purpose:
   it applies only "if request's client is non-null" and reports a user's preferences, and there is no user
   here — the two steps sitting in one algorithm is what makes that line a decision rather than an oversight.
2. **A `HEAD` response carried a body stream** ([#3280](https://github.com/sebastienros/jint/issues/3280)).
   Step 22 of [main fetch](https://fetch.spec.whatwg.org/#concept-main-fetch) sets the body to null for a
   `HEAD` or `CONNECT` request as well as for a null body status — "this standardizes the error handling for
   servers that violate HTTP" — and only the status half was consulted. One row of
   `basic/response-null-body.any.js`; the nine null-body-status rows (204, 205, 304) always passed, so it was
   the method half alone. The response's own headers are untouched by it: a `HEAD` still answers the
   `Content-Length` of the representation it describes, and that length no longer counts against
   `MaxResponseBytes`, since nothing is going to be transferred.
3. **`Content-Encoding`, `Content-Language` and `Content-Location` never left a bodiless request**
   ([#3282](https://github.com/sebastienros/jint/issues/3282)). The BCL
   files those three as *content* headers, so a GET or HEAD — which had no `HttpContent` to hang them on —
   dropped them silently, where Fetch has no such category and they are ordinary request headers. Eight rows of
   `redirect/redirect-method.any.js`; its POST rows always passed, which is what localised it. Such a request
   is now given an empty `HttpContent` to carry them, and the framing that costs is the interesting half: the
   BCL frames any message that has content, so `FetchTransport.CreateHeaderCarrier` keeps the length for a
   `POST` or `PUT` — where step 8 of
   [HTTP-network-or-cache fetch](https://fetch.spec.whatwg.org/#concept-http-network-or-cache-fetch) appends
   `Content-Length: 0` itself — and suppresses it for every other method, where the standard appends nothing
   and the same file asserts its absence. What the BCL writes instead there is `Transfer-Encoding: chunked`,
   an HTTP/1.1 transfer artefact that is in no header list and does not exist in HTTP/2, which is the cheaper
   of the two divergences the stack leaves available.
4. **`clone()` handed both bodies the same buffer** ([#3283](https://github.com/sebastienros/jint/issues/3283)).
   [Body clone](https://fetch.spec.whatwg.org/#concept-body-clone) tees the stream, and the chunks the two
   branches deliver must be structured clones of one another rather than the same object. Fourteen rows of
   `response/response-clone.any.js`, one per typed-array kind.
5. **A fetched response's `Headers` were mutable** ([#3281](https://github.com/sebastienros/jint/issues/3281)),
   where [the fetch method](https://fetch.spec.whatwg.org/#dom-global-fetch) creates the `Response` object with
   the *immutable* guard. `response/response-headers-guard.any.js` passes whole.

The 69 that remain are decisions or environment, in five groups that add up exactly: 35 + 21 + 10 + 2 + 1.

* **35 `NeedsXmlHttpRequest`.** Both `header-values*` files run their whole table twice, once through
  `XMLHttpRequest.setRequestHeader` and once through `fetch`, and the driver's XHR is a corpus reader that
  refuses anything but a GET by name. Only the `fetch` half is about anything Jint has.
* **21 `NeedsOpaqueRedirect`.** `redirect: "manual"` hands the script the redirect response rather than a
  browser's opaque filtered one, which `FetchTransport` documents as Node's reading of
  [HTTP fetch](https://fetch.spec.whatwg.org/#concept-http-fetch) step 6 — the filtered response exists to
  hide a cross-origin redirect from a *page*, which an engine with no origin does not have. The `follow` and
  `error` rows of both redirect files pass.
* **10 `NeedsApiBaseUrl`.** `text-utf8.any.js` builds a `Request` from the empty string to get at
  `Request.text()`; its other ten rows go through `fetch`, read a real response, and pass.
* **2 `NeedsPermissiveHeaderTransport`.** A header value carrying a byte above ASCII does not survive the
  .NET HTTP stack, in either direction —
  `WptServerTests.AHeaderValueAboveAsciiDoesNotSurviveTheHttpStack` measures exactly where the line falls,
  with no engine in the picture, which is what puts these rows here rather than in `NeedsTriage`.
* **1 `NeedsBrowserRequestHeaders`.** `Accept-Language` is a browser reporting a user's language preferences,
  and there is no user here to have any.

## The whole corpus, standard by standard

**This table is generated, not maintained.** `Jint.Tests/Wpt/WptCensusTests.cs` derives every figure in it
from the corpus and fails when the README disagrees — see [Taking the census](#taking-the-census) below for
the two halves of that check and the one command that rewrites the table.

Measured at this pin, on Windows, with the driver's exclusion table in force. "Not passing" is every result
the shim did not record `PASS`, which is exactly the set the table names. The last change to move a row is the
one that fixed the server lane's three remaining defects at once —
[#3279](https://github.com/sebastienros/jint/issues/3279),
[#3280](https://github.com/sebastienros/jint/issues/3280) and
[#3282](https://github.com/sebastienros/jint/issues/3282) — and moved Fetch's not-passing count alone, 154 to
144: one `Accept` row, one `HEAD` row and the eight GET and HEAD rows of `redirect-method.any.js`. Before it
[#3283](https://github.com/sebastienros/jint/issues/3283) took the same row 168 to 154 by structured-cloning
the chunks `clone()` tees, and [#3281](https://github.com/sebastienros/jint/issues/3281) took it 169 to 168 by
giving a fetched response's `Headers` the *immutable* guard. Before all three,
[#3260](https://github.com/sebastienros/jint/issues/3260) moved exactly one row: Fetch, from 29 files /
388 assertions / 75 not passing to 49 / 714 / 169 — [the server lane](#the-server-lane)'s twenty files, 326
assertions, 232 of them passing at the time. Every other row was re-derived in the same run and had not moved. The census
before that was taken by hand for [#3212](https://github.com/sebastienros/jint/issues/3212), which moved five
rows at once because Streams, Compression, User Timing and DOM had each gone stale against a fix that removed
their exclusions without revisiting this table.

| Standard | Suites | Files | Assertions | Not passing |
| --- | --- | --- | --- | --- |
| URL | `url/` | 21 | 2,068 | 0 |
| URL Pattern | `urlpattern/` | 3 | 373 | 0 |
| Encoding | `encoding/` | 20 | 11,544 | 322 |
| Web Cryptography | `WebCryptoAPI/` ×8 | 48 | 24,136 | 2,449 |
| Streams | `streams/` ×7 | 66 | 1,170 | 4 |
| Compression | `compression/` | 15 | 297 | 22 |
| File API | `FileAPI/` ×3 | 14 | 342 | 0 |
| High Resolution Time | `hr-time/` | 2 | 7 | 1 |
| User Timing | `user-timing/` | 19 | 78 | 5 |
| HTML — workers | `workers/` ×4 | 12 | 24 | 8 |
| HTML — timers, microtasks, structured clone | `html/webappapis/` ×3 | 11 | 154 | 3 |
| DOM | `dom/` ×2 | 13 | 76 | 0 |
| Fetch | `fetch/api/` ×5 | 49 | 714 | 144 |
| **total** | **38** | **293** | **40,983** | **2,958** |

Re-censused whole rather than adjusted row by row, because several rows had gone stale between the changes
that moved them: before [#3195](https://github.com/sebastienros/jint/issues/3195) the true figures were
270 files / 40,631 assertions / 2,907 not passing, where this table read 269 / 40,617 / 2,980 — and Streams
(16 against a real 11), Compression (84 against 22), User Timing (9 against 5) and DOM (2 against 0) were each
carrying a number a later fix had already improved. That class of drift is what the census closes: those four
rows were arithmetic the driver already did, and nothing checked the prose against it.

Three of those rows are worth a caveat. The Fetch row is the only one whose files reach a socket at all — the
seventeen [server-lane](#the-server-lane) files, over the loopback interface to a server in this same
process — so it is the only row whose figures could in principle depend on the machine. They do not: the
suite was run five times over and reported the same 458 xUnit cases every time, and the driver's own idle rule
is reset by a test settling rather than by a wall clock. The Encoding figure is dominated by
`textdecoder-fatal-single-byte.any.js`, 7,168 assertions of it and every one passing; of the 322 that do not,
168 are the `XMLHttpRequest` half of `single-byte-decoder.any.js` and the rest are the legacy multi-byte
labels. And the Web Cryptography figure is the one that moves per platform — macOS has 216 more, for the
reason the WebCryptoAPI section states — which is what the per-OS exclusion scoping exists for: the driver
holds every entry to matching a real failure on the leg it is running on, so no figure here decides which
rows are excluded.

The corpora arrived a group at a time. `url/` and `encoding/` came with the harness itself
([#3104](https://github.com/sebastienros/jint/issues/3104)) and `WebCryptoAPI/` shortly after; the four groups
of [#3185](https://github.com/sebastienros/jint/issues/3185) added `streams/`, then
`compression/` + `urlpattern/` + `FileAPI/`, then the timing and DOM half, and last the network-free half of
`fetch/api/` together with the two single-byte encoding files that had been parked for the decoders;
`workers/` came with the worker feature ([#3167](https://github.com/sebastienros/jint/issues/3167)) and is
the one corpus that mostly does not run in the driver's own engine —
`workers/modules/dedicated-worker-import.any.js`, which arrived with
[#3195](https://github.com/sebastienros/jint/issues/3195), is the exception, because its subject is a page
creating workers rather than the worker global itself. The rest of `fetch/api/` — `basic/`, `body/` and
`redirect/` — came last, with [the server](#the-server-lane)
([#3260](https://github.com/sebastienros/jint/issues/3260)). This file records what each of them says about
the engine.

What remains deliberately unvendored, in one place: everything in the "Deliberately not vendored" table
above, plus every upstream file that is not a `.any.js` — `.window.js`, `.html`, `.xhtml`, `.worker.js` and
`.sub.html` are all for a browsing context or a classic worker. Of the WHATWG standards Jint implements, the
directories with no vendored file at all are `fetch/api/request/` (no API base URL), `fetch/api/abort/`,
`cors/`, `credentials/` and `policies/` (each for the reason its row gives, and none of them for want of a
server), the parts of the `workers/` tree listed above, the `WebCryptoAPI` directories listed above, and
`xhr/` (there is no `XMLHttpRequest` in the engine — the shim's is a vendored-corpus reader for the suites
that need one, never an implementation).

## Taking the census

The table above is derived from the corpus by `Jint.Tests/Wpt/WptCensusTests.cs`, in two halves split by what
each column costs to know.

`TheInventoryTableNamesEveryStandardAndCountsItsFilesAndSuites` holds the **Standard**, **Suites** and
**Files** columns. Those are read off the embedded corpus — a row's files are its directory's `.any.js` files,
and its suites are the directories they sit in — so it needs no execution, runs on every platform and in every
filtered run, and costs a few milliseconds. It is also what catches a corpus arriving with no row of its own:
the census prefixes have to partition every vendored `.any.js`, so vendoring a new standard fails this test
until the standard is named.

`TheInventoryTableMatchesWhatTheCorpusMeasures` holds all five columns, which means running the suites. It is
**opt-in**, because it is the only part that costs anything: it reuses every outcome the driver already
produced — `WptHarness.Run` hands each file's results to `WptCensus` on the way back out — but a full
`Jint.Tests` pass still measured 65 s → 85 s with it always on. The PR workflow's Windows leg sets the
variable, so that is where the table is enforced; an ordinary run pays nothing. Windows only, in both modes,
because assertion counts move per operating system — the Web Cryptography row most of all — and this table is
measured on Windows.

```bash
# check the table (about a minute; it censuses the whole corpus)
JINT_WPT_CENSUS=1 dotnet test -c Release Jint.Tests/Jint.Tests.csproj -f net10.0 \
  --filter "FullyQualifiedName~Jint.Tests.Wpt.WptCensusTests"

# rewrite the table from what the corpus measures, then commit the diff
JINT_WPT_CENSUS=update dotnet test -c Release Jint.Tests/Jint.Tests.csproj -f net10.0 \
  --filter "FullyQualifiedName~Jint.Tests.Wpt.WptCensusTests"
```

A failure prints the stated table and the measured one side by side, so the rows that moved are the rows that
differ.

## Updating the pin

Resolve `master` to a concrete commit, re-fetch every file in this directory at that commit, update the SHA
above, and run the suites. Expect the exclusion table to need work in the same change: the driver fails on an
entry that no longer applies, which is the point. Re-run the census afterwards — a bump moves nearly every row
of the table above.

### Verifying that nothing has drifted

Vendored files are copied verbatim, and the failure mode is silent: a file that has quietly diverged from the
pin still runs, still passes, and stops testing what upstream asserts. Checking is mechanical, and needs no
downloads — a GitHub blob id *is* `git hash-object` of the content, and `.gitattributes` pins this whole
directory to `text eol=lf` precisely so a Windows checkout cannot make the two disagree.

```bash
cd Jint.Tests/Wpt/Vendor
SHA=$(grep -oE '\b[0-9a-f]{40}\b' README.md | head -1)

# one call per directory that holds a vendored file (48 at this pin)
for d in $(find . -type f \( -name '*.js' -o -name '*.json' \) -printf '%h\n' | sort -u | sed 's|^\./||'); do
  gh api "repos/web-platform-tests/wpt/contents/$d?ref=$SHA" \
     --jq '.[] | select(.type=="file") | "\(.sha) \(.path)"'
done > /tmp/upstream.txt

find . -type f \( -name '*.js' -o -name '*.json' \) | sort | while read -r f; do
  rel="${f#./}"
  up=$(grep -m1 " ${rel}$" /tmp/upstream.txt | cut -d' ' -f1)
  [ -z "$up" ] && { echo "NOT-IN-UPSTREAM: $rel"; continue; }
  [ "$(git hash-object "$f")" = "$up" ] || echo "DRIFT: $rel"
done
```

Silence is a clean corpus. Verified at this pin: 359 files, no drift, no file absent upstream.
