# Native DOM binding boundaries and corrections

This register describes the current `Jint.HtmlParser` / `Jint.Browser` boundary, not defects measured in
the former AngleSharp dependencies. The ownership and scope rules are in
[`Jint.Browser/AGENTS.md`](../AGENTS.md). AngleSharp is retained only for comparison benchmarks.

**The old dependency defect table is not a native conformance inventory.** Its package-version outcomes,
assembly-access limitations and workaround recipes no longer establish current behavior. Do not infer that
every historical case passes simply because the dependency was removed: use the native implementation,
the relevant Browser regression tests and the current WPT exclusions. A new entry needs a spec reference,
a reproducible case and a verified implementation boundary.

### Current boundaries

| Surface | Current implementation and remaining boundary |
| --- | --- |
| Binding generation | `contract.json` explicitly records the JavaScript projection. `BindingContract` and `Emitter` do not extract dependency assemblies. `overrides.json` / `pin.json` are historical provenance, not active inputs. |
| Identity and adoption | `DomRealm` shares a weak wrapper cache per engine and constructor/prototype state per realm. `Document.AdoptionObserver` records an associated document's native node/attribute creation realm before ownership changes; first access otherwise uses the document realm. There is no eager parser-node branding pass and no separate Browser DOM store. |
| DOM queries | `DomSelectors` calls native `SelectorCompiler` / `SelectorMatcher`, with Browser focus, press, target and control facts. Empty-tree calls still compile the selector. `querySelectorAll` is projected through `DomStaticNodeList`; it is not a live collection. |
| Form ownership | `HtmlFormOwner.Of` uses native `HtmlFormState.GetOwner`. Submission inventories walk tree order with that same owner; `form.elements` applies its listed-control filter and excludes image inputs. Do not substitute the latter for the former. |
| Mutation delivery | Native `MutationSubscription` records reach `MutationObserverLane`, which delivers script callbacks at a microtask checkpoint. Resource and custom-element subscriptions are separate trusted consumers; their pending callbacks record/schedule work rather than running script inside mutation. The resource-only `OmitInertCharacterRecords` flag must never filter script observers. |
| CSS and geometry | Native syntax/selectors and a text cascade feed `Styling/NativeCssQuery`. Values do not compute colors, math or units. `ResolvedStyle` uses synthetic box width/height; other reads return text. This is an intentional LightPanda-parity boundary. See [the cascade boundary](../AGENTS.md#where-the-cascade-diverges-from-cssom). |
| Web Animations | [Web Animations Level 1](https://drafts.csswg.org/web-animations-1/) timing, document timelines, playback promises/events and keyframes run on the page's 16 ms frame lane before rAF. Effects do not enter the computed-style cascade; stylesheets create no CSS animations/transitions. `commitStyles()` writes discrete values on connected HTML/SVG targets, without interpolation or additive composition. Keyframe values remain CSS text, and pseudo-elements are limited to supported simple selectors. `Animations/WebAnimationsTests` covers playback, replacement, discrete commits and quiescence. |
| Media | Stylesheets use native CSS media evaluation; `matchMedia` uses `Runtime/MediaQuery`'s subset. Both read page media inputs, but their supported grammars are not identical. |
| XPath | Native XPath preserves namespaces. `BrowserXPathNavigator` deliberately hides namespaces and the namespace axis so unprefixed names match HTML. Prefixed name tests therefore do not match through that Browser cursor. Node sets are snapshots and `invalidIteratorState` remains false; native guards still reject mutation during evaluation. |
| Intersection/resize | Intersection reports a target once, fully intersecting. Resize tracks the synthetic flat model and defers callback-induced changes to another task, rather than implementing a rendering engine's resize loop. |
| Selection | `JsSelection` has one range; anchor/start and focus/end do not preserve backward direction. |
| Custom elements | Browser drains native arrivals at completed mutation and parser-reaction boundaries. Parsed elements are upgraded, not synchronously constructed before their attributes exist. Recording `formAssociated` does not implement `ElementInternals` submission. |
| Script loading | External async/defer classics currently fetch sequentially and run after tokenization in encounter order. Child modules/import maps are unsupported. These are Browser scheduling boundaries, not tokenizer limitations. |
| Dynamic markup | The native session owns parser-time insertion. Secondary documents retain one incremental session across writes. Displayed-document replacement through `open`/`write` is declined and recorded; XML refuses it. See [the driver](../Runtime/Parsing/AGENTS.md#dynamic-markup-insertion). |

The [LightPanda headless target](https://github.com/lightpanda-io/browser) does not require a rendering
engine. Real layout, full CSS computation beyond automation needs, exotic at-rules, DTD-entity edge cases
and exact `document.write` insertion-point corner cases may remain outside scope. This is a scope policy,
not a claim that the native parser lacks all support for those features.

### Modern attribute factories and setters (#3950)

[DOM's attribute factories](https://dom.spec.whatwg.org/#dom-document-createattribute) and
[element attribute setters](https://dom.spec.whatwg.org/#dom-element-setattribute) operate on the actual
native document and element. `DomHostHooks` performs WebIDL argument conversion and calls
`Document.CreateAttribute`, `CreateAttributeNS`, `Element.SetAttribute` and `SetAttributeNS`.
`QualifiedName` owns the native name/namespace checks and HTML ASCII normalization.

There is no constant-name surrogate attribute, field rewriting or external qualified-name validator to
bypass. Keep conversion order in the binding and DOM validation/attribute identity in the native library.
`NameValidationTests` and `NullableStringWriteTests` cover the Browser projection.

### Modern element factories (#3950)

[DOM element creation](https://dom.spec.whatwg.org/#dom-document-createelement) uses
`Document.CreateElement` / `CreateElementNS`; `DomElementFactory` delegates directly.
The native factory chooses namespace from document kind/content type, normalizes HTML names and validates
qualified names. Browser `CustomElementCreation` adds the registry/construction behavior.

Native storage uses `Element`; Browser selects its WebIDL brand from namespace/local name and custom
element state. No external element factory, substitute `AnyNamespaceElement`, reflection or parallel tree
is involved. Clone/import and adoption must retain native identity rules and creation-realm ownership.

### Doctype creation through the native parser factory (#3950)

[`DOMImplementation.createDocumentType`](https://dom.spec.whatwg.org/#dom-domimplementation-createdocumenttype)
converts all three arguments once, in order, then validates DOM's doctype-name predicate.
`DomDocumentTypeFactory` calls `implementation.Document.CreateDocumentType` with the unchanged strings.
It does not round-trip through HTML text or manufacture a scratch node to discover the owner document.

The resulting native `DocumentType` carries the supplied name/public/system identifiers through insertion,
cloning and adoption. `DocumentTypeFactoryTests` exercises the name and identifier boundaries.

### ProcessingInstruction construction

[DOM's `ProcessingInstruction` constructor](https://dom.spec.whatwg.org/#dom-processinginstruction-processinginstruction)
is explicitly enabled by `DomConstructors`, with a required target and optional data.
It calls the native document factory directly; `document.createProcessingInstruction` reaches that same
factory through `DomProcessingInstructions`. Native validation includes supplementary XML Name characters and checks data;
there is no tokenizer-payload rewrite, scratch document or special astral-name adoption path.
`DomConstructorTests` and `NameValidationTests` cover the projection.

### HTML attribute serialization

[HTML serialization](https://html.spec.whatwg.org/multipage/parsing.html#escapingString) is native.
`HtmlScalarSerializer` escapes `&`, nonbreaking space, `<`, `>` and attribute quotes.
`DomHtmlMarkupFormatter` chooses native `HtmlMarkupSerializer` for HTML and `XmlMarkupSerializer` for XML,
passing cancellation and engine checks; it is not an override of an external formatter.

Keep traversal, template contents, raw-text policy and attribute naming in the native serializer.
`HtmlSerializationTests` covers Browser markup surfaces.

### ProcessingInstruction attributes

[DOM's seven attribute-map operations](https://dom.spec.whatwg.org/#interface-processinginstruction)
use the PI's own native ordered map in `ProcessingInstruction.Attributes.cs`.
`DomProcessingInstructionAttributes` supplies WebIDL conversion and bounded-work callbacks, not a Browser
conditional-weak-table map.

Native reads lazily parse data. Attribute writes retain the map while serializing it into character data;
ordinary data edits invalidate that map through native mutation. This also applies to raw native callers:
there is no Browser-only same-value notification workaround. Keep range repair and mutation delivery with
the native operation. `ProcessingInstructionAttributeTests` and `ProcessingInstructionRangeTests` exercise
the binding, copies and ranges; historical external-browser counts are not a current pass/fail census.

### Renderless CSS and shadow hosts

Typed colors/math/transforms/typography, container queries, registrations and keyframe models
are removed by design for [LightPanda parity](../../Jint.HtmlParser/README.md#renderless-css-boundary).
Their at-rules, and page/namespace/counter-style rules, expose only CSSRule and round-trip cssText.
They never affect the cascade. No removed rule interface is installed.

Alignment, gaps and all other catalog properties accept nonempty text rather than grammar-validated
values. Only the small layout shorthand set expands. Named/generic CSSOM accessors still share one
store, receiver checks, null removal, readonly computed views and live invalidation.
CSS.supports checks known/custom names and nonempty values; it is not proof of a value grammar.
Custom properties inherit raw text; small bounded textual var() substitution supports ordinary values.
Whitespace keywords remain usable by text extraction. Colors retain their specified spelling rather
than converting to sRGB; the affected WPT color-serialization cases are explicitly outside this boundary.

The native selector VM implements [shadow stylesheet `:host` and
`:host(...)`](https://drafts.csswg.org/css-shadow-1/#host-selector), including featureless hosts and the
normal-context functional argument. Browser cascade ordering compares encapsulation contexts before
specificity, reversing that order for important declarations. This does not make DOM queries cross shadow
boundaries or match hosts. `SelectorHostTests` and `NativeCssHostTests` cover these cases;
`:host-context()` remains unsupported.

### Sanitizer: sanitize after parsing, not while parsing

`Sanitizer`, `setHTML`, `setHTMLUnsafe`, `getHTML` and `Document.parseHTML` / `parseHTMLUnsafe` implement the
merged [HTML sanitization algorithm](https://html.spec.whatwg.org/multipage/dynamic-markup-insertion.html#sanitize):
the fragment is parsed completely and then `Jint.HtmlParser/Sanitization/HtmlSanitizer` walks it. The
configuration algorithms (canonicalize, valid, modifiers, `removeUnsafe`, `get()` ordering) are native too;
`Dom/Views/JsSanitizer.cs` is only the WebIDL conversion.

The open [whatwg/html#12756](https://github.com/whatwg/html/pull/12756) ("Sanitize while parsing", backed by
all three engines and already reflected in upstream WPT) moves sanitization into the tree builder. Until that
lands and the native tree builder gains a sanitizer hook, these are known differences:

- Text nodes separated by a removed element are **not** coalesced (`<div>a<script>b</script>c` leaves two
  text nodes, `"a"` and `"c"`).
- A removed `is` attribute, or a removed or replaced element, has already created its customized built-in or
  custom element during parsing.
- A declarative shadow root whose `<template>` or `shadowroot*` attribute is later removed has already been
  attached, with the options the removed attributes gave it.
- Replace-with-children around adoption-agency and foster-parenting recoveries hoists children from where
  the unsanitized tree put them.

Also outside the current boundary: `sethtml-xml-document` cases need `attachShadow` in XML documents, and the
`sanitizer-svg-animate` / `sanitizer-inert-document` cases need SVG animation and image loading.

### Geometry interfaces

`DOMPointReadOnly`, `DOMPoint`, `DOMRectReadOnly`, `DOMRect`, `DOMQuad`, `DOMMatrixReadOnly` and `DOMMatrix`
(`Geometry/`) follow [Geometry Interfaces Level 1](https://drafts.fxtf.org/geometry/), with these known
differences:

- `getClientRects()` answers an `Array` of `DOMRect`s rather than a `DOMRectList`: the indices and `length`
  are there, `item()` is not.
- A class extending one of these interfaces gets the base prototype from `super()`: hand-written interface
  objects ignore `new.target`, as every other one in this package does.
- A `DOMMatrix` string initializer is read by the native `CssTransformList`, which refuses `calc()` and other
  math functions even where they would resolve to an absolute length.
