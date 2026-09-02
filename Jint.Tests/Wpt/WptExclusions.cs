#if NET8_0_OR_GREATER
#nullable enable

namespace Jint.Tests.Wpt;

/// <summary>
/// Why a vendored web-platform-test does not pass. Every exclusion carries one, so the table reads as an
/// inventory of what is missing rather than as a list of things that are merely red.
/// </summary>
internal enum WptDivergence
{
    /// <summary>
    /// The seven legacy multi-byte encodings (Big5, EUC-JP, EUC-KR, GBK, gb18030, ISO-2022-JP, Shift_JIS)
    /// are named by the label table and refused as unsupported; their suites stay red until someone demands
    /// the tables. The single-byte families these entries used to cover are implemented and green.
    /// </summary>
    NeedsLegacyMultiByteEncodings,

    /// <summary>
    /// <para>
    /// The test needs <c>WebAssembly</c>, in one of the two ways a corpus reaches for it. The encoding suites
    /// obtain their <c>SharedArrayBuffer</c> constructor through <c>WebAssembly.Memory</c>, which is what
    /// <c>common/sab.js</c> does — deliberately, so that a browser gated by cross-origin isolation still gets
    /// one. Jint has <c>SharedArrayBuffer</c> but no <c>WebAssembly</c>, so the helper hands back
    /// <see langword="null"/> and every SAB-backed case of the file fails in the helper rather than in the
    /// code under test.
    /// </para>
    /// <para>
    /// <c>streams/readable-byte-streams/non-transferable-buffers.any.js</c> is the second way: a
    /// <c>WebAssembly.Memory</c> buffer is the only <c>ArrayBuffer</c> a script can obtain that cannot be
    /// transferred, and the file exists to check that a byte stream refuses one. What the engine does with a
    /// buffer it cannot take is covered by <c>bad-buffers-and-views.any.js</c> and
    /// <c>enqueue-with-detached-buffer.any.js</c>, which pass.
    /// </para>
    /// <para>
    /// WebAssembly is out of scope for an interpreter, so this is the corpus meeting an environment it was
    /// not written for rather than a gap to close.
    /// </para>
    /// </summary>
    NeedsWebAssembly,

    /// <summary>
    /// The test needs a <c>MessageChannel</c> — historically to detach a buffer by posting it through one.
    /// <para>
    /// <b>No entry uses this today, and the reason it once gave is no longer true.</b> It used to read "message
    /// ports are a worker primitive and Jint has no worker story", which stopped being the case in stages:
    /// <c>MessageChannel</c> and <c>MessagePort</c> arrived with <see cref="Jint.WebApi.WebApiFeatures"/>'s
    /// messaging feature, transferring a port between engines arrived with
    /// https://github.com/sebastienros/jint/issues/3197, and <c>Worker</c> itself with
    /// https://github.com/sebastienros/jint/issues/3167. The corpus reports the change rather than the prose
    /// doing it: <c>FileAPI/blob/Blob-constructor-detached-buffer.any.js</c> detaches its buffer with
    /// <c>new MessageChannel().port1.postMessage(buffer, [buffer])</c> and passes on exactly that mechanism,
    /// which is why its rows are not here.
    /// </para>
    /// <para>
    /// The category is kept because the driver's engine enables
    /// <see cref="Jint.WebApi.WebApiFeatures.Default"/> and not everything, so a future suite may still meet a
    /// channel it was not granted. What it must never again mean is "workers do not exist here".
    /// </para>
    /// </summary>
    NeedsMessageChannel,

    /// <summary>
    /// <para>
    /// The test names one of the two globals HTML gives a worker and Jint's worker global scope
    /// <b>deliberately does not add</b>. They are one decision, taken together and for one reason — the worker
    /// global is the global the engine already builds plus the worker names, and a name it cannot back with the
    /// object HTML says is behind it is not faked:
    /// </para>
    /// <list type="bullet">
    /// <item><description>
    /// <b><c>location</c> and <c>WorkerLocation</c></b>
    /// (https://html.spec.whatwg.org/multipage/workers.html#dom-workerglobalscope-location). A worker's script
    /// name is its <c>Module.Location</c>, which a host exposes through <c>import.meta</c>; there is no URL for
    /// the other eight members of a <c>WorkerLocation</c> to be parts of. Declined for v1 and cheap to add if
    /// porting pressure appears.
    /// </description></item>
    /// <item><description>
    /// <b><c>WorkerNavigator</c>, and <c>hardwareConcurrency</c> in particular</b>
    /// (https://html.spec.whatwg.org/multipage/workers.html#the-workernavigator-object). A worker gets whatever
    /// <c>navigator</c> its parent's <c>Navigator</c> flag gave it and no separate worker interface;
    /// <c>hardwareConcurrency</c> is absent on purpose, because in Jint the <i>host</i> owns every thread and an
    /// engine that answered a number would be guessing at a resource it does not allocate.
    /// </description></item>
    /// </list>
    /// <para>
    /// Both are numbered divergences (6 and 7) in the ledger of
    /// https://github.com/sebastienros/jint/issues/3167, so these entries are that ledger asserted rather than
    /// merely written down. Divergence 5 — no <c>WorkerGlobalScope</c> or <c>DedicatedWorkerGlobalScope</c>
    /// interface object — used to head this list and is <b>closed</b>:
    /// https://github.com/sebastienros/jint/issues/3195 gave the worker global a real
    /// <c>DedicatedWorkerGlobalScope.prototype</c> chain, so <c>self instanceof WorkerGlobalScope</c> is
    /// answered by walking it. The one link the chain still declines is <c>EventTarget</c>, and it is declined
    /// for this list's own reason: the worker global is not one, so claiming it would be an <c>instanceof</c>
    /// that lies.
    /// </para>
    /// </summary>
    NeedsDeclinedWorkerGlobals,

