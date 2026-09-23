# Jint.HtmlParser implementation record

## Scope and workflow

The requested package replaces AngleSharp in Jint.Browser with a new API for HTML,
SVG, XML, CSS, DOM mutation tracking, and browser integration. Performance must be
demonstrated against AngleSharp on equivalent work. The user explicitly supersedes
the earlier repository direction to retain AngleSharp.

- Integration worktree: `/Users/sebastienros/.codex/worktrees/bd4c/jint`.
- Integration branch: `codex/html-parser-integration`.
- Starting revision: `d78d25137526b4a7b35ccdcfc8522730acd392d1`.
- Design, planning, and reviews: GPT-6 Astra, high reasoning.
- Feature implementations: dedicated GPT-6 Sol, high reasoning tasks in local worktrees.
- Merge finalized feature commits into the integration worktree; no pull requests.

## Tasks

| Task | Identity | State |
| --- | --- | --- |
| Architecture and migration design | `01a0ceec-94ec-7f63-9bfb-189cac69df5f` | Reviewed contracts and Markdig-inspired primitive benchmarks integrated; timing pending |
| Comparison corpus and benchmark harness | `01a0ceee-7b09-7513-b507-a5c412eb4518` | Reviewed XML/SVG comparison integrated; measurements pending |
| A1 dependency and binding inventory | `01a0cef5-3c12-7d22-926c-e2aeefe58949` | Reviewed and integrated |
| A2/D1/D2 package and DOM foundation | `01a0cef5-4a7d-72e2-b551-74f03c9da7ec` | Reviewed fixes integrated; 52 combined tests pass |
| H1–H3 resumable HTML tokenizer | `01a0cf2a-8177-7941-9413-ee60edc59454` | Reviewed and integrated |
| C1 CSS syntax | `01a0cf2a-8a51-76b1-a12b-ac57c7d2b594` | Reviewed constructs and list/block extension integrated |
| C4a internal CSS syntax editors | `01a0cf89-02d0-79b0-a4d1-5f37f747f728` | Reviewed fixes integrated |
| A2 shared limits/diagnostics/errors and API snapshots | `01a0cf2c-ab5e-7023-95ab-1ab3a4fc8d77` | Reviewed and integrated |
| D3a native cloning and import | `01a0cf34-dc42-75d0-960e-735bef6eb68a` | Reviewed and integrated |
| XML shared contracts and provenance | `01a0cf2c-ab5e-7023-95ab-1ab3a4fc8d77` | Reviewed and integrated |
| X1 native XML/SVG parsing | `01a0cf3f-9380-7513-a351-4078c30d2241` | Reviewed facade/DTD fixes integrated; full corpus acceptance pending |
| Native metadata, adoption and templates | `01a0cf47-2a27-7163-8f4e-951fbe9999f5` | Reviewed and integrated, including H4 prerequisites |
| D4a iterative native traversal | `01a0cf51-0a0a-7753-8235-126166e1484c` | Reviewed and integrated, including template boundaries |
| C2a selector compiler | `01a0cf57-a964-7922-9f56-3c568a0e51f1` | Reviewed corrections integrated |
| C2b structural selector evaluator | `01a0cf84-0ef3-70f2-bb95-81bcea063cf7` | Reviewed corrections integrated |
| C2c relational selector evaluator | `01a0cfa9-3193-73c1-a9cf-f5e2d915cca7` | Reviewed contract dispatched; implementation in progress |
| H4 HTML tree construction | `01a0cf5a-9eed-7913-bcb4-e019fbbb5e8c` | Reviewed corrections integrated |
| H5a HTML table structure | `01a0cf80-d939-7a72-829a-859771c547f0` | Reviewed fixes integrated |
| H5b HTML table text and foster insertion | `01a0cf9d-4989-78a3-9ec5-3302a335a8bc` | In progress; exclusive tree-builder owner |
| H5b native fresh insertion prerequisite | `01a0cfa5-4d54-73d1-9e18-3f263463a87c` | Reviewed implementation integrated |
| D5 native mutation tracking | `01a0cf62-1c76-7411-ae32-d5d6beee5915` | Reviewed corrections integrated; native ownership retained for fixes |
| XML conformance corpus and harness | `01a0cf6e-aa5f-7631-af1b-03f6136013a1` | Full harness integrated; remaining policy/output debt fails visibly |
| Native immutable XML notation metadata | `01a0cf91-311c-7283-8de6-5272978c0529` | Metadata and XML population integrated; corpus OUTPUT adaptation in progress |
| D6s1 native shadow root ownership | `01a0cfae-7f75-7c72-bc9f-c9f06fccb468` | Independently reviewed contract dispatched; implementation in progress |
| Packed public XML and mutation consumer | `01a0cf2c-ab5e-7023-95ab-1ab3a4fc8d77` | Reviewed harness and package README integrated |
| C5 V0a CSS value primitives | `01a0cf7b-7669-7883-944b-3f3c35fc4585` | Reviewed corrections integrated |
| C5 V0b1 basic CSS math | `01a0cf9b-ba8a-7a32-b6e4-09de7672e6ed` | Reviewed design dispatched; implementation in progress |

