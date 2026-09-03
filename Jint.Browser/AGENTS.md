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
| A `DomCollectionAccessor` per collection interface, from `[DomAccessor]` | `DomInterfaceObject`, `DomBindings`, `DomConvert`, `DomHostHooks` |
| `DomTypeMap`'s candidate list, most derived first | `DomManualShapes` — the shapes the generator cannot express |
| `DomEnums`, both directions, for the WebIDL string enumerations | `DomTypeMap.For` and its per-`Type` cache |
| — | `DomManualInterfaces` and `DomConstructors` — the interface AngleSharp has no `[DomName]` for (`HTMLFrameSetElement`) and the one WebIDL really does give a constructor (`Document`) |
| — | `DomSelectorMembers` and `DomNodeMembers` — the five members whose *failure* has to be WebIDL's rather than AngleSharp's, and the one (`getRootNode`) AngleSharp has no `[DomName]` for |

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

<<<<<<< HEAD
**`Runtime/PageMediaEnvironment` is the one value every media query is answered from**, and
`PageRuntime.SetMedia` is its only writer. It holds the viewport, the emulated media type, whether the
primary pointer is coarse, whether the document's own scripts run, and the features a client emulated — as
one immutable value, swapped whole. That is not tidiness: a query's answer can depend on the viewport *and*
the media type *and* a preference, so a change that moved two of them has to reach a `change` listener once,
with both already in place. Every `MediaQueryList` the page holds then recomputes and fires a real
`MediaQueryListEvent` — `e => e.matches` is how the listener is written — only if its own answer moved. No
`resize` fires at the window: HTML fires that from update-the-rendering, and there is none.
=======
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

### The protocol layer
>>>>>>> 5714944e3 (Custom elements: a page defines one, the constructor runs where HTML says, and its callbacks run before the call that caused them returns)

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

**`getComputedStyle` is not re-evaluated against the emulated media**, and that is the one divergence this
buys: the cascade is AngleSharp.Css's `ComputeCurrentStyle()`, whose media evaluation is its own render
device, so an `@media (prefers-color-scheme: dark)` rule never becomes active. What a page reads through
`matchMedia` and through the cascade can therefore disagree — and a framework that themes itself reads the
first.

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

### Accessibility and extraction have no layout

`Accessibility/` computes an accessibility tree over AngleSharp's DOM and `Extraction/` renders the same
document as text or CommonMark. Both are pure C# over `IDocument`/`IElement`; neither touches an engine, and
that is why they were built before the page runtime existed. The consumers are the CDP `Accessibility` domain,
the custom `Jint.getMarkdown`/`getText`/`getAccessibilitySnapshot` domain, and the MCP server's `snapshot`.

**Three things a browser answers from its layout tree are answered from somewhere else, and every one is a
place where this can be wrong.**

- **Hidden** is `ElementVisibility`: the `hidden` content attribute, `aria-hidden="true"`, and `display:none`
  / `visibility:hidden|collapse` from the cascade — `IElement.ComputeCurrentStyle()`, which resolves author
  sheets and the UA sheet — falling back to the `style` content attribute alone when `AngleSharp.Css` is not
  registered. It cannot know that an element is off screen, clipped, covered or zero-sized. Two asymmetries
  are deliberate: `display:none` takes its subtree with it while `visibility:hidden` does not (CSS inherits
  `visibility`, so a `visibility:visible` descendant comes back), and `aria-hidden` removes a node from the
  accessibility tree while changing nothing about the rendering — so the extractors ask
  `RenderingReasonFor`, which ignores it, and only the tree asks `ReasonFor`, which does not.
- **Block-level** is `HtmlDisplay`, HTML's suggested rendering rather than a used display, and it is the
  table that decides — not the cascade. The cascade only wins where it *differs* from the table, which is
  what makes `<span style="display:block">` a block and stops AngleSharp's incomplete default sheet from
  calling every `<section>` inline.
- **`innerText`** is therefore the text of the document, not the text of a rendering of it: the required
  line breaks, the `<br>`s, the cell tabs and the white-space processing are all there, but nothing wraps,
  so a paragraph is one line however wide it would have been.

Three simplifications in the name computation are worth knowing before reading a wrong name as a bug: CSS
generated content (`::before`, `::after`, `::marker`) contributes nothing, `text-transform` is not applied,
and SVG `<title>`/`<desc>` children are not read. Everything else of accname 1.2 — 2A through 2I, the
recursion, the visited guard, the flattening — is the algorithm as written. HTML-AAM's mapping table is
implemented in full with one blanket simplification: where it names a computed role that is not a WAI-ARIA
role (`html-abbr`, `html-audio`, `keyboard`, `variable` and their kind) the element maps to `generic`.

