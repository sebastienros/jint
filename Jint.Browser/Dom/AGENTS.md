# Agent instructions: the DOM bindings

> **Read this when:** You are touching `Jint.Browser/Dom/` or `tools/dom-bindings/` — the generated bindings, the
> hand-written wrapper classes, the override table, or the generator that reads AngleSharp's attributes.
>
> This is one of the co-located instruction files indexed from the repository-root
> [`AGENTS.md`](../../AGENTS.md). Read that first, then [`Jint.Browser/AGENTS.md`](../../AGENTS.md) for the
> package's principle and what is generated versus hand-written. Nothing below is repeated in either.

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
| `skip` | A member whose AngleSharp implementation must not be projected: navigation (`location.assign`, `location.href`'s setter), the parser (`document.open`/`close`/`load`), `document.createEvent` whose AngleSharp `Event` must never reach script, `DOMImplementation.createHTMLDocument` whose title AngleSharp makes required, and the six the events bridge re-declares because AngleSharp's own do nothing — see the divergence table. Every one of them is re-declared in `additions`, so a skip here is a *replacement* and not an absence. `half: "setter"` skips the write half only. |
| `hooks` | A member routed through `DomHostHooks` so the package can replace its body: the `innerHTML` and `outerHTML` setters, `insertAdjacentHTML`, `document.write`/`writeln`, and `setAttribute`/`removeAttribute` — the one write of an attribute this package can see, and therefore where a handler content attribute takes its position in the element's listener list. The default implementations *are* the AngleSharp call, so the seam costs nothing until R3 uses it. The same class carries `WrapperCreated`, which is the other direction — a member the generator could not emit at all, added to one wrapper; it is also where the events bridge registers an element's handler content attributes. |
| `additions` | A member the **standard** puts on a generated interface and AngleSharp's metadata cannot express: a callback parameter, a stringifier with no `[DomName]`, a Shadow DOM v0 spelling, a missing `[DomName]`, the whole of CSSOM View's box model (`getBoundingClientRect` and its `client*`/`scroll*`/`offset*` family, answered from the flat layout — [`../Runtime/AGENTS.md`](../Runtime/AGENTS.md)), the legacy event-creation surface (`document.createEvent`), and the operations whose body is an event rather than a DOM call (`click`/`focus`/`blur`, `form.submit`/`requestSubmit`/`reset`, `document.activeElement`/`hasFocus`). Two forms, and an entry uses exactly one: **reach for the member form**, which names one member and goes through the model like any projected member, so the generated file names it and a member AngleSharp later grows under that name is reported rather than shadowed; the `"extend"` form hands the builder to a method and exists only for a *family* whose member list is computed, which today is HTML's event handler IDL attributes. `Overrides.AdditionEntry` has the whole of why, including what the extend form gives up. Either way it adds rather than replaces, so the interface stays **one** shape — the only way to add a member to a class rather than to one object without costing the prototype its shape. |
| `nullableStrings` | The members whose IDL type is `DOMString?` rather than `DOMString`. See the conversion table below. |
| `stringEnums`, `constants` | The two enum decisions the heuristics above cannot make. |

**A member the generator could not convert is not in this table.** It is skipped with the reason the generator
worked out, and the reason is in the report the regeneration prints. That split is deliberate: this file is
for decisions, the report is for consequences.

### The two interfaces the generator cannot see

`DomManualInterfaces` and `DomConstructors` are the whole of what the override table cannot express, and each
holds exactly one entry today.

- **`HTMLFrameSetElement` is declared by name and selected by local name.** AngleSharp models `<frameset>`
  with the plain `IHtmlElement` — there is no `IHtmlFrameSetElement` and no `[DomName("HTMLFrameSetElement")]`
  in the pinned assemblies — so `DomTypeMap`, which keys on the CLR type, cannot tell a frameset from a
  `<div>`. Its shape is empty: HTML gives the interface no members beyond `WindowEventHandlers`, which this
  package puts on `HTMLElement`. Its index continues `DomInterfaces`' own, which is what keeps `DomRealm`'s
  per-engine arrays a dense array.
- **`Document` is the one interface object a script may call `new` on.** AngleSharp puts `[DomConstructor]` on
  no `[DomName]` interface at all, so the generator can never learn that an interface is constructible and
  `DomInterfaceObject` refuses every `new` — which is also what a browser answers for `new HTMLDivElement()`.
  The document it makes is DOM's: an XML document with no doctype, no document element and no browsing
  context.

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
| `node.isConnected` | DOM §4.4: whether the node's shadow-including root is a document | `INode` has no member for it at all, so nothing is projected — and every client library asks a node handle this before it clicks it, so an absent member makes every element read as detached. Re-declared in `additions` |
| `delete el.dataset.foo` | DOM §3.5's deleter removes the content attribute | `IStringMap.Remove` sets its value to `null` and leaves `data-foo` in place |
| `el.querySelectorAll(…)` | DOM §4.2.6 returns a static `NodeList` | returns an `IHtmlCollection<IElement>`, so `Object.prototype.toString` reports `[object HTMLCollection]` |
| `el.style.color` | CSSOM serializes an opaque colour as `rgb(r, g, b)` | serializes every colour as `rgba(r, g, b, 1)` |
| `el.children === el.children` | one `HTMLCollection` per element | a fresh collection per call, so the wrapper cache has nothing to key identity on |
| `el.id` when the attribute is absent | `""` | `null`, which is why the conversion table's default is the empty string |
| `insertAdjacentHTML`'s position | a WebIDL enumeration | `AdjacentPosition` carries no `[DomLiterals]`; the lower-case-field heuristic is what catches it |
| `matchMedia(q).matches` | evaluates the query against the viewport | `CssMediaQueryList.ComputeMatched` is `return false`, so **every** query answers `false` — a page asking whether it is on a narrow screen is always told no. `Runtime/MediaQuery` answers the subset a page branches on instead |
| `document.currentScript` | HTML §4.12.1: the script whose text is running | the head of the *deferred* script queue, so it is `null` for exactly the case a page uses it in |
| `document.readyState` | HTML §3.1.6: the host advances it and fires `readystatechange` | `Document.ReadyState`'s setter is `protected`, so nothing outside AngleSharp can move it; a host driving its own parse has to shadow the member. `Runtime/PageRuntime.ReadyState` is that shadow |
| `<script async src>` | HTML: it executes the moment its fetch lands, in no particular order | `HtmlScriptElement.InvokeLoadingScript` puts `defer` and `async` into the one `_loadingScripts` queue, so an async script executes in document order at the end of the parse |
| `document.write` after the parse | HTML: it implies `document.open()`, which replaces the document | `Document.Open` blocks on `PromptToUnloadAsync().Result` and `Unload(recycle: true).Wait()` — synchronous waits on asynchronous work, inside a DOM member a script calls. `DomHostHooks.Write` refuses instead |
| a custom `IResourceLoader` | a host wants scripts and style sheets and not images | every request processor asks the same `IResourceLoader`, and only `WithDefaultLoader`'s `LoaderOptions` can filter by kind — so a host supplying its own loader has to refuse per element type itself |
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
| `input.labels` | the `<label>`s whose `for` names the control | an empty collection |
| `label.control` | the labeled control: the `for` target, or the first labelable **descendant** | `null` for a control the label contains, which is the commoner spelling; `Events/ActivationBehaviors` computes it instead so a click inside a wrapping label reaches its checkbox |
| `script.src` | HTML reflects it *as a URL*, so it answers the resolved absolute URL | the raw attribute value, so `<script src="a.js">.src` is `"a.js"` where a browser answers `"http://…/a.js"` |
| `select.selectedIndex` with no `selected` option | 0 — the selectedness-setting algorithm picks the first option of a non-`multiple`, display-size-1 select | −1 |
| `input.value = …` | the cursor moves to the end and the selection is dropped | `SelectionStart`/`SelectionEnd` are left where they were, so they can point past the end of the new value |
| `input.setSelectionRange` on a `type=checkbox` | `InvalidStateError` — the type does not support selection | answers, so the type test has to be the caller's |

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

The cache is also where a page's DOM is *counted*. `DomRealm.MaxNodes` — `BrowserOptions.MaxDomNodes` when a
page runtime set it, and zero, meaning no limit, for a binding installed on its own — makes the projection
that would pass the ceiling a `RangeError` in the script that asked for it. It counts node wrappers because
that is the one place every projection passes through and because the wrapper table is what a script's DOM
growth actually costs an engine — a different quantity from the one the parse bounds, and deliberately so:
seeding this counter from the parsed document's size would make merely walking a document of the permitted
size a refusal. [`Runtime/AGENTS.md`](../Runtime/AGENTS.md#budgets-what-a-turn-is-and-which-constraints-can-bound-one)
has the other side.

Read [`Jint/Native/Object/AGENTS.md`](../../Jint/Native/Object/AGENTS.md) before changing any of them: the
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

