# Exact prepared inputs for six Japanese XML corpus cases

Independently reviewed for implementation on 2026-09-25, following the 2026-09-23 source review. This is a
**test-input adaptation**, not a Japanese decoder in the production parser. The production
`MarkupParser.ParseXml(string)` contract and final XML conformance scope remain unchanged.
Six existing corpus cases receive independently decoded strings; none gains a parser expectation.

## 1. Why exact preparation

The conformance design explicitly tests already-decoded UTF-16 strings. Its current infrastructure
already verifies the full W3C archive and member hashes, stores restore products in ignored `Cache/`,
and performs restore through an explicit Python command. Tests never fetch resources or invoke Python.
Preparing these six inputs extends that established boundary without adding a runtime codec dependency.

Python strict `euc_jp`, `iso2022_jp` and `shift_jis` decoding of the pinned inputs gives the hashes
below. All raw/member/catalog pins were independently verified. Direct .NET provider probes also
matched all six strings on net8/net10, but negative probes found CP50220 accepts truncated escapes
and mis-maps JIS Roman; CP51932/932 differ from the named codecs for some characters. Therefore this
implementation adds no CodePages provider, mapping table, custom encoding state machine or general
legacy-encoding support claim. The six fixed decoded digests are the compatibility contract.

[XML §4.3.3 and Appendix F](https://www.w3.org/TR/xml/#charencoding) describe encoding detection;
[the corpus design](html-parser-xml-conformance.md) defines this string-input profile. Original
encoding declarations remain in the strings supplied to Jint. Preparation must not rewrite them to
UTF-8: UTF-8 here is only storage for a prepared string, not a new XML document byte interpretation.

## 2. Checked metadata and ignored artifacts

All paths below are relative to `Jint.Tests.HtmlParser/Xml/Conformance/`.

- Add checked `prepared-inputs.json`, exactly six rows, with `key`, `inputPath`, `rawSha256`,
  `codec`, `decodedUtf8Sha256`, `utf16Length`, `declared` and `decision`. The fixed values come
  from the source review below. No expected parse outcome, omissions, projection or OUTPUT is stored.
- Add `preparedInputsSha256` to `corpus.lock.json` and its test-only model. Verify the metadata
  file before using it. Cross-check each input path/raw hash against the existing member lock and
  case identity. Guardian tests assert the exact six keys, uniqueness and allowed codec mapping.
- Derived artifacts live at `Cache/DecodedJapanese/<rawSha256>.utf8`. Construct filenames from
  validated 64-character lowercase hex, not from arbitrary manifest paths. The existing `/Cache/`
  ignore rule covers them. Do not vendor, publish or copy these products into package/build artifacts.
- Original archives, original member pins, upstream categories and fixture count stay unchanged.
  The artifact is supplemental: the original raw member remains mandatory even when its UTF-8
  prepared counterpart exists. A cache from another raw revision cannot replace the source evidence.

The supported codec map is exact: euc-jp files → Python `euc_jp`, iso-2022-jp files → `iso2022_jp`,
shift_jis files → `shift_jis`. Case keys, not declaration guesses or filename suffixes, select this
route. The `declared` field preserves source spelling (`Shift_JIS` only in weekly-shift_jis).
Use decisions `prepared-euc-jp`, `prepared-iso-2022-jp`, `prepared-shift-jis` to distinguish exact
corpus preparation from a general declaration-based decoder.

## 3. Explicit preparation and offline reproducibility

Extend the existing tool with this explicit offline command, run from the repository root:

```sh
python3 Jint.Tests.HtmlParser/Xml/Conformance/Tools/import_corpus.py prepare-decoded
```

It requires the already-restored pinned W3C archive. It never downloads anything. The existing
`restore` action should finish by invoking the same preparation routine after verifying its restored
archives, so a fresh documented restore produces a complete cache. Do not run preparation from
MSBuild, a test initializer, or the test runner. No Python installation is needed when running tests
against a complete verified cache.

For each checked row, preparation must:

1. Verify the checked preparation metadata, archive SHA and original member SHA against the locks;
   use the existing safe tar-member reader. Reject duplicate/unexpected preparation keys.
2. Decode the member bytes with the table's codec and `errors="strict"`. Verify the unchanged XML
   declaration's label and absence of an unexpected BOM/signature for these exact inputs.
3. Encode the resulting string with strict UTF-8, with no BOM. Verify its **already reviewed**
   decoded digest and UTF-16-unit count (`len(text.encode("utf-16-le")) // 2`). Never regenerate
   expected hashes from whatever a codec currently returns. A codec/platform difference fails.
4. Write binary bytes to a temporary sibling, verify that file, then atomically replace the destination.
   Do not use text-mode file writes, universal-newline reads, normalization or added final newlines.

Running this twice produces identical bytes. It may repair a stale derived cache explicitly, but
must never repair raw bytes or change the reviewed metadata. Failure leaves no apparently valid
partial artifact. Record Python/version information in command diagnostics, not in expected outcomes;
the fixed hashes enforce reproducibility across permitted versions.

Refactor a small source-only preparation helper inside the importer so `import` uses this exact
strict decode/hash/length/declaration check when generating the six manifest rows. It may compute
that text in memory from verified archive members; it must not obtain decisions from Jint or trust
an unverified artifact. Other rows continue through the existing `decode_document` unchanged.

## 4. C# input loader and failures

Add test-only `XmlPreparedInputs.cs`. Give the conformance runner a single input-loading route:
for one of the exact six registered keys, load/verify the prepared input; otherwise call the existing
`XmlByteDecoder.Decode(bytes)`. Do not change the generic decoder's encoding repertoire or its
existing BOM/signature/declaration precedence.
Validate the exact six-key registry before selecting a route; a missing metadata entry must not
silently send a registered case through the generic decoder.

For a prepared case, verify row key/input path, original raw member hash, preparation metadata,
artifact hash, strict UTF-8 decoding and UTF-16 length. Confirm the decoded declaration's exact
recorded label and return `XmlDecodingDecision { Status="decoded", Decision=table.Decision,
Declared=table.Declared }`. The existing manifest-decision comparison remains in force. Do **not**
feed prepared UTF-8 bytes back through XmlByteDecoder's XML-declaration detection: that would
misinterpret the deliberately preserved Japanese encoding declaration as the cache's storage codec.

Missing/corrupt/stale artifact, invalid UTF-8, wrong raw member, duplicate/path mismatch, table drift,
length mismatch or declaration mismatch is **HarnessFailure**, with a specific integrity signature.
A table-level integrity exception may fail the corpus-integrity gate directly. Never convert these
failures to OptionalAdapterDebt, parser rejection, an ordinary skip or a fallback to the generic
codec. There is no network, Python execution or automatic cache rebuilding in this loader.
Use a narrow data-loading seam accepting supplied bytes for corruption tests; do not mutate global
caches or real fixture files. Avoid broad catches that hide cancellation or programming errors.

A synthetic Japanese byte string that is not one of the six exact inputs still uses the unchanged
generic decoder. A changed byte buffer presented as a registered input fails its raw hash before
preparation/loading. Thus there is no promise to classify every malformed Japanese sequence, or to
support arbitrary valid input in those encodings. The production string parser loses no coverage:
all six original source strings are added to execution and all other cases retain their prior route.

## 5. Fixed source evidence

All member paths have prefix `xmlconf/japanese/`; key is `xmlconf/japanese/japanese.xml#` plus
filename without `.xml`. These six sources contain no supplementary characters, but the length
field and implementation nevertheless count UTF-16 units explicitly. Decoded SHA is strict UTF-8
of the exact string, including original CRLF and declaration text; no normalization is permitted.

| File | Source SHA-256 | Decoded UTF-16 units | Unicode SHA-256 |
| --- | --- | ---: | --- |
| pr-xml-euc-jp.xml | 7b5b7cc9ce672e901c08daa9eadd5e4ff59191980c91f1db6acabab72b6dc655 | 156577 | 14c452dc9e91d1ba7ef9b55e76a71a8ce75fd725142b105a895267ee44979742 |
| pr-xml-iso-2022-jp.xml | 34b947550cf03967736493469e1c7a4ef9ae286fccbc73e1df564069198069ab | 156582 | 0a9030423eaca147b62b6776030d1720851650f28fb06220b9df9670976706c2 |
| pr-xml-shift_jis.xml | 96aa401656333ed6d7d6a3439b9e456ccc57c1f7722d53065eae5fe0fc6b7dee | 156580 | a71d13642192cafb8d2d23c1520b2716d7da27deaf7b1ff4465584c9195d9263 |
| weekly-euc-jp.xml | 44080d84744259ba1410b23b9cd70e83e02f6251a1ca37682e2a40c41d546537 | 1610 | 7a5daf882eafc098a90542f82e4508e52f23d954dde2d24bd97b68504daad0f7 |
| weekly-iso-2022-jp.xml | 834e76f4f57ff2d3c77ad69284091551e3fbf64f869e994652dfed7cebddac45 | 1620 | 91c5d67693e7ab7ad244d91236219552298cccaf176bf28456d3f15f89f09a9a |
| weekly-shift_jis.xml | f16cf8b16b8fe53705964a06bd82ca4cc8d8612890f0f3e6fd7040be8d3bbb19 | 1616 | 93b8781d0c9bc7624bec37f44c71ef791c641451afcff4569a51eaea8163ba86 |


Regenerate exactly these six `cases.json` entries: status becomes `decoded`, decision uses the
`prepared-*` label above, declared spelling is populated from the reviewed source table, and old
decoder error detail disappears. Four old strict-decode-error rows have no declared field to retain.
Add `external-identifier-lexical` after the existing `ENTITIES=parameter` resource signal. Preserve
category `error`, disposition `optional-error-review`, null OUTPUT fields, and resource profile
`unreviewed-external-indication`. Every other field/row stays unchanged. Refresh `casesSha256` and
the new preparation-metadata digest in the lock; do not change archive/member pins or census totals.
The declaration-prefix audit found exactly these six Japanese-encoding input rows.

## 6. Tests and acceptance

Add `XmlPreparedInputsTests.cs` and source-only Python preparation tests. Fresh Release net8/net10
runs must verify all six raw hashes, lengths and prepared Unicode hashes without invoking Jint as
an oracle. Exercise complete archive/table/raw/artifact binding, not merely reading arbitrary text.

Required negative tests: missing artifact; truncated artifact; invalid UTF-8; wrong/duplicate table
key; same-name stale raw member; swapped artifacts; wrong decoded digest/length/declaration;
BOM injected into raw or prepared bytes; and CRLF→LF conversion. Any failure is harness integrity,
not a parser outcome. Tests must show that no prepared-byte declaration is rewritten or routed
through the generic legacy decoder, and that unrelated UTF-8/UTF-16/Latin1 behavior is unchanged.
Check binary preparation twice for byte identity and exercise preparation failures without changing
repository metadata or accepting newly calculated expected hashes. Optional targeted strict-Python
negative byte probes are tooling evidence, not a new general byte-decoder conformance suite.

Update the guardian that currently uses pr-xml-euc-jp as an unavailable-adapter example. Verify all
six now reach OptionalObservedUnreviewed without policies; retain a synthetic unsupported-decoding
case to test OptionalAdapterDebt categorization. A missing prepared artifact must instead exercise
HarnessFailure. Keep the existing generic EUC-JP decoder-is-unsupported unit assertion: the generic
decoder intentionally remains unchanged, while these exact rows use the prepared-input loader.

The only semantic census movement is **six OptionalAdapterDebt → six OptionalObservedUnreviewed**.
Existing policies, required conformance passes and OUTPUT counters do not improve. No entries in
`expectations.json` or `optional-policies.json` are part of this implementation. Independent review
must subsequently establish each optional-error policy from source, including unread external
subset/PE effects, exact omissions, complete projection and notations. Keep every case in the final
inventory; neither arbitrary parser acceptance nor rejection becomes a conformance pass.
