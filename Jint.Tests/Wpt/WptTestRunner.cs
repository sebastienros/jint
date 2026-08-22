#if NET8_0_OR_GREATER
#nullable enable

using System.Reflection;
using System.Text;

namespace Jint.Tests.Wpt;

/// <summary>
/// Runs the vendored web-platform-tests suites — URL, URL Pattern, Encoding, Web Cryptography, Streams,
/// Compression, File API, High Resolution Time, User Timing, HTML's workers, timers, microtask queuing and
/// structured clone, DOM events and aborting, and the network-free half of the Fetch object model — one
/// theory case per <c>.any.js</c> file, under the harness shim in <c>Prelude/testharness-shim.js</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>The exclusion table is the point of the driver.</b> A test that does not pass has to be named in
/// <see cref="_exclusions"/> with the category it belongs to, and every entry there must match at least one
/// failing test and no passing one — so an entry that has started passing fails the run, and so does one
/// that matches nothing at all, which is what a renamed or deleted case looks like. That is the test262
/// harness's discipline: a fix cannot leave a permanent exemption behind, and neither can a corpus bump.
/// </para>
/// <para>
/// This is https://github.com/sebastienros/jint/issues/3104: the shim, then a corpus at a time. Failures are
/// expected and found; the ones that are not a missing feature are parked under
/// <see cref="WptDivergence.NeedsTriage"/> rather than fixed there, so that the change which first runs a
/// suite is not also the change that moves the engine. The four the URL and Encoding suites found were fixed
/// by https://github.com/sebastienros/jint/issues/3121, and both the WebCryptoAPI corpus found are fixed too:
/// the point at which <c>SubtleCrypto</c> copies its caller's bytes by
/// https://github.com/sebastienros/jint/issues/3179, and ECDH's mismatched-curve error by
/// https://github.com/sebastienros/jint/issues/3180, the one the File API corpus filed by
/// https://github.com/sebastienros/jint/issues/3209 — an <c>Array.prototype.values</c>/<c>keys</c>/
/// <c>entries</c> defect with no web API in it at all — and all five the streams corpus filed by
/// https://github.com/sebastienros/jint/issues/3195. The category holds the one the workers corpus
/// found, which is a <c>self</c> installed against <c>Window</c>'s definition for every global including
/// a worker's, and the ten that groups 3 and 4 of https://github.com/sebastienros/jint/issues/3185 did.
/// <c>Vendor/README.md</c> analyses each.
/// </para>
/// <para>
/// <b>A corpus with sub-directories is one suite per directory</b> rather than one for the lot, because
/// <see cref="WptCorpus.TestFiles"/> lists a directory's own files and never descends. That is deliberate: a
/// suite is a theory, a theory is a line in a test report, and thirty-four of them tell a reader which
/// operation went red where a dozen would not. <c>compression/</c>, <c>urlpattern/</c>, <c>hr-time/</c> and
/// <c>user-timing/</c> have a single directory apiece and are therefore one suite apiece.
/// </para>
/// <para>
/// <b>The <c>workers/</c> corpus runs in a lane of its own</b>: its files are the body of a real module
/// worker rather than a script in the driver's engine, because every one of them carries
/// <c>// META: global=worker</c> and would otherwise assert nothing about a worker. See
/// <see cref="WptHarness.RunsInAWorker"/> for how the lane is chosen, <see cref="WptWorkerProvider"/> for the
/// provider and the cooperative pump, and <c>Vendor/README.md</c> for the twenty upstream files the lane
/// still cannot reach.
/// </para>
/// </remarks>
public class WptTestRunner
{
    /// <summary>
    /// Upstream files that are deliberately not vendored, as globs over their path in the wpt tree. Checked
    /// against what <i>is</i> vendored, so a re-vendor that pulls one back in without revisiting the reason
    /// fails rather than silently adding a red suite. <c>Vendor/README.md</c> carries the same table in
    /// prose, with the reason for each.
    /// </summary>
    private static readonly (string Pattern, string Reason)[] _notVendored =
    [
        // idlharness needs /resources/idlharness.js and /resources/WebIDLParser.js — a WebIDL conformance
        // framework of its own, an order of magnitude larger than the shim, and testing a layer Jint's
        // source-generated built-ins do not have. The star before ".any.js" is what reaches WebCryptoAPI's
        // "idlharness.https.any.js" and "idlharness.tentative.https.any.js" as well as the bare spelling the
        // url and encoding suites use.
        ("*/idlharness*.any.js", "needs the WebIDL conformance harness"),

        // The legacy multi-byte decoders, which a sibling change implements:
        // https://github.com/sebastienros/jint/issues/3106. The single-byte families that used to sit here are
        // implemented, and their two files are vendored now — single-byte-decoder.any.js keeps its
        // XMLHttpRequest half excluded per test, because that half asks a wptserve handler to generate the
        // bytes and their charset.
        ("encoding/legacy-mb-*", "legacy multi-byte decoders, issue #3106"),
        ("encoding/iso-2022-jp-decoder.any.js", "legacy multi-byte decoder, issue #3106"),
        ("encoding/replacement-encodings.any.js", "the replacement encoding, issue #3106; also needs XMLHttpRequest"),
        ("encoding/unsupported-encodings.any.js", "needs XMLHttpRequest and data: URLs"),

        // IdnaTestV2 is a 314 kB conformance corpus for UTS-46 alone. Jint's Idna builds on IdnMapping and
        // documents where that diverges (VerifyDnsLength, CheckHyphens, ICU version skew); running the
        // corpus is an IDNA triage in its own right rather than part of standing the harness up.
        ("url/IdnaTestV2*.any.js", "UTS-46 conformance corpus, its own triage"),

        // ---------------------------------------------------------------- WebCryptoAPI
        // Upstream marks a file ".tentative" when it tests a proposal the specification has not adopted —
        // ML-KEM, ML-DSA, KMAC, cSHAKE, SHA-3, TurboSHAKE, KangarooTwelve, AES-OCB, ChaCha20-Poly1305,
        // Argon2, Ed448/X448. Jint registers what https://w3c.github.io/webcrypto/#algorithm-overview lists
        // and nothing else, so every one of these would be a red file saying only that.
        ("*.tentative.https.any.js", "tests a proposal the specification has not adopted"),

        // Curve25519. The BCL ships neither X25519 nor Ed25519 — there is no ECCurve for them and no
        // primitive to build on — so the files dedicated to those curves are out of scope for a crypto layer
        // written against it. The rows that sit inside a file which is otherwise about something else are
        // excluded one by one instead, under WptDivergence.NeedsCurve25519.
        ("*_Ed25519.https.any.js", "Ed25519: the BCL ships no Curve25519 primitive"),
        ("*_X25519.https.any.js", "X25519: the BCL ships no Curve25519 primitive"),
        ("*/eddsa*", "Ed25519 and Ed448 signatures"),
        ("*/cfrg_curves*", "X25519 and X448 key agreement"),
        ("*/okp_importKey*", "Ed25519/X25519/Ed448/X448 key import"),
        ("*/argon2*", "Argon2, a proposal"),

        // Whole directories, each for one reason. `serialization/` round-trips a CryptoKey through
        // structuredClone, which is the HTML serialization steps for a platform object rather than anything
        // crypto.subtle does; `encap_decap/` is the ML-KEM proposal; `secure_context/` is a .sub.html test
        // for a browsing context; `crashtests/` are regression reproductions rather than assertions; and
        // `tools/` is the corpus's own Python generator.
        ("WebCryptoAPI/serialization/*", "structured-clone of a CryptoKey, not a crypto.subtle operation"),
        ("WebCryptoAPI/encap_decap/*", "ML-KEM key encapsulation, a proposal"),
        ("WebCryptoAPI/secure_context/*", "a .sub.html test for a browsing context"),
        ("WebCryptoAPI/import_export/crashtests/*", "a crashtest rather than an assertion"),
        ("WebCryptoAPI/tools/*", "the corpus's own generator, not a test"),

        // ---------------------------------------------------------------- streams
        // The transferable-streams directory is vendored now that transferring a stream works
        // (https://github.com/sebastienros/jint/issues/3199), but only the one .any.js file it has. The two
        // .window.js tests drive an iframe and a MessagePort helper page, and resources/ is the iframe,
        // worker, shared-worker and service-worker plumbing they and the directory's .html files load — all
        // of it a browsing context, which is what the blanket ".any.js only" rule already excludes; these two
        // rows say so by name because the directory would otherwise look half-vendored by accident.
        ("streams/transferable/*.window.js", "drives an iframe and a worker; there is no browsing context here"),
        ("streams/transferable/resources/*", "the iframe, worker and service-worker pages those tests load"),

        // Upstream's ".tentative" marker again, this time without the ".https." the WebCryptoAPI files carry:
        // the owning-type readable streams, a proposal the Streams Standard has not adopted.
        ("*.tentative.any.js", "tests a proposal the specification has not adopted"),

        // Crash reproductions rather than assertions, the same reason WebCryptoAPI's crashtests are out.
        ("streams/*/crashtests/*", "a crashtest rather than an assertion"),

        // The file reads ReadableByteStreamController.prototype and calls `new ReadableStreamBYOBRequest(…)`
        // at file scope. Neither interface object is a global in Jint — the documented reduction is that only
        // the five interfaces a script constructs by name are installed (README.md, "Streams, including byte
        // streams") — so the file throws a ReferenceError before registering a single test. A harness error is
        // for the whole file and no per-test exclusion can name it, which is what puts this row here rather
        // than in the exclusion table beside default-reader.any.js's seven rows, which fail for the same
        // reason but inside test bodies.
        ("streams/readable-byte-streams/construct-byob-request.any.js",
            "reads the interface objects as globals at file scope; Jint installs only the five constructible ones"),

        // ---------------------------------------------------------------- compression
        // Both fetch a binary fixture out of wpt's `/media/` directory — a 384 kB WebM and, for the second,
        // a WebVTT file as well — and read it back with `response.arrayBuffer()` / `response.bytes()`. The
        // shim's `fetch` is a *text* reader over the vendored tree (see its header), so neither the transport
        // nor the accessor exists here, and vendoring a third of a megabyte of video to compress it would be
        // a strange thing for this corpus to carry. compression-stream.any.js additionally calls `fetch` at
        // file scope, so its failure could not even be a per-test one.
        ("compression/compression-output-length.any.js", "fetches a 384 kB binary media fixture from the wpt server"),
        ("compression/compression-stream.any.js", "fetches binary media fixtures from the wpt server, at file scope"),

        // The file writes a member plus one trailing byte and never closes the writer, so the second
        // `reader.read()` settles only if the trailing byte errors the stream. It does not here — that is the
        // second of the two divergences DecompressionCodec documents (see WptDivergence.NeedsIncrementalInflater)
        // — so the read waits for input that cannot arrive and the *file* stalls rather than any test failing.
        // A stalled run is a harness error for the whole file, which no per-test exclusion can name, exactly
        // as streams/readable-byte-streams/construct-byob-request.any.js is. The divergence itself is still
        // asserted, by decompression-corrupt-input.any.js's six excluded rows.
        ("compression/decompression-extra-input.any.js",
            "hangs on the trailing-byte divergence rather than failing a test"),

        // ---------------------------------------------------------------- urlpattern
        // Byte-identical to urlpattern.any.js — upstream ships both so a browser runs the corpus over http
        // and over https, and Jint has no scheme to be served over. Vendoring it would run the same 370 cases
        // twice and assert nothing the first run did not.
        ("urlpattern/urlpattern.https.any.js", "byte-identical to urlpattern.any.js, which is vendored"),

        // ---------------------------------------------------------------- FileAPI
        // All four POST a FormData to wptserve's `/fetch/api/resources/echo-content.py` and assert on the
        // multipart body that comes back, so they need the fetch object model, an outbound request and the
        // server's own Python handler. Serializing a FormData as multipart/form-data is a fetch body's job
        // and arrives with that feature; WebApiFeatures.Default never includes it.
        ("FileAPI/file/send-file-formdata*.any.js", "posts multipart/form-data to wptserve's echo-content.py"),

        // The one file in the FileAPI root that is neither vendored nor a browsing-context test. Jint has no
        // FileReader — a Blob is read here through text()/arrayBuffer()/bytes()/stream() — and the file is
        // about the reader's state machine (readyState, abort, the progress events) rather than about a Blob.
        // Its sibling unicode.any.js needs no reader at all and *is* vendored.
        ("FileAPI/fileReader.any.js", "FileReader is not implemented"),

        // ---------------------------------------------------------------- workers
        // Twenty-one files that look like the most runnable thing in the directory and are the least: every
        // single one opens with `importScripts("/resources/testharness.js")` at file scope. A .worker.js is a
        // *classic* worker's top-level script, and Jint runs module workers only — importScripts is present and
        // throws a TypeError, which is the module-worker step the specification itself prescribes. So the file
        // throws before registering a test, and a harness error is for the whole file rather than something a
        // per-test exclusion can name. What they assert about the worker global is asserted instead by the
        // vendored `.any.js` files beside them and by Jint.Tests/Runtime/WebApi/WorkerMechanismTests.cs.
        ("workers/*.worker.js", "a classic worker's top-level script: importScripts at file scope"),

        // SharedWorker and its global, which Jint does not have — the design records it as still open, needing
        // a cross-engine name registry. `global=sharedworker` files cannot even be run in the worker lane:
        // there is no shared worker to be the global of.
        ("workers/SharedWorker-*.any.js", "SharedWorker is not implemented"),
        ("workers/semantics/interface-objects/*", "global=sharedworker, and the .worker.js pair above"),

        // Upstream's own tutorial directory, and it teaches wpt rather than testing an engine: general.any.js
        // is two tests, the second of which asserts
        // `location.pathname === "/workers/examples/general.any.worker.js"` — the path of the glue script the
        // wpt server generates for a `.any.js` file. There is no server here to generate one. onconnect.any.js
        // beside it is global=sharedworker.
        ("workers/examples/*", "upstream's tutorial: asserts the wpt server's own generated glue, and a SharedWorker"),

        // `.sub.` is server-side substitution: wptserve rewrites {{host}}/{{ports[…]}} into a real origin
        // before serving the file. Nothing here serves anything, so a vendored copy would carry the
        // placeholders verbatim. Worker-location.sub.any.js additionally asserts every member of a
        // WorkerLocation, which is the declined feature below.
        ("workers/Worker-location.sub.any.js", "needs wptserve substitution and a WorkerLocation"),
        ("workers/interfaces/WorkerUtils/importScripts/*", "classic-worker importScripts, most of it .sub. as well"),
        ("workers/importscripts_mime*.any.js", "classic-worker importScripts, over server-chosen MIME types"),

        // The only file in the directory whose whole assertion is `location === location`. The harness shim
        // installs a stub `location` of its own — `/common/subset-tests.js` reads `location.search` to pick a
        // shard — so a vendored copy would pass against the shim while Jint deliberately has no WorkerLocation
        // at all (the worker's script name is its Module.Location). A test that can only assert the harness is
        // worse than no test, which is why this is a not-vendored row and not an exclusion.
        ("workers/interfaces/WorkerGlobalScope/location/*", "would assert the shim's own stub location"),

        // The module-worker corpus, and the one row here that is a finding rather than a decision. Every one of
        // the nine worker scripts import-test-cases.js drives opens with
        // `if ('DedicatedWorkerGlobalScope' in self && self instanceof DedicatedWorkerGlobalScope)` and installs
        // its onmessage handler *inside* that branch. Jint ships no DedicatedWorkerGlobalScope interface object
        // (the design's divergence #5, ruled together with #3195 — an interface object without the prototype
        // chain would make `instanceof` lie), so no branch is taken, no handler is installed, the worker never
        // answers, and all nine promise_tests hang: a harness error for the whole file with no test to name.
        // The sniff is in the fixture rather than in an assertion, which is exactly why no per-test exclusion
        // can express it. What the file would have proved about module loading in a worker — static, nested,
        // dynamic, and the two orders of the pair — is proved instead by WorkerMechanismTests' module pins.
        // The blob-url and data-url siblings need URL.createObjectURL and a data: module loader on top of that.
        ("workers/modules/*", "the fixtures sniff DedicatedWorkerGlobalScope, which Jint deliberately does not expose"),

        // ---------------------------------------------------------------- hr-time and user-timing
        // The file reads `PerformanceObserver.supportedEntryTypes` at *file scope* to decide whether to
        // register its promise tests, so on an engine with no PerformanceObserver it throws a ReferenceError
        // before the first test is registered — a harness error for the whole file, which no per-test
        // exclusion can name. The rows that reach for an observer from inside a test body are excluded one by
        // one instead, under WptDivergence.NeedsPerformanceObserver.
        ("user-timing/supported-usertiming-types.any.js",
            "reads PerformanceObserver at file scope; PerformanceInstance documents declining the observer"),

        // ---------------------------------------------------------------- dom
        // Fourteen of its fifteen tests are registered without a name, so every one of them is reported under
        // the same name and no per-test exclusion can single out the two that fail — which is what puts this
        // row here rather than in the exclusion table. The two are Event's legacy `srcElement` and
        // `returnValue` members, which this engine does not have; both are recorded as defects in
        // Vendor/README.md, and `returnValue` also has an exclusion of its own in
        // AddEventListenerOptions-passive.any.js, where the test that finds it *is* named.
        ("dom/events/Event-constructors.any.js",
            "registers every test without a name, so its two failures cannot be named; see Vendor/README.md"),

        // ---------------------------------------------------------------- html/webappapis
        // setTimeout's string handler, which TimerFunctions documents declining: compiling the string is eval
        // by another name and reachable even where a host disabled string compilation, so it is a TypeError
        // here as it is in Node. This file's whole subject is that form, and it uses it at file scope.
        ("html/webappapis/timers/evil-spec-example.any.js",
            "setTimeout's string handler, which TimerFunctions declines"),

        // The file's one test throws from a queueMicrotask callback and expects an `error` event at the global
        // scope. HTML gives queueMicrotask WebIDL's "report" exception behaviour, and Jint lets the throw
        // erupt from whatever is pumping instead, so the exception leaves the engine before the listener can
        // see it and the file is a harness error rather than a failing test. Recorded as a defect in
        // Vendor/README.md; it cannot be an exclusion, because there is no test left to name.
        ("html/webappapis/microtask-queuing/queue-microtask-exceptions.any.js",
            "an exception from a queueMicrotask callback erupts instead of being reported; see Vendor/README.md"),

        // ---------------------------------------------------------------- fetch
        // Every file in request/ builds its Request from a *relative* url — "", "./", "../resources/…" — and
        // RequestConstructor documents why that cannot work: the specification resolves such a string against
        // "the entry settings object's API base URL", which is a document's url, and an embedded engine has no
        // document. Most of them do it at file scope, so there is not even a test to exclude.
        ("fetch/api/request/*", "constructs a Request from a relative url; there is no API base URL here"),

        // The rest of fetch/api/ is a client talking to wptserve: .py handlers that echo headers, trickle
        // bytes, redirect, stall, or check CORS preflights. There is no server in this driver and the shim's
        // `fetch` is a reader over the vendored tree, so none of it can run.
        ("fetch/api/abort/*", "needs a wpt server"),
        ("fetch/api/basic/*", "needs a wpt server"),
        ("fetch/api/body/*", "needs a wpt server"),
        ("fetch/api/cors/*", "needs a wpt server"),
        ("fetch/api/credentials/*", "needs a wpt server"),
        ("fetch/api/policies/*", "needs a wpt server"),
        ("fetch/api/redirect/*", "needs a wpt server"),
        ("fetch/api/crashtests/*", "a crashtest rather than an assertion"),
        ("fetch/api/headers/header-values.any.js", "needs a wpt server"),
        ("fetch/api/headers/header-values-normalize.any.js", "needs a wpt server"),
        ("fetch/api/headers/headers-no-cors.any.js", "needs a wpt server"),
        ("fetch/api/response/json.any.js", "fetches a data: url"),
        ("fetch/api/response/response-cancel-stream.any.js", "needs a wpt server"),
        ("fetch/api/response/response-clone.any.js", "needs a wpt server"),
        ("fetch/api/response/response-headers-guard.any.js", "needs a wpt server"),
        ("fetch/api/response/response-blob-realm.any.js", "needs a document and a second realm"),
    ];

