# Jint.HtmlParser implementation record

## Scope and workflow

**All six retained chats finalized at the user’s request on September 25.** The original project remains
incomplete; Browser still uses AngleSharp. This pass integrated the already-started slices and archived their implementation chats, preserving
future work and acceptance debt in the common worktree. No new feature work was started.

### Retained-chat finalization

- HTML creation metadata source `ef64460e2` passed independent Astra review and is integrated as
  `ac2583318`. Both native/XML and current HTML creation paths now preserve the immutable IsValue.
- CSS substitution analysis full chain through `2896d3e3d` passed independent final re-review and is
  integrated through `9fec0c126`. This is analysis only; substitution execution/property validity remain.
- XML source `dd367c16a` is integrated as `3169c483b`. Root compared every one of the three 6,283-node
  expected projections against the independent source packet, checked all pins/omissions/policy wording,
  and verified all 24 previous policy entries are unchanged. Fresh common corpus: **4,022 total,
  3,766 passed, 256 expected debt failures, zero skips**. All failures are 254 pending required rows
  plus two census checks. Optional policies are now 27 verified/zero observed; required debt remains
  127 per TFM and OUTPUT remains 344 compared/42 pending. `/private/tmp/jint-finalize-xml-corpus.log`.
- Fresh common Release non-corpus tests after those integrations: **3,214/3,214**, zero failures/skips,
  net8/net10 combined: `/private/tmp/jint-finalize-metadata-substitution.log`.
- Architecture audit found all 29 existing deliverable commits patch-equivalent in common; no new
  design work was needed. Architecture, HTML metadata, substitution and XML corpus chats are archived.
  Full XML acceptance remains a parent-project obligation; closing its harness chat does not erase debt.
- Exponential source `f33b09c67`, `aa89a1743`, `83384fe13` integrated as `41cb071a4`, `9da2e4396`,
  `6d55e76ed` after independent review and both correction rounds. Functions pow/sqrt/hypot/log/exp
  complete the finite 21-function census. This does not complete CSS. Independent 2,197-case sweeps
  per TFM have no spurious overflow in 1,177 representable cases; exact-overflow-edge saturation for
  106 beyond-MaxValue cases is an explicitly accepted binary64 precision limitation. The packet is
  retained in ignored `artifacts/html-parser-review/exponential/`; no correctly-rounded claim.
  Fresh common Release non-corpus suite: **3,270/3,270**, zero failures/skips, net8/net10 combined:
  `/private/tmp/jint-finalize-all-integrated.log`. Math source/test trees match the reviewed worker.
  Task archived. Fresh common Browser Release build passed both TFMs, zero warnings/errors:
  `/private/tmp/jint-finalize-browser-build.log`.
- Benchmark harness/source audit found no unmerged work. Fresh common Release correctness checks passed
  all 12 baseline fixtures, four native/AngleSharp XML/SVG fixtures with corruption probes, and exhaustive
  primitive comparisons. `/private/tmp/jint-finalize-benchmark-validation.log`. Task archived. Timings
  and full HTML/CSS/Browser comparison remain project-level acceptance work; no speedup claim.

### Latest resumed integrations

- Numeric policy: source `d1cfff5b9` + `24321b557` integrated as `9eeecd86e` + `5b6253947`;
  approval/evidence clarification `51c6c3120`. Astra review clear. Root independently retrieved the
  pinned `input-valueasnumber.html` and confirmed the documented huge-local empty-result assertion.
  The native/spec discrepancy stays explicit. Calendar's 1024-bit bound never caps decimal remainder
  arithmetic. No numeric runtime implementation or performance result is claimed.
- Exact-six Japanese prepared-input adapter: source `185837889` integrated as `2a41e82a4` after
  independent Astra review and 24 extra negative source probes. Common Python checks 3/3 and fresh
  Release selected tests 60/60 pass across net8/net10. Log `/private/tmp/jint-prepared-inputs-integrated.log`.
  Full common corpus: 4,010 total, 3,742 passed, 268 failed, zero skips. Per TFM: 1,820 required passes,
  127 pending, six optional observed, zero adapter debt, 21 optional verified, zero harness failures
  or mismatches, OUTPUT 344 compared/42 pending. Log `/private/tmp/jint-prepared-corpus-integrated.log`.
  Additional passing tests verify the harness; no required corpus result was reclassified.
- XPath full chain `3e00d8717`, `2c61763d8`, `f795d1350`, `50bfc1350` integrated through `c131b6a9f`.
  Full semantic review and separate final cancellation re-review clear. Lookahead now polls within
  long legal whitespace; regression proves cancellation at the first 256-unit checkpoint.
  Common fresh Release non-corpus suite: **2,856/2,856**, net8/net10 combined, zero failures/skips.
  Log `/private/tmp/jint-xpath-integrated.log`. Worker/common XPath trees are identical; task archived. Worktree removal is pending because
  neither task exposes an attached worktree identity through `list_artifacts`; retain the clean checkout
  until managed cleanup can address it, rather than deleting it outside the archive tool.
- Native/XML IsValue source `eeb5b0c1a` integrated as `190f59aa9` after independent Astra review.
  Common fresh Release non-corpus suite passed 2,868/2,868. HTML capture is still separate; the native
  task `01a0dafe-8d1a-73b3-bc05-8fab6d2fdd92` is archived. Its clean `a768` checkout remains because
  managed worktree identity was not exposed; no shell deletion was used.
- Template adoption fixes `a0ab062cc` + `326513ea4` integrated as `4f971ae0f` + `f3e6d0c6a` after
  independent re-review. Final replacement uses the furthest block's owner; inner recreation uses
  the stack common ancestor per HTML step 13.6. The initial blanket inert-owner criterion was too
  broad and is corrected in the pause note. Common fresh Release non-corpus suite now passes
  **2,872/2,872**, zero failures/skips, net8/net10: `/private/tmp/jint-adoption-owner-integrated.log`.
