# R4a passive font-face source packet


> Historical design: the current [LightPanda-style CSS boundary](https://github.com/sebastienros/jint/blob/main/Jint.HtmlParser/README.md#renderless-css-boundary)
> supersedes typed-value, computation, registration, container and keyframe claims below.
> These are retained design notes, not instructions to restore removed features.

This slice is based on reviewed common commit `674ae0fc211fca17c8dfd4c8d3e4aaa36e1eb130`.
It removes the `R4:font-face` sheet-read blocker by retaining a real native rule and
one stable descriptor declaration block. Native packets: `f21a8fdb0`, `ebb34617f`, and `f457872c2`.
It does not fetch, resolve, decode,
activate, match, measure, or lay out a font. Vendor fixtures and the existing
Monaco AMD/event proof remain unchanged.

## Standards and decisions for review

- [Fonts 4 §4.1–4.4](https://drafts.csswg.org/css-fonts-4/#font-resources),
  Editor's Draft 13 September 2026, governs the descriptor context. An empty or
  missing-family/src rule remains a CSSOM rule; font selection usability is a
  separate operation. The rule is a descriptor leaf and does not traverse nested
  at-rules. Unsupported ordinary properties, custom properties, aliases, `all`
  effects, and shorthand correlation do not enter this descriptor store.
- Completed descriptors are `font-family`, `src`, `font-display`, `font-weight`,
  and `font-style`. Known unfinished descriptors remain raw and report
  `R4:font-face:<descriptor>` only when demanded. A family read/write does not
  demand `font-width` or another unrelated pending value. Missing authored
  descriptors return the empty string rather than font selection defaults.
- Family accepts one string or custom-identifier sequence, preserves case, and
  rejects a fallback list, generic/system family keywords, and CSS-wide keywords.
  `src` recovers independently at commas. Unknown format identifiers, unrecognized
  format strings and unknown technologies discard their entry; no retained entry
  makes the whole declaration invalid. The passive policy accepts *recognized
  descriptions*, independently of decoder availability. This is deliberately not
  an assertion that the browser can render every recognized font format/tech.
  Astra should review that distinction against this slice's passive scope.
- Legacy variation format strings normalize to their base format plus
  `tech(variations)`. URL and local descriptions remain in author order, and URLs
  stay unresolved. [Blink's source-value serializer](https://chromium.googlesource.com/chromium/src/+/refs/heads/main/third_party/blink/renderer/core/css/css_font_face_src_value.cc)
  provides primary implementation evidence for quoted `format` and `local`
  values and lowercase technology serialization. Its
  [descriptor parser](https://raw.githubusercontent.com/chromium/chromium/main/third_party/blink/renderer/core/css/parser/at_rule_descriptor_parser.cc)
  also shows case-insensitive legacy variation format recognition and recovery
  to the next comma. Fonts 4 remains authoritative when Blink's support filter
  differs from the passive recognition policy.
- The current style grammar includes `left` and `right`; weight accepts absolute
  values and `auto`, including one or two endpoints; style accepts `auto`, the
  specified keywords, and oblique angle endpoints. Numeric/math parsers are
  shared with the existing exact helpers. Literal degree, grad and turn bounds
  use exact number comparisons. Reversed specified endpoints retain author
  order: Fonts 4 §4.4 swaps **computed** endpoints, and this slice has no font
  selection/computation step.
- Source `!important` descriptors are discarded, including pending names. API
  `setProperty(..., 'important')` is independently retained; the keyframe API
  priority no-op is not reused for descriptors. Last valid source duplicates win,
  and an invalid later declaration preserves the earlier valid value.
- [Fonts 4 §12.1](https://drafts.csswg.org/css-fonts-4/#om-fontface) requires
  `CSSFontFaceDescriptors : CSSStyleDeclaration` and a SameObject `style` with
  PutForwards to `cssText`. The contract declares all current camel/hyphen
  descriptor accessors; the implemented five use the same native block. Legacy
  rule aliases forward to that block; unfinished aliases report named pending
  grammars rather than fabricated values.
- [CSSOM's font-face rule serializer](https://drafts.csswg.org/cssom/#serialize-a-css-rule)
  still hardcodes an older descriptor list and omits `font-display`. This slice
  deliberately serializes all retained completed descriptors through the
  declaration block in declaration order, preserving `font-display`.

## Source ownership and integration

Native shared hunks are confined to `CssRuleType` (type 5), font-face selection
and descriptor-leaf traversal in `CssStyleSheet`, rule serialization,
font-face dispatch in `CssPropertyParser`, and context gates/atomic write guards
in both `CssDeclarationBlock` partials. New rule and descriptor parser/catalog/
value files carry the slice. Two small supporting hunks expose the existing
font-weight component grammar and retain typed descriptors in `CssPropertyValue`.
No executor, query, segment, snapshot, container, or ordinary rule-parser files
are changed.

Browser shared hunks add the descriptor wrapper/cache and reuse the existing
rule declaration implementation in `NativeCssDeclarations.Browser.cs`.
`contract.json` changes only the existing CSSFontFaceRule record and adds
CSSFontFaceDescriptors. That addition uses the existing declaration collection
accessor and participates in the contract's generated native type map. It has
no second accessor class. Preserve concurrent contract records when integrating.

No `.g.cs` has been edited. Integration must regenerate from the merged contract
before compiling the Browser tests. No dotnet, build, test, restore, or emitter
command was executed in this worktree. Only source review, JSON checks and
`git diff --check` were performed. These packets are uncompiled and untested;
the designated integration checkout owns fresh Release/net10 validation after
Astra review, and the final all-framework leg.

## Regression coverage submitted for integration

`CssFontFaceRuleTests` covers static/dynamic descriptor shapes, empty/incomplete/
malformed rules, descriptor leaf behavior, typed passive sources, duplicate/
invalid/unknown declarations, source/API priority separation, context isolation,
lazy target reads, cancellation/reentrant write atomicity, ranges/math, parents,
insert/delete/detach, and mutation identity/stamps. Existing tests expecting
FontFace to remain a blanket context blocker now assert descriptor behavior.

`NativeCssFontFaceTests` covers the exact static descriptor rule from the pinned
Monaco stylesheet, capture-listener `event.target.sheet` access before the target
load listener, the dynamic five-descriptor shape, SameObject and WebIDL brands,
PutForwards, legacy store aliases, missing/pending reads, deletion identity, and
separate imported sheet/rule/style identities. Network assertions require zero
font requests, and the tests make no geometry queries. The existing full Monaco
AMD fixture remains an additional integration check.