    /// <summary>
    /// The lowest number of tests each file must produce. A suite whose corpus stopped being embedded, or
    /// whose registration loop threw after the first case, would otherwise be a green run of nothing. The
    /// figures are the counts observed at the pinned commit, rounded down, so a corpus bump that adds cases
    /// needs no edit and one that halves a file does.
    /// </summary>
    private static readonly Dictionary<string, int> _minimumTests = new(StringComparer.Ordinal)
    {
        ["url/historical.any.js"] = 5,
        ["url/url-constructor.any.js"] = 800,
        ["url/url-origin.any.js"] = 350,
        ["url/url-searchparams.any.js"] = 4,
        ["url/url-setters-stripping.any.js"] = 200,
        ["url/url-setters.any.js"] = 250,
        ["url/url-statics-canparse.any.js"] = 5,
        ["url/url-statics-parse.any.js"] = 5,
        ["url/url-tojson.any.js"] = 1,
        ["url/urlencoded-parser.any.js"] = 90,
        ["url/urlsearchparams-append.any.js"] = 4,
        ["url/urlsearchparams-constructor.any.js"] = 25,
        ["url/urlsearchparams-delete.any.js"] = 6,
        ["url/urlsearchparams-foreach.any.js"] = 5,
        ["url/urlsearchparams-get.any.js"] = 2,
        ["url/urlsearchparams-getall.any.js"] = 2,
        ["url/urlsearchparams-has.any.js"] = 4,
        ["url/urlsearchparams-set.any.js"] = 2,
        ["url/urlsearchparams-size.any.js"] = 3,
        ["url/urlsearchparams-sort.any.js"] = 15,
        ["url/urlsearchparams-stringifier.any.js"] = 12,

        ["encoding/api-basics.any.js"] = 6,
        ["encoding/api-invalid-label.any.js"] = 3000,
        ["encoding/api-replacement-encodings.any.js"] = 5,
        ["encoding/api-surrogates-utf8.any.js"] = 5,
        ["encoding/encodeInto.any.js"] = 100,
        ["encoding/textdecoder-arguments.any.js"] = 4,
        ["encoding/textdecoder-byte-order-marks.any.js"] = 3,
        ["encoding/textdecoder-copy.any.js"] = 2,
        ["encoding/textdecoder-eof.any.js"] = 2,
        ["encoding/textdecoder-fatal-streaming.any.js"] = 2,
        ["encoding/textdecoder-fatal.any.js"] = 30,
        ["encoding/textdecoder-ignorebom.any.js"] = 4,
        ["encoding/textdecoder-labels.any.js"] = 200,
        ["encoding/textdecoder-mistakes.any.js"] = 80,
        ["encoding/textdecoder-streaming.any.js"] = 30,
        ["encoding/textdecoder-utf16-surrogates.any.js"] = 8,
        ["encoding/textencoder-constructor-non-utf.any.js"] = 70,
        ["encoding/textencoder-utf16-surrogates.any.js"] = 6,

        ["WebCryptoAPI/crypto_key_cached_slots.https.any.js"] = 2,
        ["WebCryptoAPI/getRandomValues.any.js"] = 30,
        ["WebCryptoAPI/historical.any.js"] = 3,
        ["WebCryptoAPI/normalize-algorithm-name.https.any.js"] = 4,
        ["WebCryptoAPI/randomUUID.https.any.js"] = 3,

        ["WebCryptoAPI/derive_bits_keys/derive_key_and_encrypt.https.any.js"] = 5,
        ["WebCryptoAPI/derive_bits_keys/derived_bits_length.https.any.js"] = 40,
        ["WebCryptoAPI/derive_bits_keys/ecdh_bits.https.any.js"] = 35,
        ["WebCryptoAPI/derive_bits_keys/ecdh_keys.https.any.js"] = 30,
        ["WebCryptoAPI/derive_bits_keys/hkdf.https.any.js"] = 3500,
        ["WebCryptoAPI/derive_bits_keys/pbkdf2.https.any.js"] = 8500,

        ["WebCryptoAPI/digest/digest.https.any.js"] = 110,

        ["WebCryptoAPI/encrypt_decrypt/aes_cbc.https.any.js"] = 60,
        ["WebCryptoAPI/encrypt_decrypt/aes_ctr.https.any.js"] = 50,
        ["WebCryptoAPI/encrypt_decrypt/aes_gcm.https.any.js"] = 550,
        ["WebCryptoAPI/encrypt_decrypt/aes_gcm_256_iv.https.any.js"] = 550,
        ["WebCryptoAPI/encrypt_decrypt/rsa_oaep.https.any.js"] = 180,

        ["WebCryptoAPI/generateKey/failures_AES-CBC.https.any.js"] = 680,
        ["WebCryptoAPI/generateKey/failures_AES-CTR.https.any.js"] = 680,
        ["WebCryptoAPI/generateKey/failures_AES-GCM.https.any.js"] = 680,
        ["WebCryptoAPI/generateKey/failures_AES-KW.https.any.js"] = 230,
        ["WebCryptoAPI/generateKey/failures_ECDH.https.any.js"] = 170,
        ["WebCryptoAPI/generateKey/failures_ECDSA.https.any.js"] = 140,
        ["WebCryptoAPI/generateKey/failures_HMAC.https.any.js"] = 430,
        ["WebCryptoAPI/generateKey/failures_RSA-OAEP.https.any.js"] = 340,
        ["WebCryptoAPI/generateKey/failures_RSA-PSS.https.any.js"] = 110,
        ["WebCryptoAPI/generateKey/failures_RSASSA-PKCS1-v1_5.https.any.js"] = 110,
        ["WebCryptoAPI/generateKey/failures_bad_algorithm.https.any.js"] = 350,
        ["WebCryptoAPI/generateKey/successes_AES-CBC.https.any.js"] = 280,
        ["WebCryptoAPI/generateKey/successes_AES-CTR.https.any.js"] = 280,
        ["WebCryptoAPI/generateKey/successes_AES-GCM.https.any.js"] = 280,
        ["WebCryptoAPI/generateKey/successes_AES-KW.https.any.js"] = 70,
        ["WebCryptoAPI/generateKey/successes_ECDH.https.any.js"] = 100,
        ["WebCryptoAPI/generateKey/successes_ECDSA.https.any.js"] = 80,
        ["WebCryptoAPI/generateKey/successes_HMAC.https.any.js"] = 190,
        ["WebCryptoAPI/generateKey/successes_RSA-OAEP.https.any.js"] = 150,
        ["WebCryptoAPI/generateKey/successes_RSA-PSS.https.any.js"] = 36,
        ["WebCryptoAPI/generateKey/successes_RSASSA-PKCS1-v1_5.https.any.js"] = 36,

        ["WebCryptoAPI/import_export/ec_importKey.https.any.js"] = 260,
        ["WebCryptoAPI/import_export/ec_importKey_failures_ECDH.https.any.js"] = 900,
        ["WebCryptoAPI/import_export/ec_importKey_failures_ECDSA.https.any.js"] = 900,
        ["WebCryptoAPI/import_export/rsa_importKey.https.any.js"] = 1000,
        ["WebCryptoAPI/import_export/symmetric_importKey.https.any.js"] = 600,

        ["WebCryptoAPI/sign_verify/ecdsa.https.any.js"] = 320,
        ["WebCryptoAPI/sign_verify/hmac.https.any.js"] = 65,
        ["WebCryptoAPI/sign_verify/rsa_pkcs.https.any.js"] = 65,
        ["WebCryptoAPI/sign_verify/rsa_pss.https.any.js"] = 140,

        ["WebCryptoAPI/wrapKey_unwrapKey/wrapKey_unwrapKey.https.any.js"] = 230,

        ["streams/queuing-strategies.any.js"] = 18,

        ["streams/readable-streams/async-iterator.any.js"] = 36,
        ["streams/readable-streams/bad-strategies.any.js"] = 8,
        ["streams/readable-streams/bad-underlying-sources.any.js"] = 19,
        ["streams/readable-streams/cancel.any.js"] = 11,
        ["streams/readable-streams/constructor.any.js"] = 1,
        ["streams/readable-streams/count-queuing-strategy-integration.any.js"] = 4,
        ["streams/readable-streams/default-reader.any.js"] = 26,
        ["streams/readable-streams/floating-point-total-queue-size.any.js"] = 4,
        ["streams/readable-streams/from.any.js"] = 45,
        ["streams/readable-streams/garbage-collection.any.js"] = 5,
        ["streams/readable-streams/general.any.js"] = 34,
        ["streams/readable-streams/patched-global.any.js"] = 5,
        ["streams/readable-streams/reentrant-strategies.any.js"] = 10,
        ["streams/readable-streams/tee.any.js"] = 23,
        ["streams/readable-streams/templated.any.js"] = 81,

        ["streams/readable-byte-streams/bad-buffers-and-views.any.js"] = 21,
        ["streams/readable-byte-streams/enqueue-with-detached-buffer.any.js"] = 1,
        ["streams/readable-byte-streams/general.any.js"] = 90,
        ["streams/readable-byte-streams/non-transferable-buffers.any.js"] = 4,
        ["streams/readable-byte-streams/patched-global.any.js"] = 1,
        ["streams/readable-byte-streams/read-min.any.js"] = 21,
        ["streams/readable-byte-streams/respond-after-enqueue.any.js"] = 3,
        ["streams/readable-byte-streams/tee.any.js"] = 36,
        ["streams/readable-byte-streams/templated.any.js"] = 30,

        ["streams/writable-streams/aborting.any.js"] = 58,
        ["streams/writable-streams/bad-strategies.any.js"] = 7,
        ["streams/writable-streams/bad-underlying-sinks.any.js"] = 14,
        ["streams/writable-streams/byte-length-queuing-strategy.any.js"] = 1,
        ["streams/writable-streams/close.any.js"] = 23,
        ["streams/writable-streams/constructor.any.js"] = 13,
        ["streams/writable-streams/count-queuing-strategy.any.js"] = 3,
        ["streams/writable-streams/error.any.js"] = 5,
        ["streams/writable-streams/floating-point-total-queue-size.any.js"] = 4,
        ["streams/writable-streams/garbage-collection.any.js"] = 1,
        ["streams/writable-streams/general.any.js"] = 16,
        ["streams/writable-streams/properties.any.js"] = 8,
        ["streams/writable-streams/reentrant-strategy.any.js"] = 7,
        ["streams/writable-streams/start.any.js"] = 8,
        ["streams/writable-streams/write.any.js"] = 13,

        ["streams/transform-streams/backpressure.any.js"] = 14,
        ["streams/transform-streams/cancel.any.js"] = 11,
        ["streams/transform-streams/errors.any.js"] = 18,
        ["streams/transform-streams/flush.any.js"] = 6,
        ["streams/transform-streams/general.any.js"] = 23,
        ["streams/transform-streams/lipfuzz.any.js"] = 18,
        ["streams/transform-streams/patched-global.any.js"] = 2,
        ["streams/transform-streams/properties.any.js"] = 6,
        ["streams/transform-streams/reentrant-strategies.any.js"] = 11,
        ["streams/transform-streams/strategies.any.js"] = 10,
        ["streams/transform-streams/terminate.any.js"] = 6,

        ["streams/piping/abort.any.js"] = 29,
        ["streams/piping/close-propagation-backward.any.js"] = 16,
        ["streams/piping/close-propagation-forward.any.js"] = 27,
        ["streams/piping/error-propagation-backward.any.js"] = 31,
        ["streams/piping/error-propagation-forward.any.js"] = 28,
        ["streams/piping/flow-control.any.js"] = 5,
        ["streams/piping/general-addition.any.js"] = 1,
        ["streams/piping/general.any.js"] = 14,
        ["streams/piping/multiple-propagation.any.js"] = 9,
        ["streams/piping/pipe-through.any.js"] = 38,
        ["streams/piping/then-interception.any.js"] = 2,
        ["streams/piping/throwing-options.any.js"] = 8,
        ["streams/piping/transform-streams.any.js"] = 1,

        ["streams/transferable/transform-stream-members.any.js"] = 4,

        ["compression/compression-bad-chunks.any.js"] = 28,
        ["compression/compression-constructor-error.any.js"] = 3,
        ["compression/compression-including-empty-chunk.any.js"] = 12,
        ["compression/compression-large-flush-output.any.js"] = 4,
        ["compression/compression-multiple-chunks.any.js"] = 60,
        ["compression/compression-with-detach.any.js"] = 1,
        ["compression/decompression-bad-chunks.any.js"] = 36,
        ["compression/decompression-buffersource.any.js"] = 48,
        ["compression/decompression-constructor-error.any.js"] = 3,
        ["compression/decompression-correct-input.any.js"] = 4,
        ["compression/decompression-corrupt-input.any.js"] = 29,
        ["compression/decompression-empty-input.any.js"] = 4,
        ["compression/decompression-split-chunk.any.js"] = 60,
        ["compression/decompression-uint8array-output.any.js"] = 4,
        ["compression/decompression-with-detach.any.js"] = 1,

        // 369 entries in resources/urlpatterntestdata.json plus the promise_test that loads it. Rounded
        // down, because this one really is a corpus and upstream adds rows to it.
        ["urlpattern/urlpattern.any.js"] = 350,
        ["urlpattern/urlpattern-constructor.any.js"] = 2,
        ["urlpattern/urlpattern-hasregexpgroups.any.js"] = 1,

        ["FileAPI/blob/Blob-array-buffer.any.js"] = 5,
        ["FileAPI/blob/Blob-bytes.any.js"] = 5,
        ["FileAPI/blob/Blob-constructor-detached-buffer.any.js"] = 4,
        ["FileAPI/blob/Blob-constructor-endings.any.js"] = 11,
        ["FileAPI/blob/Blob-constructor.any.js"] = 70,
        ["FileAPI/blob/Blob-newobject.any.js"] = 4,
        ["FileAPI/blob/Blob-slice-overflow.any.js"] = 4,
        ["FileAPI/blob/Blob-slice.any.js"] = 150,
        ["FileAPI/blob/Blob-stream.any.js"] = 6,
        ["FileAPI/blob/Blob-text.any.js"] = 8,
        ["FileAPI/blob/Blob-textStream.any.js"] = 8,

        ["FileAPI/file/File-constructor-endings.any.js"] = 11,
        ["FileAPI/file/File-constructor.any.js"] = 49,

        ["FileAPI/unicode.any.js"] = 4,

        // Small numbers, because these files are small: the worker corpus asks one question per file about the
        // global it is running in, and the floor is what proves the file reached a worker at all rather than
        // failing to start one.
        ["workers/Worker-base64.any.js"] = 1,
        ["workers/Worker-constructor-proto.any.js"] = 1,
        ["workers/Worker-custom-event.any.js"] = 1,
        ["workers/Worker-formdata.any.js"] = 1,
        ["workers/Worker-replace-event-handler.any.js"] = 1,
        ["workers/Worker-replace-global-constructor.any.js"] = 1,
        ["workers/Worker-replace-self.any.js"] = 1,
        ["workers/WorkerNavigator-hardware-concurrency.any.js"] = 1,
        ["workers/WorkerNavigator.any.js"] = 1,
        ["workers/interfaces/WorkerGlobalScope/self.any.js"] = 4,
        ["workers/semantics/multiple-workers/exposure.any.js"] = 2,

        ["encoding/single-byte-decoder.any.js"] = 330,
        ["encoding/textdecoder-fatal-single-byte.any.js"] = 7000,

        ["hr-time/basic.any.js"] = 5,
        ["hr-time/monotonic-clock.any.js"] = 2,

        ["user-timing/buffered-flag.any.js"] = 2,
        ["user-timing/case-sensitivity.any.js"] = 1,
        ["user-timing/clear_all_marks.any.js"] = 1,
        ["user-timing/clear_all_measures.any.js"] = 1,
        ["user-timing/clear_non_existent_mark.any.js"] = 1,
        ["user-timing/clear_non_existent_measure.any.js"] = 1,
        ["user-timing/clear_one_mark.any.js"] = 1,
        ["user-timing/clear_one_measure.any.js"] = 1,
        ["user-timing/entry_type.any.js"] = 2,
        ["user-timing/mark-entry-constructor.any.js"] = 6,
        ["user-timing/mark-errors.any.js"] = 10,
        ["user-timing/mark-l3.any.js"] = 1,
        ["user-timing/mark-measure-return-objects.any.js"] = 5,
        ["user-timing/mark.any.js"] = 22,
        ["user-timing/measure-l3.any.js"] = 3,
        ["user-timing/measure-with-dict.any.js"] = 2,
        ["user-timing/measure_syntax_err.any.js"] = 5,
        ["user-timing/structured-serialize-detail.any.js"] = 9,
        ["user-timing/user_timing_exists.any.js"] = 4,

        ["html/webappapis/timers/clearinterval-from-callback.any.js"] = 1,
        ["html/webappapis/timers/cleartimeout-clearinterval.any.js"] = 2,
        ["html/webappapis/timers/missing-timeout-setinterval.any.js"] = 2,
        ["html/webappapis/timers/negative-setinterval.any.js"] = 1,
        ["html/webappapis/timers/negative-settimeout.any.js"] = 1,
        ["html/webappapis/timers/setinterval-settimeout-clamping.any.js"] = 2,
        ["html/webappapis/timers/type-long-setinterval.any.js"] = 1,
        ["html/webappapis/timers/type-long-settimeout.any.js"] = 1,

        ["html/webappapis/microtask-queuing/queue-microtask.any.js"] = 5,

        ["html/webappapis/structured-clone/structured-clone.any.js"] = 130,

        ["dom/events/AddEventListenerOptions-once.any.js"] = 4,
        ["dom/events/AddEventListenerOptions-passive.any.js"] = 5,
        ["dom/events/AddEventListenerOptions-signal.any.js"] = 11,
        ["dom/events/Event-isTrusted.any.js"] = 1,
        ["dom/events/EventTarget-add-remove-listener.any.js"] = 1,
        ["dom/events/EventTarget-addEventListener.any.js"] = 1,
        ["dom/events/EventTarget-constructible.any.js"] = 3,
        ["dom/events/EventTarget-removeEventListener.any.js"] = 1,

        ["dom/abort/AbortSignal.any.js"] = 2,
        ["dom/abort/abort-signal-any.any.js"] = 14,
        ["dom/abort/event.any.js"] = 16,
        ["dom/abort/timeout.any.js"] = 3,

        ["fetch/api/headers/header-setcookie.any.js"] = 24,
        ["fetch/api/headers/headers-basic.any.js"] = 23,
        ["fetch/api/headers/headers-casing.any.js"] = 4,
        ["fetch/api/headers/headers-combine.any.js"] = 6,
        ["fetch/api/headers/headers-errors.any.js"] = 18,
        ["fetch/api/headers/headers-forbidden-override.any.js"] = 90,
        ["fetch/api/headers/headers-normalize.any.js"] = 3,
        ["fetch/api/headers/headers-record.any.js"] = 13,
        ["fetch/api/headers/headers-structure.any.js"] = 8,

        ["fetch/api/response/response-consume-empty.any.js"] = 14,
        ["fetch/api/response/response-consume-stream.any.js"] = 15,
        ["fetch/api/response/response-error-from-stream.any.js"] = 14,
        ["fetch/api/response/response-error.any.js"] = 10,
        ["fetch/api/response/response-from-stream.any.js"] = 3,
        ["fetch/api/response/response-init-001.any.js"] = 9,
        ["fetch/api/response/response-init-002.any.js"] = 8,
        ["fetch/api/response/response-init-contenttype.any.js"] = 18,
        ["fetch/api/response/response-static-error.any.js"] = 2,
        ["fetch/api/response/response-static-json.any.js"] = 16,
        ["fetch/api/response/response-static-redirect.any.js"] = 11,
        ["fetch/api/response/response-stream-bad-chunk.any.js"] = 6,
        ["fetch/api/response/response-stream-disturbed-1.any.js"] = 12,
        ["fetch/api/response/response-stream-disturbed-2.any.js"] = 12,
        ["fetch/api/response/response-stream-disturbed-3.any.js"] = 12,
        ["fetch/api/response/response-stream-disturbed-4.any.js"] = 12,
        ["fetch/api/response/response-stream-disturbed-5.any.js"] = 12,
        ["fetch/api/response/response-stream-disturbed-6.any.js"] = 5,
        ["fetch/api/response/response-stream-disturbed-by-pipe.any.js"] = 2,
        ["fetch/api/response/response-stream-with-broken-then.any.js"] = 6,
    };

