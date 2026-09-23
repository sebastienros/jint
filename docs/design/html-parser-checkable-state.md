# D7b2 native checkbox and radio state

Preparatory design for independent review, 2026-09-23. This document dispatches no implementation.
It supplements [D7 form state](html-parser-form-state.md), the reviewed
[D7b1 input-state contract](html-parser-text-control-state.md), and
[C3 selector decisions](html-parser-selectors.md). Shared native edits require a coordinator handoff
**after D6s2**, with X4b/D7a/textarea ownership reconciled first. No Browser, parser, runtime or public
API changes accompany this document. Benchmark timing and machine sampling remain on hold.

## Sources, decisions and current consumers

Primary sources checked on 2026-09-23 are HTML Living Standard, updated 2026-09-22:
[input state, reset, cloning and activation](https://html.spec.whatwg.org/multipage/input.html#the-input-element),
[radio groups](https://html.spec.whatwg.org/multipage/input.html#radio-button-state-(type=radio)),
[checked IDL](https://html.spec.whatwg.org/multipage/input.html#dom-input-checked),
[HTML selectors](https://html.spec.whatwg.org/multipage/semantics-other.html#pseudo-classes),
[connection terminology](https://html.spec.whatwg.org/multipage/infrastructure.html#becomes-connected),
[form reset](https://html.spec.whatwg.org/multipage/form-control-infrastructure.html#resetting-a-form),
and [DOM dispatch](https://dom.spec.whatwg.org/#concept-event-dispatch).
[Selectors' selected-option states](https://drafts.csswg.org/selectors-4/#checked) supplies unchecked;
its mutual-exclusion prose conflicts with HTML's explicit checked predicate. Preserve the reviewed C3
decision: an HTML checkbox can match both checked and indeterminate. This is a documented precedence
decision, not an assertion that the two texts agree.

The [WHATWG connection correction](https://github.com/whatwg/html/issues/10897) is material:
inserting a checked radio into a **disconnected** tree without changing its form owner does not itself
uncheck its peers. A group can consequently contain multiple checked members. An index must record
that state faithfully; rebuilding an index or reading a selector must never repair it.

Upstream [radio tests](https://github.com/web-platform-tests/wpt/blob/master/html/semantics/forms/the-input-element/radio.html)
and [checkbox tests](https://github.com/web-platform-tests/wpt/blob/master/html/semantics/forms/the-input-element/checkbox.html)
were inspected for grouping and activation coverage. Those moving links are research references, not
a corpus pin or a pass claim. The implementation packet must resolve relevant cases against the
repository's WPT revision, record any newer-source differences, and add native regressions; absence
from the vendored selection is not a reason to omit an algorithm.

Consumer inspection used integration commit `093bbeb989924357ed041c7b7a35c2af104be220`:

| Current consumer | Native requirement and migration consequence |
| --- | --- |
| `Jint.Browser/Dom/Generated/DomShapes.Html.g.cs` | checked/defaultChecked/indeterminate become adapters to one native store; change generator inputs/overrides, never generated C# by hand. These properties have meaningful storage on every actual HTML input, even when its type is text. |
| `Events/ActivationBehaviors.cs`, `DomNodeObject`, `Jint/WebApi/Events/EventDispatch.cs` | Current weak wrapper-keyed snapshots, manual peer setters and unconditional old-radio restoration must be replaced by dispatch-local state and native operations. Browser retains all event dispatch and exception policy. |
| `Runtime/Parsing/PagePseudoClassSelectorFactory.cs` | Current radio indeterminate scans use case-insensitive names; replace with exact native membership. Its detached-root inclusion fix must survive. checked remains independent of checkbox indeterminate. unchecked is a distinct predicate. |
| `Events/FormSubmission.cs`, `Runtime/FormSubmitter.cs` | Native state controls successful checkbox/radio entries and native reset. Today form reset delegates to AngleSharp while ownership elsewhere uses a Browser helper; migration must reset the same owned controls it enumerates. |
| `Dom/HtmlFormOwner.cs`, native `HtmlFormAssociation` | Group keys use stored native owner identity, including parser association. Do not derive an alternative owner in radio code. Every real owner transition must notify the group coordinator. |
| Accessibility and validity consumers | Read checked and indeterminate independently; ARIA's presentation stays in accessibility. D7f consumes native group facts for required/valueMissing; it does not run eventful validation from selectors. |

Existing test starting points are Browser `Events/ActivationBehaviorTests.cs`,
`Parsing/PagePseudoClassSelectorTests.cs`, `Forms/FormOwnerTests.cs`, `FormTests.cs` and
`FormEntryTests.cs`. The current C3 case expecting name `ONE` to share with `one` is a named standards
correction: update the expected grouping at cutover and add exact-case/non-ASCII fixtures.

## Storage, applicability and concrete operations

Use the complete D7b1a `HtmlInputTypes` classifier. No local string switch or reduced type enum; missing
and invalid type still mean Text, and implemented numeric/file states must never fall back to Text.
Actual element applicability means HTML namespace plus exact lowercase local name `input`, in HTML
or XML documents alike. Uppercase XHTML names, SVG input and namespace-less XML input are unrelated.

The stable `HtmlElementState` owns **one** nullable `HtmlInputCheckedState` component. It stores three
booleans: Checked, DirtyCheckedness and Indeterminate, initially false. It is available for every
actual input, not only current checkbox/radio types. D7b1d's eventual complete `HtmlInputState` refers
to this same component; it must not copy these flags into a second store. No general Input facade or
partially implemented value/reset APIs are published by b2. Component allocation may be lazy, but all
native mutation paths must preserve the flags before the first read. DefaultChecked is attribute
presence, not a fourth stored boolean. An `indeterminate` content attribute has no effect on the flag.

Proposed internal surface under `Jint.HtmlParser/Dom/Html/`:

```csharp
internal sealed class HtmlInputCheckedState
{
    internal Element Element { get; }
    internal bool Checked { get; }
    internal bool DirtyCheckedness { get; }
    internal bool Indeterminate { get; }
    internal bool DefaultChecked { get; }
    internal void SetChecked(bool value, CancellationToken cancellationToken); // IDL
    internal void SetDefaultChecked(bool value); // normal boolean-attribute reflection
    internal void SetIndeterminate(bool value);
}

internal readonly record struct HtmlRadioGroupFacts(
    bool Applies, int MemberCount, int CheckedCount, int RequiredCount);

internal static class HtmlCheckableState
{
    internal static HtmlInputCheckedState? Get(Element element);
    internal static HtmlRadioGroupFacts GetRadioGroupFacts(Element element,
        CancellationToken cancellationToken);
    internal static bool SameRadioGroup(Element first, Element second,
        CancellationToken cancellationToken);
    internal static Element? FirstCheckedRadio(Element element,
        CancellationToken cancellationToken);
    internal static IReadOnlyList<Element> SnapshotRadioGroup(Element element,
        CancellationToken cancellationToken);
    internal static bool MatchesChecked(Element element);
    internal static bool MatchesUnchecked(Element element, CancellationToken cancellationToken);
    internal static bool MatchesIndeterminate(Element element, CancellationToken cancellationToken);
    internal static bool MatchesDefaultCheckable(Element element);
}

// Only native algorithm/Browser integration callers use these; no JS-facing origin option.
internal enum HtmlCheckedChangeOrigin { Algorithm, UserInteraction }
internal static class HtmlCheckednessAlgorithms
{
    internal static void Set(Element input, bool value, HtmlCheckedChangeOrigin origin,
        CancellationToken cancellationToken);
    internal static void ResetCheckedness(Element input, CancellationToken cancellationToken);
    internal static void CopyCheckedness(Element source, Element copy);
}
```

Null arguments throw ArgumentNullException. Get returns null for a wrong element kind. Predicate
helpers return false outside their stated checkbox/radio portion; C3 composes option/progress/default
button predicates from their own owners. They do not claim to implement all checked/default categories.
Group functions are total over elements: non-radio facts are `(false,0,0,0)`, snapshot empty,
FirstCheckedRadio null, and SameRadioGroup false even for the same non-radio element. This prevents an
empty non-applicable result being mistaken for an indeterminate radio group. Every actual radio has
at least itself as a member; missing/empty name creates an isolated singleton, not a shared empty-name
bucket. SameRadioGroup(r,r) is true while r is a radio. An applicable group with zero checked members
stays empty of a selection; no getter, parser or reset chooses an arbitrary first radio.

SnapshotRadioGroup is an immutable snapshot in ordinary tree order, including a root element that is
itself a radio. FirstCheckedRadio returns the first checked member in that order when unusual detached
construction has left several. That tie-break is this library's deterministic extension for HTML's
singular pre-activation reference in that state; ordinary single-selection cases are unchanged.
Facts are the cheap path for predicates/validity; callers must not allocate a snapshot for each match.

### Changes and dirty flags

| Operation | Checkedness / dirtiness / indeterminateness |
| --- | --- |
| checked IDL assignment, including equal assignment | Set current flag; true runs radio exclusion even if already true. Set only receiver's dirty checkedness true. No content attribute change. |
| Algorithm assignment | Change current flag and apply radio exclusion for true; preserve dirty flags. Reset, peer exclusion and canceled activation use this lane, never the IDL setter. |
| UserInteraction assignment | Apply the native checkedness transition, dirty the directly operated control when its checkedness changes. Automatic peer unchecking uses Algorithm and preserves peer dirtiness. Browser selects this explicit provenance; native code has no event trust flag. |
| Add/remove checked attribute while clean | Set current true/false respectively; true enforces exclusion. Preserve dirty false. |
| Add/remove checked attribute while dirty | Change only the default. A radio losing an attribute is not automatically unchecked while dirty. |
| Change value of an already present checked attribute | Default remains true; no add/remove checkedness transition. Do not re-check a clean radio that a peer previously unchecked. |
| indeterminate IDL | Store its boolean on any input; does not modify checked, dirty checkedness, value, default, or peers. |
| Checkedness portion of reset | Clear dirty checkedness, then assign current from checked attribute using Algorithm. Preserve indeterminate. |
| Clone/import | Copy all three flags exactly, including clean checkedness differing from the attribute after peer exclusion. Do not replay the checked IDL setter. |

DirtyCheckedness is independent of D7b1 DirtyValue and user validity. b2 does not manufacture a user
validity change, clear file state or modify value provenance. The complete reset in b1d clears user
validity and dirty value, resets value and checkedness, clears files and sanitizes in HTML order; this
component operation is an internal step, not a standalone promise that the whole input was reset.
Whole-form reset visits the resettable controls owned by that form in tree order. Each checked-default
radio runs exclusion at its turn: the last applicable default-checked reset wins. Both may still match
default afterward. A canceled Browser reset does not invoke any native reset step.

Adoption preserves these three flags and element identity. The normal removal/insertion and owner
changes can still affect grouping. Type changes preserve the flags; entering Radio with Checked true
runs exclusion at the full input algorithm's type-change signal, after value-mode changes and before
the new sanitizer. Leaving Radio unregisters membership; it does not clear Checked or Indeterminate.
Equivalent type spellings that parse to the same state do not signal a type change.

Actual changes to stored flags, or peer flags, advance the corresponding document mutation stamp
before the next query. IDL assignment can change dirtiness even when checkedness is equal. State writes
produce no fabricated attribute MutationRecord. Attribute setters retain normal records; readonly and
disabled do not prohibit programmatic state/default writes. Pure reads/index construction are silent.

## Group keys, triggers and mutation ordering

Group identity is `(ordinary tree root identity, stored form owner identity, exact nonempty name)`;
membership requires current Radio type. Compare names ordinally, including whitespace and non-ASCII
code units; no trimming, case folding, Unicode normalization or quirks-mode branch. Disabled, readonly,
required and value do not exclude members. A disabled required radio contributes RequiredCount even
if it is itself barred from constraint validation; D7f decides candidacy separately.

An ordinary tree is not a shadow-including, slot-assigned or host-inclusive tree. A ShadowRoot and a
template's content fragment each have their own groups. An attached host becoming connected can make
radios inside its shadow roots become connected, but does not merge those groups with light DOM.
OwnerDocument equality is insufficient for membership; detached roots in one document remain separate.
Connectedness uses D6's shadow-including definition. This deliberate distinction needs paired tests.

The following triggers run exclusion **only if the receiver is currently a checked radio**:

1. Assign Checked true for any reason, even if its old value was true.
2. Set/change/remove its name attribute. A same-string name setter is still a set operation; do not
   inherit HtmlFormAssociation.AttributeChanged's `oldValue == newValue` early return.
3. Change its actual stored form owner identity, including parser-established ownership.
4. Signal a type-state change whose new state is Radio.
5. Invoke insertion steps when it is now connected, in the standard's insertion traversal order.

Rekeying and exclusion are distinct operations. Structural changes always update membership, but a
disconnected insertion with unchanged owner must **not** synthesize trigger 5. Removing a node does not
reselect a peer. Reads do not run any trigger. Reordering connected radios may run insertion/connection
steps and change the winner; a future state-preserving move must follow that operation's actual moving
steps rather than accidentally call the ordinary insertion algorithm.

This yields last-triggered-wins, not unconditional last-tree-node-wins. Setting the first radio checked
after parsing makes the first win. Sequential connected insertion of initially checked radios normally
makes the later insertion win. Reparenting a disconnected checked radio without changing its owner
can leave two checked; assigning either one's Checked=true or its identical name again resolves them.
Do not snapshot all initially checked members of a connecting subtree and replay true on each: an
earlier trigger may already have unchecked a later node before its insertion step is reached.

### One integration path

Add one coordinator, `HtmlInputStateChanges`, after the shared-file handoff. It routes to implemented
family components. It must not contain default value fallbacks or unimplemented branches returning
success. b1d later extends this same coordinator for full type/value behavior, retaining the b2 signal.

| Existing seam | Required b2 integration |
| --- | --- |
| Element attribute add/remove/replace, attached Attr.Value, parsed attribute merge | Deliver namespace, exact local name, old/new presence and actual operation. Handle checked add/remove, every name set/removal, required count changes, and type-state changes. Ignore namespaced lookalikes. Replacing one present checked Attr with another remains present, not a remove-plus-add transition. |
| HtmlFormAssociation.ResetOwner / AssociateFromParser | Centralize each specified owner store and notify on actual old/new identity change after storage is current. HTML reset-owner explicitly stores null before reassociation unless its early return applies; preserve those algorithm steps, rather than comparing only entry/final owners. Do not add implementation-only null stores during lookup. Include resets caused by form/id changes elsewhere in the tree. |
| Node before-removal / post-unlink / insertion and suppressed internal paths | Remove stale memberships at unlink; restore accurate root and owner membership before a radio trigger. Traverse affected subtree including its element root; handle attached shadow subtrees through their separate roots. Reuse existing form/HTML lifecycle traversal instead of adding a second whole-document pass. |
| Native parser element creation | Initialize after the complete attribute batch, with normal initial false flags; checked presence then supplies initial checkedness. Apply parser owner association and insertion in their actual order. Attribute token order cannot create different initial state. |
| AppendParsedChild / InsertParsedBefore | Run the same native group steps; no parser-only winner cache and no EOF repair. XML-created HTML-namespace inputs receive the same intrinsic state; DTD-defaulted checked/name/type attributes are effective attributes. |
| NodeCloner.CopySingle / AppendClonedChild | Copy attributes, then flags, then normal clone insertion semantics. Do not dirty clones or infer checkedness solely from defaults. Preserve template/shadow root boundaries and new form identities; no source owner/bucket is copied. |
| Adopt/import/document clone | Source membership is removed through existing removal; target uses new actual roots/owners. Import copies state, adopt preserves it. Document clone may invoke connected insertion steps; resulting exclusion follows those steps, not a bespoke clone normalization pass. |
| Fragment append, replace-all, inner/outer markup, form removal, hierarchy failure | Existing common mutation paths must reach the coordinator exactly once per required step. Failed pre-insertion validation performs no checked/group/default/dirty write. Fragment children are inserted in their actual algorithm order. |

Before implementation, enumerate these call sites on the then-current shared base, including D6s2's
clone/parser/slot changes. Node/Element/Attr/Document/NodeCloner are a single owner reservation, not five
parallel tasks. No hooks may be added only to public SetAttribute/AppendChild or only to Browser.
Do not call Browser while a native mutation is half committed. Attribute records, form ownership,
group membership, checked flags and selector revision must agree when the operation returns.

Owner-reset effects need an explicit audit of D7a1's index optimizations: resetting to the same final
owner can still pass through the specified null-owner step. A same-value form setter still invokes
reset-owner. **When any element's ID in a tree changes, reset every listed form-associated control in
that tree that has a form attribute**, including an empty form attribute. The candidate set is root-wide;
it is not limited to controls whose form value matches the changed ID's old or new value. D7a1's
matching-ID `ResetReferences` pruning is insufficient once checkedness effects are implemented.
The corresponding ID-bearing insertion/removal/moving rules likewise require their complete specified
reset candidate set, even when the mutated subtree contains no control and no form owner finally changes.

Required exact regression fixture: build a connected tree containing `form#f`, radio `a` with
`name=g, form=f`, unowned radio `b` with `name=g`, and an unrelated `div#x`. After construction, set
both radios checked, so a belongs to f's group and b to the null-owner group. Change only the div's ID
from `x` to `y`. Resetting a's owner through `f → null → f` must uncheck b during the null-owner step;
a remains checked and its final owner remains f. Neither x nor y equals a's form value. Assert both
intermediate owner notifications and the final checked flags, and verify b's dirty flag is preserved.
Repeat with more listed controls carrying form attributes to establish complete candidate coverage.

Maintain an enumerable root-wide candidate index, reusing native form-index ownership where possible,
so collecting these candidates does not require scanning every unrelated node for each ID write.
Do not restrict the candidate index to radios: reset remains a form-association obligation for all
applicable listed controls. Pruning an ID-change or insertion/removal reset is allowed only after proof
that it preserves every specified intermediate owner transition and checkedness effect, not merely
the final FormOwner pointer. If current upstream tests or engines require a different interpretation,
report that source conflict for independent review before changing these steps. Hash-set enumeration
must not choose observable reset/exclusion order where the standard requires tree order; add
deterministic ordering at the owning batch operation without making every single fact read sort a group.

## Index, lifetime and cancellation

Use a root-owned, lazily built `HtmlRadioGroupIndex`. It maintains buckets keyed by owner reference
and ordinal nonempty name, and tracks actual radio members, checked members and required count.
Missing/empty names use the receiver's direct singleton facts without allocating one bucket per
unnamed control. A bucket's checked members form a set/list with removal handles, **not one winner
pointer**. Selecting a member visits only currently checked peers; it does not repeatedly set every
unchecked member false or dirty them. Normal one-checked groups therefore have constant-size work.

Store membership handles in the element-owned component, with direct unlink/update. Root topology
changes and owner/name/type transitions keep them current. An already registered hot query should
not climb every ancestor again. During a cold build, an iterative ordinary preorder including the
root collects members without changing their flags. Stage the entire result, then publish only after
successful cancellation checks. Never publish half a bucket or normalize duplicate checkedness during
construction. Facts after a published build are O(1); names must not be reparsed for every fact read.

Root creation/bootstrap is at most one O(N + total name units) scan of that root before incremental
maintenance, not one per radio. Attributes initialized on a fresh detached leaf take a direct singleton
path; they must not allocate a throwaway tree index for every parser token. After the first tree index
exists, appending another parsed input updates it without rebuilding the prefix. A tree with no radios
does not need a radio index. MutationStamp cannot be its universal invalidation key: changing a div's
class/text or a checkbox's indeterminate must not discard all group membership.

Ordinary radio membership maintenance costs the affected subtree traversal and actual group changes.
**Root-wide form-owner resets are an additional required cost** when an ID change or the corresponding
ID-bearing structural operation triggers them: visit all F indexed reset candidates, plus the work of
their owner resolution and resulting group transitions. Do not promise affected-subtree-only complexity
for those operations or omit candidates to achieve it. Prefer the already required form traversal and
root-wide candidate index, with shared per-invocation work accounting; avoid a separate full-document
scan when the candidate index can provide the complete set.
Moving an indexed detached root under another tree retires the old root index and clears or transfers
its member handles; it must not retain a former root as a hidden owner. Removing a subtree unregisters
all its radios from the old live tree immediately, even if the detached tree never gets queried. Empty
buckets and their owner/name references are removed. No process-wide strong dictionary, Page/Engine,
event wrapper or stale snapshot belongs in this index.

Tree order is needed for snapshots and the rare multiple-checked pre-activation tie-break, not for
GetRadioGroupFacts or ordinary selection. A bounded iterative tree walk may construct an explicitly
requested snapshot; document that O(N) cost. For FirstCheckedRadio, return the only checked member in
O(1); with several, find the first among the recorded checked identities in current tree order with
bounded traversal. Do not sort hash iteration order and call it tree order. C3 matches must use facts,
so matching K radios in one stable tree remains O(N + K), not K independent document scans.

All externally cancelable work shares one local work counter through root ascent, candidate visits,
membership building, name processing and snapshot traversal; check on entry, at most every 256 work
units, and before publishing. Test hooks count work but are absent from production hot loops.
Pure fact/snapshot cancellation leaves stored flags and published indexes unchanged; staged objects
must be reclaimable. Cached reads still honor a pre-canceled token.

For multi-member state mutation, prepare the affected checked identities and allocations with bounded
cancellation before the semantic commit. Commit checked flags, dirty updates, index links and revision
as one coherent native change; do not throw midway through unchecking peers. If existing parser/DOM
operations observe cancellation only after their semantic commit, retain that contract: report canceled
only after all native group steps for the committed operation are consistent. Do not install a new
mid-commit cancellation point in the shared hooks. Large group/membership cleanup may defer the throw
to that boundary; the implementation must document and work-test the bounded preparation and complete
commit, rather than pretend the operation rolled back. This is not a transaction across Browser events.

## Selector and form facts

These are the b2 portion of C3, using the same backing flags as APIs:

| Receiver | checked | unchecked | indeterminate | default checkable |
| --- | --- | --- | --- | --- |
| Checkbox | Checked | !Checked && !Indeterminate | Indeterminate | checked attribute present |
| Radio | Checked | !Checked && group.CheckedCount > 0 | group.CheckedCount == 0 | checked attribute present |
| Other input type or unrelated element | false | false | false | false |

For an unnamed checked radio, its singleton count is one and indeterminate is false. For an unnamed
unchecked radio, checked/unchecked are both false and indeterminate true. A named single-member radio
uses the same facts; HTML's authoring restriction against singleton groups does not make its DOM API
throw. A checkbox with both flags true matches checked and indeterminate under the reviewed HTML rule.
Changing checkbox type to Text suppresses those predicates without erasing its flags; restoring its
type reveals the stored flags. Multiple checked radios in a disconnected group can all match checked.

C3 must compose selected options from D7c and progress from its native owner; progress checks absence
of value, not an empty value string. Option checked follows HTML selectedness, while unchecked follows
the reviewed in-select applicability. Default submit buttons use ordered native form membership and
include input image as well as submit and submit buttons; disabledness does not alone remove the
default-button identity. D7f/B2 owns implicit submission and its disabled activation guard. b2 must not
smuggle a generic !checked fallback onto options, menuitem, ordinary divs or progress.

Form entry construction tests Checked, never DefaultChecked or Indeterminate. Checkbox/radio value is
D7b1's DefaultOn mode: absent value returns `on`; present empty remains empty. b2 does not implement a
parallel value getter. RequiredCount is available for D7f's group-level missing-value rule, but checked
state alone does not establish complete ValidityState or validation candidacy.

## Browser activation and canceled activation handoff

Native state operations are event-free. They know neither click, JsEvent, realm, event-loop task nor
user activation. Browser chooses an activation target through DOM dispatch, stores a **dispatch-local**
record, calls native transitions, then performs the appropriate default/canceled action. It must never
loop over peers invoking the checked IDL setter. Automatic peer exclusion and rollback preserve dirty
flags; a checked setter in a listener still dirties its receiver and rollback does not undo that write.

The later Browser record contains target identity, pre-activation Checked and Indeterminate (capture
both for every input), and a nullable previously checked radio reference captured only when the
pre-activation type was Radio. It contains no group snapshot to restore wholesale. Finite reentrancy
with a listener guard or a distinct synthetic event is valid; the current claim that same-target nested
dispatch is always unbounded is insufficient. Pass a record through dispatch, or use a per-dispatch
scope released in finally; do not retain one overwritable slot per wrapper.

Before listeners, Checkbox flips checkedness then clears indeterminate. Radio captures
FirstCheckedRadio and sets itself true through the algorithm lane. Browser applies explicit
UserInteraction provenance only for the actual user-change path; synthetic dispatch does not become
a checked IDL assignment merely because both are initiated from JavaScript. The Browser cutover must
pin tests for dirty-default behavior of trusted clicks, `.click()`, dispatched MouseEvent clicks,
already checked radios, cancellation and listener property writes, and document any interoperability
correction before changing that policy. No native `isTrusted` heuristic is introduced in b2.

On canceled activation, inspect the **current** type:

- Current Checkbox: restore captured pre-activation Checked and Indeterminate through Algorithm,
  preserving current dirtiness. Capture both even if it began as Radio or Text, because a listener can
  change type. Do not branch solely on the snapshot's original type as current Browser does.
- Current Radio: if the captured previous radio still belongs to its current group, set that previous
  member checked via Algorithm. Its normal exclusion clears the target and any intervening checked
  member. Otherwise set only the target false. Do not re-check a saved radio that was renamed, adopted,
  moved to another tree/form, or changed type out of Radio. Do not uncheck the target first when the
  saved reference still applies: the saved reference may be the target itself.
- Other current type: no checkable rollback. Still release the dispatch-local record.

Membership is tested at rollback time, not against original owner/name/root fields. Detaching both
radios together can preserve membership; moving only one can break it. A listener may create a new
selection while canceled activation is pending: restoration follows the algorithm, not a transaction
that undoes arbitrary DOM or script changes. Canceled activation restores neither attributes, dirty
flags, type, parent, form owner, value nor user validity. Browser discards the record on success,
cancellation, changed-type early returns and exceptions; no retained old document through saved peers.

On non-canceled Checkbox/Radio activation, the current input activation behavior returns when detached,
otherwise Browser fires plain input (bubbles and composed) then change (bubbles). The native setter
fires neither. Preserve dispatch/listener sequencing and test already-selected radio behavior against
the pinned standard/WPT rather than inventing a value-change-only event shortcut.

Disabled policy belongs to the caller's algorithm, not a blanket native setter guard. `HTMLElement.click()`
and queued user interaction have disabled suppression, while an explicitly dispatched activation event
is a separate path. Current HTML's general input activation explicitly exempts Checkbox/Radio from
its immutability early return. Browser's unconditional disabled guards in both pre-activation and
RunInput therefore require an explicit correction/test packet; copying them would conceal this gap.
Readonly does not make a checkbox/radio immutable. Native disabledness remains D7a2's single answer.
All Browser edits, including JsEventTarget/EventDispatch signatures if needed, require their own
reviewed ownership and tests; none are part of the b2 native implementation packet.

## Bounded implementation packets and verification

| Packet | Prerequisites, ownership and completion |
| --- | --- |
| D7b2a native component, group index and mutation integration | Complete D7b1a classifier, D7a1 ownership and D7a2 available; coordinator releases shared files after D6s2 and reconciles textarea/X4b changes. New HtmlInputCheckedState, HtmlCheckableState, HtmlRadioGroupIndex, HtmlCheckednessAlgorithms, HtmlInputStateChanges and work probe; reserve HtmlElementState/HtmlFormAssociation and actual Node/Element/Attr/Document/NodeCloner hooks as one owner. Land only working internal checked-state operations, with all public/native mutation paths tested. No incomplete general Input facade or public promotion. |
| D7b2b parser lifecycle integration | Separate handoff to current H/X owners. Complete attribute-batch initialization, parser owner ordering, XML effective defaults, fragments and connected insertion. Reuse native hooks, do not edit tree-building algorithms or create parser radio state. Runtime feature completion waits for this integration. |
| D7b1d shared complete input core | Reuse b2's component; add no second flags or hooks. Requires full b1/b2/b3/b4 dependencies, implements all 22 state transitions, whole reset and clone. Type signal is invoked once at the prescribed point; b2's temporary routing location is transferred, not duplicated. |
| C3a checkable predicates | After native integration, call b2 facts for checkbox/radio portions with work tests; option/progress/default-button portions wait for their real owners. No environment adapter fallback for intrinsic native state. |
| B2/B3 Browser cutover | After complete input core and approved promotion, generated binding inputs, dispatch-local activation records, event/rollback/disabled-policy corrections and successful-control/reset integrations. Separate engine/event ownership; no AngleSharp state retained alongside native state. |

The first packet does not claim complete HTML input semantics simply because its flags survive type
changes. Remaining value families are explicit prerequisites, not an invitation to prohibit such type
changes, return empty strings, or silently implement only text/checkbox/radio. Independent native tests
can exercise checkable flags on all classified types without publishing an incomplete whole input API.

Native regression matrices must include:

- Fresh, clean, dirty and equal assignments; checked Attr.Value versus add/remove/replace; literal
  `checked="false"`; namespaced lookalikes; default after peer exclusion; dirty false does not mean
  current always equals default. Verify peer dirtiness, no synthetic mutation records and revision use.
- Every type with stored checked/indeterminate; actual state transitions versus case-equivalent type
  spellings; missing/invalid type. Reset leaves indeterminate; clone/import copies it and all flags.
- Same/different owner, parser-only owner, nonancestor explicit owner, duplicate form IDs, first-ID
  becoming non-form, ID rename/removal, form attr set/remove, required disabled peer, all names including
  empty/missing/space/case/non-ASCII, wrong namespace/local-name applicability and empty fact results.
- The exact unrelated `div#x → div#y` fixture above: all root-wide form-attribute candidates reset;
  a's `f → null → f` transition unchecks b despite unchanged final ownership and no matching form value.
  Probe candidate coverage separately from actual radio transitions; ordinary class/text changes do
  not trigger this reset batch. Include the required ID-bearing insertion/removal variants.
- Detached element-root radios with children, fragments, separate roots in one document, template
  contents, open/closed/nested shadow roots, slot changes, host connect/disconnect and adoption. No group
  crosses an ordinary root; connection triggers still reach shadow descendants.
- Two separately checked radios appended to a disconnected unowned container retain both flags;
  a read does not normalize them; equal checked=true and equal name writes do. Connect that subtree
  and assert the actual insertion-step winner, not a precomputed last-checked snapshot. Sequential
  connected parsing, reset and explicit setters each have their own last-triggered fixtures.
- Parser complete-attribute order permutations, duplicate attributes, XML defaults, fragment contexts,
  clone/import/adopt/document-clone, suppressed mutation, replace-all and hierarchy-failure paths.
  Keep source state/identities distinct; copied clean-but-unchecked radios must not regain defaults.
- Native rollback ingredients: first checked identity, same-current-group decisions after name/type/
  owner/root changes, algorithm writes preserving dirty state. Later Browser tests must cover the full
  listener/event matrix, canceled type changes, nested finite dispatch and exception cleanup.

Work/lifetime gates use deterministic probes, not wall-clock assertions: wide groups of 1/1,000/10,000
members; many singleton/many distinct-name groups; long names and deep parents; repeated fact queries;
single-selected versus multi-checked detached groups; sequential parser appends; interleaved unrelated
DOM mutations; moving large subtrees and clearing old indexes. Measure visits, scans, affected checked
members and allocations only through suitable untimed assertions/instrumentation, without claiming a
benchmark result. Require one bootstrap per stable root, O(1) hot facts, no all-member scan per normal
selection, no prefix rebuild per append, and prompt release of empty buckets/old-tree references.
For ID-triggered work, require complete F-candidate reset coverage and charge its real owner-resolution
and group-transition work separately; do not apply the unrelated-mutation or subtree-only budget to it.
Use weak-reference GC tests with setup isolated from assertion frames to prove a retained old document
does not retain removed radios through its index. Holding a snapshot intentionally retains its members;
releasing it and any active dispatch record must release that extra ownership.

Cancellation tests cancel before entry, during cold build/deep ascent/name work/snapshot traversal and
mutation preparation; assert no half-published index, no half-applied exclusion and correct subsequent
query. Cover cancellation observed after an already committed parser insertion with all group hooks
complete. Cross-check against an independent slow tree-derived oracle over a deterministic sequence
of mutations; the oracle calculates membership from current tree/owner/type/name and reads actual
flags, without normalizing them. It must not reuse production index helpers.

Run fresh Release native focused and broad non-corpus checks on supported net8.0/net10.0 for each
implementation packet. Full Browser/event and public-consumer tests belong to their later packet.
Dedicated performance measurements, when the coordinator releases the machine, run **only the newest
TFM**, report operations/second and allocated bytes, and compare cold build/hot facts/selection/mutation
separately. Reuse the established benchmark environment and Markdig primitive harness; no short-job
speed claims, no simultaneous measurements, and no timing claim is made by this design.
