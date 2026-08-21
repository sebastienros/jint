[![Build](https://github.com/sebastienros/jint/actions/workflows/build.yml/badge.svg)](https://github.com/sebastienros/jint/actions/workflows/build.yml)
[![NuGet](https://img.shields.io/nuget/v/Jint.svg)](https://www.nuget.org/packages/Jint)
[![NuGet](https://img.shields.io/nuget/vpre/Jint.svg)](https://www.nuget.org/packages/Jint)
[![MyGet](https://img.shields.io/myget/jint/vpre/jint.svg?label=MyGet)](https://www.myget.org/feed/jint/package/nuget/Jint)
[![Join the chat at https://gitter.im/sebastienros/jint](https://badges.gitter.im/Join%20Chat.svg)](https://gitter.im/sebastienros/jint)

# Jint

Jint is a __Javascript interpreter__ for .NET which can run on __any modern .NET platform__ as it supports .NET Standard 2.0 and .NET 4.6.2 targets (and later).

## Use cases and users

- Run JavaScript inside your .NET application in a safe sand-boxed environment
- Expose native .NET objects and functions to your JavaScript code (get database query results as JSON, call .NET methods, etc.)
- Support scripting in your .NET application, allowing users to customize your application using JavaScript (like Unity games) 

Some users of Jint include 
[RavenDB](https://github.com/ravendb/ravendb), 
[EventStore](https://github.com/EventStore/EventStore), 
[OrchardCore](https://github.com/OrchardCMS/OrchardCore), 
[ELSA Workflows](https://github.com/elsa-workflows/elsa-core),
[docfx](https://github.com/dotnet/docfx), 
[JavaScript Engine Switcher](https://github.com/Taritsyn/JavaScriptEngineSwitcher),
and many more.

## Supported features

#### ECMAScript 2015 (ES6)

- ✔ ArrayBuffer
- ✔ Arrow function expression
- ✔ Binary and octal literals
- ✔ Class support
- ✔ DataView
- ✔ Destructuring
- ✔ Default, rest and spread
- ✔ Enhanced object literals
- ✔ `for...of`
- ✔ Generators
- ✔ Template strings
- ✔ Lexical scoping of variables (let and const)
- ✔ Map and Set
- ✔ Modules and module loaders
- ✔ Promises (Experimental, API is unstable)
- ✔ Reflect
- ✔ Proxies
- ✔ Symbols
- ✔ Proper tail calls in strict functions
- ✔ Typed arrays
- ✔ Unicode
- ✔ Weakmap and Weakset

Proper tail calls replace the calling strict-function frame, as required by ECMAScript. Consequently,
intermediate tail callers are intentionally absent from `error.stack` and host stack telemetry.

#### ECMAScript 2016

- ✔ `Array.prototype.includes`
- ✔ `await`, `async`
- ✔ Block-scoping of variables and functions
- ✔ Exponentiation operator `**`
- ✔ Destructuring patterns (of variables)

####  ECMAScript 2017

- ✔ `Object.values`, `Object.entries` and `Object.getOwnPropertyDescriptors`
- ✔ Shared memory and atomics

#### ECMAScript 2018

- ✔ Asynchronous iteration
- ✔ `Promise.prototype.finally`
- ✔ RegExp named capture groups
- ✔ Rest/spread operators for object literals (`...identifier`)
- ✔ SharedArrayBuffer

#### ECMAScript 2019

- ✔ `Array.prototype.flat`, `Array.prototype.flatMap`
- ✔ `String.prototype.trimStart`, `String.prototype.trimEnd`
- ✔ `Object.fromEntries`
- ✔ `Symbol.description`
- ✔ Optional catch binding

#### ECMAScript 2020

- ✔ `BigInt`
- ✔ `export * as ns from`
- ✔ `for-in` enhancements
- ✔ `globalThis` object
- ✔ `import`
- ✔ `import.meta`
- ✔ Nullish coalescing operator (`??`)
- ✔ Optional chaining
- ✔ `Promise.allSettled`
- ✔ `String.prototype.matchAll`

#### ECMAScript 2021

- ✔ Logical Assignment Operators (`&&=` `||=` `??=`)
- ✔ Numeric Separators (`1_000`)
- ✔ `AggregateError`
- ✔ `Promise.any` 
- ✔ `String.prototype.replaceAll`
- ✔ `WeakRef` 
- ✔ `FinalizationRegistry`

#### ECMAScript 2022

- ✔ Class Fields
- ✔ RegExp Match Indices
- ✔ Top-level await
- ✔ Ergonomic brand checks for Private Fields
- ✔ `.at()`
- ✔ Accessible `Object.prototype.hasOwnProperty` (`Object.hasOwn`)
- ✔ Class Static Block
- ✔ Error Cause

#### ECMAScript 2023

- ✔ Array find from last
- ✔ Change Array by copy
- ✔ Hashbang Grammar
- ✔ Symbols as WeakMap keys

#### ECMAScript 2024

- ✔ ArrayBuffer enhancements - `ArrayBuffer.prototype.resize` and `ArrayBuffer.prototype.transfer`
- ✔ `Atomics.waitAsync` 
- ✔ Ensuring that strings are well-formed - `String.prototype.ensureWellFormed` and `String.prototype.isWellFormed`
- ✔ Grouping synchronous iterables - `Object.groupBy` and `Map.groupBy`
- ✔ `Promise.withResolvers`
- ✔ Regular expression flag `/v`

#### ECMAScript 2025

- ✔ 16-bit floating point numbers (float16), Requires NET 8 or higher, `Float16Array`, `Math.f16round()`
- ✔ Array.fromAsync
- ✔ Import attributes
- ✔ Iterator helper methods 
- ✔ JSON modules
- ✔ `Promise.try`
- ✔ `RegExp.escape()`
- ✔ Regular expression pattern modifiers (inline flags)
- ✔ Duplicate named capture groups
- ✔ Set methods (`intersection`, `union`, `difference`, `symmetricDifference`, `isSubsetOf`, `isSupersetOf`, `isDisjointFrom`)

#### ECMAScript proposals (no version yet)

- ✔ Await Dictionary (`Promise.allKeyed`, `Promise.allSettledKeyed`)
- ✔ Decorators (`@decorator` syntax for classes, methods, fields, and accessors)
- ✔ `Error.isError`
- ✔ `Error.prototype.stack` accessor (error-stack-accessor)
- ✔ Explicit Resource Management (`using` and `await using`)
- ✔ Immutable Arraybuffers
- ✔ Import Bytes (`import x from './file' with { type: 'bytes' }`)
- ✔ Iterator Chunking (`Iterator.prototype.chunks`, `Iterator.prototype.windows`)
- ✔ Iterator Includes (`Iterator.prototype.includes`)
- ✔ Iterator Join (`Iterator.prototype.join`)
- ✔ Iterator Sequencing
- ✔ Joint Iteration
- ✔ JSON.parse source text access
- ✔ `Math.sumPrecise`
- ✔ `ShadowRealm`
- ✔ `Temporal`
- ✔ `Uint8Array` to/from base64
- ✔ `Upsert`

#### Other

- Further refined .NET CLR interop capabilities
- Constraints for execution (recursion, memory usage, duration)


## Web APIs (opt-in)

Beyond ECMAScript, Jint is growing a WHATWG-faithful set of web platform APIs — the surface a script author
expects from a browser or from Node: `console`, timers, `TextEncoder`, `fetch` and friends. It follows the
published standards (`console.spec.whatwg.org`, `webidl.spec.whatwg.org`, `fetch.spec.whatwg.org`, …) rather
than inventing a Jint-shaped variant, so scripts written against those standards behave the way their authors
expect.

**Everything here is opt-in and nothing is installed by default.** An engine that does not ask for these APIs
is byte-for-byte the engine it was before they existed: no extra globals, no extra work at construction, no
behaviour change. That is deliberate — an engine embedded in a workflow runner or a template renderer has no
business exposing them, and outbound network access in particular is a decision a host has to make
explicitly.

**Requires .NET 8 or higher.** The whole surface is compiled only for `net8.0` and later; on `net462`,
`netstandard2.0` and `netstandard2.1` the options and types simply do not exist.

```csharp
// Everything except network access.
var engine = new Engine(options => options.UseWebApis());

// Or exactly what you want, with somewhere for console output to go.
engine = new Engine(options => options
    .UseWebApis(WebApiFeatures.Console)
    .UseConsole(Console.Out));

engine.Execute("console.log('hello %s', 'world')");
```

`console` output goes to a `ConsoleSink`, which defaults to `ConsoleSink.Null` and discards everything —
enabling the feature never starts writing to your process's standard output by surprise. `ConsoleSink.FromTextWriter`
covers the common case; implement the abstract class to route records to your own logger, where the
`ConsoleLogLevel` tells you how loud each one is. Every method emits exactly one `Write` call per record, so a
`console.table(rows)` reaches your sink as one multi-line ASCII table rather than as a line per row, and the
formatter never invokes a script-visible getter — an accessor renders as `[Getter]`, because a console must
not be a way to run arbitrary script.

A global you registered yourself always wins: the install is non-clobbering, so if your host already exposes
its own `console` (or any other name in the table below), enabling the feature leaves yours exactly as it is.

| API | Feature flag | Status |
| --- | --- | --- |
| `console` (`log`/`warn`/`error`/`group`/`count`/`time`/`assert`/`trace`/`dir`/`table`) | `WebApiFeatures.Console` | ✔ shipped |
| `DOMException` / `QuotaExceededError` | *(no flag — installed whenever any feature is enabled)* | ✔ shipped |
| `setTimeout` / `setInterval` / `clearTimeout` / `clearInterval` / `queueMicrotask` | `WebApiFeatures.Timers` | ✔ shipped |
| `TextEncoder` (UTF-8) / `TextDecoder` (UTF-8, UTF-16LE/BE, every legacy single-byte encoding, `x-user-defined`; `fatal`, `ignoreBOM`, streaming) | `Encoding` | ✔ shipped |
| `atob` / `btoa` | `Base64` | ✔ shipped |
| `structuredClone` (incl. `{ transfer }` of `ArrayBuffer`s, `MessagePort`s and streams) | `StructuredClone` | ✔ shipped |
| `crypto.getRandomValues` / `crypto.randomUUID` / `crypto.subtle` (SHA digests, HMAC, AES-GCM, RSA, ECDSA/ECDH, HKDF, PBKDF2, `CryptoKey`) | `WebApiFeatures.Crypto` | ✔ shipped |
| `performance.now` / `timeOrigin` / `mark` / `measure` / `getEntries*` / `clearMarks` / `clearMeasures` | `WebApiFeatures.Performance` | ✔ shipped |
| `Event` / `EventTarget` / `CustomEvent` / `AbortController` / `AbortSignal` | `Events` | ✔ shipped |
| `URL` / `URLSearchParams` / `URLPattern` | `Url` | ✔ shipped |
| `Blob` (incl. `stream()`) / `File` / `FormData` | `Files` | ✔ shipped |
| `navigator.userAgent` | `Navigator` | ✔ shipped |
| `ReadableStream` / `WritableStream` / `TransformStream` (all three transferable) / `ByteLengthQueuingStrategy` / `CountQueuingStrategy` | `Streams` | ✔ shipped |
| `TextEncoderStream` / `TextDecoderStream` | `Encoding` **and** `Streams` | ✔ shipped |
| `CompressionStream` / `DecompressionStream` (`gzip`, `deflate`, `deflate-raw`) | `Compression` **and** `Streams` | ✔ shipped |
| `scheduler.postTask` / `scheduler.yield` / `TaskController` / `TaskSignal` | `Scheduler` | ✔ shipped |
| `requestIdleCallback` / `cancelIdleCallback` / `IdleDeadline` | `IdleCallback` | ✔ shipped |
| `MessageChannel` / `MessagePort` / `MessageEvent` (incl. cross-engine ports and port transfer) / `BroadcastChannel` | `Messaging` | ✔ shipped |
| `Worker` (module workers only, over a host-supplied `WorkerProvider`) | `Workers` — **opt-in on its own, and does nothing without a provider** | ✔ shipped |
| `reportError` (and the `DiagnosticsSink` behind it) | `Reporting` | ✔ shipped |
| `addEventListener` / `removeEventListener` / `dispatchEvent` / `self` on the global scope, with `ErrorEvent` / `PromiseRejectionEvent` and the `error` / `unhandledrejection` / `rejectionhandled` events | `GlobalEvents` | ✔ shipped |
| `localStorage` / `sessionStorage` / `Storage` | `Storage` *(not in `Default` — see below)* | ✔ shipped |
| `fetch` / `Headers` / `Request` / `Response` | `Fetch` — **opt-in on its own, see below** | ✔ shipped |
| `EventSource` / `MessageEvent` (server-sent events) | `EventSource` — **opt-in on its own, see below** | ✔ shipped |
| `WebSocket` / `CloseEvent` (and the `MessageEvent` its messages arrive as) | `WebSocket` — **opt-in on its own, see below** | ✔ shipped |
| `caches` / `Cache` / `CacheStorage` | `CacheApi` — **opt-in on its own, see below** | ✔ shipped |
| `FetchEvent` and `addEventListener('fetch', e => e.respondWith(…))` — script-facing request handling | `FetchEvents` — **opt-in on its own, see below** | ✔ shipped |

`WebApiFeatures.Default` — what `UseWebApis()` enables — is every non-network feature that has landed. It
grows as the table fills in, and it will never include `fetch`: network egress is always an explicit choice.
`Storage` is one standing exception, for the reason in its own section below; `CacheApi` is another, for the
neighbouring one — a cache outlives the evaluation that filled it, so where its data goes, and what bounds it,
is a decision you make rather than inherit; and `FetchEvents` is the third, because a script that can register
a `fetch` listener can take over every request you route into the engine. `Workers` is outside `Default` for a
different reason again: it needs a thread, and Jint never starts one — see its own section below.

`crypto.subtle` carries **all twelve operations** — `digest`, `sign`, `verify`, `encrypt`, `decrypt`,
`generateKey`, `importKey`, `exportKey`, `deriveBits`, `deriveKey`, `wrapKey` and `unwrapKey` — over
SHA-1/256/384/512 (for `digest` and as every keyed algorithm's inner hash), **HMAC**, the AES family
(**AES-CTR**, **AES-CBC**, **AES-GCM** and **AES-KW**, each at 128, 192 and 256 bits), the RSA family —
**RSASSA-PKCS1-v1_5** and **RSA-PSS** for signatures, **RSA-OAEP** for encryption — the elliptic curves
**P-256**, **P-384** and **P-521** under **ECDSA** and **ECDH**, and **HKDF** and **PBKDF2** for derivation.
Keys are real `CryptoKey` objects with `type`, `extractable`, `algorithm` and `usages`, and
`generateKey` for an asymmetric algorithm hands back a `CryptoKeyPair`, which is the plain
`{ privateKey, publicKey }` dictionary the specification defines rather than an interface. The key material
is never reachable from script except through `exportKey` on an extractable key: `raw` bytes or a
`kty: "oct"` JSON Web Key for a symmetric key, `spki`, `pkcs8` or a `kty: "RSA"` JSON Web Key for an RSA one,
and all four of those — `raw` being the uncompressed point `04||X||Y` — for an elliptic-curve one.

**The registries are per operation, and they are not symmetric.** HKDF and PBKDF2 register `importKey`,
`deriveBits` and the internal *get key length* and nothing else: there is nothing to `generateKey` (the key
*is* the password, or the input keying material you already have) and nothing to `exportKey`, their import
steps refusing any `extractable` that is not `false`. So a PBKDF2 key is a one-way door — the bytes go in and
only derived material comes out. ECDH registers `deriveBits` and never `sign`; ECDSA the reverse. And
**AES-KW registers `wrapKey` and `unwrapKey` and neither `encrypt` nor `decrypt`**, which is the exact
reverse of every other cipher: RFC 3394 wrapping takes a whole number of 64-bit blocks and carries an
integrity check of its own, so `encrypt({ name: 'AES-KW' }, …)` is a `NotSupportedError` here and in a
browser. That asymmetry is what `wrapKey`'s **double normalization** is for — the algorithm is normalized
for `wrapKey` and, if that fails, for `encrypt` — so `wrapKey(format, key, kek, 'AES-KW')` takes the first
route and `wrapKey(format, key, kek, { name: 'AES-GCM', iv })` (or AES-CBC, AES-CTR, RSA-OAEP) the second,
with one method call either way.

Wrapping *is* exporting, so `wrapKey` refuses a key that is not `extractable` with an `InvalidAccessError`
and needs the `wrapKey` usage on the wrapping key. `unwrapKey` is the mirror, and it is where `extractable`
and `keyUsages` for the *new* key are decided — which is what lets a server hand a script a key it can use
and cannot export, `ext: false` in the wrapped JWK being honoured on the way in. For the `jwk` format the
bytes wrapped are the UTF-8 of `JSON.stringify` of exactly the object `exportKey('jwk', key)` hands a script,
and under AES-KW they are padded with spaces before the closing brace to a multiple of 8 — the convention
every implementation follows and the web-platform tests compute their expectation with, which the
specification's own note permits ("implementations may choose to adapt the serialization to the constraints
of the wrapping algorithm").

`deriveKey` is a composition rather than an algorithm of its own: it derives bits with one algorithm and
imports them with another, so `deriveKey(pbkdf2Params, password, { name: 'AES-GCM', length: 256 }, …)` hands
back exactly the key `importKey('raw', …)` would have made from the same bytes. What it will not do is derive
an *asymmetric* key: RSA and the elliptic curves register `importKey` but not *get key length*, so a
`derivedKeyType` naming one is a `NotSupportedError` before anything is derived — which is what a browser
answers too. The `length` argument of `deriveBits` is optional and nullable, and the three algorithms read a
null differently: ECDH returns the whole shared secret (and truncates to a *bit*, not a byte, when you do
give a length), while HKDF and PBKDF2 have no natural output size and refuse it with an `OperationError`.
A length that is not a multiple of eight is likewise an `OperationError` for those two and a bit-exact
truncation for ECDH, and a length of zero is the empty `ArrayBuffer` for all three.

The two shapes an embedder actually reaches for are both one call each. A password becomes a content key —
`deriveKey({ name: 'PBKDF2', salt, iterations, hash: 'SHA-256' }, password, { name: 'AES-GCM', length: 256 },
false, ['encrypt', 'decrypt'])` — and two parties agree one, `deriveKey({ name: 'ECDH', public: theirPublicKey },
myPrivateKey, 'HKDF', false, ['deriveBits'])` followed by an HKDF expansion, which is the worked example the
specification itself gives. Together with the signature side that was already here, a script can now do the
whole of a JOSE flow in-engine: `HS*` under HMAC, `RS*` under RSASSA-PKCS1-v1_5, `PS*` under RSA-PSS and
`ES*` under ECDSA for signing and verification, and PBKDF2 or ECDH-plus-HKDF for the key that protects the
payload.

An ECDSA key carries
only its curve: the hash lives in the `EcdsaParams` of each `sign` and `verify` call, so one P-256 key signs
under SHA-256 and SHA-512 alike. Signatures are the raw `r || s` concatenation at the curve's field width
(64, 96 and 132 bytes) that the Web Cryptography API defines — not the DER `SEQUENCE` that .NET's
`ECDsa.SignData` produces when asked for `DSASignatureFormat.Rfc3279DerSequence`, so a host verifying a
script's signature with `ECDsa` must pass `IeeeP1363FixedFieldConcatenation`. An elliptic-curve JSON Web
Key's `x`, `y` and `d` are fixed-width, as RFC 7518 §6.2 requires and unlike RSA's minimal-length integers,
so a coordinate whose leading byte is zero keeps it; and an exported EC JWK carries no `alg`, exactly as the
export steps say, though `ES256`/`ES384`/`ES512` are honoured on the way in (`ES512` names **P-521** — the
number is the hash's).

Nothing here ever throws: a promise-returning WebIDL operation converts every failure into a rejection. An
algorithm that is not registered for the operation is a `NotSupportedError` `DOMException`, a key used
against its own `usages`, against another algorithm, or against the wrong half of its pair (signing with a
public key) is an `InvalidAccessError`, a malformed JWK or a DER structure that is not what its format names
is a `DataError`, a usage list an algorithm does not support is a `SyntaxError`, and anything an argument
conversion refuses is a `TypeError`. An AES-GCM, AES-CBC or RSA-OAEP decryption that does not come out
right, and an AES-KW unwrap whose integrity check fails, are each one `OperationError` carrying nothing
about which part of the input was wrong — a decrypt that could tell "the padding was malformed" from "the
padding was fine" is a padding oracle. The work is synchronous, so the promise is already settled when you
get it.

Where .NET's own cryptography is narrower than the specification the difference is visible, always as the
`OperationError` the algorithm's own steps end in and always with a message naming the restriction:

- **AES-GCM**: the `iv` must be the 96 bits NIST SP 800-38D recommends, and `tagLength` must be 96, 104,
  112, 120 or 128 — the specification also lists 32 and 64, which `System.Security.Cryptography.AesGcm` will
  not produce. The ciphertext is `ciphertext || tag`, exactly as the specification defines it, so a host
  reading a script's output with `AesGcm` splits the last `tagLength / 8` bytes off itself.
- **AES-KW**: the payload must be a whole number of 64-bit blocks — the specification's own step — and at
  least two of them, which is the wrapping algorithm's (NIST SP 800-38F §6.1 defines KW over `2 ≤ n`
  semiblocks). The BCL has no unpadded key wrap at all — .NET 10 added only the *padded* RFC 5649 variant,
  which is a different algorithm producing different bytes — so RFC 3394 is implemented here over the AES
  ECB one-shots and pinned against all six published test vectors of its Section 4.
- **AES-CTR**: .NET has no counter mode either, so the keystream is built from ECB over successive counter
  blocks. The `length` member is the width of the counter field in bits and the rest of the block is nonce,
  so the field wraps modulo 2^`length` rather than carrying into the nonce — which the specification
  requires and which is pinned against expectations computed outside the engine.
- **RSA-PSS**: `RSASignaturePadding.Pss` takes no salt-length parameter and always uses the hash's own
  output length, so `saltLength` is accepted at that one value (20, 32, 48 or 64 for SHA-1/256/384/512) and
  refused at any other rather than signing with a salt nobody asked for.
- **RSA-OAEP**: `RSAEncryptionPadding` carries a hash and no label, so `label` must be absent or empty.
- **RSA `generateKey`**: `publicExponent` must be 65537, which is the only exponent `RSA.Create` can be
  asked for, and `modulusLength` must be one the platform will generate (512 to 16384 in steps of 64 on
  Windows, of 8 elsewhere) and at most 8192, which is this engine's own ceiling on a prime search that no
  execution constraint can interrupt.
- **RSA `importKey`** from a JSON Web Key needs the CRT parameters `p`, `q`, `dp`, `dq` and `qi`, which
  `RSAParameters` describes a private key by; a JWK carrying `d` alone is a `DataError`.
- **PBKDF2 `deriveBits`**: `iterations` must be at most **4,194,304** (2^22). The algorithm has no ceiling
  of its own, but PBKDF2 is a loop whose only purpose is to be slow, whose trip count comes straight from
  script, and which happens inside one BCL call — so `iterations: 2 ** 40` is one line of script that no
  timeout, statement budget or cancellation token can interrupt. The cap is above every OWASP 2023
  recommendation for the function (1,300,000 for SHA-1, 600,000 for SHA-256, 210,000 for SHA-512) and bounds
  a single call to roughly 1.7 seconds at the ceiling itself. It is the same reasoning as the 8192-bit
  ceiling on RSA key generation above.

`TextDecoder` understands every encoding the [Encoding Standard](https://encoding.spec.whatwg.org/#names-and-labels)
names, with all of their labels, except the seven legacy multi-byte ones (`Big5`, `EUC-JP`, `EUC-KR`, `GBK`,
`gb18030`, `ISO-2022-JP` and `Shift_JIS`). Those are recognized as labels and reported as an unsupported
encoding, never decoded as something else. The label table and the single-byte index tables are generated
from the standard's own data files; `TextEncoder` is UTF-8 only, as the standard requires.


### WinterTC's Minimum Common API, member by member

The surface above is guided by [WinterTC's Minimum Common Web Platform API](https://min-common-api.proposal.wintertc.org/),
the Ecma TC55 standard that names the slice of the web platform a non-browser runtime should carry. The two
tables below are that standard's own index — §5.1 *Common interfaces* and §5.2 *Common methods and
properties* of the 2025 snapshot — one row per member, against the `WebApiFeatures` flag that provides it.
**Every flag named is part of `WebApiFeatures.Default` unless the row says otherwise**, so a bare
`UseWebApis()` gets you the whole list bar the rows that say it does not.

Five entries need more than a flag name, stated here rather than left to be discovered:

* **`WebAssembly.*`** — 16 of the members below, and a recorded decline rather than a to-do. Implementing it
  means shipping a *second* virtual machine beside the first: a decoder, a validator and an execution engine
  for a bytecode a tree-walking AST interpreter shares nothing with, since Jint deliberately has no bytecode
  and no code generation of its own. It would be larger than the ECMAScript implementation it stood next to
  and would reuse none of it, so it is absent and will stay absent.
* **`Crypto`, `SubtleCrypto` and `Performance`** — the *interface objects*. `crypto`, `crypto.subtle` and
  `performance` are all there and carry every member the standards give them, but as objects whose members
  are their own rather than an interface prototype's, so there is no constructor to name and
  `x instanceof Crypto` cannot be written. That is a documented simplification (the same one `console`
  carries), and what a script can otherwise observe is unchanged — `Object.keys(crypto)` answers the empty
  array in a browser too, because there the members live one level up.
* **Eight of the Streams interfaces** — the two readers, the writer, the four controllers and
  `ReadableStreamBYOBRequest`.
  These are implemented and their interface objects are real: a reader's `constructor` is the genuine thing
  and `new` on it behaves as the standard says. They are simply not installed on `globalThis`, which is the
  deliberate reduction described under [Streams](#streams-including-byte-streams).
* **`onerror`, `onunhandledrejection` and `onrejectionhandled`** — absent, and §6 *The global scope* is why:
  a runtime whose global object is not an `EventTarget` "shall not support" those three properties, and must
  instead fire the events through a suitable alternative mechanism. Jint's global is not an `EventTarget`
  (`GlobalEvents` puts `addEventListener` on it as an ordinary function bound to a synthetic target), so
  their absence is what conformance asks for rather than a gap. The same clause excuses a runtime from
  implementing `ErrorEvent` and `PromiseRejectionEvent`; those are here anyway, and so are the `error`,
  `unhandledrejection` and `rejectionhandled` events themselves.
* **`Headers`, `Request`, `Response` and `fetch()`** — present, but behind flags `Default` does not include,
  for the reason the whole section opens with: network egress is a grant a host makes rather than inherits.
  The three interfaces come with `Fetch`, `CacheApi` or `FetchEvents`; the function comes with `Fetch` alone.

§5.3 *Web workers* does not require a runtime to support them at all, and asks for `onerror`,
`onunhandledrejection`, `onrejectionhandled` and `self` on any global that *does* map to a
`WorkerGlobalScope`. An ordinary Jint global does not map to one, so only `self` applies there and the three
handlers are the §6 case above; a **worker's** global (`WebApiFeatures.Workers`, above) maps to one and
carries all four, with `onerror` in HTML's legacy five-argument shape.

**§5.1 Common interfaces**

| Member | Defined by | Provided by |
| --- | --- | --- |
| `AbortController` | DOM | `Events` |
| `AbortSignal` | DOM | `Events` |
| `Event` | DOM | `Events` |
| `EventTarget` | DOM | `Events` |
| `CustomEvent` | DOM | `Events` |
| `ErrorEvent` | HTML | `GlobalEvents` |
| `MessageChannel` | HTML | `Messaging` |
| `MessageEvent` | HTML | `Messaging` (also `EventSource`, `WebSocket`) |
| `MessagePort` | HTML | `Messaging` |
| `PromiseRejectionEvent` | HTML | `GlobalEvents` |
| `DOMException` | WebIDL | *no flag — installed whenever any feature is* |
| `Headers` | Fetch | `Fetch`, `CacheApi` or `FetchEvents` — **not in `Default`** |
| `Request` | Fetch | `Fetch`, `CacheApi` or `FetchEvents` — **not in `Default`** |
| `Response` | Fetch | `Fetch`, `CacheApi` or `FetchEvents` — **not in `Default`** |
| `FormData` | XHR | `Files` |
| `Blob` | File API | `Files` |
| `File` | File API | `Files` |
| `CompressionStream` | Compression | `Compression` **and** `Streams` |
| `DecompressionStream` | Compression | `Compression` **and** `Streams` |
| `ByteLengthQueuingStrategy` | Streams | `Streams` |
| `CountQueuingStrategy` | Streams | `Streams` |
| `ReadableStream` | Streams | `Streams` |
| `TransformStream` | Streams | `Streams` |
| `WritableStream` | Streams | `Streams` |
| `ReadableByteStreamController` | Streams | `Streams` — implemented, **not a global** |
| `ReadableStreamBYOBReader` | Streams | `Streams` — implemented, **not a global** |
| `ReadableStreamBYOBRequest` | Streams | `Streams` — implemented, **not a global** |
| `ReadableStreamDefaultController` | Streams | `Streams` — implemented, **not a global** |
| `ReadableStreamDefaultReader` | Streams | `Streams` — implemented, **not a global** |
| `TransformStreamDefaultController` | Streams | `Streams` — implemented, **not a global** |
| `WritableStreamDefaultController` | Streams | `Streams` — implemented, **not a global** |
| `WritableStreamDefaultWriter` | Streams | `Streams` — implemented, **not a global** |
| `TextDecoder` | Encoding | `Encoding` |
| `TextEncoder` | Encoding | `Encoding` |
| `TextDecoderStream` | Encoding | `Encoding` **and** `Streams` |
| `TextEncoderStream` | Encoding | `Encoding` **and** `Streams` |
| `URL` | URL | `Url` |
| `URLSearchParams` | URL | `Url` |
| `URLPattern` | URL Pattern | `Url` |
| `Crypto` | Web Crypto | **absent** — `crypto` has no interface object |
| `CryptoKey` | Web Crypto | `Crypto` |
| `SubtleCrypto` | Web Crypto | **absent** — `crypto.subtle` has no interface object |
| `Performance` | HR-Time | **absent** — `performance` has no interface object |
| `WebAssembly.Global` | Wasm JS API | **absent** — declined |
| `WebAssembly.Instance` | Wasm JS API | **absent** — declined |
| `WebAssembly.Memory` | Wasm JS API | **absent** — declined |
| `WebAssembly.Module` | Wasm JS API | **absent** — declined |
| `WebAssembly.Table` | Wasm JS API | **absent** — declined |
| `WebAssembly.Tag` | Wasm JS API | **absent** — declined |
| `WebAssembly.Exception` | Wasm JS API | **absent** — declined |
| `WebAssembly.CompileError` | Wasm JS API | **absent** — declined |
| `WebAssembly.LinkError` | Wasm JS API | **absent** — declined |
| `WebAssembly.RuntimeError` | Wasm JS API | **absent** — declined |

**§5.2 Common methods and properties**

| Member | Defined by | Provided by |
| --- | --- | --- |
| `globalThis` | ECMAScript | the language — always there |
| `atob()` | HTML | `Base64` |
| `btoa()` | HTML | `Base64` |
| `clearTimeout()` | HTML | `Timers` |
| `clearInterval()` | HTML | `Timers` |
| `navigator.userAgent` | HTML | `Navigator` |
| `onerror` | HTML | **absent** — §6 asks for that; see above |
| `onunhandledrejection` | HTML | **absent** — §6 asks for that; see above |
| `onrejectionhandled` | HTML | **absent** — §6 asks for that; see above |
| `queueMicrotask()` | HTML | `Timers` |
| `reportError()` | HTML | `Reporting` |
| `self` | HTML | `GlobalEvents` |
| `setTimeout()` | HTML | `Timers` |
| `setInterval()` | HTML | `Timers` |
| `structuredClone()` | HTML | `StructuredClone` |
| `fetch()` | Fetch | `Fetch` — **not in `Default`** |
| `console` | Console | `Console` |
| `crypto` | Web Crypto | `Crypto` |
| `performance` | HR-Time | `Performance` |
| `WebAssembly.compile()` | Wasm JS API | **absent** — declined |
| `WebAssembly.compileStreaming()` | Wasm Web API | **absent** — declined |
| `WebAssembly.instantiate()` | Wasm JS API | **absent** — declined |
| `WebAssembly.instantiateStreaming()` | Wasm Web API | **absent** — declined |
| `WebAssembly.JSTag` | Wasm JS API | **absent** — declined |
| `WebAssembly.validate()` | Wasm JS API | **absent** — declined |

§7 asks that `navigator.userAgent` be a single opaque RFC 7231 product token identifying the runtime; Jint
answers `Jint/<version>`, which `Options.WebApi` does not let you change — a script that branches on the
runtime should get the truth.

### Enabling web APIs on an engine that already exists

`options.WebApi.Features` is read once, when the engine is built, so a pooled engine's feature set is fixed at
construction — which is awkward for a host that only discovers *per request* what the script it is about to
run needs. `engine.Advanced.EnableWebApis(...)` is the per-engine counterpart of `options.UseWebApis(...)`,
for exactly that case:

```csharp
var engine = pool.Rent();                       // built with WebApiFeatures.Console only

if (script.NeedsTimers)
{
    engine.Advanced.EnableWebApis(WebApiFeatures.Timers | WebApiFeatures.Events);
}

if (tenant.MayReachTheNetwork)
{
    // The optional delegate is handed this engine's own options group, so a feature that carries settings
    // can be configured at the moment it is enabled.
    engine.Advanced.EnableWebApis(WebApiFeatures.Fetch, w =>
        w.Fetch.UrlFilter = uri => tenant.Allows(uri));
}

// What the engine actually carries, after the feature closure has been expanded.
WebApiFeatures live = engine.Advanced.WebApiFeatures;   // Console | Timers | Events | Fetch | Url | Files | Streams
```

It runs the same code construction runs — the same feature closure, the same per-engine state, the same lazy,
non-clobbering globals — so an engine enabled this way is the engine `options.UseWebApis(...)` would have
built. Four things are worth knowing:

* **It is additive only.** There is no way to turn a web API off again; a script that already captured
  `fetch` would keep it whatever a later call said. Naming a feature the engine already has is a plain no-op —
  not an error, not a re-install — and the return value tells you what actually changed. A global you
  registered yourself still wins, and the existence check *probes*, so your own lazy global is not built
  merely by our looking.
* **Network access is still only ever granted by name.** No feature closure pulls in `Fetch`, `EventSource`
  or `WebSocket`; asking for `WebApiFeatures.Default` here grants exactly what it grants on the options.
* **Settings come from this engine's `Options`, read at the moment of the call**, with the same read-once
  semantics they have at construction. The delegate is handed that same group — which, exactly as at options
  time, is shared with every other engine built from the same `Options` instance, so give an engine its own
  `Options` when its network policy has to be its own. Two settings are deliberately *not* re-read for an
  engine that already has web-API state: `Timers.TimeProvider`, because the timers, `performance.now()` and
  the time origin have to stay on one clock for the engine's life, and `Diagnostics.Sink`, which also decides
  whether a callback's exception erupts.
* **`RestoreGlobalSnapshot` removes globals installed after the capture**, exactly as it does for
  `AddLazyGlobal` and `SetFetchHandler` — and it does *not* revert the engine's feature record, so the engine
  is left knowing about an API whose globals script can no longer name, and re-calling `EnableWebApis` is a
  no-op that cannot bring them back. Enable **before** you capture the snapshot you reuse, or enable on the
  `Options` so the globals are part of the engine's initial state.

As at options time, only the principal realm is touched: a `ShadowRealm` still gets none of it.

### Timers fire only while the engine is being pumped

**Jint never starts a thread, and never a `System.Threading.Timer`, to run your script.** A `setTimeout`
callback runs on the first drain of the event loop at or after its due time, and a drain only happens where
one always happened: at the end of an `Execute`/`Evaluate`, inside a blocking `UnwrapIfPromise`, while
`await`ing `EvaluateAsync`, or when the host calls `engine.Advanced.ProcessTasks()` itself.

```csharp
var engine = new Engine(options => options.UseWebApis());

// Blocking: the drain waits out the timer and returns 42.
var answer = engine.Evaluate("(async () => { await new Promise(r => setTimeout(r, 100)); return 42; })()")
    .UnwrapIfPromise();

// Or without holding a thread.
answer = await engine.EvaluateAsync("(async () => { await new Promise(r => setTimeout(r, 100)); return 42; })()");

// Or from your own loop — a game loop, a message pump — where every turn provably runs on your thread.
engine.Execute("setTimeout(() => console.log('later'), 50);");
while (running) { engine.Advanced.ProcessTasks(); Thread.Sleep(5); }
```

An engine nobody pumps never fires a timer, which is what makes this safe in a request handler that returns
as soon as the script does — a scheduled callback cannot surprise you later, on a thread you did not expect,
against state you have moved on from. The corollary is that a timer outlasting the wait around it is simply
never reached: `new Promise(r => setTimeout(r, 15000))` under the default ten-second `UnwrapIfPromise` times
out, by design.

The engine's single job queue *is* the microtask queue: a due timer is promoted onto it only once it has run
dry, so `Promise.resolve().then(f)` always beats `setTimeout(g, 0)`, each timer gets its own microtask
checkpoint, and no interval can starve promise reactions. Timers are scheduled against
`Options.WebApi.Timers.TimeProvider`, so handing the engine a fake clock makes a suite that exercises them
deterministic and instant; `MaxActiveTimers` (1000 by default) bounds how many a script may register at once
and turns the excess into a catchable `QuotaExceededError` carrying the cap as `quota` and the count that
would have been reached as `requested`. A callback that throws erupts out
of whatever was pumping — the same contract a promise reaction has — and the rest of the queue runs on the
next pump, unless you installed a `DiagnosticsSink`, in which case it is reported to that instead and the
pump carries on.

### Prioritized tasks: `scheduler.postTask` and `scheduler.yield`

`scheduler.postTask(callback, { priority, signal, delay })` runs a callback as its own task and hands back a
promise for what it returned; `scheduler.yield()` hands back a promise that resolves in a fresh *continuation*
task, which is how a long piece of work breaks itself up and still resumes ahead of the work queued behind it.
Priorities are the standard's three — `user-blocking` > `user-visible` (the default) > `background` — and a
`TaskController` can abort a batch of tasks or reprioritize the ones still waiting, firing `prioritychange` at
its `TaskSignal` as it does. Neither method ever throws: a bad argument, an aborted signal or a callback that
throws all become a rejection of the promise you were handed.

Tasks run only while the engine is being pumped, exactly as timers do, and a `delay` rides the very same timer
queue. Three ordering guarantees, which the tests pin: every microtask runs before the next task, whichever
order the two were queued in; among the tasks pending together the highest priority wins, ties going to the
oldest; and every runnable task runs before any *due* timer — the one place this deliberately differs from a
browser, which is free to make the opposite choice for a `background` task and typically does.

### Events dispatch to one target, because there is no tree

`EventTarget` is constructible and `Event`, `CustomEvent`, `AbortController` and `AbortSignal` behave as the
DOM standard describes them, with one reduction: the specification's dispatch walks an *event path* built
from the target's ancestors in a node tree, and Jint has no node tree. The path is therefore always the
single item «target» — which is exactly what the algorithm produces for the tree-less `EventTarget`s the
standard itself says author code creates. Everything that survives that reduction is the real algorithm:
`capture` decides which of the two passes a listener runs in, `stopPropagation()` ends the dispatch after the
current pass and `stopImmediatePropagation()` also skips the rest of it, `once` listeners are removed before
they run, a duplicate `(type, callback, capture)` registration is ignored, and `removeEventListener` matches
on the object identity your script passed.

**A listener that throws erupts from `dispatchEvent` — unless you set a `DiagnosticsSink`.** The standard
says to *report* the exception and carry on to the next listener, which needs somewhere to report to.
Install a sink and that is exactly what happens; without one, swallowing the exception would lose it
entirely, so it propagates instead — the same contract a timer callback has. The dispatch state is unwound
either way, so the event and the target stay usable.

The global scope has its own listener list, behind `WebApiFeatures.GlobalEvents` — see [global `error`
events](#and-script-can-watch-them-too-global-error-events) below.

`AbortSignal.timeout(ms)` schedules on the same queue the timers use, so it aborts only while the engine is
being pumped and it counts against `MaxActiveTimers`; that queue exists whenever the `Events` feature does,
whether or not you also asked for `setTimeout`. `AbortSignal.any(signals)` builds a composite that is
retained by its sources until one of them aborts, at which point every link is dropped.

### The performance timeline is bounded

`performance.mark` and `performance.measure` behave as [User Timing](https://w3c.github.io/user-timing/)
describes them — the whole overload matrix, mark names or raw timestamps, `detail` deep-copied through the
same structured-clone algorithm `structuredClone` uses, and `PerformanceEntry` / `PerformanceMark` /
`PerformanceMeasure` as real interface objects so `entry instanceof PerformanceMark` works. Entries come back
from `getEntries()`, `getEntriesByType(type)` and `getEntriesByName(name, type?)` sorted by `startTime`, and
`clearMarks(name?)` / `clearMeasures(name?)` remove them.

```js
performance.mark('parse');
// ... work ...
const m = performance.measure('parse-to-now', 'parse');
console.log(m.duration);
```

One thing is deliberately not the browser's: the entry buffer holds **10,000 entries**, not an unbounded
number. A page's timeline dies with the page, and an engine embedded in a long-lived host has no such event —
`while (true) performance.mark('x')` would otherwise be a memory leak that no execution constraint describes.
Once the buffer is full, further entries are dropped exactly as the Performance Timeline standard says a full
buffer drops them: nothing throws, `mark()` and `measure()` still return the entry they built, and
`getEntries()` simply stops growing until you clear it. There is no `PerformanceObserver`, and it is absent
rather than present-and-throwing so feature detection takes its fallback path. Readings are **not coarsened** —
an embedded engine has no cross-origin data for a fine clock to help steal, and a host that wants a coarse one
supplies a coarse `TimeProvider`.

`navigator` carries `userAgent` and nothing else. It reports `Jint/<version>` — a single RFC 7231 product
token with no comment component, so nothing about your operating system or your application leaks into it —
and it is there because [WinterTC's Minimum Common API](https://min-common-api.proposal.wintertc.org/)
requires `globalThis.navigator.userAgent` of a conforming runtime. Everything else a browser's `Navigator`
carries describes a user agent with a user, a document and a network stack, so it is absent rather than faked.

### Channel messaging can span two engines

`new MessageChannel()` gives the usual entangled pair, and it behaves as the HTML Standard describes: a
message is structured-cloned *when it is posted* — so a `DataCloneError` is thrown synchronously at the
`postMessage` call and a later mutation of the value cannot reach the message — and delivered as an
event-loop task, so every already-queued promise reaction runs first. A port's message queue starts
**disabled**: nothing arrives until `start()` is called or `onmessage` is assigned, and
`addEventListener('message', …)` on its own does not start it. `{ transfer: [buffer] }` moves an
`ArrayBuffer` instead of copying it, exactly as `structuredClone` does.

**A `MessagePort` can be transferred too**, which is how a script hands one channel's end over another
channel:

```js
var side = new MessageChannel();
side.port1.onmessage = e => log('reply: ' + e.data);
side.port1.postMessage('queued before the handover');   // waits; nobody owns the far end yet

port.postMessage('here is a private channel', [side.port2]);
```

The named port is **detached**: `postMessage` on it becomes a silent no-op and no event will ever fire on it
again. Its *side* of the channel — including every message still queued on it, and every message the peer
posts while it is in transit — travels in the message and is re-entangled with a fresh `MessagePort` created
in the receiving realm, which arrives as `event.ports[0]`. The peer is untouched and never learns that the
far end moved, so a port can be relayed through as many engines as you like and still talk to the one that
created it. `structuredClone(port, { transfer: [port] })` does the same thing into the current realm.

The specification's refusals are implemented as written: transferring a port through *itself* is a
`DataCloneError`, naming one twice in a single transfer list or naming an already-closed or already-transferred
one is a `DataCloneError`, transferring the port you are posting *to* dooms the message (the transfer happens,
nothing is delivered, the channel is lost), and a port that is in the message but not in the transfer list is
a `DataCloneError` — a port is transferable, not serializable.

The same pair can also connect **two engines**, which is what makes a worker-style split possible without a
second process:

```csharp
var host = new Engine(o => o.UseWebApis());
var worker = new Engine(o => o.UseWebApis());

// Create the pair while neither engine is running, then give each half to its own engine.
var pair = host.Advanced.CreateMessagePortPair(worker);
host.SetValue("port", pair.Local);
worker.SetValue("port", pair.Remote);

worker.Execute("port.onmessage = e => port.postMessage(e.data.n * 2);");
host.Execute("port.onmessage = e => log(e.data); port.postMessage({ n: 21 });");

worker.Advanced.ProcessTasks();   // the worker's turn: it receives 21 and replies
host.Advanced.ProcessTasks();     // the host's turn: it receives 42
```

**No `JsValue` ever crosses.** `postMessage` serializes on the calling engine's thread into a record that
holds nothing engine-affine — primitives, byte arrays, lists, type tags — and enqueues a job on the receiving
engine's event loop, the one part of an engine any thread may touch. The deserialization, the `MessageEvent`
and the listeners all run on whichever thread pumps that engine, so nothing on the receiver is touched off
its own pump. The receiver must actually be pumped, exactly as for timers: an engine nobody pumps never
delivers a message. And a `RestoreGlobalSnapshot` on either engine ends that channel permanently — a port's
listeners are closures over the cycle it was created in — so a pooled engine wants a fresh pair per cycle.

Transferring a port works between engines too, and a transferred port is not tied to the two engines that
happened to move it: engine A can create a pair, send one end to B, have B forward that same end to C without
ever looking at it, and end up with A and C talking directly. The one thing that does cross in that case is
the channel *side* — a small object holding the message queue, a lock and the peer — which is the same handle
a sender has always held for its peer. A restore on the engine a transfer was in flight to **ends** that side
rather than leaving it waiting, so the other end stops queueing into something that can never be drained.

`BroadcastChannel` rides the same `Messaging` flag and is the same machinery addressed by *name* instead of by
peer. `new BroadcastChannel('room')` joins a name; `postMessage(value)` reaches every **other** channel of that
name — never the sender itself — as one event-loop task each, in the order the channels were created. There is
no `start()` and no message queue, so `addEventListener('message', …)` alone is enough to receive; there is no
transfer list either, since a message with several destinations has nowhere to move a buffer *to*. A
`postMessage` on a closed channel is an `InvalidStateError` `DOMException`, which is where it differs from a
closed port's, and `close()` is what takes the channel out of earshot.

Which channels are in earshot of each other is one object — a browser scopes it by agent cluster and origin,
and `BroadcastChannelBroker` is Jint's answer to both:

```csharp
var cluster = new BroadcastChannelBroker();

var producer = new Engine(o => { o.UseWebApis(); o.WebApi.Messaging.Broker = cluster; });
var consumer = new Engine(o => { o.UseWebApis(); o.WebApi.Messaging.Broker = cluster; });

consumer.Execute("new BroadcastChannel('jobs').onmessage = e => log(e.data);");
producer.Execute("new BroadcastChannel('jobs').postMessage({ id: 7 });");

consumer.Advanced.ProcessTasks();   // the consumer's turn: it receives { id: 7 }
```

Say nothing and each engine gets a private broker, so channels on one engine hear each other and nothing
crosses an engine boundary — including between two engines built from one shared `Options`, which has to be an
explicit act. A broker is thread-safe, and as with ports **no `JsValue` ever crosses**: one serialization record
is produced on the sender and each destination deserializes its own copy on its own pump. It holds its
subscribers strongly, exactly as a browser keeps a channel alive until it is closed, so a broker shared between
long-lived and short-lived engines wants the short-lived ones closed, restored or `Dispose()`d — a
`RestoreGlobalSnapshot` and an `Engine.Dispose` each end every channel that engine created.

### `new Worker()`, with the thread supplied by you

`WebApiFeatures.Workers` gives a script the `Worker` constructor. Everything a worker needs already exists —
a second isolated global is a second `Engine`, the channel between them is the cross-engine port pair above,
delivery on the receiver's own thread is its event loop — except the one thing that must not: **the thread**.
*Jint never starts a thread to run script*, so `new Worker()` is only implementable if you supply the
execution resource. That is what `WorkerProvider` is. The engine owns the specification-shaped parts — port
entanglement, the worker global, message and error plumbing, ordering, `terminate()` semantics — and you own
every thread, every pump, and the worker engine's configuration.

This is the ordinary layering rather than a Jint improvisation: a browser *is* a host-supplied worker factory,
with Chromium in the provider's role, and Deno, QuickJS, Moddable and GraalJS all draw the line in the same
place. What is new here is that the boundary is a public extension point, which is the right consequence of
Jint being a library and not a runtime.

```csharp
var engine = new Engine(options => options.UseWebApis().UseWorkers(new ThreadPerWorker(workerRoot)));

engine.Execute("""
    const w = new Worker('./crunch.js', { type: 'module' });
    w.onmessage = e => log(e.data);
    w.postMessage({ rows: 10000 });
    """);
```

`UseWorkers` sets the flag and the provider together so the two cannot get out of step. **With no provider the
global is not installed at all**, so `typeof Worker === 'undefined'` and a script can feature-detect —
the family's absent-rather-than-throwing convention, and better than a constructor that could only throw.

A provider decides three things: whether a worker may exist, what engine runs it, and which thread pumps it.

```csharp
sealed class ThreadPerWorker(string root) : WorkerProvider
{
    // On the PARENT's thread, with the parent's script suspended mid-statement. Read the request; do not
    // run script, do not block, and do not fetch the worker's script — that is the worker's own
    // IModuleLoader's job, on the worker's own pump. Return null to refuse (the script gets a SecurityError).
    public override Engine? CreateWorkerEngine(WorkerRequest request)
        => new Engine(request.CreateDefaultOptions().EnableModules(root));

    // Still the parent's thread, ports entangled and the start job queued. This is where you start pumping.
    public override void OnWorkerStarted(WorkerConnection c)
        => new Thread(() =>
        {
            while (!c.IsEnded)
            {
                c.Worker.Advanced.ProcessTasks();
                try { c.Worker.Advanced.WaitForScheduledWork(TimeSpan.FromSeconds(1), c.TerminationToken); }
                catch (OperationCanceledException) { }   // terminate() — fall out via IsEnded
            }

            c.Worker.Dispose();                          // on the pumping thread, after the loop
        }) { IsBackground = true }.Start();

    // OnWorkerEnded is a SIGNAL ONLY, on whichever thread ended the connection — frequently not the
    // worker's. Never Dispose() or ProcessTasks() the worker engine from it: a terminate() ends the
    // connection on the parent's thread while the worker thread sits inside ProcessTasks, and either call
    // would be the engine's concurrent-use exception thrown out of the middle of the parent's script. The
    // loop above needs nothing here — it observes IsEnded and wakes on the token.
}
```

`WaitForScheduledWork` is what keeps an idle worker cheap *and* responsive: it parks until a job arrives from
any thread or the worker's own next timer comes due, so a `postMessage` from the parent is picked up
immediately rather than at the end of a polling interval. It does not pump — you call `ProcessTasks()`
yourself, which is the same division every other host-driven API in Jint keeps.

**N workers on one existing loop** — a game frame, say — works too, with one thing to get right:
`ProcessTasks` drains until empty, so an unbounded worker handler would eat the frame. Give each worker a
slice, which is exactly what `OperationDeadlineConstraint` is for (no-op `Reset()`, so it survives the
per-entry constraint reset, and it erupts as a `TimeoutException` from `ProcessTasks`):

```csharp
foreach (var c in live)
{
    var slice = (OperationDeadlineConstraint) c.HostState!;   // registered in CreateWorkerEngine
    slice.Begin(TimeSpan.FromMilliseconds(2), c.TerminationToken);
    try { c.Worker.Advanced.ProcessTasks(); }
    catch (TimeoutException) { /* overran its slice; resumes next frame */ }
    finally { slice.End(); }
}
```

**What a worker inherits: restrictions yes, grants no.** `request.CreateDefaultOptions()` gives you a fresh
`Options` — never the parent's instance — carrying the parent's whole *restrictive* posture: the seven
`Options.Constraints` values, `Host.StringCompilationAllowed`, `AgentCanSuspend`, `Json.MaxParseDepth`, the
parser bounds, the module-graph limits and `ResultLimits`, plus a replay of every constraint **factory** the
parent registered, so each engine gets its own constraint instances. Otherwise `new Worker()` would be an
`eval` escape hatch out of a hardened parent. What it does *not* carry is anything that grants a capability:
`fetch`, `EventSource`, `WebSocket`, `Storage`, `CacheApi` and `FetchEvents` are subtracted whatever the
parent has, CLR interop and the module loader are yours to give deliberately, and **nesting is off** — a
worker gets neither the `Workers` flag nor the provider, because a worker that can spawn workers is a grant,
by implication, of the thing that manufactures engines. Turning nesting on is two visible lines, by which you
accept the accounting; `request.Depth` and `request.LiveWorkerCount` are there to bound the tree. It is a
convenience and not a security boundary: if you built the parent's `Options` from a hardening helper, build
the worker's from the same one.

Two per-engine backstops sit under all of that, neither of them the policy — the provider is the policy:
`Options.WebApi.Workers.MaxWorkers` (default 16) and `MaxQueuedMessages` (default 16384) each refuse with a
`QuotaExceededError` rather than letting a script manufacture engines, or fill a queue nobody drains, faster
than any host policy was written to notice.

**`terminate()` and `close()` are not the same mechanism with a flag.** `terminate()` closes both ends at
once, discards what the parent had already posted, and cancels the connection's token — after which the
worker's script stops within the engine's amortized check interval (64 statements) on its own thread, and not
at all while it is inside one of your CLR calls. That published bound is stronger than the field's: V8's
`TerminateExecution` is frame-bounded in the same way. Cancellation skips JavaScript `finally` blocks, which
is exactly what the standard's *abort a running script* prescribes. `close()` from inside the worker is the
opposite in every respect the standard makes it so: the turn that called it **runs to completion**, and the
parent-side queue **drains** rather than being discarded — so `postMessage(result); close();`, the commonest
idiom there is, still delivers whether or not the parent had pumped in between.

**Errors reach you by three routes.** A load or parse failure fires a plain `Event` named `error` at the
`Worker` object — no `message`, no `ErrorEvent`, which is what the standard's own step says and what libraries
branch on — and ends the connection as `StartupFailed` with `IsFaulted` and a CLR `Error` on it, so **a host
sees a startup failure without wiring anything at all**. An uncaught runtime error fires an `ErrorEvent` at
the `Worker` object carrying `message`, `filename`, `lineno`, `colno` and **`error: null`** — null for every
worker error, because the thrown value belongs to the worker's realm and its thread, and because the standard
says so; a worker that wants its parent to have the real failure catches it and `postMessage`s it, where the
serializer's `Error` support is deliberate. Whether the parent is told at all is gated on the standard's
*notHandled*, so a worker-side `preventDefault()`, or a worker global `onerror` returning `true`, stops the
propagation — and your `DiagnosticsSink` still sees every report either way, because a host's diagnostics
channel is not something the script it is running may switch off. Unhandled promise rejections stay on the
worker's own global and reach the parent through nothing at all, and a constraint failure is never an event:
it erupts from whatever is pumping, because a worker's budget is yours to observe and not the parent script's.

**Restore and dispose end connections, on either side.** A `RestoreGlobalSnapshot` or an `Engine.Dispose` on
the parent ends every connection it created (`ParentRestored` / `ParentDisposed`), and the same on a worker
engine ends its own (`WorkerRestored` / `WorkerDisposed`) — both endpoints, in both directions, because a
worker connection is one object spanning two engines rather than two independent peers, and a one-sided close
would leave the survivor paying a full structured clone per `postMessage` forever. The `Worker` object stays
alive and becomes inert: `postMessage` a no-op, `terminate()` idempotent, no further events.

Two smaller truths worth knowing before you rely on them. `type: 'module'` is required — the standard's own
default is `'classic'` and Jint refuses it with a `TypeError` that names the fix, for the reasons every
non-browser runtime refuses it. And on an engine that is *only* ever pumped, `ProcessTasks` runs jobs raw, so
a replayed `TimeoutInterval` **never fires** and `MaxStatements` becomes a lifetime budget; the worker budget
that does work is the pair built for it — `OperationDeadlineConstraint` for wall-clock and
`MemoryLimitConstraint` for allocations, both armed once with `Begin`/`End` — plus the termination token for
stopping.

#### Sharing state between a parent and a worker

No modern JavaScript runtime lets two threads share one JS heap, and Jint enforces that rather than hoping:
a second thread entering an engine gets an `InvalidOperationException`, because the engine's speed *is* its
unsynchronized state. What you have instead, in order of how much is shared:

1. **Move a buffer** — `postMessage(v, { transfer: [buf] })` is genuinely zero-copy and genuinely one-way.
2. **Move a channel** — `MessagePort` transfer, which is the shape Comlink-style RPC is built from; a
   transferable stream rides the same way.
3. **Share a CLR object.** Hand the *same* .NET object to both engines with `SetValue`: each builds its own
   `ObjectWrapper`, so no `JsValue` crosses, while the object underneath is one instance whose mutations both
   sides see. It is strictly more than `SharedArrayBuffer` offers, with three obligations — the object's
   thread-safety is entirely yours, there is no reactivity (that is what the port in (2) is for), and it must
   be *data* rather than a bridge, since calling into an engine another thread is inside is the admission
   exception. It only works inward: a JS-born object *is* an engine-affine C# instance, so it crosses by clone
   or by transfer and no other way.
4. **`SharedArrayBuffer`** — refused with a `DataCloneError`, exactly as `postMessage` refuses one in a page
   that is not cross-origin isolated. `Atomics.waitAsync`, which Jint already runs on the pump, is the
   sanctioned replacement.

### Uncaught script errors go to a `DiagnosticsSink`

A script's failures are not all catchable by the host: a promise rejects with nobody listening, a `setTimeout`
callback throws long after `Execute` returned, a script calls `reportError`. `Options.WebApi.Diagnostics.Sink`
is the one place all of that arrives.

```csharp
sealed class LogSink : DiagnosticsSink
{
    public override void Report(DiagnosticEvent report) =>
        logger.LogWarning("{Kind}: {Message}", report.Kind, report.Exception?.Message ?? report.Value.ToString());
}

var engine = new Engine(options => options.UseWebApis().UseDiagnostics(new LogSink()));
```

There are four kinds of report. `ReportedError` is what a script handed `reportError(e)`.
`UnhandledPromiseRejection` is `HostPromiseRejectionTracker`'s two operations, told apart by
`RejectionHandled` — and it is *additive*: the long-standing `engine.Advanced.PromiseRejectionTracker` event
still fires exactly as it did, first. `UncaughtCallbackError` is an exception that escaped a callback the
engine invoked for you. `WorkerError` is a failure inside a `Worker` that neither the worker nor the `Worker`
object handled; its `Value` is the message as a string, because the thrown value belongs to the worker's realm
and cannot cross, and it is its own kind so that one sink wired for a parent *and* its workers can tell the
report it already heard from the worker's side apart from this one.

**That last one is the reason a sink changes behaviour, so install one deliberately.** With no sink a timer
callback or an event listener that throws erupts out of whatever was running it, because the alternative
would be to lose the error entirely. With a sink there is somewhere for it to go, so the engine reports it
and carries on — which is what the standards actually specify (HTML invokes a timer handler with exception
behaviour `"report"`; DOM's *inner invoke* reports a throwing listener and moves to the next one). Errors that
exist to **bound** execution are never reported and always erupt: a timeout, a cancellation, the statement,
memory and recursion budgets. `DiagnosticsSink.Null` is therefore not the same as no sink — it means "carry
on, and tell me nothing".

A sink alone is enough; the `Reporting` feature flag only decides whether script can *call* `reportError`, and
`reportError` without a sink is a documented no-op that never throws. The sink is read once, when the engine is
built. It is called synchronously, on the engine's thread, and must be thread-safe if the same `Options` builds
engines that run concurrently. The `JsValue`s on a report belong to the reporting engine: read them inside the
call rather than stashing them.

### …and script can watch them too: global `error` events

`WebApiFeatures.GlobalEvents` gives the global scope `addEventListener`, `removeEventListener`,
`dispatchEvent` and `self` (`=== globalThis`), plus the `ErrorEvent` and `PromiseRejectionEvent` interfaces,
so a script written for a browser or a worker can watch its own failures:

```js
self.addEventListener('error', e => {
    // e.message, e.filename, e.lineno, e.colno, e.error
});
self.addEventListener('unhandledrejection', e => { /* e.promise, e.reason */ });
```

`error` is fired for a `reportError(e)` call, for an exception that escaped a timer callback, and for one that
escaped an event listener — HTML's [report an
exception](https://html.spec.whatwg.org/multipage/webappapis.html#report-an-exception), whose step 5 this is.
`unhandledrejection` and `rejectionhandled` are fired for the two operations of `HostPromiseRejectionTracker`.

**The global object itself is not an `EventTarget`,** and this feature does not make it one — nothing about
its prototype, its own properties or the inline caches over them changes. The listener list lives on a
synthetic target the engine keeps beside its timers; the three operations are ordinary global functions bound
to it, and `event.target`, `event.currentTarget` and a listener's `this` are the global object, which is what
a browser reports. The one thing a script could notice is that they ignore their `this`, so
`const f = addEventListener; f('error', h)` works where a browser raises *Illegal invocation*.

Four rules are worth knowing before you rely on it:

- **The events feed the `DiagnosticsSink`, they never replace it.** `preventDefault()` really does cancel the
  event — that is HTML's *notHandled* going false — and the sink is told anyway. A script can observe an
  uncaught failure; it cannot hide one from your log.
- **Only a `JavaScriptException` is ever dispatched.** A timeout, a cancellation, the statement, memory and
  recursion budgets keep erupting past both the event and the sink, exactly as they always have. An `error`
  listener is not a way to swallow a constraint.
- **A sink is still what turns a callback failure into a *report*.** Firing the event is a step of reporting,
  so with no sink a throwing timer callback or event listener erupts as before and no listener sees it.
  `reportError` is the exception, being itself a request to report: it fires the event with or without a sink.
- **Rejection events arrive at the tracker's cadence, not HTML's microtask checkpoint** — the same documented
  divergence `DiagnosticEvent.RejectionHandled` carries, so `Promise.reject(e).catch(f)` raises
  `unhandledrejection` and then `rejectionhandled` where a browser would raise neither.

**A script's own `error` listener is only as live as your sink.** `GlobalEvents` is part of
`WebApiFeatures.Default`, so a script written for a browser registers `self.addEventListener('error', …)` on a
default web-API engine quite happily — and then hears nothing when a timer callback or a listener throws,
because firing the event is a step of *reporting* and an engine with no sink has nowhere to report to (a
`reportError` call still fires the event, being itself a request to report). The browser-like recipe is
`options.UseDiagnostics(DiagnosticsSink.Null)`, or `Options.WebApi.Diagnostics.Sink` set directly: the failure
is reported rather than erupting, the script's handler runs, the pump carries on, and nothing is written on
your side. Give it a real sink instead the moment you want those failures in your log — neither choice touches
the constraints, which erupt past the event and the sink alike.

An exception thrown *while* a report is being dispatched goes to the sink alone and starts no second dispatch,
which is HTML's re-entrancy rule. And a `RestoreGlobalSnapshot` drops the listeners: they are closures over
the cycle that just ended, over globals the restore has replaced.

### Idle callbacks: `requestIdleCallback`

`requestIdleCallback(callback, { timeout })` runs a callback when the engine has nothing better to do, handing
it an `IdleDeadline` with `didTimeout` and `timeRemaining()`. A browser means "the slack before the next frame
is due"; Jint has no frames, so the mapping is stated plainly:

- **An idle period starts when a pump has run out of everything else** — every queued job, every
  `scheduler.postTask` task at every priority including `background`, every *due* timer. It is the lowest band
  the engine has.
- **Its deadline is `Options.WebApi.Timers.IdleBudget`**, 50 ms by default, which is the ceiling the standard
  itself recommends. One pump spends at most one budget on idle work, however many callbacks are waiting; the
  rest run on the next pump. Set it to `TimeSpan.Zero` if your host has no idle time to give, and then only a
  callback requested *with* a `timeout` ever runs.
- **A callback requested from inside a callback belongs to the next period**, so a self-re-arming
  `requestIdleCallback` cannot monopolise the pump it started in.
- **A `timeout` rides the timer queue**, so it counts against `MaxActiveTimers` and — like every timer — only
  elapses while the engine is being pumped. A callback it reaches runs with `didTimeout === true` and a
  `timeRemaining()` of zero. A callback with no `timeout` costs no timer slot at all.

### Driving the engine from your own loop

`engine.Advanced.TimeUntilNextScheduledWork` answers the question a host loop actually has — *when should I
pump?* — and `engine.Advanced.ProcessTasks()` is the pump. There is deliberately no third method that drains
for a budget: Jint never starts a thread, so the work always runs on your thread anyway; what was missing was
the timing, not another way to run it.

```csharp
while (running)
{
    var until = engine.Advanced.TimeUntilNextScheduledWork;
    if (until is null || until <= frameBudget)
    {
        engine.Advanced.ProcessTasks();
    }

    RenderFrame();
}
```

`TimeSpan.Zero` means there is work to run right now (a queued job, a due timer, a waiting idle callback); a
positive span is how long until the earliest *timed* work — a `setTimeout`, an `AbortSignal.timeout()`, a
delayed `postTask`, a `requestIdleCallback` timeout, an `Atomics.waitAsync` deadline — comes due; `null` means
nothing is scheduled. It is available on **every** target framework, not just .NET 8: the atomics deadline it
reports is a core-engine one. It describes the engine's own schedule and not the outside world, so a `null` is
"nothing timed is pending" rather than "nothing will ever happen".

**`engine.Advanced.WaitForScheduledWork(timeout, token)` is the answer to the work that arrives from a
background thread.** A loop with no frame of its own would otherwise have to sleep on a ceiling of its own
choosing, and then a job enqueued from *another* thread — an interop `Task` settling, an asynchronous module
load completing, a message posted in from a second engine — waits out that ceiling before anything notices it:
such a job has no due time for `TimeUntilNextScheduledWork` to report, so polling was the only way to find it.
The wait blocks until there is something worth pumping and returns `true`, or returns `false` when the ceiling
runs out; the token ends it with an `OperationCanceledException`.

```csharp
while (!token.IsCancellationRequested)
{
    engine.Advanced.ProcessTasks();

    try
    {
        engine.Advanced.WaitForScheduledWork(TimeSpan.FromMilliseconds(50), token);
    }
    catch (OperationCanceledException)
    {
        break;
    }
}
```

**It does not pump** — `ProcessTasks()` is still the pump, and there is still no third method that drains for
a budget. It is bounded internally by the engine's own next due time, so a `setTimeout(f, 1)` wakes it in about
a millisecond rather than at the ceiling above, and the ceiling is only a backstop. Treat a `true` as a hint and
re-check your own condition: spurious wakes are expected. The wait **claims the engine for its whole duration**,
so a second thread asking for it — or for anything else guarded — gets the usual *"This Engine is already in use
by another thread"*, which is what makes one drainer per engine self-enforcing. `WaitForScheduledWorkAsync` is
the same contract without holding a thread. Both are available on **every** target framework and are unaffected
by which web APIs are enabled.

**`engine.Advanced.CreateAbortSignal(cancellationToken)`** bridges your cancellation into script: hand the
returned `AbortSignal` to `fetch`, to `scheduler.postTask`, or to a listener the script adds itself. Requires
the `Events` feature. Cancelling the token **never runs script on the cancelling thread** — it enqueues, and
the abort happens on the next pump, the same contract `setTimeout` has — so an engine nobody pumps never
observes it. A token that is *already* cancelled yields an already-aborted signal on the spot, so
`fetch(url, { signal })` rejects without issuing a request. The registration is released when the abort lands,
when `RestoreGlobalSnapshot` ends the cycle, and when the engine is disposed, so a long-lived host token does
not retain a finished engine. An inbound request has its own door onto the same bridge — see
[Hosting a fetch handler](#hosting-a-fetch-handler), where the token you pass the invocation *is* the
handler's `request.signal`.

**A `ShadowRealm` does not get these globals.** Only the principal realm's global object is touched, which is
deliberately more conservative than a browser (where these APIs are `[Exposed=*]`); a host that wants them
inside a shadow realm can install them through `Host.InitializeShadowRealm`.

### `fetch` is opt-in on its own

`UseWebApis()` never enables `fetch`, and `WebApiFeatures.Default` will never include it. Network egress from
script is a decision you make explicitly:

```csharp
var engine = new Engine(options => options.UseWebApis().UseFetch(fetch =>
{
    fetch.AllowedSchemes.Remove("http");                     // https only
    fetch.UrlFilter = uri => uri.Host.EndsWith(".example.org", StringComparison.OrdinalIgnoreCase);
    fetch.MaxResponseBytes = 1024 * 1024;
    fetch.Timeout = TimeSpan.FromSeconds(5);
    fetch.HttpClient = myClient;                             // or HttpClientFactory, for a per-tenant client
}));

var body = engine.Evaluate("fetch('https://api.example.org/x').then(r => r.json())").UnwrapIfPromise();
```

Enabling it also brings `Events`, `Url`, `Files` and `Streams`, because fetch's own surface is built out of
them: a `Request` always has an `AbortSignal`, its URL is a WHATWG URL, `response.blob()` answers with a
`Blob`, and `response.body` is a `ReadableStream`.

`Headers`, `Request` and `Response` are the standard's own classes — the full `Headers` (sorted, combined
iteration, `getSetCookie`), method normalization, `redirect` modes, `clone()`, `Response.error()`,
`Response.redirect()`, `Response.json()`. A `FormData` body is serialized as `multipart/form-data` with a
cryptographically random boundary, and `formData()` reads one back — as well as an
`application/x-www-form-urlencoded` body — rejecting with a `TypeError` for anything else. `duplex` is read
and validated, because it decides whether a body may be a stream at all (see below). `credentials`, `cache`,
`mode`, `referrer` and `integrity` are accepted and ignored, the same convention Node and workerd follow,
because there is no origin, cookie jar or HTTP cache here to honour them with.

### Bodies stream, in both directions

`response.body` is a real `ReadableStream`, and a network response is read **on demand**: the promise
resolves as soon as the headers are in, and the socket is only read when a consumer asks for the next chunk.

```js
const r = await fetch('https://api.example.org/big.ndjson');
for await (const chunk of r.body) {
    // one Uint8Array per read; the transport is not read again until this loop comes back for more
}
```

The stream's high water mark is zero, so a script that takes the response and never reads its body never
touches the socket again — and `body.cancel()`, or a reader's `cancel()`, drops the connection. Chunks are
delivered as generation-stamped event-loop jobs, exactly like every other cross-thread completion in Jint,
so nothing about a body arrives on a thread the host did not pump. `MaxResponseBytes` is enforced on the
running total per chunk; because the promise has already resolved by the time a body byte is read, a body
that breaks the cap **errors the stream** rather than rejecting the `fetch` promise (a `Content-Length` that
already exceeds it is still refused before the promise settles).

The `Body` mixin sits on top of that and is unchanged in behaviour: `text()`, `json()`, `arrayBuffer()`,
`bytes()` and `blob()` read the stream to the end and answer with the whole body, `bodyUsed` is the stream's
*disturbed* flag, and a body that is disturbed or locked makes the next consumer reject. `clone()` `tee()`s
the stream, so each half has its own queue and its own flags. A body built from bytes — `new Response('…')`,
a `Blob`, a `BufferSource` — keeps its bytes and only materializes a stream if something reads `body`, so the
common `new Response(x).text()` costs no stream at all.

Every body the engine hands out is a **byte stream** — `response.body`, `request.body` and `Blob.stream()`
alike, as [Fetch](https://fetch.spec.whatwg.org/#concept-body) and
[File API](https://w3c.github.io/FileAPI/#blob-get-stream) both require — so `getReader({ mode: 'byob' })`
works on all three and a download loop can recycle one buffer instead of allocating per chunk. A default
reader sees exactly what it always did: one `Uint8Array` per chunk.

**Uploads stream too.** A request body given as a `ReadableStream` goes to the wire as the script produces
it, which is the standard's `duplex: 'half'` — and, as the standard says, that member is **compulsory** for
such a body:

```js
const { readable, writable } = new TransformStream();
const done = fetch('https://api.example.org/ingest', {
    method: 'POST',
    duplex: 'half',           // required whenever body is a ReadableStream; omitting it is a TypeError
    body: readable,
});

const writer = writable.getWriter();
for (const row of rows) { await writer.write(encode(row)); }   // each write reaches the socket
await writer.close();
await done;
```

Backpressure runs the whole way through: the next chunk is not read from your stream until the previous one
has been handed to the transport, so a slow server slows the script rather than filling memory. The request
goes out chunked, because nothing can compute a `Content-Length` in advance.

Three honest reductions, all of them consequences of the transport being `HttpClient`. A **body-preserving
redirect fails the fetch** — which is what
[the standard itself prescribes](https://fetch.spec.whatwg.org/#http-redirect-fetch) for a body with no
source, since the bytes are gone once sent; a `303`, which drops the body with the method, is followed
normally. **Nothing checks the negotiated HTTP version**, where a browser refuses a streaming upload over
HTTP/1.1; this sends it chunked, as every server-side HTTP client does. And the body can be sent **once** —
if `HttpClient` needs to resend the request, the fetch fails rather than sending a truncated body.

Like every other web API, `fetch` settles only while the engine is being pumped: the promise resolves inside a
blocking `UnwrapIfPromise`, an `await` of `EvaluateAsync`, or your own `engine.Advanced.ProcessTasks()` loop.
The deadline is the one exception, and deliberately so — it is enforced CLR-side, so an engine nobody pumps
still lets go of its socket, and it covers the body as well as the headers. An in-flight request is cancelled
by `Engine.Advanced.RestoreGlobalSnapshot`, and its promise never settles into the restored engine; a body
still arriving when that happens has its connection dropped and its stream errored, so a host holding the
response is told rather than left waiting.

**Security.** Enabling `fetch` gives the script your process's network position: anything the worker can
reach, the script can reach. The defaults bound the resource questions — 32 MiB per response body (counted
after decompression, so a compression bomb is bounded too), 20 redirects, a 30-second deadline, 10 concurrent
requests — but they cannot know which *hosts* are legitimate. That is what `UrlFilter` is for, and a
deployment running untrusted script wants one. Jint follows redirects itself with `AllowAutoRedirect` off
precisely so that **every hop is re-checked** against the scheme list and your filter: a server you allow
answering `302 Location: http://169.254.169.254/…` does not get to launder the request past a first-hop check.
`Authorization`, `Cookie` and `Proxy-Authorization` are stripped across an origin change, header values may
not carry CR or LF, and a URL with credentials in it is refused. Every network-class failure is one
`TypeError` saying only `Failed to fetch`, so a script cannot map your internal network by reading the
failures apart; the real cause rides the error value and is readable by the host through
`JintException.TryGetClrException`. See [THREAT_MODEL.md](.github/THREAT_MODEL.md) TM-21 for the full
analysis, including what these controls do *not* cover.

### Streams, including byte streams

`ReadableStream`, `WritableStream` and `TransformStream` implement the WHATWG Streams Standard operation by
operation: queuing strategies and `desiredSize`, the `pull` reentrancy rules, `tee()` with its composite
cancellation, `pipeTo()` / `pipeThrough()` with `preventClose` / `preventAbort` / `preventCancel` and an
`AbortSignal`, asynchronous iteration of a readable stream (`for await…of`, and `values({ preventCancel })`),
and `ReadableStream.from()` for any sync or async iterable. Every promise they hand out is an ordinary engine
promise and every callback you supply — `start`, `pull`, `cancel`, `write`, `close`, `abort`, `transform`,
`flush`, `size` — runs on the engine's thread from the same job queue that runs promise reactions, so the
microtask ordering the standard prescribes is the ordering you get, and nothing here ever starts a thread.

**Byte streams are there too.** `new ReadableStream({ type: 'bytes' })` gives its underlying source a
`ReadableByteStreamController`, with `byobRequest`, `autoAllocateChunkSize` and a `desiredSize` counted in
bytes; `stream.getReader({ mode: 'byob' })` gives the consumer a `ReadableStreamBYOBReader` whose
`read(view, { min })` fills the buffer the caller supplied. Buffers are *transferred* across every crossing,
exactly as the standard requires — hand a view to `enqueue()` or to a BYOB `read()` and yours is detached,
while the one you get back owns the memory — so a read loop recycles one allocation instead of allocating per
chunk. `tee()` on a byte stream produces two byte streams, and swaps between a default and a BYOB reader on
the original depending on how each branch is being read.

The streams the *engine* hands out are byte streams too: `response.body`, `request.body` and `Blob.stream()`
each give a `ReadableByteStreamController` behind the scenes, so `getReader({ mode: 'byob' })` works on a
network response, on a buffered body and on a blob — and `tee()` on any of them produces two byte streams.
`Engine.Advanced.CreateReadableStream(Stream)` is the one that is still an ordinary stream: it wraps a host
`Stream` you already own, whose reads go into a buffer the bridge owns rather than into one a script
supplied.

One deliberate reduction: **only the five interfaces a script constructs by name are globals.**
`ReadableStreamDefaultReader`, `ReadableStreamBYOBReader`, `WritableStreamDefaultWriter`, the four
controllers and `ReadableStreamBYOBRequest` exist as ordinary interface objects — a reader's `constructor` is
the real thing, and `new` on it behaves as the standard says — but they are not installed on `globalThis`,
where a browser would expose them.

#### All three streams are transferable

`ReadableStream`, `WritableStream` and `TransformStream` are transferable objects, so a stream can be handed
to another engine and read there — which is what makes a worker-style split able to pass a *pipeline* and not
only a value:

```csharp
var host = new Engine(o => o.UseWebApis());
var worker = new Engine(o => o.UseWebApis());
var pair = host.Advanced.CreateMessagePortPair(worker);
host.SetValue("port", pair.Local);
worker.SetValue("port", pair.Remote);

worker.Execute("""
    port.onmessage = async e => {
      for (const reader = e.data.getReader(); ; ) {
        const { value, done } = await reader.read();
        if (done) break;
        log(value);
      }
    };
    """);

host.Execute("""
    const rs = new ReadableStream({ start(c) { c.enqueue('a'); c.enqueue('b'); c.close(); } });
    port.postMessage(rs, [rs]);   // rs is now locked and no longer directly usable
    """);
```

The mechanism is the standard's "cross-realm transform", and it is a `MessagePort` underneath: transferring a
readable stream creates an entangled pair, wires a writable side onto one port, pipes the original stream into
it and sends the other port along with the message. The receiving engine builds a readable stream over the
port that arrived. A `WritableStream` is the mirror image, and a `TransformStream` is both — its two sides are
transferred separately, so it costs two channels. `structuredClone(stream, { transfer: [stream] })` does the
same thing into the current realm, which is what the two phases composed give you.

**Both engines have to be pumped**, exactly as for a `MessagePort` and for timers: a chunk written on the
sender is an event-loop task on the receiver, so an engine nobody pumps receives nothing. Backpressure crosses
too — the receiving side's high water mark is 0 and each read sends one `pull` back — so the sender does not
drain its source into the channel ahead of whoever is reading. Everything runs on each engine's own thread;
nothing here starts one.

The standard's refusals are implemented as written: transferring a **locked** stream is a `DataCloneError`,
transferring the same stream twice is a `DataCloneError`, and a stream that is in the message but *not* in the
transfer list is a `DataCloneError` — a stream is transferable and not serializable. After a transfer the
original is **locked and disturbed**, because the pipe holds a reader (or a writer) on it, and a transferred
`TransformStream` leaves both of its sides that way. Closing, erroring and cancelling all propagate across the
boundary in both directions, and a chunk that cannot be structured-cloned fails that write and errors both
ends.

One thing goes beyond the standard, deliberately. HTML drops a message posted into a channel whose far end has
gone, silently, so a transferred stream whose message is never delivered — or whose receiving engine calls
`RestoreGlobalSnapshot`, or is disposed — would leave the sender's pipe reading its source forever and writing
into nothing. Jint ends the stream instead: the next write (or the next read on the receiving side) finds the
channel gone and errors, which cancels the source. A pipe is never left running against a side nobody can
reach.

### Storage is opt-in on its own, and you decide where the data lives

`localStorage` and `sessionStorage` are the one non-network feature `UseWebApis()` does **not** turn on. Every
other web API gives a script something to *do*; this one gives it somewhere to *keep* things, and a host that
asked for "the web APIs" has not agreed to that. Ask for it by name:

```csharp
// Two in-memory stores, per engine, gone when the engine is.
var engine = new Engine(options => options.UseStorage());

// Or your own store, which is what makes anything persist or be shared.
var tenantStorage = new MyDatabaseStorage(tenantId);
engine = new Engine(options => options.UseStorage(tenantStorage));
```

The whole `Storage` interface is there — `length`, `key`, `getItem`, `setItem`, `removeItem`, `clear` — and so
is the named property access the standard defines alongside it, so `storage.foo = 'bar'`, `delete storage.foo`,
`'foo' in storage` and `Object.keys(storage)` all work. `Storage` is a WebIDL *legacy platform object*, which
decides the one rule that surprises people: an interface member always wins over a stored key of the same
name, so `storage.getItem` is the method even after `storage.setItem('getItem', 'x')` — and the key is still
stored, and `storage.getItem('getItem')` still reads it back.

**Where the data lives is entirely `StorageProvider`'s business.** Implement it over a file, a database, a
per-tenant cache or a request-scoped dictionary; the engine implements the algorithms and never persists
anything itself. With no provider each engine gets its own `InMemoryStorageProvider` — nothing is shared
between engines, nothing survives one, and `Options.WebApi.Storage.MaxTotalBytes` (5 MB by default) bounds it,
turning an over-quota `setItem` into the catchable `QuotaExceededError` the standard names — with `quota` and
`requested` filled in, so a script can report how far over it went. Your own provider raises the same error by
throwing `StorageQuotaExceededException`, and the constructor overload taking `quota` and `requested` is how it
supplies those two numbers; anything else it throws reaches you unchanged rather than becoming a JavaScript
error the script can swallow. A provider reached from engines that run concurrently must be thread-safe.

**There is no `storage` event.** Every mutating step ends in "broadcast", which notifies *other* browsing
contexts sharing an origin — a multi-context feature, and an engine has one context. `StorageEvent` is absent
rather than present-and-never-firing, so feature detection sees the truth.

### `EventSource` is a second, separate grant

Server-sent events are their own opt-in: `UseFetch()` does not enable `EventSource`, `UseEventSource()` does
not enable `fetch`, and `UseWebApis()` enables neither.

```csharp
var engine = new Engine(options => options.UseWebApis().UseEventSource(net =>
{
    net.UrlFilter = uri => uri.Host.EndsWith(".example.org", StringComparison.OrdinalIgnoreCase);
    net.MaxResponseBytes = 64 * 1024;   // the largest single event, not the largest stream
    net.MaxConcurrentRequests = 2;      // at most two streams open at once
}));

engine.Execute("""
    const events = new EventSource('https://api.example.org/updates');
    events.onmessage = e => console.log(e.lastEventId, e.data);
    """);

while (running) { engine.Advanced.ProcessTasks(); Thread.Sleep(5); }   // your loop, your thread
```

It is the standard's own object: `url`, `readyState` with `CONNECTING`/`OPEN`/`CLOSED`, `onopen`/`onmessage`/
`onerror`, `close()`, and `addEventListener` for the custom types an `event:` field names. The stream is
parsed exactly as the specification writes it — UTF-8 with a leading BOM stripped, CRLF/CR/LF line endings,
`data`/`event`/`id`/`retry` fields, one leading space removed after the colon, comment lines as keep-alives,
`data` values joined with a newline, and an event dispatched only at a blank line. `withCredentials` is
accepted, remembered and ignored, the same treatment `fetch` gives `credentials`: there is no origin, cookie
jar or credential store here for it to select.

**It reads `Options.WebApi.Fetch`** — the same transport (`HttpClient` / `HttpClientFactory`) and the same
policy (`AllowedSchemes`, `UrlFilter`, `MaxRedirects`) that `fetch` uses, so a filter you have already written
covers both, and every redirect hop is re-checked exactly as it is for a fetch. Three of those settings mean
something different for a stream:

- **`Timeout` does not apply.** A connection that is idle for an hour is what an event stream is *for*.
- **`MaxResponseBytes` does not bound the stream** — nothing could — it bounds **one event**: the data buffer
  plus the line being read, which is what actually has to be held in memory. Exceeding it fails the connection.
- **`MaxConcurrentRequests` bounds the streams one engine may have open**, counted separately from the
  fetches in flight, because a stream holds its socket for as long as it lives.

**Reconnection rides the timer queue**, so it too happens only while you are pumping, and it counts against
`MaxActiveTimers`. A stream that ends, or a network error, sets `readyState` back to `CONNECTING`, fires
`error`, waits the server's `retry:` value (3 seconds by default) and connects again — re-running the URL
policy from scratch and sending `Last-Event-ID` so the server can resume. A response that is not `200
text/event-stream`, a URL your policy refuses and an event over the size cap all *fail* the connection
instead: `readyState` becomes `CLOSED` and nothing retries. `close()` cancels the request in flight, and so
does `Engine.Advanced.RestoreGlobalSnapshot` — after a restore the connection is gone, its pending
reconnection with it, and nothing from the ended cycle is ever dispatched into the restored engine.

**Security.** Everything TM-21 says about destinations applies here, and one thing more: a connection is
long-lived and reconnects *on a delay the server chooses*, with no deadline to end it. See
[THREAT_MODEL.md](.github/THREAT_MODEL.md) TM-22 — the short version is to bound the engine's own lifetime
for untrusted script rather than pooling an engine a script may leave streaming.

### `WebSocket` is the third separate grant

Sockets are their own opt-in, exactly as `fetch` and `EventSource` are: `UseWebSocket()` enables none of the
other two, they enable no sockets, and `UseWebApis()` enables none of the three. They share their settings,
not their permission.

```csharp
var engine = new Engine(options => options.UseWebApis().UseWebSocket(net =>
{
    net.AllowedSchemes.Remove("http");                   // wss only
    net.UrlFilter = uri => uri.Host.EndsWith(".example.org", StringComparison.OrdinalIgnoreCase);
    net.MaxResponseBytes = 1024 * 1024;                  // the largest single message
    net.MaxConcurrentRequests = 2;                       // at most two sockets open at once
}));

engine.Execute("""
    const ws = new WebSocket('wss://api.example.org/feed', ['v2']);
    ws.onopen = () => ws.send('hello');
    ws.onmessage = e => console.log(e.data);
    ws.onclose = e => console.log(e.code, e.reason, e.wasClean);
    """);

while (running) { engine.Advanced.ProcessTasks(); Thread.Sleep(5); }   // your loop, your thread
```

It is the standard's own object: `url`, `readyState` with the four ready-state constants, `bufferedAmount`,
`protocol` and `extensions` as the handshake negotiated them, `binaryType` switching binary messages between
`Blob` (the default, as in a browser) and `ArrayBuffer`, `send` of a string, `Blob`, `ArrayBuffer` or a view
over one, `close(code, reason)` with the standard's validation, the `onopen`/`onmessage`/`onerror`/`onclose`
handlers, and the full `EventTarget` surface underneath them. A close lands as the real `CloseEvent` —
`code`, `reason`, `wasClean` — with RFC 6455's 1000 for a clean close and 1006 when the transport simply
died. Every event dispatches from the engine's job queue on the engine's thread, only while you pump.

**It reads `Options.WebApi.Fetch`**, with the scheme list read in its WebSocket sense: `http` admits `ws`
and `https` admits `wss` — so a policy written for fetch carries over — and naming `ws` or `wss` outright
works too, for a host that wants sockets and not fetches. The `UrlFilter` is shown the `ws:` URL the script
asked for, which fails safe: a filter that tests `uri.Scheme == "https"` refuses every socket rather than
admitting one it was never shown. There is no per-hop re-check because there are no hops — the WHATWG
handshake forbids redirects outright. Three settings shift meaning the same way they do for an event stream:

- **`Timeout` bounds the opening handshake only.** The peer has to answer it; a socket that then idles for an
  hour is a socket doing its job.
- **`MaxResponseBytes` bounds one message**, which is what actually has to be held in memory. A peer message
  over the cap fails the connection with close code 1009 rather than buffering without bound.
- **`MaxConcurrentRequests` bounds the sockets one engine may have open**, counted separately from fetches
  and event streams, because a socket holds its connection for as long as it lives. The constructor refuses
  the one over the limit.

The lifecycle is fenced exactly as a fetch is: the realm and the event-loop generation are captured at
construction, so `Engine.Advanced.RestoreGlobalSnapshot` closes the socket and nothing from the ended cycle
— no message, no `close` event — is ever dispatched into the restored engine. An execution constraint that
erupts through a handler stays a constraint: it is never flattened into an `error` event a script could
swallow.

**Security.** See [THREAT_MODEL.md](.github/THREAT_MODEL.md) TM-24 for the full analysis. The short version:
everything TM-21 says about destinations applies, the channel is bidirectional — admit only destinations you
would let the script *write* to — and a socket is long-lived by design with a peer that can keep it alive
indefinitely, so bound the engine's lifetime for untrusted script rather than pooling an engine a script may
leave connected.

### Hosting a fetch handler

`Headers`, `Request` and `Response` work in both directions, so an engine can *be* a request handler: you
hand it an `HttpRequestMessage` and get an `HttpResponseMessage` back, with the script in between written
exactly the way a Cloudflare Workers or Deno script is.

```js
// worker.js
export default {
    async fetch(request) {
        const body = await request.json();
        return Response.json({ echoed: body, path: new URL(request.url).pathname });
    }
};
```

```csharp
var engine = new Engine(options => options.UseWebApis(
    WebApiFeatures.Events | WebApiFeatures.Url | WebApiFeatures.Files | WebApiFeatures.Timers));

engine.Advanced.SetFetchHandler(engine.Modules.Import("./worker.js"));

using var response = await engine.Advanced.InvokeFetchHandlerAsync(request);
```

`SetFetchHandler` accepts three shapes, tried in this order: a **function**; an **object with a callable
`fetch` property** (called with that object as `this`, so a handler written as a method can reach its
siblings); or an object with a **`default`** property matching either — which is what a module namespace is,
so the `export default { fetch }` convention above is registered in one call. A script that has no modules
registers its handler just as directly:

```csharp
engine.Execute("function handle(request) { return new Response('hi'); }");
engine.Advanced.SetFetchHandler(engine.GetValue("handle"));
```

**There is no feature flag for registering a handler this way, and doing so never grants network access.**
(The script-facing form below does have one, because letting a script claim the route is a different
decision.) What the object model needs is the three features its own interfaces are built out of — a `Request` has an `AbortSignal`, its
URL is a WHATWG URL, `response.blob()` answers with a `Blob` — so an engine built without `Events | Url |
Files` refuses the registration with a message naming them. Registering the handler is itself what installs
the `Headers`, `Request` and `Response` globals, lazily and without replacing anything already under those
names; `fetch` is deliberately *not* installed, because handling an inbound request is no reason to let the
script make outbound ones. (`options.UseFetch()` satisfies the requirement too, and does grant them.) One
ordering consequence: those globals appear when you register the handler, so a module that builds a `Response`
at *top level* has to be evaluated after the registration — the ordinary shape, where the handler builds its
response when it runs, does not care.

Only the request is passed. A Workers handler takes `(request, env, ctx)`; per-request host state reaches this
one the way it always has, through `engine.Advanced.AddLazyGlobal(...)` or a host function reading
`engine.Advanced.HostDefined`.

#### The script-facing form: `addEventListener('fetch', …)`

Enable `WebApiFeatures.FetchEvents` and the script registers the handler itself, with no host call at all —
which is what a service worker and a Cloudflare Workers script written in the older, non-module style look
like. The host keeps calling exactly the same `InvokeFetchHandler` / `InvokeFetchHandlerAsync` it already
does:

```js
// worker.js
addEventListener('fetch', event => {
    event.respondWith(handle(event.request));

    // Fire-and-forget work: it keeps the event alive, and it is just jobs you pump.
    event.waitUntil(recordHit(event.request.url));
});

async function handle(request) {
    const body = await request.json();
    return Response.json({ echoed: body, path: new URL(request.url).pathname });
}
```

```csharp
var engine = new Engine(options => options.UseWebApis(WebApiFeatures.FetchEvents));
engine.Execute(File.ReadAllText("worker.js"));

using var response = await engine.Advanced.InvokeFetchHandlerAsync(request);
```

**The flag is its own grant, and is not `Fetch`.** Naming it does not enable `fetch` and enabling `fetch` does
not name it: one lets the script reach *out* to the network, the other routes requests you already have *in*
to the script, and coupling them would contradict the refusal `SetFetchHandler` already makes. What it does
imply is `GlobalEvents` (where `addEventListener` comes from, which brings `Events`), `Url` and `Files` — the
same closure the object model needs — and it installs `Headers`, `Request` and `Response` while the engine is
being built, so unlike the `SetFetchHandler` door a module may construct a `Response` at top level.

**A handler registered with `SetFetchHandler` always wins.** The listeners are the fallback for an engine that
has none, never an override of the one you chose; clearing the handler with `null` is how you hand the route
over deliberately. `HasFetchHandler` stays strictly about `SetFetchHandler` for the same reason — it is your
record of what *you* did, and a script must not be able to change the answer. To ask whether a request can be
served, invoke and let it fail.

**Not responding is a failure, like every other.** A dispatch that reaches no `event.respondWith(...)` — no
listener called it, or the ones that ran threw — fails the operation with an `InvalidOperationException`,
because an embedded engine has no network for an unanswered request to fall through to. A listener that throws
behaves as it does anywhere else on an `EventTarget`: with a `DiagnosticsSink` it is reported and the dispatch
carries on, so a later listener may still answer; with no sink it propagates, and the invoke turns it into the
operation's failure. (A listener that answers and *then* throws therefore serves its response on an engine
with a sink and fails on one without — with nowhere to report to, preferring the response would lose the
exception entirely.) A constraint firing is neither, and erupts past both.

`FetchEvent` carries `request`, `respondWith(r)` and `waitUntil(f)`, and two reductions from the Service
Workers Standard are deliberate. There is **no `ExtendableEvent` interface object** — `waitUntil` is a member
of `FetchEvent.prototype`, the flat shape Workers exposes — and there is **no timed-out flag**, so the event
stays extendable exactly as long as its own promises are pending and no longer; `respondWith` after the
dispatch, or `waitUntil` once nothing is outstanding, is an `InvalidStateError`. `respondWith` may be called
once, stops the dispatch for every later listener, and takes a `Response` or a promise of one. A `waitUntil`
promise that rejects is reported once, through `unhandledrejection` and the sink, rather than vanishing —
attaching the lifetime reaction is what would otherwise have made it look handled. `preloadResponse`,
`clientId`, `resultingClientId`, `replacesClientId` and `handled` are absent rather than faked, since every one
of them describes a service worker registration that does not exist here. One timing difference from the
handler form: `respondWith` puts its argument through `PromiseResolve`, so even the most synchronous listener
needs one turn of the pump.

**Two invoke shapes**, and the difference is who owns the thread. `InvokeFetchHandlerAsync` is the one an
ASP.NET Core host wants — `await` it and the continuations run wherever the await resumes.
`InvokeFetchHandler` returns a `FetchHandlerOperation` and runs nothing on its own: you pump the engine and
watch the operation, which is the shape a game loop or a UI thread needs, because then every turn provably
runs where you decided. It is the same pair `Engine.Modules.ImportAsync` / `StartImport` offers, for the same
reason.

```csharp
var operation = engine.Advanced.InvokeFetchHandler(request);
while (!operation.IsCompleted)
{
    engine.Advanced.ProcessTasks();     // your loop, your thread
}

using var response = operation.GetResult();
```

A handler answering a `Response` synchronously produces an operation that is already complete; one answering
a promise — every `async` handler, and anything that awaits a timer or an outbound `fetch` — needs turns. As
everywhere else in these APIs, a `setTimeout` inside a handler only fires while somebody is pumping.

**A failing handler is never turned into a 500 for you.** What a failure means on the wire is a policy
question only the host can answer, so it arrives as the exception it was: a `JavaScriptException` when the
handler threw, a `PromiseRejectedException` when its promise rejected, the constraint's own exception when an
execution constraint fired, an `InvalidOperationException` when the handler answered with something that is
not a `Response` (including `Response.error()`, which represents a network error and has no status line).
All of Jint's own exceptions derive from `JintException`, so that is the one type to catch:

```csharp
try
{
    using var handled = await engine.Advanced.InvokeFetchHandlerAsync(request, context.RequestAborted);
    // ... copy it onto the real response
}
catch (JintException ex)
{
    logger.LogWarning(ex, "the script handler failed");
    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
}
```

With the polled shape the same failures arrive through the operation instead — `IsFaulted`, `Error`, or
rethrown by `GetResult()` — including a failure of the invoke call itself, so a host written to
poll-then-`GetResult` never has to guard the start call as well.

The handler runs under the engine's execution constraints like every other host entry, so `MaxStatements`,
`TimeoutInterval`, `LimitMemory` and any custom `Constraint` bound one invocation. Read
[Execution Constraints](#execution-constraints) before relying on that: the budget is per *entry* into the
engine, so each turn you pump afterwards gets a fresh one — a wall-clock bound over the whole request is what
`Jint.Constraints.OperationDeadlineConstraint` is for, bracketed around the invoke and the pump. The
`CancellationToken` taken by `InvokeFetchHandlerAsync` behaves exactly as `EvaluateAsync`'s does where the
*await* is concerned: it is observed at event-loop continuation boundaries and does **not** preempt the
interpreter, so bounding a handler that never yields is a constraint's job.

#### Telling the handler the client is gone

Pass the token you already hold and the handler's `request.signal` becomes a real `AbortSignal` instead of one
that can never fire — which is what a script written for Workers or Deno expects, and what an outbound
`fetch(upstream, { signal: request.signal })` needs in order to stop:

```csharp
var operation = engine.Advanced.InvokeFetchHandler(request, context.RequestAborted);
```

`InvokeFetchHandlerAsync`'s existing `CancellationToken` does this too. That is deliberately an addition to
what an existing parameter does rather than a second parameter: one token on a request invocation means "this
request has been abandoned", which is exactly what `HttpContext.RequestAborted` means and exactly what
`request.signal` is for. Passing nothing, or `CancellationToken.None`, gives precisely the engine you had
before.

Four things follow from where the abort happens:

- **It lands on a pump, on your thread.** Cancelling a `CancellationTokenSource` runs its callbacks on the
  cancelling thread, and aborting a signal dispatches a JavaScript `abort` event — which is script. So the
  registration only enqueues a job, and the abort happens the next time you pump. It is the same contract
  `setTimeout`, `AbortSignal.timeout()` and `engine.Advanced.CreateAbortSignal` have, and it uses the same
  bridge. Cancel and never pump again and the signal never aborted.
- **A token that is *already* cancelled needs no pump**: the handler is called with a request whose
  `signal.aborted` is true from its first statement, so it can refuse without doing any work. The reason is
  the standard's default in both cases — a `DOMException` named `AbortError`.
- **The abort is observational.** It does not complete or fail the operation and it does not stop the handler;
  it tells the script, and what the script does next arrives through the ordinary failure contract — an
  outbound `fetch` chained on the signal rejects, and that becomes a `PromiseRejectedException` whose value is
  the abort reason. A handler that ignores it and answers anyway *is served*, because you are the one who went
  on pumping. Ending an invocation outright is your lever, not the script's: stop pumping, or end the cycle
  with `RestoreGlobalSnapshot`. And it is not an execution constraint — a handler stuck in `while (true) {}`
  never reaches a pump, so bounding the interpreter is still `Options.Constraints`' job. The two compose.
- **The registration is released when the invocation ends**, and again by `RestoreGlobalSnapshot` and
  `Engine.Dispose`, so a long-lived token accumulates nothing across the requests a pooled engine serves. What
  is deliberately *not* undone is an abort already on the event loop: it still lands on the next pump, because
  that is what cancels the outbound work an abandoned handler had started.

The polled shape is the one to prefer when a handler is meant to *answer* on abort. With the awaitable shape
the same token also ends the `await`, and the two are not ordered against each other: cancelling mid-flight
normally throws `OperationCanceledException` out of the call before the handler has had the turn in which to
notice. With `InvokeFetchHandler` you decide when to stop pumping, so you can give it that turn.

The `FetchEvent` route carries the same signal, so a Workers-shaped script reading `event.request.signal` is
told the same truth.

Request bodies are read in full before the handler runs — bodies here are buffered, not streamed. The
awaitable shape reads them without blocking; the polled shape reads them on the calling thread, so buffer the
content yourself (a `ByteArrayContent`) if it is still arriving off a socket. On the way out, `Content-Length`
and `Transfer-Encoding` are dropped: those describe the message your HTTP stack is actually writing, and a
script's claim about a body it is not sending would be a response-splitting primitive. Everything else is
copied verbatim, with equally-named headers kept apart — several `Set-Cookie`s stay several.

<details>
<summary>ASP.NET Core: a sandboxed per-request script handler</summary>

A documented sample rather than a package: Jint takes no dependency on ASP.NET Core, and this is all the glue
there is. The engine comes from a pool the host owns — one engine per concurrent request, since an `Engine` is
not thread-safe — and every one of them has had `SetFetchHandler` called on it.

```csharp
var scriptOptions = new Options()
    .UseWebApis(WebApiFeatures.Events | WebApiFeatures.Url | WebApiFeatures.Files | WebApiFeatures.Timers)
    .MaxStatements(100_000)
    .LimitMemory(16 * 1024 * 1024)
    .TimeoutInterval(TimeSpan.FromSeconds(2));

app.Map("/{**path}", async (HttpContext context, EnginePool pool, ILogger<Program> logger) =>
{
    using var lease = pool.Rent();                       // an engine with the handler already registered

    var buffer = new MemoryStream();
    await context.Request.Body.CopyToAsync(buffer, context.RequestAborted);

    var inbound = new HttpRequestMessage(new HttpMethod(context.Request.Method), context.Request.GetEncodedUrl())
    {
        Content = new ByteArrayContent(buffer.ToArray()),
    };

    foreach (var header in context.Request.Headers)
    {
        // A content header is refused by the request collection and accepted by the content's, which is how
        // the two halves of System.Net.Http's split are told apart. Jint merges them back into one list.
        if (!inbound.Headers.TryAddWithoutValidation(header.Key, (IEnumerable<string>) header.Value))
        {
            inbound.Content.Headers.TryAddWithoutValidation(header.Key, (IEnumerable<string>) header.Value);
        }
    }

    HttpResponseMessage handled;
    try
    {
        handled = await lease.Engine.Advanced.InvokeFetchHandlerAsync(inbound, context.RequestAborted);
    }
    catch (JintException ex)
    {
        // The host's policy, not Jint's: a script failure is a 500 here, a rendered error page elsewhere.
        logger.LogWarning(ex, "the script handler failed");
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        return;
    }

    using (handled)
    {
        context.Response.StatusCode = (int) handled.StatusCode;
        foreach (var header in handled.Headers.Concat(handled.Content.Headers))
        {
            context.Response.Headers[header.Key] = new StringValues(header.Value.ToArray());
        }

        await handled.Content.CopyToAsync(context.Response.Body, context.RequestAborted);
    }
});
```

Two things worth keeping when you adapt it. The engine must not be shared between concurrent requests, and a
pooled one should have `engine.Advanced.RestoreGlobalSnapshot(...)` called on it between leases so one
request's globals are not visible to the next. Take that snapshot **after** registering the handler, since
registering is what installs `Request`/`Response`/`Headers` and a restore returns the global object to its
state at capture; the handler itself is host state and survives the restore either way, so it never needs
re-registering. And bound the script: the constraints above are what stand between a rented engine and a
handler that decides to loop forever. Note that the `context.RequestAborted` already being passed to the
invoke is what makes the handler's `request.signal` fire when the client disconnects — see
[Telling the handler the client is gone](#telling-the-handler-the-client-is-gone).

</details>

### Text and compression transform streams need two flags

`TextEncoderStream`, `TextDecoderStream`, `CompressionStream` and `DecompressionStream` are each one
standard's algorithm running inside the Streams Standard's machinery, so each needs **both** flags: the text
pair wants `Encoding | Streams`, the compression pair `Compression | Streams`. Naming one half installs
neither global, which is the honest answer for feature detection — and `UseWebApis()` enables all four
anyway. None of them is a `TransformStream` subclass: like a browser, each is its own interface exposing a
`readable` and a `writable`, and the transform behind it is never handed to script.

```javascript
const bytes = textReadable.pipeThrough(new TextEncoderStream()).pipeThrough(new CompressionStream('gzip'));
const text = bytes.pipeThrough(new DecompressionStream('gzip')).pipeThrough(new TextDecoderStream());
```

`TextEncoderStream` carries the standard's leading-surrogate slot, so a surrogate pair split across two
chunks is reassembled into the one scalar value it denotes rather than becoming two U+FFFDs, and
`TextDecoderStream` is the same decoder `TextDecoder` uses with `stream: true` — a UTF-8 sequence, a UTF-16
code unit or a byte order mark split across chunks all decode as if the bytes had arrived in one piece. A
`fatal` decoder errors *both* sides with a `TypeError`, as the standard prescribes.

For compression, note that the standard's `deflate` is **RFC 1950's ZLIB container**, named that way for
consistency with HTTP `Content-Encoding`; raw RFC 1951 DEFLATE is the separate `deflate-raw`. Jint maps them
onto `ZLibStream` and `DeflateStream` accordingly (and `gzip` onto `GZipStream`), so bytes produced here are
the bytes every other implementation expects. `brotli`, which the standard's enumeration also names, is not
implemented and — like any unsupported value — raises a `TypeError`. Input a format rejects (a bad header, a
failed CRC32 or ADLER32, a malformed block) errors both sides with a `TypeError`; two truncation cases the
standard also calls errors are the documented exception, because .NET exposes no incremental inflater that
could report them: a stream that ends mid-member closes cleanly instead, and bytes following a complete
member are ignored. A stream that ends with no compressed bytes at all *is* refused.

### Bridging a stream to `System.IO.Stream`

`Engine.Advanced` connects the two worlds in both directions, so a host never has to write the pump itself:

```csharp
var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Streams | WebApiFeatures.Encoding));

// A .NET stream the script can read, with backpressure: nothing is read until the queue wants a chunk.
engine.SetValue("input", engine.Advanced.CreateReadableStream(File.OpenRead("in.txt")));

// A .NET stream the script can write; `await writer.close()` is its proof the bytes were flushed.
engine.SetValue("output", engine.Advanced.CreateWritableStream(File.Create("out.txt")));

// And a script stream read back into .NET — here, a file transformed through a script TransformStream.
var upperCased = engine.Evaluate("""
    input.pipeThrough(new TransformStream({
      transform(chunk, controller) {
        const text = new TextDecoder().decode(chunk);
        controller.enqueue(new TextEncoder().encode(text.toUpperCase()));
      }
    }))
    """);

var copy = engine.Advanced.StartReadableStreamCopy(upperCased, File.Create("shouted.txt"));
while (!copy.IsCompleted)
{
    engine.Advanced.ProcessTasks();   // the host owns the turns
}

Console.WriteLine($"{copy.GetResult()} bytes");   // throws PromiseRejectedException if the copy failed
```

**The threading contract is the whole design, and it is the same one the timers have.** Everything that
touches the engine happens on the engine's thread, from an event-loop job stamped with the cycle it was
registered in; everything that touches your `Stream` happens on whichever thread the BCL's asynchronous I/O
completes on, and produces nothing but bytes and exceptions. Three consequences:

- **The engine must be given turns or nothing moves.** An `await` in script, `EvaluateAsync`, a blocking
  `UnwrapIfPromise`, or your own `engine.Advanced.ProcessTasks()` loop. An engine nobody pumps never reads a
  byte. A read that completes synchronously — a `MemoryStream`, a warm page cache — is delivered on the spot
  instead, so such a stream costs no thread hop at all.
- **Hand the stream over and stop touching it.** A read or a write may be in flight whenever the script is
  asking for a chunk. By default the engine closes it for you: when the script reads to the end or cancels,
  when it closes or aborts a writable stream, when a copy finishes, or when `RestoreGlobalSnapshot` ends the
  cycle. `LeaveOpen` keeps ownership with you.
- **Pick the copy entry point by who owns the thread.** `StartReadableStreamCopy` returns a polling handle and
  runs nothing on its own, which is what a game loop or a UI thread needs; `CopyReadableStreamAsync` awaits
  instead and runs its turns on whichever thread resumes the await. This is exactly the
  `Engine.Modules.StartImport` / `ImportAsync` split, and for the same reason.

Backpressure is the standard's own, in both directions: `HighWaterMark` chunks are read ahead, `writer.ready`
stops being resolved once that many are queued, and a copy has one chunk in flight at a time. A failure on
your stream errors the script's stream with a `TypeError` whose message names the exception's *type* but not
its text — the exception itself rides the error value, where
[`JintException.TryGetClrException`](#getting-the-original-exception-back) can read it and the script cannot.

What a script may write to a host stream is a `BufferSource`, a `Blob` or a string (UTF-8 encoded). Anything
else is a `TypeError` rather than a stringification, because `[object Object]` silently appended to your file
is not a failure mode worth having.

There is deliberately **no adapter presenting a script's `ReadableStream` as a `System.IO.Stream`**. Its
`Read` would have to drive the engine from whichever thread called it, which is the one thing a single-threaded
engine cannot allow; the copy operation above is the same capability with the thread question answered.


## Node compatibility (opt-in)

Not everything a script expects is a web standard. `NodeStyleModuleLoader`
([npm-style packages](#npm-style-packages-opt-in)) resolves bare specifiers the way Node does; `UseNodeProcess()`
adds the other thing a script written for Node reaches for, a `process` object — most often to read
`process.env.NODE_ENV` or to branch on `process.platform`.

```csharp
var engine = new Engine(options => options.UseNodeProcess(p =>
{
    p.EnvironmentVariableAllowlist = ["NODE_ENV"];
    p.EnvironmentOverrides = new Dictionary<string, string> { ["NODE_ENV"] = "production" };
}));

engine.Evaluate("process.env.NODE_ENV"); // "production"
```

**Not one environment variable is readable until you list it.** `EnvironmentVariableAllowlist` is empty by
default, so `process.env` starts out an empty object, and only the names on it are ever looked up — the engine
never enumerates the environment block, so a variable you did not name is not read, not copied and not
reachable from the engine at all. `EnvironmentOverrides` supplies values of your own, which win over the real
environment and are filtered by the same allowlist, so a test fixture cannot accidentally widen what a script
can see; an entry whose value is `null` hides the variable rather than falling through to the real one.

| Member | What it answers |
| --- | --- |
| `process.env` | The allowed variables, materialized **once** when `process` is first touched — not a live view. Writes, and `delete`, are script-local: they never reach the real environment. |
| `process.platform` | `"win32"`, `"darwin"` or `"linux"` for the platform you are actually on; settable, e.g. to one of Node's other values. |
| `process.version` | `"v0.0.0-jint"` by default — deliberately *not* a Node version. Settable for a dependency that insists on one. |
| `process.versions` | `{ jint: "<assembly version>" }`. There is no `node` key, so feature detection has something truthful to find. |
| `process.argv` | An empty array. Jint launched no process and the script has no path of its own. |
| `process.cwd()` | `WorkingDirectory`, `"/"` by default. **Never the real current directory**, which names a deployment layout and often a user account. |
| `process.nextTick(cb, ...args)` | Queues `cb` onto the engine's job queue with the arguments forwarded, so it runs after the current script and before any timer. |
| `process.hrtime.bigint()` | A monotonic reading in nanoseconds, for measuring elapsed time. The legacy tuple form `process.hrtime()` is absent. |

`exit`, `abort`, `kill` and `chdir` are **absent rather than throwing**: an embedded script must not be able to
act on the host process, and a missing function lets a script's own `typeof` check take its other branch where
a throwing one would not. `process` is a plain object, not Node's `EventEmitter`, so there is no `on`/`emit`
either. One divergence worth knowing: Node drains its next-tick queue ahead of the promise microtask queue,
while Jint has a single job queue, so a `nextTick` callback and a `.then()` reaction run in registration order.

The global is installed lazily and non-clobbering, exactly like the web APIs — a `process` you registered
yourself is left alone whichever order the calls were made in — and a `ShadowRealm` does not get it. Every
option is read once, when `UseNodeProcess` returns, so one `Options` instance stays safe to share across
engines. **Unlike the web APIs above, this needs no particular target framework**: it compiles for every one
Jint targets.

### `node:` builtin modules (opt-in)

After resolution, the next wall a package published for Node runs into is `import 'node:path'`.
`UseNodeBuiltinModules()` supplies the ones that are pure string utilities — no file system, no process, no
network, no clock — and nothing else.

```csharp
var engine = new Engine(options => options
    .EnableModules(new NodeStyleModuleLoader(@"C:\app"))
    .UseNodeBuiltinModules());

engine.Modules.Import("./main.js"); // main.js, and anything in node_modules, may import 'node:path'
```

| Module | What it provides |
| --- | --- |
| `node:path` | `resolve`, `normalize`, `isAbsolute`, `join`, `relative`, `dirname`, `basename`, `extname`, `format`, `parse`, `toNamespacedPath`, `sep`, `delimiter`, and both flavours as `posix` and `win32`. `matchesGlob` is absent — it is the one member that is not string arithmetic. |
| `node:path/posix`, `node:path/win32` | The two flavours as modules of their own. |
| `node:querystring` | `parse`/`decode`, `stringify`/`encode`, `escape`, `unescape`. `unescapeBuffer` is absent: it answers with a `Buffer`. |
| `node:url` | `URL` and `URLSearchParams` (the engine's own WHATWG implementations, re-exported), `fileURLToPath`, `pathToFileURL`, `domainToASCII`, `domainToUnicode`. The legacy `url.parse`/`url.resolve`/`url.format` API is absent. |

**Nothing that touches a platform resource is provided, and that is not a gap to be filled later.**
`node:fs`, `node:buffer`, `node:crypto`, `node:os`, `node:child_process`, `node:http` and their kind are
deliberately absent, so a script feature-detecting one takes its other branch instead of walking into a stub.
An unknown `node:` specifier fails with a message naming what *is* available.

Both spellings work — `import 'path'` as well as `import 'node:path'` — and both name one module, because a
builtin outranks a `node_modules` package of the same name exactly as it does in Node
([ESM_RESOLVE](https://nodejs.org/api/esm.html#resolution-algorithm-specification), PACKAGE_RESOLVE step 3).
Set `AllowUnprefixedSpecifiers = false` for a tree that really does depend on an npm package called `path`,
`url` or `querystring`.

A module you register yourself wins: `engine.Modules.Add("node:path", …)` — or `Add("path", …)`, which
resolves to the same key — replaces the builtin, and is also how you supply one of the modules Jint does not
provide. Everything that is not a builtin keeps going to whichever loader you configured, in whichever order
you called the two methods, and `options.Modules.ModuleLoader` still reads back exactly what you set.

Two options are worth setting. `Platform` decides which flavour `node:path` defaults to and defaults to the
platform you are on, the same answer `process.platform` gives. `WorkingDirectory` is what `path.resolve()` and
`path.relative()` use where Node reads `process.cwd()`; like the `process` shim's, it defaults to `"/"` and
**never** answers the real current directory.

`node:querystring` and `node:url` need .NET 8 or newer, because both build on the engine's WHATWG URL
implementation. `node:path` is available on every target framework Jint has.


### The Cache API stores through a provider you supply

`caches` is the Service Workers Standard's [`CacheStorage`](https://w3c.github.io/ServiceWorker/#cache-interface)
— `open`, `has`, `delete`, `keys`, `match` — with the full `Cache` behind it: `match` / `matchAll` with
`ignoreSearch`, `ignoreMethod` and `ignoreVary`, `put`, `add`, `addAll`, `delete`, `keys`, the request-matching
algorithm including `Vary`, and the batch semantics that make a failed `addAll` store nothing at all.

Jint implements the object model and delegates the storage:

```csharp
var engine = new Engine(options => options.UseCacheApi(cache =>
{
    cache.Provider = myProvider;   // omit for a private in-memory store per engine
}));

var body = engine.Evaluate(
    "(async () => {" +
    "  const cache = await caches.open('v1');" +
    "  await cache.put('https://example.org/a', new Response('hi'));" +
    "  return (await cache.match('https://example.org/a')).text();" +
    "})()").UnwrapIfPromise();
```

A `CacheStorageProvider` opens, lists and deletes named caches; each `CacheStore` lists its entries and applies
one `CacheWrite` — the removals and the additions of a whole operation together, so a provider that writes it
in a transaction gets the standard's all-or-nothing behaviour for free. A request/response pair crosses that
seam as `CacheEntry`, a plain CLR record with no engine reference in it, so it can go into a dictionary, a
file, SQL or Redis. Everything is called on the engine's thread, synchronously, and any exception becomes a
rejection: `CacheQuotaExceededException` as the `QuotaExceededError` a browser raises — optionally carrying
`quota` and `requested` — anything else as a
`TypeError` whose cause your host can read with `JintException.TryGetClrException`.

Enabling it also brings `Headers`, `Request` and `Response` (and `Events`, `Url`, `Files` under them), because
a cache *is* a list of request/response pairs. It does **not** bring the network: `cache.add` and
`cache.addAll` fetch, so they additionally need `UseFetch` and reject with a `TypeError` naming it until they
have it — and when they do have it, they go through the very same policy, so your `UrlFilter`, scheme list,
size cap and deadline bound them exactly as they bound a `fetch` the script wrote itself. Every other `Cache`
method works with no network at all, which is what lets a host populate a cache from its own data and a script
read it back.

**The default store has no quota.** Left unconfigured, each engine gets a private `InMemoryCacheStorageProvider`
that grows until the process runs out of memory, and its contents survive `RestoreGlobalSnapshot` — a restore
reverts global bindings, not host storage. A deployment running untrusted script implements the provider
itself: that is where a size limit, an eviction policy and a per-tenant partition belong. See
[THREAT_MODEL.md](.github/THREAT_MODEL.md) TM-23 for the full analysis.



## Performance

- Because Jint neither generates any .NET bytecode nor uses the DLR it runs relatively small scripts really fast
- If you repeatedly run the same script, you should prepare it for execution using `Engine.PrepareScript` or `Engine.PrepareModule`, cache the returned `Prepared<...>` object and feed it to Jint instead of the content string
- You should prefer running engine in strict mode, it improves performance

You can check out [the engine comparison results](Jint.Benchmark), bear in mind that every use case is different and benchmarks might not reflect your real-world usage.

## Embedding performance

Notes for hosts that project their own objects into script, pool engines, or bound execution. Each of these
is a cost model rather than a rule; the XML documentation on the named APIs has the detail.

**Migrating CLR array projections.** CLR arrays now default to `ArrayConversionMode.Copy`. This is a breaking
compatibility change from the live-view default introduced in 4.14: the safer default gives script a native,
independent `JsArray` snapshot (`Array.isArray` is `true`) that it can mutate and resize without changing the
host's array, and later CLR-side mutations are not visible through that snapshot. The default recent-wrapper
cache means repeated crossings of the same CLR array reuse that first snapshot while it remains cached, so
JavaScript identity and script-side mutations persist across those reads; `TrackObjectWrapperIdentity` extends
that identity for the wrapper-map lifetime, while disabling both caches restores a fresh snapshot per crossing.

Hosts that intentionally need shared mutable state can preserve the previous behavior explicitly:

```c#
var engine = new Engine(options =>
    options.Interop.ArrayConversion = ArrayConversionMode.LiveView);
```

`LiveView` avoids the copy and its allocation, and exposes a fixed-size array-like wrapper. Reads stay connected
to the CLR array, but selecting `LiveView` does not grant write authority: script mutations that would reach the
backing array remain denied unless `AllowClrWrite()` is separately enabled. With both opt-ins, element writes
change the CLR array. Repeated wrappers over the same CLR array compare equal by target identity even with caches
disabled; the wrapper caches determine whether the same wrapper instance is reused. It is not a native JavaScript
array (`Array.isArray` is `false`), and resizing operations throw. Multidimensional and non-zero-based CLR arrays
remain unsupported in either mode: non-empty instances fail during conversion, while zero-length instances
currently fall through as empty snapshots. Choose `Copy` for isolation and JavaScript-array compatibility; opt
into `LiveView` when live observation and avoiding the initial O(N) copy and allocation are required, and
authorize writes separately. Once a snapshot is cached, its native JavaScript-array element access can be as
fast as or faster than traversing the live wrapper.

**Projecting host data.** Subclassing `ObjectInstance` is the most expensive way to expose data. Such a
receiver gets no own-property inline caching — every own read reaches your `GetOwnProperty` and allocates the
`PropertyDescriptor` it returns. Cheaper options, in order of preference:

- For fixed-shape records, do not subclass at all: `JsObject.Create(engine, layout, values)` and
  `JsObject.CreateFromEntries` build straight into the hidden-class representation, so every object sharing a
  `JsObjectLayout` shares one hidden class and a script reading a batch of them keeps a monomorphic inline
  cache. A record with expensive members most items never have read — a body that must be parsed, a field that
  must be decoded — declares them with `JsObjectLayout.CreateBuilder().AddLazy(name, factory)` and passes the
  raw payload as the `lazySlotState` argument of `JsObject.Create`: the factory runs on the first read that
  observes that member's value and the result is memoized on the object, while enumerating keys, `in` and
  `hasOwnProperty` never run it. The object stays a hidden-class object throughout.
- For CLR objects, `engine.SetValue(name, obj)` wraps them in `ObjectWrapper`, whose member resolution and
  compiled accessors are cached process-wide on the `TypeResolver`.
- For the *prototypes* those objects sit behind, declare the members once per process with `JsObjectShape` and
  create one object per engine with `Instantiate`: members materialize only when a script touches them, and
  because the engine stores and versions the whole member set itself, a shaped prototype can serve the
  prototype-method inline cache — which an `ObjectInstance` subclass used as a prototype can never do.
- For a **live indexed collection** — a DOM `NodeList`, a result window, any list computed on demand — derive
  from `ArrayLikeObject` rather than assembling the property model yourself. You implement two members,
  `uint Length` and `bool TryGetIndex(uint index, out JsValue value)`; the base class derives everything else
  (index and `length` descriptors, enumeration order, the existence and value hooks, WebIDL-shaped `delete` /
  `defineProperty` refusals) and the engine keys two lanes on the type, so `list[i]`, `Array.prototype`
  generics, `for-of` / spread / `Array.from` / destructuring and `JSON.stringify` each cost one
  `TryGetIndex` per element with no descriptor and no key allocation. It is array-*like*, not an array:
  `Array.isArray` stays `false` by design, the same answer a browser gives for a `NodeList`. If your backing
  store can test containment more cheaply than it can produce an element, also override
  `protected virtual bool HasIndex(uint index)`, and `in` / `hasOwnProperty` / `Object.keys` / `delete` stop
  projecting elements they only ever discard. Reach for it when the collection is live; when it is a snapshot,
  copying into a `JsArray` once is cheaper still — and give it a `JsObjectShape` prototype, per the bullet
  above, for the collection's own methods.
- If you must subclass, override `TryGetOwnPropertyValue` so an own read hands the value over with no
  descriptor at all, and `ProbeOwnProperty` so existence and enumerability questions (`in`, `Object.keys`,
  spread, `JSON.stringify`) are answered without materializing one either. Both carry an obligation to agree
  with `GetOwnProperty`, and neither is re-verified on the hot path — a `ProbeOwnProperty` that wrongly reports
  a key as absent drops it from every enumeration, silently. Run your integration suite once with
  **host-contract verification** on and every such disagreement throws instead, naming the type, the key and
  both answers:

  ```csharp
  // before the first use of any Jint type — the flag is read once, at type initialization
  AppContext.SetSwitch("Jint.EnableHostContractVerification", true);
  ```

  It also checks a declared `PropertyAccessSemantics.Ordinary`, an `ArrayLikeObject`'s `HasIndex`, and an
  `IObjectConverter` registered with `AddObjectConverter(converter, handledTypes)` converting a type it did not
  declare. Turn it on in a test or staging host, never in production: the checks deliberately redo the work the
  hooks exist to avoid. A Debug build of Jint has them on already and needs no switch.

**Lazy values.** `PropertyFlag.CustomJsValue` is the supported hook for a property whose value is *computed on
every read*: a `PropertyDescriptor` subclass overriding `CustomValue` keeps working under the read inline
caches, because every caching lane re-reads the flag on each hit and caches the descriptor reference rather
than a value snapshot. When the value is lazy only *once*, use `PropertyDescriptor.CreateLazy(state, factory)`
instead — it memoizes the produced value and then stops being custom-valued, which readmits the property to the
member-write fast path and the global-identifier cache that a permanently custom-valued descriptor is declined
by; store it wherever you store descriptors (`SetOwnProperty` or `GetOwnProperty` on a host subclass,
`FastSetProperty`, a hand-rolled global). It is the descriptor-shaped member of the same family as
`JsObjectLayout.AddLazy` (records) and `JsObjectShape` (prototypes), and it does not exempt you from the rule
above them: storing any raw descriptor under a string key still moves a shape-mode object to the dictionary
representation. For a whole global that may never be touched, `Options.AddLazyGlobal` defers building
the value until script reads the name, and `engine.Advanced.AddLazyGlobal` does the same on an engine that
already exists — which is what you need when the value comes from the request you are about to serve rather
than from process-wide configuration. Both install the property eagerly, so `in`, `hasOwnProperty` and
`Object.keys(globalThis)` see the name without building anything; only reading the value runs the factory,
once. The per-engine overload receives its engine, so unlike an `Options`-registered factory it may capture
engine-affine state — and where it would do nothing but capture, the overload taking the state,
`engine.Advanced.AddLazyGlobal(name, state, static (e, s) => ...)`, hands it to a `static` factory instead.
That matters only because this registration is per engine: a capturing factory costs a display class and a
delegate for every global on every engine you build, which on `FreshEngineGlobalsBenchmark`'s forty-global
row is 32 bytes per global. There is deliberately no `Options` counterpart — a registration made there is
recorded once for the process and replayed per engine, so its closure is already a one-off.

**Per-request state behind an engine.** Every host-facing factory in this API receives the engine and nothing
else, which is a problem when the value depends on the request rather than on process-wide configuration.
`engine.Advanced.HostDefined` closes that gap: an opaque `object?` the engine never reads or interprets — the
`[[HostDefined]]` field the specification reserves on a Realm Record — so a factory that captures nothing can
still reach the scope it is running in. The alternative embedders reach for is a
`static ConditionalWeakTable<Engine, IServiceProvider>`, which takes its internal write lock every time a
host associates state with an engine, across every tenant in the process.

```c#
// once per process — the factory captures nothing, so one Options serves every engine
var options = new Options()
    .AddLazyGlobal("user", static engine =>
        JsValue.FromObject(engine, ((RequestContext) engine.Advanced.HostDefined!).User));

// per request, before you run anything
engine.Advanced.HostDefined = requestContext;
```

It is the *principal* realm's field, not the current one, so it answers the same value inside a `ShadowRealm`
callback as outside. A shadow realm is a distinct realm and gets its own, empty by specification — deliberate,
since propagating the outer request's services into sandboxed code is exactly the ambient authority a shadow
realm exists to withhold; `Host.InitializeShadowRealm` is the hook if you do want to populate it. The value
dies with the engine, and nothing in the engine clears it: a restore (below) does not touch it, so a pooled
engine keeps it across one and you replace it yourself, typically right after restoring.

**Sparse data.** Hosts that read deep chains off optional data — `input.Address.City.length`, where any link
may be absent — usually install an `IReferenceResolver` so a nullish base yields a value instead of throwing.
Register `NullPropagatingReferenceResolver.Instance` rather than writing that class yourself: the engine
recognizes the singleton and serves the propagation inline, with no interface call and no pooled `Reference`
per nullish read, which an equivalent hand-written resolver cannot get. Pass
`ReferenceResolverInterests.NullishPropertyBase` alongside it so every unrelated read lane stays armed. The
boundary is that *reads* propagate: a call on a nullish base still throws, and a host that needs callable
substitution or unresolvable-identifier handling writes its own resolver and forgoes the inline lane.

**Prepared scripts and engine reuse.** `Engine.PrepareScript` / `PrepareModule` return an object that is
reusable *and* thread-safe: prepare once at startup and feed the same `Prepared<T>` to as many engines, on as
many threads, as you like. The engine's own per-node caches are a separate matter — they are engine-owned and
engage only on the **second** evaluation of a given script on a given engine, so a host that builds a fresh
engine per operation never reaches them by design. Note the mirror image if you pool engines instead: a
warmed call site holds a reference to the last receiver it served until it caches a different one, so pooled
engines can keep host objects alive between runs.

**Sharing a module graph across pooled engines.** A host whose templates or plugins are ES modules pays for
that graph per engine: `IModuleLoader.LoadModule` is called by every engine that imports a module, so the
obvious loader re-reads and re-parses the whole graph for every engine in the pool. Prepare each module once
per *build* instead — cache the `Prepared<AstModule>` (`AstModule` being Acornima's `Module`, which an alias
keeps apart from Jint's runtime `Module`) keyed by module location, and hand it to the overload that takes an
already prepared AST, `ModuleFactory.BuildSourceTextModule(engine, in prepared)`. Name the prepared module
exactly as the engine would: `Engine.PrepareModule(code, source)` takes the name up front, and it becomes
`Module.Location` and therefore the `referencingModuleLocation` echoed back into `IModuleLoader.Resolve` for
that module's own imports — so a name derived by hand that differs from the engine's breaks relative-import
resolution with nothing to point at. `ModuleFactory.LocationOf(resolved)` *is* that rule; call it rather than
reimplementing it.

Single-flight the cache. `ConcurrentDictionary.GetOrAdd` does not run its factory under a lock, so N engines
filling a pool concurrently each prepare the same module and N-1 results are prepared only to be discarded.
Wrapping the value in a `Lazy<T>` — whose default thread-safety mode is exactly "one factory run, everyone
else waits" — is enough:

```c#
using AstModule = Acornima.Ast.Module; // Jint.Runtime.Modules.Module is a different type

private readonly ConcurrentDictionary<string, Lazy<Prepared<AstModule>>> _prepared
    = new(StringComparer.Ordinal);

public Module LoadModule(Engine engine, ResolvedSpecifier resolved)
{
    var location = ModuleFactory.LocationOf(resolved);
    var prepared = _prepared.GetOrAdd(location,
        static key => new Lazy<Prepared<AstModule>>(() => Engine.PrepareModule(ReadSource(key), key))).Value;

    return ModuleFactory.BuildSourceTextModule(engine, in prepared);
}
```

Invalidate by build, not by file. The prepared ASTs are derived from a compilation, so key the cache to the
same identity the rest of your host already uses for that compilation and let a rebuild drop the whole set at
once, rather than expiring entries per file and serving a graph half of which is stale.

Be clear about what the thread-safety of `Prepared<T>` buys. It is safe to share and safe to run concurrently,
which is what lets one cache serve the pool — it does not make the *engines* shareable (one engine, one
thread) and it does not make the module registry shared: every engine still builds, links and evaluates its
own module records from the shared ASTs, and `engine.Modules` is per engine. That per-engine cost is real, and
it is what the pool pays no matter how much preparation is shared.

`StaticAnalysis` is a trade with a break-even, not a speed-up. Preparing with
`new ModulePreparationOptions { StaticAnalysis = false }` skips the pass that pre-publishes interpreter state
onto the parsed tree, leaving preparation at roughly the cost of a plain parse; each engine then rebuilds
lazily what the pass would have published once for all of them. Measured on `ModuleGraphEmbeddingBenchmark`'s
ten-module graph, preparation cost -47.6% time and -27.5% allocation, while each engine materializing that
graph from the shared cache paid +4.8% time and +9.5% allocation — break-even, on that graph, at about ten
engines on time and about two on allocation. A long-lived pool should therefore keep the default (`true`);
the option is for preparation that sits on a latency-critical path — a cold start, a rebuild in a dev loop —
or for a host whose prepared programs outnumber the engines that ever run them.
`ScriptPreparationOptions.StaticAnalysis` is the same option for scripts.

**Registering only what a script uses.** When the ambient API is large and scripts touch little of it,
prepare with `ScriptPreparationOptions.CollectReferencedGlobals` and read `Prepared<T>.ReferencedGlobals`:
the free identifiers the program actually references, resolved per binding site, as an immutable set you can
intersect with your registry — including the CLR-side context you would otherwise build speculatively, which
lazy globals cannot defer. Honor `HasDirectEvalCall`: a program with direct `eval` can reference anything,
so install everything for those.

**Reusing a configured engine.** If you build a fresh engine per evaluation only because you need a clean
global, `engine.Advanced.CaptureGlobalSnapshot()` and `RestoreGlobalSnapshot(snapshot)` are the cheaper route:
capture once after your `SetValue` calls and module setup, then restore between evaluations. Restore reverts
the global object's own properties, its prototype and extensibility, and the top-level `let`/`const`/`class`
declarations (which nothing else can clear, so a script with a top-level `let` can otherwise only be run once
per engine); it also clears the `RegExp.$1`-style legacy statics and resets the interop wrapper caches. The
per-node caches above are deliberately kept, so the next run starts warm. Keep the snapshot in a field beside
the engine and put the restore in a `finally` — a script that throws still declared its globals, so restoring
only on the success path hands them to the next caller. `engine.Advanced.WithRestoredGlobals(snapshot, action)`
is exactly that `try`/`finally` in one call, so the restore cannot be left off the throwing path; it adds
nothing else, and in particular no isolation the restore does not already give you.

Choosing between this and `AddLazyGlobal` is a question of engine lifetime: a fresh-engine-per-evaluation
host wants lazy globals (nothing to restore — the win is never building what the script does not read); a
pooled host wants the snapshot. They compose: restore returns a global that was still lazy at capture to its
unmaterialized state, so a pooled engine keeps both benefits.

Restore also ends the previous cycle on the event loop. Queued jobs are discarded, and — because discarding
cannot reach work that has not been enqueued yet — any promise registered before the restore is dropped when
it settles instead of resuming its continuation, so a fire-and-forget `async` function suspended on a host
`Task` never wakes up against the restored globals. Register a promise that is meant to outlive a restore
*after* it. Restore refuses (`InvalidOperationException`) while an evaluation is in progress, including an
`EvaluateAsync`/`ExecuteAsync`/`InvokeAsync` whose `Task` you still hold. What it cannot fence is you calling
back in: invoking a function a previous evaluation handed you runs it against the restored surface.

**It is a configuration-reuse primitive, not an isolation boundary** — mutations of `Object.prototype` and
other intrinsics, of object graphs behind restored bindings, of host CLR state, plus `Symbol.for`
registrations and registered modules, all survive a restore, so mutually distrusting scripts still need
separate engines. Note that surviving intrinsic pollution is a surviving *binding*, not just a surviving
property: bare identifiers resolve through the global's prototype chain, so `Object.prototype.leaked = 1` in
one evaluation makes `leaked` a name the next one can read with no qualifier. (The global's own
`[[Prototype]]` is captured and restored, so a `setPrototypeOf(globalThis, …)` is reverted.) A snapshot also
keeps its engine and every captured value strongly reachable, so do not cache one past that engine's
lifetime.

**Constraints and options.** An `Options` instance is meant to be shared across engines, including concurrent
ones — the built-in constraint helpers register a *factory*, so each engine gets its own counter and its own
deadline. (Sharing it is not required: building an `Options` per scope is fine when your globals depend on
scoped state.) Watch for the sentinel trap: `MaxStatements(int.MaxValue)`, `LimitMemory(long.MaxValue)` and
`TimeoutInterval(TimeSpan.MaxValue)` register *no* constraint at all and remove any previously registered one
of that kind, so spelling "effectively unlimited" that way leaves you with no limit rather than a large one.
`LimitMemory` is a managed-allocation budget, not a retained-heap or process-memory quota. It accounts only
while an engine thread is actively executing the operation, including synchronous host callbacks, and carries
the accumulated total with promise and module continuations when they resume on another thread. It does not
charge unrelated allocations while an async operation is suspended.

**Values do not cross engines.** A `JsValue` that is an `ObjectInstance` holds a hard reference to the engine
and realm that created it, and passing one to a different engine is not supported — it is neither validated
nor made safe. `Prepared<Script>` / `Prepared<Module>` and `ModuleBuilder` are the supported ways to share
work between engines. For a script result, prefer
`engine.Advanced.ConvertResult(value, limits)`: it copies arrays, typed arrays, array buffers, maps, sets and
enumerable own properties into a detached CLR data graph while incrementally enforcing depth, cumulative
property/element count, individual string length, aggregate characters and binary bytes. Cycles, functions and
symbols are rejected. A CLR wrapper is already host-owned, so conversion returns its target without walking or
limiting that target's graph.

**Bound untrusted output.** `Options.ResultLimits` defaults to `ResultLimits.Unlimited` for compatibility.
`ResultLimits.Conservative` is an opt-in starting point, not a complete sandbox profile:

```csharp
var engine = new Engine(options =>
{
    options.ResultLimits = ResultLimits.Conservative;
    options.TimeoutInterval(TimeSpan.FromSeconds(2));
    options.MaxStatements(50_000);
    options.LimitMemory(16_000_000);
});

var value = engine.Evaluate(source);
var result = engine.Advanced.ConvertResult(value);
```

The same option bounds Jint's `JsonSerializer` and script-visible `JSON.stringify`; per-call overloads can use
a tighter policy. JSON output counts escaped UTF-16 characters before appending and exact UTF-8 bytes before
touching an `IBufferWriter<byte>`. Conversion and serialization run under execution constraints because getters,
proxy traps, `toJSON`, replacers and error `stack` accessors can execute script. Limits do not make host code
interruptible: pair them with time, statement, cancellation, memory and stack constraints plus an outer worker
deadline.

`Evaluate`, `ConvertResult`, `JsonSerializer`, and bounded error rendering are separate top-level entries. If
evaluation plus result handling is one request, bracket every phase with the same `OperationDeadlineConstraint`
`Begin`/`End` pair; otherwise each phase receives a fresh ordinary per-entry budget.

For CLR conversion, `MaxPropertyCount` is the structural-work and container-allocation bound. Shared references
that are not cycles are copied once per occurrence, so a graph can amplify as it is detached; string, character
and binary-byte limits do not bound that shape by themselves.

Jint cannot impose these limits inside external serializers. `System.Text.Json`, Newtonsoft.Json, a configured
`Interop.SerializeToJson` delegate, custom logging/diagnostic formatters, and serialization of a returned CLR
wrapper target remain the host's responsibility. The delegate's returned JSON text is capped before Jint copies
it, but any work or allocation the delegate performed to produce that string has already happened. Standard
`Exception.ToString()` and debugger frontends are external sinks too; use
`JavaScriptException.GetJavaScriptErrorString(limits)` for bounded script-error text.

## Profiling scripts (opt-in)

When a script is slow and you want to know which of *its* functions is responsible, opt the engine in and
bracket the run. The profiler is **evented, not sampling** — it records at the call boundary on the engine's
own thread, because inspecting a running engine from another thread is not something Jint supports.

```csharp
var engine = new Engine(options => options.Profiling.Enabled = true);

engine.Advanced.StartProfiling();
engine.Execute(script);
var profile = engine.Advanced.StopProfiling();

using var file = File.Create("run.speedscope.json");
profile.WriteSpeedscopeJson(file);
```

Drop the file onto [speedscope.app](https://www.speedscope.app) — it is written in that tool's
[evented profile format](https://www.speedscope.app/file-format-schema.json), in nanoseconds. The
`ScriptProfile` is also readable directly: `Frames` (name, file, line, column), `Events` (open/close plus a
timestamp), `Truncated` and `Duration`. It holds strings and numbers only, so keeping a profile does not keep
the engine that produced it alive.

Worth knowing before you read a profile:

- **It costs.** Every recorded call pays a frame lookup and a 16-byte event, so a profiled run is slower than
  an unprofiled one. An engine that is not profiling pays one null test per call-stack push and pop.
- **`Options.Profiling.MaxEvents`** (default one million) caps a session. Reaching it stops recording, closes
  whatever was open and sets `Truncated`; the event stream stays balanced and replayable either way — through
  exceptions, through `ResetCallStack()`, and through a session stopped mid-call.
- **A tail call is a close followed by an open**, since it replaces its caller's frame rather than nesting in
  it. Unbounded tail recursion therefore profiles as a flat run of siblings, not as a bottomless tree.
- **Some calls are elided**, and always in a way that keeps the tree coherent rather than merely incomplete —
  the eliding call has no frame either, so anything it calls is still recorded at the depth the call stack
  really has. Those are: trivial built-ins taken through the frameless fast-call lane (`Math.abs(1)` on a warm
  call site), callbacks a built-in invokes per element (`arr.map(cb)` records `map`, not `cb`), and promise
  reaction handlers (`p.then(cb)` records `then`, not `cb`).

`StartProfiling()` throws `InvalidOperationException` when `Options.Profiling.Enabled` is false, which is the
default — an engine running untrusted script can refuse profiling outright.

## Discussion

Join the chat on [Gitter](https://gitter.im/sebastienros/jint) or post your questions with the `jint` tag on [stackoverflow](http://stackoverflow.com/questions/tagged/jint).

## Video

Here is a short video of how Jint works and some sample usage

https://docs.microsoft.com/shows/code-conversations/sebastien-ros-on-jint-javascript-interpreter-net

## Thread-safety

An `Engine` is single-operation, not thread-safe. Public host entries fail fast with
`InvalidOperationException` when another thread is using the same engine or while one of its async APIs
is still outstanding; callers are never silently serialized. Same-thread re-entry from a host callback is
supported for synchronous APIs. A JavaScript callback converted to a CLR delegate may also be dispatched
to another thread by the host: while host code is running, or while the async operation that received the
callback remains outstanding, Jint reserves the engine against unrelated callers and transfers ownership
to that callback one turn at a time. Such an authorized transfer may wait for the current callback turn;
ordinary public callers still fail immediately rather than being serialized. Starting an async engine API
from inside an active engine call is rejected: the callback must return before ownership can transfer
safely. A top-level awaited async operation may resume on a different thread after ownership transfers.
Background task and module completions may enqueue work safely, but they do not grant permission for other
host calls while the owning async operation is pending.

Keep one engine exclusively assigned to one request or operation at a time. If engines are pooled, await
`EvaluateAsync`, `ExecuteAsync`, `InvokeAsync`, `ImportAsync`, or `UnwrapIfPromiseAsync` before returning an
engine to the pool. Direct mutation through engine-owned objects such as `engine.Global` remains subject to
the same contract and cannot be guarded by the `Engine` entry-point check. `Dispose` also fails fast while
an operation owns the engine; await or finish that operation before disposing. This is observable during
exception unwinding too, so a `using` scope must not outlive an async engine operation.

## Examples

This example defines a new value named `log` pointing to `Console.WriteLine`, then runs
a script calling `log('Hello World!')`. 

```c#
var engine = new Engine()
    .SetValue("log", new Action<object>(Console.WriteLine));
    
engine.Execute(@"
    function hello() { 
        log('Hello World');
    };
 
    hello();
");
```

Here, the variable `x` is set to `3` and `x * x` is evaluated in JavaScript. The result is returned to .NET directly, in this case as a `double` value `9`. 
```c#
var square = new Engine()
    .SetValue("x", 3) // define a new variable
    .Evaluate("x * x") // evaluate a statement
    .ToObject(); // converts the value to .NET
```

You can also directly pass POCOs or anonymous objects and use them from JavaScript. Direct writes through
projected CLR objects are disabled by default. Opt in with `AllowClrWrite()` when scripts should be able to
change CLR fields, properties, indexers, dictionaries, lists, or arrays:

```c#
var p = new Person {
    Name = "Mickey Mouse"
};

var engine = new Engine(options => options.AllowClrWrite())
    .SetValue("p", p)
    .Execute("p.Name = 'Minnie'");

Assert.AreEqual("Minnie", p.Name);
```

This is a breaking default change. Applications upgrading from a version where CLR writes were enabled by
default must add `AllowClrWrite()` to preserve that behavior. The option only controls direct writes through
Jint's projected-object wrappers; it does not make a projected object immutable. CLR methods and registered
extension methods remain callable and can mutate host state:

```c#
var engine = new Engine(options => options.AddExtensionMethods(typeof(MyExtensions)));
```

Do not expose mutating methods to untrusted scripts when method side effects are outside the intended
capability set.

You can invoke JavaScript function reference
```c#
var result = new Engine()
    .Execute("function add(a, b) { return a + b; }")
    .Invoke("add",1, 2); // -> 3
```
or directly by name 
```c#
var engine = new Engine()
   .Execute("function add(a, b) { return a + b; }");

engine.Invoke("add", 1, 2); // -> 3
```
## Accessing .NET assemblies and classes

You can allow an engine to access types from the core assembly that contains `System.Object` by configuring the engine instance like this:
```c#
var engine = new Engine(cfg => cfg.AllowClr());
```

Then you have access to the `System` namespace as a global value. Here is how it's used in the context on the command line utility:
```javascript
jint> var file = new System.IO.StreamWriter('log.txt');
jint> file.WriteLine('Hello World !');
jint> file.Dispose();
```
And even create shortcuts to common .NET methods
```javascript
jint> var log = System.Console.WriteLine;
jint> log('Hello World !');
=> "Hello World !"
```

When allowing the CLR, you can instead pass the exact assemblies from which namespace lookup may resolve types.
```c#
var engine = new Engine(cfg => cfg
    .AllowClr(typeof(Bar).Assembly)
);
```

`AllowedAssemblies` is a closed namespace-discovery allow-list. Namespace lookup admits only effectively
public types: a top-level type must be public, and every declaring type in a nested chain must be public.
Supplying an assembly does not implicitly add the core runtime, Jint, the calling assembly, or the executing
assembly. Add every assembly whose public types must be discoverable:

```c#
var engine = new Engine(cfg => cfg.AllowClr(
    typeof(object).Assembly,
    typeof(Bar).Assembly));
```

This is a security-relevant compatibility change. Earlier releases could resolve core runtime and Jint types
outside a narrow `AllowedAssemblies` list. Hosts that intentionally relied on that fallback must now list the
required assemblies. `AllowClr()` without arguments continues to add the assembly containing `System.Object`.
Passing an explicitly empty assembly array adds nothing, so a dynamically computed allow-list fails closed.

This list is not a complete sandbox for everything a discovered type can do. An admitted API may itself load
assemblies, resolve types, access files, start processes, or return powerful objects. For untrusted scripts,
prefer leaving CLR access disabled; otherwise combine the minimum assembly set with a positive
`TypeResolver.MemberFilter`, purpose-built projected capabilities, and process isolation.

The boundary applies to `System` and `importNamespace`, including nested and generic type definitions.
`TypeResolver.MemberFilter` is also consulted for each discovered type and for nested types. Generic type
arguments must already be `TypeReference` values; passing one does not make its assembly discoverable.
An explicitly exported `TypeReference`, such as the example below, remains a host-granted capability even
when its assembly is absent from `AllowedAssemblies`, while its constructors, static members, and nested
types still obey `MemberFilter`. Converting that explicit reference to a `System.Type` object and back through
`clrHelper` preserves the same capability; an unrelated `System.Type` object must satisfy the namespace policy.

`AllowGetType` exposes instance `object.GetType` and the type-widening `clrHelper.unwrap`, `typeOf`,
`typeToObject`, and `objectToType` operations. This means `clrHelper.unwrap` now throws unless
`AllowGetType` is enabled. The helper operations cannot widen to a type rejected by the namespace policy.
Jint filters the static `System.Type.GetType(string)` family from ordinary member resolution, but
`AllowGetType` is not a general type-resolution or reflection sandbox: other APIs admitted by the host can
carry equivalent authority. `AllowSystemReflection` must be enabled before namespace lookup may resolve
`System.Reflection` types; leave both options disabled for untrusted scripts.

and then to assign local namespaces the same way `System` does it for you, use `importNamespace`
```javascript
jint> var Foo = importNamespace('Foo');
jint> var bar = new Foo.Bar();
jint> log(bar.ToString());
```    

adding a specific CLR type reference can be done like this
```csharp
engine.SetValue("TheType", TypeReference.CreateTypeReference<TheType>(engine));
```

and used this way
```javascript
jint> var o = new TheType();
```

Generic types are also supported. Here is how to declare, instantiate and use a `List<string>`:
```javascript
jint> var ListOfString = System.Collections.Generic.List(System.String);
jint> var list = new ListOfString();
jint> list.Add('foo');
jint> list.Add(1); // automatically converted to String
jint> list.Count; // 2
```

### Intercepting access to .NET objects

ECMAScript Proxy traps can be implemented in .NET by deriving from `ProxyHandler` and creating the proxy via `engine.Advanced.CreateProxy` (or `CreateRevocableProxy`). A trap returning `null` forwards the operation to the target, exactly like an absent trap on a JavaScript handler object, and all proxy invariants are enforced on trap results. Combine with `SetWrapObjectHandler` to transparently intercept every wrapped .NET object. Note that `proxy.method()` fires the `Get` trap (then calls the returned value) — the `Apply` trap only fires when the proxy itself is invoked, so intercepting method calls means returning a wrapping function from `Get`. `new Proxy(target, handlerObject)` from script remains the JavaScript-side equivalent.

```c#
class LoggingHandler : ProxyHandler
{
    public override JsValue? Get(ObjectInstance target, JsValue property, JsValue receiver)
    {
        Console.WriteLine($"get {property}");
        return null; // forward to the target
    }

    public override bool? Has(ObjectInstance target, JsValue property)
    {
        Console.WriteLine($"has {property}");
        return null;
    }
}

var engine = new Engine(options =>
{
    // wrap every interop object in a logging proxy
    options.SetWrapObjectHandler((e, obj, type) =>
        e.Advanced.CreateProxy(ObjectWrapper.Create(e, obj, type), new LoggingHandler()));
});
```

## Internationalization

You can enforce what Time Zone or Culture the engine should use when locale JavaScript methods are used if you don't want to use the computer's default values.

This example forces the Time Zone to Pacific Standard Time.
```c#
var PST = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
var engine = new Engine(cfg => cfg.LocalTimeZone(PST));
    
engine.Execute("new Date().toString()"); // Wed Dec 31 1969 16:00:00 GMT-08:00
```

This example is using French as the default culture.
```c#
var FR = CultureInfo.GetCultureInfo("fr-FR");
var engine = new Engine(cfg => cfg.Culture(FR));
    
engine.Execute("new Number(1.23).toString()"); // 1.23
engine.Execute("new Number(1.23).toLocaleString()"); // 1,23
```

### Extending Temporal and Intl with custom providers

Jint ships English-only / ISO-only defaults to keep the binary small. Non-English CLDR
data and full IANA timezone DST history are reachable via two pluggable providers:

| Extension point | Default | What it covers | Reusable example |
| --- | --- | --- | --- |
| `Options.Temporal.TimeZoneProvider` (`ITimeZoneProvider`) | `DefaultTimeZoneProvider` (BCL `TimeZoneInfo` + Windows↔IANA mapping) | UTC offsets, DST transitions, IANA canonicalization | [`NodaTimeZoneProvider.cs`](Jint.Tests.Test262/NodaTimeZoneProvider.cs) — uses NodaTime TZDB for full historical accuracy |
| `Options.Intl.CldrProvider` (`ICldrProvider`) | `DefaultCldrProvider` (English + .NET `CultureInfo`) | locale names, currencies, units, plural rules, calendar month/era names | [`IcuCldrProvider.cs`](Jint.Tests.Test262/IcuCldrProvider.cs) — uses ICU4N for full CLDR coverage |

The provider files in `Jint.Tests.Test262/` are **MIT-licensed copy-and-modify templates**,
not a stable API contract. They are also exactly how the test suite reaches its current
test262 conformance numbers.

To wire them into your project, add the same NuGet packages (`NodaTime`, `ICU4N`), copy
the provider files in, and register them on `Engine` options:

```c#
var engine = new Engine(options =>
{
    options.Temporal.TimeZoneProvider = new NodaTimeZoneProvider();
    options.Intl.CldrProvider          = new IcuCldrProvider();
});
```

A separate `ICalendarProvider` for non-ISO calendar arithmetic (Islamic UmAlQura, Persian
astronomical, Chinese/Dangi at extreme dates) is on the roadmap; until then non-ISO
calendars use `System.Globalization.Calendar` subclasses, which constrains some date
ranges (see [`Jint/Native/Temporal/NonIsoCalendars.cs`](Jint/Native/Temporal/NonIsoCalendars.cs)).

## Running untrusted code

`ForUntrustedCode` applies Jint's opt-in hardened profile. It disables CLR namespace and reflection access,
registered extension methods, module loading, debugger handling, blocking `Atomics.wait`, writes through
projected CLR objects, live views over CLR arrays, and `eval`/function constructors that compile strings. It
also enables the native stack-overflow guard.

Core execution limits are required rather than guessed: a useful budget depends on the request and workload,
and a security API must not silently turn saturated sentinels into "unlimited." Parser, module-graph, and
result limits have conservative finite defaults that should still be tuned for the host:

```c#
var limits = new UntrustedCodeLimits(
    timeoutInterval: TimeSpan.FromSeconds(1),       // one Engine entry
    maxStatements: 100_000,                        // one Engine entry
    memoryLimit: 16_000_000,                       // one Engine entry
    maxRecursionDepth: 64,
    maxArraySize: 10_000,
    regexTimeout: TimeSpan.FromMilliseconds(250),
    promiseTimeout: TimeSpan.FromSeconds(1),
    maxOperationDuration: TimeSpan.FromSeconds(2),
    maxSourceLength: 100_000,
    maxNodeCount: 25_000,
    maxModuleCount: 50,
    maxTotalModuleSourceBytes: 1_000_000,
    maxModuleGraphDepth: 10,
    maxModuleResolutionHops: 200,
    resultLimits: new ResultLimits(
        maxDepth: 16,
        maxPropertyCount: 10_000,
        maxStringLength: 100_000,
        maxOutputCharacters: 1_000_000,
        maxOutputBytes: 2_000_000));

// Build once and share concurrently. ForUntrustedCode records an immutable profile declaration;
// every Engine gets a private effective snapshot and construction never mutates sharedOptions.
var sharedOptions = new Options().Strict().ForUntrustedCode(limits);
using var engine = new Engine(sharedOptions);

using (limits.BeginOperation(engine, cancellationToken))
{
    try
    {
        var value = engine.Evaluate(source);
        var json = new JsonSerializer(engine).Serialize(value).AsString();
        return Encoding.UTF8.GetBytes(json); // keep the transport's own response-byte cap too
    }
    catch (JavaScriptException exception)
    {
        return Encoding.UTF8.GetBytes(exception.GetJavaScriptErrorString(limits.ResultLimits));
    }
}
```

`TimeoutInterval` and statement count reset around each top-level engine entry. `BeginOperation` requires a
cancellable host token and spans one cumulative wall-clock deadline and managed-allocation budget across
evaluation/import, callbacks, `ConvertResult`, `JsonSerializer`, and bounded error rendering. Ordinary
statement limits intentionally remain per-entry. Like Jint's other constraints, these are cooperative: they
cannot preempt a host callback that does not return. Keep an outer worker/request deadline as the hard stop.
Scopes cannot overlap, cannot end while async work still owns the engine, and failed disposal remains
retryable after that work completes.

Prepared scripts and modules carry the regex timeout selected when they were prepared. When preparing
untrusted code for reuse, pass the same limit explicitly:

```c#
var prepared = Engine.PrepareScript(source, new ScriptPreparationOptions
{
    ParsingOptions = new ScriptParsingOptions { RegexTimeout = limits.RegexTimeout }
});
```

Configuration order cannot reopen profile-controlled settings. The profile is applied to the private engine
snapshot before construction and reapplied after user `Configure` callbacks; registered extension methods,
custom converters/factories, detailed errors, unsafe CLR policies, loaders, and programmatic modules are
removed. User callbacks remain an honestly reported `JINTSEC031` host capability. Mutating the shared source
`Options` after engines are being constructed is still unsupported; finish configuration before sharing it.
The compatibility tradeoffs are intentional: projected CLR arrays become snapshots, projected CLR members
are read-only, registered extension methods are removed, dynamic string compilation and module imports fail,
and `Atomics.wait` raises a JavaScript `TypeError`. Projected methods/delegates and callbacks deliberately
exposed by the host remain capabilities. The profile is defense in depth, not process isolation; mutually
distrusting scripts should still use separate engines in disposable least-privileged workers with OS CPU,
memory, filesystem, network, and lifetime controls.

## Execution Constraints 

Execution constraints are used during script execution to ensure that requirements around resource consumption are met, for example:

* Scripts should not use more than X memory.
* Scripts should only run for a maximum amount of time.

### Validating options for untrusted scripts

Jint does not change its general-purpose defaults when a host runs untrusted source. A host can inspect its
fully configured `Options` before constructing an engine and enforce the built-in untrusted-script policy
explicitly:

```c#
var options = new Options()
    .MaxStatements(100_000)
    .LimitMemory(4_000_000)
    .TimeoutInterval(TimeSpan.FromSeconds(2))
    .CancellationToken(requestAborted)
    .Constraint(static () => new OperationDeadlineConstraint())
    .LimitRecursion(64)
    .DisableStringCompilation()
    .AllowClrWrite(false)
    .MaxArraySize(100_000)
    .RegexTimeoutInterval(TimeSpan.FromSeconds(1));

options.AgentCanSuspend = false;
options.Constraints.StackOverflowGuard = true;
options.Constraints.PromiseTimeout = TimeSpan.FromSeconds(1);
options.Interop.ArrayConversion = ArrayConversionMode.Copy;
options.Parsing.MaxSourceLength = 100_000;
options.Parsing.MaxNodeCount = 25_000;
options.ResultLimits = ResultLimits.Conservative;

var report = options.ValidateSecurityConfiguration(SecurityConfigurationPolicy.UntrustedScripts);
foreach (var diagnostic in report.Diagnostics)
{
    logger.LogWarning(
        "{Code} {Severity}: {Message} {Guidance}",
        diagnostic.Code,
        diagnostic.Severity,
        diagnostic.Message,
        diagnostic.Guidance);
}

// Throws SecurityConfigurationException if the report contains an error.
var engine = new Engine(options.EnsureSecurityConfiguration(SecurityConfigurationPolicy.UntrustedScripts));
var effectiveReport = engine.Advanced.ValidateSecurityConfiguration(SecurityConfigurationPolicy.UntrustedScripts);
```

Validation is read-only: it does not apply a hardened profile or change normal engine defaults, so it also
works for `Options` assembled by direct property assignment or by application-specific helpers. Diagnostic
codes are stable and results are ordered by code for deterministic logging. Warnings identify settings that
need host-specific review (for example a custom module loader); they remain in the report but do not make
`EnsureSecurityConfiguration` throw. Treat a validated `Options` as immutable and pass it directly to
`Engine(Options)`. Public engine-construction callbacks registered by `Configure`, `SetTypeConverter`, or
`UseHostFactory` produce `JINTSEC031`; inspect `engine.Advanced.ValidateSecurityConfiguration()` after
construction to validate their final effects without replaying them. Custom constraint factories remain
supported under their existing contract and are invoked only by engine construction, never by diagnostics.

| Codes | Coverage |
| --- | --- |
| `JINTSEC001`-`009`, `036`-`037` | Statement, memory, per-entry timeout, cancellation, and cumulative operation deadline |
| `JINTSEC010`-`014` | Recursion, native stack guard, legacy stack lane, and `Atomics.wait` suspension |
| `JINTSEC015`-`020`, `030`, `048`-`055`, `057` | CLR access policy, `GetType`/unwrap, reflection, writes, array crossing, extension methods, error disclosure, callbacks, and compatibility mode |
| `JINTSEC021`-`022`, `041`-`046`, `050`, `056` | Module loader trust, count/bytes/depth/hops, destination policy, per-load reset semantics, and detailed errors |
| `JINTSEC023`-`029`, `032` | Debugger, arrays, regex, promise waits, and shared constraint instances |
| `JINTSEC031` | User-provided deferred engine configuration |
| `JINTSEC033`-`035`, `038`-`040` | Actual parsing/preparation regex settings, source and AST limits, and source retention |
| `JINTSEC047` | Result conversion and Jint JSON serialization limits; external serializers, responses, and logs remain host responsibilities |

A report with no findings is not proof of sandboxing. Jint remains in-process; worker isolation, least
privilege, request/response limits, callback authorization, and correct operation bracketing are host
architecture responsibilities.

Explicit `ScriptParsingOptions` / `ModuleParsingOptions` can override the engine regex timeout, and
prepared programs embed their preparation-time timeout instead of consulting the engine. Validate
the actual pair before direct parsing, and validate every preparation configuration before preparing:

```c#
options.EnsureSecurityConfiguration(scriptParsingOptions);
scriptPreparationOptions.EnsureSecurityConfiguration();
modulePreparationOptions.EnsureSecurityConfiguration();
```

The default preparation options use Jint's 10-second regex timeout and unbounded parser size limits.
Set `RegexTimeout`, `MaxSourceLength`, and `MaxNodeCount` on the actual nested parsing options used
for untrusted source.

You can configure them via the options:

```c#
var engine = new Engine(options => {

    // Limit memory allocations to 4 MB
    options.LimitMemory(4_000_000);

    // Set a timeout to 4 seconds.
    options.TimeoutInterval(TimeSpan.FromSeconds(4));

    // Set limit of 1000 executed statements.
    options.MaxStatements(1000);

    // Use a cancellation token.
    options.CancellationToken(cancellationToken);
}
```

`LimitMemory` measures managed bytes allocated while Jint actively executes one top-level operation. Promise
reactions, `EvaluateAsync` / `InvokeAsync`, and asynchronous module loading keep the originating budget across
thread hops. Finite web continuations such as fetch and stream completions, timers, idle callbacks and scheduler
tasks do the same. Persistent external event sources — message ports, broadcast channels, sockets, event streams,
host cancellation and finalization cleanup — start a fresh ordinary budget for each delivered task rather than
accumulating forever against the operation that created the object. Synchronous host callbacks are included;
allocations performed by background work before it hands a result back to Jint are not. The limit is not retained
heap, unmanaged memory, or a process-wide quota, and initial source parsing happens before the execution
constraint starts. Use an operating-system memory limit for a hard worker boundary.

Exceeding the limit ends that evaluation cycle's transient asynchronous work: queued jobs, pending loads,
timers and scheduler/idle tasks are discarded, host-stream bridges are abandoned, and completions arriving
later are rejected by the event-loop generation fence. Global bindings are not rolled back; use a snapshot or
a fresh engine when request isolation requires that.

The runtime capability is explicit through `MemoryLimitConstraint.Accuracy`. It is
`MemoryLimitAccuracy.ExecutionThread` when the per-thread allocation counter is available; otherwise execution
fails with `PlatformNotSupportedException` rather than silently skipping enforcement.

Like other execution constraints, the allocation budget normally resets for each top-level entry. To cover a
host operation made from several calls with one budget, retrieve the engine-owned constraint and bracket those
calls:

```c#
var engine = new Engine(options => options.LimitMemory(4_000_000));
var memory = engine.Constraints.Find<MemoryLimitConstraint>()!;

memory.Begin();
try
{
    engine.Execute(initialization);
    engine.Invoke("render", input);
}
finally
{
    memory.End();
}
```

The memory scope is part of the engine's single-operation ownership contract. Its mutable state and diagnostics
fail fast with `InvalidOperationException` while another thread or an outstanding async API owns the engine.
When the bracket contains `EvaluateAsync`, `InvokeAsync`, or `ImportAsync`, await that operation before calling
`End`; the async owner carries the same memory state through every continuation.

You can also write a custom constraint by deriving from the `Constraint` base class:

```c#
public abstract class Constraint
{
    /// Called before script is run and useful when you use an engine object for multiple executions.
    public abstract void Reset();

    // Called before each statement to check if your requirements are met; if not - throws an exception.
    public abstract void Check();
}
```

For example we can write a constraint that stops scripts when the CPU usage gets too high:

```c#
class MyCPUConstraint : Constraint
{
    public override void Reset()
    {
    }

    public override void Check()
    {
        var cpuUsage = GetCPUUsage();

        if (cpuUsage > 0.8) // 80%
        {
            throw new OperationCancelledException();
        }
    }
}

var engine = new Engine(options =>
{
    options.Constraint(new MyCPUConstraint());
});
```

### Bounding an operation that makes several calls into the engine

Every call into the engine — `Execute`, `Evaluate`, `Invoke`, `Call` — is a complete run, and the engine
resets the constraints around each one. `TimeoutInterval` therefore bounds *one* call, not a sequence of
them: a loop such as `foreach (var row in rows) engine.Invoke("render", row);` gives every row the full
interval to itself, so the loop as a whole is unbounded.

When the thing you need to bound is your own operation — a whole render, a whole request — rather than one
call, use `OperationDeadlineConstraint`. Its budget is started and stopped by you, and the engine's
per-call reset leaves it alone:

```c#
// one instance per engine, and you keep the reference
var deadline = new OperationDeadlineConstraint();
var engine = new Engine(options => options.Constraint(deadline));

deadline.Begin(TimeSpan.FromSeconds(2), cancellationToken);
try
{
    foreach (var row in rows)
    {
        engine.Invoke("render", row);
    }
}
finally
{
    deadline.End();
}
```

The whole loop shares the two seconds. When it runs out, the next call fails with a `TimeoutException` —
the same exception the built-in timeout throws. If the token is cancelled, the call fails with a real
`OperationCanceledException` carrying that token, so cancellation you asked for stays distinguishable from
a script failure. Outside a `Begin`/`End` pair the constraint is inert, which makes it safe to leave
registered on a pooled engine between operations. The two constraints are independent, so you can keep a
`TimeoutInterval` as a per-call ceiling alongside it.

When you reuse the engine and want to use cancellation tokens you have to reset the token before each call of `Execute`:

```c#
var engine = new Engine(options =>
{
    options.CancellationToken(new CancellationToken(true));
});

var constraint = engine.Constraints.Find<CancellationConstraint>();

for (var i = 0; i < 10; i++) 
{
    using (var tcs = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
    {
        constraint.Reset(tcs.Token);

        engine.SetValue("a", 1);
        engine.Execute("a++");
    }
}
```

### Surviving an unbounded recursion

A script that recurses without bound can exhaust the native stack of the thread it is running on, and that
ends the host process: no exception, nothing to `catch`, nothing in the log but an exit code. Every route
into a function body can do it — a call, `new`, a getter or setter, a `valueOf`/`toString` coercion, a Proxy
trap, a callback a built-in invokes, a host delegate that calls back into the engine.

```c#
var engine = new Engine(options =>
{
    // Only opt out when every script is trusted and independently bounded.
    options.Constraints.StackOverflowGuard = false;
});
```

The guard is on by default. Every entry into a script function first checks that the native stack has not
been used up, and an unbounded recursion raises `RangeError: Maximum call stack size exceeded` while there
is still room to unwind — an ordinary JavaScript error, catchable by the script itself and by your
`catch (JavaScriptException)`, with the engine still usable afterwards. It measures the remaining stack
rather than counting calls, so it covers all of those routes and adapts to the thread the engine runs on:
a host that provisions a larger stack gets proportionally more depth, rather than the frame count someone
had to guess in advance.

A proper tail call in a strict function is exempt, and does not need the guard: it replaces the caller's
frame instead of stacking one on top, so the recursion consumes no native stack however deep it runs. The
check sits on the entries that do add a frame, so a strict tail recursion neither pays for it nor is
stopped by it. Sloppy-mode recursion, a call out of tail position, and the non-call routes above are what
remain in scope.

The check runs at every entry into a script function and is not free. The benchmark gate measured
recursion-heavy workloads (`Fib`, `DeepSum`, `Tak`) roughly 1.5–3% slower with it on, while hot shallow
calls stayed within run-to-run noise. Jint accepts that default cost because terminating the host process
is worse. Set `StackOverflowGuard` to `false` only when every script is trusted and independently bounded.

`options.LimitRecursion(n)` answers a different question and composes with it. The recursion limit counts
frames and is checked before the callee is entered, so where it is configured it is what fires; the guard
answers only when no limit was set, or when the limit was set higher than the thread's stack can actually
hold — which is easy to do by accident, since how many frames a stack holds is not something a host can
see. A third and older setting, `options.Constraints.MaxExecutionStackCount`, continues the call chain on
a fresh thread instead of throwing; it is checked at call expressions only, so it does not cover the other
routes above, and it takes precedence over the guard when both are set.

## Using Modules

You can use modules to `import` and `export` variables from multiple script files:

```c#
var engine = new Engine(options =>
{
    options.EnableModules(@"C:\Scripts");
});

var ns = engine.Modules.Import("./my-module.js");

var value = ns.Get("value").AsString();
```

By default, the module resolution algorithm will be restricted to the base path specified in `EnableModules`, and there is no package support. However you can provide your own packages in two ways.

Defining modules using JavaScript source code:

```c#
engine.Modules.Add("user", "export const name = 'John';");

var ns = engine.Modules.Import("user");

var name = ns.Get("name").AsString();
```

Defining modules using the module builder, which allows you to export CLR classes and values from .NET:

```c#
// Create the module 'lib' with the class MyClass and the variable version
engine.Modules.Add("lib", builder => builder
    .ExportType<MyClass>()
    .ExportValue("version", 15)
);

// Create a user-defined module and do something with 'lib'
engine.Modules.Add("custom", @"
    import { MyClass, version } from 'lib';
    const x = new MyClass();
    export const result = x.doSomething();
");

// Import the user-defined module; this will execute the import chain
var ns = engine.Modules.Import("custom");

// The result contains "live" bindings to the module
var id = ns.Get("result").AsInteger();
```

Note that you don't need to `EnableModules` if you only use modules created using `Engine.Modules.Add`.

If you serve the same modules from a pool of engines, see **Sharing a module graph across pooled engines**
under [Embedding performance](#embedding-performance): a loader that caches prepared modules keeps every
engine but the first from parsing them again.

### How a module is named

Every module has a location — the string it knows itself by, `Module.Location` — and for a module a loader
produced, that string is `ModuleFactory.LocationOf(resolved)`: the `LocalPath` of an absolute `file:` uri, and
otherwise `ResolvedSpecifier.Key`, exactly as your `Resolve` wrote it. It is never null.

It is worth getting right because four things read it:

- your own `ModuleLoader.Resolve`, as `referencingModuleLocation`, whenever that module imports something
  relative;
- `error.stack`, and any `SyntaxError` a malformed module raises, through `SourceLocation.SourceFile`;
- the debugger, which keys breakpoints on it — `new BreakPoint(location, line, column)`;
- `import.meta.url`, if you report it. Jint leaves `import.meta` to the host, so it is your
  `Host.GetImportMetaProperties` override that puts the location there.

So a loader serving modules over a transport should key them by the whole url rather than by a path. A module
that knows itself as `/lib/entry.js` has no origin left for its own `./dep.js` to resolve against; one that
knows itself as `http://localhost/lib/entry.js` resolves it the way a browser would. The key is used verbatim
rather than `Uri.AbsoluteUri`, which canonicalizes — strips a default port, lowercases the host, collapses
dot segments, percent-encodes — and whose .NET Framework and .NET Core parsers disagree on parts of that, so
a canonicalized name would differ per target framework and could drift from the key the module map is cached
under. Loading from disk is unaffected: a `file:` uri keeps its path, which is what `DefaultModuleLoader`
resolves against a filesystem base path.

One consequence to weigh if the url carries anything sensitive. This string reaches script through
`new Error().stack`, so a loader keying modules by `https://svc:s3cr3t@internal.corp/lib/a.js?apikey=...`
hands untrusted script the credential and the api key. A browser's `import.meta.url` carries the query too,
so this follows from naming a module by its url at all; keep secrets out of the key, or out of the url.

If you name modules yourself — preparing each one once and sharing the `Prepared<AstModule>` across pooled
engines, per **Sharing a module graph across pooled engines** — call `ModuleFactory.LocationOf` for the name
rather than deriving it by hand. A name that differs from the engine's own answer breaks relative-import
resolution with nothing to point at.

### Loading modules asynchronously

`IModuleLoader.LoadModule` must return a parsed module immediately, so a loader that fetches module source over I/O — HTTP, a dev server, an asset pipeline — would have to block the calling thread. Implement `IAsyncModuleLoader` instead, or derive from the `AsyncModuleLoader` template, and no thread is held while the fetch is in flight:

```c#
internal sealed class HttpModuleLoader : AsyncModuleLoader
{
    private readonly HttpClient _client = new() { BaseAddress = new Uri("http://localhost:5173/") };

    // Resolution stays synchronous — only fetching an unseen module is allowed to take time
    public override ResolvedSpecifier Resolve(string? referencingModuleLocation, ModuleRequest moduleRequest)
    {
        var baseUri = new Uri(referencingModuleLocation ?? _client.BaseAddress!.ToString());
        var uri = new Uri(baseUri, moduleRequest.Specifier);
        return new ResolvedSpecifier(moduleRequest, uri.AbsoluteUri, uri, SpecifierType.RelativeOrAbsolute);
    }

    protected override Task<string> LoadModuleContentsAsync(Engine engine, ResolvedSpecifier resolved, CancellationToken cancellationToken)
        => _client.GetStringAsync(resolved.Uri);
}

var engine = new Engine(options => options.EnableModules(new HttpModuleLoader()));

var ns = await engine.Modules.ImportAsync("./main.js");
```

Static imports inside an asynchronously loaded module are fetched the same way, transitively, before anything is linked; a module wanted by several importers is fetched once. A loader failure — a faulted task, or `ModuleLoadCompletion.SetError` — becomes a rejected promise rather than an exception on the calling thread, and a dynamic `import()` in script keeps working with nothing blocked:

```c#
// Returns immediately; the promise settles on a later turn of the event loop
engine.Execute("import('./late.js').then(ns => log(ns.value));");
```

For full control over when and where the load finishes, implement `IAsyncModuleLoader` directly and settle the `ModuleLoadCompletion` whenever the content arrives, from any thread:

```c#
public void LoadModuleAsync(Engine engine, ResolvedSpecifier resolved, ModuleLoadCompletion completion)
{
    // e.g. a Unity coroutine, a callback-based transport, a worker queue
    StartCoroutine(Fetch(resolved.Uri, onSuccess: completion.SetSource, onError: completion.SetError));
}
```

The engine is single-threaded, and settling the completion does not change that: the outcome is queued and applied when the engine is next given a turn. On a thread the engine must not block — a game loop, a UI thread — drive the import from your own loop instead of awaiting it:

```c#
// once
var import = engine.Modules.StartImport("./main.js");

// every frame
engine.Advanced.ProcessTasks();
if (import.IsCompleted)
{
    var ns = import.GetResult();   // throws PromiseRejectedException if the load or evaluation failed
}
```

The synchronous `Engine.Modules.Import` still works with an async loader, but the calling thread is what drives the event loop while the loads are in flight — so it deadlocks if the loader's own completions need that same thread. Prefer `ImportAsync` or `StartImport` there. Existing synchronous `IModuleLoader` implementations are unaffected; async loading is opt-in.

A completion settled *before* `LoadModuleAsync` returns — a cache hit, source already in hand — is the exception to all of the above: the load finishes on the calling stack, no event loop involved, so a graph made entirely of such answers keeps the blocking `Import` fully synchronous, exactly as if a synchronous `IModuleLoader` had served it. `AsyncModuleLoader` does this automatically whenever `LoadModuleContentsAsync` returns an already-completed task.

### Bounding and restricting module graphs

When running untrusted or semi-trusted scripts, you can limit the size and shape of the module graph and restrict which modules may be loaded:

```csharp
var engine = new Engine(options =>
{
    options.EnableModules("/scripts");

    // Numeric limits — all default to unlimited (int.MaxValue / long.MaxValue).
    options.Modules.MaxModuleCount = 50;                   // distinct modules per engine lifetime
    options.Modules.MaxTotalModuleSourceBytes = 1_000_000; // cumulative UTF-8 source bytes
    options.Modules.MaxModuleGraphDepth = 10;              // import-chain depth per graph load
    options.Modules.MaxModuleResolutionHops = 200;         // Resolve calls per import operation

    // URI/path allowlist policy
    options.Modules.LoadPolicy = new ModuleAllowlistPolicy
    {
        AllowedSchemes = { "file" },                      // only file:// URIs
        AllowedFileRoots = { "/scripts" },                // must be under this directory
        AllowBareSpecifiers = true,                       // allow programmatic modules
    };
});
```

**Counting semantics:**

| Limit | Scope | What counts |
|---|---|---|
| `MaxModuleCount` | Engine lifetime | Each distinct successfully registered module record. Duplicates, cached lookups, and coalesced async fetches do not recount. Programmatic modules (`Engine.Modules.Add`) participate. |
| `MaxTotalModuleSourceBytes` | Engine lifetime | UTF-8 byte count of source text for string-based modules; exact byte length for `byte[]` modules. Prepared modules, exports-only modules, and custom `Module` records whose original encoded size is unknowable charge **0 bytes** — this is a known limitation. |
| `MaxModuleGraphDepth` | Per graph load | Conservative import-chain depth (root = 1). Cycles are collapsed into strongly connected components but every module in a cycle contributes to its component's weight; Jint then enforces the longest weighted path through the resulting acyclic graph. The answer is independent of source traversal and async completion order. |
| `MaxModuleResolutionHops` | Per import operation | Each actual `Resolve` call caused by an import. Cached `[[LoadedModules]]` hits do not resolve and do not count. Registration indexing does not consume hops. Budget resets per `Import`/`StartImport` call, so pooled engines never fail from accumulated operations. |

**Failure behavior:** Exceeding a numeric limit throws `ModuleGraphLimitException`, a `JintException` subclass that is **not** `JavaScriptException` and propagates like a constraint violation — it cannot be caught by script and is not turned into a promise rejection. Invalid finite limits (≤ 0) throw `ArgumentException` at engine construction. Policy denials throw `ModuleResolutionException` and follow existing sync/rejection behavior.

**Policy composition:** `ModuleAllowlistPolicy` applies AND across configured dimensions (schemes, hosts, origins, file roots) and OR within each list. An unconfigured dimension imposes no restriction. A file target cannot satisfy a configured host/origin dimension, and a non-file target cannot satisfy a configured file-root dimension, so cross-kind mismatches are denied rather than ignored. Bare/no-URI specifiers are denied by default when any dimension is configured; set `AllowBareSpecifiers = true` to permit them. File roots and resolved files are canonicalized before a separator-aware lexical containment check; symbolic links and Windows reparse points are not resolved, so allowed roots must not contain attacker-controlled links. The policy applies to the final `ResolvedSpecifier`; `DefaultModuleLoader`'s base-path restriction runs independently as an earlier check. Transport redirects occur inside a custom loader and are not visible to Jint's resolver, so that loader must apply the same policy and its own redirect limit to every redirect target.

## Asynchronous Execution

Jint supports non-blocking execution of JavaScript that involves `async`/`await` and Promises. This is important in ASP.NET Core and other environments where blocking a thread while waiting for I/O can cause thread-pool exhaustion.

### EvaluateAsync / ExecuteAsync / InvokeAsync

Use `EvaluateAsync` when you want to evaluate JavaScript code that may return a Promise (e.g., an `async` function call). The method awaits Promise settlement without blocking any thread — the calling thread is released back to the pool while I/O is in flight:

```c#
var engine = new Engine();

// Expose an async .NET method to JavaScript
engine.SetValue("fetchData", new Func<string, Task<string>>(async url =>
{
    using var client = new HttpClient();
    return await client.GetStringAsync(url);
}));

// EvaluateAsync properly awaits the Promise returned by the async IIFE
var result = await engine.EvaluateAsync("""
    (async () => {
        const data = await fetchData('https://example.com/api');
        return data;
    })()
    """);

Console.WriteLine(result.AsString());
```

`ExecuteAsync` and `InvokeAsync` follow the same pattern:

```c#
// Execute a script that may produce a Promise and await its completion
await engine.ExecuteAsync("async function init() { ... } init()");

// Invoke a named async function and await its result
var value = await engine.InvokeAsync("myAsyncFunction", arg1, arg2);
```

By default, async execution respects `Options.Constraints.PromiseTimeout`. You can also pass a `CancellationToken`:

```c#
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
var result = await engine.EvaluateAsync("(async () => await fetchData(url))()", cancellationToken: cts.Token);
```

### UnwrapIfPromiseAsync

When you already hold a `JsValue` that may be a Promise — for example returned from `engine.Invoke(...)`, `value.Call(...)`, or a property access — use `UnwrapIfPromiseAsync` to await it without blocking:

```c#
// Obtain a JsValue that may be a Promise
var jsValue = engine.Invoke("computeAsync", someArg);

// Await it asynchronously (non-blocking)
var result = await jsValue.UnwrapIfPromiseAsync();

// With cancellation support
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
var result = await jsValue.UnwrapIfPromiseAsync(cts.Token);
```

If `jsValue` is not a Promise it is returned immediately. If it is a rejected Promise a `PromiseRejectedException` is thrown.

The synchronous `UnwrapIfPromise` is still available for scenarios where blocking is acceptable (e.g., CPU-bound scripts with no I/O), but `UnwrapIfPromiseAsync` should be preferred in any `async` call chain.

### Task/ValueTask to Promise Interop (Experimental)

When the `TaskInterop` experimental feature is enabled, .NET `Task` and `ValueTask` return values are automatically converted to JavaScript Promises. This allows JavaScript code to `await` or `.then()` the results of .NET async methods without any manual wrapping:

```c#
var engine = new Engine(options =>
{
    options.ExperimentalFeatures = ExperimentalFeature.TaskInterop;
});

engine.SetValue("fetchData", new Func<string, Task<string>>(async url =>
{
    using var client = new HttpClient();
    return await client.GetStringAsync(url);
}));

// .NET Task is automatically converted to a JavaScript Promise
var result = engine.Evaluate("fetchData('https://example.com/api').then(data => data)");
result = result.UnwrapIfPromise();
```

Without `TaskInterop`, .NET Tasks passed to JavaScript are exposed as opaque CLR objects. With it enabled, they become native Promises that support `await`, `.then()`, and `.catch()`.

You can configure the timeout for promise resolution via `Options.Constraints.PromiseTimeout`:

```c#
var engine = new Engine(options =>
{
    options.ExperimentalFeatures = ExperimentalFeature.TaskInterop;
    options.Constraints.PromiseTimeout = TimeSpan.FromSeconds(10);
});
```

## .NET Interoperability

- Manipulate CLR objects from JavaScript, including:
  - Single values
  - Objects
    - Properties
    - Methods
  - Delegates
  - Anonymous objects
- Convert JavaScript values to CLR objects
  - Primitive values
  - Object -> expando objects (`IDictionary<string, object>` and dynamic)
  - Array -> object[]
  - Date -> DateTime
  - number -> double
  - string -> string
  - boolean -> bool
  - RegExp -> Regex
  - Function -> Delegate
- Extensions methods

Direct writes through projected CLR objects are disabled by default. Assignments to CLR fields, properties,
indexers, dictionary/list entries, and live array elements require `AllowClrWrite()`. In sloppy JavaScript a
blocked assignment is ignored; in strict JavaScript it throws a `TypeError`. Calls are outside this option's
scope: instance, static, and extension methods can still mutate CLR state.

## Error handling

### A CLR exception thrown by host code

By default an exception thrown by host code — a delegate you registered, a reflected member, a proxy trap —
is **not** converted into anything. It bubbles straight out of `Execute`/`Evaluate`/`Invoke` to you, with its
message, .NET stack trace and inner exceptions untouched, and the script gets no chance to catch it:

```c#
var engine = new Engine();
engine.SetValue("parse", new Action<string>(s => new XmlDocument().LoadXml(s)));

// throws System.Xml.XmlException, with its own stack trace
engine.Evaluate("parse('<not xml')");
```

`CatchClrExceptions` changes that: the exception becomes a JavaScript `Error` which the script can
`try`/`catch` like any other. Its message is `A host operation failed.` by default; the original message,
stack trace and inner exceptions remain host-only. The predicate overload decides per exception, so you can
let the script handle what it should handle and keep the rest fatal. Cancellation, timeout, memory, statement
and recursion-limit exceptions always remain control flow and are never converted into script errors.

```c#
var engine = new Engine(options => options.CatchClrExceptions(e => e is not OperationCanceledException));
```

### Getting the original exception back

Once an exception has been turned into a JavaScript error, the error is what travels — so when the script does
not catch it and it reaches you as a `JavaScriptException`, the message alone is rarely enough to diagnose
anything. `JintException.TryGetClrException` gives you the exception itself:

```c#
try
{
    engine.Evaluate(script);
}
catch (JavaScriptException e) when (JintException.TryGetClrException(e, out var clrException))
{
    logger.LogError(clrException, "host call failed while running {Script}", name);
}
```

It survives nested frames, a script catching and rethrowing the same error, module evaluation and a rejected
promise (`PromiseRejectedException`), and it follows the standard `cause` chain, so a script that rewraps with
`throw new Error(msg, { cause: err })` keeps it too. A script that throws something unrelated instead keeps
nothing, which is correct — it discarded the original.

The exception is host-only. It is CLR state on the error object rather than a JavaScript property, so a script
can neither read it nor strip it, and it does not appear in `Object.getOwnPropertyNames`, `Reflect.ownKeys` or
`JSON.stringify`. Note that the error object holds the exception, and everything the exception's object graph
reaches, for as long as the script keeps the error reachable.

If you would rather your logging pipeline find it without asking, `ChainClrExceptions` also hangs it in the
`InnerException` chain, so `ToString()` and any chain-walking logger render your own frames alongside the
JavaScript ones. It is off by default because it puts host stack traces into whatever consumes that string —
treat it the way an ASP.NET application treats developer exception details.

```c#
var engine = new Engine(options => options
    .CatchClrExceptions()
    .ChainClrExceptions());
```

Two related accessors work the same way: `JintException.TryGetJavaScriptLocation` and
`TryGetJavaScriptCallStack` give the JavaScript position for both `JavaScriptException` and a bubbled CLR
exception, and `TryGetClrType`/`TryGetClrMemberName` report the type and member behind a failed interop
resolution. To shape what the *script* sees — rewrite the message, attach an error code —
use `DecorateClrExceptionErrors`.

### Detailed development errors

Host exception messages, module loader failures, and CLR method/constructor resolution details are redacted by
default because they commonly contain secrets, filesystem paths, URLs, CLR type names and candidate signatures.
`ExposeDetailedErrors` restores the previous development-friendly messages across all three surfaces:

```c#
var engine = new Engine(options => options
    .CatchClrExceptions()
    .EnableModules(loader)
    .ExposeDetailedErrors());
```

Use it only when scripts and their error output are trusted. For narrower opt-ins, set
`options.Interop.ExposeDetailedExceptionMessages`, `options.Interop.ExposeDetailedResolutionErrors`, or
`options.Modules.ExposeDetailedLoadErrors` individually. These settings affect only script-visible messages;
`TryGetClrException`, `TryGetClrType`, `TryGetClrMemberName`, and the CLR error decorators retain full host-side
diagnostics under the safe defaults. `ModuleLoadCompletion.SetError(string)` remains an explicit script-facing
message; use `SetError(Exception)` when the detail must be retained for the host and redacted from script.
`DecorateClrExceptionErrors` also runs once for a module loader exception's final script-visible error, so the
same host-side decorator can attach a safe error code to interop and module failures.

### Raising an error from host code

To fail a host function in a way the script can catch, throw a `JavaScriptException`. The overload taking a
CLR exception records it for `TryGetClrException`, so the script sees an ordinary `Error` while you keep the
original:

```c#
engine.SetValue("parse", new Action<string>(s =>
{
    try
    {
        new XmlDocument().LoadXml(s);
    }
    catch (XmlException e)
    {
        throw new JavaScriptException(engine.Intrinsics.Error, "XML parsing failed", e);
    }
}));
```

Do not throw the exception projected into the script instead
(`new JavaScriptException(JsValue.FromObject(engine, e))`). That value is not an `Error` — it has no `stack`
and fails `instanceof Error` — and it hands the running script the exception's members, including its .NET
stack trace and inner exceptions.

## Code coverage (opt-in)

An engine can count what it executes, so a host can tell which parts of a script actually ran — a test
harness reporting coverage of embedded rules, a CI gate over business scripts, a dead-code audit.

```c#
var engine = new Engine(options => options.Coverage.Enabled = true);

engine.Execute("function f(n) { if (n > 0) { return 'positive'; } return 'other'; } f(1); f(2);", "rules.js");

foreach (var source in engine.Advanced.GetCoverage().Sources)
{
    foreach (var entry in source.Entries)
    {
        // rules.js  line 1  Statement  x2   ...
        Console.WriteLine($"{source.Name} line {entry.Start.Line} {entry.Kind} x{entry.HitCount}");
    }
}

engine.Advanced.ResetCoverage(); // start a fresh measurement
```

- `Options.Coverage.Granularity` is `Statements` by default, or `Functions` to report function-body entries
  only.
- A hit count is how many times the construct was *entered*: a statement in a loop counts once per iteration,
  a function body once per call (and once per resumption for a generator or an `await`).
- Only constructs that ran appear. The report is the covered set, not a ratio — derive the denominator by
  walking the AST you prepared if you need one. Block statements are never reported; the statements inside
  them are.
- Counters are per engine, so a `Prepared<Script>` shared across a pool of engines keeps a separate count per
  engine and the shared AST is untouched. `Options` stays shareable.
- `GetCoverage()` and `ResetCoverage()` throw `InvalidOperationException` on an engine that did not enable
  collection, rather than reporting an empty result that looks like a script which never ran.

Coverage is collected through the same per-statement path the debugger and the exact execution constraints
use, so an engine collecting it runs the instrumented interpreter path and its tight-loop optimizations are
disarmed — measured code is not byte-for-byte the code an uninstrumented engine runs. That is the normal
bargain for statement-level coverage, and the reason the option is off by default. An engine that leaves it
off pays nothing: the per-statement path the counting rides on is not armed at all.

## Security

Jint is an in-process interpreter, not an operating-system security boundary. Running
untrusted scripts safely requires explicit limits, a minimal host capability surface, fresh
engines across trust domains, and process-level isolation. See the
[threat model](.github/THREAT_MODEL.md) for the supported boundaries, known limitations, and
a hardened deployment baseline.

- Define memory limits, to prevent allocations from depleting the memory.
- Enable/disable usage of BCL to prevent scripts from invoking .NET code.
- Direct writes through projected CLR objects are disabled by default; enable them only with
  `AllowClrWrite()`. This does not block side effects from callable CLR methods.
- Limit number of statements to prevent infinite loops.
- Limit depth of calls to prevent deep recursion calls.
- Define a timeout, to prevent scripts from taking too long to finish.
- Turn an unbounded recursion into a catchable error rather than a stack overflow that ends the process
  (see [Surviving an unbounded recursion](#surviving-an-unbounded-recursion); enabled by default).

Parsing happens before execution constraints start, so hosts accepting untrusted source should bound it
separately:

```csharp
var engine = new Engine(options =>
{
    options.Parsing.MaxSourceLength = 100_000;
    options.Parsing.MaxNodeCount = 25_000;
});
```

These limits apply to `Execute`/`Evaluate`, `eval`, function constructors, ShadowRealm evaluation,
debugger evaluation, module builders, and JavaScript or JSON source returned by module loaders.
`MaxSourceLength` counts UTF-16 code units (`string.Length`), not encoded bytes. It includes generated
source-offset padding and the wrapper generated by a function constructor; byte-backed module transports
are checked after decoding. `MaxNodeCount` counts the AST nodes Acornima completes. Crossing either limit
throws `ParsingLimitException`, which is a host resource-limit exception and is not converted to a
catchable JavaScript error.

Both limits default to `null` for compatibility, meaning unlimited. Per-call `ScriptParsingOptions` and
`ModuleParsingOptions` can set tighter limits, and static preparation accepts them through
`ScriptPreparationOptions.ParsingOptions` and `ModulePreparationOptions.ParsingOptions`. Executing an
already prepared script or module does not parse or recheck its AST; dynamic source compiled by that code
uses the tighter of its preparation limits and the engine limits.
When asynchronous module source arrives on another thread, its parser limits and the originating
`LimitMemory` operation both remain attached to the engine turn that builds it; concurrent host access
continues to fail fast until an owning async import completes.

These controls bound source representation and final AST size, not parser CPU for every adversarial
grammar shape, encoded response bytes, module count, graph depth, redirects, or network access. Enforce
transport and module-graph policy in the host and keep hostile parsing inside a disposable worker with
OS-level memory and CPU limits.

**Migration note — `Atomics.wait`:** synchronous suspension is disabled by default. Calling
`Atomics.wait` on a default engine throws a JavaScript `TypeError` before registering a waiter, while
`Atomics.waitAsync` remains available. A worker-like host that deliberately runs Jint where blocking is
acceptable can restore the previous behavior explicitly:

```c#
var engine = new Engine(options => options.AgentCanSuspend = true);
```

Do not enable this on a request, UI, or event-loop thread: a script can call `Atomics.wait` without a
timeout and block that thread indefinitely.

## Branches and releases

- The recommended branch is __main__, any PR should target this branch
- The __main__ branch is automatically built and published on [MyGet](https://www.myget.org/feed/Packages/jint). Add this feed to your NuGet sources to use it: https://www.myget.org/F/jint/api/v3/index.json
- The __main__ branch is occasionally published on [NuGet](https://www.nuget.org/packages/jint)