- Three exact weekly Japanese optional policies source `3d6926625` integrated as `fa6a79fd4` after
  source packet review and independent 150-node projection comparisons. The full common run found
  one stale prepared-input guardian; reviewed explicit six-key correction `7ca8ae471` integrated as
  `cca66f703`. Latest corpus: **4,016 total, 3,754 passed, 262 expected debt failures, zero skips**.
  Every failure is a pending required case, one of the three unreviewed `pr-xml-*` cases, or the census.
  Per TFM: 1,820 required passes, 127 pending, three optional observed, zero adapter debt, 24 optional
  verified, zero harness/mismatch failures, OUTPUT 344 compared/42 pending. No required-pass gain.
  `/private/tmp/jint-weekly-policy-guard-integrated.log`. The remaining three source policies now have
  an independently approved packet; worker `dd367c16a` awaits implementation review and common tests.
- Trig source `db97abc02`, `3f7a25b58`, `6c552a0fd` integrated through `d43915da9` after Astra
  re-review. Exact turn/grad quarter turns survive canonical conversion without epsilon matching.
  Common fresh Release non-corpus suite: 2,986/2,986. Task archived; clean `ba36` cleanup pending.
- H6e framesets source `150faca00`, `7cb1f9922`, `95519f45d` integrated through `e4e72d0e9` after
  independent review. Latest common non-corpus suite **3,022/3,022**, zero failures/skips, net8/net10:
  `/private/tmp/jint-framesets-integrated.log`. Task archived; clean `ad95` cleanup pending.
- TreeConstruction ownership transferred to dedicated HTML IsValue task in `aebc`. Worker
  `ef64460e2` is clean and locally tested but awaits independent review/integration.
- V0c1 corrections through `2896d3e3d` remain unmerged; the last provisional-wrapper fix still needs
  re-review. Dedicated exponential math task in `4cb4` has checkpointed all five functions at `f33b09c67`. Neither
  slice is common functionality yet. All active tasks were asked to checkpoint and stop for this pause.



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
- User cleanup rule: archive tasks and remove their worktrees after review, integration and
  successful common-worktree checks. Retain interrupted/failed tasks until unfinished work is
  recovered. Keep task history and Git branches; use managed archive cleanup and verify removal.
  Fifty-three completed tasks have now been archived; forty-three worktrees are gone.
  Ten clean completed checkouts remain pending managed cleanup because attached identities are absent:
  `ff28`, `a768`, `ba36`, `ad95`, `dc58`, `aebc`, `4983`, `b0f9`, `b0b0` and `4cb4`.
  Patch equivalence, clean local state and stopped-task status were checked before removal.
  Earlier worktrees already removed by Codex remain represented by their integrated commits.

## Tasks

