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

SVG 2's [element hierarchy](https://svgwg.org/svg2-draft/types.html#DOMInterfacesForSVGElements)
is recorded in the generated contract; `SVGAElement` retains its manual shape and now inherits
`SVGGraphicsElement`. Value interfaces use hand-written shared shapes, with live-reflection state
in `SvgRealm`'s weak per-element table, not on native nodes. On-demand native readers cache by attribute
string; base-value mutation serializes through the normal native mutation boundary. Animated views
are read-only mirrors, without SMIL. SVG DOM lists retain item identity by index when source text changes,
detach removed items, and clone already-associated items when inserting.

`getBBox` derives basic shapes from attributes or falls back to synthetic layout/zero; it ignores stroke,
clipping, markers and transforms. Outline lengths ignore rectangle corner rounding, approximate ellipses
with 256 segments, and return zero for paths. `getPointAtLength` uses that same outline model.
`getCTM`/`getScreenCTM` compose ancestor transform attributes without CSS transforms or viewport fitting.
Text lengths are zero; character counts use Unicode code points. No rendering, SMIL, path parser or
`fe*` filter primitives are claimed. `Dom/SvgDomTests` and the native `SvgParserTests` pin these boundaries;
the [package limitations](../../docs/packages/jint-browser/limitations.md) list the deferred APIs.

