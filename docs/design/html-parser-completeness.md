# Native parser CSS and public API completion

This is the remaining-work tracker, not a claim that removing package references completed the
replacement. AngleSharp is permitted only in `Jint.Benchmark` for comparisons with `Jint.HtmlParser`.
The [CSSOM contract](html-parser-cssom.md), [standalone contract](html-parser.md#4-native-api-contract)
and focused serialization/XPath designs remain the acceptance definitions.

## Completed vertical slices

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
- [x] **C5/V3 content distribution**: `align-content`, `justify-content`, `place-content`;
  canonical baseline values, baseline omission default, overflow alignment and distribution grammar.
- [x] **C5/V3 gaps**: `gap`, `row-gap`, `column-gap`, and all three `grid-*` aliases;
  nonnegative typed lengths/percentages/math, normal and line-width keywords, pair expansion,
  cascade/priority, pending substitutions and canonical reconstruction.
- [x] **C6/B5 support for these six properties**: specified/computed values, font-relative units,
  nonnegative math clamping, inheritance, live invalidation, named and generic accessors,
  stylesheet declarations and `CSS.supports`. The new named accessors share real declaration
  storage; null removes, invalid assignments preserve the prior value, computed writes fail.
  Evidence: `ContentAlignmentAndGapTests`, `CssLayoutDeclarationTests`,
  `NativeCssBoxQueryTests`, `NativeCssContentAlignmentAndGapTests`.
- [x] **Existing whole-sheet syntax API reconciliation**: `ParseCss` and `CssStyleSheetSyntax`
  are included in the API snapshots and exercised by the packed consumer.
- [x] **SVG fill/stroke paint slice**: typed colors, `none`, `context-fill`/`context-stroke`,
  `url()`/`src()` references and optional color/none fallbacks, priority and pending substitutions.
  Browser resolves nonlocal URLs with its WHATWG parser against the declaring sheet or document;
  local fragments, empty URLs and unresolvable URLs keep the CSS Values 4 serialization.
  Inherited current-color dependencies remain live. No paint server is fetched or rendered.
  Advanced colors and URL modifiers retain explicit incomplete-grammar failures.
  Evidence: `CssPaintDeclarationTests`, `ColorGrammarTests`, `NativeCssQueryFactoryTests`,
  `NativeCssPaintTests`. Realm-free queries need an explicit URL resolver for nonlocal references.
- [x] **Native acceptance corrections**: synchronous mutation completion for ARIA reflection,
  dataset and Selection; CDATASection branding; DOM ancestor/reference-child exception precedence;
  namespace-wildcard attribute-name casing; native inherited `:lang()` and Browser-backed `:dir()`.
  Language matching does not yet include document/HTTP language metadata fallback.
- [x] **Stylesheet cache acceptance**: tests now require reuse when unchanged and invalidation on
  child sheet edits, import insertion, resource arrival and import removal. No timing limit was raised.
- [x] **XML no-fetch and canonical-output acceptance**: all 101 pending reviews are resolved,
  including the internal-parameter-entity case with no external-resource indication. Every eligible
  canonical output is compared. DTD processing instructions now have immutable public parse metadata,
  separate from DOM children, with exact data, order and original-input offsets.
  Evidence: `XmlConformanceTests`, `XmlCorpusTests`, `XmlDtdProcessingInstructionTests`,
  both public API snapshots and the unsigned packed consumer.
- [x] **Parser insertion reactions**: native parsing yields after potentially custom element insertion,
  before processing children, including `document.write` and reconstructed customized formatting.
  Browser drains reactions outside the entered parser; nested writes retain the active insertion point.
  The document-write WPT timing case and two formerly excluded attribute/connection checks now pass.
  Synchronous construction before attributes and constructor-failure semantics remain open.
  Evidence: `HtmlScriptHandoffTests`, `CustomElementUpgradeTests` and the pinned custom-element WPT cases.
- [x] **V0 `all` reset**: expands the known ordinary longhand/reset membership, excluding
  descriptors, aliases, `direction`, `unicode-bidi` and custom properties. CSS-wide values,
  priority, deferred substitutions, partial removal and shorthand reconstruction share declaration
  storage. Computed `all` is empty rather than demanding every pending value grammar.
  This does not implement the non-wide grammars of reset longhands or close the V9 context audit.
  Evidence: `CssAllDeclarationTests`, `NativeCssAllTests`.
- [x] **Clip references and background image references**: typed `url()`/`src()` values,
  clip geometry-box keywords and ordered `background-image` lists with `none`. Specified, named,
  generic and computed routes share validation, null removal, substitution and source-aware URL
  resolution; no referenced resource is fetched. Basic shapes, gradients, other image functions and
  URL modifiers retain named incomplete-grammar failures.
  Evidence: `CssClipPathDeclarationTests`, `CssImageDeclarationTests`,
  `NativeCssClipPathTests`, `NativeCssImageTests`, `NativeCssQueryFactoryTests`.
- [x] **R2 cascade layer blocks/statements**: named, dotted, anonymous and nested layer identities;
  first-occurrence ordering across sheets, origin/shadow separation, conditional ordering,
  reversed important precedence and ordinary/custom-property `revert-layer` rollback.
  `CSSLayerBlockRule` and `CSSLayerStatementRule` expose real identities, frozen name lists,
  parentage, live mutation and serialization. The `CSSGroupingRule` interface object is exposed.
  Leading layer statements preserve the import prologue; statements cannot split imports.
  Layered imports and layers nested in style rules still require their named R1/C2 work.
  The historical, nonstandard `CSSLayerRule` remains unavailable; it is not a brand for either
  standard layer rule. Evidence: `CssLayerRuleTests`, both `NativeCssLayerTests` fixtures.
- [x] **Selector observation scaling**: repeated matches retain one mutation witness per document,
  not per candidate. Mutation and detached-node adoption still invalidate the operation; ownerless
  nodes retain identity witnesses. Deterministic witness-count and invalidation tests cover the
  change. This removes Swagger's click-time stall without changing its task budget.
  Evidence: `SelectorInteractionWorkTests`, `SelectorControlFactsTests`, `SwaggerFixtureTests`.
- [x] **R6 primitive property-registration slice**: required descriptors, syntax alternatives and
  list multipliers, initial-value independence, typed numeric/color/string/identifier values,
  inheritance, substitution and custom-variable cycles. Registration does not change specified-value
  acceptance or `CSS.supports(property, value)`. Computed enumeration includes registered defaults
  and valid empty values, but excludes guaranteed-invalid custom values.
  Active registrations are document-global, including
  shadow sheets; media, imports, disabled sheets and rule removal participate in invalidation.
  `CSSPropertyRule` exposes readonly `name`, `syntax`, `inherits` and nullable `initialValue`,
  rather than the dormant nonstandard descriptor-mutation surface.
  Evidence: `CssPropertyRuleTests` and both `NativeCssPropertyRegistrationTests` fixtures.
  URL/image/transform registration values, advanced colors, ordinary-property dependency cycles
  and JavaScript registration remain open; this is not all of R6.
- [x] **Shadow stylesheet host selectors**: `:host` and `:host(...)`, featureless-host restrictions,
  logical predicates and combinators, argument specificity, and normal/important encapsulation
  precedence. DOM queries do not acquire stylesheet host context. Ordinary host features are
  visible only inside the functional argument; selector matching cannot escape into outer ancestors.
  Live class/sheet changes and layer rollback preserve the context boundary.
  Evidence: `SelectorHostTests`, `CssSupportsTests`, `NativeCssHostTests`.
  `:host-context()` remains an explicit unsupported predicate.

Gaps and distribution above are grammar/CSSOM/computed-value work, not grid/flex layout completion.
Normal gap and line-width keywords remain keywords at computed-value time, per
[CSS Gaps 1](https://drafts.csswg.org/css-gaps-1/#column-row-gap). Percentage bases and normal's
multicolumn used value must not be replaced with guessed pixels.

The registration slice follows the required-descriptor and independent-initial-value contract in
[published CSS Properties and Values API Level 1](https://www.w3.org/TR/css-properties-values-api-1/#at-property-rule),
which is the R6 contract below. The newer editor's draft changes descriptor defaults and permits
multiple names; those changes are not included or silently treated as this checkpoint's acceptance.

## CSS completion still required

The per-name source of truth is
[`CssPropertyCatalog.Obligations`](../../Jint.HtmlParser/Css/Values/Properties/CssPropertyCatalog.cs),
with 433 original registrations, plus `all` and standards additions. The registry contains completed
ordinary grammar entries, not a disposition for every descriptor/alias. Do not count descriptor-only
names as missing ordinary properties or mark an entire family complete from a few working values.

Every row needs valid/invalid fixtures, aliases and contexts, initial/inheritance metadata,
shorthand reset membership, serialization, pending-substitution behavior, atomic mutation and
Browser named/generic route evidence before it can close.

| Status | Contract group | Remaining completion criterion |
| --- | --- | --- |
| Open | V0 shared grammar | Remaining advanced/relative colors; reference functions including typed `attr`, `if`, `inherit`, `ident`, `random-item` and custom functions; preserve real typed dependencies. The `all` reset slice is complete above. |
| Open | V1 paint/decoration | Border/background/image/layer shorthands, outlines/shadows and remaining paint grammars. Existing colors/decoration/clip do not complete the group. |
| Open | V2 box/position | Logical dimensions/spacing/insets, anchors and remaining sizing grammar; retain percentage and writing-mode dependencies. |
| Open | V3 layout/containment | Grid lines/tracks/templates/auto-placement, columns, fragmentation and remaining containment. Content distribution and gaps are complete slices above. |
| Open | V4 typography | Font family/line-height/font shorthand and remaining writing/text/ruby/whitespace properties; descriptor contexts separate. |
| Open | V5 motion | Animation/transition lists, timing/ranges/timelines, perspective and remaining transform obligations. |
| Open | V6 interaction | Scroll/overscroll/snap/padding/margin, scrollbar, touch and selection families; image cursors and other named pending syntax. |
| Open | V7 SVG/replaced content | Paint URL modifiers, markers, masks/filters/clipping/images/object/shape grammar and SVG baseline properties. The fill/stroke slice above does not complete this family. |
| Open | V8 generated/table/page | Content/counters/lists/quotes, table and paged-media values; contextual descriptors belong to R5. |
| Open | V9/context audit | Disposition every descriptor-only registration against real descriptor parsers; never admit arbitrary ordinary values. |
| Open | R1 prologue/imports | Namespaces and selector environments, import layer/supports modifiers and placement; explicit decoded-string charset policy. |
| Open | R2 groups/nesting | Scope, starting-style, nested conditional selector contexts (including layers inside style rules) and interleaved declarations. Layer blocks/statements and their cascade order are complete slices above. |
| Open | R3 animation rules | Timeline-range keyframe selectors and reviewed aliases; classic keyframes already exist. |
| Open | R4 fonts | Feature-value maps, palette descriptors and remaining font-face descriptor obligations. |
| Open | R5 pages/counters | Page selectors/margin rules, counter descriptors, dependencies and whole-rule invalidity. |
| Open | R6 registrations | Complete URL/image/transform and advanced-color registration grammars, ordinary-property dependency cycles and JavaScript registration; view-transition/position-try/color-profile descriptors. Primitive descriptor/computation support is the completed slice above. |
| Open | R7 legacy rules | Reviewed standards dispositions for document/viewport; named corrections, not silent removal. |
| Open | C6 computation | Advanced color/environment/font/container dependencies, registered-property cycles through ordinary properties (currently `C6:registered-property-cycle`), height/non-px/scroll-state queries, positioned/SVG/reference-box/automatic-minimum used values. |
| Open | Browser CSS loading | Charset/BOM selection and MIME eligibility, with fixture-level evidence. |

## Public API dependencies still required

- [ ] **Selectors**: promote an immutable compiled program and query/match operations only after
  standalone intrinsic form/state facts work without Browser. Missing host interaction means no
  focus/hover/active target, not disabled selector parsing. Namespaces and limits are inputs, not
  feature switches.
- [ ] **Validated CSS declarations/values/CSSOM**: complete the C5/R1-R7 gates above before promotion;
  preserve syntax versus semantics and invalid versus unsupported versus deferred results.
  Mutable sheet/rule/declaration identities, mutations, parentage and serialization need external proof.
- [ ] **Incremental HTML**: finish a standalone contract over the existing internal session without
  publishing Browser script handoff, callbacks or state that can outlive its owner incorrectly.
- [ ] **Bytes/streams**: separately review encoding, partial-output and cancellation contracts.
  These are not implied by the implemented decoded-string entry points.

Each public slice requires intentional API snapshots on net8.0/net10.0 and execution from the
[unsigned local-feed package consumer](../../tools/html-parser-package-consumer/README.md), without
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
- [ ] Finish the Scalar fixture. Its 66 property registrations and document-level `:host` selectors
  no longer stop scrollbar measurement. It now fetches and processes the captured OpenAPI document,
  then reports `CSSStyleDeclaration.clip: Unimplemented CSS grammar: V7:clip`; navigation buttons
  still do not appear. Implement the missing clip grammar rather than discarding the declaration.
  Framework-caught failures require bounded console/request/DOM diagnostics; the console snapshot
  retains both its first and last messages so later timing logs cannot hide the initial exception.
  An empty `Page.Errors` list alone does not prove success. Budgets remain unchanged.
- [ ] Remeasure the current native Browser WPT census and finish its named debt.
  The `document.write` custom-element connection timing case is fixed. Two newly passing
  ordinary-parser reaction exclusions were removed; passing rows cannot remain excluded.
  The canonical numeric cause table still needs its prescribed Windows measurement.
- [ ] Complete equivalent CSSOM comparison workloads and the separately specified paired
  benchmark acceptance. No performance claim follows from correctness tests.
- [ ] Close all open grammar/API rows above. Targeted passing slices do not close this overall gate.

Routine checks are Release builds/tests, `ParserDependencyTests`, `DomBindingsStalenessTests`,
the public API snapshots, the packed consumer and the dependency inventory. Never accept an
unfinished grammar as valid raw text merely to make an acceptance run pass.

### Evidence for the completed slices

The public-API completion checkpoint passed 5,290 parser CSS/serialization/shadow/XPath/public-API cases and 100
targeted Browser integration/binding/dependency cases, across net8.0 and net10.0. Both unsigned
packed consumers and the net10.0 osx-arm64 Native AOT publish/run passed. After the optimization merge,
the package contains both framework assets, its `System.IO.Hashing` dependency and the imported
`ValueStringBuilder` MIT license. AngleSharp remains comparison-benchmark-only. The refreshed inventory reports
1,734 generated members and passes its six tooling tests. These are targeted acceptance results;
they do not replace the open full-suite, WPT and performance gates above.

The native acceptance/paint checkpoint runs 13,892 parser cases across net8.0/net10.0: 13,688 pass;
204 fail solely on the 101 unresolved XML cases and their zero-pending census, once per framework.
The XML profile counts 1,846 passing runnable cases, no known required-profile parser defects and
no harness failures; 344 of 386 eligible canonical outputs are compared.
The net10.0 WPT/native-CSS/Swagger run has 623 passes, two failures (the connection timing case
and Swagger's `clip-path` dependency), and two opt-in census skips. These figures are local evidence,
not a refreshed Windows WPT census or a claim that the overall completion gate has closed.

The subsequent XML completion checkpoint passes the complete parser suite on net8.0 and net10.0.
The former XML pending counts above are historical: the refreshed profile compares all 386 outputs
and has zero pending classifications or required-profile failures. Review expectations come from
the pinned document/resource bytes and XML productions, not captured native parser results.
The three IBM outputs containing DTD processing instructions now observe those instructions through
the public parse inventory; they no longer disappear at the DOM boundary.

The XML/insertion-reaction checkpoint passes 13,944 parser cases across net8.0/net10.0 and
1,142 Browser custom-element, WPT, binding-staleness and dependency cases. The four skipped cases
are the two opt-in Windows census checks on each framework; no ordinary case fails or is newly
excluded. Both freshly packed unsigned consumers and the net10.0 osx-arm64 Native AOT consumer pass.
The dependency inventory and its six tooling tests pass; only reviewed exclusion-source drift changed
the lock, not its historical Windows census. This checkpoint does not close the remaining CSS,
public API, application-fixture or benchmark obligations.

The reset/reference/layer checkpoint passes all 14,150 native parser cases across net8.0/net10.0.
The broader Browser native-CSS, Swagger, binding and pinned WPT selection passes 1,304 cases;
the four skipped cases are the two opt-in census checks on each framework. Final native-CSS,
Swagger and dependency checks pass 352 cases, and the layer/interface/default-argument check
passes 38 cases. Both freshly packed unsigned consumers and the net10.0 osx-arm64 Native AOT
consumer pass. The inventory and its six tooling checks pass with 1,739 generated members and
166 interfaces; its historical Windows WPT table is unchanged. Scalar remains a failing acceptance
fixture at the then-unsupported registration boundary. None of these results closes the remaining
grammar/API rows or substitutes for paired benchmarks and the prescribed Windows census.

The registration/host-selector checkpoint passes all 14,390 native parser cases across
net8.0/net10.0. The broader Browser native-CSS, Swagger, binding, dependency and pinned WPT selection
passes 1,310 cases, with four skips for the two opt-in census checks on each framework.
Both freshly packed unsigned consumers and the net10.0 osx-arm64 Native AOT consumer pass;
the package retains both framework assets, only the `System.IO.Hashing` dependency and the imported
MIT license. The inventory and its six tooling checks pass with 1,737 generated members and
166 interfaces; the historical Windows WPT table is unchanged. Scalar fails on `V7:clip` on both
frameworks under its original budgets. The overall grammar/API, Scalar, Windows census and paired
benchmark completion gates remain open.