| Task | Identity | State |
| --- | --- | --- |
| Architecture and migration design | `01a0ceec-94ec-7f63-9bfb-189cac69df5f` | All existing design deliverables integrated; task archived; managed cleanup pending |
| Comparison corpus and benchmark harness | `01a0ceee-7b09-7513-b507-a5c412eb4518` | Existing corpus/comparison harness integrated and correctness-validated; archived; timings remain parent acceptance work |
| A1 dependency and binding inventory | `01a0cef5-3c12-7d22-926c-e2aeefe58949` | Reviewed and integrated; archived, worktree removed |
| A2/D1/D2 package and DOM foundation | `01a0cef5-4a7d-72e2-b551-74f03c9da7ec` | Reviewed fixes integrated; 52 combined tests pass; archived, worktree removed |
| H1–H3 resumable HTML tokenizer | `01a0cf2a-8177-7941-9413-ee60edc59454` | Reviewed and integrated; archived, worktree removed |
| C1 CSS syntax | `01a0cf2a-8a51-76b1-a12b-ac57c7d2b594` | Reviewed constructs and list/block extension integrated; archived, worktree removed |
| C4a internal CSS syntax editors | `01a0cf89-02d0-79b0-a4d1-5f37f747f728` | Reviewed fixes integrated; archived, worktree removed |
| A2 shared limits/diagnostics/errors and API snapshots | `01a0cf2c-ab5e-7023-95ab-1ab3a4fc8d77` | Reviewed and integrated; archived, worktree removed |
| D3a native cloning and import | `01a0cf34-dc42-75d0-960e-735bef6eb68a` | Reviewed and integrated; archived, worktree removed |
| XML shared contracts and provenance | `01a0cf2c-ab5e-7023-95ab-1ab3a4fc8d77` | Reviewed and integrated; archived, worktree removed |
| X1 native XML/SVG parsing | `01a0cf3f-9380-7513-a351-4078c30d2241` | Reviewed facade/DTD fixes integrated; full corpus acceptance pending; archived, worktree removed |
| Native metadata, adoption and templates | `01a0cf47-2a27-7163-8f4e-951fbe9999f5` | Reviewed and integrated, including H4 prerequisites; archived, worktree removed |
| D4a iterative native traversal | `01a0cf51-0a0a-7753-8235-126166e1484c` | Reviewed and integrated, including template boundaries; archived, worktree removed |
| C2a selector compiler | `01a0cf57-a964-7922-9f56-3c568a0e51f1` | Reviewed corrections integrated; archived, worktree removed |
| C2b structural selector evaluator | `01a0cf84-0ef3-70f2-bb95-81bcea063cf7` | Reviewed corrections integrated; archived, worktree removed |
| C2c relational selector evaluator | `01a0cfa9-3193-73c1-a9cf-f5e2d915cca7` | Reviewed correctness and complexity fixes integrated; archived, worktree removed |
| H4 HTML tree construction | `01a0cf5a-9eed-7913-bcb4-e019fbbb5e8c` | Reviewed corrections integrated; archived, worktree removed |
| H5a HTML table structure | `01a0cf80-d939-7a72-829a-859771c547f0` | Reviewed fixes integrated; archived, worktree removed |
| H5b HTML table text and foster insertion | `01a0cf9d-4989-78a3-9ec5-3302a335a8bc` | Reviewed implementation and diagnostic correction integrated; archived, worktree removed |
| H6a active formatting reconstruction | `01a0cfba-939d-7c52-9867-dc86e37f5b0d` | Reviewed fixes integrated and tested; archived, worktree removed |
| H6b adoption agency | `01a0cfd9-5ae3-7db1-bb26-d25650617b85` | Reviewed corrections integrated and tested; archived, worktree removed |
| H5b native fresh insertion prerequisite | `01a0cfa5-4d54-73d1-9e18-3f263463a87c` | Reviewed implementation integrated; archived, worktree removed |
| D5 native mutation tracking | `01a0cf62-1c76-7411-ae32-d5d6beee5915` | Reviewed mutation and PI corrections integrated; archived, worktree removed |
| XML conformance corpus and harness | `01a0cf6e-aa5f-7631-af1b-03f6136013a1` | Existing harness/policies integrated and tested; task archived; remaining acceptance debt owned by parent |
| Native immutable XML notation metadata | `01a0cf91-311c-7283-8de6-5272978c0529` | Metadata, parser population and reviewed SCF output integrated; archived, worktree removed |
| D6s1 native shadow root ownership | `01a0cfae-7f75-7c72-bc9f-c9f06fccb468` | Reviewed roots/ownership and API snapshots integrated; archived, worktree removed |
| Packed public XML and mutation consumer | `01a0cf2c-ab5e-7023-95ab-1ab3a4fc8d77` | Reviewed harness and package README integrated; archived, worktree removed |
| C5 V0a CSS value primitives | `01a0cf7b-7669-7883-944b-3f3c35fc4585` | Reviewed corrections integrated; archived, worktree removed |
| C5 V0b1 basic CSS math | `01a0cf9b-ba8a-7a32-b6e4-09de7672e6ed` | Reviewed fixes integrated and tested; archived, worktree removed |
| D7a1 native form association | `01a0cfce-720d-76a2-aff0-6ab0c6c1315a` | Reviewed corrections integrated and tested; archived, worktree removed |
| X4a native XPath adapter | `01a0cfc2-9808-7643-a56f-c29d6ec8005c` | Reviewed fixes integrated and tested; archived, worktree removed |
| C2d1 native table-column model | `01a0cfd3-4970-7c53-af17-767d5a5cbdaa` | Reviewed corrections integrated and tested; archived, worktree removed |
| D6r1 native boundary/static-range primitives | `01a0cfd7-4a2d-76e3-a72b-4af81516af7f` | Reviewed fixes integrated and tested; archived, worktree removed |
| X4b1 native attribute provenance | `01a0cfeb-6ebe-7261-844c-93d84fb517eb` | Reviewed and integrated; common checks pass; archived, worktree removed |
| X3a serialization kernels | `01a0cfe9-8708-7943-9168-25178627af9b` | Reviewed correction integrated; common checks pass; archived, worktree removed |
| C2d2 column selector matching | `01a0cfe9-f30f-71e2-84e1-272257d4bc26` | Reviewed corrections integrated and tested; archived, worktree removed |
| D7a2 disabledness and select ancestry | `01a0cfec-967a-7be0-a9d9-d40f474e52a1` | Reviewed cancellation correction integrated and tested; archived, worktree removed |
| D7b1a input-type classifier | `01a0cffb-5f42-7ac0-9c50-3dc022442df9` | Reviewed, integrated and tested; archived, worktree removed |
| X3b XML serialization | `01a0cffb-eb5e-7843-8a64-7bab61470fa1` | Reviewed fixes integrated and tested; archived, worktree removed |
| X4b2 XML ID typing | `01a0cff7-3642-7891-b575-1157126eaef5` | Reviewed, integrated and tested; archived, worktree removed |
| H7 tokenizer context | `01a0cfeb-eb74-7403-9fe5-5876c9699172` | Reviewed exhaustive coverage integrated and tested; archived, worktree removed |
| D6s2/D6s3 slot assignment and signals | `01a0cff7-448b-73e0-81de-577b76d90bd8` | Reviewed fixes integrated and tested; archived, worktree removed |
| H6c select parsing | `01a0d000-0601-7721-8396-d767beaf6e04` | Reviewed scope correction integrated and tested; archived, worktree removed |
| V0b2 stepped CSS math | `01a0d000-f77f-7c41-b474-a8e407aa41fe` | Reviewed, integrated and tested; archived, worktree removed |
| D7b1b pure text algorithms | `01a0d00a-ab70-7510-9978-13e50051aa2e` | Reviewed, integrated and tested; archived, worktree removed |
| X4b3 XPath completion | `01a0d011-17de-7c01-92b6-cbb7cfcba6ed` | Reviewed, integrated and common tests pass; task archived, worktree cleanup pending attachment identity |
| C3a1 form-state selectors | `01a0d016-7f69-7670-b118-27244e5dcfad` | Reviewed, integrated and tested; archived, worktree removed |
| V0b3a CSS abs/sign | `01a0d017-ef27-7d61-84a3-6a628ce82374` | Reviewed, integrated and tested; archived, worktree removed |
| H6d ordinary templates | `01a0d01a-09e1-7312-b099-2afb6efc86b7` | Integrated/tested/archived; follow-up adoption ownership corrections also integrated |
| H6e framesets | `01a0dab4-0ad4-7720-ada6-80c245e9c7ff` | Reviewed/integrated through e4e72d0e9; tested/archived; managed cleanup pending |
| Native/XML creation-time IsValue | `01a0dafe-8d1a-73b3-bc05-8fab6d2fdd92` | Reviewed, integrated, tested; archived; managed checkout cleanup pending |
| V0b3b CSS trigonometry | `01a0d029-93fb-7352-bf0c-ff2c92aec72d` | Reviewed/integrated through d43915da9; tested/archived; managed cleanup pending |
| V0c1 CSS substitution analysis | `01a0d029-9c47-7600-8b5b-e10aeb7b3b9e` | Reviewed/integrated through9fec0c126; tested/archived; managed cleanup pending |
| V0b3c CSS exponential math | `01a0db10-f9d2-7501-872f-fcfe719fdbbb` | Reviewed/corrected/integrated through6d55e76ed; tested/archived; managed cleanup pending |
| HTML creation-time IsValue | `01a0db13-f920-7580-8392-b5798ebd7d93` | Reviewed/integrated ac2583318; tested/archived; managed cleanup pending |

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
- With reviewed H5b pending table text and foster insertion, the full integrated suite runs
  5,598 tests: 4,796 pass, 802 fail, zero skips. Every failure is an XML corpus case or its
  census gate; no other test fails. This precedes the notation SCF handoff, which remains under
  review for canonical system-identifier handling. Independent H5b probes also verified 15,000
  malformed-input trees at quotas 1/3/100000; that bounded probe is not a full HTML conformance claim.
