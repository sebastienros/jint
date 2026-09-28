# Agent instructions: the browser package

> **Read this when:** You are touching anything under `Jint.Browser/`, the binding generator under
> `tools/dom-bindings/`, or its contract.
>
> This is one of the co-located instruction files indexed from the repository-root
> [`AGENTS.md`](../AGENTS.md). Read that first: it carries the build and test commands, the branch to
> target, and the conventions that apply to every file in the repository.
> [`docs/design/headless-browser.md`](../docs/design/headless-browser.md) records the original design;
> its dependency and parser-thread descriptions predate the native-parser migration.

### The principle this package is checked against

**Jint.HtmlParser is the native document foundation; Jint.Browser supplies browser semantics over its
output.** Tokenization, tree construction, DOM storage, CSS syntax/CSSOM/cascade machinery, selectors,
XPath, XML and serialization belong to the native foundation, not to a replacement tree inside Browser.
The current `Styling/NativeCssQuery` composes native CSS models, selectors and value algorithms into the
Browser cascade; bindings, page state, events, resource loading, observer delivery and layout-free geometry
remain Browser's responsibility. The binding layer projects native identities onto Jint's shapes, without
a reflection trampoline.

- **Parser performance is paramount.** Consume parser output without adding per-node Browser work during
  parsing: no eager wrapper construction, creation-realm tree walks or generic notification listeners just
  to rediscover facts the parser already knows. The resource watch in
  `Runtime/Parsing/ParserDriver.NativeResources.cs` sets the internal
  `MutationSubscription.OmitInertCharacterRecords`: character-node changes outside HTML/SVG `style` and
  `script` cannot start a resource load or change a sheet. This is a resource-host optimization, **never**
  an option for a script-visible `MutationObserver`.
- **Record creation realms lazily.** `Dom/DomRealm.cs` associates a document with its realm and installs
  `Document.AdoptionObserver`. Its weak `INodeAdoptionObserver` captures a node's or attribute's creation
  realm just before the native owner changes. A previously unseen parser node otherwise takes its
  document's realm. Do not replace this with an eager pass over every parsed node or a scan on a known
  wrapper read. These hooks are internal; `Jint.HtmlParser/Parsing/AssemblyInfo.cs` grants Browser access.
