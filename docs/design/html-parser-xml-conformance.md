# XML conformance acceptance

Design supplement, 2026-09-23. This adds independent evidence for
[the XML implementation contract](html-parser-xml.md); it does not introduce a resolver,
validation mode, byte-input API, or another XML implementation.

## Corpus, pin and redistribution

Use the official [W3C XML Test Suite 20130923 release](https://www.w3.org/XML/Test/),
including its XML 1.0 fifth-edition and Namespaces collections. The selected archive is
`https://www.w3.org/XML/Test/xmlts20130923.tar.gz`; its SHA-256, verified from the downloaded
archive during this design, is:

```text
9b61db9f5dbffa545f4b8d78422167083a8568c59bd1129f94138f936cf6fc1f
```

Archive catalogs enumerate **2585 rows**: 812 valid, 242 invalid, 1498 not-wf, 33 error,
before version, namespace, input-boundary or resource-policy classification. These are inventory
counts, not executed or passing counts. The archived catalog is authoritative for the pin:
the online 20130923 report still carries an older heading and older download links.

Preserve contributor notices. The [W3C FAQ](https://www.w3.org/XML/Test/faq.html) describes
distribution under the [W3C Software Notice and License](https://www.w3.org/Consortium/Legal/copyright-software-19980720),
which requires retaining existing notices. In particular, `xmlconf/xmltest/readme.html`
restricts redistribution of James Clark's collection to the unmodified `xmltest.zip`.
Do not silently replace that notice or assume a general license overrides it.

There is a concrete permitted archive route. [The author's page](http://www.jclark.com/xml/)
links `ftp://ftp.jclark.com/pub/xml/xmltest.zip`. The original 1998-11-18 archive was retrieved
and inspected: 107060 bytes, SHA-256:

```text
a919d7142fe6f72af51fc796b4df40732f385c9eb313b8993c6d39cc92acc410
```

Nothing from either archive is committed. The W3C `xmltest` tree has nine changed files and
thirteen additional files, including two catalogs, compared with the original ZIP; the import tool
reads the ZIP from the ignored cache only to record that mapping in `corpus.lock.json`
(`unchanged-clark-zip` vs `verified-cache` per member, plus both hashes for changed paths). Tests
never read the ZIP: every member is served from, and pinned against, the W3C archive. Never silently
substitute the original bytes under a W3C expectation. Separate original-only tests are possible but
are not W3C passes.

The tests download the pinned W3C archive on first use into the ignored
`Jint.Tests.HtmlParser/Xml/Conformance/Cache/` folder, verify its SHA-256 before reading it, and reuse
it while it still matches the pin (a stale or torn file is replaced). Writes go through a unique sibling
and an atomic rename, so the net8.0 and net10.0 test processes can share the folder. Offline, place the
archive at that path; a missing or altered archive fails the corpus-integrity gate rather than producing
zero tests or a green skip. Do not publish that cache, extracted fixtures, or a repackaged archive as
build artifacts. The Edinburgh `errata-2e`, `errata-3e`, `errata-4e`, `namespaces/1.0` and
`namespaces/errata-1e` catalogs (34 + 13 + 393 + 48 + 3 = **491 rows**) are an initial
implementation slice, not the final acceptance denominator.

## One explicit parser profile

The tested profile is XML 1.0 fifth edition, Namespaces in XML 1.0 third edition, nonvalidating,
already-decoded UTF-16 strings, internal declarations processed, and external resources unread
except the eleven recognized local PUBLIC catalogs. The production parser receives only the
input string and its ordinary options. No fixture resolver is passed to it.

Read the pinned `testcases.dtd` metadata contract independently of the parser under test.
Its actual attribute is **`NAMESPACE`**, singular, despite plural wording in a comment.
Defaults are `RECOMMENDATION=XML1.0`, `NAMESPACE=yes`, and `ENTITIES=none`; absent VERSION or
EDITION means all versions or editions. Preserve IDs, catalog path, collection, URI, descriptions,
SECTIONS, RECOMMENDATION, VERSION, EDITION, ENTITIES, OUTPUT and OUTPUT3 in the generated manifest.
Use `(catalog path, ID)` as the stable key, not a filename alone.

Select VERSION containing `1.0` and EDITION containing `5`, or their absent defaults.
Include the XML1.0 errata collections despite their historical directory names. Include NS1.0
and NS1.0-errata1e under the current third-edition rules. XML1.1, NS1.1, and earlier-edition-only
rows are explicitly outside the profile; do not reinterpret them by editing declarations.
`NAMESPACE=no` rows require a non-namespace parser and are outside this API's profile; retain
their exact IDs in the inventory. Any retained assertion against their bytes is a separately
named namespace-profile test, not a pass of their original expectation.

Namespace constraints are required even without DTD validation. Review older namespace cases
against [the third edition](https://www.w3.org/TR/xml-names/): default namespace reset is legal,
prefixed undeclaration is not; namespace names compare as strings; a reserved `xml...` prefix
other than `xml`/`xmlns` is not itself a fatal error. Do not apply Namespaces 1.1 rules or turn
optional URI-name diagnostics into blanket rejection. A changed expectation requires an exact
case entry with its current normative citation; retain the upstream classification beside it.

## Expected outcomes and unread resources

The [W3C test matrix](https://www.w3.org/XML/Test/xmlconf-20130923.html) distinguishes
validation from well-formedness and warns that unread external entities change some outcomes.
Use this classification, then review the external-dependent cases individually:

| Upstream category | Required result in the selected profile |
| --- | --- |
| `valid` | Accept, and verify available output/DOM expectations. |
| `invalid` | Accept when the defect is only a validity constraint; still enforce namespace and well-formedness requirements in material actually read. |
| `not-wf` | Reject with `MarkupParseException` when the violation is in the document or included internal/local-catalog material. |
| `not-wf` whose only violation is inside an unread external entity | Reviewed no-fetch acceptance, with exact omission records and retained-content assertions. |
| `error` | Per-case documented optional-error policy; run and report separately, never count either arbitrary outcome as a conformance pass. |

`ENTITIES=parameter/general/both` is a review hint, not an automatic exemption or acceptance rule.
Inspect the document and referenced files to locate the asserted violation. A malformed external
identifier declaration in the main document remains a failure even when the external file is
unread. A declared external general reference in an attribute remains fatal. Internal replacement
markup must balance within its own entity, and included internal parameter entities remain checked.
An unread external subset cannot excuse unrelated trailing junk or namespace errors.

Apply [XML sections 4.1, 4.4 and 5.1](https://www.w3.org/TR/xml/#sec-conformance) and the reviewed
XML design for Entity Declared, standalone declarations, and declaration processing after an unread
parameter entity. Preserve the distinction between missing declarations and known external entities.
For each successful resource-dependent row, store independently reviewed expected
`SkippedXmlEntities` records: ordered kind/name/public ID/system ID/UTF-16 offset, including repeated
occurrences and null versus empty identifiers. Store the expected surviving DOM projection as well.
No skip metadata is observable on a failed parse. The recognized local catalog is actually read and
must not be exempted as an external omission.

The harness may read pinned external fixture files to derive/review expectations, but never feeds
them to production parsing, concatenates them into input, or replaces references. Catalog metadata
loading is separate: allow only exact pinned catalog/DTD resources in a test-only loader; deny arbitrary
paths, URI schemes, symlinks and traversal. Resolving metadata must not resolve test documents.

## String-input boundary and output evidence

Keep source bytes unchanged. Decode document bytes in a test-only, strict, explicit encoding adapter
with BOM handling and no replacement fallback or newline conversion. Record the encoding decision
per case. UTF-8 and UTF-16 cases provide lexical coverage after decoding. Tests whose sole failure is
bad byte encoding, contradictory encoding declarations, or byte auto-detection are outside a string
API; a decoder failure is never a parser pass. List their IDs and reasons. Do not broadly exclude all
encoded files or remove an XML declaration to get a passing result. Unsupported legacy encodings
in the harness are coverage debt until an explicit adapter or a justified input-boundary classification
is reviewed. Offsets in parser assertions refer to the decoded string.

Start with binary outcomes plus reviewed DOM projections for entity/default/namespace cases.
Projection is an iterative walk recording node kinds, names, namespace/prefix, attributes and values,
text/CDATA/PI/comment content, doctype identifiers and child order. Adjacent text may be coalesced
only in comparisons whose upstream format coalesces it; separate native tests retain CDATA identity,
specified-before-default attribute order, document metadata and template ownership checks.

`OUTPUT` is the suite's Second Canonical Form, not arbitrary modern C14N or OuterHtml; `OUTPUT3`
targets validating behavior and is outside this profile. Do not compare a serializer with unrelated
escaping rules. A bounded test-only output adapter may implement the documented Second Canonical
Form, with independent positive/negative tests. Until it does, mark those output assertions as pending
while still running their binary cases. DTD notation output not represented by the public DOM is an
explicit observation-surface gap, not a reason to invent public parser APIs. External-inclusive output
cannot be reused for the no-fetch profile without a reviewed per-case alternative.

Never parse expected XML with the implementation under test to obtain the expected tree. BCL/browser
comparisons are supplemental diagnostics, not replacements for corpus expectations and spec review.

## Minimal files and gates

Keep this lane under `Jint.Tests.HtmlParser/Xml/Conformance/`; do not change WPT or Test262 data.

- `corpus.lock.json`: archive pins, catalog inventory, per-file hashes, recorded Clark-ZIP provenance,
  and full-suite census. The archives themselves are downloaded on demand into the ignored `Cache/`.
- `cases.json`: generated upstream metadata and explicit decoding/resource-profile classification.
  A small deterministic import tool (`Tools/import_corpus.py`) reproduces it from the pinned inputs; no outcome harvesting.
- `expectations.json` and `deviations.json`: reviewed no-fetch/output expectations and exact failing
  case IDs with issue, reason, expected failure signature and normative citation where applicable.
- `XmlConformanceTests.cs` plus small `XmlCorpus`/`XmlExpectations` helpers: one NUnit case per selected
  upstream row, immutable shared fixture data, fresh parser/document per invocation.
- `XmlCorpusTests.cs`: pin/license/manifest integrity, classification census, path/resource confinement,
  decoder tests, and negative probes proving wrong result/record/output cannot report a pass.

Borrow the repository's [WPT exclusion discipline](https://github.com/sebastienros/jint/blob/main/Jint.Tests/Wpt/AGENTS.md) and
[Test262 content-digest discipline](https://github.com/sebastienros/jint/blob/main/Jint.Tests.Test262/AGENTS.md), not their production dependencies.
Require every full-suite row to have exactly one disposition. Print counts by collection/category:
inventoried, outside profile, runnable, passing, known failing, unresolved classification, and harness
failures; separately count eligible/output-compared/pending-output rows and no-fetch adaptations.
Checksums include all referenced bytes, not just the catalogs. Unknown metadata, missing files,
duplicate keys, stale deviations, unexpected passes, and incorrect omission records fail the gate.

Known failures continue executing and must fail for the recorded reason; no catch-all exception
exclusions or globs. A cancellation, budget exception, crash, timeout or programming exception is not
the expected syntax rejection. No retries. Census updates may remove debt after fixes but cannot
silently increase failure allowances. During implementation, named parser defects may remain visible
debt; zero unresolved classifications, zero harness failures, and zero known required-profile parser defects
are necessary before the final XML acceptance claim. Publishing only the Edinburgh denominator does
not satisfy that gate. Optional errors, outside-profile rows and justified observation gaps stay visible.
The final gate also requires zero pending eligible output assertions: each must pass its comparison,
pass an independently reviewed no-fetch expected result, or have an individually approved observation
gap with its exact case ID and rationale. Unimplemented output adapters remain blocking harness debt.
A passing binary outcome never counts as a passing output assertion.

`expectations.json` and `optional-policies.json` keep one projection entry per line with null members
omitted. A projection of at least 100 entries that is identical to one stored earlier is replaced by
`projectionSameAs`, naming the row that stores it. Each Japanese `pr-xml` document is a 6,283-entry
projection repeated across encodings. The reference only saves storage: the loader holds the row to
exactly the same entries, and it rejects a reference that chains, sits next to its own projection, or
names a missing row. After editing either file, run `Tools/format_reviews.py`. It expands every
reference and shares again, so its output is canonical.

The completed review covers all 411 runnable rows with external-resource indications and the
additional `eduni/errata-3e/errata3e.xml#rmt-e3e-13` internal-parameter case. Its undeclared `ent2`
is a validity issue even without an external subset; a lexical resource scan alone cannot classify
that omission. The 101 formerly pending cases have exact surviving projections and ordered omission
records, with notation and DTD PI inventories where applicable. The expectations were derived from
the pinned sources and referenced resources independently of the native parser. Japanese UTF-16
fixtures retain their actual doubled newlines and text differences rather than borrowing the UTF-8
or previously reviewed legacy-encoding projection.

The pinned IBM P28/P29 OUTPUTs place DTD PI events before their reconstructed notation block.
The output adapter preserves that concrete corpus convention (which is more specific than the
draft `CanonXML2` grammar), reading only `Document.XmlDtdProcessingInstructions`; it never rescans
input to recover lost PIs. Negative probes reject missing, changed and reordered instruction records.
All 386 eligible outputs are now compared, including 66 independently reviewed no-fetch alternatives.
There are 1,947 passing runnable rows, 27 verified optional policies, 593 outside-profile rows and
18 reviewed byte-boundary exclusions; no pending classifications, harness failures or known required
profile defects remain. The zero-debt gate remains live rather than replacing failures with exclusions.

The first commit can run the internal `XmlTreeParser` milestone while the facade is pending. Switch
the main corpus lane to public `MarkupParser.ParseXml` once implemented, and add unsigned consumer
tests proving the real public entry points are accessible. Fragment/SVG/native-template behavior,
catalog rules, budgets and deterministic cancellation still need their focused tests; document corpus
counts cannot stand in for those contracts. Run Release on net8.0 and net10.0 without skipping builds.

The defensible result is a named corpus/profile/pin census with its remaining limitations. Passing a
test corpus is evidence, not proof of full XML conformance; the [W3C FAQ](https://www.w3.org/XML/Test/faq.html)
explicitly makes that distinction.