| Surface | Current implementation and remaining boundary |
| --- | --- |
| Binding generation | `contract.json` explicitly records the JavaScript projection. `BindingContract` and `Emitter` do not extract dependency assemblies. `overrides.json` / `pin.json` are historical provenance, not active inputs. |
| Identity and adoption | `DomRealm` shares a weak wrapper cache per engine and constructor/prototype state per realm. `Document.AdoptionObserver` records an associated document's native node/attribute creation realm before ownership changes; first access otherwise uses the document realm. There is no eager parser-node branding pass and no separate Browser DOM store. |
| DOM queries | `DomSelectors` calls native `SelectorCompiler` / `SelectorMatcher`, with Browser focus, press, target and control facts. Empty-tree calls still compile the selector. `querySelectorAll` is projected through `DomStaticNodeList`; it is not a live collection. |
| Form ownership | `HtmlFormOwner.Of` uses native `HtmlFormState.GetOwner`. Submission inventories walk tree order with that same owner; `form.elements` applies its listed-control filter and excludes image inputs. Do not substitute the latter for the former. |
| Mutation delivery | Native `MutationSubscription` records reach `MutationObserverLane`, which delivers script callbacks at a microtask checkpoint. Resource and custom-element subscriptions are separate trusted consumers; their pending callbacks record/schedule work rather than running script inside mutation. The resource-only `OmitInertCharacterRecords` flag must never filter script observers. |
| CSS and geometry | Native syntax/selectors and a text cascade feed `Styling/NativeCssQuery`. Values do not compute colors, math or units. `ResolvedStyle` uses synthetic box width/height; other reads return text. This is an intentional LightPanda-parity boundary. See [the cascade boundary](../AGENTS.md#where-the-cascade-diverges-from-cssom). |
| Canvas | [HTML 2D canvas](https://html.spec.whatwg.org/multipage/canvas.html#2dcontext) has stateful no-op contexts, Path2D, gradients, patterns, ImageData and window-scoped OffscreenCanvas. Hand-written shared shapes follow Geometry; a per-realm weak element table owns contexts, with dimension-only native subscriptions created on demand. No pixels, taint, path hit-testing or font shaping: reads are transparent black and PNG exports are blank, including alpha-disabled contexts. Metrics use half an em per code point plus spacing, with 0.8/0.2 em ascent/descent. Colors support named/hex/RGB/HSL/HWB only; currentColor is black, filters omit URL/drop-shadow, and negotiation stays sRGB/unorm8. Float16 ImageData, workers, structured clone/transfer, ImageBitmap, capture and transferControlToOffscreen are omitted. Encoding and ImageData allocation cap at 16,777,216 pixels; PNG rows stream through zlib without a raster allocation. `Dom/CanvasTests` pins state, conversions, PNG bytes and asynchronous delivery; see the [package limitations](../../docs/packages/jint-browser/limitations.md). |
| Web Animations | [Web Animations Level 1](https://drafts.csswg.org/web-animations-1/) timing, document timelines, playback promises/events and keyframes run on the page's 16 ms frame lane before rAF. Effects do not enter the computed-style cascade; stylesheets create no CSS animations/transitions. `commitStyles()` writes discrete values on connected HTML/SVG targets, without interpolation or additive composition. Keyframe values remain CSS text, and pseudo-elements are limited to supported simple selectors. `Animations/WebAnimationsTests` covers playback, replacement, discrete commits and quiescence. |
| Media | Stylesheets use native CSS media evaluation; `matchMedia` uses `Runtime/MediaQuery`'s subset. Both read page media inputs, but their supported grammars are not identical. |
| XPath | Native XPath preserves namespaces. `BrowserXPathNavigator` deliberately hides namespaces and the namespace axis so unprefixed names match HTML. Prefixed name tests therefore do not match through that Browser cursor. Node sets are snapshots and `invalidIteratorState` remains false; native guards still reject mutation during evaluation. |
| Intersection/resize | Intersection reports a target once, fully intersecting. Resize tracks the synthetic flat model and defers callback-induced changes to another task, rather than implementing a rendering engine's resize loop. |
| Cookie Store | [Cookie Store](https://cookiestore.spec.whatwg.org/#process-cookie-changes) is window-only, without secure-context exposure gating or service-worker subscriptions. Listener-gated snapshots detect net jar changes at page-turn/nested-rendering boundaries and idle wakes, then queue one event per interval; intermediate and identical rewrites are not observable. Results follow the spec's name/value-only dictionary rather than Chrome's extended objects. SameSite/partitioned and a public suffix list are not represented by the default jar. `Navigation/CookieStoreTests` covers operations, events, shared HttpOnly protection and the unused-API cost. See the [package limitations](../../docs/packages/jint-browser/limitations.md). |
| Cache Storage | [Service Workers' caches attribute](https://w3c.github.io/ServiceWorker/#self-caches) uses the context's origin partition, including dedicated workers and creator-inherited blank documents. HTTPS/loopback origins are granted; insecure contexts have no attribute and secure opaque content throws `SecurityError`. `Storage/CacheStorageTests` covers isolation, quotas, retained deleted handles and concurrent page writes. Custom partitions can decline the API; CDP cache clearing is not implemented. |
| IndexedDB | [IndexedDB](https://w3c.github.io/IndexedDB/) uses context-owned, origin-partitioned in-memory storage, including plain HTTP and dedicated workers. Opaque origins reject storage access. Durability hints do not flush to disk; there is no public provider, eviction or CDP storage clearing, and IDB 3.1 additions are omitted. Quotas bound committed data, not transaction-copy allocations. `Storage/IndexedDbTests` covers navigation, isolation, cross-page events, workers and quota rollback. |
| Resource/navigation timing | [Mark resource timing](https://w3c.github.io/resource-timing/#dfn-mark-resource-timing) and [Navigation Timing](https://w3c.github.io/navigation-timing/#sec-PerformanceNavigationTiming) use engine-owned entries and loop-side delivery of transport facts. `Navigation/ResourceTimingTests` covers subresource initiators, frame isolation, TAO and document milestones. DNS/connect phases collapse, TLS/redirect/unload phases are unavailable, Server Timing is empty, and body sizes may be decompressed-size estimates. No speculative fetches or child navigation entries are invented; see the [package limitations](../../docs/packages/jint-browser/limitations.md). |
| Navigation API | [HTML Navigation API](https://html.spec.whatwg.org/multipage/nav-history-apis.html#navigation-api) uses the page's existing `SessionHistory`, with engine-free keys, IDs, origin and serialized state. `Navigation/NavigationApiTests` covers interception/ordering, rejection, disposal, native paths, reload and cross-document identity/origin filtering. Precommit handlers throw `NotSupportedError`; child navigation/history services, visual transitions, persisted scroll restoration and download management are outside the current runtime. Focus reset compares the current focus with the pre-interception focus, rather than tracking every intervening focus change. See the [package limitations](../../docs/packages/jint-browser/limitations.md). |
| Popups and targets | [Window open steps](https://html.spec.whatwg.org/multipage/nav-history-apis.html#window-open-steps) and [choosing a navigable](https://html.spec.whatwg.org/multipage/document-sequences.html#the-rules-for-choosing-a-navigable) create/reuse context pages, including link/form targets and POST bodies. `Popups/WindowOpenTests` covers pending identity, messaging, referrers, close and CDP discovery. Each page has its own engine, so [cross-origin WindowProxy semantics](https://html.spec.whatwg.org/multipage/nav-history-apis.html#windowproxy-getownproperty) apply even to same-origin pages. New auxiliary contexts are refused at `BrowserOptions.MaxPages` / `BrowserContextOptions.MaxPages`: open pages and pending creations share one context cap, `window.open` returns null, and link/form targets do nothing. Named reuse remains allowed at capacity. Trusted contexts default to unlimited; `ForUntrustedContent` defaults to 16 and replaces zero / int.MaxValue with a finite cap. Host `NewPageAsync` fails with `InvalidOperationException` at capacity; closing or failed initialization releases its slot. No UI features. Named lookup checks [HTML familiarity](https://html.spec.whatwg.org/multipage/document-sequences.html#familiar-with) using current immutable origins and the target’s opener chain, including pending popups and opaque origin identity. Unfamiliar names create a separate popup or return null at capacity; they expose no proxy to the existing target. [Allowed-by-sandboxing navigation](https://html.spec.whatwg.org/multipage/browsing-the-web.html#allowed-to-navigate) permits cross-origin navigation among the modeled unsandboxed top-level pages, including through an already acquired proxy after disowning. Full browsing-context groups, COOP and child-frame navigation/sandbox policy remain unsupported. Cross-page transfer lists throw `DataCloneError`. Named child frames retain a recorded same-page fallback. |
| Shared workers | [HTML SharedWorker](https://html.spec.whatwg.org/multipage/workers.html#shared-workers-and-the-sharedworker-interface) uses a locked context registry keyed by constructor origin, resolved URL and name, with opaque owners isolated. Top-level pages using the default worker provider can connect; child-frame/worker exposure, custom providers, data/blob scripts and CDP worker targets are absent. Each shared worker owns a bounded network log rather than a page request log; its client factory receives the worker engine. Startup uses creator referrer settings; subsequent requests use the worker script response URL and Referrer-Policy, independent of owner navigation/closure. Pump failures report to current owners. Immediate last-document termination also applies to `extendedLifetime`; port closing alone retains ownership. `Workers/SharedWorkerTests` covers sharing, lifecycle, error channels, interfaces and budgets; see the [worker guide](../../docs/guide/web-apis/workers.md). |
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

[Nested grouping at-rules](https://drafts.csswg.org/css-nesting-1/#nesting-at-rules)
inside style rules (`@media`, `@supports`, `@layer`) also remain opaque CSSRule objects.
Their complete text is retained and an optional native parse diagnostic records
`css/unsupported-nested-at-rule`; their contents do not cascade. Supported declarations
around them and other rules still apply, including during `getComputedStyle` and `innerText` reads.

Visibility/layout declarations use a small token grammar gate before entering the cascade; invalid
keywords, primitive lengths/numbers and shorthand arity cannot replace earlier valid declarations.
CSS-wide keywords and custom text remain accepted. Ordinary var() values defer validity until
substitution; invalid results use inheritance/initial defaulting, without falling back to an earlier
winner. Inline-only extraction uses the same declaration filtering and importance.
See [CSS Cascade §4.1](https://drafts.csswg.org/css-cascade-5/#declared) and
[CSS Variables §3.1](https://drafts.csswg.org/css-variables-1/#invalid-variables).
Recognized math/sizing functions retain text without full argument validation or evaluation; other
catalog properties still accept nonempty text. Only the small layout shorthand set expands. Named/generic CSSOM accessors still share one
store, receiver checks, null removal, readonly computed views and live invalidation.
CSS.supports checks known/custom names and nonempty values; it is not proof of a value grammar.
Custom properties inherit raw text; small bounded textual var() substitution supports ordinary values.
Whitespace keywords remain usable by text extraction. The cascade keeps colors as declared text; only the
CSSOM resolved value (`getComputedStyle`) and DevTools' computed style serialize absolute sRGB colors and
`currentcolor` as `rgb()`/`rgba()`. Shorthands such as `border-color` and `background` and system colors
stay declared text.

The native selector VM implements [shadow stylesheet `:host` and
`:host(...)`](https://drafts.csswg.org/css-shadow-1/#host-selector), including featureless hosts and the
normal-context functional argument. Browser cascade ordering compares encapsulation contexts before
specificity, reversing that order for important declarations. This does not make DOM queries cross shadow
boundaries or match hosts. `SelectorHostTests` and `NativeCssHostTests` cover these cases;
`:host-context()` and `:unchecked` remain unsupported and are rejected during selector compilation.
DOM queries expose a catchable `SyntaxError`, including on empty trees; forgiving `:is()` / `:where()`
lists discard these branches, following [Selectors error handling](https://drafts.csswg.org/selectors/#invalid).
`UnsupportedSelectorTests` covers script catches through the public page API.

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

XML entity replacement work (XML 1.0 [§4.4](https://www.w3.org/TR/xml/#entproc)) is capped at
10,000,000 UTF-16 units by default. Browser XML navigation, `DOMParser` and XML fragments also cap
input and atomic tokens by `MemoryLimit / 2` units when a finite memory budget is configured;
entity work uses the smaller of that ceiling and the default. Parser limits are resource failures,
not XML syntax errors or `parsererror` documents. Allocation constraints can fail before these ceilings.
Standalone parser callers can explicitly select `ParseLimits.Unbounded` for trusted XML.