The XML conformance gate inventories all 2,585 pinned W3C rows with explicit profile
classifications and separate output assertions. Current execution results and remaining debt are below.
The reviewed CSSOM plan separates internal syntax editing from validated property and
rule semantics; public CSSOM completion requires the full reviewed registry disposition.

## Initial repository evidence

At the starting revision, Jint.Browser targets `net8.0;net10.0` and references
AngleSharp 1.8.2, AngleSharp.Css 1.1.2, AngleSharp.Xml 1.2.0, and
AngleSharp.XPath 2.0.6. There are 194 browser C# files containing AngleSharp
references. This includes generated bindings, DOM storage, mutation observation,
CSSOM, parsing, and script/resource scheduling.

`Runtime/DocumentFetch.cs` reads a bounded response body, decodes it, and supplies
the resulting string to `Runtime/Parsing/ParserDriver.cs`. Script suspension and
`document.write` insertion are separate requirements from transport streaming.

## Validation

- Baseline Release build: passed on .NET 8 and .NET 10, zero warnings/errors.
- Baseline document-loading, DOMParser, and mutation-observer tests: 88 passed,
  zero failed/skipped across both frameworks. Filter:
  `FullyQualifiedName~DocumentLoadTests|FullyQualifiedName~MutationObserverTests|FullyQualifiedName~DomParserTests`.
- Local restore requires `-p:RestoreSources=https://api.nuget.org/v3/index.json`
  because the user-level additional feed has no source mapping (`NU1507`).
- Native DOM and shared parser contracts are implemented; parsing algorithms and
  browser migration remain in progress. No performance claim has been established.
- Integrated corpus validation: all 12 fixtures pass, plus count-preserving HTML,
  XML and CSS corruption probes. This was correctness validation, not timing.
- Integrated inventory: 192 source/build consumers, 1,691 named generated members,
  17 WPT suites; lock check and all six Python regression tests pass.
- Integrated DOM/shared-contract/API-snapshot suite: 52 tests passed on net8.0/net10.0,
  no failures/skips. Author separately verified signed packing and a Native AOT
  consumer of the packed package; this establishes only the current native surface.
- After integrating clone/import, the suite passes 64 tests across both frameworks,
  with zero failures/skips.
- After integrating CSS syntax, XML shared contracts and reviewed generated API
  snapshots, the suite passes 102 tests across both frameworks, zero failures/skips.
  No document parser or browser replacement
  is claimed by these construct-level and native-DOM milestones.
- After integrating H1/H2 and its reviewed preprocessing, EOF and cooperative quota
  fixes, 332 tests pass across both frameworks, zero failures/skips. H3 text modes,
  tree construction and the full XML implementation remain unfinished.
- After integrating document MIME/charset metadata and XHTML creation behavior,
  340 tests pass across both frameworks, zero failures/skips.
- Trusted construction and deterministic bulk cancellation bring the suite to 352
  passing tests. Adoption/replace-all then bring it to 372, zero failures/skips.
- With native traversal and the reviewed XML core, 442 tests pass. Integrating H3,
  the internal selector exception and corrected template ownership brings the suite
  to 630 tests across both frameworks, zero failures/skips. Full XML/HTML facades,
  browser replacement and comparative performance acceptance remain unfinished.
- H4 shared options, native document mode and explicit template traversal tests bring
  the integrated suite to 646 passing tests across both frameworks, zero failures/skips.
- With the reviewed parsed-attribute merge, 658 tests pass across both frameworks,
  zero failures/skips.
