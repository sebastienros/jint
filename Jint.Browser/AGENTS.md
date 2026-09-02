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
| `skip` | A member a later campaign item owns: navigation (`location.assign`, `location.href`'s setter), activation (`click`, `focus`, `blur`, `form.reset`), the parser (`document.open`/`close`/`load`), and `document.createEvent`, whose AngleSharp `Event` must never reach script. `half: "setter"` skips the write half only. |
| `hooks` | A member routed through `DomHostHooks` so the parser driver can replace its body: the `innerHTML` and `outerHTML` setters, `insertAdjacentHTML`, `document.write`/`writeln`. The default implementations *are* the AngleSharp call, so the seam costs nothing until R3 uses it. |
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
| `location.host = …` (and `pathname`, `port`, `protocol`, `search`) | a navigation the page can observe | raises `Location.Changed`, which `Document.LocationChanged` handles in a fire-and-forget `async void` calling `IBrowsingContext.OpenAsync` — on whatever thread the setter ran on. It is inert here only because no `IRequester` is registered, so every navigation handler declines the protocol and the call answers `null`. Registering a document loader without a navigation handler of our own would put a second thread in the DOM |

The `dataset` one has a visible consequence inside the binding, and the one place a workaround is legitimate:
the generated `SupportedNames` filters out a `null` value, because the projection's three hooks must agree at
the same instant or host-contract verification fails. That is keeping *our* contract, not mending AngleSharp's.

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

### The page loop, and the thread rule

**One thread per page owns its engine and its DOM, and nothing else may touch either.** `Runtime/PageLoop`
is that thread: it drains a `Channel<Action<Engine?>>` mailbox, calls `Tasks.ProcessTasks()`, and parks in
`Tasks.WaitForScheduledWork` for the shorter of `BrowserOptions.PumpIdle` and
`Tasks.TimeUntilNextScheduledWork`. The engine is built on it and disposed on it, because both are
engine-owning operations, and a navigation replaces the engine from inside a mailbox request rather than from
outside. Jint starts no thread of its own; this is what makes a page's timers fire at all.

Four rules follow, and each of them is a way to break the package silently:

- **Every public `Page` member is a mailbox request, and the request is what holds the engine.** A new member
  is `_loop.PostAsync(engine => …)`, never a field read that reaches into the runtime. A caller is on some
  other thread by definition.
- **Nothing that belongs to an engine may be in the returned task.** A `JsValue` belongs to the engine that
  made it *and* to the thread that owns it, and an AngleSharp node is safe to read only while the loop is not
  mutating the tree. Convert inside the request — `JsValue.ToObject()`, a `string`, a `PageError` rendered by
  `ValueInspector` — and let the task carry the conversion. `PageTests` pins that what comes back is not from
  Jint's assembly, which is the cheapest possible check and worth keeping.
- **Posting wakes the park.** `PostAsync` writes to the mailbox and then calls `engine.Tasks.Post` with an
  empty action, purely for its documented side effect of ending a `WaitForScheduledWork`. Without it a request
  waits out `PumpIdle`; the channel write happens first, so the wake can never be lost.
- **A request that arrives after the loop stopped fails with `ObjectDisposedException`.** It is never left to
  hang, and the pending queue is failed the same way on the way down.
- **Teardown is not a request.** `PageLoop.CloseAsync` takes the action that releases the document and the
  browsing context and runs it in the loop's own shutdown, because a request would queue behind whatever is
  running — and what is running may be the very wait the close is ending. Posting it instead deadlocks.

`WaitForIdleAsync` runs its whole wait *inside* one request rather than polling from outside, because the wake
poke is itself an engine job: a probe posted from another thread would find the queue non-empty and never see
idle. Idle is `TimeUntilNextScheduledWork is null`. It holds the loop for its whole duration, so it takes the
page's cancellation token: a wait that closing could not end would make closing wait out its ceiling.

### The window: why the global stays `GlobalObject`, and the trap in it

`Runtime/WindowInstaller` sets the global object's `[[Prototype]]` to
`Window.prototype → EventTarget.prototype → Object.prototype` and installs the per-document singletons
(`window`, `self`, `frames`, `top`, `parent`, `document`, `location`, `screen`) as own lazy globals through the
public `Engine.AddLazyGlobal`. Substituting a host global would forfeit the global-identifier inline cache and
mean re-installing every intrinsic; the prototype swap costs nothing, and
`GlobalEnvironment.HasBindingOnGlobalPrototype` already resolves a prototype member as a global.

**The trap: a shape *method* on `Window.prototype` cannot find its page.** A page calls `alert(…)` and
`requestAnimationFrame(…)` unqualified, and an unqualified call to a member of the global object passes
`undefined` as the receiver — the global environment record has no `with` base object — so a member body
reading `thisObject` has no engine to reach the runtime through and can only answer `Illegal invocation`.
Accessors are unaffected, because a bare identifier *read* goes through the global object's `[[Get]]` with the
global as receiver. So: **an operation that needs its page is a `PerRealmSlot` holding a `ClrFunction` bound to
the engine** (`WindowInstaller.Operation`), and only an operation that needs nothing — `scrollTo`,
`getSelection` — stays a `Method`. Adding a window operation the other way compiles, passes `window.foo()`,
and fails `foo()`.

Two members are own properties of their object rather than accessors on a shaped prototype, and both say why in
place: `document.defaultView` (the binding excludes AngleSharp's `IWindow`, so nothing generates it) and
`document.currentScript` (AngleSharp answers the wrong thing — see the divergence table). `Location`'s `href`
setter, `assign`, `replace`, `reload` and `toString` are own properties of the location wrapper for the same
reason, and they call `Page.RequestNavigation`, which starts a navigation for a URL this version can load and
records a page error for one it cannot. **A member installed this way shadows the prototype and is visible to
`Object.getOwnPropertyNames`**; it is the right tool for one object and the wrong one for a class, because a
shaped prototype that takes an undeclared property loses its shape and its inline caching with it.

### The parse, and the parser hop

`Runtime/PageDocument` opens the document through `IBrowsingContext.OpenAsync` on the loop thread and blocks on
it. AngleSharp's parse is an asynchronous method whose every `await` carries `ConfigureAwait(false)`, so a
genuinely asynchronous step anywhere in it would resume the parse — and the scripting hook with it — on a pool
thread while the loop sat blocked, and the engine would be entered from two threads with nothing to say so.
**Nothing in this configuration is asynchronous** (the source is in memory, no `IResourceLoader` is registered,
no `IEventLoop` is registered so `Document.QueueTaskAsync` completes inline, and the scripting hook answers
`Task.CompletedTask`), so the whole parse including script execution happens on the one thread. That is
checked, not believed: `PageScriptingService.ObservedThreadId` records the thread it was called on, and a
mismatch becomes a page error naming the fallback the design specifies — a fully synchronous parse on the loop
with blocking script fetches. **Anything that makes a step of the parse genuinely asynchronous — a resource
loader, an event loop service, a scripting hook that awaits — takes the fallback with it.**

What runs: a classic inline `<script>`, synchronously at its own `</script>`, in document order. What does not:
an external `src` (no network yet), a module, an import map. `SupportsType` answering `false` is what stops
AngleSharp preparing a module at all, and `PageDocument.Survey` walks the parsed document afterwards so that
every skipped script is named in `Page.UnsupportedScripts` rather than silently doing nothing.

`readystatechange`, `DOMContentLoaded` (bubbling) and `load` (at the window) are dispatched afterwards through
Jint's own dispatcher, because AngleSharp's own firing goes into its own listener lists, which hold nothing a
script registered. `document.readyState` already reads `"complete"` at all three: AngleSharp advances it during
the parse and `Document.ReadyState`'s setter is not reachable from outside its assembly.

### The seams promoted later

The package publishes exactly one thing: the host API — `Browser`, `BrowserContext`, `BrowserOptions`,
`BrowserContextOptions`, `Page`, `Frame`, `Viewport`, `PageError`, `DialogEventArgs` — and
`Jint.Tests.Browser/Verify/PublicApiTest.verified.txt` is the baseline that makes a change to it a reviewable
diff. Everything else is internal, and that is a decision with a date on it: the binding is a working surface
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