- The dependency inventory now matches 193 AngleSharp-bearing source/build files and 1,691
  generated members; all six inventory regression tests pass. The new reference belongs only
  to the reviewed XML/SVG comparison benchmark, not production Browser code.
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
- `7792a9152`: exact original OUTPUT after a reviewed parameter-entity omission
  (source `843bfa0da`, originally `96636f70a`), with negative output evidence.
- `f43f9eb18`: reviewed benchmark-only dependency inventory refresh.
- `836c4b63a`: independently reviewed native form-association/disabledness dispatch design;
  implementation awaits the shared native files currently owned by D6s1.
- `7776b504e`, `1ea70953e`: reviewed H5b pending table text/foster insertion and
  resumable diagnostic correction (source `659c22ab5`, `56b2c300a`).

## Latest integration and cleanup checkpoint

- Native shadow roots and reviewed adoption/custom-name corrections: `2adc25415`,
  `7c7cc9765` (sources `6da427a66`, `4664260dc`). Both framework API snapshots record
  only DocumentFragment becoming unsealed, in `95e003601`; its constructor remains internal.
- Reviewed XPath dispatch design: `04c75b18f`. Live traversal and column-selector designs
  remain under independent review; they are not implementation-completion claims.
- XML notation SCF and URI corrections: `c0ebd34be`, `f9e6c997b`, `0d32dc659`,
  `ef7568427` (sources `9ffd768f8`, `a9232174b`, `ab23d1713`, `1d2db11a2`).
- Exact 17-row XML default packet and 20-row OASIS packet: `89683f4e7`, `0ffd34f4a`
  (sources `5aa862e94`, `49af94df9`). The OASIS packet is nine valid and eleven not-well-formed
  upstream cases, including p62fail1; all twenty main documents meet the reviewed no-fetch policy.
- Full common suite after shadow/XML integration: 5,644 tests, 4,926 passed, 718 failed, no skips.
  All failures are XML corpus rows and its census gate. Each framework reports 1,609 conformance
  passes, 338 unresolved required cases, zero known defects/harness failures, fourteen unreviewed
  optional observations, six optional adapter debts, seven verified optional policies and zero
  mismatches. Canonical OUTPUT: 263/386 compared, 123 pending, sixteen reviewed no-fetch alternatives.
  Evidence: `/private/tmp/jint-shadow-xml-integrated.log`. This remains an intentionally red gate.
- Relational selectors and all reviewed scope/backtracking/featureless complexity corrections:
  `3f4be7cd7`, `2563d1219`, `eb5d7af66`, `f03d2ba51` (sources `6567860cf`,
  `438728ea1`, `73bfeb207`, `27a1b1693`). Fresh combined non-corpus tests after this integration
  pass 1,672/1,672 across net8/net10 with no skips.
  Evidence: `/private/tmp/jint-relational-native-integrated.log`.
- Browser still uses AngleSharp. Measurements remain pending; no speedup is claimed.

- `61583b638`: independently reviewed column-selector model and matcher dispatch design
  (source `7a721ef4e`), including the explicitly reviewed col-only mapping and EOF/footer case.
- `6c2bba12b`: twenty independently reviewed Sun validity expectations (source `56f6dc3b0`).
  Fresh integrated XML corpus tests on both frameworks: 3,998 total, 3,320 pass, 678 fail, no skips.
  Each framework now reports 1,629 conformance passes and 318 unresolved required cases; optional
  and canonical-output counts are unchanged from the previous checkpoint. Evidence:
  `/private/tmp/jint-xml-sun20-integrated.log`. No shared harness rule or runtime behavior changed.

- `decd6a290`: independently reviewed live-range/traversal stages and D6r1 dispatch.
  D6r1 adds only working boundary/static-range primitives; shared native mutations remain reserved
  to D7a1. Later live registration, text fixups, filter traversal and Browser gates remain required.

- `5db571025`, `0eb6a1bd3`: reviewed active formatting reconstruction and marker/attribute
  work corrections (sources `53b2d2b06`, `f8ea5c231`). Fresh combined non-corpus tests pass
  1,728/1,728 across both frameworks with no skips. Evidence:
  `/private/tmp/jint-formatting-native-integrated.log`. H6b adoption agency is dispatched separately.