    /// <summary>
    /// The tests that do not pass, each with the category it belongs to. See <see cref="WptDivergence"/> for
    /// what the categories mean and which of them are debt.
    /// </summary>
    /// <remarks>
    /// An entry must match at least one failing test and no passing one — see <see cref="RunSuiteFile"/>.
    /// That is what makes a glob safe to write: it cannot quietly widen to cover a case that works, so
    /// <c>* =&gt; windows-1252</c> below says exactly "every label of windows-1252 fails" and stops saying it
    /// the moment one of them stops.
    /// </remarks>
    /// <summary>The one platform any exclusion is scoped to today; see <c>WptExclusion.Platform</c>.</summary>
    private static readonly System.Runtime.InteropServices.OSPlatform MacOs = System.Runtime.InteropServices.OSPlatform.OSX;

    private static readonly WptExclusion[] _exclusions =
    [
        new("encoding/textdecoder-eof.any.js", "TextDecoder end-of-queue handling", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-eof.any.js", "TextDecoder end-of-queue handling using stream: true", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-labels.any.js", "big5 => Big5", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-labels.any.js", "big5-hkscs => Big5", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-labels.any.js", "chinese => GBK", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-labels.any.js", "cn-big5 => Big5", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-labels.any.js", "csbig5 => Big5", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-labels.any.js", "cseuckr => EUC-KR", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-labels.any.js", "cseucpkdfmtjapanese => EUC-JP", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-labels.any.js", "csgb2312 => GBK", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-labels.any.js", "csiso2022jp => ISO-2022-JP", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-labels.any.js", "csiso58gb231280 => GBK", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-labels.any.js", "csksc56011987 => EUC-KR", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-labels.any.js", "csshiftjis => Shift_JIS", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-labels.any.js", "euc-jp => EUC-JP", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-labels.any.js", "euc-kr => EUC-KR", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-labels.any.js", "gb18030 => gb18030", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-labels.any.js", "gb2312 => GBK", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-labels.any.js", "gb_2312 => GBK", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-labels.any.js", "gb_2312-80 => GBK", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-labels.any.js", "gbk => GBK", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-labels.any.js", "iso-2022-jp => ISO-2022-JP", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-labels.any.js", "iso-ir-149 => EUC-KR", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-labels.any.js", "iso-ir-58 => GBK", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-labels.any.js", "korean => EUC-KR", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-labels.any.js", "ks_c_5601-1987 => EUC-KR", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-labels.any.js", "ks_c_5601-1989 => EUC-KR", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-labels.any.js", "ksc5601 => EUC-KR", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-labels.any.js", "ksc_5601 => EUC-KR", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-labels.any.js", "ms932 => Shift_JIS", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-labels.any.js", "ms_kanji => Shift_JIS", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-labels.any.js", "shift-jis => Shift_JIS", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-labels.any.js", "shift_jis => Shift_JIS", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-labels.any.js", "sjis => Shift_JIS", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-labels.any.js", "windows-31j => Shift_JIS", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-labels.any.js", "windows-949 => EUC-KR", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-labels.any.js", "x-euc-jp => EUC-JP", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-labels.any.js", "x-gbk => GBK", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-labels.any.js", "x-sjis => Shift_JIS", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-labels.any.js", "x-x-big5 => Big5", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-mistakes.any.js", "Concatenating two ISO-2022-JP outputs is not always valid", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-mistakes.any.js", "Most legacy multi-byte encodings are ASCII supersets: big5", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-mistakes.any.js", "Most legacy multi-byte encodings are ASCII supersets: euc-jp", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-mistakes.any.js", "Most legacy multi-byte encodings are ASCII supersets: euc-kr", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-mistakes.any.js", "Most legacy multi-byte encodings are ASCII supersets: gb18030", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-mistakes.any.js", "Most legacy multi-byte encodings are ASCII supersets: gbk", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-mistakes.any.js", "Most legacy multi-byte encodings are ASCII supersets: shift_jis", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-mistakes.any.js", "Replacement, push back ASCII characters: big5", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-mistakes.any.js", "Replacement, push back ASCII characters: euc-jp", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-mistakes.any.js", "Replacement, push back ASCII characters: euc-kr", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-mistakes.any.js", "Replacement, push back ASCII characters: gb18030", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-mistakes.any.js", "Replacement, push back ASCII characters: gbk", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-mistakes.any.js", "Replacement, push back ASCII characters: iso-2022-jp", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-mistakes.any.js", "Replacement, push back ASCII characters: shift_jis", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-mistakes.any.js", "Sticky multibyte state: big5", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-mistakes.any.js", "Sticky multibyte state: euc-jp", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-mistakes.any.js", "Sticky multibyte state: euc-kr", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-mistakes.any.js", "Sticky multibyte state: gb18030", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-mistakes.any.js", "Sticky multibyte state: gbk", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-mistakes.any.js", "Sticky multibyte state: iso-2022-jp", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-mistakes.any.js", "Sticky multibyte state: shift_jis", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-mistakes.any.js", "WPT mislabels: euc-jp", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-mistakes.any.js", "WPT mislabels: iso-2022-jp", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-mistakes.any.js", "WPT mislabels: shift_jis", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-mistakes.any.js", "fatal stream: iso-2022-jp", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-mistakes.any.js", "gb18030 version and ranges", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-mistakes.any.js", "gbk decoder is gb18030 decoder", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-mistakes.any.js", "gbk version and ranges", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-mistakes.any.js", "specific: big5", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-mistakes.any.js", "stream: big5", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-mistakes.any.js", "stream: euc-jp", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-mistakes.any.js", "stream: euc-kr", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-mistakes.any.js", "stream: gb18030", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-mistakes.any.js", "stream: gbk", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-mistakes.any.js", "stream: iso-2022-jp", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-mistakes.any.js", "stream: shift_jis", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textencoder-constructor-non-utf.any.js", "Encoding argument supported for decode: Big5", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textencoder-constructor-non-utf.any.js", "Encoding argument supported for decode: EUC-JP", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textencoder-constructor-non-utf.any.js", "Encoding argument supported for decode: EUC-KR", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textencoder-constructor-non-utf.any.js", "Encoding argument supported for decode: GBK", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textencoder-constructor-non-utf.any.js", "Encoding argument supported for decode: ISO-2022-JP", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textencoder-constructor-non-utf.any.js", "Encoding argument supported for decode: Shift_JIS", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textencoder-constructor-non-utf.any.js", "Encoding argument supported for decode: gb18030", WptDivergence.NeedsLegacyMultiByteEncodings),

        // ---------------------------------------------------------------- encoding
        // common/sab.js takes its SharedArrayBuffer constructor from WebAssembly.Memory. Note that
        // "Invalid encodeInto() destination: SharedArrayBuffer" is *not* here: it asserts a TypeError, and
        // the helper throws one, so it passes for the wrong reason — which is why these two globs are narrow
        // rather than one over the word.
        new("encoding/encodeInto.any.js", "encodeInto() into SharedArrayBuffer *", WptDivergence.NeedsWebAssembly),
        new("encoding/encodeInto.any.js", "Invalid encodeInto() destination: *, backed by: SharedArrayBuffer", WptDivergence.NeedsWebAssembly),
        new("encoding/textdecoder-copy.any.js", "Modify buffer after passing it in (SharedArrayBuffer)", WptDivergence.NeedsWebAssembly),
        new("encoding/textdecoder-streaming.any.js", "*(SharedArrayBuffer)", WptDivergence.NeedsWebAssembly),

        // Everything below is one missing feature: the Encoding Standard's legacy single-byte and multi-byte
        // decoders, which EncodingLabels documents as out of scope and issue #3106 implements. Each of these
        // fails with "the encoding label provided ('…') is invalid" and nothing else.


        // Named one at a time rather than as "stream: *": the utf-8 row of that family passes, and an
        // exclusion is not allowed to cover a test that works.

        // The encoder half of every row here passes: a TextEncoder ignores its argument and is always utf-8.
        // Only "supported for decode" is excluded, and the utf-8, utf-16le and utf-16be rows of that half
        // pass too, which is why the 36 names are spelled out rather than globbed.

        // ---------------------------------------------------------------- WebCryptoAPI
        // Jint has no scheme and therefore no secure-context bit; the file's third test passes anyway.
        new("WebCryptoAPI/historical.any.js", "Non-secure context window does not have access to crypto.subtle", WptDivergence.NeedsSecureContextModel),
        new("WebCryptoAPI/historical.any.js", "Non-secure context window does not have access to CryptoKey", WptDivergence.NeedsSecureContextModel),

        // The nine typed-array rows of "Large length" used to sit here: each asks for the QuotaExceededError
        // *interface* (https://webidl.spec.whatwg.org/#quotaexceedederror) and used to get the name on a plain
        // DOMException. https://github.com/sebastienros/jint/issues/3189 implemented the interface, and
        // getRandomValues throws one with `quota` and `requested` both null — which is exactly what those rows
        // assert, because the algorithm says only "throw a QuotaExceededError" and names no numbers.

        // Nothing here excludes a "… during call" row for the ordering any more. Those families — across
        // digest, aes_cbc, aes_ctr, symmetric_importKey, ecdsa, hmac, rsa_pkcs, rsa_pss, rsa_oaep and
        // aes_gcm — were one defect, the caller's bytes copied before the algorithm was normalized rather
        // than after, and https://github.com/sebastienros/jint/issues/3179 moved each copy to the numbered
        // step that performs it. The "… during call" globs that remain further down are all in the other
        // category: the *platform* refuses the operation before the ordering could decide anything, so they
        // would fail whatever order the bytes were taken in.

        // The X25519 rows of a file that is otherwise about the `length` parameter of deriveBits.
        new("WebCryptoAPI/derive_bits_keys/derived_bits_length.https.any.js", "X25519 derivation with *", WptDivergence.NeedsCurve25519),

        // Compressed EC points, which the corpus itself treats as optional: these are recorded
        // PRECONDITION_FAILED rather than FAIL. The glob is the ", compressed)" the suite writes into the
        // parameter string; the uncompressed rows of every one of these keys pass.
        new("WebCryptoAPI/import_export/ec_importKey.https.any.js", "*, compressed), *", WptDivergence.NeedsCompressedEcPointImport),

        // AES-GCM's 32- and 64-bit tags. Six globs per tag rather than one "…-bit tag*", because the
        // "decryption with transferred ciphertext during call" row of those two tags *passes*: the operation
        // was going to throw anyway, and that is what the test asks for.
        new("WebCryptoAPI/encrypt_decrypt/aes_gcm.https.any.js", "AES-GCM *, 32-bit tag, 96-bit iv", WptDivergence.NeedsPlatformCryptoParameters),
        new("WebCryptoAPI/encrypt_decrypt/aes_gcm.https.any.js", "AES-GCM *, 32-bit tag, 96-bit iv decryption", WptDivergence.NeedsPlatformCryptoParameters),
        new("WebCryptoAPI/encrypt_decrypt/aes_gcm.https.any.js", "AES-GCM *, 32-bit tag, 96-bit iv decryption with * after call", WptDivergence.NeedsPlatformCryptoParameters),
        new("WebCryptoAPI/encrypt_decrypt/aes_gcm.https.any.js", "AES-GCM *, 32-bit tag, 96-bit iv decryption with altered ciphertext during call", WptDivergence.NeedsPlatformCryptoParameters),
        new("WebCryptoAPI/encrypt_decrypt/aes_gcm.https.any.js", "AES-GCM *, 32-bit tag, 96-bit iv with * after call", WptDivergence.NeedsPlatformCryptoParameters),
        new("WebCryptoAPI/encrypt_decrypt/aes_gcm.https.any.js", "AES-GCM *, 32-bit tag, 96-bit iv with * during call", WptDivergence.NeedsPlatformCryptoParameters),
        new("WebCryptoAPI/encrypt_decrypt/aes_gcm.https.any.js", "AES-GCM *, 64-bit tag, 96-bit iv", WptDivergence.NeedsPlatformCryptoParameters),
        new("WebCryptoAPI/encrypt_decrypt/aes_gcm.https.any.js", "AES-GCM *, 64-bit tag, 96-bit iv decryption", WptDivergence.NeedsPlatformCryptoParameters),
        new("WebCryptoAPI/encrypt_decrypt/aes_gcm.https.any.js", "AES-GCM *, 64-bit tag, 96-bit iv decryption with * after call", WptDivergence.NeedsPlatformCryptoParameters),
        new("WebCryptoAPI/encrypt_decrypt/aes_gcm.https.any.js", "AES-GCM *, 64-bit tag, 96-bit iv decryption with altered ciphertext during call", WptDivergence.NeedsPlatformCryptoParameters),
        new("WebCryptoAPI/encrypt_decrypt/aes_gcm.https.any.js", "AES-GCM *, 64-bit tag, 96-bit iv with * after call", WptDivergence.NeedsPlatformCryptoParameters),
        new("WebCryptoAPI/encrypt_decrypt/aes_gcm.https.any.js", "AES-GCM *, 64-bit tag, 96-bit iv with * during call", WptDivergence.NeedsPlatformCryptoParameters),

        // The four tag lengths between 96 and 120 bits work on Windows (CNG) and Linux (OpenSSL), whose
        // AesGcm.TagByteSizes is 12 to 16 — and not on macOS, where Apple's implementation answers 16 to 16
        // and the engine's ask-the-platform gate refuses everything shorter. These rows therefore PASS on two
        // legs and FAIL on the third, which no platform-neutral entry can say; they are scoped to macOS, where
        // the staleness rule still holds them to matching real failures.
        //
        // Each tag length gets the same six globs the 32- and 64-bit block above gets, and for the same
        // reason: AesGcmAlgorithm resolves the tag length before it looks at anything else, so on macOS the
        // refusal comes first and every row that wanted a result fails, whatever the rest of the request
        // said. That includes the "… during call" rows, which is why they are *here* rather than under
        // NeedsTriage — the copy order was never what decided them on this platform, and it decides nothing
        // anywhere now that issue #3179 has moved the copy to its numbered step. One row of the family is
        // deliberately in no entry, "decryption with transferred ciphertext during call": it asserts an
        // OperationError and the tag refusal is one, so it passes here for a reason it cannot see, exactly as
        // it does for the 32- and 64-bit tags.
        new("WebCryptoAPI/encrypt_decrypt/aes_gcm.https.any.js", "AES-GCM *, 96-bit tag, 96-bit iv", WptDivergence.NeedsPlatformCryptoParameters, MacOs),
        new("WebCryptoAPI/encrypt_decrypt/aes_gcm.https.any.js", "AES-GCM *, 96-bit tag, 96-bit iv decryption", WptDivergence.NeedsPlatformCryptoParameters, MacOs),
        new("WebCryptoAPI/encrypt_decrypt/aes_gcm.https.any.js", "AES-GCM *, 96-bit tag, 96-bit iv decryption with * after call", WptDivergence.NeedsPlatformCryptoParameters, MacOs),
        new("WebCryptoAPI/encrypt_decrypt/aes_gcm.https.any.js", "AES-GCM *, 96-bit tag, 96-bit iv decryption with altered ciphertext during call", WptDivergence.NeedsPlatformCryptoParameters, MacOs),
        new("WebCryptoAPI/encrypt_decrypt/aes_gcm.https.any.js", "AES-GCM *, 96-bit tag, 96-bit iv with * after call", WptDivergence.NeedsPlatformCryptoParameters, MacOs),
        new("WebCryptoAPI/encrypt_decrypt/aes_gcm.https.any.js", "AES-GCM *, 96-bit tag, 96-bit iv with * during call", WptDivergence.NeedsPlatformCryptoParameters, MacOs),
        new("WebCryptoAPI/encrypt_decrypt/aes_gcm.https.any.js", "AES-GCM *, 104-bit tag, 96-bit iv", WptDivergence.NeedsPlatformCryptoParameters, MacOs),
        new("WebCryptoAPI/encrypt_decrypt/aes_gcm.https.any.js", "AES-GCM *, 104-bit tag, 96-bit iv decryption", WptDivergence.NeedsPlatformCryptoParameters, MacOs),
        new("WebCryptoAPI/encrypt_decrypt/aes_gcm.https.any.js", "AES-GCM *, 104-bit tag, 96-bit iv decryption with * after call", WptDivergence.NeedsPlatformCryptoParameters, MacOs),
        new("WebCryptoAPI/encrypt_decrypt/aes_gcm.https.any.js", "AES-GCM *, 104-bit tag, 96-bit iv decryption with altered ciphertext during call", WptDivergence.NeedsPlatformCryptoParameters, MacOs),
        new("WebCryptoAPI/encrypt_decrypt/aes_gcm.https.any.js", "AES-GCM *, 104-bit tag, 96-bit iv with * after call", WptDivergence.NeedsPlatformCryptoParameters, MacOs),
        new("WebCryptoAPI/encrypt_decrypt/aes_gcm.https.any.js", "AES-GCM *, 104-bit tag, 96-bit iv with * during call", WptDivergence.NeedsPlatformCryptoParameters, MacOs),
        new("WebCryptoAPI/encrypt_decrypt/aes_gcm.https.any.js", "AES-GCM *, 112-bit tag, 96-bit iv", WptDivergence.NeedsPlatformCryptoParameters, MacOs),
        new("WebCryptoAPI/encrypt_decrypt/aes_gcm.https.any.js", "AES-GCM *, 112-bit tag, 96-bit iv decryption", WptDivergence.NeedsPlatformCryptoParameters, MacOs),
        new("WebCryptoAPI/encrypt_decrypt/aes_gcm.https.any.js", "AES-GCM *, 112-bit tag, 96-bit iv decryption with * after call", WptDivergence.NeedsPlatformCryptoParameters, MacOs),
        new("WebCryptoAPI/encrypt_decrypt/aes_gcm.https.any.js", "AES-GCM *, 112-bit tag, 96-bit iv decryption with altered ciphertext during call", WptDivergence.NeedsPlatformCryptoParameters, MacOs),
        new("WebCryptoAPI/encrypt_decrypt/aes_gcm.https.any.js", "AES-GCM *, 112-bit tag, 96-bit iv with * after call", WptDivergence.NeedsPlatformCryptoParameters, MacOs),
        new("WebCryptoAPI/encrypt_decrypt/aes_gcm.https.any.js", "AES-GCM *, 112-bit tag, 96-bit iv with * during call", WptDivergence.NeedsPlatformCryptoParameters, MacOs),
        new("WebCryptoAPI/encrypt_decrypt/aes_gcm.https.any.js", "AES-GCM *, 120-bit tag, 96-bit iv", WptDivergence.NeedsPlatformCryptoParameters, MacOs),
        new("WebCryptoAPI/encrypt_decrypt/aes_gcm.https.any.js", "AES-GCM *, 120-bit tag, 96-bit iv decryption", WptDivergence.NeedsPlatformCryptoParameters, MacOs),
        new("WebCryptoAPI/encrypt_decrypt/aes_gcm.https.any.js", "AES-GCM *, 120-bit tag, 96-bit iv decryption with * after call", WptDivergence.NeedsPlatformCryptoParameters, MacOs),
        new("WebCryptoAPI/encrypt_decrypt/aes_gcm.https.any.js", "AES-GCM *, 120-bit tag, 96-bit iv decryption with altered ciphertext during call", WptDivergence.NeedsPlatformCryptoParameters, MacOs),
        new("WebCryptoAPI/encrypt_decrypt/aes_gcm.https.any.js", "AES-GCM *, 120-bit tag, 96-bit iv with * after call", WptDivergence.NeedsPlatformCryptoParameters, MacOs),
        new("WebCryptoAPI/encrypt_decrypt/aes_gcm.https.any.js", "AES-GCM *, 120-bit tag, 96-bit iv with * during call", WptDivergence.NeedsPlatformCryptoParameters, MacOs),

        // The 128-bit tag has no entry of any kind: it is the one length every platform's AES-GCM takes, so
        // its rows pass on all three legs — including the four "… during call" ones, which were the last
        // NeedsTriage entries this file carried.

        // The whole of the 256-bit-iv file bar the rows that expect a throw for another reason — the
        // usage-matrix rows, the illegal-tag-length rows, and again "decryption with transferred ciphertext
        // during call", all of which pass.
        new("WebCryptoAPI/encrypt_decrypt/aes_gcm_256_iv.https.any.js", "AES-GCM *, 256-bit iv", WptDivergence.NeedsPlatformCryptoParameters),
        new("WebCryptoAPI/encrypt_decrypt/aes_gcm_256_iv.https.any.js", "AES-GCM *, 256-bit iv decryption", WptDivergence.NeedsPlatformCryptoParameters),
        new("WebCryptoAPI/encrypt_decrypt/aes_gcm_256_iv.https.any.js", "AES-GCM *, 256-bit iv decryption with * after call", WptDivergence.NeedsPlatformCryptoParameters),
        new("WebCryptoAPI/encrypt_decrypt/aes_gcm_256_iv.https.any.js", "AES-GCM *, 256-bit iv decryption with altered ciphertext during call", WptDivergence.NeedsPlatformCryptoParameters),
        new("WebCryptoAPI/encrypt_decrypt/aes_gcm_256_iv.https.any.js", "AES-GCM *, 256-bit iv with * after call", WptDivergence.NeedsPlatformCryptoParameters),
        new("WebCryptoAPI/encrypt_decrypt/aes_gcm_256_iv.https.any.js", "AES-GCM *, 256-bit iv with * during call", WptDivergence.NeedsPlatformCryptoParameters),

        // RSA-OAEP's label, in the same six-glob shape as an unsupported AES-GCM tag length and for the same
        // reason: RsaAlgorithm refuses a present and non-empty label outright, so every "a label" row that
        // wanted a result fails whatever else the request said. The "no label" and "empty label" rows carry
        // no entry at all — they used to, for the copy order alone, and issue #3179 fixed that.
        new("WebCryptoAPI/encrypt_decrypt/rsa_oaep.https.any.js", "RSA-OAEP with SHA-* and a label", WptDivergence.NeedsPlatformCryptoParameters),
        new("WebCryptoAPI/encrypt_decrypt/rsa_oaep.https.any.js", "RSA-OAEP with SHA-* and a label decryption", WptDivergence.NeedsPlatformCryptoParameters),
        new("WebCryptoAPI/encrypt_decrypt/rsa_oaep.https.any.js", "RSA-OAEP with SHA-* and a label decryption with * after call", WptDivergence.NeedsPlatformCryptoParameters),
        new("WebCryptoAPI/encrypt_decrypt/rsa_oaep.https.any.js", "RSA-OAEP with SHA-* and a label decryption with altered ciphertext during call", WptDivergence.NeedsPlatformCryptoParameters),
        new("WebCryptoAPI/encrypt_decrypt/rsa_oaep.https.any.js", "RSA-OAEP with SHA-* and a label with * after call", WptDivergence.NeedsPlatformCryptoParameters),
        new("WebCryptoAPI/encrypt_decrypt/rsa_oaep.https.any.js", "RSA-OAEP with SHA-* and a label with * during call", WptDivergence.NeedsPlatformCryptoParameters),

        // RSA-PSS's salt length. "no salt" is saltLength 0, which .NET cannot ask for, so every one of those
        // rows fails — except SHA-256's "wrong saltLength", whose wrong length happens to be SHA-256's own
        // and is therefore the one .NET does accept. The ", salted" rows now fail only where the *wrong* salt
        // length is asked for: their copy-order family went with issue #3179.
        new("WebCryptoAPI/sign_verify/rsa_pss.https.any.js", "RSA-PSS with SHA-* and no salt round trip", WptDivergence.NeedsPlatformCryptoParameters),
        new("WebCryptoAPI/sign_verify/rsa_pss.https.any.js", "RSA-PSS with SHA-* and no salt verification", WptDivergence.NeedsPlatformCryptoParameters),
        new("WebCryptoAPI/sign_verify/rsa_pss.https.any.js", "RSA-PSS with SHA-* and no salt verification failure with altered *", WptDivergence.NeedsPlatformCryptoParameters),
        new("WebCryptoAPI/sign_verify/rsa_pss.https.any.js", "RSA-PSS with SHA-1 and no salt verification failure with wrong saltLength", WptDivergence.NeedsPlatformCryptoParameters),
        new("WebCryptoAPI/sign_verify/rsa_pss.https.any.js", "RSA-PSS with SHA-384 and no salt verification failure with wrong saltLength", WptDivergence.NeedsPlatformCryptoParameters),
        new("WebCryptoAPI/sign_verify/rsa_pss.https.any.js", "RSA-PSS with SHA-512 and no salt verification failure with wrong saltLength", WptDivergence.NeedsPlatformCryptoParameters),
        new("WebCryptoAPI/sign_verify/rsa_pss.https.any.js", "RSA-PSS with SHA-* and no salt verification with * call", WptDivergence.NeedsPlatformCryptoParameters),
        new("WebCryptoAPI/sign_verify/rsa_pss.https.any.js", "RSA-PSS with SHA-* and no salt with * call", WptDivergence.NeedsPlatformCryptoParameters),
        new("WebCryptoAPI/sign_verify/rsa_pss.https.any.js", "RSA-PSS with SHA-*, salted verification failure with wrong saltLength", WptDivergence.NeedsPlatformCryptoParameters),

        // wrapKey/unwrapKey, where the two platform limits above meet: the corpus wraps under AES-GCM with a
        // 128-bit iv and under RSA-OAEP with a label, so those two wrapping algorithms fail outright. The
        // four narrower globs are the rows that wrap an AES-GCM or RSA-OAEP *key* under something else and
        // then compare two non-extractable keys by using them, which reaches the same two limits.
        new("WebCryptoAPI/wrapKey_unwrapKey/wrapKey_unwrapKey.https.any.js", "* and AES-GCM", WptDivergence.NeedsPlatformCryptoParameters),
        new("WebCryptoAPI/wrapKey_unwrapKey/wrapKey_unwrapKey.https.any.js", "* and RSA-OAEP", WptDivergence.NeedsPlatformCryptoParameters),
        new("WebCryptoAPI/wrapKey_unwrapKey/wrapKey_unwrapKey.https.any.js", "Can wrap and unwrap AES-GCM keys as non-extractable using *", WptDivergence.NeedsPlatformCryptoParameters),
        new("WebCryptoAPI/wrapKey_unwrapKey/wrapKey_unwrapKey.https.any.js", "Can unwrap AES-GCM non-extractable keys using *", WptDivergence.NeedsPlatformCryptoParameters),
        new("WebCryptoAPI/wrapKey_unwrapKey/wrapKey_unwrapKey.https.any.js", "Can wrap and unwrap RSA-OAEP private key keys as non-extractable using *", WptDivergence.NeedsPlatformCryptoParameters),
        new("WebCryptoAPI/wrapKey_unwrapKey/wrapKey_unwrapKey.https.any.js", "Can unwrap RSA-OAEP private key non-extractable keys using *", WptDivergence.NeedsPlatformCryptoParameters),

        // The KeyUsage values ML-KEM proposes, which the enumeration does not have. One glob per file: the
        // suites build the name out of the usage list, and "apsulate" is the substring the four values share.
        new("WebCryptoAPI/generateKey/failures_AES-CBC.https.any.js", "Bad usages: *apsulate*", WptDivergence.NeedsKeyEncapsulation),
        new("WebCryptoAPI/generateKey/failures_AES-CTR.https.any.js", "Bad usages: *apsulate*", WptDivergence.NeedsKeyEncapsulation),
        new("WebCryptoAPI/generateKey/failures_AES-GCM.https.any.js", "Bad usages: *apsulate*", WptDivergence.NeedsKeyEncapsulation),
        new("WebCryptoAPI/generateKey/failures_AES-KW.https.any.js", "Bad usages: *apsulate*", WptDivergence.NeedsKeyEncapsulation),
        new("WebCryptoAPI/generateKey/failures_ECDH.https.any.js", "Bad usages: *apsulate*", WptDivergence.NeedsKeyEncapsulation),
        new("WebCryptoAPI/generateKey/failures_ECDSA.https.any.js", "Bad usages: *apsulate*", WptDivergence.NeedsKeyEncapsulation),
        new("WebCryptoAPI/generateKey/failures_HMAC.https.any.js", "Bad usages: *apsulate*", WptDivergence.NeedsKeyEncapsulation),
        new("WebCryptoAPI/generateKey/failures_RSA-OAEP.https.any.js", "Bad usages: *apsulate*", WptDivergence.NeedsKeyEncapsulation),
        new("WebCryptoAPI/generateKey/failures_RSA-PSS.https.any.js", "Bad usages: *apsulate*", WptDivergence.NeedsKeyEncapsulation),
        new("WebCryptoAPI/generateKey/failures_RSASSA-PKCS1-v1_5.https.any.js", "Bad usages: *apsulate*", WptDivergence.NeedsKeyEncapsulation),
        new("WebCryptoAPI/import_export/ec_importKey_failures_ECDH.https.any.js", "Bad usages: *apsulate*", WptDivergence.NeedsKeyEncapsulation),
        new("WebCryptoAPI/import_export/ec_importKey_failures_ECDSA.https.any.js", "Bad usages: *apsulate*", WptDivergence.NeedsKeyEncapsulation),

        // ---------------------------------------------------------------- streams
        // Seven rows of one file, each reaching for the `ReadableStreamDefaultReader` global. Named one at a
        // time rather than globbed: the file's other 22 rows obtain the same interface as
        // `stream.getReader().constructor` and pass, and two of these seven do reach an assertion — they fail
        // it with "expected TypeError but got object ReferenceError" rather than with a bare ReferenceError,
        // which is what a glob over the name would have hidden.
        new("streams/readable-streams/default-reader.any.js",
            "ReadableStreamDefaultReader constructor should get a ReadableStream object as argument", WptDivergence.NeedsStreamInterfaceGlobals),
        new("streams/readable-streams/default-reader.any.js",
            "ReadableStreamDefaultReader closed should always return the same promise object", WptDivergence.NeedsStreamInterfaceGlobals),
        new("streams/readable-streams/default-reader.any.js",
            "Constructing a ReadableStreamDefaultReader directly should fail if the stream is already locked (via direct construction)", WptDivergence.NeedsStreamInterfaceGlobals),
        new("streams/readable-streams/default-reader.any.js",
            "Getting a ReadableStreamDefaultReader via getReader should fail if the stream is already locked (via direct construction)", WptDivergence.NeedsStreamInterfaceGlobals),
        new("streams/readable-streams/default-reader.any.js",
            "Constructing a ReadableStreamDefaultReader directly should fail if the stream is already locked (via getReader)", WptDivergence.NeedsStreamInterfaceGlobals),
        new("streams/readable-streams/default-reader.any.js",
            "Constructing a ReadableStreamDefaultReader directly should be OK if the stream is closed", WptDivergence.NeedsStreamInterfaceGlobals),
        new("streams/readable-streams/default-reader.any.js",
            "Constructing a ReadableStreamDefaultReader directly should be OK if the stream is errored", WptDivergence.NeedsStreamInterfaceGlobals),

        // The whole file: it obtains a non-transferable ArrayBuffer from WebAssembly.Memory, which is the only
        // way to get one, so every row fails in the fixture rather than in the code under test. The engine's
        // own refusal of a detached or non-transferable buffer is covered by bad-buffers-and-views.any.js and
        // enqueue-with-detached-buffer.any.js, which both pass.
        new("streams/readable-byte-streams/non-transferable-buffers.any.js", "*", WptDivergence.NeedsWebAssembly),

        // ---------------------------------------------------------------- compression
        // The SharedArrayBuffer rows of the two bad-chunk files, which take their SAB constructor from
        // WebAssembly.Memory through their own inline helper rather than through common/sab.js. One glob per
        // chunk type covers all four formats, which it could not do while brotli's rows of the same families
        // failed for a different reason — https://github.com/sebastienros/jint/issues/3210 removed that
        // reason, so the entry can now say the one thing that is actually true of every row it names.
        new("compression/compression-bad-chunks.any.js", "chunk of type SharedArrayBuffer should error the stream for *", WptDivergence.NeedsWebAssembly),
        new("compression/compression-bad-chunks.any.js", "chunk of type shared Uint8Array should error the stream for *", WptDivergence.NeedsWebAssembly),
        new("compression/decompression-bad-chunks.any.js", "chunk of type SharedArrayBuffer should error the stream for *", WptDivergence.NeedsWebAssembly),
        new("compression/decompression-bad-chunks.any.js", "chunk of type shared Uint8Array should error the stream for *", WptDivergence.NeedsWebAssembly),

        // The two lenient-decompression divergences DecompressionCodec documents, and the only six rows in
        // the whole corpus that assert them: a member that ends early and a member with something after it.
        // Globbed over the format now, because all three of the formats this file carries — deflate, gzip and
        // brotli, the fourth value deflate-raw not being one of them — diverge in exactly the same way and for
        // exactly the same reason: BrotliStream reports how many bytes it consumed no more than the deflate
        // family does, so the last byte of a brotli stream can be dropped, or a junk byte appended, and the
        // decoder still hands over the payload rather than erroring. The unchanged-input row of all three
        // passes, which is what the driver's no-passing-test rule holds these two globs to.
        new("compression/decompression-corrupt-input.any.js", "truncating the input for * should give an error", WptDivergence.NeedsIncrementalInflater),
        new("compression/decompression-corrupt-input.any.js", "trailing junk for * should give an error", WptDivergence.NeedsIncrementalInflater),

        // ---------------------------------------------------------------- FileAPI
        // Nothing: the corpus is 342-for-342. Blob.textStream() arrived with sebastienros/jint#3211, and the
        // three Blob-constructor.any.js rows that sat here under NeedsTriage were one engine defect rather
        // than three Blob ones — Array.prototype.values/keys/entries gated on ObjectInstance.IsArrayLike
        // where https://tc39.es/ecma262/#sec-array.prototype.values reads no `length` at all
        // (sebastienros/jint#3209). Vendor/README.md keeps both accounts.

        // ---------------------------------------------------------------- workers
        // Six rows, five of them one decision family and none of them a surprise: the worker global is the
        // global the engine already builds plus the worker names, and these are the names it deliberately does
        // not add. Each is a numbered divergence in the design — see WptDivergence.NeedsDeclinedWorkerGlobals
        // for the three citations. That is the whole of what this corpus found to be missing, which is the
        // useful figure: everything else it asks of a worker global passes.
        new("workers/Worker-replace-self.any.js",
            "Test that self is not replaceable.", WptDivergence.NeedsDeclinedWorkerGlobals),
        new("workers/interfaces/WorkerGlobalScope/self.any.js",
            "self instanceof WorkerGlobalScope", WptDivergence.NeedsDeclinedWorkerGlobals),
        new("workers/Worker-constructor-proto.any.js",
            "Tests that setting the proto of a built in constructor is not reset.", WptDivergence.NeedsDeclinedWorkerGlobals),
        new("workers/WorkerNavigator.any.js",
            "Testing Navigator properties on workers.", WptDivergence.NeedsDeclinedWorkerGlobals),
        new("workers/WorkerNavigator-hardware-concurrency.any.js",
            "Test worker navigator hardware concurrency.", WptDivergence.NeedsDeclinedWorkerGlobals),

        // Nesting is off by default, so `Worker` is undefined inside a worker — a grant withheld rather than a
        // name declined, which is why it is its own category. The file's other row asserts that SharedWorker is
        // absent outside a window and passes.
        new("workers/semantics/multiple-workers/exposure.any.js", "Worker exposure", WptDivergence.NeedsWorkerNesting),

        // The one genuine defect this corpus found, and it is not in the worker code: `self` is installed once
        // for every global as a writable data property, against Window's [Replaceable] definition, and
        // WorkerGlobalScope's is read-only. Recorded rather than fixed — the install predates Worker and is
        // shared with the top-level lane, so moving it is not a change this corpus gets to make. See
        // WptDivergence.NeedsTriage.
        new("workers/interfaces/WorkerGlobalScope/self.any.js", "self = 1", WptDivergence.NeedsTriage),

        // ---------------------------------------------------------------- encoding, the XMLHttpRequest half
        // The file runs each single-byte decoder twice: once through TextDecoder over a locally built
        // Uint8Array, and once through XMLHttpRequest over resources/single-byte-raw.py, a wptserve handler
        // that answers with the bytes 0x00..0xFE labelled with the charset from its query string. The
        // TextDecoder half — the same 168 labels, the same expectations — passes.
        new("encoding/single-byte-decoder.any.js", "*(XMLHttpRequest)", WptDivergence.NeedsWptServer),

        // ---------------------------------------------------------------- hr-time and user-timing
        new("hr-time/basic.any.js", "Performance interface extends EventTarget.", WptDivergence.NeedsPerformanceEventTarget),

        new("user-timing/buffered-flag.any.js", "PerformanceObserver with buffered flag sees previous marks", WptDivergence.NeedsPerformanceObserver),
        new("user-timing/buffered-flag.any.js", "PerformanceObserver with buffered flag sees previous measures", WptDivergence.NeedsPerformanceObserver),
        new("user-timing/case-sensitivity.any.js", "getEntriesByType values are case sensitive", WptDivergence.NeedsPerformanceObserver),
        new("user-timing/mark-l3.any.js", "mark entries' detail and startTime are customizable.", WptDivergence.NeedsPerformanceObserver),
        new("user-timing/measure-with-dict.any.js", "measure entries' detail and start/end are customizable", WptDivergence.NeedsPerformanceObserver),

        // A defect, and a narrow one: the file runs each case twice, once through `performance.mark(name, x)`
        // and once through `new PerformanceMark(name, x)`, and only the constructor accepts a non-object where
        // WebIDL's dictionary conversion refuses one. The `[performance.mark]` half of all five rows passes,
        // and so does the constructor's own `{startTime: -1}` row, so what is missing is exactly the
        // "not an object and not null/undefined is a TypeError" step. See Vendor/README.md.
        new("user-timing/mark-errors.any.js", "[new PerformanceMark]: Number should be rejected as the mark-options.", WptDivergence.NeedsTriage),
        new("user-timing/mark-errors.any.js", "[new PerformanceMark]: NaN should be rejected as the mark-options.", WptDivergence.NeedsTriage),
        new("user-timing/mark-errors.any.js", "[new PerformanceMark]: Infinity should be rejected as the mark-options.", WptDivergence.NeedsTriage),
        new("user-timing/mark-errors.any.js", "[new PerformanceMark]: String should be rejected as the mark-options.", WptDivergence.NeedsTriage),

        // ---------------------------------------------------------------- dom
        // Event's two legacy members. `returnValue` is https://dom.spec.whatwg.org/#dom-event-returnvalue and
        // `isTrusted` is [LegacyUnforgeable], so it must be an *own* property of every event rather than an
        // accessor on the prototype. Both are defects rather than declines; Vendor/README.md analyses them,
        // and Event-constructors.any.js is not vendored because its own two failures are the same pair and it
        // registers every one of its tests without a name.
        new("dom/events/AddEventListenerOptions-passive.any.js",
            "returnValue should be ignored if-and-only-if the passive option is true", WptDivergence.NeedsTriage),
        new("dom/events/Event-isTrusted.any.js", "undefined", WptDivergence.NeedsTriage),

        // ---------------------------------------------------------------- fetch
        // The forbidden-header-name lists, which HeadersGuard documents declining. The 18 "is allowed to use"
        // rows of the same file pass, which is what keeps the glob honest.
        new("fetch/api/headers/headers-forbidden-override.any.js", "header * is forbidden to use value *", WptDivergence.NeedsForbiddenHeaderNames),
        new("fetch/api/headers/header-setcookie.any.js", "Set-Cookie is a forbidden response header", WptDivergence.NeedsForbiddenHeaderNames),

        // WebIDL gives an iterator prototype object's `next` { writable, enumerable, configurable }; Jint's is
        // non-enumerable. The same defect the streams corpus filed against the async iterator prototype.
        new("fetch/api/headers/headers-basic.any.js", "Check keys method", WptDivergence.NeedsTriage),
        new("fetch/api/headers/headers-basic.any.js", "Check values method", WptDivergence.NeedsTriage),
        new("fetch/api/headers/headers-basic.any.js", "Check entries method", WptDivergence.NeedsTriage),

        // Two rows counting the operations a record<> conversion performs; Jint does one more than the
        // specification's order allows. See Vendor/README.md.
        new("fetch/api/headers/headers-record.any.js",
            "Correct operation ordering with two properties one of which has an invalid name", WptDivergence.NeedsTriage),
        new("fetch/api/headers/headers-record.any.js", "Basic operation with Symbol keys", WptDivergence.NeedsTriage),

        new("fetch/api/response/response-consume-stream.any.js", "Getting a redirect Response stream", WptDivergence.NeedsApiBaseUrl),

        // Eight of the twelve rows of a file whose other four pass: after a Response whose body came from
        // *bytes* — a string, or the loader's answer — has been consumed, `response.body.getReader()` must
        // throw because the body is disturbed, and here it does not. The four rows whose body source is a
        // ReadableStream the test built itself pass, which is what locates the defect. See Vendor/README.md.
        new("fetch/api/response/response-stream-disturbed-5.any.js", "* (body source: string)", WptDivergence.NeedsTriage),
        new("fetch/api/response/response-stream-disturbed-5.any.js", "* (body source: fetch)", WptDivergence.NeedsTriage),

        new("fetch/api/response/response-consume-empty.any.js",
            "Consume empty FormData response body as text", WptDivergence.NeedsTriage),

        // ---------------------------------------------------------------- structured clone
        new("html/webappapis/structured-clone/structured-clone.any.js", "Growable SharedArrayBuffer", WptDivergence.NeedsWebAssembly),
        new("html/webappapis/structured-clone/structured-clone.any.js", "ImageBitmap", WptDivergence.NeedsOffscreenCanvas),
        new("html/webappapis/structured-clone/structured-clone.any.js", "OffscreenCanvas", WptDivergence.NeedsOffscreenCanvas),

        // Three defects, analysed in Vendor/README.md: an Error's `cause` is not carried, Blob and File are
        // not serializable at all, and %Object.prototype% is refused where HTML clones it as an ordinary
        // object. The Blob rows are named as three globs because that is how the battery names them.
        new("html/webappapis/structured-clone/structured-clone.any.js", "Error object", WptDivergence.NeedsTriage),
        new("html/webappapis/structured-clone/structured-clone.any.js", "EvalError object", WptDivergence.NeedsTriage),
        new("html/webappapis/structured-clone/structured-clone.any.js", "RangeError object", WptDivergence.NeedsTriage),
        new("html/webappapis/structured-clone/structured-clone.any.js", "ReferenceError object", WptDivergence.NeedsTriage),
        new("html/webappapis/structured-clone/structured-clone.any.js", "SyntaxError object", WptDivergence.NeedsTriage),
        new("html/webappapis/structured-clone/structured-clone.any.js", "TypeError object", WptDivergence.NeedsTriage),
        new("html/webappapis/structured-clone/structured-clone.any.js", "URIError object", WptDivergence.NeedsTriage),
        new("html/webappapis/structured-clone/structured-clone.any.js", "Blob *", WptDivergence.NeedsTriage),
        new("html/webappapis/structured-clone/structured-clone.any.js", "Array Blob object, *", WptDivergence.NeedsTriage),
        new("html/webappapis/structured-clone/structured-clone.any.js", "Object Blob object, *", WptDivergence.NeedsTriage),
        new("html/webappapis/structured-clone/structured-clone.any.js", "File basic", WptDivergence.NeedsTriage),
        new("html/webappapis/structured-clone/structured-clone.any.js",
            "An object whose interface is deleted from the global must still deserialize", WptDivergence.NeedsTriage),
        new("html/webappapis/structured-clone/structured-clone.any.js",
            "A subclass instance will deserialize as its closest serializable superclass", WptDivergence.NeedsTriage),
        new("html/webappapis/structured-clone/structured-clone.any.js",
            "ObjectPrototype must lose its exotic-ness when cloned", WptDivergence.NeedsTriage),
    ];