- With owned appendable Text storage and cancellation-atomic parser appends, 672
  tests pass across both frameworks, zero failures/skips. This is functional and
  structural validation, not a measured speedup against AngleSharp.
- The integrated XML/SVG facade, reviewed DTD and fragment namespace fixes, and
  native mutation subscriptions pass 844 tests across both frameworks, zero
  failures/skips. Compiled public API snapshots add exactly the three XML/SVG
  entry points. Full W3C corpus acceptance and Browser migration remain pending.
- Corrected H4 document/head/body/text tree construction brings the combined suite
  to 1,004 passing tests across both frameworks, zero failures/skips. Table and
  later families remain explicit internal stops; no public HTML parser is claimed.
- The corrected internal selector compiler brings the combined suite to 1,162
  passing tests across both frameworks, zero failures/skips. Exact numeric conversion
  includes deterministic cancellation and operand-work scaling guards. Matching and
  public selector APIs remain separate unfinished work.
- CSS list/block recovery brings the suite to 1,194 passes. The corpus-driven XML
  version correction brings it to 1,212, and the reviewed native PI factory changes
  bring it to 1,218, all across both frameworks with zero failures/skips. A separate
  clone/import test follow-up passes all eight focused PI tests across the two TFMs.
- With the XML parser using trusted PI construction and all seven corpus-character
  regressions covered through parsing/clone/import, the full combined suite passes
  1,240 tests across both frameworks, zero failures/skips. Corpus reruns still owe
  independent confirmation and required notation-reporting/output work remains.
- Reviewed CSS value primitives bring the pre-corpus suite to 1,372 passing tests
  across both frameworks. The integrated full corpus harness then runs 5,364 total
  tests: 4,494 pass and 870 fail, with zero skips. Per framework, all non-corpus
  tests pass; 434 individual corpus cases and the census gate fail visibly.
- After reviewed H5a table structure, C4a syntax editors and thirteen additional exact XML
  expectations, the integrated suite runs 5,436 tests: 4,592 pass and 844 fail, zero skips.
  All failures are the existing corpus obligations: 421 individual cases and the census per TFM.
- Native notation contracts and the separated optional-error policy gate bring the integrated
  full suite to 5,446 tests: 4,624 pass, 822 fail, zero skips. Failures remain 410 individual
  corpus obligations and the census per TFM; all other tests pass. Subsequent structural-selector
  integration passes all 48 focused tests across both TFMs. Native notation/API checks pass 26.
- The current corpus census is 2,585 inventoried rows: 593 outside the XML/namespace
  profile, 18 reviewed byte-boundary exclusions, 1,947 runnable candidates and 27
  optional-error decisions. It reports 1,559 conformance passing cases, 387 unresolved required cases,
  one known required notation-reporting defect, and zero harness-error outcomes.
  Output evidence is 238 comparisons passed out of 386 eligible, with 148 pending.
  The original eight parser defects are confirmed fixed by this corpus rerun.
  Optional policy results are separate: 16 observed but unreviewed, six unsupported byte
  adapters, five verified policies and zero policy mismatches. Verified optional outcomes
  never increment conformance passes; unreviewed and adapter categories still fail the gate.
- Twelve separately named UTF-16 surrogate tests pass after constructing code units
  at runtime. An earlier failure report came from attribute-metadata replacement of
  invalid surrogate strings, not a scanner defect; no production fix was made.
- Integrated XML/SVG comparison validation passes all four unchanged corpus fixtures
  and rejects count-preserving corruption. It uses only the reviewed default-xmlns
  representation projection and documents SVG MIME branding. These are correctness
  results, not benchmark timing or a speedup claim.
- Integrated Markdig-inspired primitive correctness checks pass on net10.0: exhaustive
  UTF-16 membership, sliced delimiter positions, exact tag membership and every row's inputs.
  The 33 throughput/allocation rows remain untimed; external machine activity prevents an
  uncontended measurement window. The reviewed batch plan retains gate jobs and idle checks.
- Fresh integration checks pass 118 tests across both TFMs for parser insertion, construction,
  templates, mutations and structural selectors. XML notation parsing, immutable metadata and
  public API checks pass another 62 tests. These focused runs follow the full-suite counts above;
  corpus notation OUTPUT comparisons are still being wired to the actual metadata.
