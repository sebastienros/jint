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
`UseWebApis(WebApiFeatures.Default)` plus the fetch object model — `Headers`, `Request` and `Response`, and
pointedly not `fetch`, so no suite gets outbound network access. `Jint.Tests/Wpt/Prelude/testharness-shim.js`
— *not* vendored — stands in for upstream's `testharness.js`; its header says what it implements and where it
deliberately differs. `WptHarness.cs` documents the three decisions a reader is most likely to want: the
engine supplies its own `setTimeout` — the shim's `step_timeout` is a forwarder onto it, so the streams
suites' 45 timer sites are decided by the shipped `TimerQueue` — `// META: variant=` sharding is ignored
because one unsharded run is the union of every variant, and why the object model is there at all
(`url/urlencoded-parser.any.js` runs each of its 35 inputs through `URLSearchParams`, `Request.formData()`
and `Response.formData()`, one algorithm reached three ways).

Eight standards are vendored: `url/`, `encoding/`, `compression/` and `urlpattern/` as one suite each,
`FileAPI/` as **three** (its root, `blob/` and `file/`), `workers/` as **three**, `WebCryptoAPI/` as **eight**
and `streams/` as **seven** — their root files plus one suite per sub-directory, because
`WptCorpus.TestFiles` lists a directory's own files and never descends. That is 195 theory cases: 48 of them
the WebCryptoAPI corpus's 24,136 assertions, 65 the streams corpus's 1,154, 15 the compression corpus's 297,
3 the URL Pattern corpus's 373, 14 the File API's 342 and 11 the workers corpus's 15; the whole driver runs
in about a minute.

## Two lanes: the top-level engine, and a real worker

Every suite but one runs its file in the driver's own engine. The `workers/` corpus does not, and cannot:
every `.any.js` file in it that is reachable at all carries `// META: global=worker` or
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
  is that module code is strict. No vendored file depends on sloppy mode; the nearest thing,
  `interfaces/WorkerGlobalScope/self.any.js`'s `self = 1`, assigns to a property that is writable either way —
  which is itself the one triage entry below.
* **The worker gets the same environment the top-level engine gets**: `WebApiFeatures.Default` minus the
  grants a worker never inherits, plus the fetch object model, plus the shim's resource reader. A file's
  outcome therefore does not depend on which lane ran it.

The shim changed in one place for this. It used to install `self` unconditionally; it now does so only when
the engine has not, because `WorkerGlobalScope.self` is read-only in HTML and an unconditional assignment
would both overwrite what is under test and — the day the engine makes it read-only — throw out of a
strict-mode function and take every suite with it.

One thing deliberately did **not** change: `GLOBAL.isWorker()` still answers `false` in the worker lane. The
shim cannot tell the lanes apart and nothing in the corpus asks — `isWindow()` is the only one of the three
ever called, and `false` is the right answer for it in both lanes. A file that guards a worker-specific branch
on `isWorker()` would skip it silently, so vendoring one means giving the shim a way to know first. That is
recorded rather than fixed because an unused mechanism is one nothing proves right.

Tests that do not pass are named in the driver's exclusion table with the category they belong to. An entry
there must match at least one failing test and no passing one, so a fix, a rename or a corpus bump cannot
leave a permanent exemption behind — the run fails until the table is brought back in line.

## Deliberately not vendored

The driver enforces this list (`WptTestRunner._notVendored`): a re-vendor that brings one of these back
without revisiting the reason fails rather than quietly adding a red suite.

