# Vendored web-platform-tests

Everything in this directory is copied verbatim from
[web-platform-tests](https://github.com/web-platform-tests/wpt), at commit

    6c7127bdd9f2cc6a3668fd9791757843e09d5a9e

`wpt-LICENSE.md` is that commit's `LICENSE.md`; the corpus is redistributed here under the 3-Clause BSD
License it carries. Paths under this directory mirror paths in the wpt tree, which is what the harness's
`// META: script=` and `fetch()` resolution rely on — see `Jint.Tests.csproj` for the `LogicalName` that
keeps them intact through embedding.

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

Four standards are vendored: `url/` and `encoding/` as one suite each, `WebCryptoAPI/` as **eight** and
`streams/` as **six** — their root files plus one suite per sub-directory, because `WptCorpus.TestFiles`
lists a directory's own files and never descends. That is 151 theory cases: 48 of them the WebCryptoAPI
corpus's 24,136 assertions, 64 the streams corpus's 1,150, and the whole driver runs in about 50 seconds.

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
| `streams/transferable/*` | Transferring a stream through `postMessage()` is the one part of the Streams Standard Jint does not implement, there being nothing to transfer it to; the directory is worker, iframe and service-worker plumbing on top of that. [Issue #3186](https://github.com/sebastienros/jint/issues/3186). |
| `streams/readable-streams/owning-type*.tentative.any.js` | Upstream's `.tentative` marker again: owning-type readable streams are a proposal the Streams Standard has not adopted. |
| `streams/*/crashtests/*` | A crashtest — a regression reproduction rather than an assertion. |
| `streams/readable-byte-streams/construct-byob-request.any.js` | Reads `ReadableByteStreamController.prototype` and calls `new ReadableStreamBYOBRequest(…)` at *file scope*. Neither is a global here — see "Streams, including byte streams" in the repository README for the reduction — so the file throws before registering a test, and a harness error is for the whole file rather than something a per-test exclusion can name. The seven rows of `default-reader.any.js` that fail for the same reason fail *inside* test bodies, so those are in the exclusion table under `NeedsStreamInterfaceGlobals`. |

Nothing was left out for being slow. Every vendored file was timed at the pin; the slowest is
`derive_bits_keys/pbkdf2.https.any.js` at ~20 s for 8,632 cases (it is `// META: timeout=long` and sharded
nine ways upstream), then `generateKey/successes_RSA-OAEP.https.any.js` at ~7 s, which really does generate
156 RSA key pairs. Everything else is under 2 s. The whole streams corpus is 6.4 s for its 64 files, the
slowest being `readable-byte-streams/templated.any.js` at ~2.1 s and `readable-streams/templated.any.js` at
~1.0 s — both are `rs-test-templates.js` run over every stream shape — so nothing there is near the bar
either.

Everything else upstream that is not a `.any.js` file is out of scope by construction: `.window.js`, `.html`
and `.xhtml` tests are for a browsing context — which is what excludes
`WebCryptoAPI/algorithm-discards-context.https.window.js`.

## Two copies of `urltestdata.json`

`Jint.Tests/Runtime/WebApi/Resources/` holds its own copy of `urltestdata.json` and `setters_tests.json`,
pinned to an earlier commit (`67456344…`) and run row-by-row against the parser with no engine at all by
`UrlCorpusTests`. This directory's copy is at the pin above and is read by the suites through their own
`fetch()`, on a real engine, through the real `URL` bindings — so the two exercise different layers and are
deliberately not merged in this change. Unifying them onto one pin is worth doing once the harness has
settled; it is a change to `UrlCorpusTests` as well as to this directory, and it belongs in its own commit.

## What the WebCryptoAPI corpus says about this engine

2,459 of its 24,136 assertions do not pass, and every one is named in the driver's table under one of seven
categories, whose own documentation in `WptExclusions.cs` carries the citation. Three are the platform:
`NeedsPlatformCryptoParameters` (AES-GCM's 96-bit-only iv and 96-to-128-bit tag, RSA-OAEP's empty-only label,
RSA-PSS's hash-length-only salt — all four are limits of the BCL primitives, documented on the classes that
hit them), `NeedsCompressedEcPointImport` and `NeedsCurve25519`. Two are the corpus running ahead of the
specification: `NeedsKeyEncapsulation` (ML-KEM's `encapsulateKey`/`decapsulateKey` `KeyUsage` values, which
the current `KeyUsage` enumeration does not declare) and `NeedsQuotaExceededErrorInterface`. One is the
corpus meeting an environment it was not written for: `NeedsSecureContextModel`.

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

1,150 assertions across 64 files, of which **16 do not pass** — the highest-passing corpus vendored so far,
which is what one expects of an implementation written operation by operation against the standard, and also
why the sixteen are worth naming individually.

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

## Updating the pin

Resolve `master` to a concrete commit, re-fetch every file in this directory at that commit, update the SHA
above, and run the suites. Expect the exclusion table to need work in the same change: the driver fails on an
entry that no longer applies, which is the point.