- The unsigned packed consumer passed fresh local-package runs on net8.0 and
  net10.0, plus a Native AOT publish/run on osx-arm64 in its implementation worktree.
  Package inspection found no production dependencies. Its reviewed probes cover
  public XML/SVG/fragments and mutation records; this is not the final full-package gate.
- The first full XML corpus run exposed a valid 1.x declaration rejection and seven
  current XML Name character rejections at the native PI factory. All eight are fixed;
  unfinished output/profile expectations remain visible.

## Integrated commits

- `30f9343eb`: initial Astra architecture and dependency plan, from `e47db2a0d`.
  Independent Astra review requested tighter configuration scope, complete standalone
  construct APIs, explicit ownership of intrinsic element semantics, and completion
  gates covering the standalone package as well as browser replacement.
- `b859c4098`: all four design review findings addressed; Astra recheck clear.
- `df30e1793`, `4e90d1b3b`: corpus/baseline and semantic validation corrections;
  Astra recheck clear. Source commits `a86964ce2`, `2bf2537cd`.
- `813e51988`: inventory plus coverage/CRLF corrections; Astra recheck clear.
  Source commit `26297b86e`. Lock refreshed for the integrated benchmark consumer.
- `4ebd36845`: exact independent HTML, CSS and shared-contract feature scopes,
  from Astra commit `6d8df1d10`.
- `6bc4057b3`, `97edc7997`, `b4c61bbe8`: native DOM plus all Astra correctness and
  document-append performance fixes (source `5f7c3a854`, `61d1fbe20`, `2f97a615c`).
  Final Astra recheck clear.
- `a8a3aeb66`, `d1cf75f02`: shared contracts and generated public API snapshots
  (source `e20551f4a`, `1e0da12b`); both Astra reviews clear.
- `3ca355b77`: iterative clone/import and generated API snapshot additions
  (source `21387e9fb`); Astra review clear.
- `30ab14f99`, `7e2a94adb`, `307ad2b43`: XML/SVG design and corrections
  (source `8f275e24b`, `8f18f92bc`, `862696a2c`). The final reviewed policy supersedes
  the intermediate rejection proposal: no-fetch external entity omissions remain
  successful parses and are exposed through immutable document provenance.
- `951f1693f`: reviewed current HTML PI and CSS UnicodeRange contract additions
  (source `0d4384907`).
- `9c5428c13`, `f25eb56ed`, `18fa7a2c0`: CSS syntax tokenizer, four construct APIs,
  recovery and UnicodeRange corrections, and bounded cancellation through final
  scans (source `370721cd7`, `1a99cca13`, `35d21d71d`); final Astra recheck clear.
- `552108b99`: XML options, errors, expansion budget and immutable skipped-entity
  provenance (source `e1c210c9e`); Astra review clear.
- `ccc8e41c9`: honest cooperative work-quota contract around unavoidable runtime
  allocation/copy operations (source `5b07559e1`); Astra design and review agree.
- `c0e96e3f8`: reviewed metadata, adoption and template ownership contracts
  (source `163977719`).
- `497b6fad6`, `efb6a1ef3`, `8577fe450`, `5137d1587`, `e29b5b838`: H1/H2 tokenizer,
  current-spec processing instructions, focused boundary coverage, CR preprocessing
  and EOF diagnostics, and cooperative materialization accounting (source `1c917e00e`,
  `ad07d7f3e`, `03a72284b`, `69953b997`, `dc805af10`); final Astra rechecks clear.
- `3a530e187`: reviewed mutation subscription and delivery contracts
  (source `56e4fc086`).
- `96b2fd38b`, `6e9c7d667`: reviewed trusted parser construction contracts and required
  bulk-initialization cancellation (source `ccf8673a4`, `5f6837e3d`).
- `c0a9ea848`, `686d5d43a`: document metadata, XHTML creation and XML MIME suffix
  correction (source `bfcc65603`, `98399a3bd`); final Astra recheck clear.
- `f46b030d9`, `a17b9c326`: trusted native construction and deterministic mid-batch
  cancellation (source `ea54f0f0c`, `84ce04f44`); final Astra recheck clear.
- `5dd824c92`: explicit adoption and replace-all with complete pre-validation,
  fragment ownership distinctions and generated API snapshots (source `cb4ed3d8e`);
  Astra review clear.
