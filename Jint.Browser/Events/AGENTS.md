# Agent instructions: the events bridge

> **Read this when:** You are touching anything under `Jint.Browser/Events/`, dispatching an event a page can
> hear, or one of the members that stand in for an AngleSharp one that does nothing — `click`, `focus`,
> `blur`, `document.activeElement`, `document.hasFocus`, `document.createEvent`.
>
> This is one of the co-located instruction files indexed from the repository-root
> [`AGENTS.md`](../../AGENTS.md). Read that first, and read [`Jint.Browser/AGENTS.md`](../AGENTS.md) beside
> it — it carries the package's principle, what is generated versus hand-written, and the divergence tables
> these sections cite. [`Runtime/AGENTS.md`](../Runtime/AGENTS.md) carries the loop every one of these events
> is dispatched on and the one-thread rule that governs all of it. Nothing below is repeated in either.
> The design this implements is [`docs/design/headless-browser.md`](../../docs/design/headless-browser.md) §5.

### The events bridge

**AngleSharp's event bus is neither observed nor driven by script** (design doc §5). Everything script-visible
is a Jint `Event` dispatched through the engine's tree-aware dispatcher, at the algorithm points this package
owns. `Events/` is that: the interfaces, the handler attributes, the activation behaviours, focus and the
input dispatcher. Six things are worth knowing before changing any of it.

**AngleSharp has no activation behaviour at all, so none of it can be delegated.** Measured against the pinned
1.7.2, with and without a browsing context: `IHtmlElement.DoClick()` dispatches a `click` on AngleSharp's own
bus and returns — a checkbox it clicks does not toggle, a radio group does not change, a `<summary>` does not
open its `<details>`, an `<a href>` does not navigate. `DoFocus()` never assigns `IDocument.ActiveElement`, so
that property answers `null` for the life of every document where HTML says the body element. Those two are
why `click`, `focus`, `blur`, `document.activeElement` and `document.hasFocus` are `skip`ped in the override
table and re-declared through `additions`, and they are recorded in
[`../Dom/divergences.md`](../Dom/divergences.md).

**Which algorithm point raises which event.** The table is the artefact — an event fired anywhere else is a
second bus:

