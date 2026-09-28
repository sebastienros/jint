# Native parser CSS and public API completion

This is the remaining-work tracker, not a claim that removing package references completed the
replacement. AngleSharp is permitted only in `Jint.Benchmark` for comparisons with `Jint.HtmlParser`.
The [CSSOM contract](html-parser-cssom.md), [standalone contract](html-parser.md#4-native-api-contract)
and focused serialization/XPath designs remain the acceptance definitions.

The latest implemented baseline is `eddc69994` (primitive registrations and shadow host selectors).
The checkboxes below track remaining work, not a claim that writing this plan completed it.
Historical measurements remain at the end; the older `html-parser-progress.md` is a chronological
record, not the current backlog.

## Tracking and completion rules

Task IDs in this document are stable and local to this backlog. `V*` and `R*` refer to the CSSOM
contract's value/rule families, not the older architecture document's runtime task IDs. Keep an
unfinished item unchecked; record its blocker and next concrete action rather than changing its
definition of done. Split a task further with suffixed IDs if its implementation cannot be reviewed
coherently, preserving the parent goal and dependencies.

Every implementation needs a specification citation, positive and negative cases, exact serialization
expectations and cancellation/mutation coverage for the surfaces it changes. A grammar task must
preserve typed dependencies, not convert unresolved input into guessed pixels or accept raw text.
Its declaration gate includes CSS-wide keywords, ranges, aliases, shorthand/reset membership,
pending substitutions, invalid-at-computed-value behavior, and Browser named/generic agreement.
Rule tasks also cover insertion hierarchy, readonly versus mutable descriptors, brands, parentage,
detachment, atomic edits and DevTools ranges into the serialized text. Do not copy this checklist
into each task or treat a parser-only test as proof of Browser integration.

When checking an item off, append the implementation commit, test names/commands and the remaining
boundary to its entry or to a linked completed slice. Update the family roll-up only when all its
assigned names and tasks have dispositions; a standards-based rejection requires a reviewed
specification/fixture explanation, not an unsupported-feature success fallback.

**Recommended order:** implement `V7-01` and rerun `ACC-01` first; then finish the registration gaps
`R6-01` through `R6-03`. Shared-value, rule and intrinsic-selector tasks can proceed independently
where their named dependencies permit. Public CSS promotion waits for its grammar gates.
Byte/stream API review is a separate phase, not a reason to delay decoded-string fixes.

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

| Status | Contract group | Task IDs | Remaining completion criterion |
| --- | --- | --- | --- |
| Open | Coverage inventory | INV-01 | Assign every catalog name, context, alias and partial grammar to a task or completed fixture. |
| Open | V0 shared grammar | V0-01 through V0-05 | Advanced/relative colors, math follow-ups and typed reference functions. The `all` reset is complete. |
| Open | V1 paint/decoration | V1-01 through V1-05 | Borders, image/layer shorthands, backgrounds, outlines/shadows and remaining paint values. |
| Open | V2 box/position | V2-01 through V2-03 | Logical dimensions/spacing/insets, anchors and remaining sizing grammar. |
| Open | V3 layout/containment | V3-01 through V3-04 | Grid tracks/templates, columns, fragmentation and remaining containment. Distribution and gaps are complete. |
| Open | V4 typography | V4-01 through V4-04 | Fonts, line-height and remaining writing/text/ruby/whitespace properties. |
| Open | V5 motion | V5-01 through V5-04 | Timing functions, animation/transition lists, timelines and remaining transforms. |
| Open | V6 interaction | V6-01 through V6-04 | Scroll/overscroll/snap, scrollbar/touch/selection and image cursors. |
| Open | V7 SVG/replaced content | V7-01 through V7-05 | Legacy clip, shapes, masks/filters, remaining SVG paint and image/object grammar. |
| Open | V8 generated/table/page | V8-01 through V8-03 | Content/counters/lists/quotes, tables and paged-media values. |
| Open | V9/context audit | V9-01 | Descriptor-only registrations stay in their real descriptor contexts. |
| Open | R1 prologue/imports | R1-01 through R1-03 | Namespace environments, layered/conditional imports and decoded-string charset policy. |
| Open | R2 groups/nesting | R2-01 through R2-03 | Nested conditions/interleaved declarations, scope and starting-style. |
| Open | R3 animation rules | R3-01 | Timeline-range keyframe selectors and reviewed aliases. |
| Open | R4 fonts | R4-01 through R4-03 | Remaining font-face descriptors, feature maps and palettes. |
| Open | R5 pages/counters | R5-01, R5-02 | Page/margin rules and complete counter-style descriptors. |
| Open | R6 registrations | R6-01 through R6-07 | Remaining types/cycles/JS registration, edition review and special descriptor rules. |
| Open | R7 legacy rules | R7-01 | Reviewed standards dispositions for document/viewport. |
| Open | C6 computation | C6-01 through C6-05; R6-02 | Missing environment/container/used-value dependencies and invalidation. |
| Open | Browser CSS loading | LOAD-01, LOAD-02 | Charset/BOM selection and MIME eligibility with real load-order evidence. |

## CSS implementation tasks

### Inventory and implementation entry points

- [ ] **INV-01 - Make the remaining-name inventory checkable.** Reconcile
  `CssPropertyCatalog.Obligations`, `CssPropertyRegistry.Completed` in
  `Css/Values/Properties/CssPropertyMetadata.cs`, descriptor catalogs and
  `tools/dom-bindings/contract.json`. Assign every original name plus standards additions to a
  task/completed slice, permitted contexts and exact alias target. Include partial productions
  inside registered grammars, not just names missing from `Completed`.
  Done when a coverage test rejects unassigned names, duplicate ownership, accidental ordinary
  descriptors and bindings with no reviewed implementation/disposition.

Unless noted otherwise, value implementations belong in `Jint.HtmlParser/Css/Values/` and reuse
the existing syntax, primitive, math and reference parsers. Wire grammar dispatch/metadata through
`CssPropertyParser` and `CssPropertyMetadata`, storage through `CssDeclarationBlock`, computation
through `Jint.Browser/Styling/NativeCss*`, and adapters through the existing declaration bindings.
Regenerate the lookup tables and DOM bindings when their inputs change; do not edit generated C#.
The names below identify bounded work packages; `INV-01` supplies the exhaustive per-name mapping.

### V0 - Shared values

- [ ] **V0-01 - Absolute advanced colors.** Extend `CssColorParser`, the immutable color model
  and serializers for pending Lab/LCH/OKLab/OKLCH and non-sRGB `color()` spaces. Preserve missing
  channels, alpha, out-of-range values and math until the specified computation/clamping step.
  Done when declarations, registered colors and computed styles agree on conversion and
  serialization, with invalid channel/space/arity cases rejected.
- [ ] **V0-02 - Relative and contextual color functions.** Implement relative `from` syntax,
  channel references and the adopted color-mixing/contextual productions; inventory the exact
  functions against the color contract before adding them. Reuse V0-01's spaces rather than
  string-rewriting to RGB. Done when currentColor/system-color dependencies remain live,
  missing-component/interpolation rules are covered and unsupported profiles stay distinguishable.
- [ ] **V0-03 - Remaining math productions.** Reconcile the pending cases in
  [the math follow-ups](html-parser-css-math-followups.md) with current `CssMathParser` support.
  Implement each remaining typed function and dimension/percentage interaction; preserve integer
  rounding, range checks and dependency metadata. Done when the old pending cases have exact
  valid/invalid/computed expectations, including cancellation and bounded deep expressions.
- [ ] **V0-04 - Typed `attr()` substitution.** Extend the existing reference analysis/execution
  pipeline with attribute lookup, type/unit conversion, fallback and attribute-taint rules.
  Keep DOM reads in invocation-owned producers, not shared programs or frozen snapshots.
  Done when missing versus empty attributes, inheritance, URL restrictions and live attribute
  mutations produce the standard computed result without retaining a document in parsed values.
- [ ] **V0-05 - Remaining reference-function families.** Give `if`, `inherit`, `ident`,
  `random-item` and custom functions separate grammar/evaluation subcases under the
  [substitution contract](html-parser-css-substitution.md). Define branch evaluation,
  evaluation-lifetime rules, fallback/cycle edges and required host inputs before implementation.
  Done when each recognized pending family has implemented semantics or a reviewed scope
  disposition; unknown functions must not become successful arbitrary values.

### V1 - Paint and decoration

- [ ] **V1-01 - Borders, radii and outlines.** Implement physical/logical border width/style/color
  longhands, corner radii and outline values, then their shorthands. Reuse color/numeric values;
  specify omitted-component defaults, radius slash syntax and border-image reset-only membership.
  Done when mixed shorthand/longhand priority, invalid atomic assignments and logical aliases agree
  through specified and computed routes.
- [ ] **V1-02 - Shared image and gradient grammar.** Extend `CssImagePropertyParser` beyond
  `none` and URL lists with the adopted gradient/image functions, stops, color spaces and URL
  modifiers. Reuse V0 color work where required. Done when typed images can be shared by background,
  border-image, masks and cursors; URL computation remains source-aware and parsing never fetches.
- [ ] **V1-03 - Background layers and shorthand.** Implement attachment, origin, position axes,
  repeat axes and size, then `background` using V1-02. Preserve the existing background-clip/image
  implementations. Done when comma layers, slash sizes, final-layer-only color, list lengths and
  shorthand resets serialize correctly before and after substitution.
- [ ] **V1-04 - Border images and shadows.** Implement border-image source/slice/width/outset/repeat
  and shorthand, plus box-shadow; share the shadow-value primitives needed by text-shadow.
  Depends on V1-01/V1-02 as appropriate. Done when fill/inset, negative/ranged lengths, color defaults
  and ordered lists are validated without implying image loading or painting.
- [ ] **V1-05 - Remaining paint controls.** Cover appearance, accent/caret colors, color-scheme,
  forced/print-color adjustment, blending/isolation and box-decoration-break from INV-01.
  Done when keyword sets, contextual color dependencies and inherited/initial values have
  named/generic evidence; retain a distinction between CSSOM support and actual rendering behavior.

### V2 - Boxes and positioning

- [ ] **V2-01 - Logical sizes, spacing and insets.** Add block/inline min/max dimensions,
  margin/padding logical pairs and inset shorthands using the existing physical parsers.
  Done when writing-mode/direction select the correct physical side, conflicting logical/physical
  declarations obey cascade order, and percentages remain unresolved until their real basis exists.
- [ ] **V2-02 - Remaining non-anchor box values.** Implement aspect-ratio, float/clear and pending
  sizing productions such as `contain`, using `CssSizingPropertyParser` rather than a second
  dimension parser. Done when ratios, zero/negative cases, auto/intrinsic combinations and
  min/max behavior have typed results; used-layout work is explicitly assigned to C6-04.
- [ ] **V2-03 - Anchor and position-try inputs.** Implement anchor names/scopes, `anchor()`/
  `anchor-size()`, position-area/visibility and position-try lists/order, including the existing
  `alignment:anchor-center` pending branch. Done when fallback and typed geometry dependencies
  survive parsing/substitution; R6-06 supplies rule descriptors and C6-04 supplies actual geometry.

### V3 - Layout and containment values

- [ ] **V3-01 - Grid longhands.** Implement line names/numbers/spans, template areas,
  track lists, `repeat`/`minmax`/`fit-content`, auto tracks/flow and adopted subgrid syntax.
  Done when duplicate/escaped names, rectangular-area validation, range restrictions and nested
  functions have exact parse/serialization tests. This task describes tracks, not a grid renderer.
- [ ] **V3-02 - Grid shorthands.** Build grid-row/column/area, grid-template and grid over V3-01.
  Done when slash branches, omitted line defaults, auto-flow alternatives, reset-only longhands
  and pending shorthand substitution preserve cascade order and reconstruct canonically.
- [ ] **V3-03 - Multicolumn values.** Implement column width/count/fill/span, column-rule and
  columns shorthand, reusing completed gaps and V1 border primitives.
  Done when auto/positive-integer/length alternatives, omitted components and computed defaults
  are consistent without claiming multicolumn used layout.
- [ ] **V3-04 - Containment, fragmentation and remaining layout controls.** Implement contain,
  contain-intrinsic sizes, content-visibility, order and break-before/after/inside; map page-break
  aliases with V8-03. Done when keyword combinations, remembered-size dependencies and aliases
  have explicit semantics, and current display/flex/alignment support remains unchanged.

### V4 - Typography and text

- [ ] **V4-01 - Font primitives and line-height.** Add family lists, style/stretch and line-height;
  reuse existing font-size/font-weight and the descriptor parser's lexical helpers, not its context
  defaults. Done when quoted/multiword/generic families, oblique ranges, unitless line-height
  inheritance, percentages and relative lengths have correct specified/computed distinctions.
- [ ] **V4-02 - Font shorthand and font feature controls.** Implement font shorthand reset
  membership, system-font dependencies, variants/synthesis, feature/variation settings, kerning,
  optical sizing, palette and font-size-adjust controls from INV-01. Depends on V4-01.
  Done when omitted/reset-only values and descriptor-versus-ordinary differences are covered;
  no host font data is invented to make system-font computation succeed.
- [ ] **V4-03 - Text spacing, breaking and wrapping.** Implement remaining letter/word spacing,
  indentation, tab size, word/line breaking, hyphenation, overflow and wrap-style/text-wrap values.
  Preserve completed white-space and text-align behavior, including their aliases.
  Done when lists/strings/keywords/ranges and shorthand conflicts are validated; line layout is
  not inferred from grammar support.
- [ ] **V4-04 - Remaining inline, ruby and decoration controls.** Cover unicode-bidi, vertical-align,
  ruby properties, initial-letter, hanging punctuation, underline offsets/positions/skip-ink,
  text-transform/justify and text-shadow; finish the alignment-string/MathML-size pending cases.
  Reuse V1-04 shadow primitives. Done when each assigned name and contextual dependency has a
  disposition and inherited behavior is not confused with decoration propagation.

### V5 - Motion values

- [ ] **V5-01 - Timing functions.** Implement the adopted easing grammar, including cubic-bezier,
  steps and linear stop lists. Done when control-point ranges, jump modes, duplicate/omitted stops
  and canonical forms are validated by shared immutable values.
- [ ] **V5-02 - Animation and transition lists/shorthands.** Add names/properties, durations/delays,
  iteration counts, direction/fill/play/composition and timing lists, then the shorthands.
  Depends on V5-01. Done when ambiguous identifiers, negative-delay versus duration rules, list
  repetition and reset membership are tested; passive CSSOM support does not claim animation execution.
- [ ] **V5-03 - Remaining transform metadata.** Add transform/perspective origins, perspective,
  transform-style and backface-visibility; reconcile remaining obligations with the already
  implemented transform list and individual transforms. Done when origin defaults, dimensionality
  and reference-box dependencies remain typed and connect to C6-04 rather than guessed matrices.
- [ ] **V5-04 - Timelines and transition naming.** Implement animation ranges/timelines,
  view-transition names/classes and will-change, including their name restrictions and reset rules.
  Done when range endpoints and named/functional timelines round-trip and unsupported execution
  remains explicit. Coordinate timeline-range selectors with R3-01.

### V6 - Scrolling and interaction

- [ ] **V6-01 - Scroll margins, padding and overscroll.** Implement physical/logical scroll edges
  and shorthands, overscroll pairs and scroll-behavior. Reuse V2-01's logical mapping.
  Done when auto, sign/range differences and writing-mode-dependent axes are correct; do not
  implement physical scrolling by changing declaration parsing.
- [ ] **V6-02 - Snap and overflow extensions.** Implement scroll-snap type/stop and assigned snap
  values, overflow-anchor/clip-margin/wrap and their reviewed aliases.
  Done when axes/strictness/box-length combinations and computed defaults are validated without
  disturbing the existing overflow-axis coupling.
- [ ] **V6-03 - Scrollbar, touch and selection values.** Implement scrollbar color/width/gutter,
  touch-action, user-select and resize. Route legacy scrollbar color names through the INV-01/V9-01
  standards audit instead of accepting everything in the old catalog.
  Done when keyword combinations and interaction-policy versus CSSOM behavior are documented.
- [ ] **V6-04 - Image cursors.** Replace `V6:cursor-images` with URL/image candidates, hotspot
  validation and mandatory keyword fallback, reusing V1-02 and source-aware URLs.
  Done when lists, invalid/missing fallback, substitution and computed URLs work without fetching
  or presenting a cursor.

### V7 - SVG, clipping and replaced content

- [ ] **V7-01 - Legacy `clip: rect(...)` (next Scalar blocker).** Add `clip` metadata and a typed
  rectangle grammar for `auto` or four length/auto edges, including the standard legacy separator
  forms. Wire named `style.clip`, generic operations, substitution and computed serialization.
  Done when `rect(0, 0, 0, 0)`, mixed auto/relative/negative lengths and malformed arity/unit cases
  have regression coverage, then rerun ACC-01. Do not implement this as clip-path or a raw string.
- [ ] **V7-02 - Basic shapes and shape dependencies.** Extend clip-path beyond references/boxes
  and implement shape-outside/margin/image-threshold using the adopted inset/circle/ellipse/polygon/
  path productions. Done when fill rules, positions, reference boxes and percentages retain typed
  dependencies; non-rendering CSSOM results must not pretend to be a clipping engine.
- [ ] **V7-03 - Masks and filters.** Implement mask layers/borders, filter/backdrop-filter lists,
  references and function-specific ranges, reusing V1-02 images and V1-04 border-image helpers.
  Done when layer expansion/reset semantics, function order and source-relative URLs are correct
  and no resource is fetched merely to parse or serialize.
- [ ] **V7-04 - Remaining SVG paint/stroke/baseline values.** Complete opacity/rule/line/dash/miter/
  width values, marker references, interpolation/rendering controls and baseline properties.
  Preserve completed fill/stroke behavior and share URL-modifier work with V1-02.
  Done when nonnegative lists, unitless SVG lengths, keyword applicability and inheritance have
  exact fixtures; geometry-specific computation belongs to C6-04.
- [ ] **V7-05 - Image and object placement.** Implement object-fit/position, image orientation/
  rendering and remaining assigned replaced-content values. Reuse position and image primitives.
  Done when two-/four-component positions, angle/flip combinations and intrinsic-size dependencies
  are represented correctly, without treating absent image dimensions as known.

### V8/V9 - Generated content, pages and contexts

- [ ] **V8-01 - Generated content and counters.** Implement content lists, counter reset/set/
  increment, quotes and related string/counter functions with typed values and case-sensitive names.
  Done when optional integers, reversed counters, quote pairs, alt text and reference substitution
  follow their grammars; counter-style lookup depends on R5-02, not an unvalidated name fallback.
- [ ] **V8-02 - Lists and tables.** Implement list-style type/image/position and shorthand,
  border-collapse/spacing, caption-side, empty-cells and table-layout.
  Depends on shared images and counter styles where applicable. Done when resets, spacing arity,
  custom names and initial/inherited values have Browser CSSOM evidence.
- [ ] **V8-03 - Paged-media and legacy value dispositions.** Implement the adopted orphans/widows,
  page-break aliases and page-related values; review bookmark, footnote, running and string-set
  obligations against their specifications. Done when every catalog name has either implemented
  grammar or an explicit reviewed rejection, with page/margin context delegated to R5-01.
- [ ] **V9-01 - Context and alias closure.** Give `ascent-override`, `descent-override`, `font-display`,
  `line-gap-override`, `size-adjust`, `src` and `unicode-range` their descriptor-only dispositions.
  Reconcile all remaining ordinary/descriptor overlaps and legacy aliases from INV-01.
  Done when wrong-context writes/removals are rejected, correct contexts reach real parsers and
  neither `all` nor a generic declaration path admits a descriptor as an ordinary longhand.

### R1/R2 - Stylesheet structure and selector context

Rule work extends `Css/Model/CssStyleSheet.cs`, the existing rule hierarchy and
`Css/Serialization/CssRuleSerializer.cs`; it does not fork the syntax parser.

- [ ] **R1-01 - Namespace rules and compilation environments.** Add validated namespace preludes,
  prefix/default bindings and prologue ordering; pass the effective immutable environment to style
  selector compilation and CSSOM selector edits. Done when unresolved prefixes, default namespaces,
  attribute namespaces and insert/delete/replacement invalidation have native and Browser evidence.
- [ ] **R1-02 - Layered and supports-qualified imports.** Parse import modifiers in their legal
  order and connect imported rules to their declared layer/supports condition.
  Reuse `ApplicableRules`, layer identities and `NativeCssStyleSheets.Imports`.
  Done when unsupported conditions, duplicate/nested imports, asynchronous arrival, removal and
  important/revert-layer ordering work without losing imported source URLs.
- [ ] **R1-03 - Decoded-string charset disposition.** Specify and test `@charset` placement,
  syntax retention and absence of an active CSSCharsetRule in validated CSSOM.
  Done when decoded-string entry points never restart decoding and the historical binding
  difference is recorded. Actual byte selection belongs to LOAD-01.
- [ ] **R2-01 - Nested conditions and interleaved declarations.** Remove the named
  `C2:nesting-selector-context`/`C2:interleaved-declarations` boundaries by preserving declaration
  runs and their order around nested rules, including layers inside style rules.
  Done when `&`, parent-list specificity, nested conditions, CSSOM edits and serialized ranges
  remain correct without flattening away identities or using recursive unbounded traversal.
- [ ] **R2-02 - Scope rules and cascade proximity.** Implement scope start/end selector grammar,
  implicit roots, scoped matching and proximity as a separate cascade criterion.
  Depends on R1-01/R2-01 where nested environments require them. Done when nested scopes, limits,
  `:scope`, specificity versus proximity and shadow boundaries have competing-rule fixtures.
- [ ] **R2-03 - Starting-style rules.** Add allowed standalone/nested rule forms and CSSOM
  identity; keep starting-style declarations out of the ordinary after-change cascade.
  Done when parsing/mutation and before-/after-change participation have explicit tests.
  If transition execution is absent, document the boundary rather than applying these rules always.

### R3/R4/R5 - Keyframes, fonts, pages and counters

- [ ] **R3-01 - Keyframe range selectors and aliases.** Extend classic keyframe parsing,
  serialization and find/delete matching with adopted timeline-range selectors; audit vendor
  aliases against the contract. Depends on V5-04's shared range vocabulary.
  Done when mixed selector lists, invalid ranges and last-match mutation preserve classic behavior.
- [ ] **R4-01 - Finish font-face descriptors.** Extend `CssFontFaceDescriptorCatalog` and parser
  for width/stretch, unicode-range, features/variations/named instance, language override,
  metric overrides, size-adjust and variants. Reuse already supported family/src/display/weight/style.
  Done when ranges, Unicode wildcards, source-list validity and ordinary-versus-descriptor
  acceptance have exact fixtures; a font-face rule does not itself promise font fetching.
- [ ] **R4-02 - Font feature-value maps.** Implement family preludes and typed named maps for
  stylistic/styleset/character-variant/swash/ornaments/annotation and adopted extensions.
  Done when map-specific integer arities, duplicate names, mutable CSSOM map operations and
  nested serialization are validated independently of ordinary declarations.
- [ ] **R4-03 - Font palette values.** Implement name/family, base-palette and override-colors
  descriptors, sharing color grammar with V0. Done when index ranges, allowed colors, descriptor
  replacement and CSSFontPaletteValuesRule's actual standard surface are covered.
- [ ] **R5-01 - Page selectors and margin rules.** Implement named/pseudo page selectors,
  all permitted margin-box rules and page/margin descriptor allowlists, sharing V8 value grammars.
  Done when contextual declarations, placement, invalid whole-rule recovery and CSSOM parentage
  work; no pagination/layout capability is inferred.
- [ ] **R5-02 - Counter styles.** Implement system/symbol/additive-symbol, range/pad/negative,
  prefix/suffix/fallback/speak-as descriptors and their cross-descriptor validity.
  Done when required combinations, ordered weights, fallback/extends dependencies and cycles
  are handled without accepting an invalid whole rule or breaking list-style references.

### R6/R7 - Registration completion and special rules

- [ ] **R6-01 - Remaining registered value types.** Replace `R6:property:<url>`, `<image>`,
  `<transform-function>` and `<transform-list>` boundaries in `CssRegisteredSyntax`.
  Reuse existing URL/transform parsers and V1-02 images; advanced colors depend on V0-01/V0-02.
  Done when alternatives, list multipliers, computational independence, computed serialization and
  inherited source-relative URLs work in both registration fixtures. Do not reimplement primitives.
- [ ] **R6-02 - Registered/ordinary-property dependency cycles.** Replace
  `C6:registered-property-cycle` with the specified dependency-graph invalidation/defaulting behavior.
  Extend the invocation-owned resolver/active-dependency machinery for edges such as
  `--x:1em; font-size:var(--x)` and `--x:currentColor; color:var(--x)`.
  Done when direct/indirect cycles, parent scopes and mixed registered/unregistered variables
  compute the required defaults, without recursion overflow or changing unrelated container errors.
- [ ] **R6-03 - JavaScript registration.** Add `CSS.registerProperty` to the CSS namespace through
  shared registration validation and document-owned storage, not a second grammar registry.
  Implement dictionary conversion, exception precedence, duplicate-name handling, precedence
  against stylesheet registrations and query invalidation.
  Done when independent pages/navigation, failed atomic registration, inheritance and live CSSOM
  all agree; no registration state may leak through a static dictionary or parsed program.
- [ ] **R6-04 - Resolve the specification-edition delta.** Compare the published Level 1 contract
  used here with the editor's-draft descriptor defaults/multiple-name changes and relevant WPT.
  Produce an explicit retain-or-adopt decision and, if adopting, implement its grammar, CSSOM and
  compatibility changes with named tests. Done means a documented scope decision, not an implicit
  claim that the existing implementation already follows both editions.
- [ ] **R6-05 - View-transition descriptors.** Implement navigation/types, rule validity and the
  standard readonly/mutable CSSOM surface. Coordinate names with V5-04.
  Done when allowed contexts and descriptor mutation are tested without claiming visual transitions.
- [ ] **R6-06 - Position-try rules.** Implement custom names and the permitted descriptor subset,
  reusing V2-03's position vocabulary. Done when forbidden ordinary declarations, descriptor
  defaults, rule lookup and live replacement are covered; geometry remains C6-04's responsibility.
- [ ] **R6-07 - Color-profile rules.** Implement profile names, src/rendering-intent and the adopted
  descriptor set; connect profile references to typed color dependencies.
  Done when contextual validation and source URL serialization work; unavailable profile data is
  not silently treated as sRGB or fetched by the parser.
- [ ] **R7-01 - Legacy document/viewport rules.** Review each recognized prelude, descriptor and
  binding against current standards and the pinned compatibility surface.
  Done when supported behavior has native/CSSOM fixtures or a named standards correction replaces
  it; neither silently dropping recognized rules nor accepting arbitrary descriptors closes this task.

### C6 - Computation and dependency closure

- [ ] **C6-01 - Font, environment and color inputs.** Complete the concrete pending dependencies
  in `NativeCssComputation`, `NativeCssTypography` and `NativeCssColors`, using invocation-owned
  metrics/environment snapshots and V0/V4 typed values.
  Done when relative font/root/line metrics, writing modes, environment values and contextual colors
  invalidate correctly, while genuinely absent host inputs retain explicit failures.
- [ ] **C6-02 - Container dimensions and relative units.** Extend `NativeCssQuery.Container`
  beyond its supported metric cases to height, logical/non-px dimensions and the remaining
  flat-tree/container-unit dependencies. Done when nested/named container selection, fallback,
  shadow/slot ancestry and percent/font-dependent sizes have exact results and bounded cycle checks.
- [ ] **C6-03 - Remaining container conditions.** Implement the adopted style/scroll-state
  conditions and their required snapshots separately from parsing a condition.
  Done when unsupported state is not assumed false/true, per-turn state changes invalidate
  results and container-dependent global-rule restrictions remain enforced.
- [ ] **C6-04 - Used-value geometry boundaries.** Inventory and implement positioned offsets,
  percentage bases, automatic minimums, replaced/SVG dimensions, transform reference boxes and
  anchor geometry against the Browser's documented flat layout model.
  Depends on the relevant V2/V3/V5/V7 inputs. Done when each removed failure has a real geometry
  producer and fixtures distinguishing computed from used values; do not globally replace synthetic
  block geometry merely to make a declaration test observe its authored width.
- [ ] **C6-05 - Cross-surface invalidation audit.** Extend existing query/cache tests for new
  descriptors, registrations, scopes, imports, namespaces, slots and host-state dependencies.
  Done when unchanged input reuses results, each relevant mutation invalidates them, callbacks
  cannot publish stale partial results, and shared syntax never retains a query/page.
  Run this audit after each dependent slice and again at ACC-07.

### Browser loading, selectors and parser integration

- [ ] **LOAD-01 - Stylesheet byte decoding.** Trace the current native stylesheet loading and
  import paths, then implement the specified BOM/transport/@charset/environment encoding choice.
  Done when a local-server fixture checks conflicting/absent labels, malformed sequences,
  imported-sheet encoding and cancellation; decoded-string CSS parsing remains unchanged.
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
- [ ] **API-02 - Promote validated CSS values and declarations.** Complete the C5 value/context
  gates, then expose the reviewed `ParseCssValue`/`ParseCssDeclarations` contracts.
  Done when the package distinguishes invalid, unsupported and deferred values, exposes no
  temporary validation flags, and external mutations preserve declaration identity and atomicity.
- [ ] **API-03 - Promote mutable stylesheet/rule CSSOM.** After R1-R7 and API-02, review and expose
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
- [ ] **ACC-01 - Finish the real Scalar interaction.** Its 66 property registrations and
  document-level `:host` selectors
  no longer stop scrollbar measurement. It now fetches and processes the captured OpenAPI document,
  then reports `CSSStyleDeclaration.clip: Unimplemented CSS grammar: V7:clip`; navigation buttons
  still do not appear. Implement the missing clip grammar rather than discarding the declaration.
  Framework-caught failures require bounded console/request/DOM diagnostics; the console snapshot
  retains both its first and last messages so later timing logs cannot hide the initial exception.
  An empty `Page.Errors` list alone does not prove success. Budgets remain unchanged.
  After V7-01, rerun `ScalarFixtureTests` after each newly exposed fix. Done when the fixture opens
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
  Separate syntax parsing, validated sheet/declaration parsing, mutation and CSSOM reads; both arms
  must do identical work. Done when untimed structural/semantic comparison and deliberate corruption
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
  Swagger, Scalar, XML comparisons and the C6-05 cache checks. Run dependency/binding staleness and
  inventory checks; keep AngleSharp only in comparison benchmarks.
  Done when remaining failures/skips have exact reviewed dispositions and the final evidence
  records commit, platform, framework, command and outcome rather than only aggregate pass counts.
- [ ] **ACC-08 - Close the overall tracker.** Reconcile INV-01 with every value/rule task and public
  API gate; link implementation evidence and accepted separate-phase decisions.
  Done only when all required items above are closed and ACC-01 through ACC-07 have current evidence.
  Targeted passing slices, dependency removal or this expanded plan cannot close the overall gate.

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