- `32b48cf7e`: iterative traversal and cancellation through final ascent
  (source `ef53e0d84`); Astra review clear.
- `be0b46f70`: reviewed selector compilation/matching dispatch and explicit
  standards/compatibility decisions.
- `b7b5d6a36`, `5418ccd99`, `c44261538`, `c8e3c5bd1`: internal XML core and all
  reviewed namespace, fragment, linear-construction, allocation and polling fixes
  (source `ceb09b1a8`, `28519f37a`, `e0a27b030`, `d7c76cccc`); final Astra rechecks clear.
- `eb778d660`: H3 text modes and script escapes (source `2d4655709`); Astra review clear.
- `850ce0628`: internal selector parse exception (source `f18cc643b`); Astra review clear.
- `2de85c47c`, `902774bb7`: native templates and same-document adoption-boundary
  correction (source `1bf760697`, `e8ebfb4b`); final Astra recheck clear.
- `fc5083c41`: reviewed H4 tree-construction dispatch and native/shared prerequisites.
- `26777525d`, `c889b18c6`, `83d055e61`: internal HTML options, document mode and
  template traversal coverage (source `f2e23bfb3`, `5003d0bed`, `4f42454ea`);
  all three Astra reviews clear.
- `650046e3a`: linear missing-attribute merge for published parser elements, with
  cancellation and native attachment semantics (source `4f4be0e6`); Astra review clear.
- `b181d595e`: owned text accumulation, cached reads and atomic append cancellation
  (source `8fc2f247`); Astra review clear.
- `f63b974ee`: pinned XML conformance design with independent corpus/license review
  and a corrected final gate requiring zero pending eligible output assertions.
- `e1ca3d670`, `1c5471849`: reviewed CSSOM, C1 list/block prerequisite and full
  property/rule grammar handoff (source `3f4cbe893`, `b42bd9d9d`).
- `0ba9f5cf7`, `08fdc86e8`, `8d6ec41d7`, `225022c11`, `73bec95dc`: XML DTD,
  public XML/SVG/fragments, template coverage and reviewed entity, namespace,
  linear binding and cancellation fixes (source `3ce31a8c0`, `ab00fb2c0`,
  `b96ae83aa`, `4ff50f33c`, `9982abe36`); final Astra scoped rechecks clear.
- `6f2ceb8f7`, `c7a5e212e`: native mutation subscriptions and reviewed sibling,
  replacement, allocation and document-local observer-summary fixes (source
  `49267e53f`, `320af71ba`); final Astra recheck clear.
- `a09e4ba52`: pinned W3C XML corpus import, licensed source routes and complete
  inventory (source `7fcd8c112`); Astra independently verified hashes and exact
  regeneration. Counts are inventory classifications, not parser passes.
- `576861c28`: reviewed finite CSS atom, math, substitution and color dispatches,
  with an exact first V0a primitive contract and explicit validated-CSS completion debt.
- `996243c61`: Astra clarification that CSS recovery preserves only the boundaries
  specified by CSS Syntax, including top-level stray closing braces.
- `3b39e044c`: independently reviewed H5–H8 table, formatting, foreign, fragment and
  Browser handoffs (source `8ab522a5d`).
- `04f165009`, `baa152c97`, `c125dafc2`: H4 tree construction and reviewed CR,
  resumability, suffix-index work and recovery fixes (source `115caab7e`,
  `1db66eced`, `43416aaea`); final Astra recheck clear.
- `544cd394d`, `00aaf428f`: unsigned packed consumer and accurate package capability
  README (source `cb2311329`, `12106a58fa`); both Astra reviews clear.
- `b50d2c20a`, `0ebf91c58`, `a9dfec5e3`, `8b12667f1`, `4cc1c9ada`, `f83bda131`:
  selector compilation and reviewed grammar, offsets, balanced decimal conversion,
  cancellation and complexity guards (source `d7e9fa6c4`, `de075bbb1`, `6ab888064`,
  `18f6d9dc6`, `175d169bb`, `13643b781`); final Astra rechecks clear.
- `5ea8680d4`, `a05acf8df`, `061b18b92`: CSS list/block parsing and reviewed recovery
  and actual closing-token ownership fixes (source `e19bc96f3`, `92dc66729`,
  `a6d910e7b`); final Astra recheck clear.
- `fe1575d67`: XML/SVG AngleSharp/native comparison rows and semantic corruption
  probes (source `00c7b7ec1`); Astra review and integrated untimed validation clear.
