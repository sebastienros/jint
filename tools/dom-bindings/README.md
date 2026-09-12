# The DOM binding generator

`Jint.Browser.BindingGenerator` reads the pinned **AngleSharp** and **AngleSharp.Css** assemblies through
`System.Reflection.MetadataLoadContext`, treats their `AngleSharp.Attributes` as the WebIDL they effectively
are, and emits the checked-in `Jint.Browser/Dom/Generated/*.g.cs`.

`pin.json` records the two versions the checked-in output was produced from. It and the `AngleSharp` /
`AngleSharp.Css` entries in `Directory.Packages.props` have to say the same thing, which
`Jint.Tests.Browser/DomBindingsPinTests.cs` checks by reading both — plus the versions the test process
actually loaded, so a pin cannot drift from the reference.

Nothing about AngleSharp is vendored here: the assemblies come from the package reference, and the generator
is pointed at whatever the build resolved.

`DomReturnType` supplies an operation's IDL return type when its CLR signature cannot change. For example,
AngleSharp 1.8.0's `querySelectorAll` still declares `IHtmlCollection<IElement>`, but its result also implements
`INodeList`. The generator casts to that annotated type and selects the NodeList projection explicitly;
ordinary HTMLCollection-returning members retain their named properties. `DomSameObject` is only a metadata
promise, not an identity implementation: the wrapper cache still depends on the object AngleSharp returns.

## A bump is a code change

Re-pointing the pin is not a configuration edit that lands on its own. AngleSharp adds interfaces, renames
`[DomName]`s, changes a signature from `string` to `string?` and moves a member between interfaces, and every
one of those changes the generated surface. So a bump is:

1. Update both versions in `Directory.Packages.props` and in `pin.json`.
2. Regenerate (below) and **read the diff of `Jint.Browser/Dom/Generated/`**. That diff is the upstream
   change, stated in the vocabulary this repository compiles.
3. Read the report's *diagnostics* and *skipped members* sections. A new diagnostic is something nobody has
   looked at; a new skip is a member that stopped crossing the boundary.
4. Fix whatever the diff broke, in the same pull request.

The same discipline `tools/devtools-protocol` and `Jint.Tests.Test262/Test262Harness.settings.json`'s
`SuiteGitSha` carry, and for the same reason: a pin that moves without anybody reading what moved is an
upstream change landing unread.

## Regenerating

The short way, which is also what fails when the checked-in code is stale:

```bash
JINT_DOM_BINDINGS=update dotnet test -c Release Jint.Tests.Browser/Jint.Tests.Browser.csproj \
    --filter FullyQualifiedName~DomBindingsStalenessTests
```

The long way, which additionally prints the inventory and the skip report:

```bash
dotnet run --project tools/dom-bindings/Jint.Browser.BindingGenerator -c Release -- \
    --core  <path>/AngleSharp.dll \
    --css   <path>/AngleSharp.Css.dll \
    --overrides tools/dom-bindings/overrides.json \
    --output Jint.Browser/Dom/Generated \
    --report /tmp/dom-bindings-report.txt
```

`--core` and `--css` are the two assemblies the build resolved; the easiest way to find them is
`artifacts/bin/Jint.Browser/release_net8.0/AngleSharp.dll` after a build, or the NuGet cache at the pinned
version. The staleness test does not need them named at all: it asks the loaded `IElement` and
`ICssStyleDeclaration` types where they came from.

A Roslyn source generator was considered and rejected, for the reasons `tools/devtools-protocol/README.md`
gives: a binding surface is exactly the kind of thing whose diff a reviewer wants to read, and the build stays
analyzer-free.

## The two files beside the generator

- **`pin.json`** — the versions above.
- **`overrides.json`** — the curated half of the binding: the interfaces the runtime owns instead, the shapes
  that are hand-written, the members a later campaign item owns, the members routed through a host hook, the
  reflected content attributes, the `DOMString?` list, and the two enum decisions the heuristics cannot make.
  Every entry carries a reason, and an entry naming a member the pinned assemblies no longer have becomes a
  diagnostic. One list is the exception to that shape and says so in the file: `reflected` states what **HTML**
  says about an attribute rather than correcting what AngleSharp says, so its entries replace a projection
  rather than colliding with one, and the report names every one of them.
  What each list is for, and why it exists, is the table below.

A member the generator simply could not convert is **not** in `overrides.json`. It is skipped with the reason
the generator worked out, and that reason is in the report. The split is deliberate: the table is for
decisions, the report is for consequences.

## What every list in `overrides.json` is for