- `e4c37f985`: seventeen independently reviewed Sun standalone/validity expectations
  (source `826eafdd2`). Fresh common XML corpus tests across both frameworks: 3,998 total,
  3,354 passed, 644 failed, no skips. Each framework reports 1,646 conformance passes,
  301 unresolved required cases, zero known defects/harness failures; optional and canonical-output
  counts are unchanged. Evidence: `/private/tmp/jint-xml-sa17-integrated.log`.
- `995927c5d`, `2d7a13c0d`: reviewed internal XPath navigator and namespace/cancellation
  corrections (sources `e978af316`, `c1635d86e`). Fresh combined non-corpus tests pass
  1,756/1,756 across net8/net10, no skips. Evidence:
  `/private/tmp/jint-xpath-native-integrated.log`. Typed IDs, detached attributes and public
  promotion remain separate required stages. The finalized task is archived and its worktree removed.
- `debfd4c0a`, `d4661b2cb`: reviewed native boundary ordering/static-range primitives and
  exact in-work cancellation assertions (sources `0e9fc3e87`, `12aecc5a5`). The task is archived;
  its clean, patch-equivalent worktree was removed after common verification.
- `246fd9961`: sixteen independently source-reviewed IBM external-ID/NDATA rejection
  expectations (source `722eec8ef`), with exact pinned-byte offsets and no generic exemption.
  Fresh full common suite after this and D6r1: 5,772 total, 5,160 passed, 612 failed, no skips.
  All 1,774 non-corpus tests pass. All failures are XML corpus rows/census; each framework reports
  1,662 conformance passes, 285 unresolved required cases, zero known defects/harness failures,
  fourteen optional observations, six optional adapter debts, seven verified policies, zero mismatches.
  Canonical OUTPUT remains 263/386 compared, 123 pending, sixteen no-fetch alternatives.
  Evidence: `/private/tmp/jint-range-ibm16-integrated.log`. The full conformance gate remains red.
- `2213565e4`, `f198e8945`: reviewed native table-column model and traversal-frame work
  correction (sources `a0314a0e5`, `fe6d0eb1d`). Independent review additionally checked 1,000
  randomized placement fixtures and 15,000 interval/order queries. Conservative forward-query
  work is O((K+1) log N + K log K) after a lazy O(N log N) index build; no timing claim.
  Fresh common non-corpus suite passes 1,820/1,820 across both frameworks, no skips.
  Evidence: `/private/tmp/jint-tablegrid-native-integrated.log`. The task is archived and its
  clean, patch-equivalent worktree removed. C2d2 matcher integration remains required.
- `02bbe4cb5`: independently reviewed native serialization design. X3a writer/scalar helpers
  can proceed independently; full walkers, metadata prerequisites, public and Browser gates remain.
- `57068b63b`, `95003f61b`, `dd332becd`, `f8cfd2065`: reviewed native form association
  and index/adoption/cancellation corrections (sources `1b6b690da`, `a9b6c3e7e`, `d52703d121`,
  `064c5574da`). ID-bearing control-free parser/clone appends have zero form tree/index visits;
  duplicate-heavy control and reorder probes have the reviewed additive work growth. Independent
  shadow, detached-root and template-content adoption probes pass. Task archived and worktree removed.
- `c127efe6a`, `ac999bdf9`: independently reviewed XPath ID provenance/detached-attribute design,
  including ID contributions from actually DTD-typed namespace declarations. X4b1 owns only its
  native prerequisite files; X4b2 parser typing and X4b3 navigator completion remain separate.
- `9153be8f8`: fourteen independently reviewed IBM notation syntax rejection expectations.
  Fresh common suite after form/notation integration: 5,850 total, 5,266 passed, 584 failed, no skips.
  All 1,852 non-corpus tests pass; every failure is XML corpus debt/census. Each framework reports
  1,676 conformance passes, 271 unresolved required cases, zero known defects/harness failures;
  optional/output counts unchanged. Evidence: `/private/tmp/jint-form-notation14-integrated.log`.
- New dedicated Sol High worktree tasks are dispatched for X3a serialization kernels, C2d2 column
  matching, X4b1 attribute prerequisites, H7 tokenizer-only context controls, and D7a2 disabledness.
  D7a2 owns HtmlElementState/HtmlFormAssociation plus new HTML helpers; X4b1 owns Attr/ParserAttribute/
  Element/NodeCloner. First-legend invalidation precedes association early returns and triggers only
  for direct HTML legend changes under HTML fieldsets. H6b remains the exclusive tree-builder owner.
- `e9ad28bd1`: sixteen independently source-reviewed NotationType rejection expectations
  (source `6eeea2af4`). Fresh common corpus tests: 3,998 total, 3,446 passed, 552 failed, no skips.
  Each framework reports 1,692 conformance passes, 255 unresolved required cases; optional and
  output counts unchanged, no known defects/harness failures. Evidence:
  `/private/tmp/jint-xml-notationtype16-integrated.log`. Full conformance remains incomplete.
- Verified next-task worktree allocation: X3a `/Users/sebastienros/.codex/worktrees/0f6d/jint`,
  C2d2 `/Users/sebastienros/.codex/worktrees/08ad/jint`, X4b1
  `/Users/sebastienros/.codex/worktrees/2a0e/jint`, H7 tokenizer prerequisites
  `/Users/sebastienros/.codex/worktrees/2ce4/jint`, D7a2
  `/Users/sebastienros/.codex/worktrees/4ad0/jint`. Task identities are pending worker callbacks;
  do not recreate tasks merely because the sidebar listing omits them.
