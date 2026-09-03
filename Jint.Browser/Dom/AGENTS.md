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
| `skip` | A member whose AngleSharp implementation must not be projected: navigation (`location.assign`, `location.href`'s setter), the parser (`document.open`/`close`/`load`), `document.createEvent` whose AngleSharp `Event` must never reach script, `DOMImplementation.createHTMLDocument` whose title AngleSharp makes required, the three whose answer for a defined custom element name is the constructor's element rather than AngleSharp's (`document.createElement`, `createElementNS` and `Node.cloneNode`), and the six the events bridge re-declares because AngleSharp's own do nothing — see the divergence table. Every one of them is re-declared in `additions`, so a skip here is a *replacement* and not an absence. `half: "setter"` skips the write half only. |
| `hooks` | A member routed through `DomHostHooks` so the package can replace its body: the `innerHTML` and `outerHTML` setters, `insertAdjacentHTML`, `document.write`/`writeln`, and `setAttribute`/`removeAttribute` — the one write of an attribute this package can see, and therefore where a handler content attribute takes its position in the element's listener list. The default implementations *are* the AngleSharp call, so the seam costs nothing until R3 uses it. The same class carries `WrapperCreated`, which is the other direction — a member the generator could not emit at all, added to one wrapper; it is also where the events bridge registers an element's handler content attributes. |
| `additions` | A member the **standard** puts on a generated interface and AngleSharp's metadata cannot express: a callback parameter, a stringifier with no `[DomName]`, a Shadow DOM v0 spelling, a missing `[DomName]`, the whole of CSSOM View's box model (`getBoundingClientRect` and its `client*`/`scroll*`/`offset*` family, answered from the flat layout — [`../Runtime/AGENTS.md`](../Runtime/AGENTS.md)), the legacy event-creation surface (`document.createEvent`), and the operations whose body is an event rather than a DOM call (`click`/`focus`/`blur`, `form.submit`/`requestSubmit`/`reset`, `document.activeElement`/`hasFocus`). Two forms, and an entry uses exactly one: **reach for the member form**, which names one member and goes through the model like any projected member, so the generated file names it and a member AngleSharp later grows under that name is reported rather than shadowed; the `"extend"` form hands the builder to a method and exists only for a *family* whose member list is computed, which today is HTML's event handler IDL attributes. `Overrides.AdditionEntry` has the whole of why, including what the extend form gives up. Either way it adds rather than replaces, so the interface stays **one** shape — the only way to add a member to a class rather than to one object without costing the prototype its shape. |
| `nullableStrings` | The members whose IDL type is `DOMString?` rather than `DOMString`. See the conversion table below. |
| `stringEnums`, `constants` | The two enum decisions the heuristics above cannot make. |

**A member the generator could not convert is not in this table.** It is skipped with the reason the generator
worked out, and the reason is in the report the regeneration prints. That split is deliberate: this file is
for decisions, the report is for consequences.

### The two interfaces the generator cannot see

`DomManualInterfaces` and `DomConstructors` are the whole of what the override table cannot express.

- **`DomManualInterfaces.For` answers two questions, and the second is HTML's element interface rule**: a
  name in the HTML namespace that is a valid custom element name is an `HTMLElement`, and only a name that is
  not is an `HTMLUnknownElement`. AngleSharp builds the same `HtmlUnknownElement` for both.
- **`HTMLFrameSetElement` is declared by name and selected by local name.** AngleSharp models `<frameset>`
  with the plain `IHtmlElement` — there is no `IHtmlFrameSetElement` and no `[DomName("HTMLFrameSetElement")]`
  in the pinned assemblies — so `DomTypeMap`, which keys on the CLR type, cannot tell a frameset from a
  `<div>`. Its shape is empty: HTML gives the interface no members beyond `WindowEventHandlers`, which this
  package puts on `HTMLElement`. Its index continues `DomInterfaces`' own, which is what keeps `DomRealm`'s
  per-engine arrays a dense array.
- **`Document` and `DocumentFragment` are the interface objects a script may call `new` on.** AngleSharp
  puts `[DomConstructor]` on no `[DomName]` interface at all, so the generator can never learn that an
  interface is constructible and `DomInterfaceObject` refuses every `new` — which is also what a browser
  answers for `new HTMLDivElement()`. The document it makes is DOM's: an XML document with no doctype, no
  document element and no browsing context; the fragment's node document is the page's.

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

### Every member body goes through one invoker, and that is where a refusal is converted

`DomFailures.Guard` wraps every emitted body — an operation, both halves of an attribute — and the emitter
wraps it in exactly one place (`Emitter.AppendGuardedBody`); `Views/ViewInstaller`'s `Selection` shape is the
one hand-written shape that takes it too, because its members reach AngleSharp's range algorithms.
**Nothing generated carries a `catch`**, because
two thousand copies of one decision is two thousand chances to disagree with it. What crosses is
AngleSharp's `DomException` as the `DOMException` its `DomError` names, an `ArgumentException` as a
`TypeError` (WebIDL's answer for an argument no conversion accepts), and a `NotSupportedException` /
`NotImplementedException` as `NotSupportedError`; **everything else keeps the engine's own interop
behaviour**, which [`Jint/Runtime/Interop/AGENTS.md`](../../Jint/Runtime/Interop/AGENTS.md) says is frozen —
so a `JavaScriptException` a body raised itself, and every constraint and cancellation signal, are outside
the filter. A member the standard gives a *different* name to is written by hand and calls
`DomFailures.Refuse`, which is the same door `DomSelectorMembers` uses; the register in [`divergences.md`](divergences.md) is what
says which those are.

Divergences that are **AngleSharp's** — where it answers differently from the standard and the binding has to
work around it — are the register in [`divergences.md`](divergences.md), which is data rather than instruction
and so is not budgeted here. **Add a row there for every one you find**, and never work around a divergence
silently; never open an issue on the AngleSharp repositories without being asked to.
Divergences that are **AngleSharp's** — where it answers differently from the standard and the binding has to
work around it — are the register in [`divergences.md`](divergences.md), which is data rather than instruction
and so is not budgeted here. **Add a row there for every one you find**, and never work around a divergence
silently; never open an issue on the AngleSharp repositories without being asked to.

The `dataset` one has a visible consequence inside the binding, and the one place a workaround is legitimate:
the generated `SupportedNames` filters out a `null` value, because the projection's three hooks must agree at
the same instant or host-contract verification fails. That is keeping *our* contract, not mending AngleSharp's.

### DOM §7's XPath, and CSSOM's `CSS`

Two surfaces neither pinned assembly declares, so neither could be generated: there is no
`[DomName("evaluate")]` and no `[DomName("escape")]` anywhere in AngleSharp or AngleSharp.Css. Both are
hand-written in `Dom/Views/` beside `DOMParser`, and the three `Document` members XPath adds are
`overrides.json` `additions`. `Jint.Tests.Browser/Fixtures/htmx` is why: htmx 2 builds an `XPathEvaluator`
expression and calls `CSS.escape` at the top level of its bundle.

- **The XPath engine is `System.Xml.XPath` over `AngleSharp.XPath`'s `HtmlDocumentNavigator`** — the
  AngleSharp project's own package, referenced for this and nothing else, and exactly the seam the BCL's
  XPath 1.0 evaluator takes. Writing an evaluator here instead is the one thing this package is not for.
- **Namespaces are ignored, and that is what makes `//div` match.** An HTML element is in the XHTML
  namespace, so an unprefixed XPath 1.0 name test — which is what every page writes — would match nothing
  if the navigator reported it; `AngleSharp.XPath`'s own default is the same choice. The consequence is
  stated rather than hidden: a *prefixed* test (`svg:circle`) compiles, because a resolver the page
  supplied is consulted while the expression is compiled, and then matches nothing.
- **A node set is materialized at evaluation**, so `invalidIteratorState` is always `false` and an
  iterator survives a mutation instead of raising `InvalidStateError`. DOM's iterator is live and needs a
  mutation signal this has none of; the direction is the safe one, because what it removes is a page
  throwing.
- **`CSS` is a namespace object, not an interface** — no constructor, no prototype, `[object CSS]` — and
  it carries both members rather than the one htmx needs, because `window.CSS && CSS.supports(…)` is how
  the feature is detected and half of it is a trap. `escape` is CSSOM's serialize-an-identifier;
  `supports` parses the condition as an `@supports` rule and asks AngleSharp.Css's own
  `IConditionFunction.Check`, so what this claims to support is exactly what the cascade can act on.
- **`DomConstructors` grew a second entry**: `new DocumentFragment()`, which DOM gives a constructor and
  htmx builds for every swap whose response starts with `<html>` or `<body>`. The shortness of that table
  is still the point.

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