    public static IEnumerable<object[]> UrlSuiteFiles() => Cases("url");

    public static IEnumerable<object[]> EncodingSuiteFiles() => Cases("encoding");

    /// <summary>
    /// Every suite of the WebCryptoAPI corpus, as (suite directory) pairs. A sub-directory is a suite of its
    /// own — <see cref="WptCorpus.TestFiles"/> lists a directory's own <c>.any.js</c> files and never
    /// descends — so the root files and each of the seven operation directories get their own theory.
    /// </summary>
    private static readonly string[] _webCryptoSuites =
    [
        "WebCryptoAPI",
        "WebCryptoAPI/derive_bits_keys",
        "WebCryptoAPI/digest",
        "WebCryptoAPI/encrypt_decrypt",
        "WebCryptoAPI/generateKey",
        "WebCryptoAPI/import_export",
        "WebCryptoAPI/sign_verify",
        "WebCryptoAPI/wrapKey_unwrapKey",
    ];

    public static IEnumerable<object[]> WebCryptoSuiteFiles() => Cases("WebCryptoAPI");

    public static IEnumerable<object[]> WebCryptoDeriveSuiteFiles() => Cases("WebCryptoAPI/derive_bits_keys");

    public static IEnumerable<object[]> WebCryptoDigestSuiteFiles() => Cases("WebCryptoAPI/digest");

