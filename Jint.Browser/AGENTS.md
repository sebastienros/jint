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

**Never hand-edit a `.g.cs`.** `DomBindingsStalenessTests` runs the same emitter in memory and fails on any
difference; `JINT_DOM_BINDINGS=update` writes the difference back, which is also the shortest regeneration
path after an `overrides.json` edit. `tools/dom-bindings/README.md` has the command-line one and the rule that
**a pin bump is a code change**, not a configuration edit.

### How AngleSharp's attributes are read as WebIDL

`[DomName]` is the whole surface: an interface or member without one is not projected. Five refinements are
worth knowing before changing the model builder, because none of them is stated by AngleSharp.

- **`[DomNoInterfaceObject]` does not mean "mixin".** An interface carrying it is a mixin only when it *also*
  has no `[DomName]` base of its own **and** at least one other `[DomName]` interface extends it — that is
  `ParentNode`, `ChildNode`, `GlobalEventHandlers`, `NavigatorID` and their kind. `CSSGroupingRule` carries
  the same attribute and has a base, so it stays a real link in the chain; `CaretPosition` carries it with
  neither a base nor an includer, so it stays a standalone interface that simply cannot be named.
- **A plain CLR interface with no `[DomName]` but with `[DomName]` members is a mixin too** — `IValidation`,
  `ILoadableElement`, `IMediaController`. Nothing marks them; the member closure picks them up.
- **Members are attributed to the shallowest interface that has them.** `MembersOf(I)` is the `[DomName]`
  closure of `I` minus the closure of its primary base, so `querySelector` lands on `Element`, `Document` and
  `DocumentFragment` — exactly where WebIDL's `includes` puts it — and never again below.
- **Some members are extension methods.** Every one of `CSSStyleDeclaration`'s two hundred-odd CSS property
  attributes lives on `StyleDeclarationExtensions`, and `element.style` on
  `ElementCssInlineStyleExtensions`; a static method with `[DomName]` + `[DomAccessor]` whose first parameter
  is a `[DomName]` interface is a member of that interface. They are emitted as **extension calls** rather
  than as static ones, because AngleSharp and AngleSharp.Css both declare an `AngleSharp.Dom.ElementExtensions`
  and naming either by its full name is CS0433.
- **An enum is a string enumeration when any of its `[DomName]` field values contains a lower-case letter**,
  and a set of numeric constants otherwise: `ShadowRootMode` is `"open"`/`"closed"`, `NodeType` is
  `ELEMENT_NODE = 1`. `overrides.json`'s `stringEnums` corrects a wrong answer. Numeric-enum constants attach
  to the interface that **returns** the enum, which is what puts `ELEMENT_NODE` on `Node` and not on everything
  that mentions a node type; `constants.add` / `constants.skip` fix the rest.

### The override table

`tools/dom-bindings/overrides.json` is the curated half, and every entry carries a reason. Entries that name
a member the pinned assemblies no longer have become diagnostics, so the table can never quietly describe a
version of AngleSharp nobody references.