- `c9587a6d2`, `af8fefe79`, `694579050`, `c85e869bc`, `ee9eb3693`: reviewed internal
  basic CSS math and all dimension/grouping, generated-sum/product, sibling ownership and polling
  corrections (sources `68c7178c5`, `bc6e53b5a`, `cb3e7836d`, `8b9a228e9`, `cf3d07520`).
  Independent review passed prior repros and 2,000 transformed-expression combinations, with linear
  counted work on growing deferred sums/products. Fresh common non-corpus suite: 2,098/2,098 passed
  across net8/net10, no skips. Evidence: `/private/tmp/jint-cssmath-native-integrated.log`.
  Task archived and clean patch-equivalent worktree removed. Seventeen later math functions and
  broader value/property/public CSSOM gates remain outstanding; this is not full CSS completion.
- H6b source `4470e694b` is under independent Astra review. After its integration the next builder
  stage is H6c select handling, then H6d templates, as already specified in the follow-up contract.
  D6s2 slot assignment must wait for X4b1's native-file release; it cannot be implemented coherently
  only in new files. D6s3's real signal sink is a separate sequential commit under that slot owner.
- `d9f01a690`, `253b2feb1`: independently reviewed conditional18/entity18 XML packets
  (sources `4acf9f587`, `3f799d7cc`). Fresh corpus run: 3,998 total, 3,518 passed, 480 failed,
  no skips; 1,728 conformance passes and 219 unresolved required cases per framework.
  Evidence: `/private/tmp/jint-xml-conditional-entity36-integrated.log`.
- `60c5636f9`: reviewed native attribute provenance/freshness (source `ba0207803`). Fresh common
  non-corpus suite passes 2,106/2,106 across both frameworks, no skips. Evidence:
  `/private/tmp/jint-attribute-provenance-integrated.log`. Task archived and worktree removed.
  X4b2 parser ID typing and D6s2/D6s3 slot state/signals now have separate Sol worktree dispatches.
- `9dd853b2a`: nineteen independently reviewed literal rejections (source `77d20660b`). Fresh
  corpus run: 3,998 total, 3,556 passed, 442 failed, no skips; each framework has 1,747 conformance
  passes, 200 unresolved required cases, zero known defects/harness failures. Optional/output counts
  unchanged. Evidence: `/private/tmp/jint-xml-literal19-integrated.log`.
- Fourteen decoded optional-error policies have independent source approval with all 39 pinned
  members and exact projections/skips/notations. The XML owner must first implement actual optional
  OUTPUT comparison and census evidence for eight IBM cases; verified optional policies never become
  conformance passes. Six input-adapter debts remain separate. OUTPUT9 source review is also approved
  after correcting five PUBLIC omission identifiers, including empty-versus-null and trailing space.
- `3ed170764`, `dec41527a`: reviewed native text-control design and origin/selection corrections
  (sources `72a5c46f4`, `ab41b7a81`). D7b1a's complete input-type classifier is dispatched separately;
  later value/state/parser/notification/Browser stages and numeric/file dependencies remain required.
- `b7ba4d16d`, `2569b3efe`: reviewed serialization writer/XML scalar kernels and in-copy
  cancellation-test correction (sources `4b8ce64db`, `1942e64eb`). Fresh common non-corpus tests
  pass 2,138/2,138 across both frameworks, no skips. Evidence:
  `/private/tmp/jint-serialization-kernels-integrated.log`. Task archived and worktree removed.
  X3b XML namespace-aware walker is dispatched in a separate Sol worktree.
- H6b's additive performance correction `bde7cb0fb` is under independent re-review. The exact
  middle-stack and absent-formatting-subject regression shapes are required complexity gates.

## Current checkpoint

- `83e4fd32b`, `c76e1e0b8`: reviewed adoption agency and additive work correction. Independent
  checks covered 5,000 malformed cases and quota equivalence; the finalized task was archived and
  its clean, patch-equivalent worktree removed after common tests.
- `1c384b1ae`, `266f7da54`: reviewed disabledness/select ancestry and attribute-scan cancellation.
  Combined common non-corpus suite passed 2,246/2,246 across net8/net10 with no skips. Evidence:
  `/private/tmp/jint-adoption-disabledness-integrated.log`. Task archived and worktree removed.
- `8568661cc`, `9801d1f44`: reviewed column combinators, column pseudo-classes and relational
  matching, with mutation/cancellation coverage. Independent review exercised 40,000 mixed selector
  chains. Common non-corpus tests passed 2,272/2,272 across both frameworks, no skips; evidence:
  `/private/tmp/jint-column-selectors-integrated.log`. Task archived and worktree removal verified.
- `c6bda2615`, `093bbeb98`: actual optional XML OUTPUT comparisons and fourteen exact policies.
  Common corpus-only run: 4,000 total, 3,586 passed, 414 failed, no skips. Per framework: 1,747
  conformance passes, 200 unresolved required cases, six optional adapter debts, 21 verified optional
  policies, zero policy mismatches; OUTPUT 271 compared and 115 pending. These failures remain
  visible and this is not a full-suite pass. Evidence: `/private/tmp/jint-xml-optional14-integrated.log`.
- `4519520b0`: reviewed stepped CSS math follow-up contract; separate Sol implementation dispatched.
- `6dec61a2d`: explicit parsed PUBLIC normalization contract, independently reviewed against XML
  Infoset; lexical source evidence remains unchanged. OUTPUT9 and separate provenance tests await
  independent review and common integration.
- Dedicated Sol worktrees are active for XML ID typing, slot assignment/signals, the XML serializer,
  select parsing, stepped CSS math, input classification and tokenizer context controls. Their tasks
  and worktrees remain until their own review, integration and common checks finish. No benchmark
  timings or speedup claims have been produced; Browser migration and full conformance remain open.