| Upstream path | Why not |
| --- | --- |
| `url/idlharness.any.js`, `encoding/idlharness.any.js` | Need `/resources/idlharness.js` and `/resources/WebIDLParser.js` — a WebIDL conformance framework an order of magnitude larger than the shim, testing a layer Jint's source-generated built-ins do not have. |
| `url/IdnaTestV2.any.js`, `url/IdnaTestV2-removed.any.js` | A 314 kB UTS-46 conformance corpus. `Jint.WebApi.Url.Parsing.Idna` builds on `IdnMapping` and documents where that diverges (VerifyDnsLength, CheckHyphens, ICU version skew); running it is an IDNA triage of its own rather than part of standing the harness up. |
| `encoding/legacy-mb-*`, `encoding/iso-2022-jp-decoder.any.js`, `encoding/single-byte-decoder.any.js`, `encoding/textdecoder-fatal-single-byte.any.js`, `encoding/replacement-encodings.any.js` | The Encoding Standard's legacy single-byte and multi-byte decoders, which [issue #3106](https://github.com/sebastienros/jint/issues/3106) implements. `single-byte-decoder` and `replacement-encodings` additionally need `XMLHttpRequest`. |
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
| `streams/readable-byte-streams/construct-byob-request.any.js` | Reads `ReadableByteStreamController.prototype` and calls `new ReadableStreamBYOBRequest(…)` at *file scope*. Neither is a global here — see "Streams, including byte streams" in the repository README for the reduction — so the file throws before registering a test, and a harness error is for the whole file rather than something a per-test exclusion can name. The seven rows of `default-reader.any.js` that fail for the same reason fail *inside* test bodies, so those are in the exclusion table under `NeedsStreamInterfaceGlobals`. |
| `compression/compression-output-length.any.js`, `compression/compression-stream.any.js` | Both fetch a binary fixture out of wpt's `/media/` directory — a 384 kB WebM, and for the second a WebVTT file as well — and read it back with `response.arrayBuffer()` / `response.bytes()`. The shim's `fetch` is a *text* reader over the vendored tree, so neither the transport nor the accessor exists here, and vendoring a third of a megabyte of video in order to compress it would be a strange thing for this corpus to carry. `compression-stream.any.js` additionally calls `fetch` at file scope, so its failure could not even be a per-test one. |
| `compression/decompression-extra-input.any.js` | Writes a member plus one trailing byte and never closes the writer, so its second `reader.read()` settles only if the trailing byte errors the stream. It does not here — that is the second of `DecompressionCodec`'s two documented divergences — so the read waits for input that cannot arrive and the *file* stalls rather than any test failing, which is a harness error no per-test exclusion can name. The divergence is still asserted, by the four excluded rows of `decompression-corrupt-input.any.js`. |
| `urlpattern/urlpattern.https.any.js` | Byte-identical to `urlpattern.any.js` (both are two `// META:` lines). Upstream ships both so a browser runs the corpus over http and over https; Jint has no scheme to be served over, so the second copy would run the same 370 cases again and assert nothing the first did not. |
| `urlpattern/*.tentative.any.js`, `urlpattern/*.tentative.https.any.js` | Upstream's `.tentative` marker: `compare()` and `generate()` are proposals the URL Pattern Standard has not adopted. Covered by the two existing `*.tentative.*` rows. |
| `FileAPI/file/send-file-formdata*.any.js` | All four POST a `FormData` to wptserve's `/fetch/api/resources/echo-content.py` and assert on the multipart body that comes back, so they need the fetch object model, an outbound request and the server's own Python handler. Serializing a `FormData` as `multipart/form-data` is a fetch body's job and arrives with that feature, which `WebApiFeatures.Default` never includes. |
| `FileAPI/fileReader.any.js` | Jint has no `FileReader` — a `Blob` is read here through `text()`, `arrayBuffer()`, `bytes()` and `stream()` — and this file is about that reader's state machine (`readyState`, `abort()`, the progress events) rather than about a `Blob`. It is the only `.any.js` in the File API's root that is not vendored; its sibling `unicode.any.js` needs no reader and is. |
| `workers/*.worker.js` | Twenty-one files that look like the most runnable thing in the directory and are the least: **every one opens with `importScripts("/resources/testharness.js")` at file scope.** A `.worker.js` is a *classic* worker's top-level script, and Jint runs module workers only — `importScripts` is present and throws a `TypeError`, which is the module-worker step [the standard itself prescribes](https://html.spec.whatwg.org/multipage/workers.html#dom-workerglobalscope-importscripts). So the file throws before registering a test, and a harness error is for the whole file rather than anything a per-test exclusion can name. What they assert about the worker global is asserted instead by the `.any.js` files beside them and by `Jint.Tests/Runtime/WebApi/WorkerMechanismTests.cs`. |
| `workers/modules/*` | **The one row here that is a finding rather than a decision.** `dedicated-worker-import.any.js` is the flagship of the directory — nine `promise_test`s over static, nested and dynamic `import` in a module worker, exactly the feature Jint has. It cannot run, and the reason is in the *fixture*: every one of the nine worker scripts `import-test-cases.js` drives opens with `if ('DedicatedWorkerGlobalScope' in self && self instanceof DedicatedWorkerGlobalScope)` and installs its `onmessage` handler **inside** that branch. Jint ships no `DedicatedWorkerGlobalScope` interface object — the design's divergence #5, ruled together with [#3195](https://github.com/sebastienros/jint/issues/3195), because an interface object without the prototype chain would make `instanceof` lie — so no branch is taken, no handler is installed, the worker never answers, and all nine tests hang. A stall is a harness error for the whole file and no per-test exclusion can express a sniff that never reached an assertion. The `blob-url` and `data-url` siblings need `URL.createObjectURL` and a `data:` module loader on top of that. The module loading itself is covered by `WorkerMechanismTests`. |
| `workers/SharedWorker-*.any.js`, `workers/semantics/interface-objects/*` | `SharedWorker` and `SharedWorkerGlobalScope`, which Jint does not have — the design records it as still open, needing a cross-engine name registry of the shape `BroadcastChannelBroker` has. A `global=sharedworker` file cannot even be run in the worker lane: there is no shared worker to be the global of. |
| `workers/examples/*` | Upstream's own tutorial for writing worker tests, and it teaches wpt rather than testing an engine: `general.any.js` is two tests, the second asserting `location.pathname === "/workers/examples/general.any.worker.js"` — the path of the glue script the wpt server generates for a `.any.js` file. There is no server here to generate one. `onconnect.any.js` beside it is `global=sharedworker`. |
| `workers/Worker-location.sub.any.js`, `workers/interfaces/WorkerUtils/importScripts/*`, `workers/importscripts_mime*.any.js` | `.sub.` is wptserve's server-side substitution: it rewrites `{{host}}` and `{{ports[…]}}` into a real origin before serving the file, so a vendored copy carries the placeholders verbatim. The `importScripts` families are classic-worker script loading on top of that, over server-chosen MIME types and cross-origin redirects. `Worker-location.sub.any.js` additionally asserts every member of a `WorkerLocation`, which is declined below. |
| `workers/interfaces/WorkerGlobalScope/location/*` | The whole assertion of `returns-same-object.any.js` is `location === location`. The harness shim installs a stub `location` of its own — `/common/subset-tests.js` reads `location.search` to pick a shard — so a vendored copy would pass **against the shim** while Jint deliberately has no `WorkerLocation` at all. A test that can only assert the harness is worse than no test, which is why this is a row here and not an exclusion. |