    public static IEnumerable<object[]> WebCryptoEncryptSuiteFiles() => Cases("WebCryptoAPI/encrypt_decrypt");

    public static IEnumerable<object[]> WebCryptoGenerateKeySuiteFiles() => Cases("WebCryptoAPI/generateKey");

    public static IEnumerable<object[]> WebCryptoImportExportSuiteFiles() => Cases("WebCryptoAPI/import_export");

    public static IEnumerable<object[]> WebCryptoSignVerifySuiteFiles() => Cases("WebCryptoAPI/sign_verify");

    public static IEnumerable<object[]> WebCryptoWrapKeySuiteFiles() => Cases("WebCryptoAPI/wrapKey_unwrapKey");

    /// <summary>
    /// The Streams Standard's corpus, split the same way the WebCryptoAPI one is: the root files are a suite
    /// and each sub-directory is another, because <see cref="WptCorpus.TestFiles"/> lists a directory's own
    /// files and never descends. <c>transferable/</c> contributes exactly one file — the rest of that
    /// directory is a browsing context, see <see cref="_notVendored"/>.
    /// </summary>
    private static readonly string[] _streamsSuites =
    [
        "streams",
        "streams/readable-streams",
        "streams/readable-byte-streams",
        "streams/writable-streams",
        "streams/transform-streams",
        "streams/piping",
        "streams/transferable",
    ];