| List | What it is for |
| --- | --- |
| `excludedInterfaces` | An interface the runtime owns instead, or one the standard no longer has. Today: `IWindow` (campaign item R1), and `ISettableTokenList` — DOM merged `DOMSettableTokenList` into `DOMTokenList` in 2016 and AngleSharp still carries the `[DomName]`, so excluding it lets `DomTypeMap` fall through to `ITokenList`. Excluding an interface removes it from every member closure *and* from the conversion table, so a member typed with it is skipped: `area.ping` and `td.headers` are `reflected` string entries because of it, which is HTML's type for both. |
| `manual` | An interface the binding writes by hand: its shape in `DomManualShapes`, and optionally the two other things AngleSharp's metadata answers wrongly for it — `wrapper` names the `DomWrapperKind` its instances get, and `inherits` names what its prototype inherits, with the **empty string** meaning `Object.prototype`. Two entries. `IHtmlCollection<T>` uses the shape alone, because its generic invariance keeps a member body from naming its receiver. `IHtmlAllCollection` uses all three: HTML §4.13.2.3 makes `HTMLAllCollection` a standalone interface with a named lookup that answers an element *or* a collection, an `item` taking a name or an index, a legacy caller and Annex B's `[[IsHTMLDDA]]` slot — none of which an `HTMLCollection` can carry — while AngleSharp models it as an `IHtmlCollection<IElement>`. A manual interface contributes no members to any closure — its children inherit them through the prototype chain. |
| `skip` | A member whose AngleSharp implementation must not be projected: HTML's two union-typed `add` operations (re-declared over `DomUnionMembers`), navigation (`location.assign`, `location.href`'s setter), the parser (`document.open`/`close`/`load`), `document.createEvent` whose AngleSharp `Event` must never reach script, `DOMImplementation.createHTMLDocument` whose title AngleSharp makes required, and the six the events bridge re-declares because AngleSharp's own do nothing — see [`divergences.md`](../../Jint.Browser/Dom/divergences.md). These are re-declared in `additions`. A nonstandard member such as `HTMLMetaElement.charset` is instead omitted: a CLR annotation cannot add a member HTML's IDL does not declare. `half: "setter"` skips the write half only. |
| `hooks` | A member routed through `DomHostHooks` so the package can replace its body: the `innerHTML` and `outerHTML` setters, `insertAdjacentHTML`, `document.write`/`writeln`, and `setAttribute`/`removeAttribute` — the one write of an attribute this package can see, and therefore where a handler content attribute takes its position in the element's listener list. The default implementations *are* the AngleSharp call, so the seam costs nothing until R3 uses it. `"half": "getter"` replaces the *read* of an attribute, which is what a member whose value the host has and AngleSharp does not needs: `document.currentScript`, `readyState`, `URL`, `documentURI`, `referrer`, `cookie` and `Node.baseURI` are accessors on their prototypes because of it, where they used to be own properties written onto the document wrapper. It is also the form for a member AngleSharp *has* and answers by a different rule than the standard's — `document.location` (null with no browsing context), `characterSet` (the Encoding Standard's name, with `charset` and `inputEncoding` added beside it), `contentType` (the one the algorithm that made the document gave it) and `Element.tagName` (uppercased only in an HTML document); [`divergences.md`](../../Jint.Browser/Dom/divergences.md) carries one row each. `"returns": true` is the form for a member whose *answer* belongs to the host rather than its effect, which is `document.createElement`, `createElementNS`, `Node.cloneNode` and the four collection queries (`getElementById`, `getElementsByClassName`, `getElementsByTagName`/`NS`) whose whole algorithm is DOM's rather than AngleSharp's: with the synchronous custom elements flag set the element a defined name produces is the constructor's and not AngleSharp's. **Reach for it before `skip` + `additions`** — a hook stands in front of a generated member, so the member keeps its arity and its name, and a member AngleSharp later grows under that name is still reported rather than shadowed. The same class carries `WrapperCreated`, which is the other direction — a member the generator could not emit at all, added to one wrapper; it is also where the events bridge registers an element's handler content attributes. |
| `additions` | A member the **standard** puts on a generated interface and AngleSharp's metadata cannot express: a callback parameter, a stringifier with no `[DomName]`, a Shadow DOM v0 spelling, a missing `[DomName]`, the whole of CSSOM View's box model (`getBoundingClientRect` and its `client*`/`scroll*`/`offset*` family, answered from the flat layout — [`Jint.Browser/Runtime/AGENTS.md`](../../Jint.Browser/Runtime/AGENTS.md)), the legacy event-creation surface (`document.createEvent`), and the operations whose body is an event rather than a DOM call (`click`/`focus`/`blur`, `form.submit`/`requestSubmit`/`reset`, `document.activeElement`/`hasFocus`). Two forms, and an entry uses exactly one: **reach for the member form**, which names one member and goes through the model like any projected member, so the generated file names it and a member AngleSharp later grows under that name is reported rather than shadowed; the `"extend"` form hands the builder to a method and exists only for a *family* whose member list is computed, which today is HTML's event handler IDL attributes. `Overrides.AdditionEntry` has the whole of why, including what the extend form gives up. Either way it adds rather than replaces, so the interface stays **one** shape — the only way to add a member to a class rather than to one object without costing the prototype its shape. |
| `reflected` | The **standard's** half of the table rather than AngleSharp's: which of [HTML §2.6.1](https://html.spec.whatwg.org/multipage/common-dom-interfaces.html#reflecting-content-attributes-in-idl-attributes)'s thirteen reflection algorithms one IDL attribute takes, plus its keywords, its invalid and missing value defaults and its range. None of that is in a CLR signature — a `long` and a `long` limited to only non-negative numbers are the same `Int32` property, and `<col span>` defaulting to 1 while `<select size>` defaults to 0 is nowhere in the metadata — which is why it is a table and not a heuristic. An entry **replaces** the member AngleSharp projects under that name, the opposite of `additions`, because most reflected attributes *are* projected already, from a property that hands back the raw attribute value or parses it with the wrong default; neither presence nor absence is an error, and the generated report says which each entry was — `replaces`, `adds`, or `sets`. That third word is `"setterOnly": true`, which replaces the **setter alone** and keeps the projected getter's body verbatim: HTML defines several IDL attributes as reflecting *on setting* while the read is a computation no reflection type expresses, and where the pinned assemblies already make that computation a whole replacement is a regression. `<meter>`'s six members are the case it exists for — their getters are HTML §4.10.14's clamping and defaults and their setters were writing .NET's number format. A row that asks for it with no projected getter to keep, or that also names a `hooks` getter for the same member, is a diagnostic rather than generated code; where the **host** has the right read (`img.width`) that getter hook is the tool instead. The bodies are one shared `ReflectedAttribute` descriptor per member in `DomReflected.g.cs`, process-shared and immutable, so HTML's rules for parsing integers, non-negative integers and floating-point number values exist once rather than once per emitted member. |
| `unscopables` | Which of an interface's members are `[Unscopable]`, which is the **standard's** half of the table rather than AngleSharp's: nothing in the metadata says it and nothing could, because it is a statement about how a name behaves inside a `with` and not about the CLR property. DOM §4.2.8 and §4.2.9 mark every member of `ChildNode` and `ParentNode`, so five interfaces carry one. A non-empty list puts an `@@unscopables` object on the interface prototype object (`DomUnscopables`), and every name has to be a member the interface really declares — one that is not is a diagnostic, because Web IDL builds the object out of the interface's own members. It matters outside a conformance suite because HTML compiles an inline event handler with the element, its form owner and the document on the scope chain: without it, `<div onclick="remove()">` calls the element's `remove()`. |
| `nullableStrings` | The members whose **return** type is `DOMString?` rather than `DOMString`. See the conversion table in [`Jint.Browser/Dom/AGENTS.md`](../../Jint.Browser/Dom/AGENTS.md#the-conversion-table-and-where-it-diverges-from-a-browser). |
| `nullToEmptyStrings` | The IDL attributes whose setter carries WebIDL's `[LegacyNullToEmptyString]`, or whose own steps say the same thing in prose: JavaScript `null` writes `""` rather than the string `"null"`, and `undefined` still writes `"undefined"`. A CLR `string` setter cannot carry the extended attribute and AngleSharp's nullable metadata means the *opposite* thing, so this is the standard's half of the table. Today `CharacterData.data` and `Node.nodeValue`. |
| `nullableParameters`, `nonNullableParameters` | The two directions of *argument* nullability; the conversion table in [`Jint.Browser/Dom/AGENTS.md`](../../Jint.Browser/Dom/AGENTS.md#the-conversion-table-and-where-it-diverges-from-a-browser) says which type reads which, and why only one of the two is a list of everything. |
| `stringEnums`, `constants` | The two enum decisions the heuristics in [`Jint.Browser/Dom/AGENTS.md`](../../Jint.Browser/Dom/AGENTS.md#how-anglesharps-attributes-are-read-as-webidl) cannot make. |

## Adoption by another host

The [X6 adoption proposal](../../docs/integration/upstream-adoption.md#proposal-for-anglesharp-js) inventories
the generator, runtime and event bridge, including the internal APIs that currently prevent independent
consumption. It describes a small AngleSharp.Js integration experiment and its acceptance conditions;
it is a review draft, not a published binding-only package or a delivered upstream offer.
