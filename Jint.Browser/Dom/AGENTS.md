# Agent instructions: the DOM bindings

> **Read this when:** You are touching `Jint.Browser/Dom/` or `tools/dom-bindings/`: generated bindings,
> hand-written wrappers, the binding contract or its emitter.
>
> Read the repository-root [`AGENTS.md`](../../AGENTS.md) first, then
> [`Jint.Browser/AGENTS.md`](../AGENTS.md) for native/parser ownership, performance and scope.

### How the explicit contract is read as WebIDL

`tools/dom-bindings/contract.json` is the generator's input, not a description inferred from dependency
assemblies. `BindingGenerator.Run` calls `BindingContract.Load(...).ToModel()` and `Emitter.Emit()`.
The JSON records interface/parent names, receiver types, type-map eligibility, wrapper kinds, interface-object
availability, member bodies, method lengths, constants, unscopables, collection accessors and string enums.

**Change the contract, regenerate, review the emitted diff.** Never hand-edit `Generated/*.g.cs`.
`DomBindingsStalenessTests` runs the same generator in memory, checks diagnostics and compares output;
`JINT_DOM_BINDINGS=update` regenerates it. Commands are in
[`tools/dom-bindings/README.md`](../../tools/dom-bindings/README.md).

`BindingContract.ToModel` rejects duplicate interface names, generated fields and member names, and requires
parents before children. The emitter builds the registry and orders type-map candidates most-derived first.
JavaScript conversions live in the recorded member bodies: changing a native CLR signature does not
automatically update the contract's WebIDL behavior.

### The override table

`overrides.json` and `pin.json` retain the old extraction decisions and assembly provenance. They are **not
generation inputs**, and changing them alone changes no binding. `Inventory`, `ModelBuilder`, `Conversions`
and `Nullability` remain in the generator sources as extraction-era code; the active entry point does not
call them. Do not revive assembly reflection or infer behavior from their old `[DomName]` rules.

When replacing a getter, setter or operation, preserve its contract name, length, conversions and descriptor
attributes. Keep computed getters when changing only reflection on setting: a meter getter's clamping is
not the same algorithm as serializing its setter's argument. Record the actual body/adapter in the contract,
not a historical `hooks`, `reflected` or `additions` row.

### The interfaces the generator cannot see

The manual surface is an explicit implementation choice, not a limitation of external metadata:

- **Native elements are selected by namespace and local name.** `DomRealm` calls `DomTypeMap.For(Node)`
  in `DomTypeMap.Native.cs`, separately from the generated CLR type map for non-nodes.
  One native `Element` class represents many WebIDL brands; a type-only cache cannot distinguish a `div`
  from a `button`.
- **Manual element shapes include `HTMLDListElement`, `HTMLDirectoryElement`, `HTMLFontElement`,
  `HTMLFrameElement`, `HTMLFrameSetElement` and `SVGAElement`.** Their reflected members use
  `ReflectedAttribute`; do not put them on `HTMLElement` to avoid declaring the correct brand.
  SVG local-name matching is case-sensitive. `DomAttributeTokenList` backs the SVG anchor's `relList`.
  The SVG anchor inherits `SVGGraphicsElement`, like the generated graphics elements.
  `Svg/` owns the live value objects; its per-realm weak element table caches raw attribute strings
  and reparses only on access. Never add SVG parse state to native elements or tree construction.
- **`XMLDocument` and `StaticRange` are manual brands.** `DocumentKind.Xml` selects the former, except that
  `new Document()` explicitly wraps its native XML document as `Document`.
- **Constructibility is decided by `DomConstructors` and `DomInterfaceObject`.** The table includes
  `Document`, `DocumentFragment`, `Comment`, `Text`, `ProcessingInstruction`, `Range` and `StaticRange`,
  plus the separate legacy `Image` factory. Ordinary element interface objects remain illegal constructors
  except through a registered custom-element subclass. The associated document comes from the owning
  `DomRealm`, with an inert XML document used when none is associated.

### The conversion table, and where it diverges from a browser

**A non-nullable `DOMString` return maps CLR null to the empty string; a nullable one preserves null.**
`DomConvert.Text` and `NullableText` implement the distinction; the contract chooses which to call.
Arguments are a separate decision: `NullableText` accepts null/undefined as null, whereas ordinary string
conversion makes `el.id = null` write `"null"`. Use null-to-empty only where WebIDL requires it.
No current generator pass discovers this from nullable-reference metadata.