    /// <summary>
    /// <para>
    /// The test expects a worker to be able to create a worker. Jint's default is the opposite:
    /// <c>WorkerRequest.CreateDefaultOptions()</c> subtracts <c>WebApiFeatures.Workers</c> and does not copy the
    /// provider, so <c>Worker</c> is <c>undefined</c> inside a worker until a provider sets both — one visible
    /// line, by which the host accepts the accounting.
    /// </para>
    /// <para>
    /// This is the opposite kind of decision from <see cref="NeedsDeclinedWorkerGlobals"/>: not a name declined
    /// but a <i>grant</i> withheld, on the feature's rule that grants never travel by implication. A per-engine
    /// worker cap bounds the branching factor of a tree, never its depth, so inheriting the capability that
    /// manufactures engines would make a three-line self-spawning module an unbounded fork bomb on the shipped
    /// defaults. QuickJS refuses nesting outright, browsers bound the tree only with a global cap, and Deno
    /// answers with monotone capability; off-by-default is the shape all three agree on for a library.
    /// <c>WorkerRequest.Depth</c> and <c>LiveWorkerCount</c> exist to let a provider that opts in bound the
    /// tree itself.
    /// </para>
    /// </summary>
    NeedsWorkerNesting,

    /// <summary>
    /// <para>
    /// The test asserts what a <b>sloppy-mode</b> assignment does, and the worker lane has no sloppy mode to
    /// offer it. Jint runs module workers only (divergence 2 of
    /// https://github.com/sebastienros/jint/issues/3167, and <c>WorkerType</c> says why), so a worker-scoped
    /// <c>.any.js</c> file is the body of a <i>module</i> where a browser runs it as a classic
    /// <c>.any.worker.js</c> script — and module code is strict. That is the one behavioural difference
    /// between the two lanes, recorded in <c>Vendor/README.md</c> since the lane was built.
    /// </para>
    /// <para>
    /// Both entries are the same assignment seen from two files: <c>self = …</c> against
    /// <c>WorkerGlobalScope</c>'s
    /// <see href="https://html.spec.whatwg.org/multipage/workers.html#dom-workerglobalscope-self">
    /// <c>readonly attribute WorkerGlobalScope self</c></see>. A read-only attribute refuses an assignment
    /// two different ways — silently in sloppy mode, with a <c>TypeError</c> in strict — and the corpus was
    /// written for the first, so <c>self.any.js</c> asserts the value did not change and
    /// <c>Worker-replace-self.any.js</c> asserts nothing was thrown. The engine implements the attribute
    /// (https://github.com/sebastienros/jint/issues/3224): before that fix both rows failed because the
    /// assignment <i>succeeded</i> — <c>self.any.js</c> reporting "expected object
    /// &quot;DedicatedWorkerGlobalScope&quot; but got 1", and <c>Worker-replace-self.any.js</c> that
    /// <c>self instanceof WorkerGlobalScope</c> had become false — and they now fail with the
    /// <c>TypeError</c> strict mode owes, which is the same refusal in the other of its two voices.
    /// </para>
    /// <para>
    /// So this is deliberately <b>not</b> <see cref="NeedsTriage"/>: there is no defect left to chase, and
    /// nothing short of a classic-script worker would move either row. It is just as deliberately not
    /// <see cref="AssertsWhatNothingRequires"/>, which it superficially resembles by also being permanent and
    /// also not debt. The line is what the standard asks. There, nothing asks for what the test asserts and
    /// no implementation delivers it; here HTML asks for exactly what these two rows assert — a sloppy-mode
    /// assignment to a read-only attribute <i>is</i> a silent no-op — and an implementation that runs the
    /// file as the classic <c>.any.worker.js</c> script wpt generates for it satisfies them, which is what
    /// upstream's <c>global=worker</c> key means the file to be. What makes them permanent is a lane Jint
    /// chose, which is the family <see cref="NeedsDeclinedWorkerGlobals"/> and
    /// <see cref="NeedsForbiddenHeaderNames"/> are in and not that one. What keeps them honest is that the
    /// engine's half is pinned from both sides in
    /// <c>Jint.Tests/Runtime/WebApi/WorkerMechanismTests.cs</c>, in both modes — the module body for strict,
    /// an indirect <c>eval</c> for the sloppy one this lane cannot otherwise reach.
    /// </para>
    /// </summary>
    NeedsClassicWorkerScript,

    /// <summary>
    /// <para>
    /// The test asks an algorithm for a parameter .NET's own primitives refuse, and the refusal is what
    /// <c>Jint/WebApi/Crypto/</c> documents on the class that makes it rather than something the engine could
    /// choose differently. Four of them, each named in the message the operation rejects with:
    /// </para>
    /// <list type="bullet">
    /// <item><description>
    /// AES-GCM takes a <b>96-bit iv and nothing else</b> (<c>AesGcm.NonceByteSizes</c> is 12 to 12), where
    /// https://w3c.github.io/webcrypto/#aes-gcm allows one "up to 2^64-1 bytes long". That is the whole of
    /// <c>aes_gcm_256_iv</c>, and the reason a <c>wrapKey</c> under AES-GCM cannot work either.
    /// </description></item>
    /// <item><description>
    /// AES-GCM takes a <b>96- to 128-bit tag</b> (<c>AesGcm.TagByteSizes</c> is 12 to 16), where the
    /// specification's list also holds 32 and 64 — and on macOS <b>only 128 bits</b>, Apple's implementation
    /// answering 16 to 16, which is why the 96- to 120-bit rows are the platform-scoped entries the
    /// <c>Platform</c> parameter exists for.
    /// </description></item>
    /// <item><description>
    /// RSA-OAEP takes <b>no label</b>: .NET exposes OAEP through <c>RSAEncryptionPadding</c>, which has no
    /// place for one, so a present and non-empty <c>label</c> member is an <c>OperationError</c>.
    /// </description></item>
    /// <item><description>
    /// RSA-PSS takes a <b>salt as long as the hash and no other length</b> —
    /// <c>RSASignaturePadding.Pss</c> is defined that way — where <c>RsaPssParams.saltLength</c> is any
    /// unsigned long.
    /// </description></item>
    /// </list>
    /// <para>
    /// These are the corpus meeting a platform rather than a gap in Jint. Removing one means reaching past
    /// the BCL to a hand-written primitive, which is not a thing to do quietly.
    /// </para>
    /// </summary>
    NeedsPlatformCryptoParameters,