- `6b01ff1dc`, `740752360`: independently reviewed nine IBM OUTPUT expectations and six PUBLIC
  provenance cases. Fresh full common run: 6,284 total, 5,888 passed, 396 failed, no skips; every failure
  is an XML corpus case or its debt census. All 2,284 non-corpus tests pass across net8/net10. Per
  framework: 1,756 conformance passes, 191 unresolved required cases, six optional adapter debts,
  21 verified optional policies, zero harness failures/mismatches, OUTPUT 280 compared/106 pending.
  Evidence: `/private/tmp/jint-output9-provenance-integrated.log`. The XML task stays active with its
  unfinished work; this full run is not a green conformance gate.
- `b74916679`: independently reviewed complete input-type classifier (source `e4bd4b692`). Fresh
  common non-corpus tests pass 2,358/2,358 across net8/net10, no skips; evidence:
  `/private/tmp/jint-input-type-integrated.log`. Clean patch-equivalent worktree removed after
  verified task completion and archive. D7b1b pure text algorithms dispatched in a new Sol worktree;
  cancellable consumers must poll attribute scans before calling Parse, not use tokenless Get.
- X3b XML serializer source `2de6914f7` is under independent Astra review. Both Clark XML source
  packets (valid-sa-001–011 and valid-not-sa-001/002/010) are independently approved; the corpus
  owner is implementing their exact expectations in separate commits. Their work remains retained.
- `3176c388c`, `e9188cd3e`: both Clark packets reviewed and integrated. Common corpus-only run:
  4,000 total, 3,610 passed, 390 failed, zero skips; per framework 1,759 conformance passes,
  188 unresolved required cases, six optional adapter debts, zero harness failures. OUTPUT 283
  compared/103 pending. Evidence: `/private/tmp/jint-clark-output14-integrated.log`.
- `f7ba5abad`, `4bbad90a2`: independently reviewed checkable-state design and root-wide ID-triggered
  form-owner reset correction. Shared native implementation waits for slot ownership handoff. The
  architecture task continues with numeric/date/time/range/color value-family design.
- `0e256ea0a`: independently reviewed XML DTD ID typing, source `93022b063`. Fresh common non-corpus
  suite passed 2,376/2,376 across net8/net10, no skips. Evidence:
  `/private/tmp/jint-xml-id-typing-integrated.log`. Finalized task archived and clean patch-equivalent
  worktree removal verified. X4b3 navigator completion dispatched from the integrated common branch.
- `6fcc117b1`: three exact Clark no-fetch alternatives independently source-reviewed and matched
  against their implementation. Common corpus verification is pending the next approved five-case
  parameter/conditional packet batch; no passing common result is claimed yet.
- X3b review identified empty strict Document validation, ordered namespace restoration, ancestor
  prefix shadowing and quadratic synthetic URI accounting. First three corrections re-reviewed clear;
  final counted-work correction `932ca2853` awaits recheck. H6c review requires `select` in the ordinary
  scope boundary to prevent generic recovery from popping across it. Both owners are correcting their
  features before integration. No runtime benchmark measurements or Browser cutover yet.
- `1afff3e0e`, `1f7a8ad6a`, `1b29605c1`, `3984145b8`: XML serializer plus all four
  independently cleared corrections integrated. Fresh common non-corpus tests pass 2,422/2,422,
  no skips, both frameworks. Evidence: `/private/tmp/jint-xml-serializer-integrated.log`.
  Task archived and clean patch-equivalent worktree removal verified.
- `d5f7b09d7`, `c41fbd796`: reviewed tokenizer context APIs and exhaustive split/permission
  regression follow-up. Common non-corpus tests pass 2,444/2,444, no skips, both frameworks.
  Evidence: `/private/tmp/jint-tokenizer-context-integrated.log`. Task archived, worktree removed.
- `54376445d`: reviewed round/mod/rem including device-dependent line-width and all numeric seams.
  Independent extreme-value and 10,000 finite-pair checks clear. Common non-corpus tests pass
  2,530/2,530, no skips; `/private/tmp/jint-stepped-math-integrated.log`. Task archived, worktree
  removed. Census is seven implemented math functions and fourteen pending; no timing claim.
- `3fa3bb878`: reviewed pure text-control algorithms (source `1dcdbd3df`), with independent
  exhaustive small-string and shared cancellation probes. `d30b99472`, `9b661bd6b` integrate
  independently approved five-case XML parameter/conditional expectations. Fresh common full run:
  6,594 total, 6,220 passed, 374 failed, no skips; all 2,594 non-corpus tests pass. Every failure is
  an XML corpus case or debt census. Per framework 1,767 conformance passes, 180 unresolved required
  cases, six optional adapter debts, 21 verified optional policies, zero harness failures/mismatches;
  OUTPUT 291 compared/95 pending and 24 no-fetch alternatives. Evidence:
  `/private/tmp/jint-text-algorithms-xml5-integrated.log`. Text task archived, worktree removed;
  unfinished XML task retained. Three further source policies (Clark 026/031, Sun ext02) approved.
- `883bf5a91`, `81b77f4d8`, `c2aafb652`: reviewed current select parsing and ordinary-scope fix.
  Common non-corpus tests pass 2,644/2,644, no skips; `/private/tmp/jint-select-parsing-integrated.log`.
  Task archived and worktree removal verified. H6d ordinary templates dispatched separately.
- `3c84d802c`: reviewed four built-in form-state selector dispatch contract. Dedicated Sol tasks
  are dispatched for C3a1, X4b3 navigator completion, V0b3a abs/sign and H6d ordinary templates;
  task IDs await coordinator retrieval. D7b1c textarea state and D7b2 checkable state still await
  slot-owner shared-file release. No public promotion or Browser cutover is implied.