    public static IEnumerable<object[]> StreamsSuiteFiles() => Cases("streams");

    public static IEnumerable<object[]> ReadableStreamsSuiteFiles() => Cases("streams/readable-streams");

    public static IEnumerable<object[]> ReadableByteStreamsSuiteFiles() => Cases("streams/readable-byte-streams");

    public static IEnumerable<object[]> WritableStreamsSuiteFiles() => Cases("streams/writable-streams");

    public static IEnumerable<object[]> TransformStreamsSuiteFiles() => Cases("streams/transform-streams");

    public static IEnumerable<object[]> StreamsPipingSuiteFiles() => Cases("streams/piping");

    public static IEnumerable<object[]> TransferableStreamsSuiteFiles() => Cases("streams/transferable");

    /// <summary>
    /// The Compression Standard's corpus and the URL Pattern one, a single directory each.
    /// </summary>
    public static IEnumerable<object[]> CompressionSuiteFiles() => Cases("compression");

    public static IEnumerable<object[]> UrlPatternSuiteFiles() => Cases("urlpattern");

    /// <summary>
    /// The File API's three vendored suites: its two directories, plus the one root file that is about a
    /// <c>Blob</c> rather than about a <c>FileReader</c> — see <see cref="_notVendored"/> for the one that
    /// is not.
    /// </summary>
    private static readonly string[] _fileApiSuites =
    [
        "FileAPI",
        "FileAPI/blob",
        "FileAPI/file",
    ];