    /// <summary>
    /// The test asks for a <c>KeyUsage</c> the enumeration does not have. The corpus at this pin passes
    /// <c>encapsulateKey</c>, <c>decapsulateKey</c>, <c>encapsulateBits</c> and <c>decapsulateBits</c> to
    /// <c>generateKey</c> and <c>importKey</c> and expects a <c>SyntaxError</c> — "recognized, but not one
    /// this algorithm supports". WebIDL's own conversion says otherwise for an engine that does not have
    /// them: https://w3c.github.io/webcrypto/#dfn-KeyUsage declares eight values and none of these is among
    /// them, so a ninth is outside the enumeration and a <c>TypeError</c>, which is what Jint answers. The
    /// values arrive with the ML-KEM proposal, whose own tests are in <c>.tentative.</c> files this corpus
    /// does not vendor — these rows are that proposal leaking into the stable ones. The entries stop applying
    /// when the specification adopts the values (Jint would then have to accept and refuse them) or when
    /// upstream moves the rows, and either is the right moment to revisit them.
    /// </summary>
    NeedsKeyEncapsulation,

    /// <summary>
    /// The test is over Curve25519 — X25519 or Ed25519. The BCL ships neither, so the whole family is out of
    /// scope for a crypto layer built on it; the files dedicated to those curves are not vendored at all (see
    /// <c>Vendor/README.md</c>) and this category is for the rows that sit inside a file which is otherwise
    /// about something else.
    /// </summary>
    NeedsCurve25519,

    /// <summary>
    /// The test imports an EC key whose point is in compressed form, which
    /// https://w3c.github.io/webcrypto/#ecdsa-operations makes optional — the corpus says so itself by giving
    /// up through <c>assert_implements_optional</c> when the import raises a <c>DataError</c>, so these are
    /// recorded <c>PRECONDITION_FAILED</c> rather than <c>FAIL</c>. .NET's <c>ECDsa</c>/<c>ECDiffieHellman</c>
    /// import paths want an uncompressed point, and decompressing one means implementing the curve's square
    /// root by hand.
    /// </summary>
    NeedsCompressedEcPointImport,

    /// <summary>
    /// The test asserts what a <b>non-secure</b> context sees, which Jint has no way to be: it has no scheme,
    /// no origin and therefore no secure-context bit, so <c>crypto.subtle</c>, the <c>SubtleCrypto</c>
    /// interface object and <c>CryptoKey</c> are all simply there once the feature is enabled. Upstream runs
    /// <c>historical.any.js</c> over plain http for exactly the property it asserts, so this is the corpus
    /// meeting an environment it was not written for rather than a gap to close. All three of the file's tests
    /// are here now: the <c>SubtleCrypto</c> row used to pass because there was no such interface object at
    /// all, and sebastienros/jint#3195 installed it because WinterTC's Minimum Common API §5.1 lists it.
    /// </summary>
    NeedsSecureContextModel,

    /// <summary>
    /// <para>
    /// The test asserts one of the two lenient-decompression divergences <c>DecompressionCodec</c> documents
    /// on itself: https://compression.spec.whatwg.org/#decompressionstream makes it an error for a stream to
    /// end before its member is complete, and an error for anything to follow the member. Detecting either
    /// needs to know how many of the bytes handed over the decompressor actually consumed, and .NET exposes
    /// no incremental inflater or decoder — only <c>GZipStream</c>, <c>ZLibStream</c>, <c>DeflateStream</c>
    /// and <c>BrotliStream</c>, which pull from a source stream, buffer input internally and report neither
    /// figure. So a truncated stream ends its readable side cleanly and trailing bytes are ignored.
    /// </para>
    /// <para>
    /// <b><c>brotli</c> shares the divergence rather than escaping it.</b> Its rows were parked under a
    /// category of their own while the format was refused outright, on the reasoning that they never reached
    /// the point where this divergence could decide them;
    /// https://github.com/sebastienros/jint/issues/3210 implemented the format and they landed here, because
    /// <c>BrotliStream</c> answers a dropped last byte and an appended junk byte exactly as the deflate
    /// family does — it hands over the payload it decoded and then reports "no more input for now".
    /// </para>
    /// <para>
    /// Six rows of <c>decompression-corrupt-input.any.js</c> are the whole of it — <c>truncating the input</c>
    /// and <c>trailing junk</c>, for each of that file's three formats — and everything the decompressor
    /// itself rejects — a bad header, a failed CRC32/ADLER32, a malformed DEFLATE block, a brotli stream the
    /// decoder cannot parse, a dictionary-compressed stream, an empty input — is still an error and still
    /// passes, which is the case that matters for telling corrupt input from good. The one file that asserts
    /// the same divergence by <i>waiting</i> for the error rather than by comparing a result,
    /// <c>compression/decompression-extra-input.any.js</c>, is not vendored: it stalls rather than fails.
    /// </para>
    /// </summary>
    NeedsIncrementalInflater,

    /// <summary>
    /// The test needs an HTTP response that only a wpt server can produce — a <c>.py</c> handler that echoes
    /// headers, generates bytes with a charset, trickles a body or redirects — <b>from a lane that has no
    /// server</b>. <see cref="WptServer"/> now stands in for wptserve, but only for the files
    /// <c>WptHarness._serverBackedFiles</c> names: every other engine the driver builds still has no
    /// <c>fetch</c> at all, and the shim's <c>XMLHttpRequest</c> is a reader over the vendored tree on every
    /// lane that has no server; the server lane itself carries the shipped <c>XMLHttpRequest</c>.
    /// Whole files whose every test does this are not vendored; these entries are the rows
    /// that sit inside a file which is otherwise about something else — the <c>(XMLHttpRequest)</c> half of
    /// <c>encoding/single-byte-decoder.any.js</c>, whose <c>(TextDecoder)</c> half tests the same decoders and
    /// passes, and two of the nine cases of <c>workers/modules/dedicated-worker-import.any.js</c>, whose
    /// fixtures are a <c>.sub.js</c> worker importing from a second origin and a worker importing through
    /// <c>redirect.py</c>. Those two reach the file's own <c>onerror</c> reject path — the worker's module
    /// loader refuses a specifier the vendored corpus does not hold, which the parent hears as an
    /// <c>error</c> event — so they fail as tests rather than stalling the file, which is what lets the other
    /// seven run.
    /// </summary>
    NeedsWptServer,

