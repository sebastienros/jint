# C3b: bounded selector interaction state

Finite implementation dispatch, 2026-09-25. Extends the existing
[selector contract](html-parser-selectors.md) and preserves the
[C3a1 intrinsic predicates](html-parser-selector-form-states.md).
This implements an internal Browser integration seam and real matching; it does not
publish the incomplete selector API or implement missing control state.

Authority: [Selectors user-action predicates](https://drafts.csswg.org/selectors-4/#useraction-pseudos),
[HTML selector rules](https://html.spec.whatwg.org/multipage/semantics-other.html#pseudo-classes),
[flat-tree construction](https://drafts.csswg.org/css-shadow-1/#flat-tree),
[DOM find a slot](https://dom.spec.whatwg.org/#find-a-slot), and
[HTML labeled control](https://html.spec.whatwg.org/multipage/forms.html#labeled-control).

## Scope and exclusive files

Own changes under `Jint.HtmlParser/Css/Selectors/`, new focused tests under
`Jint.Tests.HtmlParser/Css/Selectors/`, and the narrow shared-work overload in
`Jint.HtmlParser/Dom/Shadow/SlotAssignment.cs` specified below. No Node, Document,
Element, Attr, CharacterNodes, HtmlElementState, NodeCloner, tree-construction,
Browser, generator, public API, or status-file edits. Those shared DOM files have
other owners. Do not copy slot assignment merely to avoid that ownership boundary.

New source files: `SelectorEnvironment.cs`, `SelectorMatchWork.cs`,
`SelectorMatcher.Environment.cs`, and `SelectorStateTraversal.cs`. Existing matcher
partials may change to thread work by reference. Keep compiler grammar unchanged.

Enable exactly Focus, FocusWithin, Active, Target, Hover, FocusVisible, and Autofill.
The last three have the explicit current headless policies below. Keep every other
unfinished kind rejected by the existing whole-program validation, including Lang,
Dir, Checked, Unchecked, Indeterminate, Default, Valid, Invalid, InRange, OutOfRange,
ReadOnly, ReadWrite, PlaceholderShown, Open, Closed, AnyLink, Link, Visited, Host,
and HostContext. Do not enable one partition of an intrinsic predicate and answer
false for its other partitions. Unknown syntax/forgiving recovery remains unchanged.

## Exact internal entry seam

All new selector types use `Jint.HtmlParser.Css.Selectors`.

```csharp
internal readonly record struct SelectorEnvironment(
    Document? Document,
    Element? FocusedElement,
    Element? PointerPressTarget,
    Element? TargetElement);

internal struct SelectorMatchWork
{
    internal SelectorMatchWork(Node observationRoot,
        CancellationToken cancellationToken, Action? checkpoint = null);
    internal void Step();
    internal void Check();
}

// Additional SelectorMatcher overloads; retain the existing overloads.
internal static bool Matches(CompiledSelector program, Element element,
    Node? scopingRoot, in SelectorEnvironment environment, ref SelectorMatchWork work);
internal static bool TryMatch(CompiledSelector program, Element element,
    out SelectorSpecificity specificity, Node? scopingRoot,
    in SelectorEnvironment environment, ref SelectorMatchWork work);
internal static Element? Closest(CompiledSelector program, Element element,
    in SelectorEnvironment environment, ref SelectorMatchWork work);
internal static Element? QuerySelector(CompiledSelector program, Node root,
    in SelectorEnvironment environment, ref SelectorMatchWork work);
internal static IReadOnlyList<Element> QuerySelectorAll(CompiledSelector program, Node root,
    in SelectorEnvironment environment, ref SelectorMatchWork work);
```

The work value is invocation-owned, initialized once and passed by reference. Default
uninitialized work is invalid and throws InvalidOperationException. Do not copy an
active work value, pool it, retain it on a program/document, or share it concurrently.
One work value covers host preparation and one matcher invocation. Private matcher
Work may become a ref struct referring to that value; internal recursive-looking VM
calls still use explicit frames and the same work reference. Existing convenience
overloads construct fresh work and pass a default environment.

An environment is a capture of native identities, not a provider. Browser captures
it after required argument coercion, on its owning page loop. It retains no JsValue,
Engine, wrapper, predicate delegate, URL parser, event dispatcher, or mutable caller
collection. A null Document means no environment: non-null seeds in that case are
an ArgumentException at entry. Seeds belonging to another owner document are also
an ArgumentException. Browser filters its per-engine focus record by the queried
document before constructing the value, including frame/secondary-document calls.
Do not read or walk unused seeds for an ordinary `#id` program; these O(1) identity
validations are sufficient at entry. Reuse a compiled program with a new environment
to observe later focus/press/target changes.

Browser resolves its current URL target, including raw-before-decoded fragment,
ID precedence, first duplicate, and legacy-anchor rules, before passing TargetElement.
That preparation calls the same work's Step/Check, never a fresh cancellation budget.
This native dispatch neither parses URLs nor infers target from an id attribute.
The later Browser integration owns that bounded resolver and tests; it is not a
production callback supplied to this matcher. Null target is a real empty target.

## Work, allocation, and read guards

Preserve the current normal structural/lone-compound allocation behavior. Do not add
a work class, environment dictionary, ancestor list, closure, or seed traversal to
every standalone Matches call. The new work is a struct. A guard/reference cell is
allowed only when a checkpoint is supplied or an actually evaluated environment
predicate needs shared cached state. If that cell is created lazily, transfer the
existing HtmlDisabledWork value with its accumulated count and subsequently route
every selector/native step to that one cell. Never leave two counters advancing.

Continue passing that exact HtmlDisabledWork by ref to disabledness/requiredness.
Its sub-256 remainder must survive entry into and return from slot/label work.
Charge each visited node/link/attribute, text comparison/hash code unit, stack entry,
membership operation, and published result entry. Entry, exit, unsuccessful scans,
and final ancestor backtracking all check cancellation. No partial list is returned.
Ancestor caches live only in the invocation, are built at first relevant predicate,
and are reused across candidates and nested VM evaluation. Compiled programs remain
document-free. A plain `#id` must not become O(focus-depth) with an environment present.

The checkpoint is the existing deterministic-test seam, also usable by one trusted
Browser budget adapter. It is not a predicate provider. Production adaptation may
check constraints but may not run JS, dispatch events, flush reactions, pump jobs,
or perform property conversion. Exceptions from it escape unchanged.

Observe the node document and its MutationStamp before any read; observe an explicit
scoping root's document before reading it, and the environment document before its
state is used. Keep the first observation inline; additional document guards may be
allocated lazily. Retain observed node/owner identities so adoption is also detected.
Before and after every checkpoint, and before result publication, verify these
identities and stamps. A changed or saturated stamp throws InvalidOperationException
with invariant message `The native selector view was invalidated by mutation.`
Check cancellation first, so an already requested cancellation remains
OperationCanceledException. A callback's own exception is never replaced in finally.
Re-entering the same work while a checkpoint or matcher invocation is active throws
InvalidOperationException with message `The native selector invocation is already active.`
Clear active/checkpoint flags in finally. Ordinary concurrent mutation remains
unsupported; this guard prevents a testing/host checkpoint from publishing stale data.
Use the existing XPathReadSession as the behavioral precedent, not as a new dependency.

## One slot algorithm and bounded flat-tree ancestry

SlotAssignment.GetAssignedSlot/Node.StoredAssignedSlot is a stored association and
can intentionally remain stale after reassignment/removal. It is not fresh lookup.
Reuse FindSlotCore/FindSlotInRoot, which implements current manual intent and the
maintained named first-slot index. Add only this internal seam in namespace
`Jint.HtmlParser`, in SlotAssignment.cs:

```csharp
internal interface ISlotQueryWork
{
    void Step();
    void Check();
}

// SlotAssignment
internal static Element? FindSlot(Node slottable, bool openOnly, ISlotQueryWork work);
```

The selector's lazily created private sealed work cell implements that interface;
it is not implemented by Browser and does not evaluate user code. Slot QueryWork
gets an optional shared-work lane: every Step forwards one unit immediately, and
entry/Finish forward Check. Existing token/checkpoint overloads keep their behavior.
Do not forward only old 256-step callbacks: 200 selector steps plus 100 slot steps
must poll at aggregate step 256, not at Finish. No DOM-to-CSS reference is introduced.
The same FindSlotInRoot algorithm and cold index build are used in both lanes; this
avoids independent scans/algorithms on each flat ancestor. Account for long slot-name
spelling in the shared lane before index operations; do not add unpolled text scans.
No callback adapter or shared cell is constructed when no slot lookup is needed.

Add read-only helpers under SelectorStateTraversal, taking ref SelectorMatchWork.
Flat-parent lookup uses these exact native relations:

- A child of a shadow host uses fresh FindSlot(openOnly: false); an unassigned
  light child has no parent in the flat tree. Never substitute host for a missing slot.
- A child of a ShadowRoot has that root's Host as flat parent. Open/closed mode
  does not alter internal ancestry; do not use OpenShadowRoot.
- Fallback children of an HTML slot in a shadow tree participate only while that
  slot's actual `SlotState?.Assigned.Count` is zero. This is the maintained assigned
  list, not the stale pointer on an old slottable. Suppressed fallback cannot connect
  an environment seed to the host through ordinary ParentNode links.
- Otherwise use the ordinary parent element. A Document terminates the chain;
  an ordinary/template fragment never crosses its Host. Slots themselves remain
  elements in the flat tree.

Distinguish a suppressed edge from successful arrival at the environment document.
Build an ancestor result privately, and publish it only after confirming the path
participates in that document's flat tree. A seed's own supplied state does not
invent ancestors when the path is suppressed or detached. Ordinary selector
combinators, query enumeration, :scope, and featureless fragment semantics do not
change to flat-tree traversal.

## Predicate semantics and label resolution

Focus: match the supplied focused element, excluding an HTML iframe/frame seed
(navigable containers are not :focus). Also match shadow hosts required by HTML's
focus rule: follow the focused element's TreeShadowRoot to its Host, then that
host's TreeShadowRoot, iteratively. Ordinary containing elements and slots do not
become :focus. This rule does not depend on delegatesFocus or open mode. Require
shadow-including connection to the environment document before using a focus seed;
Browser remains responsible for choosing the actual current focus-chain leaf.

FocusWithin: include focus matches and the focused leaf's flat-tree ancestors.
Active: use the current PointerPressTarget and its flat-tree ancestors, then add
the labeled controls of active labels. A labeled-control addition does not propagate
activity to that control's ancestors; HTML explicitly distinguishes it from being
activated. Browser owns press/release timing and formal keyboard activation; this
slice preserves its current pointer-press state, without inventing keyboard state.
Require the press seed to be shadow-including connected to the environment document.

Current Browser has no top-layer interaction store. This dispatch models its empty
top layer and propagates to the root; it does not claim modal/popover top-layer
support. When that store is implemented, pass its boundaries as data in a separately
reviewed context extension. Do not infer top-layer membership from an open attribute.

Resolve labels locally, without Browser callbacks or a new native mutation service:
exact HTML namespace/lowercase `label`; a present no-namespace `for` uses the first
element with that exact ID in the label's ordinary tree, and that first element must
be labelable. A nonlabelable first duplicate blocks later duplicates. Empty for
selects nothing. Without for, use the first labelable ordinary descendant, excluding
the label itself. Never cross template or shadow roots during those searches.
Labelable built-ins are button, meter, output, progress, select, textarea, and input
whose existing HtmlInputTypes classifier is not Hidden. Read type in a charged
attribute scan and call Parse/Info; do not call tokenless Get. Custom names do not
gain labelability without the separately owned form-associated definition state.

Collect labels on the one active ancestor chain, then resolve them in batches by
ordinary root. At most one ordinary-tree pass per root in this invocation supplies
the requested first-ID answers and first-labelable-descendant answers; do not rescan
the whole document per active label or per candidate. Store requested ID keys and
answers only, use charged hashing/equality, and resolve nested no-for labels with an
iterative pending-ancestor stack. The first labelable descendant discharges each
pending label once. This keeps auxiliary state proportional to active labels, not
to all document IDs. No long-lived label index or node cache is added.

Target: identity-match TargetElement only when its ordinary root is the environment
Document. Detached, shadow, template, and wrong-document targets cannot match.
Validate that path lazily once; query scope does not select a different URL target.

Hover, FocusVisible, Autofill: explicitly return false under this current headless
profile, with or without an environment. This preserves the existing Browser policy
(no recorded hover state, focus-ring decision, or UA autofill state). Autofill also
covers the compiler's existing :-webkit-autofill alias. These cases are named in
IsImplemented/MatchPredicate and tests; no general unknown/unimplemented fallback.

## Acceptance and handoff

- All five entry points, matching-branch specificity, is/where/not/has, filtered
  forward/reverse nth, and columns with nested environment predicates. Preserve
  strict/forgiving grammar and featureless fragment behavior.
- Reuse one compiled program across focus changes, press/release, target changes,
  different documents, and no-host calls. Every query has fresh state; earlier static
  result snapshots preserve identity/membership. Node/program lifetimes stay separate.
- Shadow-host focus, nested open/closed roots, named/manual assignment, slot renaming,
  reassignment/removal with stale StoredAssignedSlot, suppressed fallback/unassigned
  light children, detached/template boundaries, and slot state after adoption.
- Active labels: explicit/implicit, empty/namespaced for, hidden/type changes,
  nonlabelable first duplicate, root element ID, nested labels, external associated
  control, no activity on that control's separate ancestor chain, and shadow roots.
- Work tests: aggregate 200+100 selector/native steps, many short slot calls, cold
  slot index, deep final ascent, wide label scans, huge attribute/ID/slot spelling,
  entry/exit cancellation, retry with fresh work, checkpoint mutation/adoption,
  saturated stamp, checkpoint exception preservation, and same-work reentrancy.
- Deterministic visit-count growth proves no whole-document scan per candidate or
  per label. A plain #id with deep irrelevant environment seeds performs the same
  bounded selector work and adds no state-cell/ancestor allocations. No timings.
- `#hit, :checked`, `:is(#hit, :valid)`, `:not(:lang(en))`, and `:has(:dir(rtl))`
  still reject the whole accepted program before results, even with an environment
  and even when the unsupported branch would not be selected. Empty queries do too.
- Fresh Release focused/native non-corpus runs on net8.0 and net10.0. Authored fixtures
  are labeled authored. No unsupported-to-false changes or exclusion broadening.

Browser integration follows in its existing owner: native identity capture, bounded
target resolution and budget adapter, DomHostHooks/DomSelectors entry points, and
cascade TryMatch. Internal access/signing belongs to that owner; do not publish the
incomplete selector API to make it callable. C3b completion removes the interaction
gap while the whole-program gate continues exposing actual D7/language/control gaps.