    public static IEnumerable<object[]> FileApiSuiteFiles() => Cases("FileAPI");

    public static IEnumerable<object[]> FileApiBlobSuiteFiles() => Cases("FileAPI/blob");

    public static IEnumerable<object[]> FileApiFileSuiteFiles() => Cases("FileAPI/file");

    /// <summary>
    /// The three vendored <c>workers/</c> directories. Every file in them runs <b>inside a real module
    /// worker</b> — see <see cref="WptHarness.RunsInAWorker"/> for the rule and <c>Vendor/README.md</c> for the
    /// twenty upstream files that cannot be reached at all.
    /// </summary>
    private static readonly string[] _workersSuites =
    [
        "workers",
        "workers/interfaces/WorkerGlobalScope",
        "workers/semantics/multiple-workers",
    ];

    public static IEnumerable<object[]> WorkersSuiteFiles() => Cases("workers");

    public static IEnumerable<object[]> WorkerGlobalScopeSuiteFiles() => Cases("workers/interfaces/WorkerGlobalScope");

    public static IEnumerable<object[]> MultipleWorkersSuiteFiles() => Cases("workers/semantics/multiple-workers");

    /// <summary>
    /// The HTML and DOM corpora, split the way the others are: one suite per directory, because
    /// <see cref="WptCorpus.TestFiles"/> lists a directory's own files and never descends.
    /// </summary>
    private static readonly string[] _htmlSuites =
    [
        "html/webappapis/timers",
        "html/webappapis/microtask-queuing",
        "html/webappapis/structured-clone",
    ];

    private static readonly string[] _domSuites =
    [
        "dom/events",
        "dom/abort",
    ];

    /// <summary>
    /// The network-free half of the Fetch corpus: the <c>Headers</c> suite and the <c>Response</c> files that
    /// build their bodies themselves. <c>fetch/api/request</c> is not among them — see
    /// <see cref="_notVendored"/> — because a <c>Request</c> needs a url and every file in it writes a
    /// relative one.
    /// </summary>
    private static readonly string[] _fetchSuites =
    [
        "fetch/api/headers",
        "fetch/api/response",
    ];

    public static IEnumerable<object[]> HrTimeSuiteFiles() => Cases("hr-time");

    public static IEnumerable<object[]> UserTimingSuiteFiles() => Cases("user-timing");

    public static IEnumerable<object[]> TimersSuiteFiles() => Cases("html/webappapis/timers");

    public static IEnumerable<object[]> MicrotaskQueuingSuiteFiles() => Cases("html/webappapis/microtask-queuing");

    public static IEnumerable<object[]> StructuredCloneSuiteFiles() => Cases("html/webappapis/structured-clone");

    public static IEnumerable<object[]> DomEventsSuiteFiles() => Cases("dom/events");

    public static IEnumerable<object[]> DomAbortSuiteFiles() => Cases("dom/abort");

    public static IEnumerable<object[]> FetchHeadersSuiteFiles() => Cases("fetch/api/headers");

    public static IEnumerable<object[]> FetchResponseSuiteFiles() => Cases("fetch/api/response");

    [Theory]
    [MemberData(nameof(UrlSuiteFiles))]
    public void RunsTheUrlSuite(string file) => RunSuiteFile(file);

    [Theory]
    [MemberData(nameof(EncodingSuiteFiles))]
    public void RunsTheEncodingSuite(string file) => RunSuiteFile(file);

    [Theory]
    [MemberData(nameof(WebCryptoSuiteFiles))]
    public void RunsTheWebCryptoSuite(string file) => RunSuiteFile(file);

    [Theory]
    [MemberData(nameof(WebCryptoDeriveSuiteFiles))]
    public void RunsTheWebCryptoDeriveSuite(string file) => RunSuiteFile(file);

    [Theory]
    [MemberData(nameof(WebCryptoDigestSuiteFiles))]
    public void RunsTheWebCryptoDigestSuite(string file) => RunSuiteFile(file);

    [Theory]
    [MemberData(nameof(WebCryptoEncryptSuiteFiles))]
    public void RunsTheWebCryptoEncryptDecryptSuite(string file) => RunSuiteFile(file);

    [Theory]
    [MemberData(nameof(WebCryptoGenerateKeySuiteFiles))]
    public void RunsTheWebCryptoGenerateKeySuite(string file) => RunSuiteFile(file);

    [Theory]
    [MemberData(nameof(WebCryptoImportExportSuiteFiles))]
    public void RunsTheWebCryptoImportExportSuite(string file) => RunSuiteFile(file);

    [Theory]
    [MemberData(nameof(WebCryptoSignVerifySuiteFiles))]
    public void RunsTheWebCryptoSignVerifySuite(string file) => RunSuiteFile(file);

    [Theory]
    [MemberData(nameof(WebCryptoWrapKeySuiteFiles))]
    public void RunsTheWebCryptoWrapKeySuite(string file) => RunSuiteFile(file);

    [Theory]
    [MemberData(nameof(StreamsSuiteFiles))]
    public void RunsTheStreamsSuite(string file) => RunSuiteFile(file);

    [Theory]
    [MemberData(nameof(ReadableStreamsSuiteFiles))]
    public void RunsTheReadableStreamsSuite(string file) => RunSuiteFile(file);

    [Theory]
    [MemberData(nameof(ReadableByteStreamsSuiteFiles))]
    public void RunsTheReadableByteStreamsSuite(string file) => RunSuiteFile(file);

    [Theory]
    [MemberData(nameof(WritableStreamsSuiteFiles))]
    public void RunsTheWritableStreamsSuite(string file) => RunSuiteFile(file);

    [Theory]
    [MemberData(nameof(TransformStreamsSuiteFiles))]
    public void RunsTheTransformStreamsSuite(string file) => RunSuiteFile(file);

    [Theory]
    [MemberData(nameof(StreamsPipingSuiteFiles))]
    public void RunsTheStreamsPipingSuite(string file) => RunSuiteFile(file);

    [Theory]
    [MemberData(nameof(TransferableStreamsSuiteFiles))]
    public void RunsTheTransferableStreamsSuite(string file) => RunSuiteFile(file);

    [Theory]
    [MemberData(nameof(CompressionSuiteFiles))]
    public void RunsTheCompressionSuite(string file) => RunSuiteFile(file);

    [Theory]
    [MemberData(nameof(UrlPatternSuiteFiles))]
    public void RunsTheUrlPatternSuite(string file) => RunSuiteFile(file);

    [Theory]
    [MemberData(nameof(FileApiSuiteFiles))]
    public void RunsTheFileApiSuite(string file) => RunSuiteFile(file);

    [Theory]
    [MemberData(nameof(FileApiBlobSuiteFiles))]
    public void RunsTheFileApiBlobSuite(string file) => RunSuiteFile(file);

    [Theory]
    [MemberData(nameof(FileApiFileSuiteFiles))]
    public void RunsTheFileApiFileSuite(string file) => RunSuiteFile(file);

    [Theory]
    [MemberData(nameof(WorkersSuiteFiles))]
    public void RunsTheWorkersSuite(string file) => RunSuiteFile(file);

    [Theory]
    [MemberData(nameof(WorkerGlobalScopeSuiteFiles))]
    public void RunsTheWorkerGlobalScopeSuite(string file) => RunSuiteFile(file);

    [Theory]
    [MemberData(nameof(MultipleWorkersSuiteFiles))]
    public void RunsTheMultipleWorkersSuite(string file) => RunSuiteFile(file);

    [Theory]
    [MemberData(nameof(HrTimeSuiteFiles))]
    public void RunsTheHrTimeSuite(string file) => RunSuiteFile(file);

    [Theory]
    [MemberData(nameof(UserTimingSuiteFiles))]
    public void RunsTheUserTimingSuite(string file) => RunSuiteFile(file);

    [Theory]
    [MemberData(nameof(TimersSuiteFiles))]
    public void RunsTheTimersSuite(string file) => RunSuiteFile(file);

    [Theory]
    [MemberData(nameof(MicrotaskQueuingSuiteFiles))]
    public void RunsTheMicrotaskQueuingSuite(string file) => RunSuiteFile(file);

    [Theory]
    [MemberData(nameof(StructuredCloneSuiteFiles))]
    public void RunsTheStructuredCloneSuite(string file) => RunSuiteFile(file);

    [Theory]
    [MemberData(nameof(DomEventsSuiteFiles))]
    public void RunsTheDomEventsSuite(string file) => RunSuiteFile(file);

    [Theory]
    [MemberData(nameof(DomAbortSuiteFiles))]
    public void RunsTheDomAbortSuite(string file) => RunSuiteFile(file);

    [Theory]
    [MemberData(nameof(FetchHeadersSuiteFiles))]
    public void RunsTheFetchHeadersSuite(string file) => RunSuiteFile(file);

    [Theory]
    [MemberData(nameof(FetchResponseSuiteFiles))]
    public void RunsTheFetchResponseSuite(string file) => RunSuiteFile(file);

