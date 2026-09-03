# Agent instructions: the browser package

> **Read this when:** You are touching anything under `Jint.Browser/`, the binding generator under
> `tools/dom-bindings/`, or its override table.
>
> This is one of the co-located instruction files indexed from the repository-root
> [`AGENTS.md`](../AGENTS.md). Read that first — it carries the build and test commands, the branch to
> target, and the conventions that apply to every file in the repository. Nothing below is repeated there.
> The design this implements is [`docs/design/headless-browser.md`](../docs/design/headless-browser.md):
> §4 for the bindings, §3 for the page runtime, §5 for events, §6 for the parse.

### The principle this package is checked against

> Jint should add value to AngleSharp without competing too much.

That is the project founder's guidance for the whole headless-browser campaign, and it decides arguments here
rather than merely decorating them. **AngleSharp is the parser, the DOM and the CSSOM; nothing in this package
re-implements any of them.** What Jint owns is the binding layer — a projection built on the engine's own
shape and layout machinery instead of a reflection trampoline — and the output is deliberately shaped so that
[AngleSharp.Js](https://github.com/AngleSharp/AngleSharp.Js) could adopt it without adopting anything else
here. Three consequences bind every change:

- **Every AngleSharp behaviour that disagrees with the DOM standard is reported upstream and recorded below,
  never worked around silently.** A workaround in the binding hides a defect from the project that can fix it,
  and makes the next reader believe the standard says what AngleSharp does. The one thing a wrapper may do is
  keep *its own* contracts coherent — the `dataset` name filter below is the worked example, and it says so.
- **No document or README sentence positions this as a rival DOM stack.** It is "AngleSharp + Jint".
- **A seam that proves useful is offered, not hoarded.** The tree-aware event dispatcher the engine grew for
  this package (`Jint/WebApi/Events/EventDispatch.cs`) knows nothing about a node; it asks the target. The
  same is true of the generator: it reads AngleSharp's attributes and emits against Jint's public shape API.

### What is generated and what is hand-written

`tools/dom-bindings/Jint.Browser.BindingGenerator` loads the two pinned AngleSharp assemblies through
`System.Reflection.MetadataLoadContext` and emits `Jint.Browser/Dom/Generated/*.g.cs`. The output is
**checked in** for the reasons `tools/devtools-protocol` gives: reviewable diffs, an analyzer-free build, and
a swap to a source generator later is mechanical.

| Generated | Hand-written |
| --- | --- |
| One `JsObjectShape` per interface — operations, attributes, constants, the `constructor` slot, `@@toStringTag` | The wrapper classes (`DomObject`, `DomNodeObject`, `Collections/`) |
| The registry (`DomInterfaces`): every interface, its parent, whether it roots at `EventTarget`, its wrapper kind | `DomRealm` (per-engine prototypes, interface objects, the wrapper cache) |
| A `DomCollectionAccessor` per collection interface, from `[DomAccessor]` | `DomInterfaceObject`, `DomBindings`, `DomConvert`, `DomHostHooks`, `DomFailures` — the invoker every generated body is wrapped in, so an AngleSharp refusal is a `DOMException` and no `catch` is ever generated |
| `DomTypeMap`'s candidate list, most derived first | `DomManualShapes` — the shapes the generator cannot express |
| `DomEnums`, both directions, for the WebIDL string enumerations | `DomTypeMap.For` and its per-`Type` cache |
| — | `DomManualInterfaces` and `DomConstructors` — the interface AngleSharp has no `[DomName]` for (`HTMLFrameSetElement`) and the one WebIDL really does give a constructor (`Document`) |
| — | `DomSelectorMembers` and `DomNodeMembers` — the five members whose *failure* has to be WebIDL's rather than AngleSharp's, and the one (`getRootNode`) AngleSharp has no `[DomName]` for |
| the default style sheet's `display` rules | HTML's rendering section gives `display: block` to `section`, `article`, `nav`, `aside`, `header`, `footer`, `main`, `figure`, `figcaption`, `details`, `summary`, `dialog`, `hgroup` | no rule at all, so every one of them falls through to CSS's initial value and `getComputedStyle` reads `inline` |
| a longhand nothing declared, through `getComputedStyle` | CSSOM's *resolved value*: every supported longhand answers, and a property nothing declared answers its initial value | the empty string, which read every element of every page as hidden to an automation client (`style.visibility !== "visible"` is where Playwright's actionability check ends). `Dom/Views/ResolvedStyle` is the exception this bought — **ten** properties, and it argues which ten. Everything else is still the declared cascade, a declaration always wins, and `length`/`item(i)` stay the declared set |
| a relative length through `getComputedStyle` | the used value in `px` for `width`/`height`, resolved against the containing block; the percentage *kept* in the computed value of `min-width`, a margin and a padding | `px` against the **viewport** for every one of them, and against its *width* whichever axis the property is on — so `height: 50%` is half the window's width. `Runtime/PageRenderDevice` is the device that makes any of it computable: with none registered AngleSharp.Css raises `ArgumentException` rather than skipping the declaration, and one `width: 100%` rule took `getComputedStyle` **and every box query** down with it ([#3730](https://github.com/sebastienros/jint/issues/3730)). `ch` and `ex` have no conversion at all and still raise, which is why `Dom/Views/CssCascade` is the one guarded door all four callers come through |