    /// <summary>
    /// The test hands a <b>relative</b> url to <c>Request</c>, <c>Response.redirect()</c> or another member
    /// that parses one. <c>RequestConstructor</c> documents the decision: the specification parses such a
    /// string against "the entry settings object's API base URL", which is a document's url, and an embedded
    /// engine has no document. That is now a <i>setting</i> rather than a refusal —
    /// <c>Options.WebApi.Fetch.BaseUrl</c>, which the driver hands every server-lane engine, and which is
    /// what let <c>fetch/api/request/</c> be vendored at all — so what is left here is the rows in a file
    /// that runs outside that lane, where there is still no document and no base URL to resolve against.
    /// </summary>
    NeedsApiBaseUrl,

    /// <summary>
    /// <para>
    /// The test names a <c>Request</c> member this engine deliberately does not have, or asks it to refuse a
    /// value for one. <c>JsRequest</c> states the rule the surface is chosen by — a member must describe
    /// something the engine <i>has</i> — and these are the seven that describe a browser concept it has not:
    /// <c>destination</c> (there are no fetch destinations), <c>mode</c> and the <c>window</c> member (there
    /// is no same-origin policy and no CORS model, which is also why a redirect is never opaque-filtered),
    /// <c>cache</c> (there is no HTTP cache), <c>integrity</c> (no subresource integrity),
    /// <c>isReloadNavigation</c> and <c>isHistoryNavigation</c> (there are no navigations).
    /// </para>
    /// <para>
    /// Accepting and ignoring them is the Node and workerd convention, and the alternative is worse than the
    /// failure: a <c>mode</c> attribute answering <c>"cors"</c> on an engine with no origin would be a member
    /// that lies, and feature detection is written against absence. <c>credentials</c>, <c>referrer</c> and
    /// <c>referrerPolicy</c> used to be in this list and left it when
    /// <c>Options.WebApi.Fetch.CookieJar</c>, <c>Referrer</c> and <c>Origin</c> gave the engine the things
    /// they describe, which is the shape of the argument for ever removing an entry here.
    /// </para>
    /// </summary>
    NeedsBrowserRequestModel,


    /// <summary>
    /// <para>
    /// The test asserts the <b>opaque redirect filtered response</b> that <c>redirect: "manual"</c> produces
    /// in a browser — status 0, type <c>"opaqueredirect"</c>, an empty header list — and gets the redirect
    /// response itself, <c>Location</c> and all.
    /// </para>
    /// <para>
    /// <c>FetchTransport</c> documents the choice and it is Node's reading of
    /// https://fetch.spec.whatwg.org/#concept-http-fetch step 6: the filtered response exists to hide a
    /// cross-origin redirect from a <i>page</i>, which is a concern an embedded engine with no origin does
    /// not have, and handing the script the response it actually got is more useful than handing it a blank.
    /// So this is a divergence Jint chose rather than a gap — the same kind of fact as
    /// <see cref="NeedsForbiddenHeaderNames"/>, and for the same reason it is not
    /// <see cref="AssertsWhatNothingRequires"/>: the standard really does require what the test asserts.
    /// </para>
    /// </summary>
    NeedsOpaqueRedirect,

    /// <summary>
    /// The test sends or reads a header value the Fetch Standard calls valid — a header value may hold any
    /// byte but NUL, LF and CR (https://fetch.spec.whatwg.org/#header-value) — and the <b>.NET HTTP stack</b>
    /// does not carry it. It is not an engine defect and not the driver's server either: the bytes leave
    /// <see cref="WptServer"/> intact, and <c>WptServerTests.AHeaderValueAboveAsciiDoesNotSurviveTheHttpStack</c>
    /// measures where the line falls — exactly at ASCII, control characters surviving and every byte from
    /// 0x80 up lost — with no engine in the picture at all, which is what that test exists for. Nothing Jint
    /// can do moves these rows short of writing its own HTTP client.
    /// </summary>
    NeedsPermissiveHeaderTransport,

    /// <summary>
    /// The test asserts a request header a <b>browser</b> adds on the script's behalf out of user or document
    /// state — <c>Accept-Language</c> from the user's language preferences, and the <c>Referer</c> family
    /// from the document that made the request. An embedded engine has neither, and inventing a value would
    /// be inventing a fact about a user who does not exist. Deliberately not the same row as a header the
    /// <i>standard</i> requires of every client: <c>Accept: */*</c> is step 12 of
    /// https://fetch.spec.whatwg.org/#concept-fetch and is owed by anyone implementing fetch, so its row was a
    /// defect (https://github.com/sebastienros/jint/issues/3279) rather than an entry here, and it passes. The
    /// two steps sit next to each other in the same algorithm, and the condition on the second — "and
    /// request's client is non-null" — is precisely the line between them.
    /// </summary>
    NeedsBrowserRequestHeaders,

    /// <summary>
    /// The test asserts the <i>forbidden header name</i> or <i>forbidden response header name</i> list —
    /// https://fetch.spec.whatwg.org/#forbidden-request-header. <c>HeadersGuard</c> documents declining both:
    /// they are a browser's protection of headers the user agent alone controls (<c>Host</c>, <c>Cookie</c>,
    /// <c>Origin</c>, <c>Set-Cookie</c>, the method-override family), and Jint runs server-side, where those
    /// headers are exactly what a script legitimately needs to set — the same choice Node and Deno make. The
    /// guards themselves are tracked, so the two objects the standard makes immutable really are.
    /// </summary>
    NeedsForbiddenHeaderNames,

    /// <summary>
    /// The test asserts a rule the standard applies <b>only when the global object is a <c>Window</c></b>, or
    /// reaches for that window's <c>document</c>. Jint's global is neither: it is in the position a worker is
    /// in, where the same standard allows the opposite. Six rows of <c>xhr/responsetype.any.js</c> are the
    /// bulk of it — <c>open(…, false)</c> followed by a <c>responseType</c> is an <c>InvalidAccessError</c> in
    /// a window and legal in a worker — and one row of <c>xhr/send-data-es-object.any.js</c> builds a
    /// <c>Document</c> to send. The harness cannot say which of the two a Jint engine is, because it is
    /// honestly neither: <c>GLOBAL.isWindow()</c> and <c>GLOBAL.isWorker()</c> both answer false, so a file
    /// that branches on them takes the window arm.
    /// </summary>
    NeedsWindowGlobal,