    /// <summary>
    /// The inventory check: what is vendored, what is run, and what is deliberately absent must all agree.
    /// </summary>
    /// <remarks>
    /// This is what a re-vendor runs into. A suite file that arrives without a minimum-test entry would
    /// otherwise be embedded and never run — a theory case is generated per file, but the file has to be
    /// declared to be held to anything — and one that arrives against a <see cref="_notVendored"/> reason
    /// would quietly go red for a cause somebody already decided about.
    /// </remarks>
    [Fact]
    public void EveryVendoredFileIsAccountedFor()
    {
        var problems = new List<string>();

        foreach (var path in WptCorpus.Paths)
        {
            // Checked over every vendored path, not only over the two suites' own directories, so that a
            // pattern naming a directory (encoding/legacy-mb-*) is checked by something.
            foreach (var (pattern, reason) in _notVendored)
            {
                if (WptExclusion.MatchesPattern(pattern, path))
                {
                    problems.Add($"{path} is vendored although \"{pattern}\" says it should not be ({reason})");
                }
            }

            if (path.EndsWith(".any.js", StringComparison.Ordinal) && !_minimumTests.ContainsKey(path))
            {
                problems.Add($"{path} is vendored but has no entry in the minimum-test table, so nothing runs it");
            }
        }

        foreach (var declared in _minimumTests.Keys)
        {
            if (!WptCorpus.Contains(declared))
            {
                problems.Add($"{declared} has a minimum-test entry but is not vendored");
            }
        }

        foreach (var exclusion in _exclusions)
        {
            if (!WptCorpus.Contains(exclusion.File))
            {
                problems.Add($"{exclusion.File} carries an exclusion but is not vendored");
            }
        }

        // The theory cases are generated from the corpus, so an empty corpus would be an empty, green run.
        WptCorpus.TestFiles("url").Should().HaveCountGreaterThan(15);
        WptCorpus.TestFiles("encoding").Should().HaveCountGreaterThan(15);
        WptCorpus.TestFiles("compression").Should().HaveCountGreaterThan(10);
        WptCorpus.TestFiles("urlpattern").Should().HaveCountGreaterThan(2);
        WptCorpus.TestFiles("hr-time").Should().HaveCount(2);
        WptCorpus.TestFiles("user-timing").Should().HaveCountGreaterThan(15);

        // And a sub-directory that lost its theory member would be a suite nothing runs, which the
        // minimum-test check above cannot see: it proves a file is declared, not that a theory reaches it.
        // Every declared suite must therefore produce cases, and every vendored .any.js must belong to one of
        // the declared suites.
        CheckSuiteGroup("WebCryptoAPI/", _webCryptoSuites);
        CheckSuiteGroup("streams/", _streamsSuites);
        CheckSuiteGroup("FileAPI/", _fileApiSuites);
        CheckSuiteGroup("workers/", _workersSuites);
        CheckSuiteGroup("html/", _htmlSuites);
        CheckSuiteGroup("dom/", _domSuites);
        CheckSuiteGroup("fetch/", _fetchSuites);

        string.Join(Environment.NewLine, problems).Should().BeEmpty();

        void CheckSuiteGroup(string prefix, string[] suites)
        {
            foreach (var suite in suites)
            {
                WptCorpus.TestFiles(suite).Should().NotBeEmpty($"{suite} must produce theory cases");
            }

            foreach (var path in WptCorpus.Paths)
            {
                if (!path.StartsWith(prefix, StringComparison.Ordinal)
                    || !path.EndsWith(".any.js", StringComparison.Ordinal))
                {
                    continue;
                }

                if (Array.TrueForAll(suites, suite => !WptCorpus.TestFiles(suite).Contains(path)))
                {
                    problems.Add($"{path} is vendored but belongs to no declared {prefix.TrimEnd('/')} suite, so no theory runs it");
                }
            }
        }
    }

    /// <summary>
    /// Every vendored <c>.any.js</c> is reached by exactly one theory, and every theory reaches only
    /// vendored files.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The check above proves a file is <i>declared</i> — that it has a minimum-test entry and belongs to a
    /// suite in one of the suite arrays. It cannot prove anything <i>runs</i> it: those arrays are prose
    /// until a <c>[Theory]</c> with a matching <c>[MemberData]</c> exists, and deleting the theory (or its
    /// attribute, or renaming the member it names) would leave a whole standard silently unrun with the
    /// whole inventory still green. So this walks the theories themselves and holds their union to the
    /// corpus.
    /// </para>
    /// <para>
    /// It is also what stops two theories overlapping. Suites are directories and
    /// <see cref="WptCorpus.TestFiles"/> never descends, so a file belongs to exactly one — a second theory
    /// covering it would double every one of its cases and, worse, make an exclusion that is stale in one
    /// theory look live because the other still matched it.
    /// </para>
    /// </remarks>
    [Fact]
    public void EveryVendoredTestFileIsReachedByExactlyOneTheory()
    {
        var reachedBy = new Dictionary<string, string>(StringComparer.Ordinal);
        var theories = 0;

        foreach (var method in typeof(WptTestRunner).GetMethods(BindingFlags.Public | BindingFlags.Instance))
        {
            var memberData = method.GetCustomAttributes<MemberDataAttribute>().ToArray();
            if (memberData.Length == 0)
            {
                continue;
            }

            method.GetCustomAttribute<TheoryAttribute>()
                .Should().NotBeNull($"{method.Name} carries [MemberData] and so must be a [Theory]");
            theories++;

            foreach (var attribute in memberData)
            {
                var member = typeof(WptTestRunner).GetMethod(
                    attribute.MemberName,
                    BindingFlags.Public | BindingFlags.Static);
                member.Should().NotBeNull($"{method.Name} names \"{attribute.MemberName}\"");

                var rows = (IEnumerable<object[]>) member!.Invoke(null, null)!;
                var any = false;
                foreach (var row in rows)
                {
                    any = true;
                    var file = (string) row[0];
                    reachedBy.TryGetValue(file, out var already).Should().BeFalse(
                        $"{file} is reached by both {already} and {method.Name}");
                    reachedBy[file] = method.Name;
                }

                any.Should().BeTrue($"{attribute.MemberName} must produce cases");
            }
        }

        // A floor rather than an equality, so adding a standard needs no edit here — but low enough to be
        // meaningless only if most of the file were deleted, which is the failure this number guards.
        theories.Should().BeGreaterThanOrEqualTo(20, "each vendored suite is one theory");

        var vendored = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var path in WptCorpus.Paths)
        {
            if (path.EndsWith(".any.js", StringComparison.Ordinal))
            {
                vendored.Add(path);
            }
        }

        reachedBy.Keys.Should().BeEquivalentTo(vendored,
            "every vendored .any.js must be reached by a theory, and a theory must reach nothing else");
    }

    /// <summary>
    /// The worker lane holds exactly the <c>workers/</c> corpus, and holds all of it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="WptHarness.RunsInAWorker"/> asks two questions, and a corpus bump can break either. A
    /// <c>workers/</c> file whose <c>// META: global=</c> line stopped naming a worker global would quietly
    /// fall back to the top-level engine, where it would assert nothing about a worker and stay green — the
    /// most expensive kind of silence a conformance driver can have. And a file vendored into
    /// <c>workers/</c> that is really about a <i>window</i> creating a worker would be run as a worker's own
    /// body, which is not what it is for.
    /// </para>
    /// <para>
    /// So both directions are pinned here rather than left to the rule. Everything outside <c>workers/</c> is
    /// covered by the same walk, which is what says the lane cannot widen: the directory clause is the reason
    /// no previously vendored suite can move, and this is the check that it stays the reason.
    /// </para>
    /// </remarks>
    [Fact]
    public void EveryWorkerLaneFileIsAWorkersFile()
    {
        var problems = new List<string>();
        var inTheWorkerLane = 0;

        foreach (var path in WptCorpus.Paths)
        {
            if (!path.EndsWith(".any.js", StringComparison.Ordinal))
            {
                continue;
            }

            var inWorkersDirectory = path.StartsWith("workers/", StringComparison.Ordinal);
            var runsInAWorker = WptHarness.RunsInAWorker(path);

            if (runsInAWorker)
            {
                inTheWorkerLane++;
            }

            if (runsInAWorker && !inWorkersDirectory)
            {
                problems.Add($"{path} would run inside a worker, but only the workers/ corpus is meant to");
            }
            else if (inWorkersDirectory && !runsInAWorker)
            {
                problems.Add($"{path} is a workers/ file that would run in the top-level engine, where it "
                    + "would assert nothing about a worker");
            }
        }

        string.Join(Environment.NewLine, problems).Should().BeEmpty();

        // And the lane is not empty, which the two rules above would both be satisfied by.
        inTheWorkerLane.Should().BeGreaterThanOrEqualTo(10, "the workers/ corpus runs inside real workers");
    }

    [Fact]
    public void TheHarnessShimAndItsHelpersAreEmbedded()
    {
        WptCorpus.Prelude.Should().Contain("__wpt", "the driver reads its results back through that object");

        // The two suites' `// META: script=` lines name these; a build that stopped embedding one would
        // otherwise show up as every file in a suite failing for an unrelated-looking reason.
        foreach (var helper in new[]
                 {
                     "common/subset-tests.js",
                     "common/subset-tests-by-key.js",
                     "common/sab.js",
                     "encoding/resources/encodings.js",
                     "url/resources/urltestdata.json",
                     "url/resources/urltestdata-javascript-only.json",
                     "url/resources/setters_tests.json",
                     "WebCryptoAPI/util/helpers.js",
                     "WebCryptoAPI/util/ec_key_fixtures.js",
                     "WebCryptoAPI/util/rsa_key_fixtures.js",
                     "WebCryptoAPI/util/okp_key_fixtures.js",
                     "streams/resources/recording-streams.js",
                     "streams/resources/rs-test-templates.js",
                     "streams/resources/rs-utils.js",
                     "streams/resources/test-utils.js",
                     // The two streams garbage-collection files and FileAPI/blob/Blob-stream.any.js name it
                     // as `/common/gc.js`, from the wpt root.
                     "common/gc.js",
                     "compression/resources/formats.js",
                     "compression/resources/decompress.js",
                     "compression/resources/decompression-input.js",
                     "compression/resources/concatenate-stream.js",
                     // Vendored third-party code with its own licence beside it: three compression files
                     // check their own output by inflating it with pako rather than with the engine's own
                     // DecompressionStream, which is the point — a round trip through one implementation
                     // proves nothing about the bytes.
                     "compression/third_party/pako/pako_inflate.min.js",
                     "urlpattern/resources/urlpatterntests.js",
                     "urlpattern/resources/urlpattern-hasregexpgroups-tests.js",
                     "urlpattern/resources/urlpatterntestdata.json",
                     // Named as `../support/Blob.js` from both FileAPI suites, which is the one shape of
                     // META reference that leaves a suite's own directory.
                     "FileAPI/support/Blob.js",

                     "user-timing/resources/user-timing-helper.js",
                     "dom/abort/resources/abort-signal-any-tests.js",
                     "html/webappapis/structured-clone/structured-clone-battery-of-tests.js",
                     "html/webappapis/structured-clone/structured-clone-battery-of-tests-with-transferables.js",
                     "html/webappapis/structured-clone/structured-clone-battery-of-tests-harness.js",
                     "fetch/api/resources/utils.js",
                     "fetch/api/resources/data.json",
                     "fetch/api/response/response-stream-disturbed-util.js",
                     "encoding/resources/single-byte-decoder.js",
                 })
        {
            WptCorpus.Contains(helper).Should().BeTrue($"{helper} must be embedded");
        }
    }

    private static IEnumerable<object[]> Cases(string suite)
    {
        foreach (var file in WptCorpus.TestFiles(suite))
        {
            yield return [file];
        }
    }

    /// <summary>
    /// Runs one file and holds it to two rules. Every failing test must be named by an exclusion, and every
    /// exclusion must match at least one failing test and no passing one.
    /// </summary>
    /// <remarks>
    /// The second rule is the whole reason a glob is safe to write here. An entry that has stopped applying
    /// — because the engine was fixed, or because a corpus bump renamed or removed the case — fails the run
    /// instead of sitting there forever, and one that would widen to cover a test that works fails for that
    /// too, so <c>*</c> can never turn an exclusion into a blanket.
    /// </remarks>
    private static void RunSuiteFile(string file)
    {
        var outcome = WptHarness.Run(file);

        outcome.HarnessError.Should().BeNull($"{file} must run to completion");

        // An entry scoped to another operating system is invisible here — it excludes nothing and the
        // staleness rules below do not ask it to match, which is what keeps the table exact per OS.
        var exclusions = Array.FindAll(_exclusions,
            e => string.Equals(e.File, file, StringComparison.Ordinal) && e.AppliesOnThisPlatform);
        var matchedFailing = new bool[exclusions.Length];
        var matchedPassing = new List<string>?[exclusions.Length];
        var failures = new List<string>();

        foreach (var result in outcome.Results)
        {
            var excluded = false;
            for (var i = 0; i < exclusions.Length; i++)
            {
                if (!exclusions[i].Matches(result.Name))
                {
                    continue;
                }

                excluded = true;
                if (result.Passed)
                {
                    (matchedPassing[i] ??= []).Add(result.Name);
                }
                else
                {
                    matchedFailing[i] = true;
                }
            }

            if (!result.Passed && !excluded)
            {
                failures.Add($"[{result.Status}] {result.Name}: {result.Message}");
            }
        }

        var stale = new List<string>();
        for (var i = 0; i < exclusions.Length; i++)
        {
            if (matchedPassing[i] is { } passing)
            {
                stale.Add($"\"{exclusions[i].TestName}\" ({exclusions[i].Divergence}) covers {passing.Count} test(s) that pass, "
                    + $"the first being \"{passing[0]}\"");
            }
            else if (!matchedFailing[i])
            {
                stale.Add($"\"{exclusions[i].TestName}\" ({exclusions[i].Divergence}) matches no test in the file");
            }
        }

        // Before the failure and staleness reports, because a file that produced nothing would otherwise be
        // reported as a wall of exclusions that match no test rather than as the empty run it is.
        outcome.Results.Count.Should().BeGreaterThanOrEqualTo(
            _minimumTests[file],
            $"{file} must actually have run its corpus");

        Report(file, failures, stale);
    }

    private static void Report(string file, List<string> failures, List<string> stale)
    {
        if (failures.Count == 0 && stale.Count == 0)
        {
            return;
        }

        var message = new StringBuilder();
        message.Append(file).AppendLine(":");

        if (stale.Count > 0)
        {
            message.Append("  ").Append(stale.Count)
                .AppendLine(" exclusion(s) that no longer apply — remove or narrow them:");
            foreach (var entry in stale)
            {
                message.Append("    ").AppendLine(entry);
            }
        }

        if (failures.Count > 0)
        {
            message.Append("  ").Append(failures.Count)
                .AppendLine(" failing test(s) — fix them, or add them to the exclusion table with a category:");
            foreach (var entry in failures)
            {
                message.Append("    ").AppendLine(entry);
            }
        }

        message.ToString().Should().BeEmpty();
    }
}
#endif