- **The feature-parity target is [LightPanda](https://github.com/lightpanda-io/browser), not a rendering
  engine.** Work beyond that headless-automation scope may remain unsupported: real layout, full CSS
  computation beyond automation's needs, exotic at-rules, DTD entities and exact `document.write`
  insertion-point edge cases. State the boundary explicitly; this is not permission to silently change
  supported behavior, lose identity or bypass execution limits.
- **Keep one answer across bindings, selectors, forms, events and automation.** Fix native algorithms in
  the native library and browser policy in Browser. Use existing factories and internal integration hooks
  rather than private reflection or a parallel DOM store.
- **A correction needs a specification citation, regression coverage and a divergence entry.** Record
  current limitations in [`Dom/divergences.md`](Dom/divergences.md); remove exclusions only when their tests
  establish that they are stale. Historical dependency defects are not evidence of a current native defect.
- **Do not add AngleSharp back.** Its packages are comparison controls in `Jint.Benchmark` only, not
  production, test or binding-generator dependencies. `ParserDependencyTests` enforces the boundary.

### What is generated and what is hand-written

`tools/dom-bindings/Jint.Browser.BindingGenerator/BindingGenerator.cs` loads `contract.json` through
`BindingContract.Load(...).ToModel()` and passes that model to `Emitter`. It does not load dependency
assemblies or extract their attributes. The output, `Jint.Browser/Dom/Generated/*.g.cs`, is **checked in**:
binding changes are explicit contract changes followed by regeneration and review.

| Generated | Hand-written |
| --- | --- |
| One `JsObjectShape` per generated interface: operations, attributes, constants, `constructor`, `@@toStringTag` | Wrapper classes (`DomObject`, `DomNodeObject`, `Collections/`) |
| `DomInterfaces`: parent, `EventTarget` root, wrapper kind and interface-object availability | `DomRealm`: per-realm prototypes/interface objects, shared engine-wide identity cache |
| Collection accessors stored in the contract | `DomInterfaceObject`, `DomBindings`, `DomConvert`, `DomHostHooks`, `DomFailures` |
| `DomTypeMap`'s candidate list, most derived first | `DomTypeMap.For`, native element-brand selection and its type cache |
| `DomEnums`, both directions, for string enumerations | `DomManualShapes`, `DomManualInterfaces`, `DomConstructors`, and native adapters called by contract bodies |

**Never hand-edit a `.g.cs`.** `DomBindingsStalenessTests` runs the same emitter in memory and fails on any
difference; `JINT_DOM_BINDINGS=update` writes the difference back.
[`tools/dom-bindings/README.md`](../tools/dom-bindings/README.md) has both regeneration commands.
`overrides.json` and `pin.json` preserve historical decisions and provenance; neither drives generation.

### Where the cascade diverges from CSSOM

`Dom/Views/CssCascade` is the shared on-demand query entrance over `Styling/NativeCssStyleSheets` and
`NativeCssQuery`. Native CSS models own parsed rules and declarations; Browser supplies author/UA sheets,
media and selector state, and adapts computed values to its geometry. Do not restore the old whole-declaration
fallbacks or the ten-property initial-value patch.

| Surface | Current implementation and boundary |
| --- | --- |
| Defaults and cascade | `NativeCssBrowserDefaults` supplies supported HTML UA rules. `NativeCssQuery` handles initial/inherited values and computes requested properties lazily, rather than eagerly computing unrelated paint or sizing values. |
| Resolved values | `Dom/Views/ResolvedStyle` applies CSSOM's resolved-value layer to the synthetic flat model: supported width/height, edge percentages and transform reference boxes use that model, not real layout. Missing positioned, SVG or other geometry dependencies raise `CssIncompleteGrammarException`; do not invent metrics. |
| Computed declaration | `ReadOnlyStyleDeclaration` is a read-only Browser view, not a mutable detached stylesheet declaration. Preserve mutation/read witnesses when reusing queries. |

**Rule-usage coverage observes the same native rule identities the cascade matched**, independently of
which declaration won. `Dom/Views/CssRuleUsage` has a static arming switch; `CssCascade.Traversal.Of` records
matches while tracking is active, and the tracker can sweep the document and shadow trees. Keep new cascade
consumers on this shared path so the `CSS` domain's coverage does not silently miss them
([`DevTools/AGENTS.md`](DevTools/AGENTS.md)).

### DOM §7's XPath, and CSSOM's `CSS`

These Browser-facing objects are hand-written in `Dom/Views/` beside `DOMParser`; the `Document` XPath
members are explicit binding-contract bodies. The htmx fixture exercises both XPath and `CSS.escape`.

- **XPath uses `Jint.HtmlParser.NativeXPath` and its guarded `NativeXPathNavigator`**, backed by
  `System.Xml.XPath`. `BrowserXPathNavigator` adapts that cursor; cancellation, read validation and result
  work accounting still flow through the native navigator.
- **Namespaces are ignored by the Browser cursor, which makes `//div` match HTML elements.** This is
  Browser's policy only: the native navigator preserves namespaces and its namespace axis. A prefixed
  name test such as `svg:circle` can compile with the page's resolver, but matches nothing through the
  namespace-ignoring Browser cursor.
- **A node set is materialized at evaluation.** `invalidIteratorState` is always `false`; subsequent
  mutation does not invalidate the returned iterator. Native read guards still reject invalidation during
  evaluation. Do not confuse the snapshot policy with permission to ignore those guards.
- **`CSS` is a namespace object, not an interface**: no constructor or interface prototype, `[object CSS]`.
  `escape` implements CSSOM's serialize-an-identifier; `supports` calls native
  `CssSupports.EvaluateCondition` / `EvaluateDeclaration` with bounded work. Pending grammar answers false;
  a support query neither executes a stylesheet nor matches against the DOM.

### The bindings have a file of their own

The checked-in contract, conversion rules, wrapper identity and shape discipline are
[`Dom/AGENTS.md`](Dom/AGENTS.md). The rule to carry across: **never hand-edit `Dom/Generated/`**; regenerate
from the contract, and record supported behavior and remaining divergences against the native implementation.

### The events bridge has a file of its own

Every script-visible event is a Jint `Event` dispatched through the engine's tree-aware dispatcher, at the
algorithm points Browser owns, not through a second native event bus. The contract routes `click`, `focus`,
`blur`, `form.reset`, `document.activeElement` and `document.hasFocus` to Browser semantics;
`document.createEvent` reaches `Events/LegacyEventCreation`. Activation, handler reconciliation, keyboard
input and editing are [`Events/AGENTS.md`](Events/AGENTS.md).

### The page runtime is a file of its own

`Page`, its loop, the `Window` installer, navigation, forms, history, cookies, storage and workers are
[`Runtime/AGENTS.md`](Runtime/AGENTS.md). **One thread owns a page's engine and mutable DOM**; operations
that touch them are mailbox requests, and neither a `JsValue` nor a native DOM node may escape in their
returned tasks. Native HTML parsing also runs on that loop. Cooperative parser yields are not permission
to run unrelated page tasks; resource waits use the pump described in
[`Runtime/Parsing/AGENTS.md`](Runtime/Parsing/AGENTS.md#the-parser-driver-and-the-baton).

### The observers, and when each of them delivers

`Observers/` holds three observer kinds. **The delivery lane is part of each one's contract.**

- **`MutationObserver` delivers at a microtask checkpoint.** Native `MutationTracking` matches
  inclusive-ancestor registrations, subtree/filter options and per-observer old values, including
  detached-subtree transients. `JsMutationObserver` owns a `MutationSubscription`; its internal
  `PendingRecord` callback only enlists the observer in `MutationObserverLane`. The lane posts one
  `EventLoopJobKind.Microtask` per batch. **Only delivery invokes script**, never the native mutation stack.
  `TakeRecordsForDelivery` clears transients; `takeRecords()` drains records without withdrawing the
  scheduled delivery, because that delivery still owes transient cleanup. `disconnect()` unregisters,
  clears records and withdraws from the notify set. A registered target retains its subscription and
  callback; the pending lane retains queued observers until delivery or disconnection.
- **`IntersectionObserver` and `ResizeObserver` deliver as tasks**, through a zero-delay `ObserverTask`.
  These rendering-adjacent callbacks must follow the turn's promises, and be visible to
  `Page.WaitForIdleAsync`.
- **`IntersectionObserver` reports each observed target once, fully intersecting.** Never intersecting
  would leave lazy lists permanently empty. `root`, `rootMargin` and `thresholds` are parsed and reflected
  but do not change that policy. Entries use the same flat boxes as `getBoundingClientRect`, through
  `Layout/DomRects`; the rectangles are plain objects, not `DOMRectReadOnly` instances.
- **`ResizeObserver` tracks changes in the flat model**, not just initial size. `ResizeObserverLane`
  checks at page-turn boundaries and in nested pumps, shares a size-only query per check/delivery, and
  schedules a task only for changed dimensions. Queries visit observed subtrees, visibility ancestors
  and needed flex siblings, not unrelated branches; measurements are captured before callbacks.
  `PageLayout` reuse requires a current Browser invalidation identity; mutation scopes and unknown writers
  force fresh queries. No layout mutation observer is installed: CSSOM, ancestor classes and viewport
  changes must work too. Idle wakes do not scan; pages with no resize observers allocate no lane.
  Callback-caused changes wait for another task, not a depth-limited resize loop. All three box options use
  the same synthetic dimensions.

The five observer interface objects are hand-written shapes behind `HostInterfaceObject`.
`Views/HostInterfaceDisciplineTests` holds them to the same shape and property-attribute rules as generated
interfaces.

`Dom/Views/` also supplies browser-facing `DOMParser`, `XMLSerializer`, `Selection` and
`MediaQueryListEvent`, plus adapters for ranges and traversal:

- **`DOMParser` uses native parsers**: `HtmlParserSession` with scripting disabled for HTML,
  `XmlDocumentParser` for XML. Only `MarkupParseException` becomes a `parsererror` document; cancellation
  and resource failures remain failures. `XMLSerializer` uses native `XmlMarkupSerializer` over the same
  identities.
- **A DOMParser document runs no script and loads no resources**: it has no page parser driver.
  Navigations and frame loads have their own driver and policy.
- **`Selection` has no direction**: its anchor is always the range's start.

### Emulation, and the media environment it moves

**`Runtime/PageMediaEnvironment` is the page's immutable media input**, replaced through
`PageRuntime.SetMedia`. It carries viewport, media type, pointer capability, scripting and preferences.
Every retained `MediaQueryList` recomputes against the whole new value and fires a real
`MediaQueryListEvent` only if its answer changed. No window `resize` is synthesized.

`Styling/NativeCssStyleSheets.Browser.cs` builds a native `CssMediaEnvironment` from those values for
stylesheet queries. `matchMedia` uses `Runtime/MediaQuery`; both receive page state, not process-global
preferences. **Shared inputs do not imply identical grammars**: `Runtime/MediaQuery` implements its own
headless subset, whereas stylesheet media conditions use native CSS parsing/evaluation. Preserve the page's
change-event scheduling rather than replacing the evaluator on the assumption that the two are equivalent.

An `Emulation` command writes the page's `Runtime/EmulationState`, not protocol-target state: overrides
outlive the document. Viewport, media, touch, focus, geolocation, user agent and hardware concurrency can
affect the loaded document; time zone, locale and script execution affect the next document. Each command
states its boundary, including accepted no-ops.

`NavigatorInstaller` and `TouchEmulation` add accessors without discarding the engine's shared shapes.
Touch emulation controls detection; `Input.dispatchTouchEvent` can deliver input regardless.
`EventHandlerContentAttributes.Reconcile` checks scripting policy before compiling a handler. The parser
uses `HtmlParseOptions.ScriptingEnabled`, and the driver's script preparation and module paths also check
the policy. `Runtime.evaluate` is unaffected.

### Custom elements, and where a reaction actually runs

`CustomElements/` owns the JavaScript registry, constructors and reaction delivery over native elements.
Browser reaction records live in a `ConditionalWeakTable` keyed on the element; the native DOM also carries
custom-element registry identity and HTML state. A page that never mentions `customElements` creates no
Browser registry.

- **The Browser registry is per page engine**, so definitions do not survive navigation. `define` reads
  the constructor's configuration once. `disabledFeatures: ['shadow']` is honored; recording
  `formAssociated` does not by itself implement `ElementInternals` or a custom submission value.
- **A valid undefined custom-element name has the `HTMLElement` brand**, not `HTMLUnknownElement`.
  `DomTypeMap.Native.cs` selects it from the native namespace/local name.
- **Three creation paths end in the registered constructor.** Contract bodies for `createElement` and
  `createElementNS` call `CustomElementCreation`; `new MyElement()` reaches
  `DomInterfaceObject.Construct`; parser-created elements are upgraded. Clone/import preserve the
  element's `is` value, not merely the possibly different `is` content attribute.
- **The construction stack makes `super()` return the element being upgraded**, rather than a second
  element, and refuses a constructor that reaches the base twice.

**Native notifications are arrivals, not reaction delivery.** `CustomElementRegistry.Tree` subscribes to
child-list changes on documents/shadow roots and separately to attributes on each tracked element, including
detached ones. `PendingRecord` queues subscriptions and schedules work; it runs no author code.
`FlushNativeMutations` translates records, and `Drain` invokes reactions on the page loop.
`DomFailures.GuardMutation` calls `CompleteMutation` after the complete native operation, so generated
mutators drain reactions before returning to script. Each drain takes the current element queue and leaves
a fresh one for nested callbacks. Do not restore the old inline-notification drain or an attribute-service
side channel.

**Parser-created custom elements are upgraded, not synchronously constructed before attributes exist.**
The native session can yield `CustomElementReactions` after a potential custom-element insertion; the driver
drains before continuing. `UpgradeParsedElements` remains a fallback at script and parse-end boundaries,
not the only opportunity to upgrade. Keep the remaining construction-timing differences explicit.

### The protocol layer has a file of its own

Page targets, page-level domains and the request log are [`DevTools/AGENTS.md`](DevTools/AGENTS.md).
**A domain reads its target's current runtime per command and never caches an engine; a `JsValue` never
leaves the page loop.**

### The obstacle course, and what a red fixture means

`Jint.Tests.Browser/Fixtures/` holds offline pages built from vendored libraries, served over a real socket
and driven through the public `Page` API; protocol cases also use PuppeteerSharp and Playwright for .NET.
[`Fixtures/README.md`](../Jint.Tests.Browser/Fixtures/README.md) is the inventory and `FixtureInventoryTests`
holds it to the corpus.

- **Assert the DOM end state and the error sink.** A framework that threw halfway through can still render
  something. Expect `Page.Errors` to be empty unless the fixture explicitly names the intended error.
- **Never delete or quietly ignore a failing fixture.** Record a `needs triage` row with the failing
  assertion and diagnosis, and mark the case `[Explicit("<fixture>: ...")]`. The inventory test requires
  the two sets to agree, just as the WPT exclusion table must agree with its corpus.

### The seams promoted later

The pressure to promote and the table of promoted seams are
[`Jint.Browser.Tool/AGENTS.md`](../Jint.Browser.Tool/AGENTS.md). `Jint.Browser.Tool` and `Jint.Browser.Mcp`
take **no** `InternalsVisibleTo` grant: what they cannot reach is what an embedder cannot reach. Publish a
necessary `Page` seam over existing internals rather than granting either friend access. **A target is a
selector or a `ref=`**; `Runtime/ElementLocator` decides.

`Jint.Tests.Browser/Verify/PublicApiTest.verified.txt` is the reviewable public API baseline.
**The public Browser API does not expose native DOM nodes**, which is why `Page.SubmitFormAsync` takes a
selector. `DomBindings`, `DomRealm`, `DomInterfaceDefinition` and `DomHostHooks` remain internal.
Promote a seam only when a concrete consumer establishes its contract, not merely to avoid the existing
native friend-assembly boundary.

### Accessibility and extraction have a file of their own

The accessibility tree and text/markdown extractors are
[`Accessibility/AGENTS.md`](Accessibility/AGENTS.md). **Nothing there runs the page's script or needs a box.**