The contract also states integer/boolean/number conversions, interface receiver checks and native wrapping.
`DomConvert.Window` asks the host for the global corresponding to a `DomBrowsingContext`; never project a
second window wrapper over an engine's global. Native `Node` and `Attr` identities cross through the same
realm/cache machinery even though `Attr` is not a subclass of native `Node`.

Several observable rules must survive changes to the projection:

- **Collection `length` is a prototype accessor.** `DomCollectionBase` opts out of `ArrayLikeObject`'s
  default own property. Length-consuming lanes must observe `[[Get]]`, including a replaced getter.
  `DomRealm.CaptureLengthAccessor` captures the pristine getter when it creates the prototype, before
  author code can replace it; the wrapper can then supply its count only while that exact getter resolves.
  A new collection kind must participate in that capture, and the accessor must agree with the wrapper's
  count. `JINT_HOST_CONTRACT_VERIFICATION=1` checks the promise.
- **`Symbol.iterator` belongs on the interface declaring indexed access**, not every derived interface.
  Its per-realm value is `%Array.prototype.values%` through `DomIterator.ArrayValues`, so
  `NodeList.prototype[Symbol.iterator] === Array.prototype[Symbol.iterator]`.
- **ARIA element references are not only reflected strings.** `AriaElementReflection` retains an explicitly
  set element; it detects later attribute changes by value. A manual write of the same empty string cannot
  be distinguished from the IDL setter's own empty string, so it retains the reference.
- **Repeated property reads of a multi-match `document.all[name]` reuse a live collection.**
  `namedItem`, `item` and the legacy caller can create fresh collections, but the property lane memoizes
  its last named result so value and descriptor reads agree during host-contract verification.
- **Absent `tabindex` still reflects as 0.** That is not a focusability test; `FocusController` uses the
  element kind and content attribute directly.

### Every member body goes through one invoker, and that is where a refusal is converted

`DomFailures.Guard` wraps reads; `GuardMutation` wraps setters and operations except the emitter's explicit
read list. New operations default to mutation scopes. The scope invalidates Browser layout on entry/exit
and prevents retained measurements during reentrant callbacks. Manual writes, including named-property
hooks, must also use `DomRealm.MutateLayout()`.

**Mutation completion is a semantic boundary, not just invalidation.** `PrepareMutation` recovers pending
native notifications and installs needed watches. After a successful complete native call,
`CompleteMutation` drains resource changes, custom-element reactions and file-transfer changes.
Native `PendingRecord` callbacks must schedule/record only; never execute author code mid-mutation.

`Emitter.AppendGuardedBody` applies the shared exception policy rather than emitting a `catch` into each
body. Native `Jint.HtmlParser.DomException.Name` supplies the DOM error name; `ArgumentException` and
`Jint.Runtime.TypeErrorException` become `TypeError`; `NotSupportedException` / `NotImplementedException` become
`NotSupportedError`. A `JavaScriptException` raised by a body, constraints and cancellation remain outside
that filter. Preserve the engine's interop contract
([`Jint/Runtime/Interop/AGENTS.md`](../../Jint/Runtime/Interop/AGENTS.md)).

Use `DomFailures.Refuse` for a Browser algorithm's own named DOM refusal. Record limitations in
[`divergences.md`](divergences.md), not an obsolete dependency workaround list.
`DomSelectors` compiles through the native `SelectorCompiler` before matching, even on an empty tree;
native grammar handles forgiving lists, escapes and EOF recovery without a Browser text-rewriting prepass.
`SelectorEnvironment` carries the page's focus, pointer press, fragment target and control facts so CSS and
DOM queries read the same state.

### DOM §7's XPath, and CSSOM's `CSS` are in the package file