Nothing was left out for being slow. Every vendored file was timed at the pin; the slowest is
`derive_bits_keys/pbkdf2.https.any.js` at ~20 s for 8,632 cases (it is `// META: timeout=long` and sharded
nine ways upstream), then `generateKey/successes_RSA-OAEP.https.any.js` at ~7 s, which really does generate
156 RSA key pairs. Everything else is under 3 s. The whole streams corpus is 6.4 s for its 65 files, the
slowest being `readable-byte-streams/templated.any.js` at ~2.1 s and `readable-streams/templated.any.js` at
~1.0 s — both are `rs-test-templates.js` run over every stream shape — so nothing there is near the bar
either. `transferable/transform-stream-members.any.js`, the file this pin's newest change added, is four
assertions and does not register.

The three corpora added last measure 2.6 s (compression, 15 files), 3.1 s (urlpattern, 3 files) and 0.2 s
(FileAPI, 14 files), run one after another on one thread. Two files carry almost all of that: `urlpattern.any.js`
at ~2.9 s, which is 369 patterns each compiled and then matched, and `compression/compression-large-flush-output.any.js`
at ~1.5 s, which compresses half a megabyte and inflates it again with pako. Both are `// META: timeout=long`
upstream. Everything else in the three is under 120 ms. The workers corpus added after them measures 0.46 s
for its 11 files — a whole engine is constructed, entangled and pumped per file, and that is still what it
costs.

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

2,450 of its 24,136 assertions do not pass, and every one is named in the driver's table under one of six
categories, whose own documentation in `WptExclusions.cs` carries the citation. Three are the platform:
`NeedsPlatformCryptoParameters` (AES-GCM's 96-bit-only iv and 96-to-128-bit tag, RSA-OAEP's empty-only label,
RSA-PSS's hash-length-only salt — all four are limits of the BCL primitives, documented on the classes that
hit them), `NeedsCompressedEcPointImport` and `NeedsCurve25519`. One is the corpus running ahead of the
specification: `NeedsKeyEncapsulation` (ML-KEM's `encapsulateKey`/`decapsulateKey` `KeyUsage` values, which
the current `KeyUsage` enumeration does not declare). One is the corpus meeting an environment it was not
written for: `NeedsSecureContextModel`.