- Slot review on `0a651e79e` requires exact attribute old/new guards and old/new slot signal order,
  correct insertion/removal targets, preservation of omitted nodes' stored assigned-slot pointers,
  linear manual transfer and fallback distribution, and polling through cold-index construction and
  final materialization. Deterministic probes exposed quadratic work and missing query checks;
  those commits remain unmerged while the owner corrects them.
- `73af97cfe`, `68214f80e`, `80d5cd5c3`: exact independently approved Clark 026/031 and Sun ext02
  expectations integrated. Common corpus-only run: 4,000 total, 3,632 passed, 368 failed, no skips.
  Per framework: 1,770 conformance passes, 177 unresolved required cases, six optional adapter debts,
  OUTPUT 294 compared/92 pending and 27 no-fetch alternatives, zero harness failures/mismatches.
  Evidence: `/private/tmp/jint-clark-sun-three-integrated.log`. Corpus task remains unfinished.
- `7b8bf2c91`: independently reviewed atan2 negative-axis source conflict and full special-value
  matrix recorded for V0b3b; the separate Log interpretation still needs review before V0b3c.
  D7b3 preparatory numeric/date/time/range/color design and corrected WebIDL conversion ordering are
  integrated as `3336e5571` and `38beb27d3`; remaining numeric policy decisions stay with the design task.
- `546d1719d`, `cd8921f49`: independently reviewed built-in form-state selectors and CSS abs/sign.
  Fresh common non-corpus tests pass 2,730/2,730 across both frameworks, no failures/skips:
  `/private/tmp/jint-form-states-sign-integrated.log`. Both tasks were idle and clean, exact source
  patch equivalence was verified, then tasks archived and worktrees removed. Math census is nine
  implemented functions and twelve pending; no measurements or Browser completion implied.
- `090281296`, `f888b246a`: thirteen exact independently source-reviewed Clark expectations.
  All 246 existing entries are unchanged. Common corpus run: 4,000 total, 3,658 passed, 342 failed,
  zero skips. Per framework: 1,783 conformance passes, 164 unresolved required cases, six optional
  adapter debts, 21 verified optional policies, zero harness failures/mismatches; OUTPUT 307
  compared/79 pending and 39 no-fetch alternatives. Evidence:
  `/private/tmp/jint-clark-thirteen-integrated.log`. Corpus task remains unfinished and retained.
- `c303091aa`: independently reviewed source-preserving var/env analysis design, including corrected
  comment-insensitive spread token adjacency. Separate Sol tasks now implement V0c1 and V0b3b.
  XPath parentless-attribute evaluation requires an owned compilation adapter; slot fixes remain
  under independent re-review. Neither incomplete task is eligible for cleanup.
- `89ab6fa62`: reviewed XPath evaluator amendment retains an internal truthful cursor and owns
  guarded compilation plus materialized native results. Implementation owner is addressing it;
  the previous skipped following-axis gate must become a real passing result before acceptance.
- `a98630fbb`, `3594f2e81`, `64dbebcde`, `2f57675d9`: sixteen source-reviewed Sun, Edinburgh and IBM
  expectations exactly match approved packets; all 259 previous entries remain unchanged. Common
  corpus run: 4,000 total, 3,690 passed, 310 failed, zero skips. Per framework: 1,799 conformance
  passes, 148 unresolved required cases, six optional adapter debts, 21 verified optional policies,
  zero harness failures/mismatches; OUTPUT 323 compared/63 pending, 52 no-fetch alternatives.
  Evidence: `/private/tmp/jint-xml-sixteen-integrated.log`. This is still a failing acceptance gate.
- Slot source `598de6522` fixes the reviewed semantic and quadratic-work findings, but query
  attribute scans and initial root-child enumeration still lack bounded polling. Exact-source
  probes and 1,000 independent mutation sequences isolate those two remaining blockers; owner is
  correcting them. The task/worktree remain retained and no native shared-file handoff occurred.

## Resumed integration, 2026-09-25

- `86eda440f`, `57e93f939`: the retained 21 IBM expectations exactly match approved packets; all275
  previous entries unchanged. Fresh common corpus: 4,000 total, 3,732 passed, 268 failed, no skips.
  Per TFM: 1,820 conformance passes, 127 unresolved required cases, six optional adapter debts,
  21 verified optional policies, zero harness failures/mismatches; OUTPUT344 compared/42 pending,
  52 no-fetch alternatives. Evidence: `/private/tmp/jint-resumed-xml-twentyone-integrated.log`.
- `c88366528`, `d5ddf18a0`, `1ddd8b2c5`, `243d41773`, `1c4c4cfbb`: full reviewed slot-assignment
  and signal chain. Final independent probes pass on both TFMs, including all previously missing
  cancellation checkpoints and 1,000 mutation sequences. Common non-corpus tests pass2,778/2,778;
  evidence `/private/tmp/jint-slots-integrated.log`. Clean/stopped/patch-equivalent task archived,
  worktree removal verified. Native shared-file ownership is released for the next coordinated slice.
- `93c9e8e84`: reviewed ordinary templates. Independent split/quota, ownership, observer and EOF
  cancellation probes pass both TFMs. Common non-corpus tests pass2,820/2,820, zero skips;
  evidence `/private/tmp/jint-templates-integrated.log`. Clean/stopped/patch-equivalent task archived,
  worktree removal verified. H6e framesets dispatched separately; foreign/fragment/patch work remains.
- Exact Japanese prepared-input design cleared for the existing corpus implementation owner.
  Log endpoint matrix cleared for V0b3c after trigonometry's integration; neither is an implementation
  completion claim. Numeric input policy still needs a standards-faithful extreme-domain revision.