**Never hand-edit a `.g.cs`.** `DomBindingsStalenessTests` runs the same emitter in memory and fails on any
difference; `JINT_DOM_BINDINGS=update` writes the difference back, which is also the shortest regeneration
path after an `overrides.json` edit. `tools/dom-bindings/README.md` has the command-line one and the rule that
**a pin bump is a code change**, not a configuration edit.

### The bindings have a file of their own

How AngleSharp's attributes are read as WebIDL, the override table, the conversion table and its divergences in
both directions, wrapper identity and the shape discipline are [`Dom/AGENTS.md`](Dom/AGENTS.md). The one rule to
carry across without opening it: **never hand-edit a file under `Dom/Generated/`**; regenerate with
`JINT_DOM_BINDINGS=update`, and report an AngleSharp divergence upstream rather than working around it.

### The events bridge lives with the runtime

Every script-visible event is a Jint `Event` dispatched through the engine's tree-aware dispatcher, at the
algorithm points the package owns (design doc §5) — never AngleSharp's own bus, which holds nothing a script
registered. What that costs the binding is the `skip` and `additions` rows above: `click`, `focus`, `blur`,
`form.reset`, `document.activeElement` and `document.hasFocus` are all AngleSharp members that do nothing
useful, so they are skipped and re-declared — and `document.createEvent`, whose AngleSharp `Event` must never
reach script, is re-declared against Jint's in `Events/LegacyEventCreation`. The behaviour behind them — which algorithm point raises which
event, what activation means with no layout, and why the handler content attributes need no notification from
AngleSharp — is [`Runtime/AGENTS.md`](Runtime/AGENTS.md#the-events-bridge).

### The keyboard, and the editor under it

The other half of the events bridge, and it lives here rather than beside the rest of it only because
`Runtime/AGENTS.md` has no room left.

**`Input.dispatchKeyEvent`'s four types are three questions**, and `Events/InputDispatcher.DispatchKey` is
where the answers are: which event fires, whether a `keypress` follows, and whether a character may be
inserted. `keyDown` fires `keydown` and — for a key that produces text — `keypress`, then runs the whole
default action; `rawKeyDown` fires `keydown` and runs it **without the insertion**, because that is what every
client sends for a key whose character is coming separately or not at all, which is every editing key; `char`
is that character alone; `keyUp` fires `keyup` always. Modifier state is the client's, never this package's:
each event carries its own bit field.

**The editor is a string and two offsets, and the direction is load-bearing.** `Events/TextEditing` splices a
control's value; <kbd>Shift</kbd> extends from the *anchor*, so `selectionDirection` decides which offset moves
and a selection dragged back through its anchor flips it. `ArrowUp`/`ArrowDown` are line moves computed from
the newlines in the value, exact here and not in a browser: nothing wraps, so a visual line and a logical one
are the same. `maxlength` bounds an insertion and nothing else, because HTML applies it to what a *user*
enters. **`change` fires from two places** — the focus update steps on the way out, and <kbd>Enter</kbd> in a
single-line control, which commits the value and re-arms the snapshot so a later blur does not fire again.

**`contenteditable` is light and the boundary is one text node.** `Events/ContentEditing` splices a `Text`
node's data; nothing splits, merges or inserts an element, so <kbd>Enter</kbd> there does nothing rather than
something structural and wrong. The caret is the document's own `Selection`, so a page reading
`getSelection().focusOffset` is told where typing goes. AngleSharp's `IsContentEditable` cannot be used for
any of it — it answers `false` for `<div contenteditable>` — and the divergence table below records why.

### The page runtime is a file of its own

`Page`, the loop that owns its engine, the `Window` installer, navigation, forms, history, cookies, storage
and workers are [`Runtime/AGENTS.md`](Runtime/AGENTS.md). The one rule to carry across the boundary without
opening it: **one thread owns a page's engine and its DOM**, every public `Page` member is a mailbox request,
and nothing belonging to an engine — a `JsValue`, an AngleSharp node — may be in the task it answers.

### The observers, and when each of them delivers

`Observers/` holds three. **Each delivers on a different lane, and the lane is the design.**

- **`MutationObserver` delivers on the engine's job queue — the microtask checkpoint.** The *records* are
  AngleSharp's: `DocumentExtensions.QueueMutation` already walks a mutated node's inclusive ancestors, matches
  each against the registered observer list, honours `subtree` and `attributeFilter`, and clears `oldValue`
  for an observer that did not ask for it. What it has no answer for is *when*, and the reason is the
  [parser driver](Runtime/Parsing/AGENTS.md#the-parser-driver-and-the-baton): its `MutationHost` schedules through an
  `IEventLoop` service, **nothing in AngleSharp implements one**, and `EventLoopExtensions.Enqueue` on a null
  loop runs the action *inline* — so out of the box the callback fires synchronously inside `appendChild`.
  Registering an event loop to fix that would put a second scheduler under a parse whose hand-offs the baton
  already owns, and every one of its turns would land on whichever thread AngleSharp resumed on. So the
  inline call is used as the
  **arrival** of a record and nothing else: `JsMutationObserver` parks it and `MutationObserverLane` puts one
  job on the engine's queue per batch. Ordering then falls out of a plain enqueue, and
  `Observers/MutationObserverTests` pins it. Lifetime falls out too, and it is DOM's own rule: a *connected*
  observer is held by the document (AngleSharp's `MutationHost` holds the callback, which holds the wrapper),
  and a disconnected one is held only by the notify set until its records are taken — so an observer a page
  dropped with nothing queued collects together with its callback.
- **`IntersectionObserver` and `ResizeObserver` deliver as a *task*** — a zero-delay timer entry
  (`ObserverTask`) — because both belong to update-the-rendering and a microtask would run before the promises
  of the same turn. It also makes the delivery visible to `Page.WaitForIdleAsync`.
- **Both are stubs, and the shape of the lie is the point.** Each observed target is reported exactly once,
  fully intersecting or at its own size, and never again, because nothing here can change what a box is.
  "Never intersecting" would stop every lazy list and reveal-on-scroll animation dead, and the initial resize
  notification is the one a component uses to measure itself when it mounts. `root`, `rootMargin` and
  `thresholds` are parsed, validated and reflected exactly as the specification says and change nothing.
  **The rectangles are real numbers now** — the flat box model gives every element a row, and an entry
  reports the target's own box through the same `Layout/DomRects` factory `getBoundingClientRect` answers
  from, so the two agree. They are still **plain objects, not `DOMRectReadOnly` instances**: the eight
  members are there and the interface object is not, and `Layout/DomRects` says what adding one would cost.

None of the five interface objects is generated, so they are hand-written `JsObjectShape`s behind
`HostInterfaceObject`, and `Views/HostInterfaceDisciplineTests` holds them to the same two rules
`DomPrototypeTests` and `WebIdlPropertyAttributeTests` hold the generated ones to.

`Dom/Views/` is the same story for the interfaces a *browser* supplies rather than the DOM — `DOMParser`,
`XMLSerializer`, `Selection`, `MediaQueryListEvent` — plus the members that make the generated `Range`,
`TreeWalker` and `NodeIterator` usable. Three things there are worth knowing:

- **`DOMParser`'s XML half is `AngleSharp.Xml`**, referenced for that and nothing else; writing an XML parser
  here instead is the one thing this package is not for. It is deliberately **not** in `pin.json` — the
  generator reads two assemblies and projects no interface from this one. A failed parse answers the
  `parsererror` document the standard prescribes, which is what a page tests for.
- **A parsed document cannot run anything**: its parser gets a browsing context of its own with no scripting
  service and `IsScripting` false, so a `<script>` in the input is an element with text and nothing more.
- **`Selection` has no direction**, because direction comes from which end a user dragged from: the anchor is
  always the range's start.

### Emulation, and the media environment it moves

**`Runtime/PageMediaEnvironment` is the one value every media query is answered from**, and
`PageRuntime.SetMedia` is its only writer. It holds the viewport, the emulated media type, whether the
primary pointer is coarse, whether the document's own scripts run, and the features a client emulated — as
one immutable value, swapped whole. That is not tidiness: a query's answer can depend on the viewport *and*
the media type *and* a preference, so a change that moved two of them has to reach a `change` listener once,
with both already in place. Every `MediaQueryList` the page holds then recomputes and fires a real
`MediaQueryListEvent` — `e => e.matches` is how the listener is written — only if its own answer moved. No
`resize` fires at the window: HTML fires that from update-the-rendering, and there is none.

**The Level 5 preference features are the page's own answer, not AngleSharp.Css's**, and they had to be: that
library evaluates `width` and its kind, has no notion of `prefers-color-scheme`, `forced-colors`, `hover` or
`pointer` at all, and its own `CssMediaQueryList.ComputeMatched` is a stub answering `false` for every query.
`PageMediaEnvironment.ValueOf` is the table, and the one place that will delegate the day it grows them.

**An `Emulation` command is a write to that value or to the page's `Runtime/EmulationState`**, which is where
an override lives — on the **page**, not on the protocol target, because an override outlives the document it
was set on. What separates one command from the next is *when* it becomes effective, and each summary says
so: the viewport, the media, touch, focus, geolocation, the user agent and the hardware concurrency move the
document that is loaded; the time zone and the locale (`Options` an engine is *constructed* from) and script
execution (the parse is what refuses) reach the next one; and the remainder are accepted no-ops naming what
there is none of.

**Four decisions are made in the code and argued there**, and each is one an edit can undo without noticing.
`Runtime/NavigatorInstaller` says why the page's `navigator` members are own non-enumerable properties of the
instance rather than accessors on the shaped `Navigator.prototype`, and why `userAgent` is *shadowed* — a
page's is `BrowserOptions.UserAgent` and a client's override, and it has to be the string every request the
page makes carries. `Runtime/TouchEmulation` says that touch emulation changes what a page *detects* and not
what it receives — no touch event is ever dispatched — and why `Element.prototype` deliberately gets no
`ontouchstart`. `PageRuntime.VisibilityState` says why visibility and focus are one flag here and cannot be
two. And `Events/EventHandlerContentAttributes.Reconcile` is the one place scripting-disabled is checked,
because it is the one place every path arrives at; the parse's own half is that the `IScriptingService` is
not registered at all, which is how AngleSharp is told, and `Runtime.evaluate` is unaffected either way.

**The cascade is evaluated against the page's own device, and that closes half of the divergence this used
to buy.** `Runtime/PageRenderDevice` is registered on the browsing context `Parsing/ParserDriver` builds and
holds no numbers of its own — every member is read off `PageMediaEnvironment` at the moment
`ComputeCurrentStyle()` asks — so a dimension query and `@media print` in a style sheet answer from the same
viewport and media type `matchMedia` does, with nothing to re-register when a client emulates
([#3721](https://github.com/sebastienros/jint/issues/3721)). What still disagrees was measured rather than
assumed, and it is two kinds of thing. `IRenderDevice` has no member for a Level 5 preference, so
`@media (prefers-color-scheme: dark)` never becomes active while `matchMedia` answers it from the table
above — a framework that themes itself reads the second. And `(scripting)`, `(color)`, both `orientation`
values and every `min-resolution` answer the same whatever the device reports, so they are AngleSharp.Css's
own arithmetic rather than anything a device can fix: `scripting` is
[#233](https://github.com/AngleSharp/AngleSharp.Css/issues/233) and `orientation`
[#232](https://github.com/AngleSharp/AngleSharp.Css/issues/232); the other two are not filed.

### Custom elements, and where a reaction actually runs

`CustomElements/` is HTML §4.13 over a DOM that has none of it: AngleSharp builds an `HtmlUnknownElement`
for `<my-el>` and carries no custom element state, no definition and no reaction queue. What this package
adds is the registry, the three creation paths and the reaction lane; the element state hangs off a
`ConditionalWeakTable` keyed on the AngleSharp element, exactly as the wrapper cache does, and a document
that never mentions `customElements` builds no registry at all and pays for none of it.

- **The registry is per document**, which here is per engine, so a definition does not survive a navigation.
  `define` reads the constructor once, in the specification's order, so a page that changes
  `observedAttributes` afterwards changes nothing. `formAssociated` and `disabledFeatures` are parsed;
  `disabledFeatures: ['shadow']` is honoured by the upgrade and `formAssociated` is recorded on the element
  and consulted by nothing — there is no `ElementInternals` here, so a form-associated custom element takes
  part in no entry list.
- **An undefined `<my-el>` is an `HTMLElement`, not an `HTMLUnknownElement`.** That is HTML's element
  interface rule, and `DomManualInterfaces.For` is where it is made: AngleSharp builds the same class for
  both, so the name is the only thing separating them.
- **Three creation paths, all ending in one constructor.** `document.createElement` and `createElementNS` are
  `skip`ped in the override table and re-declared, because for a defined name the element is the
  *constructor's* rather than AngleSharp's; `new MyElement()` reaches `DomInterfaceObject.Construct`, which
  is HTML's `HTMLElement` constructor and the only `new` that object ever answers; and a parser-created
  element is **upgraded**. `cloneNode` is re-declared too, so a clone of a custom element is one.
- **The construction stack is the specification's**, which is what makes `super()` answer the element being
  upgraded rather than a second one, and a constructor that reaches the base twice an `InvalidStateError`.

**The `[CEReactions]` approximation, which is the one thing to know before changing any of it.** HTML
processes the element queue when the outermost `[CEReactions]` operation returns to script. Nothing here can
see a generated member return, so the queue is drained **at the moment a reaction arrives** instead — which
for everything a script does is inside the DOM call that caused it, and therefore before that call returns.
Two channels deliver those arrivals and both run inline, for the reason
[the observer section](#the-observers-and-when-each-of-them-delivers) gives: AngleSharp's mutation records
say what entered and left the document, and its `IAttributeObserver` service says what attribute changed —
the service and not the records, because a record needs the element to be under the observed document and
`el.setAttribute` before insertion is the commonest thing a component does. What is deliberately **not**
drained on arrival is a reaction that arrived on the parser's thread or while the queue was already
draining: those wait for the enclosing drain, or for the microtask checkpoint. So
`el.setAttribute('x', 1); assert(calls === 1)` holds as it does in a browser, and a reaction from a mutation
inside a *host* operation — one the page loop makes with no script to return to — runs at the checkpoint
rather than before that operation returns.

**A parser-created element is upgraded, not constructed, and that costs one shape of test.** AngleSharp
creates a parser element with no notification to hook, so `<my-el>` written in the markup is *undefined*
until the driver's next boundary: before each script it runs, and once when the parse ends, both of which are
`UpgradeParsedElements`. A page cannot tell, because a script only ever sees the document at those
boundaries — except for a constructor that constructs its own name *before* calling `super()`, which HTML
gives an empty construction stack and this gives the element being upgraded.
`Jint.Tests.Browser/Wpt/README.md` names the one corpus file about exactly that.

**Two attribute writes reach neither channel, and both are AngleSharp's**: `classList` writes the content
attribute without notifying its own `IAttributeObserver` or queueing a record, and `setAttributeNS` notifies
only the record channel. Both are in [`Dom/AGENTS.md`](Dom/AGENTS.md)'s divergence table.

### The protocol layer has a file of its own

Page targets, the page-level domains and the request log they read are [`DevTools/AGENTS.md`](DevTools/AGENTS.md).
The one rule to carry across without opening it: a domain reads the target's *current* runtime per command and
never caches an engine, and a `JsValue` never leaves the page loop.

### The obstacle course, and what a red fixture means

`Jint.Tests.Browser/Fixtures/` is eighteen offline pages built out of vendored libraries — TodoMVC on React,
Vue 3, Preact and Svelte, React hydrating server markup, jQuery, htmx, Alpine, a `pushState` router, custom
elements, an import map, `fetch`, a form that redirects, a cookie login, storage across navigations, both
observers, dialogs — served over a real socket and driven through the public `Page` API. Three of them are
driven again over the protocol by PuppeteerSharp and by Playwright for .NET.
[`Fixtures/README.md`](../Jint.Tests.Browser/Fixtures/README.md) is the inventory, what each proves, and how
one is added; `FixtureInventoryTests` holds it to the corpus so it cannot drift.

Two rules are worth carrying across without opening it:

- **A case asserts a DOM end state *and* that `Page.Errors` is empty.** A framework that threw half way
  through still renders something, so the error sink is what tells a half-working page from a working one.
- **A fixture that does not pass is never deleted and never quietly ignored.** It becomes a `needs triage`
  row in that README with the failing assertion and a one-line diagnosis, and its case is marked
  `[Explicit("<fixture>: …")]` — and `FixtureInventoryTests` fails unless the two sets are exactly equal, the
  way the web-platform-tests exclusion table is the artefact for that lane. Two rows stand today: `htmx`
  (htmx 2 builds an `XPathEvaluator` at the top level of its bundle and this browser has no XPath at all) and
  `custom-elements` (campaign item C6).

### The seams promoted later

**Where the pressure to promote comes from, and the table of what it has promoted so far, are
[`Jint.Browser.Tool/AGENTS.md`](../Jint.Browser.Tool/AGENTS.md).** `Jint.Browser.Tool` and
`Jint.Browser.Mcp` take **no** `InternalsVisibleTo` grant and must never be given one: they are the first
consumers outside this repository's own tests, so what they cannot reach is what an embedder cannot reach,
and every seam they have needed was published as a `Page` member over the internals the protocol layer
already used. One rule to carry across without opening that file: **a target is a selector or a `ref=`**, and
`Runtime/ElementLocator` is the one place that decides.

The package publishes the host API — `Browser`, `BrowserContext`, `BrowserOptions`,
`BrowserContextOptions`, `Page`, `Frame`, `Viewport`, `PageError`, `DialogEventArgs` and what a navigation
takes and answers — plus `DevToolsServerExtensions.AddBrowser`, and
`Jint.Tests.Browser/Verify/PublicApiTest.verified.txt` is the baseline that makes a change to it a reviewable
diff. **Nothing public takes or answers an AngleSharp node**, which is why `Page.SubmitFormAsync` takes a
selector; R2 reaches the same algorithm through the internal `FormSubmitter.Submit` from inside the loop.
Everything else is internal, and that is a decision with a date on it. `DomBindings`, `DomRealm`,
`DomInterfaceDefinition` and `DomHostHooks` are the four most likely to be promoted next, each with XML docs
and a `docs/v5-migration.md` row. Until then `Jint.Tests.Browser` is the only consumer, which is why it is
named in `InternalsVisibleTo` and why every test of the binding is written against the internal surface
rather than around it.

### Accessibility and extraction have a file of their own

The accessibility tree (html-aam roles, the accessible name, what `hidden` means without layout) and the text
and markdown extractors are [`Accessibility/AGENTS.md`](Accessibility/AGENTS.md). The one rule to carry across:
nothing there runs a line of the page's script, and nothing there needs a box.
