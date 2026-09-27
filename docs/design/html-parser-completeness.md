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

Gaps and distribution above are grammar/CSSOM/computed-value work, not grid/flex layout completion.
Normal gap and line-width keywords remain keywords at computed-value time, per
[CSS Gaps 1](https://drafts.csswg.org/css-gaps-1/#column-row-gap). Percentage bases and normal's
multicolumn used value must not be replaced with guessed pixels.

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
| Open | V0 shared grammar | `all` reset; remaining advanced/relative colors; reference functions including typed `attr`, `if`, `inherit`, `ident`, `random-item` and custom functions; preserve real typed dependencies. |
| Open | V1 paint/decoration | Border/background/image/layer shorthands, outlines/shadows and remaining paint grammars. Existing colors/decoration/clip do not complete the group. |
| Open | V2 box/position | Logical dimensions/spacing/insets, anchors and remaining sizing grammar; retain percentage and writing-mode dependencies. |
| Open | V3 layout/containment | Grid lines/tracks/templates/auto-placement, columns, fragmentation and remaining containment. Content distribution and gaps are complete slices above. |
| Open | V4 typography | Font family/line-height/font shorthand and remaining writing/text/ruby/whitespace properties; descriptor contexts separate. |
| Open | V5 motion | Animation/transition lists, timing/ranges/timelines, perspective and remaining transform obligations. |
| Open | V6 interaction | Scroll/overscroll/snap/padding/margin, scrollbar, touch and selection families; image cursors and other named pending syntax. |
| Open | V7 SVG/replaced content | Fill/stroke/markers, masks/filters/clipping/images/object/shape grammar and SVG baseline properties. |
| Open | V8 generated/table/page | Content/counters/lists/quotes, table and paged-media values; contextual descriptors belong to R5. |
| Open | V9/context audit | Disposition every descriptor-only registration against real descriptor parsers; never admit arbitrary ordinary values. |
| Open | R1 prologue/imports | Namespaces and selector environments, import layer/supports modifiers and placement; explicit decoded-string charset policy. |
| Open | R2 groups/nesting | Layers/order, scope, starting-style, nested conditional selector contexts and interleaved declarations. |
| Open | R3 animation rules | Timeline-range keyframe selectors and reviewed aliases; classic keyframes already exist. |
| Open | R4 fonts | Feature-value maps, palette descriptors and remaining font-face descriptor obligations. |
| Open | R5 pages/counters | Page selectors/margin rules, counter descriptors, dependencies and whole-rule invalidity. |
| Open | R6 registrations | Property syntax/inherits/initial-value and independence checks; view-transition/position-try/color-profile descriptors. |
| Open | R7 legacy rules | Reviewed standards dispositions for document/viewport; named corrections, not silent removal. |
| Open | C6 computation | Advanced color/environment/font/container dependencies, height/non-px/scroll-state queries, positioned/SVG/reference-box/automatic-minimum used values. |
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

- [ ] Resolve the 127 previously pending XML external-resource/no-fetch review cases and the
  42 pending eligible canonical-output comparisons; rerun the census rather than exclude them.
- [ ] Reconcile the three native-sheet layout-cache expectations and the Scalar/Swagger fixture
  timeouts recorded during the dependency-removal baseline. Do not invert assertions without
  preserving actual invalidation evidence or widen timeout assertions to hide failures.
- [ ] Remeasure the current native Browser WPT census and finish its named debt.
- [ ] Complete equivalent CSSOM comparison workloads and the separately specified paired
  benchmark acceptance. No performance claim follows from correctness tests.
- [ ] Close all open grammar/API rows above. Targeted passing slices do not close this overall gate.

Routine checks are Release builds/tests, `ParserDependencyTests`, `DomBindingsStalenessTests`,
the public API snapshots, the packed consumer and the dependency inventory. Never accept an
unfinished grammar as valid raw text merely to make an acceptance run pass.

### Evidence for the completed slices

The current changes passed 5,290 parser CSS/serialization/shadow/XPath/public-API cases and 100
targeted Browser integration/binding/dependency cases, across net8.0 and net10.0. Both unsigned
packed consumers and the net10.0 osx-arm64 Native AOT publish/run passed. The inspected package
contains both framework assets and no package dependencies. The refreshed inventory reports
1,734 generated members and passes its six tooling tests. These are targeted acceptance results;
they do not replace the open full-suite, WPT and performance gates above.