    /// <summary>
    /// The test clones an <c>ImageBitmap</c> or an <c>OffscreenCanvas</c>. Both are browser graphics objects
    /// with no analogue in an embedded interpreter — the rows obtain one by drawing into a canvas — so this is
    /// the corpus meeting an environment it was not written for rather than a gap to close.
    /// </summary>
    NeedsOffscreenCanvas,

    /// <summary>
    /// A genuine failure that is not attributable to a feature Jint has decided not to have, over behaviour
    /// something really does require — which is the line between this and
    /// <see cref="AssertsWhatNothingRequires"/>. Every entry
    /// here is a bug or a specification detail to chase, and the phase of the harness work that stood the
    /// suites up deliberately recorded them rather than fixing them: the point was to find out what they
    /// say, and mixing engine fixes into the change that first ran them would have hidden which of the two
    /// moved. The four the first phase recorded — WebIDL constant order, <c>TextDecoder.decode()</c> reading
    /// its input before the options dictionary was converted, and the shared UTF-16 decoder's end-of-queue
    /// step for both endiannesses — were fixed by https://github.com/sebastienros/jint/issues/3121. The
    /// WebCryptoAPI corpus filed two more, and both are fixed as well. <b>Every "… during call" row</b>
    /// failed because <c>SubtleCrypto</c> copied the caller's <c>data</c>/<c>keyData</c> <i>before</i>
    /// normalizing the algorithm where the specification copies it after — normalization is step 2 and
    /// "let data be the result of getting a copy of the bytes held by the data parameter" is step 4
    /// (https://w3c.github.io/webcrypto/#SubtleCrypto-method-encrypt, and the same shape in <c>decrypt</c>,
    /// <c>sign</c>, <c>digest</c> and <c>importKey</c>, steps 4 and 5 for <c>verify</c>'s two buffers, and
    /// step 6 for <c>unwrapKey</c>); https://github.com/sebastienros/jint/issues/3179 moved each copy to its
    /// numbered step, and the <c>SubtleCryptoKeyTests</c> pin that used to assert the old order now asserts
    /// the new one. And <b>ECDH's two mismatched-curve rows</b> were the corpus asserting the browsers' order
    /// rather than the prose's, so https://github.com/sebastienros/jint/issues/3180 made
    /// <c>EcAlgorithm.DeriveBits</c> run the key-agreement checks before the <i>maximumLength</i> ceiling,
    /// documenting the divergence on itself and raising it upstream as
    /// https://github.com/w3c/webcrypto/issues/560.
    /// <para>
    /// <b>The streams corpus filed five rows, and they are gone.</b> The three defects behind them — the
    /// async iterator prototype's non-enumerable <c>next</c> and <c>return</c>, a <c>TransformStream</c>
    /// whose <c>readable.cancel()</c> rejected where
    /// https://streams.spec.whatwg.org/#transform-stream-default-source-cancel fulfils it, and a
    /// <c>pipeTo()</c> that reached the sink's <c>write</c> synchronously with an <c>enqueue()</c> on the
    /// source — were fixed under sebastienros/jint#3195, so the five entries that used to be here now
    /// enforce them. <c>Vendor/README.md</c> keeps the analysis, because two of the three turned out to be
    /// something other than the triage note predicted.
    /// </para>
    /// <para>
    /// <b>The workers corpus filed one, and it is fixed.</b> <c>self</c> was installed once, for every
    /// global, as an ordinary writable data property — against
    /// "HTML exposes <c>self</c> through a <c>[Replaceable]</c> accessor pair on <b>Window</b>"
    /// (https://html.spec.whatwg.org/multipage/nav-history-apis.html#dom-self), which was the only global
    /// there was at the time — where <c>WorkerGlobalScope</c>'s is a plain
    /// <c>readonly attribute WorkerGlobalScope self</c>
    /// (https://html.spec.whatwg.org/multipage/workers.html#dom-workerglobalscope-self) with no
    /// <c>[Replaceable]</c>, so the worker inherited the window's semantics along with the property.
    /// https://github.com/sebastienros/jint/issues/3224 gave the worker global its own definition, in
    /// <c>WorkerGlobalScope.Install</c> and on that global alone, because the two globals genuinely differ
    /// and the top-level one is right as it is. The two rows it held did <b>not</b> come back green, and that
    /// is the interesting part: they assert what a <i>sloppy-mode</i> assignment does, and the worker lane
    /// runs its file as a module, so a refused assignment is a <c>TypeError</c> there rather than a silent
    /// no-op. They moved to <see cref="NeedsClassicWorkerScript"/>, which is the lane and not a defect. The
    /// sibling <c>DedicatedWorkerGlobalScope.name</c>, which the old note here called "the same shape and the
    /// same question", turned out to be neither: it is
    /// <c>[Replaceable] readonly attribute DOMString name</c>
    /// (https://html.spec.whatwg.org/multipage/workers.html#dom-dedicatedworkerglobalscope-name), so writable
    /// is what its own IDL asks for.
    /// </para>
    /// <para>
    /// The File API corpus filed one too, and it is gone: three rows of
    /// <c>FileAPI/blob/Blob-constructor.any.js</c> were one <em>engine</em> defect rather than three
    /// <c>Blob</c> ones — <c>Array.prototype.values</c>/<c>keys</c>/<c>entries</c> gated on
    /// <c>ObjectInstance.IsArrayLike</c> where https://tc39.es/ecma262/#sec-array.prototype.values reads no
    /// <c>length</c> at all, the iterator's own step 1.b doing <i>LengthOfArrayLike</i> on each
    /// <c>next()</c>. Fixed under sebastienros/jint#3209, which is what the deleted entries now enforce.
    /// <b>The hr-time, DOM and fetch corpora filed seven, and six of them are gone</b> — the
    /// <c>PerformanceMarkOptions</c> dictionary conversion, <c>Event.isTrusted</c>'s
    /// <c>[LegacyUnforgeable]</c> shape, <c>Event</c>'s missing <c>srcElement</c>/<c>returnValue</c>, the
    /// <c>Headers</c> iterator prototype's non-enumerable <c>next</c>, the stream a consumed bytes-source
    /// body handed back unlocked, and a <c>record&lt;ByteString, ByteString&gt;</c> conversion that read a
    /// property WebIDL's order never reaches — all fixed under sebastienros/jint#3212, so the thirteen
    /// entries that used to be here now enforce them and
    /// <c>dom/events/Event-constructors.any.js</c> left <c>_notVendored</c> with them. <c>Vendor/README.md</c>
    /// analyses each with its citation. <b>The seventh was the last entry in this category and it was never a
    /// defect</b> — an empty <c>FormData</c> body serializing to its closing boundary rather than to nothing
    /// — so it is not here any more: it is the whole of <see cref="AssertsWhatNothingRequires"/>, moved
    /// there by https://github.com/sebastienros/jint/issues/3261, which is where that reading and its
    /// citation now live.
    /// </para>
    /// <para>
    /// <b>The structured-clone corpus filed three more, and they are gone</b> — an <c>Error</c>'s
    /// <c>cause</c> was not carried, <c>Blob</c> and <c>File</c> were not serializable at all, and
    /// <c>structuredClone(Object.prototype)</c> raised a <c>DataCloneError</c>. All three are fixed, so the
    /// thirty rows they held here left with them and that corpus now runs with three exclusions, all of them
    /// environment rather than debt. The analysis is still in <c>Vendor/README.md</c>, because two of the
    /// three are places where Jint follows web-platform-tests past what the prose of the relevant standard
    /// currently says.
    /// </para>
    /// <para>
    /// <b>One further defect never had an entry here at all, and it is gone too.</b> A throw from a
    /// <c>queueMicrotask</c> callback erupted from the pump instead of being reported, which took its whole
    /// file down and left no test for an exclusion to cover, so
    /// <c>html/webappapis/microtask-queuing/queue-microtask-exceptions.any.js</c> sat in <c>_notVendored</c>
    /// with its analysis in <c>Vendor/README.md</c>. <c>TimerFunctions</c> now invokes the callback with
    /// WebIDL's <c>"report"</c> exception behaviour — the third instance of the catch shape
    /// <c>TimerEntry.Fire</c> and <c>JsEventTarget.InvokePass</c> carry — and the file is vendored and green.
    /// </para>
    /// <para>
    /// <b>So the category is empty, and its emptiness is the signal.</b> That is the state to keep it in: a
    /// row belongs here only while somebody still owes the engine a fix, and every corpus that filed one has
    /// had it paid. Kept named rather than deleted for exactly that reason — a non-zero count here is a real
    /// finding rather than a number a reader has to go and re-read to discover it is one permanent entry —
    /// and it is legal to keep because nothing enumerates this type: the driver reads
    /// <see cref="WptExclusion.Divergence"/> only to name the category in a staleness message, so a member
    /// with no entries costs nothing and fails nothing. <see cref="NeedsMessageChannel"/> is the older
    /// precedent for a named-but-empty category. An entry that turns out not to be a defect leaves for
    /// <see cref="AssertsWhatNothingRequires"/> rather than earning a longer explanation under this one.
    /// </para>
    /// <para>
    /// <b>It went non-empty once, and the whole cycle is what the mechanism looks like when it works.</b>
    /// Standing <see cref="WptServer"/> up (https://github.com/sebastienros/jint/issues/3260) let the fetch
    /// corpus make a real request for the first time, and five things it asserts turned out not to hold. Each
    /// was filed as its own issue and deliberately left unfixed there, for the reason the paragraphs above
    /// give: the change that first runs a suite must not also be the change that moves the engine, or nobody
    /// can tell which of the two a number came from. All five are now fixed — the response <c>Headers</c>
    /// guard (https://github.com/sebastienros/jint/issues/3281) and <c>clone()</c>'s shared buffer
    /// (https://github.com/sebastienros/jint/issues/3283) first, then the three the request and response
    /// plumbing owed, which left this category empty again:
    /// <list type="bullet">
    /// <item><description>
    /// https://github.com/sebastienros/jint/issues/3279 — <c>basic/accept-header.any.js</c>: no
    /// <c>Accept: */*</c> was appended, which is step 12 of https://fetch.spec.whatwg.org/#concept-fetch.
    /// </description></item>
    /// <item><description>
    /// https://github.com/sebastienros/jint/issues/3280 — <c>basic/response-null-body.any.js</c>: a
    /// <c>HEAD</c> response carried a body stream where step 22 of
    /// https://fetch.spec.whatwg.org/#concept-main-fetch gives it a null body.
    /// </description></item>
    /// <item><description>
    /// https://github.com/sebastienros/jint/issues/3282 — <c>redirect/redirect-method.any.js</c>:
    /// <c>Content-Encoding</c>, <c>Content-Language</c> and <c>Content-Location</c> set on a bodiless request
    /// never reached the wire, because the BCL files them as content headers and a GET has no content to hang
    /// them on. It has one now, and <c>FetchTransport.CreateHeaderCarrier</c> is where the framing that costs
    /// is argued.
    /// </description></item>
    /// </list>
    /// </para>
    /// </summary>
    NeedsTriage,

