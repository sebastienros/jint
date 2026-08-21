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
`UseWebApis(WebApiFeatures.Default)`. `Jint.Tests/Wpt/Prelude/testharness-shim.js` — *not* vendored — stands
in for upstream's `testharness.js`; its header says what it implements and where it deliberately differs.
`WptHarness.cs` documents the two decisions a reader is most likely to want: the engine supplies its own
`setTimeout`, and `// META: variant=` sharding is ignored because one unsharded run is the union of every
variant.

Three standards are vendored: `url/` and `encoding/` as one suite each, and `WebCryptoAPI/` as **eight** —
its root files plus one per operation directory, because `WptCorpus.TestFiles` lists a directory's own files
and never descends. That is 87 theory cases, 48 of them the WebCryptoAPI corpus's 24,136 assertions, and the
whole driver runs in about 40 seconds.

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

Nothing was left out for being slow. Every vendored file was timed at the pin; the slowest is
`derive_bits_keys/pbkdf2.https.any.js` at ~20 s for 8,632 cases (it is `// META: timeout=long` and sharded
nine ways upstream), then `generateKey/successes_RSA-OAEP.https.any.js` at ~7 s, which really does generate
156 RSA key pairs. Everything else is under 2 s.

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

2,839 of its ~24,000 assertions do not pass, and every one is named in the driver's table under one of six
categories, whose own documentation in `WptExclusions.cs` carries the citation. Three are the platform:
`NeedsPlatformCryptoParameters` (AES-GCM's 96-bit-only iv and 96-to-128-bit tag, RSA-OAEP's empty-only label,
RSA-PSS's hash-length-only salt — all four are limits of the BCL primitives, documented on the classes that
hit them), `NeedsCompressedEcPointImport` and `NeedsCurve25519`. Two are the corpus running ahead of the
specification: `NeedsKeyEncapsulation` (ML-KEM's `encapsulateKey`/`decapsulateKey` `KeyUsage` values, which
the current `KeyUsage` enumeration does not declare) and `NeedsQuotaExceededErrorInterface`. One is the
corpus meeting an environment it was not written for: `NeedsSecureContextModel`.

The sixth is `NeedsTriage`, and it is **debt**: two genuine defects the corpus found, recorded rather than
fixed so that the change which first ran these suites was not also the change that moved the engine.
`SubtleCrypto` copies its caller's bytes before normalizing the algorithm where the specification copies them
after, which is every "… during call" row; and ECDH's mismatched-curve rows get the `OperationError` of the
prose where a browser answers `InvalidAccessError`.

## Updating the pin

Resolve `master` to a concrete commit, re-fetch every file in this directory at that commit, update the SHA
above, and run the suites. Expect the exclusion table to need work in the same change: the driver fails on an
entry that no longer applies, which is the point.