| Point | Fires | Where |
| --- | --- | --- |
| the parse ends | `readystatechange`, `DOMContentLoaded` (document), `load` (window) | `Runtime/Parsing/ParserDriver` |
| a click's activation behaviour | `input` + `change` (checkbox, radio, `<option>`), `submit`, `reset`, `toggle`, a forwarded `click` | `Events/ActivationBehaviors` |
| a form submits or resets | `invalid` at each failing control, `submit` (cancelable, carrying the `submitter`), `reset` (cancelable) | `Events/FormSubmission` |
| focus moves | `blur`, `focusout`, `focus`, `focusin`, and `change` for a control the user edited | `Events/FocusController` |
| a key edits a text control | `keydown`, `keypress`, `beforeinput` (cancelable), `input`, `keyup` | `Events/InputDispatcher`, `Events/TextEditing`, `Events/ContentEditing` ([below](#the-keyboard-and-the-editor-under-it)) |
| the selection moves | `selectionchange`, queued and coalesced | `Events/SelectionChange`, from `Events/TextEditing` for a text control and from `Dom/Views/JsSelection` for the document |
| a client drives a touch | `touchstart`, `touchmove`, `touchend`, `touchcancel`, and the four compatibility mouse events a tap leaves | `Events/InputDispatcher.Touch` ([below](#touch-a-gesture-outlives-the-command)) |

Every listener on that table returns to a microtask checkpoint, because the point that fires it is a turn of the loop rather than a script — see [`Jint/WebApi/AGENTS.md`](../../Jint/WebApi/AGENTS.md#web-apis). `AnimationFrameLane` owes the same cleanup by hand, since one frame is one job over many callbacks.

`toggle` and `selectionchange` are the two that are **queued** rather than fired in place, on the engine's
own task queue, because their specifications say so — a test that clicks a `<summary>` or moves the caret has
to pump before it sees the event. `selectionchange` is also **coalesced**: the Selection API gives each
document and each text control a *has scheduled selectionchange event* flag, so a script that moves the
caret ten times in one turn is heard once, after its own code returns. **Where it is fired is the target's
decision** — at the document, not bubbling, for the document's own selection; at the *element*, bubbling, for
an `<input>` or `<textarea>`, which is what lets the one `document.addEventListener("selectionchange", …)`
every editor library writes hear a caret moving inside a control. What no member can see is a script mutating
the boundary points of a `Range` it took out of `getRangeAt`, and `Events/SelectionChange` says why.

**Activation without a layout is exact where it is state and a seam where it is not.** Checkedness,
`details.open` and selectedness are pure state, so they are implemented outright, legacy pre-activation
rollback included. A link to follow, a form to submit and a file chooser to open leave the DOM, so they go to
`Events/BrowserActivationHost`, whose default *records* rather than acting; the navigation layer (campaign
item R5) replaces it through `BrowserEventRealm.ActivationHost`. A colour or date picker has nothing to pick
with and is honestly nothing rather than a guessed value. Focusability is computed from the element's kind and
its `tabindex` content attribute rather than from AngleSharp's `TabIndex`, which answers 0 for every element
including a bare `<div>`. **Selector pseudo-classes are deliberately not wired to any of it**: `:focus`,
`:checked` and `:hover` in a `querySelector` go to AngleSharp's own selector engine, which knows nothing about
the focus this package tracks, so `el.matches(':focus')` is not an answer about it.

**Handler content attributes need no notification from AngleSharp, and that is a decision.** The attribute's
text *is* the state: a handler slot records which text it was last reconciled against, and any difference is
what HTML's "set the content attribute" step observes. Three points reconcile — `DomHostHooks.WrapperCreated`, which
fires once for the wrapper that won the identity cache and is what registers a markup handler ahead of any
listener a script can add; `DomNodeObject.GetParent`, which the dispatcher calls exactly once per event path
item and which costs one `GetAttribute`; and a read or write of the IDL attribute. The alternatives — a document-wide `MutationObserver` (R4's lane) or AngleSharp's
`IAttributeObserver` service (a registration in the `IConfiguration` the page runtime builds, and one
AngleSharp uses internally) — would put a notification path in a file another campaign item owns to learn
something the attribute already says. The one case that needs more is `<body onload>`, because HTML redirects
it to the **window** and `load` never touches the body: `EventHandlerContentAttributes.InstallBodyHandlers`
builds that wrapper once when the parse ends.

**An interface a page can construct is not the same thing as an event the runtime fires, and the second is
not a reason to skip the first.** `BrowserEventInterfaces` builds `DragEvent`, `StorageEvent` and the two
device events, and the table above raises none of them: there is no drag, no second document sharing a
storage area and no sensor. What a page does with them is construct one from its
dictionary and dispatch it itself, which is what every synthetic-drag library, every storage-sync shim and
`document.createEvent`'s alias table need — so where a member's value would come from state this browser has
none of, the standard's construction-from-dictionary semantics are implemented in full and the class says
which state is missing. Two of them own more than an `Event`: `DragEvent` carries the real `DataTransfer`
`Dom/Files/` already builds, and `TouchEvent` — which *is* fired, by the row the table gained — carries
`Touch` and `TouchList`, which are
`Events/TouchInterfaces` rather than AngleSharp's — nothing in the pinned assemblies implements
`ITouchPoint` or `ITouchList`, so both are `excludedInterfaces` rows and
[`../Dom/divergences.md`](../Dom/divergences.md) records it. **Detection stays a client's decision**: the four
`ontouch*` handler attributes are exposed only under touch emulation
([`../Runtime/AGENTS.md`](../Runtime/AGENTS.md)), which is why the corpus's `TouchEvent` rows are declined on
a page nobody configured and why neither building the interface nor dispatching one changed what a page
detects.

**`isTrusted` is the line between a script and a client.** `element.click()` is untrusted — HTML's `click()`
says to fire the synthetic pointer event "with the not trusted flag set", and the activation behaviour still
runs, because trust decides what a page can *tell apart*, not whether the default action happens. Everything
`InputDispatcher` fires is trusted, because a protocol client driving a page stands in for a user.

**Form submission is split down the middle, and the middle is HTML's.** `Events/FormSubmission` is everything
up to and including "if the event was canceled, return" — the interactive validation that fires `invalid` at
each failing control and can refuse outright, then the `submit` event; `Runtime/FormSubmitter` is everything
after it — the entry list with its `formdata` event, the encoding, the request. The order is the
specification's and it is observable: validation is step 4.5 and `submit` is step 4.7, so a form whose
constraints fail never fires `submit` at all, and `form.submit()` skips both, which is the whole of what
distinguishes it from `form.requestSubmit()` and from a submit button. Constraint validation asks
`willValidate` before it asks `validity`, because that one member is what excludes a button, a disabled or
readonly control and a control inside a disabled fieldset — without it every `<button type=button>` in the
form would be examined.

**Which controls the two halves are about is one question with one answer, and it is not `form.elements`.**
`Dom/HtmlFormOwner` is HTML's *reset the form owner* — a connected listed element's `form` attribute outranks
every ancestor form — and `HtmlFormOwner.ControlsOf(form)` is the inventory the entry list, the static
validity check, the default button, implicit submission and a radio button group all walk. Reading a control's
`Form` off AngleSharp instead inverts that priority ([#3939](https://github.com/sebastienros/jint/issues/3939)),
and reading `form.elements` takes AngleSharp's ownership rule *and* drops every image button, so a form would
validate one set of controls and submit another. `form.elements` itself is AngleSharp's collection and stays
wrong; [`../Dom/divergences.md`](../Dom/divergences.md) records both halves.

**An image input selects a coordinate only out of an image it really has, and the position is measured
before any listener runs.** HTML gives an image button a *selected coordinate* and lets it be a real position
only when `src` identifies an available image the user agent displays *and* a pointing device activated it;
everything else is the fallback submit button's (0, 0) — no `src`, a fetch that failed, bytes in no container
`Media/ImageHeader` reads, `element.click()`, a dispatched `MouseEvent`, `requestSubmit`, a keyboard. Those
are three conditions and `ActivationBehaviors.RunInput`'s image arm asks all three: `Media/PageImages` says
*completely available*, the click says `isTrusted`, and `BrowserEventRealm.PendingImagePoint` says the
pointer was measured inside **this** button. **That measurement is taken from the release's own hit test
before any listener fires**, because the activation behaviour that reads it runs after `pointerup`, `mouseup`
and `click` and any of the three may move, adopt or detach the input first; measuring is not selecting, so a
canceled click promotes nothing and leaves the coordinate the last activation selected. **A tap takes it
too**, from the same hit test its compatibility mouse events are dispatched at: "activated using a pointing
device" is the condition, and a finger is one, so `InputDispatcher.DispatchMouse`'s release arm and
`InputDispatcher.Touch`'s compatibility sequence both park a point and both clear it in a `finally` — a
coordinate a tap could not select while a click at the same point could would be one input model disagreeing
with the other. The result lives on the *element* — a weak table on `BrowserEventRealm`, beside the
mouse press target — rather than on the event, because `new FormData(form, submitter)` reads it arbitrarily
long afterwards. `Runtime/FormSubmitter` appends x then y, with a name prefix only when nonempty. Its
inventory is submittable controls, not `form.elements`, which excludes image inputs: AngleSharp's tree
traversal and form-owner properties supply tree order and external association. **The position is in the flat
box model's geometry** and so can exceed the image's own `naturalWidth`; [`../Dom/divergences.md`](../Dom/divergences.md)
records why clamping it to the image would be a second geometry disagreeing with the one every box, hit test
and `offsetX` already answers from.

### Touch: a gesture outlives the command

`Input.dispatchTouchEvent` is `Events/InputDispatcher.Touch`, and three of its rules decide the rest.

**`touches` and `targetTouches` are about the surface, not the event**, so the contacts a gesture holds live
on `BrowserEventRealm.Touches` and outlive the command that pressed them — per engine, like the mouse's press
target, so a navigation mid-gesture leaves the next document with nothing down. A contact's `target` is fixed
when it goes down (Touch Events §"the touch point"), which is why a finger dragged off its button still ends
on the button while the *mouse* events a tap leaves go where the finger really came off.

**A tap is a click, made of the same parts.** §8's compatibility events — `mousemove`, `mousedown`, `mouseup`,
`click` — are dispatched through the helpers `DispatchMouse` uses, at the released point, so one activation
behaviour runs rather than two nearly identical ones — including the image button's selected coordinate,
which is measured from the released point's hit test the way the mouse's is (above). They are owed only by a *single-finger* gesture nothing
cancelled: a second contact, a cancelled `touchstart` or first `touchmove`, or a `touchcancel` withdraws them.
**No pointer event is fired for a touch** — `pointerType: "touch"` with a `pointerId` per contact and its own
boundary events is a second pointer model over the same contacts, and this package has one.

**Emulation is not consulted.** A client that sends a touch is asking for one; what
`Emulation.setTouchEmulationEnabled` decides is whether the page can *detect* that it might get one.

### The keyboard, and the editor under it

The other half of the events bridge, and what a protocol client reaches it through: every one of these
rules is what `Input.dispatchKeyEvent` and `Input.insertText` end up doing.

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
any of it — it answers `false` for `<div contenteditable>` — and the divergence table in
[`../Accessibility/AGENTS.md`](../Accessibility/AGENTS.md) records why.