    /// <summary>
    /// <para>
    /// <b>The browser lane's.</b> The test asks a question only a layout answers: where a box is, how large it
    /// is, what is under a point, whether an element scrolled. <c>Jint.Browser</c> renders nothing — that is
    /// the second sentence of its own README — and campaign item C4's flat renderer gives every element a
    /// deterministic synthetic box rather than a computed one, so a co-ordinate here is a convention and never
    /// a measurement.
    /// </para>
    /// <para>
    /// It is the browser lane's analogue of <see cref="NeedsWebAssembly"/>: the corpus meeting an environment
    /// it was not written for rather than a gap somebody is meant to close. What distinguishes it from
    /// <see cref="NeedsTriage"/> is that no fix inside this package could move the row.
    /// </para>
    /// </summary>
    NeedsLayout,

    /// <summary>
    /// <para>
    /// <b>The browser lane's.</b> The test needs a nested browsing context that <i>runs script</i> — an
    /// <c>&lt;iframe&gt;</c>, an <c>&lt;object&gt;</c>, a <c>&lt;frameset&gt;</c>, or a window it opened. A
    /// page here parses child frames and lists them (<c>Frame.IsScripted</c> is the property that says so) and
    /// gives none of them an engine, because one engine per top-level navigation is what lets "per document"
    /// and "per engine" coincide and needs no <c>WindowProxy</c> —
    /// <c>Jint.Browser/Runtime/BrowserEngineFactory.cs</c> argues it.
    /// </para>
    /// <para>
    /// So a file whose subject is a second realm — an event dispatched across two documents, a cross-realm
    /// <c>handleEvent</c>, an <c>onerror</c> that must fire in the frame's global and not the parent's — has
    /// nothing to be the second half of. It is a design decision rather than an unimplemented feature, which
    /// is why it is a category and not debt.
    /// </para>
    /// </summary>
    NeedsIframeScripting,