- `9068b8d00`, `079efebc3`: reviewed XML version/PI contract correction and version
  implementation (source `6b329f0d7`, `1c0fa5cd2`).
- `37c8a6712`, `2156e4f85`: reviewed native PI Name/trusted construction and clone
  tests (source `b69f678e1` plus test-only delta to `6eb97386c`).
- `f1a0c5064`: reviewed XML PI callsite and parser-specific name/cancellation
  regressions (source `dc2cd51d9`).
- `62d507d48`, `10e6e6a02`: reviewed CSS atom primitives and first-offending-component
  fixes, with meaningful cancellation tests (source `e418197ec`, `a41f4ce4d`).
- `45cba8797`: reviewed all-read immutable XML notation reporting design
  (source `0b7ae9a8b`); native and parser implementation remain required.
- `cac76ef1d`, `eab3aca1c`: reviewed public-parser corpus runner, strict decoding,
  Second Canonical Form evidence, exact byte/resource classifications and corrected
  projection/output gates (source `f81da684`, `841569ee`). Remaining debt fails tests.
- `d247afc8f`: thirteen exact reviewed XML rejection expectations (source `ea47120b7`).
- `cd036b7de`: independently reviewed finite basic CSS math implementation contract.
- `ff8b96592`, `8f99ad326`: reviewed HTML table structure and heading-scope/reset-index
  corrections (source `8e5d87112`, `17090b80e`); deterministic repeated-prefix work is linear.
- `2ae5e4f67`: optional XML cases execute without becoming conformance passes
  (source `2943b43b2`); exact policy verification remains separate.
- `7bcaddb84`: reviewed Markdig-inspired throughput/allocation kernels and independent
  correctness checks (source `35233301e`); no timing or optimization winner claimed.
- `0a2ec01fd`, `14a86667a`, `7a1e1320d`: reviewed internal CSS syntax editors,
  quoted-URL serialization and deterministic projection cancellation fixes
  (source `0514cfa14`, `a91e4c417`, `eb70e3d76`).
- `6d16ae869`, `0699a0075`: immutable XML notation metadata and meaningful in-copy
  cancellation coverage (source `2af2f6929`, `f7436629d`); final Astra review clear.
- `82ccc8c88`: separate reviewed optional XML policy outcomes and exact evidence
  (source `b5744d46f`); no conformance credit for arbitrary optional outcomes.
- `9f6a62c04`: six reviewed external-subset/input expectations (source `386cba7dc`).
- `60e4e5fe8`: independently reviewed fresh-node insertion-before prerequisite contract.
- `70f449e02`, `7134ce47e`: reviewed structural selector matcher and namespace,
  document-whitespace, cancellation and quadratic-search corrections
  (source `ce034565f`, `503a4984a`); final Astra re-review clear.
- `817a7e2cd`: actual in-scan selector cancellation regression (source `8e23ddac3`).
- `3d402598e`: six behavior-preserving System.Math qualifications required by the new
  CSS math namespace (source `946ebfb4b`); narrow Astra review and both framework builds clear.
- `1325bef6a`: five exact reviewed XML omission expectations (source `827440291`),
  including original CRLF offsets of 82 for the external general entity references.
- The reviewed unused-external-declaration packet (source `5b11c30ea`) retains three
  original canonical OUTPUT comparisons. Its author census reports 1,567 conformance passes,
  379 unresolved required cases and one known notation defect; 241 of 386 eligible OUTPUT
  comparisons pass. Integrated full-suite counts above precede these last two expectation packets.
- `5db884318`: integrated unused-declaration packet identified above.
- `5c35e0045`: reviewed native fresh parser insertion before a reference child
  (source `803a9257e`), with shared mutation semantics and coherent cancellation.
- `4c760074e`: independently reviewed staged native shadow-root and slot contracts,
  including scoped registry cloning, copied-slot signals and stored assignment semantics.
- `e140d6e34`, `6b84be947`, `0ab67dfc5`: all-read XML notation parsing and precise
  normalization/collection cancellation coverage (source `9b558a3cd`, `83081d70c`,
  `03efd23de`); final Astra re-review clear.
- `7687ff6a2`: reviewed optional system-identifier fragment policy (source `0ef35c784`),
  counted separately from conformance passing cases.