The Browser XPath interfaces and `CSS` namespace are hand-written in `Dom/Views/`, using native
XPath and CSS facilities. The Browser cursor's namespace policy, snapshot results, bounded native
evaluation and `CSS.supports` are described in
[`../AGENTS.md`](../AGENTS.md#dom-7s-xpath-and-cssoms-css).

### Wrapper identity, and the two classes that are not one hierarchy

**One `ConditionalWeakTable<object, ObjectInstance>` per engine, keyed on native identity.**
A node retained by the tree keeps its wrapper and expandos alive; a node dropped by both tree and script
can collect with its wrapper. `DomRealm` is reached through a weak table on the engine rather than through
`Engine.HostDefined`, which belongs to the embedder.

**Constructor and prototype state is per Realm; wrapper identity is shared per engine.** Read
`DomRealm.OwningRealm`, never the realm that happens to be executing. `RealmScope` binds shaped prototype
construction to that realm as well. Do not manufacture a second wrapper when a node is adopted.

**Parser nodes need no eager creation-realm walk.** Associating a document installs its weak
`Document.AdoptionObserver` (`INodeAdoptionObserver`). Before native adoption changes an owner, the hook
records the node/attribute's original realm. Otherwise a first lookup takes the owner document's realm;
known identities only read the shared weak table. `AssociateTemplateContents` associates the inert template
owner document without traversing its contents. Existing explicit `RecordSubtree` boundaries for binding
operations are not a reason to add one to every parse. A document must be associated before its native
nodes leave it; the hook cannot reconstruct an original realm for a document Browser never knew.
Focus and activation ownership remain engine-wide and receiver-gated, separate from creation brands.

The wrappers share `IDomWrapper`, not a common wrapper base:

- **`DomNodeObject : JsEventTarget`** participates in Jint's tree dispatch. `IsNode` selects that lane;
  `GetParent` alone is insufficient. It supplies native assigned slots and shadow parents, handler
  reconciliation, and `ActivationBehaviors` including legacy pre-activation/cancellation.
- **`Collections/DomCollectionObject : ArrayLikeObject`** serves indexed/iterable collections through one
  generated accessor. Keep different read strategies in the existing wrapper when it already serves the
  interface: a sibling wrapper changes the CLR type profile of every hot indexed call site.
- **Static and live NodeLists share a wrapper, not a caching policy.** `DomStaticNodeList` is the selector
  snapshot; only it can memoize one wrapper per index. The memo is the same identity `DomRealm.WrapNode`
  supplies. A live collection must not retain such an index cache: membership changes would return stale
  nodes and pin removed ones.
- **`Collections/DomIndexedNodeObject : DomNodeObject`** supplies indexed/named properties for nodes such
  as forms and selects. It must remain a node/event target; replacing it with an `ArrayLikeObject` loses
  tree dispatch. Value, descriptor and key reads must describe the same current membership.
- **`Collections/DomHtmlAllCollectionObject : DomCollectionBase`** supplies `document.all`'s
  `[[IsHTMLDDA]]`, callable behavior and HTML-specific supported names. A multi-match name is a live
  collection, not just its first element.
- **`Collections/DomNamedMapObject : NamedPropertyObject`** serves `dataset`. `DomStringMapAdapter`
  handles camelCase/name conversion, validation and real attribute deletion.
- **`DomObject : ObjectInstance`** handles other projected objects without overriding ordinary access.

`DomRealm.MaxNodes` bounds the number of node wrappers, not the parsed tree's node count. Exceeding it is a
script `RangeError`. `BrowserOptions.MaxDomNodes` also bounds the parsed document separately; seeding the
wrapper counter from the parse would make walking an allowed document fail. See
[`Runtime/AGENTS.md`](../Runtime/AGENTS.md#budgets-what-a-turn-is-and-which-constraints-can-bound-one).

Read [`Jint/Native/Object/AGENTS.md`](../../Jint/Native/Object/AGENTS.md) before changing these wrappers:
host subclasses owe coherent access lanes and host-contract verification.

### Shape discipline

Every interface prototype is a `JsObjectShape.Instantiate` result; `DomPrototypeTests` checks shared shapes.
Adding an undeclared property deoptimizes the prototype and loses prototype-method caching. The
`constructor` slot is declared by the shape, then filled with `DefineOwnPropertyUnchecked`: an in-place
replacement, not permission to add arbitrary slots.

`WebIdlPropertyAttributeTests` checks WebIDL attributes, not ECMAScript built-in defaults:
**an operation is enumerable**. Keep process-shared shapes free of engine/realm state and use per-realm
slots for values that need it.