    /// <summary>
    /// <para>
    /// <b>The browser lane's.</b> The test needs <c>IndexedDB</c>, which neither the engine's web APIs nor
    /// this package has. It is a category rather than a not-vendored row because a file that uses a database
    /// for one of its subtests and asserts something else in the rest is a file worth running.
    /// </para>
    /// <para>
    /// <b>No entry uses it at the suites vendored today</b>, and it is here for the reason
    /// <see cref="NeedsMessageChannel"/> is: the vocabulary is shared between the two lanes, and a suite that
    /// meets a storage API this environment does not grant should have a name to be recorded under rather
    /// than reaching for <see cref="NeedsTriage"/>, which means something else entirely.
    /// </para>
    /// </summary>
    NeedsIndexedDb,

    /// <summary>
    /// <para>
    /// <b>The browser lane's.</b> The test drives input through <c>/resources/testdriver.js</c> —
    /// <c>test_driver.click()</c>, <c>send_keys()</c>, <c>Actions()</c>, <c>bless()</c> — which is wpt's
    /// automation API and is deliberately not wired to anything yet. The harness file <i>is</i> vendored, and
    /// upstream's own <c>testdriver-vendor.js</c> is the empty hook a vendor replaces, so every call rejects
    /// and the test times out or fails rather than erupting.
    /// </para>
    /// <para>
    /// This one is <b>debt with an owner</b>, unlike the three above it: campaign item C4 maps
    /// <c>testdriver.js</c> onto the same <c>InputDispatcher</c> that <c>Input.dispatchMouseEvent</c> and
    /// <c>Input.dispatchKeyEvent</c> already reach (design doc §8), so every entry in this category is a row
    /// that change is expected to move. Recording them under their own name is what will make that change's
    /// effect countable.
    /// </para>
    /// </summary>
    NeedsTestDriver,

    /// <summary>
    /// <para>
    /// The test asserts behaviour <b>nothing requires of anybody</b> — not of Jint, and not of any
    /// implementation. This is the corpus's analogue of test262's <c>=== PERMANENT EXCLUSIONS ===</c> banner
    /// and it is earned the same way: only when the standard the test claims to be about does not ask for
    /// what the test asks for, and only with a comment that records the decision and the normative citation
    /// making it defensible, never a to-do nobody intends to do. It is the one member not spelled
    /// <c>Needs…</c>, deliberately: every other names something absent that would turn its rows green if it
    /// arrived, whereas here the divergence is in the test and no change to this engine would move it.
    /// </para>
    /// <para>
    /// Two divergences today. The second is <c>fetch/api/request/request-disturbed.any.js</c>, "Input request
    /// used for creating new request became disturbed even if body is not used", which asks that
    /// <c>new Request(input, { body })</c> disturb <c>input</c>. https://fetch.spec.whatwg.org/#dom-request
    /// creates the proxy that disturbs it only "if initBody is null and inputBody is non-null", so with an
    /// init body nothing reads the input and <c>bodyUsed</c> stays false — the answer undici gives as well.
    /// Neither engine that satisfies the assertion passes the row: Chrome and Firefox both fail this file's
    /// two "became disturbed" rows, Chrome on the very next assertion because it replaces the input's stream
    /// where a proxy leaves it in place. Its siblings were the <see cref="NeedsTriage"/> debt
    /// https://github.com/sebastienros/jint/issues/3618 paid, which is the shape to keep: the rows that were
    /// a defect left the table, and the row that is the test's own divergence moved here.
    /// </para>
    /// <para>
    /// The first was moved out of <see cref="NeedsTriage"/> by
    /// https://github.com/sebastienros/jint/issues/3261 — <c>fetch/api/response/response-consume-empty.any.js</c>,
    /// "Consume empty FormData response body as text", which asks that
    /// <c>await new Response(new FormData()).text()</c> have length 0 and gets the 50-byte closing boundary
    /// <c>MultipartFormData</c> writes. HTML's
    /// https://html.spec.whatwg.org/multipage/form-control-infrastructure.html#multipart/form-data-encoding-algorithm
    /// defines the escaping and delegates the framing to RFC 7578, which defers in turn to RFC 2046, whose
    /// section 5.1.1 grammar ends every <c>multipart-body</c> with a <c>close-delimiter</c> that is not
    /// optional — so an empty entry list has no shorter conforming encoding than the one Jint writes. The
    /// empirical half agrees: on wpt.fyi's four aligned stable runs that row is 0/1 in Chrome, Edge, Firefox
    /// and Safari, where the file's thirteen other rows are 1/1 in all four. <c>Vendor/README.md</c> keeps
    /// the grammar and the measurement.
    /// </para>
    /// <para>
    /// Age never promotes an entry into this category: a row nobody has got round to stays
    /// <see cref="NeedsTriage"/> however long it has sat there, because what this member records is intent
    /// and not age. Reversing an entry is a perfectly ordinary change — a standard can move, and a reading
    /// can turn out to be wrong — but it has to argue the decision and rewrite the reasoning rather than
    /// merely delete the line. It is also not the place for a divergence Jint <i>chose</i>: a lane taken
    /// (<see cref="NeedsClassicWorkerScript"/>), a name declined (<see cref="NeedsDeclinedWorkerGlobals"/>)
    /// or a browser protection refused (<see cref="NeedsForbiddenHeaderNames"/>) are all cases where the
    /// test asserts something the standard really does require and Jint answers otherwise on purpose, which
    /// is a different fact about a row and keeps its own category.
    /// </para>
    /// </summary>
    AssertsWhatNothingRequires,
}

