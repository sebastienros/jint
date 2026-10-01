# Native parser public API and integration work

The current CSS target is [LightPanda-style renderless text](https://github.com/sebastienros/jint/blob/main/Jint.HtmlParser/README.md#renderless-css-boundary).
This supersedes the former full-computed-CSS plan and its V0-V9/R3-R7/C6 grammar obligations.
Removing these features is an intentional behavior change, not deferred implementation debt.
Historical implementation measurements in git history do not describe the current engine.

## CSS boundary

| Area | Disposition |
| --- | --- |
| Syntax, selectors, media | Retained, including bounded work and existing selector specificity. |
| Style/media/supports/layer/import/font-face CSSOM | Retained; declarations and font descriptors store text. |
| Other at-rules | Generic CSSRule with source cssText; no cascade effects or specialized interfaces. |
| V0-V9 typed grammars, math, colors, transforms, all resets | Removed by design: parsing cost is not justified by renderless automation. Catalog names are data, not grammar obligations. |
| Keyframes, property registrations, container queries, pages/counters/namespaces | Semantic models removed by design; opaque rule text remains. |
| Computed values | Text cascade, inheritance and small defaults; synthetic width/height only. No unit/color computation. |
| Variables | Inherited custom text, with optional depth/size-bounded textual substitution in ordinary properties; no typed substitution graph. |
| Retained limitations | Nested conditional rules/interleaved declarations and import layer()/supports() still have explicit unsupported boundaries; stylesheet byte/MIME work remains below. |

UA/shadow/import handling, broader selectors/media conditions, basic synthetic flex geometry and textual
variables are deliberate conveniences beyond LightPanda. CSS parsing remains on demand.
Public XPath, HTML/XML serialization, syntax-only ParseCss and the native parser improvements remain
unchanged. No HTML tokenizer, DOM, XML or XPath work is part of this CSS reduction.

## Retained completed slices

- [x] **Owned public XPath**: `NativeXPath.Compile`, `Evaluate` and `Select`, for native nodes,
  attributes and namespace contexts; private prepared BCL expressions, typed immutable results,
  detached-following guard, namespace freshness, cancellation and identity preservation.
  Evidence: `NativeXPathEvaluationTests`, `NativeXPathNamespaceContextTests`,
  `NativeXPathIdentifierTests`, public API snapshots and the unsigned packed consumer.
- [x] **Public HTML/XML serialization**: all five `MarkupSerializer` routes, immutable options,
  output-limit taxonomy, cancellation, template/namespace behavior and native shadow selection.
  `Element.AttachShadow`, `OpenShadowRoot` and readonly root metadata provide real public identities;
  registry identities, Browser attachment policy and mutable parser hooks stay internal.
  Evidence: `MarkupSerializerTests`, the existing scalar/tree/shadow/work serializer fixtures,
  public API snapshots and the unsigned packed consumer.
- [x] **Existing whole-sheet syntax API reconciliation**: `ParseCss` and `CssStyleSheetSyntax`
  are included in the API snapshots and exercised by the packed consumer.
- [x] **Native acceptance corrections**: synchronous mutation completion for ARIA reflection,
  dataset and Selection; CDATASection branding; DOM ancestor/reference-child exception precedence;
  namespace-wildcard attribute-name casing; native inherited `:lang()` and Browser-backed `:dir()`.
  Language matching does not yet include document/HTTP language metadata fallback.
- [x] **Stylesheet cache acceptance**: reuse when unchanged and invalidation on child sheet edits,
  import insertion, resource arrival and import removal. No timing limit was raised.
- [x] **XML no-fetch and canonical-output acceptance**: all 101 pending reviews are resolved,
  including the internal-parameter-entity case with no external-resource indication. Every eligible
  canonical output is compared. DTD processing instructions have immutable public parse metadata,
  separate from DOM children, with exact data, order and original-input offsets.
  Evidence: `XmlConformanceTests`, `XmlCorpusTests`, `XmlDtdProcessingInstructionTests`,
  both public API snapshots and the unsigned packed consumer.
- [x] **Parser insertion reactions**: native parsing yields after potentially custom element insertion,
  before processing children, including `document.write` and reconstructed customized formatting.
  Browser drains reactions outside the entered parser; nested writes retain the active insertion point.
  Synchronous construction before attributes and constructor-failure semantics remain open.
  Evidence: `HtmlScriptHandoffTests`, `CustomElementUpgradeTests` and the pinned custom-element WPT cases.
- [x] **Cascade layers**: named/dotted/anonymous/nested ordering, reversed important precedence,
  ordinary-property rollback, CSSOM identity/mutation and import prologue handling remain.
  Custom property rollback keywords now remain text. Evidence: `CssLayerRuleTests`, `NativeCssLayerTests`.
- [x] **Selector observation scaling**: one mutation witness per document rather than candidate;
  mutation and detached-node adoption still invalidate reads. Deterministic witness-count and
  invalidation tests cover this. Evidence: `SelectorInteractionWorkTests`, `SelectorControlFactsTests`,
  `SwaggerFixtureTests`.
- [x] **Shadow stylesheet host selectors**: `:host`/`:host(...)`, featureless-host restrictions,
  predicates/combinators, specificity and encapsulation precedence, with live class/sheet invalidation.
  DOM queries do not acquire stylesheet host context. Evidence: `SelectorHostTests`, `CssSupportsTests`,
  `NativeCssHostTests`. `:host-context()` remains unsupported.

### Browser loading, selectors and parser integration

- [ ] **LOAD-01 - Stylesheet byte decoding.** Trace the current native stylesheet loading and
  import paths, then implement the specified BOM/transport/@charset/environment encoding choice.
  Done when a local-server fixture checks conflicting/absent labels, malformed sequences,
  imported-sheet encoding and cancellation; decoded-string CSS parsing remains unchanged.
  Progress: the parser owns CSS Syntax §3.2 fallback selection (`CssStyleSheetEncoding`) and drops
  `@charset` as a rule; Browser decodes `<link>` and `@import` bytes through the engine's WHATWG
  decoders with BOM override and parent-encoding inheritance (`CssStyleSheetDecoding`). Evidence:
  `CssStyleSheetEncodingTests`, `StyleSheetBytesAreDecodedWith…`. Malformed-sequence and
  cancellation fixtures remain.
- [ ] **LOAD-02 - Stylesheet eligibility and load ordering.** Implement MIME/nosniff and relevant
  response/origin eligibility at the loader boundary. Preserve URL/base metadata and distinguish
  rejected sheets from empty valid sheets.
  Done when local-server tests prove load/error events, failed imports, script/style ordering and
  cache invalidation without altering unrelated fetch policy. Coordinate with LOAD-01.
- [ ] **SEL-01 - Standalone intrinsic control facts.** Provide native producers for the default-
  submit, placeholder, editability, validity, range and directionality facts currently requiring
  `ISelectorControlFactsFactory`. Reuse native form/control state rather than Browser wrappers.
  Done when standalone matching works across input types, form owners, disabled/read-only states,
  mutation/adoption and cancellation, while absent interaction state means no focus/hover/active.
- [ ] **SEL-02 - Host-context and stylesheet pseudo-element audit.** Implement `:host-context()`
  with shadow-including ancestor matching and normal-context arguments in the existing iterative VM.
  Separately disposition stylesheet `::slotted()`/other accepted pseudo-elements versus DOM queries
  before claiming evaluator support. Done when scope, specificity, featureless-host restrictions,
  fallback content and encapsulation precedence have explicit fixtures; DOM queries never pierce roots.
- [ ] **SEL-03 - Language metadata fallback.** Complete document/HTTP language metadata fallback
  behind the existing inherited lang/xml:lang matcher. Done when missing, empty and conflicting
  language sources follow the standard and results do not depend on process locale.
- [ ] **HTML-01 - Parser custom-element construction remainder.** Complete synchronous construction
  before attributes/children and constructor-failure semantics over the existing parser handoff and
  reaction queue. Preserve the completed insertion-reaction timing fixes.
  Done when constructors that observe attributes, throw, reenter or use document.write have native/
  Browser/WPT evidence without adding another reaction channel or allowing two threads into the DOM.

## Public API dependencies still required

- [ ] **API-01 - Promote selectors.** After SEL-01 and the accepted-selector capability audit,
  expose the immutable program/context/specificity and parse/match/query/closest operations in
  [the selector contract](html-parser-selectors.md#exact-eventual-public-surface).
  Done when an unsigned consumer proves namespace copying, errors, specificity, current-tree
  results, cancellation and cross-document reuse without accessing Browser/friend-only state.
- [ ] **API-02 - Review public text declarations.** Define a small text-based declaration contract
  before promotion; typed values and ParseCssValue are outside the renderless boundary.
  External mutations must preserve identity, bounded work and atomicity.
- [ ] **API-03 - Promote mutable stylesheet/rule CSSOM.** After API-02, review and expose
  the useful sheet/rule/list operations without exposing loader/page internals.
  Done when an unsigned consumer covers every advertised rule kind, ownership, insertion/deletion,
  descriptor mutation and serialization; existing public `ParseCss` remains a syntax-only API.
- [ ] **API-04 - Review the standalone incremental HTML lifecycle.** Design the smallest public
  contract over `Html/TreeConstruction/HtmlParserSession`: input ownership, append/advance/finish,
  observable partial trees, reentrancy, disposal, limits and cancellation.
  Done when these decisions and signatures are reviewed with executable internal contract cases;
  Browser script handoff and arbitrary callbacks must not become public by convenience.
- [ ] **API-05 - Implement and publish incremental HTML.** Depends on API-04. Preserve tokenizer/
  tree-builder state across CRLF, surrogate, entity, tag/comment and raw-text chunk boundaries,
  with one operation-wide limit budget and well-defined EOF/failure behavior.
  Done when every boundary split matches one-shot parsing, cancellation/disposal cannot resume
  invalid state, and an unsigned consumer exercises the real lifecycle.
- [ ] **API-06 - Review byte/stream APIs as a separate phase.** Decide encoding sniff/restart,
  stream ownership, leave-open behavior, backpressure, partial-output and async cancellation
  before changing signatures or Browser transport.
  Done when the accepted contract and follow-on implementation tasks are explicit. If approved,
  implement them separately with multibyte/chunk/cancellation fixtures and proof that no script
  executes twice after a decode restart; string/incremental UTF-16 support does not close this gate.

Each public slice requires intentional API snapshots on net8.0/net10.0 and execution from the
[unsigned local-feed package consumer](https://github.com/sebastienros/jint/blob/main/tools/html-parser-package-consumer/README.md), without
friend access or a project reference. A separate native pack/run is required for AOT evidence.

## Acceptance still required

- [x] Resolve the XML external-resource/no-fetch reviews and all eligible canonical-output comparisons.
  The complete 2,585-row census has 1,947 passing runnable cases, 27 verified optional policies,
  593 outside-profile rows and 18 reviewed byte-boundary rows. All 386 eligible outputs are compared,
  including 66 reviewed no-fetch alternatives; unresolved cases, harness failures and known
  required-profile defects are zero. Missing-review, omission, notation, DTD PI and output corruption
  probes remain enforced.
- [x] Finish the Swagger fixture. Its logo renders and the real summary click expands the operation
  through the Try-it-out control on both frameworks, with its original task and wait budgets.
- [ ] **ACC-01 - Finish the real Scalar interaction.** The pre-trim fixture reaches the GetEndpoint group and GET operation,
  then fails with `System.TimeoutException: The operation's time budget elapsed.`
  after the GET operation click, in Scalar's `measure` / `onScrollChanged` callback through
  `getBoundingClientRect`, `PageLayout.ClientBoxOf`, `SizeQuery.HeightUpTo` and selector matching.
  Next: isolate and bound the repeated geometry/cascade work with a deterministic regression,
  then rerun the interaction; do not remove the scroll handler or increase the task/wait budgets.
  Framework-caught failures require bounded console/request/DOM diagnostics; the console snapshot
  retains both its first and last messages so later timing logs cannot hide the initial exception.
  The fixture now also retains the first 20 warning/error console records separately from timing
  logs, and corrects two stale accessible button names without weakening their exact-match checks.
  An empty `Page.Errors` list alone does not prove success. Budgets remain unchanged.
  Rerun `ScalarFixtureTests` after each newly exposed fix. Done when the fixture opens
  the GetEndpoint group/GET operation, opens Test Request's API Client dialog, observes the expected
  OpenAPI request and reports no unexpected request/page/console failures on both frameworks.
- [ ] **ACC-02 - Close named native Browser WPT debt.**
  The `document.write` custom-element connection timing case is fixed. Two newly passing
  ordinary-parser reaction exclusions were removed; passing rows cannot remain excluded.
  Assign each remaining NeedsTriage subtest/cause to a concrete fix and regression; retain justified
  capability categories without mislabeling them implemented. Done when no unnamed failure or
  harness error remains and every exclusion matches failing tests but no passing tests.
- [ ] **ACC-03 - Refresh the canonical Windows census.** After the relevant ACC-02 fixes, run the
  documented `JINT_WPT_BROWSER_CENSUS=1` check on Windows, review drift, then use `=update` only for
  justified reductions/equality refreshes. Commit both census/cause tables and refresh the inventory.
  Done when file/variant/registration totals and named causes reconcile, without raising failure
  ceilings or substituting macOS/Linux targeted results for the prescribed measurement.
- [ ] **ACC-04 - Implement equivalent CSSOM comparison workloads.** Extend the benchmark-only
  comparison infrastructure beyond `HtmlParserComparisonBenchmark`'s complete-tree oracle.
  Separate syntax parsing, text sheet/declaration parsing, mutation and CSSOM reads; compare only the
  retained semantics, not full computed CSS against raw text. Done when untimed structural/semantic comparison and deliberate corruption
  probes reject mismatches in values, priority, parentage, ranges and mutations before any timing.
- [ ] **ACC-05 - Complete paired performance acceptance.** Depends on ACC-04 and the representative
  feature gates. Agree workloads/tolerances before measurement, then run the repository's default
  configured jobs in gate mode with alternating paired arms and control rows.
  Use at least six rounds for small effects; report confidence intervals, allocations and separate
  retained-memory evidence. Done when no unresolved supported regression remains, or a concrete
  tradeoff is explicitly accepted. No `--job short` numbers or invented speedup target.
- [ ] **ACC-06 - External package and API acceptance.** At every public promotion and the final
  checkpoint, verify both API snapshots, pack to a fresh local feed, run unsigned net8.0/net10.0
  consumers and publish/run the Native AOT consumer.
  Done when package assets/dependencies/licenses are correct and each advertised API is exercised
  without signing, friend access, a project reference or a stale same-version package cache.
- [ ] **ACC-07 - Full integration and dependency closure.** After the feature gates, run complete
  native/Browser and relevant client/protocol fixtures on the supported frameworks, preserving
  Swagger, Scalar, XML comparisons and demand-parsing cache checks. Run dependency/binding staleness and
  inventory checks; keep AngleSharp only in comparison benchmarks.
  Done when remaining failures/skips have exact reviewed dispositions and the final evidence
  records commit, platform, framework, command and outcome rather than only aggregate pass counts.
- [ ] **ACC-08 - Close the overall tracker.** Reconcile the dependency inventory with the intentional text boundary and public
  API gate; link implementation evidence and accepted separate-phase decisions.
  Done only when all required items above are closed and ACC-01 through ACC-07 have current evidence.
  Targeted passing slices, dependency removal or this expanded plan cannot close the overall gate.

Routine checks are Release builds/tests, `ParserDependencyTests`, `DomBindingsStalenessTests`,
the public API snapshots, the packed consumer and the dependency inventory. Raw CSS values are intentional; do not misrepresent them as full computed-style conformance.