`AccessibilityOptions` has three presets and they are not interchangeable: `Default` is the pruned tree,
`Snapshot` adds the text between the nodes (which is what `AccessibilitySnapshot.Render` needs to say
anything at all), and `Full` is what `Accessibility.getFullAXTree` answers with. A snapshot states each
string once — text that is already a node's accessible name is not published again as a text node.

The four fixture pages under `Jint.Tests.Browser/Accessibility/Golden/` are rendered three ways each and the
output is checked in. **`JINT_BROWSER_GOLDEN=update` rewrites them**, the same discipline `JINT_SPEC_ANCHORS`
and `JINT_DOM_BINDINGS` use: the diff is the artefact, so a change to what an agent reads has to be looked at.

Divergences that are **AngleSharp's**, found by this work, to be reported upstream rather than patched here:

| What | The standard | AngleSharp.Css 1.0.2 |
| --- | --- | --- |
| `el.ComputeCurrentStyle()` without the CSS services | an empty declaration, or a documented failure | throws `InvalidOperationException("Sequence contains no elements")`, which is why every call here is guarded and the guard latches |
| the default style sheet's `display` rules | HTML's rendering section gives `display: block` to `section`, `article`, `nav`, `aside`, `header`, `footer`, `main`, `figure`, `figcaption`, `details`, `summary`, `dialog`, `hgroup` | no rule at all, so every one of them computes to nothing and reads as inline |
| `[hidden] { display: none }` | in HTML's rendering section | absent, so `<div hidden>` computes `display: block` |
| `textarea { white-space: pre-wrap }` | in HTML's rendering section | absent, though `pre { white-space: pre }` is there |
| `CssMediaQueryList.matches` | evaluate the query against the device and answer | a stub: `ComputeMatched` returns `false` for every query, so a page asking whether it is on a narrow screen is always told no — which is why `Runtime/MediaQuery` exists at all |
| Media Queries Level 5's preference features | `prefers-color-scheme`, `prefers-reduced-motion`, `prefers-contrast`, `forced-colors`, `hover`, `pointer`, `scripting`, `color-gamut` are media features the cascade evaluates | not modelled: `IRenderDevice` has no member for any of them, so an `@media` rule naming one can never match and `Runtime/PageMediaEnvironment` answers them itself |
| a longhand nothing declared, through `getComputedStyle` | CSSOM's *resolved value*: every supported longhand answers, and a property nothing declared answers its initial value — `visibility` is `visible` | the empty string. **This is the one that stops an automation client.** Playwright's actionability check ends in `style.visibility !== "visible"`, so it reads every element of every page as hidden: `IsVisibleAsync` is false for an element with a real 1280×16 box, an unforced `ClickAsync` or `WaitForSelectorAsync` waits out its timeout, and the ARIA role engine drops the element as hidden. `Jint.Tests.Browser/DevTools/PlaywrightCourseTests` drives past it with `Force` and `IncludeHidden` and pins the reason; the standing decision (`Views/ComputedStyleTests`) is to record this rather than keep an initial-value table here, and what is new is that a supported client is unusable without one |
| the selector parser on `:has(*,:jqfake)` | a parse failure the caller can act on | `CssSelectorConstructor.HasFunctionState.Produce()` dereferences null, so the failure is a `NullReferenceException` rather than the `DomException` every other bad selector raises. jQuery 3.7 asks for exactly that selector inside a `try` during its support detection, so an unwrapped binding refuses to load jQuery at all — `Dom/DomSelectorMembers` contains both shapes and answers the `SyntaxError` the standard names |

One more, in AngleSharp itself rather than in `AngleSharp.Css`:

| What | The standard | AngleSharp 1.7.2 |
| --- | --- | --- |
| `IHtmlElement.IsContentEditable` on `<div contenteditable>` | `true`: HTML's [`contenteditable`](https://html.spec.whatwg.org/multipage/interaction.html#attr-contenteditable) is an enumerated attribute whose `true` keyword has the **empty string** as its other spelling, which is how nearly every page in the world writes it | `false` — the attribute is mapped through an enumeration that does not admit the empty string, so only `contenteditable="true"` reads as editable. `Events/ContentEditing.HostOf` computes the state itself for the editor and for focusability; the script-visible `el.isContentEditable` is still AngleSharp's answer, because that member is the binding forwarding it |
| `Node.getRootNode()` | DOM §4.4: `Node getRootNode(optional GetRootNodeOptions options = {})` | absent — there is no `[DomName("getRootNode")]` anywhere in the assembly, so nothing could generate it. `Dom/DomNodeMembers` declares it over `INode.Parent`, and it is not a corner: Playwright's injected script calls it on every element it touches |