/// <summary>
/// One excluded test: a file, the test's name or a glob over it, and why.
/// </summary>
/// <param name="File">The suite file, as a path in the vendored tree (<c>url/historical.any.js</c>).</param>
/// <param name="TestName">
/// The exact name the suite gives the test, or — when the name embeds data and a whole family diverges for
/// one reason — a glob in which <c>*</c> matches any run of characters. A glob keeps a table of two hundred
/// mechanically generated names readable, and it is safe because the driver holds every entry to the same
/// rule: it must match at least one failing test and no passing one, so a glob can never widen into a
/// blanket over cases that work.
/// </param>
/// <param name="Divergence">Which category of not-passing this is.</param>
/// <param name="Platform">
/// The one operating system this entry applies on, or <see langword="null"/> — almost always null — for an
/// entry that applies everywhere. A platform-scoped entry exists for the case where the <i>platform's</i>
/// crypto draws its limits differently per OS: Apple's AES-GCM takes only a 128-bit tag where CNG and
/// OpenSSL take 96 to 128 bits, so the sub-128-bit-tag rows pass on Windows and Linux and fail on macOS —
/// no platform-neutral entry can name them without covering passing tests somewhere. On any other OS a
/// scoped entry is invisible: it excludes nothing and the staleness rule does not ask it to match, so the
/// discipline stays exact on every leg rather than being loosened to their union.
/// </param>
/// <param name="ExceptPlatform">
/// The one operating system this entry does <b>not</b> apply on, or <see langword="null"/>. The mirror image
/// of <paramref name="Platform"/>, for a divergence that is everywhere <i>but</i> one platform.
/// <para>
/// <b>No entry uses it today</b>, and the one that did is worth recording because it is the shape that will
/// want it again. Before https://github.com/sebastienros/jint/issues/3179, AES-GCM's
/// <c>decryption … during call</c> rows failed on Windows and Linux for the copy order and <i>passed</i> on
/// macOS, where the platform's tag refusal produced the very <c>OperationError</c> the test asserts — for the
/// wrong reason, which the assertion cannot see. A platform-neutral entry would have been stale on that one
/// leg, so it excused itself from it. The fix removed the divergence rather than the mechanism.
/// </para>
/// </param>
internal sealed record WptExclusion(
    string File,
    string TestName,
    WptDivergence Divergence,
    System.Runtime.InteropServices.OSPlatform? Platform = null,
    System.Runtime.InteropServices.OSPlatform? ExceptPlatform = null)
{
    internal bool Matches(string testName) => MatchesPattern(TestName, testName);

    /// <summary>Whether this entry participates on the operating system the run is on.</summary>
    internal bool AppliesOnThisPlatform =>
        (Platform is not { } platform || System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(platform))
        && (ExceptPlatform is not { } excluded || !System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(excluded));

    /// <summary>
    /// Whether <paramref name="value"/> is what <paramref name="pattern"/> names: an ordinal match, unless
    /// the pattern carries a <c>*</c>, which stands for any run of characters. Also what the not-vendored
    /// table is checked with, since that is the same question asked about a path.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Iterative rather than recursive, so a pattern with several stars cannot blow the stack on a long
    /// name — the URL corpus builds test names out of its inputs and some of those are long.
    /// </para>
    /// <para>
    /// <c>\*</c> is a literal asterisk, which some test <i>names</i> genuinely contain:
    /// <c>fetch/api/basic/accept-header.any.js</c> has a row called "…with value '*/*'" beside one called
    /// "…with value 'custom/*'", and without an escape there is no pattern at all that names the first and
    /// not the second — every literal segment of the one is a segment of the other, so the wildcard reading
    /// makes them indistinguishable and the two-sided rule (match a failing test, match no passing one) can
    /// never be satisfied. No other escape is recognized, and a lone backslash is a lone backslash.
    /// <b>No entry spells it today</b>: that pair is exactly the case
    /// https://github.com/sebastienros/jint/issues/3279 fixed, so both rows pass and the exclusion that
    /// needed the escape is gone. <c>WptCorpusTests.AnExclusionMatchesExactlyWhatItsPatternSays</c> holds the
    /// escape instead, so it stays correct for the next name that carries a star.
    /// </para>
    /// </remarks>
    internal static bool MatchesPattern(string pattern, string value)
    {
        if (!pattern.Contains('*', StringComparison.Ordinal))
        {
            return string.Equals(pattern, value, StringComparison.Ordinal);
        }

        int p = 0, v = 0, starPattern = -1, starValue = 0;

        while (v < value.Length)
        {
            if (p < pattern.Length && IsWildcard(pattern, p))
            {
                starPattern = p++;
                starValue = v;
            }
            else if (p < pattern.Length && LiteralAt(pattern, p) == value[v])
            {
                p += WidthAt(pattern, p);
                v++;
            }
            else if (starPattern >= 0)
            {
                p = starPattern + 1;
                v = ++starValue;
            }
            else
            {
                return false;
            }
        }

        while (p < pattern.Length && IsWildcard(pattern, p))
        {
            p++;
        }

        return p == pattern.Length;

        static bool IsWildcard(string pattern, int at)
            => pattern[at] == '*' && (at == 0 || pattern[at - 1] != '\\');

        static bool IsEscapedStar(string pattern, int at)
            => pattern[at] == '\\' && at + 1 < pattern.Length && pattern[at + 1] == '*';

        static char LiteralAt(string pattern, int at) => IsEscapedStar(pattern, at) ? '*' : pattern[at];

        static int WidthAt(string pattern, int at) => IsEscapedStar(pattern, at) ? 2 : 1;
    }
}
#endif