| List | What it is for |
| --- | --- |
| `excludedInterfaces` | An interface the runtime owns instead. Today: `IWindow` (campaign item R1). |
| `manual` | An interface whose shape is hand-written in `DomManualShapes`. Today: `IHtmlCollection<T>`, whose generic invariance keeps a member body from naming its receiver. A manual interface contributes no members to any closure — its children inherit them through the prototype chain. |
| `skip` | A member whose AngleSharp implementation must not be projected: navigation (`location.assign`, `location.href`'s setter), the parser (`document.open`/`close`/`load`), `document.createEvent` whose AngleSharp `Event` must never reach script, and the six the events bridge re-declares because AngleSharp's own do nothing — see the divergence table. `half: "setter"` skips the write half only. |
| `hooks` | A member routed through `DomHostHooks` so the parser driver can replace its body: the `innerHTML` and `outerHTML` setters, `insertAdjacentHTML`, `document.write`/`writeln`. The default implementations *are* the AngleSharp call, so the seam costs nothing until R3 uses it. The same class carries `WrapperCreated`, which is the other direction — a member the generator could not emit at all, added to one wrapper; it is also where the events bridge registers an element's handler content attributes. |
| `additions` | A member the **standard** puts on a generated interface and AngleSharp's metadata cannot express: a callback parameter, a stringifier with no `[DomName]`, a Shadow DOM v0 spelling, a missing `[DomName]`, a CSSOM View rectangle, and the operations whose body is an event rather than a DOM call (`click`/`focus`/`blur`, `form.submit`/`requestSubmit`/`reset`, `document.activeElement`/`hasFocus`). Two forms, and an entry uses exactly one: **reach for the member form**, which names one member and goes through the model like any projected member, so the generated file names it and a member AngleSharp later grows under that name is reported rather than shadowed; the `"extend"` form hands the builder to a method and exists only for a *family* whose member list is computed, which today is HTML's event handler IDL attributes. `Overrides.AdditionEntry` has the whole of why, including what the extend form gives up. Either way it adds rather than replaces, so the interface stays **one** shape — the only way to add a member to a class rather than to one object without costing the prototype its shape. |
| `nullableStrings` | The members whose IDL type is `DOMString?` rather than `DOMString`. See the conversion table below. |
| `stringEnums`, `constants` | The two enum decisions the heuristics above cannot make. |

**A member the generator could not convert is not in this table.** It is skipped with the reason the generator
worked out, and the reason is in the report the regeneration prints. That split is deliberate: this file is
for decisions, the report is for consequences.

### The conversion table, and where it diverges from a browser

One decision is worth stating in full, because it is the one a page notices. **A CLR `string` return maps
`null` to the empty string**, because WebIDL's `DOMString` is not nullable and the overwhelming majority of
these members are reflected content attributes whose specified value when the attribute is absent is `""`.
AngleSharp returns `null` for most of them, which is its divergence rather than a nullable IDL type on ours.
The members whose IDL type genuinely *is* `DOMString?` are listed in `overrides.json`'s `nullableStrings` and
emit `null` instead. **That list is the artefact**: a member missing from it answers `""` where a browser
answers `null`, and one wrongly in it does the reverse.

The rest is mechanical — `bool`, the integer and floating types through WebIDL's `ToInt32`/`ToUint32`/
`ToNumber`, `DateTime` as a `DOMTimeStamp`, interfaces through the wrapper cache, `IHtmlCollection<T>`
through a call-site-closed generic, `object` as WebIDL `any`. What it cannot convert (a `Task`, a delegate
parameter, an `IWindow`) becomes a recorded skip.

Divergences from a browser that are **ours** and deliberate:

- **`length` on a collection is an own property**, not a prototype accessor, because `ArrayLikeObject` owns
  it; `list.hasOwnProperty('length')` answers `true`. `Jint/Native/Object/AGENTS.md` says why it cannot move.
- **`Symbol.iterator` is on the instance**, not the interface prototype, because `JsObjectShape` has no
  symbol-keyed member yet. The value is the same function object; nothing a script does can tell.
- **`(Node or DOMString)...` takes only the `Node` half**: `append('text')` is a `TypeError` where a browser
  inserts a text node, because a static member body has no document to create one against.
- **`select.add` and `HTMLOptionsCollection.add` take one of their two overloads**, because HTML's union type
  is two CLR methods sharing a `[DomName]`. Two diagnostics say so and `DomBindingsStalenessTests` pins them
  at exactly two, so a third means something new turned up.
- **A node's indexed and named getters are dropped** (`form[0]`, `form.username`, `select[0]`): a node wrapper
  is a `JsEventTarget` rather than an `ArrayLikeObject`, so it carries no property projection.
  `form.elements[0]` and `form.elements.namedItem('username')` are the same values.

Divergences that are **AngleSharp's**, found by this work and to be reported upstream rather than patched
here:

| What | The standard | AngleSharp |
| --- | --- | --- |
| `delete el.dataset.foo` | DOM §3.5's deleter removes the content attribute | `IStringMap.Remove` sets its value to `null` and leaves `data-foo` in place |
| `el.querySelectorAll(…)` | DOM §4.2.6 returns a static `NodeList` | returns an `IHtmlCollection<IElement>`, so `Object.prototype.toString` reports `[object HTMLCollection]` |
| `el.style.color` | CSSOM serializes an opaque colour as `rgb(r, g, b)` | serializes every colour as `rgba(r, g, b, 1)` |
| `el.children === el.children` | one `HTMLCollection` per element | a fresh collection per call, so the wrapper cache has nothing to key identity on |
| `el.id` when the attribute is absent | `""` | `null`, which is why the conversion table's default is the empty string |
| `insertAdjacentHTML`'s position | a WebIDL enumeration | `AdjacentPosition` carries no `[DomLiterals]`; the lower-case-field heuristic is what catches it |
| `matchMedia(q).matches` | evaluates the query against the viewport | `CssMediaQueryList.ComputeMatched` is `return false`, so **every** query answers `false` — a page asking whether it is on a narrow screen is always told no. `Runtime/MediaQuery` answers the subset a page branches on instead |
| `document.currentScript` | HTML §4.12.1: the script whose text is running | the head of the *deferred* script queue, so it is `null` for exactly the case a page uses it in |
| `location.host = …` (and `pathname`, `port`, `protocol`, `search`) | a navigation the page can observe | raises `Location.Changed`, which `Document.LocationChanged` handles in a fire-and-forget `async void` calling `IBrowsingContext.OpenAsync` — on whatever thread the setter ran on. **Unreachable from script**, because `Runtime/LocationInstaller` shadows every `Location` member with an own property over the page's own URL; AngleSharp's location is never written |
| `observer.disconnect()` | DOM §4.3.3 empties the observer's registered-node list | `MutationObserver.Disconnect` unregisters it from the document but leaves that list populated, so the **next** `observe()` of any node silently re-registers every node it used to watch |
| `observer.observe(document, …)` | observes the document node | `Connect` silently retargets a `Document` to its `documentElement`, so a `childList` mutation of the document itself is never reported. With `subtree: true` — how nearly every page writes it — nothing else changes |
| `observe(…)` with bad options | a `TypeError` | a `DomException` (`TypeMismatchError` / `SyntaxError`), and its resolution of the dictionary's optional members differs from the standard's: `{ attributeOldValue: false }` alone is valid in DOM and invalid there |
| `record.addedNodes` on an attribute record | an empty `NodeList` | `null`, for both `Added` and `Removed` |
| `IShadowRoot.Mode` | `ShadowRoot.mode` | the one member of the interface with no `[DomName]`, so nothing says it is projected |
| `slot.assignedNodes()` | DOM §4.2.5 | named `getDistributedNodes`, the Shadow DOM v0 spelling |
| `getComputedStyle(span).display` | `inline` | the empty string: the cascade reports only what a stylesheet *declared*, and `display: inline` is CSS's initial value, so the user-agent sheet does not declare it. A declared one (`div` → `block`) resolves |
| the computed style is writable | CSSOM's computed flag makes every write a `NoModificationAllowedError` | an ordinary writable declaration, and a detached one, so a write neither throws nor changes anything readable — see `Dom/Views/ReadOnlyStyleDeclaration` |
| `el.click()` on a checkbox, radio, `<summary>` or `<a href>` | the element's activation behaviour runs | `DoClick` dispatches on AngleSharp's own bus and runs **no activation behaviour at all**, with or without a browsing context: nothing toggles, opens or navigates |
| `document.activeElement` | the focused element, or the body | `null` for the life of every document — nothing in AngleSharp ever assigns it, `DoFocus()` included |
| `el.tabIndex` when the attribute is absent | −1 for anything not inherently focusable | 0 for every element, including a bare `<div>`, so it cannot decide focusability |
| `input.labels` | the `<label>`s whose `for` names the control | an empty collection, though `label.control` resolves correctly in the other direction |
| `select.selectedIndex` with no `selected` option | 0 — the selectedness-setting algorithm picks the first option of a non-`multiple`, display-size-1 select | −1 |
| `input.value = …` | the cursor moves to the end and the selection is dropped | `SelectionStart`/`SelectionEnd` are left where they were, so they can point past the end of the new value |
| `input.setSelectionRange` on a `type=checkbox` | `InvalidStateError` — the type does not support selection | answers, so the type test has to be the caller's |

The `dataset` one has a visible consequence inside the binding, and the one place a workaround is legitimate:
the generated `SupportedNames` filters out a `null` value, because the projection's three hooks must agree at
the same instant or host-contract verification fails. That is keeping *our* contract, not mending AngleSharp's.

### The events bridge lives with the runtime

Every script-visible event is a Jint `Event` dispatched through the engine's tree-aware dispatcher, at the
algorithm points the package owns (design doc §5) — never AngleSharp's own bus, which holds nothing a script
registered. What that costs the binding is the `skip` and `additions` rows above: `click`, `focus`, `blur`,
`form.reset`, `document.activeElement` and `document.hasFocus` are all AngleSharp members that do nothing
useful, so they are skipped and re-declared. The behaviour behind them — which algorithm point raises which
event, what activation means with no layout, and why the handler content attributes need no notification from
AngleSharp — is [`Runtime/AGENTS.md`](Runtime/AGENTS.md#the-events-bridge).

### Wrapper identity, and the two classes that are not one hierarchy

One `ConditionalWeakTable<object, ObjectInstance>` per engine, keyed on the AngleSharp object. That single
choice buys the browsers' wrapper-preservation rule: a node in the tree keeps its wrapper and therefore its
expandos alive (React and Vue rely on that), and a node dropped by both the tree and script collects with its
wrapper. It is on the engine through a `ConditionalWeakTable<Engine, DomRealm>` rather than in
`Engine.HostDefined`, because that slot belongs to the embedder.

**Everything that creates an object reads `DomRealm.PrincipalRealm`, never `Engine.Realm`.** The latter
answers the realm currently *executing*, so a wrapper first reached from inside a `ShadowRealm` callback would
take its prototype root, its interface object and its `Symbol.iterator` from intrinsics its own object does not
belong to — and every wrapper built afterwards would disagree with it about what `Object.prototype` is. It is
the same call `WebApiRegistration.InstallGlobals` makes, for the same reason.

The wrappers deliberately do **not** share a base class, and `IDomWrapper` is what they share instead:

- **`DomNodeObject : JsEventTarget`** — so the engine's DOM §2.9 dispatch walks a real path. The seam that
  must not be forgotten is `IsNode`, because it is what selects the tree lane at all: overriding `GetParent`
  without it dispatches to the target alone, in silence. Assigned slots answer `null` and there is no
  activation behaviour yet; both are campaign item R2, and answering a wrong slot would be worse than none.
- **`Collections/DomCollectionObject : ArrayLikeObject`** — so `list[i]`, `for..of`, spread, `Array.from` and
  the `Array.prototype` generics reach the engine's one-callback-per-element lane with no `Reference`, no key
  object and no descriptor. Its interface-specific half is the generated accessor.
- **`Collections/DomNamedMapObject : NamedPropertyObject`** — `dataset`, whose whole model is a named getter.
- **`DomObject : ObjectInstance`** — everything else, overriding nothing, which is what keeps it on the
  engine's ordinary access lane.

The cache is also where a page's DOM is *counted*. `DomRealm.MaxNodes` — `BrowserOptions.MaxDomNodes` when a
page runtime set it, and zero, meaning no limit, for a binding installed on its own — makes the projection
that would pass the ceiling a `RangeError` in the script that asked for it. It counts node wrappers because
that is the one place every projection passes through and because the wrapper table is what a script's DOM
growth actually costs an engine — a different quantity from the one the parse bounds, and deliberately so:
seeding this counter from the parsed document's size would make merely walking a document of the permitted
size a refusal. [`Runtime/AGENTS.md`](Runtime/AGENTS.md#budgets-what-a-turn-is-and-which-constraints-can-bound-one)
has the other side.

Read [`Jint/Native/Object/AGENTS.md`](../Jint/Native/Object/AGENTS.md) before changing any of them: the
subclassing cliff, the named-projection hook table and the coherence obligations every one of these classes
carries are there, and host-contract verification (`JINT_HOST_CONTRACT_VERIFICATION=1`) is what checks them.

### Shape discipline

Every prototype is a `JsObjectShape.Instantiate` result, and `DomPrototypeTests` asserts
`Engine.Advanced.HasSharedShape` for **every** interface. That is not decoration: a shaped prototype is a
valid holder for the prototype-method inline cache and a dictionary-mode one is not, so a single careless
`DefineOwnProperty` would quietly cost the whole surface its caching. The one write a prototype takes after
instantiation is the `constructor` slot, filled with `DefineOwnPropertyUnchecked` under a name the shape
declared — the sanctioned in-place slot replacement, and the only kind a shaped object survives.

`WebIdlPropertyAttributeTests` holds every emitted member to its kind's attributes, which are WebIDL's and not
ECMAScript's: an operation is **enumerable**. The same rule `Jint/WebApi/AGENTS.md` states, checked the same
way, and it is the mistake a generator makes by default.

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
  [parser hop](Runtime/AGENTS.md#the-parse-and-the-parser-hop): its `MutationHost` schedules through an
  `IEventLoop` service, **nothing in AngleSharp implements one**, and `EventLoopExtensions.Enqueue` on a null
  loop runs the action *inline* — so out of the box the callback fires synchronously inside `appendChild`.
  Registering an event loop to fix that is precisely what would make a step of the parse asynchronous and
  take that fallback. So the inline call is used as the
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
  fully intersecting or at zero size, and never again, because with no layout nothing can change. "Never
  intersecting" would stop every lazy list and reveal-on-scroll animation dead, and the initial resize
  notification is the one a component uses to measure itself when it mounts. `root`, `rootMargin` and
  `thresholds` are parsed, validated and reflected exactly as the specification says and change nothing.
  Rectangles are `ObserverGeometry` zeros and are **plain objects, not `DOMRectReadOnly`**: there is no
  rectangle interface in this package yet, and one whose every instance is zeros would be worse than none.
  The flat-box model (campaign item C4) turns all of it into real numbers.

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

`matchMedia` gained its other half. `PageRuntime.SetViewport` is the seam device emulation (campaign item C5)
drives, and every `MediaQueryList` the page holds recomputes and fires `change` — a real
`MediaQueryListEvent`, because `e => e.matches` is how the listener is written — only if its own answer moved.
No `resize` event fires at the window: HTML fires that from update-the-rendering, and there is none.

### The seams promoted later

The package publishes exactly one thing: the host API — `Browser`, `BrowserContext`, `BrowserOptions`,
`BrowserContextOptions`, `Page`, `Frame`, `Viewport`, `PageError`, `DialogEventArgs` and what a navigation
takes and answers — and `Jint.Tests.Browser/Verify/PublicApiTest.verified.txt` is the baseline that makes a
change to it a reviewable diff. **Nothing public takes or answers an AngleSharp node**, which is why
`Page.SubmitFormAsync` takes a selector; R2 reaches the same algorithm through the internal
`FormSubmitter.Submit` from inside the loop. Everything else is internal, and that is a decision with a date on it: the binding is a working surface
until the protocol layer has settled what a host actually holds. `DomBindings`, `DomRealm`,
`DomInterfaceDefinition` and `DomHostHooks` are the four most likely to be promoted next, and each would arrive
with XML docs and a `docs/v5-migration.md` row. Until then `Jint.Tests.Browser` is the only consumer, which is
why it is named in `InternalsVisibleTo` and why every test of the binding is written against the internal
surface rather than around it.

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