The seventh category was `NeedsQuotaExceededErrorInterface`, the nine `Large length: *` rows of
`getRandomValues.any.js`. They pass since
[#3189](https://github.com/sebastienros/jint/issues/3189) implemented WebIDL's
[`QuotaExceededError`](https://webidl.spec.whatwg.org/#quotaexceedederror) interface, and the entry and the
category are gone with them.

That figure is Windows and Linux, whose AES-GCM takes a 96- to 128-bit tag. **macOS has 216 more**, all in
`aes_gcm.https.any.js`: Apple's implementation takes a 128-bit tag and nothing else, so the four tag lengths
between 96 and 120 bits are refused there and their rows are scoped to that platform in the table.

The seventh is `NeedsTriage`, and it is **debt**. It held two genuine defects the corpus found, recorded
rather than fixed so that the change which first ran these suites was not also the change that moved the
engine. One is fixed: `SubtleCrypto` copied its caller's bytes before normalizing the algorithm where the
specification copies them after, which was every "… during call" row, and issue #3179 moved each copy to its
numbered step. The one still open is ECDH's mismatched-curve rows, which get the `OperationError` of the
prose where a browser answers `InvalidAccessError`.

## What the streams corpus says about this engine

1,154 assertions across 65 files, of which **16 do not pass** — 98.6%, which is what one expects of an
implementation written operation by operation against the standard, and also why the sixteen are worth naming
individually. (Only the URL Pattern corpus below beats it, at 100%.)

`transferable/transform-stream-members.any.js` is the newest of the 65 and all four of its assertions pass.
It is the whole `.any.js` surface of transferable streams, and it asks one thing four ways: naming a
`TransformStream` *and* one of its two sides in a single transfer list must be a `DataCloneError`, in either
order. It passes because the transform stream's own transfer steps transfer each side in turn, so by the time
the list's other entry is reached that side is both locked and `[[Detached]]` — which is what makes the
refusal fall out of the steps rather than needing a rule of its own. The file took 0.4 s at the pin,
including the runner's start-up.

Eleven are a decision already taken. Seven rows of `readable-streams/default-reader.any.js` reach for the
`ReadableStreamDefaultReader` **global** (`NeedsStreamInterfaceGlobals`): only the five interfaces a script
constructs by name are installed here, and the other 22 rows of that file obtain the same interface as
`stream.getReader().constructor` and pass. Four are `readable-byte-streams/non-transferable-buffers.any.js`
(`NeedsWebAssembly`), which needs a `WebAssembly.Memory` buffer because that is the only `ArrayBuffer` a
script can obtain that cannot be transferred.

**The other five are `NeedsTriage` — genuine defects, recorded rather than fixed**, because the change that
first runs a suite must not also be the change that moves the engine. Each reproduces outside the harness.

1. **The async iterator's methods are not enumerable.**
   `readable-streams/async-iterator.any.js`, "Async iterator instances should have the correct list of
   properties". `Object.getOwnPropertyDescriptor(Object.getPrototypeOf(rs.values()), 'next')` reports
   `enumerable: false`, and so does `return`;
   [WebIDL's asynchronous iterator prototype object](https://webidl.spec.whatwg.org/#js-asynchronous-iterator-prototype-object)
   gives both `{ [[Writable]]: true, [[Enumerable]]: true, [[Configurable]]: true }`. `writable` and
   `configurable` are already right, so this is one attribute on two `[JsFunction]` declarations in
   `ReadableStreamAsyncIteratorPrototype`. The rest of that file's 40 rows pass.

2. **`readable.cancel()` on a `TransformStream` rejects where it must fulfil** — three rows, one defect seen
   from three angles: `transform-streams/errors.any.js`'s "abort should set the close reason for the writable
   when it happens before cancel during start, and cancel should reject" (through `writer.abort()`) and
   "controller.error() should close writable immediately after readable.cancel()" (through
   `controller.error()`), and `transform-streams/general.any.js`'s "terminate() should abort writable
   immediately after readable.cancel()" (through `controller.terminate()`). In each, the promise
   `ts.readable.cancel(…)` returned rejects with the writable side's stored error where every browser fulfils
   it. `TransformStreamOperations.SourceCancelAlgorithm` is a faithful transcription of
   [TransformStreamDefaultSourceCancelAlgorithm](https://streams.spec.whatwg.org/#transform-stream-default-source-cancel),
   including its "if *writable*.[[state]] is `errored`, reject" branch — so what diverges is *when* that
   branch looks: the writable has already finished erroring by the time the reaction on `cancelPromise` runs,
   where the specification's microtask chain leaves it still `erroring` (its controller's `[[started]]` has
   not flipped yet), which is the state that takes the "otherwise" path and resolves. The triage is therefore
   about ordering in `WritableStreamOperations.StartErroring`/`FinishErroring` relative to the transform
   stream's start promise, not about the cancel algorithm itself.

3. **`pipeTo()` reaches the sink's `write` synchronously with an `enqueue()` on the source.**
   `piping/general-addition.any.js`, "enqueue() must not synchronously call write algorithm".
   `ReadableStreamPipe` reads through a raw `ReadRequest` rather than through a promise, and a `ReadRequest`'s
   *chunk steps* are run synchronously by `ReadableStreamFulfillReadRequest` — so the write is started on the
   `enqueue()` call's own stack. [ReadableStreamPipeTo](https://streams.spec.whatwg.org/#readable-stream-pipe-to)
   deliberately leaves the mechanism flexible ("the exact manner in which this happens is not observable to
   author code"), which is precisely the property that fails here: it *is* observable. The other 228 rows of
   the piping suite pass, so this is the shape of the read rather than the pipe's semantics.

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

342 assertions across 14 files (11 in `blob/`, 2 in `file/`, 1 in the root), of which **3 do not pass**.

The eight that used to be red were the whole of `Blob-textStream.any.js`, under a `NeedsBlobTextStream`
category, because [the File API added](https://w3c.github.io/FileAPI/#dom-blob-textstream) `textStream()`
after this `Blob` was written. They pass since
[#3211](https://github.com/sebastienros/jint/issues/3211) implemented it — the blob's stream piped through a
UTF-8 `TextDecoderStream`, which is four steps over pieces the engine already had — and the two entries and
the category are gone with them.

**The 3 that remain are `NeedsTriage`, and the defect is not in `Blob` at all.** All three rows of
`Blob-constructor.any.js` fail with `TypeError: cannot construct iterator`, thrown by
`Array.prototype.values`:

```js
[...Array.prototype.values.call({})]                    // spec: []           Jint: TypeError
[...Array.prototype.values.call({length: '3', 0: 'a', 1: 'b', 2: 'c'})]
                                                        // spec: [a, b, c]    Jint: TypeError
[...Array.prototype.values.call({length: -1})]          // spec: []           Jint: TypeError
[...Array.prototype.values.call({length: null})]        // spec: []           Jint: TypeError
[...Array.prototype.values.call({length: true, 0: 'a'})]// spec: ['a']        Jint: TypeError
Array.prototype.values.call({get length() { throw e; }})// spec: throws at the first next()
                                                        // Jint: throws at values()
```

[`Array.prototype.values`](https://tc39.es/ecma262/#sec-array.prototype.values) is *ToObject(this)* followed
by *CreateArrayIterator(O, value)*, and neither step reads `length`; the iterator's own closure does
*LengthOfArrayLike* — a `Get` and a `ToLength` — on each `next()`. `ArrayPrototype.Values` instead gates on
`ObjectInstance.IsArrayLike`, which demands a `length` that is *present*, already a `JsNumber`, and
non-negative, and throws when it is not. That is wrong in six ways at once: absent means 0, a string or a
boolean is coerced, a negative clamps to 0, and the read belongs to `next()` rather than to `values()`.
`keys` and `entries` have the same three-line body and the same defect.

The corpus reaches it because `new Blob(x)` converts `x` to a WebIDL `sequence`, and the file deliberately
hands it plain objects whose `@@iterator` *is* `Array.prototype[Symbol.iterator]` — which is how a browser's
`Blob` sees `{length: 1, 0: 'PASS'}` as a one-element sequence. It reproduces with no web API enabled at all,
which is what makes it an engine finding rather than a `Blob` one. The rest of `Blob-constructor.any.js`'s 73
rows pass, including the sibling that supplies a numeric `length` and the one that supplies a hand-written
`@@iterator`.

`Blob-stream.any.js` passes, and it is worth knowing why it can: it calls `garbageCollect()` from
`/common/gc.js`, whose fallback merely allocates a lot of garbage, so it asserts that a blob's stream keeps
working *across* a point where a collection may have happened — the same terms a browser runs it on without
`--expose-gc`. `Blob-constructor-detached-buffer.any.js` passes on the strength of
`Engine.Advanced`'s message ports: it detaches its buffer with
`new MessageChannel().port1.postMessage(buffer, [buffer])`.

## What the workers corpus says about this engine

**15 assertions across 11 files, of which 8 pass and 7 do not** — and the interesting figure is not the ratio
but that **six of the seven were decided in writing before a line of this corpus was run**, in the divergence
ledger of [issue #3167](https://github.com/sebastienros/jint/issues/3167). This is the smallest corpus here
and the one that most nearly assays a *design* rather than an implementation.

What passes is the worker global doing its job. `Worker-custom-event.any.js` adds a listener for a custom
event on `self` and dispatches one, so the worker global really is an event target.
`Worker-replace-event-handler.any.js` assigns `onmessage` eight times over.
`Worker-replace-global-constructor.any.js` replaces `self.MessageEvent`. `Worker-base64.any.js` finds `atob`
and `btoa`. `Worker-formdata.any.js` is the best of them: it builds a `FormData`, appends a `Blob` to it, and
then asserts that `postMessage(formData)` is a `DataCloneError` — so the worker global's `postMessage` is the
port's, running the real serializer, refusing the right value. The second row of
`semantics/multiple-workers/exposure.any.js` asserts `SharedWorker` is absent outside a window, and two of the
four rows of `interfaces/WorkerGlobalScope/self.any.js` pass on `self === self` and `'self' in self`.

**Five of the seven failures are one decision family** (`NeedsDeclinedWorkerGlobals`): the worker global is
the global the engine already builds plus the worker names, and there are three names it deliberately does not
add. `WorkerGlobalScope`/`DedicatedWorkerGlobalScope` (divergence #5) — an interface object with no such
prototype chain would make `self instanceof WorkerGlobalScope` answer false while the constructor was
nevertheless reachable, an `instanceof` that lies, and absence is the coherent half of that pair.
`WorkerLocation` (#6) — a worker's script name is its `Module.Location`, and there is no URL for the other
eight members to be parts of. `WorkerNavigator`, and `hardwareConcurrency` in particular (#7) — in Jint the
*host* owns every thread, so an engine answering a number would be guessing at a resource it does not
allocate. **The sixth** is `exposure.any.js`'s "Worker exposure" (`NeedsWorkerNesting`): nesting is off by
default, so `Worker` is `undefined` inside a worker until a provider grants it — a grant withheld rather than
a name declined, which is why it is a category of its own.

**The seventh is `NeedsTriage`, and the defect is not in the worker code.**
`interfaces/WorkerGlobalScope/self.any.js`, "self = 1", assigns to `self` and asserts it did not change.
`WebApiRegistration` installs `self` once, for every global, as an ordinary writable data property, and its
own comment says which definition that was written against: "HTML exposes `self` through a `[Replaceable]`
accessor pair on **Window**" — which was the only global there was at the time.
[`WorkerGlobalScope`'s](https://html.spec.whatwg.org/multipage/workers.html#dom-workerglobalscope-self) is a
plain `readonly attribute` with no `[Replaceable]`, so the worker inherited the window's semantics along with
the property. The install predates `Worker` and is shared with the top-level lane, so it is recorded rather
than fixed here, on the standing rule that the change which first runs a suite is not the change that moves
the engine. Fixing it means installing `self` per global rather than once; `DedicatedWorkerGlobalScope.name`
is the same shape and the same question.

**And the divergence the corpus made expensive rather than merely visible** is #5 again, from the other side:
it is why `workers/modules/` is not vendored at all. The canonical "am I in a dedicated worker" sniff —
`'DedicatedWorkerGlobalScope' in self && self instanceof DedicatedWorkerGlobalScope` — is how every one of
wpt's module-worker fixtures decides where to install its `onmessage` handler, so in Jint none of them
installs one and the whole flagship file hangs. The design named that wrinkle when it took the decision; this
is what it costs, and it is a cost paid by real third-party worker code and not only by a conformance suite.

## Updating the pin

Resolve `master` to a concrete commit, re-fetch every file in this directory at that commit, update the SHA
above, and run the suites. Expect the exclusion table to need work in the same change: the driver fails on an
entry that no longer applies, which is the point.
