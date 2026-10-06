# Native CSS stylesheet and declaration model: C4/C5 handoff


> Historical design: the current [LightPanda-style CSS boundary](https://github.com/sebastienros/jint/blob/main/Jint.HtmlParser/README.md#renderless-css-boundary)
> supersedes typed-value, computation, registration, container and keyframe claims below.
> These are retained design notes, not instructions to restore removed features.

Design only, 2026-09-23. This refines C4 and scopes C5 in
[the parser architecture](html-parser.md); it does not reopen the package or Browser architecture.
The immediate dispatch is **C4a, internal syntax editing and serialization**, after the small C1
extension below. It can ship independently of D5 native DOM mutations. It does **not** complete
`MarkupParser.ParseCss`, `ParseCssDeclarations`, `CSS.supports`, or Browser CSSOM replacement.
Those names promise validated CSS and remain absent until the C4b/C5 gate passes. No temporary public
syntax-only implementation, validation switch, injected validator, or placeholder property values.

## 1. Evidence and the surface that must survive

The baseline is AngleSharp.Css **1.1.2**, whose NuGet repository commit is
[`c5ddd09b54605551583d058bf37a31b82c8932f4`](https://github.com/AngleSharp/AngleSharp.Css/tree/c5ddd09b54605551583d058bf37a31b82c8932f4/src/AngleSharp.Css).
`dotnet-inspect` confirmed `ICssStyleSheet`, `ICssStyleDeclaration`, and `ICssProperties`; pinned
source inspection supplied the parser registry and declaration converters. The declaration factory
registers **433 distinct names**. That is an inventory of implementation obligations, not evidence
that every name is a conforming ordinary property. It includes descriptor names and legacy extensions.
The generic factory fallback accepts arbitrary token sequences for unknown names; copying that
fallback would make an unsupported grammar appear implemented.

| Existing consumer | Required native information or behavior |
| --- | --- |
| `Jint.Browser/Dom/Generated/DomShapes.Css.g.cs` and its generator inputs | Stable rule, declaration and list identities; rule brands; indexed declarations; getters and setters; sheet/group insert/delete; keyframe append/find/delete; media mutation; descriptor rules. Named style accessors are only part of the surface: generic `setProperty` reaches the larger registry. Regenerate bindings later; never edit generated output. |
| `Dom/Views/CssCascade.cs`, `ResolvedStyle.cs`, `ReadOnlyStyleDeclaration.cs` | Declaration enumeration, priority, shorthand expansion, specified values, initial/inheritance metadata, custom properties, current selectors and source order. Computed values are C6 results, never C4 defaults. The computed-style wrapper stays live and rejects writes. |
| `Dom/Views/JsCssNamespace.cs` | `CSS.supports` must use the same property and condition grammars as stylesheet parsing. Component-value parsing success is insufficient. |
| `Layout/PageLayout.Invalidation.cs` | Every style change capable of changing layout must invalidate reuse, including CSSOM writes and asynchronous sheet/import attachment. Existing mutation/reentrancy and bypass-write protections remain until equivalent native coverage is proved. |
| `DevTools/CssStyleSheetText.cs`, `CssStyleSheetTracker.cs`, `CssDomain` | Sheet/rule identity, owner/source metadata, imported sheets, serialized CSS text, and UTF-16 ranges into that exact text. Original author offsets cannot stand in for serialization offsets. |
| `Parsing/StyleSheetLoadingTests.cs`, `Views/*StyleTests.cs`, `MediaQueryChangeTests.cs`, `Layout/LayoutInvalidationTests.cs`, `DevTools/CssCoverageTests.cs` | Inline/external sheets, live changes, import loading, media/preferences, inherited and computed values, coverage, and mutation-driven layout changes. These are eventual Browser gates, not a substitute for standalone parser tests. |

Current generated rule families include charset, color-profile, condition, container, counter-style,
document, font-face, font-feature-values, font-palette-values, grouping, import, keyframe/keyframes,
layer, margin, media, namespace, page, position-try, property, scope, starting-style, style, supports,
and view-transition. The parser also recognizes `@viewport`. An absent layout consumer does not
authorize dropping a rule's parsing, CSSOM, serialization or JS exposure.

## 2. Exact ownership and dependencies

All new implementation types start `internal`, under `Jint.HtmlParser.Css`. The final named public
entry points and their result types are promoted together after packed-consumer verification. Native
types have no AngleSharp or Jint-engine references. JS prototype brands and WebIDL conversions belong
to Browser adapters; no AngleSharp interface hierarchy is recreated merely to satisfy the generator.

| Owner / bounded dispatch | Files and responsibility | Requires |
| --- | --- | --- |
| C1 extension | Existing `Css/Syntax/CssSyntaxParser.cs`, new list/block syntax records and syntax tests; operation-wide recovery, limits, source offsets, cancellation | Existing C1 |
| C4a | New `Css/Model/Syntax/` files for the three syntax editors below; `Css/Serialization/CssSyntaxSerializer.cs`; internal mutation stamp and focused tests | C1 extension |
| C5 foundation | New `Css/Values/` metadata, value result and common value grammars; explicit property/descriptor context tables | C1; storage need not be public or complete |
| C5 families | Owned grammar files and fixtures per property family in section 9; rule grammar groups in section 8 | Foundation; named grammar dependencies only |
| C4b | New validated `Css/Model/` sheet/rule/declaration types, CSSOM algorithms and CSSOM serializer; integrate actual C2/C5 parsers | C1 lists, C2 appropriate selector contexts, completed C5 groups |
| D5 + Browser integration | Native style-attribute binding, sheet attachment, JS adapters and generated surfaces, page invalidation/DevTools | Validated C4b/C5; native D5 mutation hooks |
| C6 | Cascade, media evaluation, variable substitution and computation | Validated model; selectors; device/element state |

The parser owner alone edits existing C1 parser/shared syntax types. C4a requests that prerequisite
as a separate change; it does not fork a second tokenizer. The C4 owner owns `CssStyleSheet`, `CssRule`,
`CssRuleList`, `CssDeclarationBlock`, `CssRuleType`, and `CssMutationStamp`. C5 owns `CssValueParseResult`,
`CssValue`, property/descriptor IDs and metadata; no duplicate value-result enum in C4. C2 owns selector
programs and nested-selector context. Existing `CssRuleKind` means **AtRule or QualifiedRule** and
must not be repurposed as the CSSOM rule-kind enum.

Existing shared exceptions remain authoritative: `CssParseException` for strict standalone syntax
failure, `ParseLimitException` for quotas, `OperationCanceledException` for cancellation, and native
`DomException.Name` for CSSOM failures. Add narrowly named CSSOM helpers to the existing `DomException`
only through its owner, or construct it with the existing constructor. No `CssOmException`, duplicate
DOM error enum, or broad catch that converts limits/cancellation into recoverable CSS errors.

## 3. C1 prerequisite: lists and mixed blocks

Single-rule and single-declaration entry points already exist. They require complete-input success
and cannot be called on guessed semicolon/brace substrings to parse a sheet. Add these internal
operations to the same parser, with names reserved here for the handoff:

```csharp
CssRuleSyntax[] ParseStyleSheet();
CssDeclarationSyntax[] ParseDeclarationList();
CssBlockSyntax ParseBlockContents(CssComponentValue block);
```

`ParseStyleSheet` uses stylesheet-level CDO/CDC handling; a nested rule-list consumer uses the
appropriate non-top-level grammar. `ParseDeclarationList` returns syntactically recovered declarations,
including unknown names and duplicates. `ParseBlockContents` returns ordered **declaration runs and
rules**, not two independent lists. It accepts a curly block from this parser's input and works from
the token/component representation with original coordinates. A parser-internal range cursor may
replace that parameter if needed to preserve operation ownership; do not add a public source/range API.

`CssBlockSyntax` is an immutable ordered sequence of `CssBlockItemSyntax`. Each item has a discriminant
(`Rule` or `Declarations`) and exactly one corresponding immutable payload: `CssRuleSyntax` or
`CssDeclarationSyntax[]` behind a read-only view. Keep source spans on both payloads. No nullable
bag permitting both kinds at once; use internal constructors enforcing the invariant. Unknown at-rule
bodies remain opaque component values until a known grammar specifically requests block interpretation.

This is the [CSS Syntax list and block parser](https://drafts.csswg.org/css-syntax/#parser-algorithms),
including recovery around malformed declarations and nested qualified rules. It is not line splitting:
semicolons and braces inside strings, URLs, functions and blocks are not list boundaries. A component
block's recovered EOF is distinct from an absent qualified-rule block. Recovery preserves subsequent
siblings at the specification's recovery boundaries; it must not invent boundaries to salvage text
consumed into an invalid rule. At top level, `} a{}` retains one syntax rule with prelude `} a` and
a parse diagnostic; validated CSSOM later rejects that selector. `} a{} b{}` retains two syntax
rules, of which only `b{}` survives selector validation. In nested parsing, `}` ends the enclosing
block instead. Diagnostics describe discarded syntax as well as recovered EOFs.

Clear the diagnostic collector **once** per top-level operation. Keep the original UTF-16 source span
through nested consumers. Carry the existing cancellation token, work cadence and depth/input/token
limits through all consumers; no new constructor per child that resets diagnostics or budgets. Use
explicit bounded parser frames for arbitrary nesting. Do not serialize/re-tokenize each nested body,
copy overlapping source slices at each depth, or use unbounded recursive tree traversal.

Required tests: top-level versus nested CDO/CDC; unknown block and statement at-rules; EOF recovery;
bad declarations followed by valid ones; comments/escapes/URLs; custom names and `!important`; mixed
rule/declaration order; deep nesting and long sibling lists; original diagnostic offsets; cancellation
and every existing quota propagating without a successful partial result.

## 4. C4a: a complete internal syntax-editing dispatch

Use three small sealed editors. Their names deliberately retain **Syntax**. They preserve C1 meaning
and never answer property support, cascade, computed style or validated CSSOM questions.

| Type | Exact internal surface and invariant |
| --- | --- |
| `CssSyntaxStyleSheet` | Static `Parse(string, CssParseOptions?, CancellationToken)`; stable live `Rules` read-only view of `CssSyntaxRule`; `InsertRule(string, int, CssParseOptions?, CancellationToken)` returns index; `DeleteRule(int)`; `Serialize()`; `Stamp`. Input parses as a recovered stylesheet. Insert parses exactly one C1 rule and applies no CSSOM rule-placement restrictions. |
| `CssSyntaxRule` | `Syntax` returns current immutable `CssRuleSyntax`; `ParentStyleSheet`; `ReplaceSyntax(string, CssParseOptions?, CancellationToken)` parses exactly one C1 rule, retains this editor's identity, then swaps syntax; `Serialize()`; `Stamp`. Bodies stay immutable opaque C1 component trees. No nested live editors or guessing a body grammar from its name in this dispatch. |
| `CssSyntaxDeclarationBlock` | Static `Parse(string, CssParseOptions?, CancellationToken)`; live read-only `Declarations` view in syntax order; `InsertDeclaration(string, int, CssParseOptions?, CancellationToken)`; `ReplaceDeclaration(string, int, CssParseOptions?, CancellationToken)`; `DeleteDeclaration(int)`; `ReplaceText(string, CssParseOptions?, CancellationToken)`; `Serialize()`; `Stamp`. Duplicates, original decoded name case and unknown property names survive. No name-based effective-value lookup. |

Lists expose count, index and enumeration only. Use existing collection conventions rather than
exporting `List<T>`. Syntax-editor indices are ordinary native argument validation
(`ArgumentOutOfRangeException`); these are not the web's insert/delete algorithms. A removed rule's
parent becomes null and further edits do not touch the old sheet. Rules are created by parsing, not
attached by a public reparent operation. Only the local syntax text is mutable: no network/import
objects, element references, selectors or callbacks are stored here.

Every text edit parses its replacement privately, checks cancellation immediately before commit, and
then changes state and stamps together. Failure leaves the prior identity, contents, serialization and
stamps intact. `ReplaceText` with syntactically bad fragments can succeed with a recovered empty list;
that is deliberate syntax-list recovery. A successful replace may conservatively advance a stamp
even if its serialization is unchanged. Querying or serializing never advances one.

The validated builder later **consumes immutable C1 payloads**, builds validated objects, and discards
the syntax editor if one was used. It does not keep an independently mutable raw syntax shadow of every
CSSOM declaration. Sharing immutable component arrays is allowed; sharing mutable list ownership is
not. A direct private C1-to-C4b path avoids constructing editors merely to parse a production sheet.
This keeps C4a honest and useful without forcing unvalidated entries into the eventual public model.

Acceptance is limited to syntax parse/edit/recovery, stable views and rule identity, serialization,
ownership and stamps. Unknown rules and `made-up: tokens` must survive here, demonstrating why this
is not public `ParseCss`. Tests explicitly include two same-name declarations and an invalid property
value that is legal C1 syntax. Do not add `GetPropertyValue`, `IsSupported`, always-empty defaults,
or a callback slot for a future validator.

## 5. C4b: the validated native object model

The following is the target model, not a request to stub it during C4a. Constructors and mutator cores
remain internal until validators exist. Promote only the useful standalone surface described by the
architecture; rule-specific internals can remain narrow and be consumed by Browser through its normal
native access boundary.

| Type | State and operations |
| --- | --- |
| `CssStyleSheet` | Stable `Rules: CssRuleList`; `InsertRule(string, int, CssParseOptions?, CancellationToken): int`; `DeleteRule(int)`; `Serialize(): string`; `Stamp`. A mutable `Disabled` state participates in the stamp. Import owner, base/source URL, media and owner-node association are explicit internal attachment state, assigned by the loader, not inferred from the parse string. Parsing never fetches. |
| `CssRuleList` | Stable live count/index/enumeration view, owned by exactly one sheet or rule; internal mutation only. Native indexer throws for an invalid index; Browser `item()` returns null and applies WebIDL conversion. Enumeration does not grant an edit handle. |
| `CssRule` | Stable reference identity; `Type: CssRuleType`; `ParentRule`, `ParentStyleSheet`; read-only `CssText`; `Stamp`. A closed internal payload per rule type holds its validated prelude, permitted children, declarations/descriptors and kind-specific data. No extensible user subclass/plugin factory. |
| `CssDeclarationBlock` | Stable identity; `Count`, ordered `GetPropertyName(int)`, `GetPropertyValue(string)`, `GetPropertyPriority(string)`, `SetProperty(string, string, string?, CssParseOptions?, CancellationToken)`, `RemoveProperty(string): string`, `CssText`, `Stamp`. A text-taking core carries options/cancellation; a property setter uses defaults. Store validated declarations and parsed values, not just strings. |

Avoid a public all-purpose `SetPrelude(string)` that has ambiguous validation and failure semantics.
Internal typed operations are `SetSelectorText`, `SetConditionText`, `SetKeyText`, and rule-specific
name/descriptor/media operations, present only for kinds that have that operation. Style rules have
their declaration block and nested rules; groups have rules; keyframes have keyframe children;
descriptor rules have a descriptor-context block. A stylesheet has no declaration block. Non-container
rules do not fabricate empty mutable child lists. Browser selects each JS brand using the native kind.

`CssRuleType` must distinguish Style, NestedDeclarations, Import, Namespace, Media, Supports,
Container, LayerStatement, LayerBlock, Scope, StartingStyle, Keyframes, Keyframe, FontFace, Page,
Margin, CounterStyle, FontFeatureValues and its feature maps, Property, ViewTransition, PositionTry,
FontPaletteValues, ColorProfile, Document and Viewport. Native values need not copy historical JS
`CSSRule.type` numbers. Charset is accounted for as a compatibility decision in section 8, not an
invented active rule. Unknown syntax has no validated CSSOM kind.

Rule removal clears the removed root's exposed parent links according to CSSOM; descendant parent-rule
relationships and their existing exposed sheet references remain intact: the removal algorithm clears
the removed rule's links, not every descendant's. Keep attachment/invalidation ownership separate from
those exposed links: a retained detached subtree cannot invalidate the old sheet merely because a
historical CSSOM owner reference exists. Freeze this distinction with nested-group removal fixtures,
including edits through a retained descendant. Removing an owning DOM node is a different operation
from deleting a rule and does not itself erase a retained sheet's rule objects.

In-place selector, condition, declaration and descriptor edits preserve rule and list identity.
Whole-sheet replacement may replace rule identities, but must retain the sheet and its live list view;
detach discarded roots and build the replacement privately first. Keyframe find/delete uses parsed
key-list equivalence and the specified last matching rule, not substring comparison. Disabled/media
changes are style changes even when rule serialization is identical.

## 6. Parsing and mutation contracts at the validated gate

| Operation | Required result |
| --- | --- |
| `ParseCss` | Recover stylesheet syntax, then apply rule context, selector/prelude, property and descriptor grammars. Omit invalid/unsupported CSSOM rules and declarations at their specified recovery boundary; preserve valid siblings. An empty valid sheet is a real result. |
| `ParseCssDeclarations` / declaration `CssText` assignment | Parse a declaration list and validate in ordinary-style context. Invalid entries drop; valid duplicates resolve with importance/order. Assignment replaces the old list even when the result is empty. |
| C1 `ParseCssRule` / `ParseCssDeclaration` | Continue to return syntax only, preserving structurally legal unknown names. No newly inferred property-validity promise. |
| `ParseCssValue(name, text)` | C5 distinguishes Valid, Invalid and UnsupportedProperty; only Valid has a value. Parsing a supported but invalid grammar must not become UnsupportedProperty. Deferred substitution has an explicit value form. No computed value or invented initial value. |
| `SetProperty` | Ordinary names are ASCII case-insensitive; custom names are case-sensitive. Validate priority and the complete property value; invalid or unknown ordinary input is a no-op. Empty value removes before invalid-priority rejection. Priority is separate from the value; a value containing an importance suffix is not silently reinterpreted. |
| `RemoveProperty` | Return the previously serialized value, remove the property or shorthand's represented longhands, and update ordering/stamps only as required. Absent values return the CSSOM empty string. |
| Rule-specific setters | Apply the owning rule's grammar and standard setter failure policy. A selector-text syntax failure leaves the existing selector intact. Do not generalize every setter into either throw-on-failure or drop-on-failure. |

CSSOM insert/delete use native `DomException` names. Bounds are checked before parsing; insertion
accepts the endpoint but deletion does not. One parsed rule is required. Context-dependent declaration
fallback creates a NestedDeclarations rule only where CSS nesting permits it. Placement failures are
`HierarchyRequestError`; namespace mutation has the additional `InvalidStateError` restriction;
strict parse failure is `SyntaxError`. Test precedence when multiple failures apply, import/namespace
ordering, layer ordering interactions, and each group context. This contract follows the
[CSSOM mutation algorithms](https://drafts.csswg.org/cssom/#insert-a-css-rule), not C4a's syntax editor.
JS read-only/origin-clean/constructed-sheet checks remain explicit adapter/attachment concerns and
must run in their specified order before reaching the native mutation core.

Prepare and validate off-model; commit once. Cancellation and limits abort without partially installing
a sheet, rule or declaration edit. Recoverable CSS invalidity is not cancellation. Allocation failure
is not caught and reported as a successful empty sheet. A synchronous structural commit invokes no
author script; internal attribute integration handles its distinct reentrancy/notification rules.

Shorthand and declaration behavior is **C5 work**, including expansion, reset-only components,
priority propagation, mixed shorthand/longhand order, logical/physical interleaving, and serialization
eligibility. `GetPropertyValue(shorthand)` is neither the last raw shorthand string nor a join of
arbitrary longhands. An empty string can be correct for a nonrepresentable shorthand; it cannot be the
implementation's fallback for missing grammar. Custom properties preserve their case and value-token
semantics; `var()`/`env()` acceptance and deferred shorthand values use explicit parsed forms. Cycle
resolution, fallback substitution and invalid-at-computed-value handling belong to C6, using C5 data.

Current nesting requires declarations after child rules to retain their place as NestedDeclarations
objects. Do not hoist all declarations into the parent's first block or synthesize an ordinary `&`
rule: matching pseudo-elements and specificity differ. See
[CSS Nesting's mixed-content and declaration-rule model](https://drafts.csswg.org/css-nesting-1/#mixing).
C2 must add the correct nested-selector context and scope binding before this path is validated;
reusing a forgiving selector-list or relative-selector flag is insufficient. C4a can retain the syntax
without falsely claiming the nested selector has compiled.

One explicit standards correction needs a cutover regression: pinned `CssRule.CssText` parses and
replaces a same-kind rule, while current CSSOM defines the web setter as a no-op. The native validated
property is read-only and Browser's JS setter follows current CSSOM; record that delta in the root
cutover review. C4a's explicitly named `ReplaceSyntax` remains an internal editing operation.

## 7. Serialization, identity and honest stamps

There are two serializers. C4a serializes C1 syntax, preserving token boundaries and recovered
structure without asserting property semantics. C4b serializes validated CSSOM, including selectors,
values, declarations, descriptors and each rule kind. Neither promises byte-for-byte source round-trip.
Whitespace/comments are not a lossless source-editing API. Original spans remain provenance into the
particular parsed input, and must never be rewritten into serialized offsets.

The syntax serializer uses token kind and flags, identifier/string escaping, numeric representation,
hash identity, URL form and nested component structure. Joining decoded token text can change meaning;
test adjacent identifiers/numbers, escapes, comment-separated tokens and `url()` specially. Retain any
necessary source slice or lexical information within immutable syntax storage when C1's current token
fields cannot serialize faithfully. That extension belongs to C1, not a second tokenizer in C4.
Use the [CSS Syntax serialization requirements](https://drafts.csswg.org/css-syntax/#serialization).
For recovered bad tokens, test the documented canonical recovery representation rather than claiming
all malformed input must reproduce an identical token stream.

CSSOM serialization uses portable LF and the rule-specific standard representation; there is no single
generic `prelude + '{' + body` formatter for every rule. Declaration serialization preserves the
ordering constraints of shorthands and logical properties and the significant custom-property value
representation. An unchanged validated parse/serialize/parse must retain observable declaration/rule
meaning, priority and order; it need not retain original whitespace or numeric spelling.

Give DevTools one internal `SerializeWithRanges()` operation returning `CssSerializationSnapshot`
with `Text` and rule-identity-to-`CssTextRange` entries. Ranges are UTF-16 half-open offsets into that
same returned string. Build text and ranges in the same traversal; never find the first `{` in a
serialized prelude or reuse author spans. Snapshot entries include nested declaration and nested style
rules where exposed, and cover empty serialized cases deliberately. The range table is a result of
serialization, not a live mutable map on each rule. Snapshot identity plus text is the validity unit
for coverage; a later mutation requires a new snapshot.

`CssMutationStamp` is an internal immutable value snapshot with `Value: ulong` and
`CanReuse: bool` (`Value != ulong.MaxValue`). Each mutable owner keeps a private counter and returns
a fresh value snapshot from `Stamp`; reading a stamp must not hand out a live mutable counter reference.
Advance monotonically. Saturate at `ulong.MaxValue` and
permanently decline reuse from that object once saturated; equality at saturation is not freshness.
Do not use document D5 counters as the sheet's only stamp. C4a and C4b have their own independent stamps.

| Change | Stamp/invalidation effect |
| --- | --- |
| Rule insert/delete or successful replacement | Owning list/container and sheet advance; unrelated siblings retain identity. Removed root is detached from invalidation propagation. |
| Declaration, selector, descriptor, keyframe-name or condition mutation | Local object and its attached ancestor chain advance. Every mutating route, including named JS setters, reaches the same core. |
| Disabled/media/base attachment change | Sheet/attachment state invalidates style consumers even if rule text did not change. |
| Read, enumeration, serialization, invalid rejected input | No mutation stamp advance. Conservative advancement after a successful equivalent replacement is allowed; missed advancement is not. |
| Imported sheet completion/change | Loader updates explicit imported-sheet attachment and page invalidation. A parent sheet stamp alone is not a proof that a separately loaded imported sheet is unchanged. |

A stamp only describes writes the object owns. Cache users must also account for stylesheet membership,
imports, DOM/state/media dependencies and inline attributes. Keep the existing Browser no-reuse path
for uncontrolled host writes/import behavior until integration closes it. Do not expose a mutation
event or arbitrary callback framework merely to bridge unfinished D5/C6 work. Native objects are
single-thread-owned; a version counter does not make them thread safe.

### Later inline-style binding seam

The eventual binding is one native element-associated validated `CssDeclarationBlock`, created lazily
and retained so `element.style` identity survives `setAttribute`, attribute-node changes and
`style.cssText`. Its initial parse reads the current attribute and uses ordinary declaration context.
D5's synchronous attribute-commit hook updates that same block for every native attribute-writing
path. CSSOM writes serialize the block back through that same attribute mutation core exactly once.

Use a narrowly scoped origin/reentrancy guard only for the binding's own write-back echo. Never suppress
a distinct nested author mutation. Parse a replacement before committing; no asynchronous MutationObserver
is the cache-coherence mechanism. Removing the style attribute clears the existing block. Cloning an
element creates independent mutable state. Namespace handling follows the actual unqualified style
attribute rule; arbitrary same-local-name attributes cannot silently bind style.

The style stamp advances when the declaration block changes; the D5 attribute stamp and observer
record follow the attribute operation's own rules, even if CSS semantics happen to be equivalent.
Keep raw `getAttribute('style')` text and declaration serialization separate. This seam is specified
now but implemented only with the real D5 hook; C4a has no element field or empty callback placeholder.

## 8. C5 rule and descriptor grammar inventory

The pinned parser recognizes the following 21 at-rule names. Each row is a finite grammar task,
including prelude, allowed location/body, parse recovery, mutation setters and serialization tests.
Sharing C1's block parser is expected; treating every body as ordinary declarations is wrong.

| Group | Names / nested contexts | Required ownership beyond C1 |
| --- | --- | --- |
| R1 sheet prologue | `@import`, `@namespace`, `@charset` | URL/media/layer/supports import prelude and placement; namespace environment for C2; encoding declaration policy. String `ParseCss` is already decoded: `@charset` cannot restart decoding. Current CSS syntax/CSSOM does not expose it as an ordinary active rule; explicitly test/document the difference from pinned CSSCharsetRule. |
| R2 conditional and scoped groups | `@media`, `@supports`, `@container`, `@scope`, `@starting-style`, `@layer` statement/block | Separate media, supports, container and scope grammars; valid nested contexts; layer names/order. Parsing conditions is distinct from C6 evaluating them against device/element state. `CSS.supports` consumes real C5/C2 validation. |
| R3 animation | `@keyframes` and keyframe selector blocks | Names, key-list percentages/from/to and timeline-range forms supported by the adopted grammar; declarations in keyframe context; invalid importance handling; find/append/delete semantics. Explicitly inventory aliases instead of accepting every vendor spelling. |
| R4 fonts | `@font-face`, `@font-feature-values`, `@font-palette-values` | Font-face descriptor allowlist and descriptor-specific ranges; family preludes; feature maps (stylistic, styleset, character-variant, swash, ornaments, annotation and current supported extensions); palette descriptors `font-family`, `base-palette`, `override-colors`. Feature maps are not ordinary style-rule lists. |
| R5 pages/counters | `@page`, page-margin rules, `@counter-style` | Page selector/pseudo grammar and page context; all page margin-box names; counter descriptors `system`, `symbols`, `additive-symbols`, `negative`, `prefix`, `suffix`, `range`, `pad`, `fallback`, `speak-as`. Descriptor dependencies and invalid whole-rule cases are required. |
| R6 registration/special descriptors | `@property`, `@view-transition`, `@position-try`, `@color-profile` | Property registration `syntax`/`inherits`/`initial-value`, required fields and computational independence; view-transition `navigation`/`types`; position-try's permitted declaration subset; profile `src`/`rendering-intent` and current supported descriptors. Context grammars cannot inherit the arbitrary-value pinned descriptor fallback. |
| R7 legacy recognized rules | `@document`, `@viewport` | Characterize existing parse/serialization/JS behavior and the normative status before cutover. Keep tested supported behavior or submit a named standards correction for root review. They cannot silently disappear because layout does not read them. |

The [completion tracker](html-parser-completeness.md) records the implemented primitive `@property`
slice and its published Level 1 acceptance contract. Its CSSOM surface follows the standard
readonly `name`, `syntax`, `inherits` and nullable `initialValue` attributes, not the pinned
descriptor-map methods. Registration affects computed values, not ordinary custom-property
declaration acceptance. Unsupported typed grammars and cycles through ordinary properties remain
named failures; completing descriptor parsing alone does not close R6.

Unknown at-rules remain opaque in C1/C4a and are discarded by validated stylesheet parsing at the
appropriate boundary. A known but not-yet-implemented rule is a **completion blocker**, not an
unknown rule that can be silently dropped to make C4 pass. No tolerant/unknown-rule flags in the
public API. Descriptor registry keys include context, not only property name; `src`, `size-adjust`
and `unicode-range` illustrate why a global ordinary-property allowlist is insufficient.

The font descriptor inventory includes the registered `font-*` descriptor forms, `src`,
`unicode-range`, `font-display`, `ascent-override`, `descent-override`, `line-gap-override` and
`size-adjust`, with grammar checked in font-face context. Additional names found in a rule's actual
setter surface must join its inventory; a named getter returning an unvalidated raw string is not proof
of implemented grammar. Snapshot the pinned descriptor surface and compare it with the current rule
specification/WPT. Record any corrective rejection explicitly, rather than implementing the source's
`RawValue => null` descriptor model.

## 9. C5 property families and the completion gate

The appendix partitions all **433** pinned registrations into finite ownership groups. Each group
must supply a checked manifest of names, ordinary-property versus descriptor contexts, aliases,
shorthand membership/reset-only longhands, inheritance/initial metadata, accepted value grammar,
specified serialization, and its positive/negative fixtures. Unknown-name fallback is not a group.
The registry is the scope baseline, not permission to reproduce library bugs or arbitrary-value
acceptance. Standards corrections require a named old/new fixture and root cutover review.

| Group | Work and key cross-dependencies |
| --- | --- |
| V0 shared foundation | CSS-wide keywords including `revert-layer`; numeric/dimension/percentage grammars and ranges; `calc`/math typing; identifiers/strings/URLs; common color grammar; explicit deferred substitution and custom properties; `all` reset metadata. `all` is an additional Browser-relevant obligation, absent from the 433-entry factory inventory. |
| V1 paint and decoration | Color-bearing values, backgrounds/layers/images/gradients, borders/outlines/shadows, blending/opacity and appearance; share V0 color/image grammars without unchecked strings. |
| V2 box and positioning | Physical/logical sizes, spacing/insets, box sizing, positioning/floats and anchors; preserve percentages and relative units for C6. |
| V3 layout and containment | Display, flex/grid/alignment, columns, containment/containers and fragmentation; grid line/track/template syntax and shorthand dependencies. |
| V4 typography and text | Font/style/shorthands, inline text/writing modes, text decoration, ruby and whitespace; font descriptor contexts remain distinct from inherited properties. |
| V5 transforms and motion | Transform functions and individual transforms, perspective, transitions/animations including lists/ranges/timelines; property-name lists use the registry. |
| V6 scrolling and interaction | Overflow/overscroll/scroll margins and padding/snap, scrollbar families, cursor/pointer/touch/user selection, visibility and resizing. |
| V7 SVG, masks and replaced content | Fill/stroke/markers, clipping/masks/filters, image/object/shape properties and SVG baselines. No promise of a paint engine is inferred from parsed values. |
| V8 generated content, tables and pages | Content/counters/lists/quotes/bookmarks/footnotes, tables and paged-media properties; page and counter descriptor grammars are R5. |
| V9 descriptor-only registrations | The registered standalone descriptor names assigned here are parsed in their allowed contexts by R4; never automatically exposed as ordinary properties. |

V0 is a prerequisite, not an alternative small public CSS subset. Family owners can develop against
internal grammar tests, but public completion requires every assigned registration to have a reviewed
disposition, all Browser named/generic routes to be covered, and all seven rule groups accounted for.
Within each family, `Invalid` must be distinguishable from `UnsupportedProperty`. Tests cover values
that are valid C1 token streams but invalid CSS values, keyword case, escapes, ranges, trailing junk,
custom-property case, invalid priorities, shorthand conflict order and pending substitution.

C5 only parses and describes values. It does not return a fabricated computed pixel length for `ch`,
invent colors for unsupported syntax, or claim a condition evaluates true because it parsed. C6 must
retain the Browser's explicit inability to compute where environment/font/layout information is absent.
Conversely, a property that the flat layout does not use still needs its specified CSSOM behavior.

Final C4b/C5 verification includes standalone package consumers exercising every advertised parser,
mutable sheet/declaration/rule route, recovery/error distinction, ownership and serialization; focused
tests for all grammar groups; and Browser adapter parity for current live styles, imports, media,
named/generic setters, descriptor rules and DevTools ranges. Run the repository's relevant Release
tests after implementation, without `--no-build`. This design change runs documentation checks only
and makes no benchmark or speed claim.

### Ready C4a dispatch after the C1 prerequisite

Implement only section 4's three internal syntax editors, the syntax serializer and section 7's value
stamp contract in the reserved new files. Add focused tests under `Jint.Tests.HtmlParser/Css/Model/`
and `Css/Serialization/`. Consume the existing C1 tokenizer and the agreed list entry points; coordinate
any missing lexical data with the C1 owner. Do not edit native DOM/parser construction, the public
`MarkupParser` surface, Browser bindings, property grammars or C2 selector internals. Do not add a
stylesheet validator interface, property support flags or fake CSSOM methods. Exercise recovery,
edit atomicity, detached identity, live views, unknown syntax, duplicate declarations, token-boundary
serialization and saturation. Run `dotnet test -c Release --project Jint.Tests.HtmlParser/Jint.Tests.HtmlParser.csproj`.
Deliver the bounded implementation and tests as one reviewable change, reporting any C1 prerequisite
that actually remains missing instead of implementing a second parser. This dispatch can run alongside
D5; it does not wait for native attribute mutation or claim the public C4/C5 milestone complete.

### Appendix: exact registered-name assignment

Names below come from `Factories/DefaultDeclarationFactory.cs` declaration-class `Name` references,
resolved through `Constants/PropertyNames.cs` at the pinned commit. They are sorted within each group,
appear exactly once, and exclude unregistered constants. The grouping is an ownership assignment;
the context/standards audit above still applies to every name, particularly legacy scrollbar and
paged-media extensions. Custom names (`--*`), `all`, rule descriptors absent from this registry and
current standard additions found by the Browser/WPT audit are tracked separately and cannot be erased
by treating this appendix as the entire CSS language.

#### V1 paint and decoration (90)

`accent-color`, `appearance`, `background`, `background-attachment`, `background-blend-mode`,
`background-clip`, `background-color`, `background-image`, `background-origin`, `background-position`,
`background-position-x`, `background-position-y`, `background-repeat`, `background-repeat-x`,
`background-repeat-y`, `background-size`, `border`, `border-block`, `border-block-color`,
`border-block-end`, `border-block-end-color`, `border-block-end-style`, `border-block-end-width`,
`border-block-start`, `border-block-start-color`, `border-block-start-style`, `border-block-start-width`,
`border-block-style`, `border-block-width`, `border-bottom`, `border-bottom-color`,
`border-bottom-left-radius`, `border-bottom-right-radius`, `border-bottom-style`, `border-bottom-width`,
`border-color`, `border-end-end-radius`, `border-end-start-radius`, `border-image`, `border-image-outset`,
`border-image-repeat`, `border-image-slice`, `border-image-source`, `border-image-width`, `border-inline`,
`border-inline-color`, `border-inline-end`, `border-inline-end-color`, `border-inline-end-style`,
`border-inline-end-width`, `border-inline-start`, `border-inline-start-color`,
`border-inline-start-style`, `border-inline-start-width`, `border-inline-style`, `border-inline-width`,
`border-left`, `border-left-color`, `border-left-style`, `border-left-width`, `border-radius`,
`border-right`, `border-right-color`, `border-right-style`, `border-right-width`,
`border-start-end-radius`, `border-start-start-radius`, `border-style`, `border-top`, `border-top-color`,
`border-top-left-radius`, `border-top-right-radius`, `border-top-style`, `border-top-width`,
`border-width`, `box-decoration-break`, `box-shadow`, `caret-color`, `color`, `color-scheme`,
`forced-color-adjust`, `isolation`, `mix-blend-mode`, `opacity`, `outline`, `outline-color`,
`outline-offset`, `outline-style`, `outline-width`, `print-color-adjust`.

#### V2 box and positioning (58)

`anchor-name`, `anchor-scope`, `aspect-ratio`, `block-size`, `bottom`, `box-sizing`, `clear`, `float`,
`height`, `inline-size`, `inset`, `inset-block`, `inset-block-end`, `inset-block-start`, `inset-inline`,
`inset-inline-end`, `inset-inline-start`, `left`, `margin`, `margin-block`, `margin-block-end`,
`margin-block-start`, `margin-bottom`, `margin-inline`, `margin-inline-end`, `margin-inline-start`,
`margin-left`, `margin-right`, `margin-top`, `max-block-size`, `max-height`, `max-inline-size`,
`max-width`, `min-block-size`, `min-height`, `min-inline-size`, `min-width`, `padding`, `padding-block`,
`padding-block-end`, `padding-block-start`, `padding-bottom`, `padding-inline`, `padding-inline-end`,
`padding-inline-start`, `padding-left`, `padding-right`, `padding-top`, `position`, `position-anchor`,
`position-area`, `position-try-fallbacks`, `position-try-order`, `position-visibility`, `right`, `top`,
`width`, `z-index`.

#### V3 layout and containment (61)

`align-content`, `align-items`, `align-self`, `break-after`, `break-before`, `break-inside`,
`column-count`, `column-fill`, `column-gap`, `column-rule`, `column-rule-color`, `column-rule-style`,
`column-rule-width`, `column-span`, `column-width`, `columns`, `contain`, `contain-intrinsic-block-size`,
`contain-intrinsic-height`, `contain-intrinsic-inline-size`, `contain-intrinsic-size`,
`contain-intrinsic-width`, `container`, `container-name`, `container-type`, `content-visibility`,
`display`, `flex`, `flex-basis`, `flex-direction`, `flex-flow`, `flex-grow`, `flex-shrink`, `flex-wrap`,
`gap`, `grid`, `grid-area`, `grid-auto-columns`, `grid-auto-flow`, `grid-auto-rows`, `grid-column`,
`grid-column-end`, `grid-column-gap`, `grid-column-start`, `grid-gap`, `grid-row`, `grid-row-end`,
`grid-row-gap`, `grid-row-start`, `grid-template`, `grid-template-areas`, `grid-template-columns`,
`grid-template-rows`, `justify-content`, `justify-items`, `justify-self`, `order`, `place-content`,
`place-items`, `place-self`, `row-gap`.

#### V4 typography and text (59)

`direction`, `font`, `font-family`, `font-feature-settings`, `font-kerning`, `font-language-override`,
`font-optical-sizing`, `font-palette`, `font-size`, `font-size-adjust`, `font-stretch`, `font-style`,
`font-synthesis`, `font-synthesis-small-caps`, `font-synthesis-style`, `font-synthesis-weight`,
`font-variant`, `font-variation-settings`, `font-weight`, `hanging-punctuation`, `hyphenate-character`,
`hyphenate-limit-chars`, `hyphens`, `initial-letter`, `initial-letter-align`, `letter-spacing`,
`line-break`, `line-height`, `ruby-align`, `ruby-overhang`, `ruby-position`, `tab-size`, `text-align`,
`text-align-last`, `text-anchor`, `text-decoration`, `text-decoration-color`, `text-decoration-line`,
`text-decoration-skip-ink`, `text-decoration-style`, `text-decoration-thickness`, `text-indent`,
`text-justify`, `text-overflow`, `text-shadow`, `text-transform`, `text-underline-offset`,
`text-underline-position`, `text-wrap`, `text-wrap-mode`, `text-wrap-style`, `unicode-bidi`,
`vertical-align`, `white-space`, `white-space-collapse`, `word-break`, `word-spacing`, `word-wrap`,
`writing-mode`.

#### V5 transforms and motion (31)

`animation`, `animation-composition`, `animation-delay`, `animation-direction`, `animation-duration`,
`animation-fill-mode`, `animation-iteration-count`, `animation-name`, `animation-play-state`,
`animation-range`, `animation-range-end`, `animation-range-start`, `animation-timeline`,
`animation-timing-function`, `backface-visibility`, `perspective`, `perspective-origin`, `rotate`,
`scale`, `transform`, `transform-origin`, `transform-style`, `transition`, `transition-delay`,
`transition-duration`, `transition-property`, `transition-timing-function`, `translate`,
`view-transition-class`, `view-transition-name`, `will-change`.

#### V6 scrolling and interaction (53)

`cursor`, `overflow`, `overflow-anchor`, `overflow-clip-margin`, `overflow-wrap`, `overflow-x`,
`overflow-y`, `overscroll-behavior`, `overscroll-behavior-block`, `overscroll-behavior-inline`,
`overscroll-behavior-x`, `overscroll-behavior-y`, `pointer-events`, `resize`, `scroll-behavior`,
`scroll-margin`, `scroll-margin-block`, `scroll-margin-block-end`, `scroll-margin-block-start`,
`scroll-margin-bottom`, `scroll-margin-inline`, `scroll-margin-inline-end`, `scroll-margin-inline-start`,
`scroll-margin-left`, `scroll-margin-right`, `scroll-margin-top`, `scroll-padding`,
`scroll-padding-block`, `scroll-padding-block-end`, `scroll-padding-block-start`, `scroll-padding-bottom`,
`scroll-padding-inline`, `scroll-padding-inline-end`, `scroll-padding-inline-start`,
`scroll-padding-left`, `scroll-padding-right`, `scroll-padding-top`, `scroll-snap-stop`,
`scroll-snap-type`, `scrollbar-arrow-color`, `scrollbar-base-color`, `scrollbar-color`,
`scrollbar-dark-shadow-color`, `scrollbar-face-color`, `scrollbar-gutter`, `scrollbar-highlight-color`,
`scrollbar-shadow-color`, `scrollbar-track-color`, `scrollbar-width`, `scrollbar3d-light-color`,
`touch-action`, `user-select`, `visibility`.

#### V7 SVG, masks and replaced content (48)

`alignment-baseline`, `backdrop-filter`, `baseline-shift`, `clip`, `clip-path`, `clip-rule`,
`color-interpolation-filters`, `dominant-baseline`, `fill`, `fill-opacity`, `fill-rule`, `filter`,
`image-orientation`, `image-rendering`, `marker-end`, `marker-mid`, `marker-start`, `mask`, `mask-border`,
`mask-border-mode`, `mask-border-outset`, `mask-border-repeat`, `mask-border-slice`, `mask-border-source`,
`mask-border-width`, `mask-clip`, `mask-composite`, `mask-image`, `mask-mode`, `mask-origin`,
`mask-position`, `mask-repeat`, `mask-size`, `mask-type`, `object-fit`, `object-position`,
`shape-image-threshold`, `shape-margin`, `shape-outside`, `shape-rendering`, `stroke`, `stroke-dasharray`,
`stroke-dashoffset`, `stroke-linecap`, `stroke-linejoin`, `stroke-miterlimit`, `stroke-opacity`,
`stroke-width`.

#### V8 generated content, tables and pages (26)

`bookmark-label`, `bookmark-level`, `bookmark-state`, `border-collapse`, `border-spacing`, `caption-side`,
`content`, `counter-increment`, `counter-reset`, `counter-set`, `empty-cells`, `footnote-display`,
`footnote-policy`, `list-style`, `list-style-image`, `list-style-position`, `list-style-type`, `orphans`,
`page-break-after`, `page-break-before`, `page-break-inside`, `quotes`, `running`, `string-set`,
`table-layout`, `widows`.

#### V9 descriptor-only registrations (7)

`ascent-override`, `descent-override`, `font-display`, `line-gap-override`, `size-adjust`, `src`,
`unicode-range`.
