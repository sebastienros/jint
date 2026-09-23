# D7b1: native input text and textarea state

Design for independent review, 2026-09-23. Refines [D7's required family inventory](html-parser-form-state.md)
without reducing D7b3/b4 or publishing a partially working general input API. Common sources inspected
through `ee9eb3693`, including D7a1 association, the native mutation/clone lanes and HTML/XML parsers.
This change is documentation only. Shared native files require a fresh coordinator handoff after their
current X4b1/D7/D6 owners; a completed earlier task does not imply that its files are unreserved now.

The native element owns the current value, dirtiness, edit provenance and selection. Browser owns
WebIDL conversion, editing UI decisions, script, events, focus, tasks, composition and layout. An HTML
namespace/local-name match is required even in XML documents; uppercase/no-namespace lookalikes do not
acquire input or textarea state. No AngleSharp state store remains behind migrated getters.

## Evidence and consumer corrections

Use the HTML Living Standard inspected at its 2026-09-22 revision:
[input types, transitions and value modes](https://html.spec.whatwg.org/multipage/input.html#the-input-element),
[textarea](https://html.spec.whatwg.org/multipage/form-elements.html#the-textarea-element),
[selection algorithms](https://html.spec.whatwg.org/multipage/form-control-infrastructure.html#textFieldSelection),
[length constraints](https://html.spec.whatwg.org/multipage/form-control-infrastructure.html#attr-fe-maxlength),
[DOM child text content](https://dom.spec.whatwg.org/#concept-child-text-content), and
[selectionchange scheduling](https://w3c.github.io/selection-api/#selectionchange-event).
The algorithms, not the non-normative applicability summary or an older engine, settle disagreements.

| Existing consumer | Native seam and required migration |
| --- | --- |
| `Dom/Generated/DomShapes.Html.g.cs`, HTMLInputElement/HTMLTextAreaElement | Generated value/defaultValue/type/reflection and selection members must use the same native state. Current selection offsets are signed Int32, direction is getter-only, and setRangeText is absent. B2 must add the missing standard operations and correct unsigned/nullable WebIDL conversion through the generator/overrides, not edit generated C#. |
| `Events/TextEditing.cs` | Replace direct AngleSharp Value/Select and clamp-on-read repairs with native value and selection operations. Its editable list includes email and number; public selection APIs do not apply to either. Preserve their user-edit support through distinct internal editing capabilities, with number's bad-input representation assigned to b3. |
| `Events/SelectionChange.cs`, `FocusController`, input dispatch | Browser keeps task coalescing, focus-time change baselines and beforeinput/input/change. Exact direction changes, including none versus forward, matter; the current backward-versus-not comparison is an AngleSharp workaround to remove. |
| `Events/FormSubmission.cs`, `Runtime/FormSubmitter.cs` | Reset after the cancelable Browser reset event uses native control reset. Textarea entry construction uses its submission value, including hard wrapping; current code reads only its API Value. CRLF encoding remains after formdata, not in value getters. |
| `Runtime/Parsing/PagePseudoClassSelectorFactory.cs`, accessibility, `HtmlDirectionality` | Consume shared type applicability, current API value, dirtiness, last user edit, disabledness and readonly facts. Required/readonly/placeholder are not generic attribute-presence predicates; D7f still owns complete validity. |
| HTML `HtmlTreeBuilder` and XML `XmlTreeParser` | Initialize input after its complete resolved attribute batch; textarea default follows direct text children, and parser completion resets it at the specified pop point. No Browser-only parser fixup. |

Existing Browser regressions include Events/EditingTests, PageInputTests, SelectionChangeTests,
Forms/FormTests, FormEntryTests, DirectionNameTests and Parsing/PagePseudoClassSelectorTests. A1 remains
the exhaustive binding inventory; this document also names standard gaps absent from A1. The selected
local WPT files do not establish full forms/selection coverage. Future corpus work must inventory the
pinned input/textarea/selection tests and expected outcomes; no upstream pass count is claimed here.

## One complete input-state table, shared by b1/b2/b3/b4

Land the classifier independently before state integration. All 22 current states remain required.
Missing, empty and invalid type tokens select Text. Compare recognized keywords ASCII-insensitively,
without trimming, Unicode folding or allocating a lowercase copy. `datetime` is invalid, not an alias
for datetime-local. The type IDL getter returns the canonical keyword; its setter writes the supplied
attribute string and lets the native transition hook act. Equivalent spellings do not change state.

`V` = value mode, `D` = default mode, `O` = default/on mode, `F` = filename mode. `S` means the public
selectionStart/End/Direction, setSelectionRange and setRangeText APIs apply. `R` means readonly applies,
`Q` required, `L` min/max length and size, `P` placeholder. Select means select() applicability, which is
distinct from S and can still be a no-op when the control has no selectable text.

| State keyword | Mode | S/R/Q/L/P | Select | Value family/default and sanitization owner |
| --- | --- | --- | --- | --- |
| hidden | D | — | no | b1 mode plumbing; absent value is empty |
| text | V | S R Q L P | yes | b1 newline removal |
| search | V | S R Q L P | yes | b1 newline removal |
| tel | V | S R Q L P | yes | b1 newline removal |
| url | V | S R Q L P | yes | b1 newline removal and ASCII edge-space trimming |
| email | V | R Q L P | yes | b1 single-address versus multiple-token normalization |
| password | V | S R Q L P | yes | b1 newline removal |
| date | V | R Q | yes | b3 date grammar; invalid becomes empty |
| month | V | R Q | yes | b3 month grammar; invalid becomes empty |
| week | V | R Q | yes | b3 week grammar; invalid becomes empty |
| time | V | R Q | yes | b3 time grammar; invalid becomes empty |
| datetime-local | V | R Q | yes | b3 local date/time normalization; invalid becomes empty |
| number | V | R Q P | yes | b3 HTML floating grammar, empty on invalid; separate user bad-input state |
| range | V | — | no | b3 min/max/default/step sanitization; default is not universally 50 |
| color | V | — | yes | b3 + CSS color owner: current color-well parse/conversion/serialization, alpha/colorspace |
| checkbox | O | Q | no | b1 value mode; absent value is on; b2 checked/group state |
| radio | O | Q | no | b1 value mode; absent value is on; b2 checked/group state |
| file | F | Q | yes | b4 selected-file state and value restrictions |
| submit | D | — | no | b1 value mode; b4 UI/default label and submit overrides |
| image | D | — | no | b1 value mode; b4 image/coordinates/submit overrides |
| reset | D | — | no | b1 value mode; b4 UI/default label |
| button | D | — | no | b1 value mode; b4 remaining reflection |

Select's UI-dependent states must not be silently equated to S. The standalone model provides a
selectable buffer for textarea and b1's six textual input states, including email. Date/time/number/
color/file UI selection is supplied by their native family/presentation integration; no public null
selection getter is repurposed as that internal editing buffer. A control with no selectable buffer
makes select() a no-op, not InvalidStateError. Range never gains select() through a generic text buffer.

Use `HtmlInputType` with one enum member per row, `HtmlInputValueMode { Value, Default, DefaultOn,
Filename }`, and a readonly `HtmlInputTypeInfo` containing the mode and the exact applicability booleans
above (separate HasSelectionApi and SelectApplies). Internal static `HtmlInputTypes.Parse(string?)`,
`Get(Element)` and `Info(HtmlInputType)` are the only classifier. Get requires an actual HTML input.
Replace D7a1's local image check only in a coordinator-owned integration commit. No unimplemented
type collapses to Text after parsing; classifier completeness does not imply sanitizer completeness.

The full type transition must execute the HTML mode-change algorithm in order, including writing a
nonempty old V value into the content attribute on V→D/O, loading default and clearing dirtiness on
non-V→V, clearing value on entry into F, signaling b2 type changes, running the new sanitizer and
initializing selection when entering S from a non-S state. A V→V transition preserves dirtiness.
Do not implement type as merely replacing an enum then normalizing one string. Same-state `TEXT`→`text`
must not reset value, selection or dirty flags. Type-triggered content-attribute writes use ordinary
attribute hooks and records, with a bounded reentrancy-free native transition context.

Current Color is not the old lowercase-six-hex-digits algorithm. Its dependency includes CSS colors,
alpha, limited-srgb/display-p3 and the specified serialization path. This design keeps that work in b3
with its CSS owner. The date/month/week/time/datetime-local, range and file lanes likewise cannot return
fake empty values until implemented. The input-state integration slice below waits for those value
prerequisites; independent textarea/classifier/string work can land before them.

## Storage and concrete operations

Keep the existing stable HtmlElementState view and one element-owned backing state. Add nullable Input
and TextArea views only when their respective complete operations land; null means wrong native element
kind, never unfinished implementation. No public constructor or state dictionary keyed globally by nodes.
All types below are internal in `Jint.HtmlParser`, implemented under `Dom/Html/`.

`HtmlInputState` owns the stored value, dirty value flag, last-value-change origin and family state;
`HtmlTextAreaState` owns raw value, dirty value flag and last-value-change origin. User validity is one
element-owned boolean initially false, shared with D7f and later select state, not inferred from dirty
value. Selection is one common `HtmlTextSelection` component with UTF-16 offsets and direction. Dirty
and origin data are retained even when no Browser exists. Read-only default reflection does not create
a second default string; attributes/child text remain authoritative for defaults.

Fresh stored/raw values start empty, dirty false and origin NonUser. The input creation path then
applies its complete initial attribute batch and current sanitizer; factory-created inputs follow the
same rule with no attributes. Lazy view allocation must never replay initialization or lose stateful
type/attribute operations that happened before the first view read. Changes to value, dirty/origin,
user validity or selection advance the owning document's mutation stamp before the next query, even
when no content attribute changed; no fabricated DOM MutationRecord accompanies those state writes.
Pure reads/view allocation do not advance it. Adoption uses existing source/destination invalidation.

```csharp
internal enum HtmlSelectionDirection { None, Forward, Backward }
internal enum HtmlRangeTextMode { Select, Start, End, Preserve }
internal enum HtmlValueChangeOrigin { NonUser, User }
internal readonly record struct HtmlTextSelection(uint Start, uint End, HtmlSelectionDirection Direction);

// On both completed state types; input's GetValue follows all four modes.
internal string GetValue(CancellationToken cancellationToken);
internal string GetDefaultValue(CancellationToken cancellationToken);
internal void SetValue(string value, CancellationToken cancellationToken);
internal void SetDefaultValue(string value, CancellationToken cancellationToken);
internal bool DirtyValue { get; }
internal HtmlValueChangeOrigin LastValueChangeOrigin { get; }
internal bool UserValidity { get; }
internal void SetUserValidity(bool value); // native commit/submission callers, no event
internal void Reset(CancellationToken cancellationToken);

// Shared operations facade accepts an HTML input or textarea and enforces applicability.
internal static class HtmlTextControl
{
    internal static HtmlTextSelection? GetSelection(Element element, CancellationToken cancellationToken);
    internal static void SetSelectionStart(Element element, uint? value, CancellationToken cancellationToken);
    internal static void SetSelectionEnd(Element element, uint? value, CancellationToken cancellationToken);
    internal static void SetSelectionDirection(Element element, string? direction, CancellationToken cancellationToken);
    internal static void SetSelectionRange(Element element, uint start, uint end, string? direction,
        CancellationToken cancellationToken);
    internal static void Select(Element element, CancellationToken cancellationToken);
    internal static void SetRangeText(Element element, string replacement, CancellationToken cancellationToken);
    internal static void SetRangeText(Element element, string replacement, uint start, uint end,
        HtmlRangeTextMode mode, CancellationToken cancellationToken);
    internal static HtmlTextSelection? GetEditingSelection(Element element,
        CancellationToken cancellationToken);
    internal static bool SetEditingSelection(Element element, uint start, uint end, string? direction,
        CancellationToken cancellationToken);
    internal static bool ApplyUserValue(Element element, string value, HtmlTextSelection selection,
        CancellationToken cancellationToken);
}

// Additional textarea operations; raw value remains internal storage, not an extra public property.
internal uint GetTextLength(CancellationToken cancellationToken);
internal string GetSubmissionValue(CancellationToken cancellationToken);
```

Null required strings/elements throw ArgumentNullException. Wrong element kind throws ArgumentException.
Native methods receive already converted WebIDL values; uint can represent the full unsigned-long
range, and nullable selection setters implement input's null→0 behavior. Browser converts textarea's
non-nullable setters separately. Browser must honor LegacyNullToEmptyString on value while retaining
ordinary DOMString conversion for defaultValue and replacement; missing overload arguments and invalid
SelectionMode are WebIDL failures, not native defaults. Negative JS offsets undergo unsigned conversion
before native clamping. Unknown direction strings become None; use ordinal exact matching.
Native standalone behavior supports None and empty selections on all OSes, so there is no machine-
dependent forward default. Invalid internal mode enum arguments throw ArgumentOutOfRangeException;
Browser's invalid SelectionMode conversion is a TypeError before any native call.

ApplyUserValue is the event-free post-beforeinput commit for b1 textual inputs and textarea: it checks
current native mutability, returns false unchanged if no longer applicable/mutable, sanitizes the new
value, sets dirty/user origin when an actual user edit changes it, and applies the supplied final
selection clamped to the resulting relevant value. It bypasses public selection applicability for
email's internal editor only. It does not create a number editor; b3 supplies that family operation
with explicit display/bad-input semantics. No caller may use SetValue then set a user flag afterward,
because observers/selectors could see an incoherent cause and script cursor movement in between.

GetEditingSelection and SetEditingSelection are the selection-only counterpart for the same six b1
textual input states and textarea, including email. They read/write the same HtmlTextSelection backing
component used by ApplyUserValue, Select and applicable public selection APIs; there is no second caret.
For an input outside this editing family the getter returns null and the setter returns false unchanged;
the wrong native element kind still throws ArgumentException. B3 supplies number's distinct editor
integration rather than making these operations pretend number has a b1 textual buffer.

SetEditingSelection clamps and normalizes direction through the common selection-range algorithm,
returns true for an applicable operation even if unchanged, and never alters value, dirtiness, edit
origin or user validity. Selection-only movement does not require value mutability: readonly/disabled
do not make the native setter reject it. Browser separately decides focused/disabled UI eligibility and
allows selection without treating readonly as permission to edit. A changed extent/direction emits the
same native change record with SelectionRangeApplied=true; no change emits none. Browser maps these to
select and selectionchange exactly as for other selection-range operations. Arrow/Shift/Home/End and
select-all paths must use this seam after any script callback, not email's public null getter or its
public SetSelectionRange, which continues to throw InvalidStateError.

The UI computes its intended splice and caret; native code owns the actual value and offsets. No JS
callback runs inside ApplyUserValue. After beforeinput, Browser must reread type, value, selection,
disabledness and readonly before committing; the current TextEditing snapshot from before the callback
cannot overwrite script's changes blindly. Focus snapshots and undo/composition history are Browser
interaction state, not alternative native current values. Distinguish actual edit provenance from
user validity: ordinary keystrokes do not automatically make user validity true. Commit/blur/submission
owners call SetUserValidity at their specified algorithm points, before the corresponding observable
event. Reset clears it; a script value assignment does not clear it merely by being a script assignment.

## Value, default and textarea semantics

Input's content value/defaultValue is the unsanitized default. Clean value state follows attribute
append/change/remove through the current sanitizer; dirty state does not. SetValue in V mode dirties
even on equal assignment and applies the current sanitizer. Compare the final relevant value to the
old relevant value before deciding cursor movement: changed value moves to the end/None, equal value
retains selection. D/O writes affect the content attribute, not a hidden dirty-value override. File
mode permits empty assignment to clear files and rejects nonempty assignment with InvalidStateError;
its getter and selected-file lifecycle are b4's required implementation, not a b1 string imitation.

Implement b1's sanitizers as independent pure bounded helpers before hooking state: text/search/tel/
password remove CR and LF; URL and single email additionally trim ASCII edge whitespace. Multiple
email follows its distinct comma-token normalization; do not prepend single-email sanitization to
that branch and erase interior newlines the branch does not remove. Changing multiple while in Email
runs the applicable sanitizer even when dirty. Sanitization is not email/URL constraint validation.
No universal string.Trim, culture comparison or regex validation belongs in these helpers.

Textarea defaultValue reads **child text content**, including direct Text/CDATA but not a nested
element's descendants or Comment/PI. SetDefaultValue uses native string-replace-all; it produces normal
DOM mutations. Clean children-changed steps replace raw value; dirty textarea children can change the
default without replacing the current raw value. CharacterData replacement on a direct text child
must run that same children-changed semantic hook. Generic descendant TextContent is not the getter.

Keep raw value distinct from API value. CRLF and lone CR become LF only for API value; offsets,
textLength and length constraints use that API string. SetValue preserves supplied raw newlines and
makes the control dirty; move selection only if the normalized API value differs. Thus changing raw
`A\r\nB` to `A\nB` need not move an existing selection. Do not normalize the default text nodes as a
side effect of reading Value, and do not use submitted CRLF positions for selection.

GetSubmissionValue starts from API value and applies hard wrapping without mutating raw/API storage.
Choose and document this standalone implementation-defined wrap policy: break each LF-delimited line
after at most cols Unicode code points, no word search, preserving existing LF, with surrogate pairs
kept together and unpaired UTF-16 units counted singly. Do not add another LF immediately before an
existing LF or at an exact final boundary. This is a non-layout policy, not a visual-line claim.
Invalid/missing cols uses character width 20; rows does not affect submitted value. Browser entry-list
construction uses this native result before its existing post-formdata CRLF encoding. Soft/default
wrap does not add breaks. The wrap IDL string reflects the literal attribute; its effective Soft/Hard
state is a separate enum read (missing/invalid→Soft), not the reflection getter.

Reflections name, dirName, readonly, required, placeholder and textarea wrap still route through native
Element attributes and B2's reflection rules. Do not persist copies in control state. Autocomplete's
IDL getter uses the autofill token algorithm, not raw reflection: that bounded helper is a separate
b1 reflection slice shared with the form/input owners; actual autofill remains Browser. Input size is
unsigned with default 20 and its specified zero-setter error; textarea cols/rows use positive-with-
fallback defaults 20/2. minLength/maxLength are signed nonnegative reflections with absent/invalid -1
and their setter errors, distinct from effective semantic limits. Do not reuse signed Browser selection
conversion for these unrelated algorithms.

Provide internal `HtmlTextControlAttributes.GetMinimumAllowedLength(Element, CancellationToken)` and
`GetMaximumAllowedLength(...)` returning `long?`: null when inapplicable/missing/invalid, otherwise
the HTML nonnegative-integer parse result saturated at Int32.MaxValue+1, which remains above every
representable native string length. This preserves too-short comparisons even for enormous declared
minima. This semantic saturation is not the numeric IDL getter. Parse allowed whitespace/sign/digit
prefix rules with cancellation; the current Browser Trim + strict Int32 parser is not the authority.

Script SetValue/setRangeText and default updates are never truncated by maxlength. Browser may retain
its user-entry truncation policy, but bases it on native applicability and normalized candidate API
length, not raw CRLF length. D7f computes tooLong/tooShort from dirty state, last edit origin and API
length (and nonempty value for tooShort); do not confuse those flags with the UI truncation policy.
Record NonUser for successful script setter/setRangeText, including equal programmatic assignment,
while still preserving the specified equal-value selection behavior. Native default/child/attribute
hooks preserve current-value origin when dirtiness suppresses the current-value update; changing only
the default is not a programmatic edit of the current value. Thus a dirty user-edited input or textarea
with maxlength=1 and current value "ab" retains User origin and its tooLong prerequisite after a
defaultValue-only change. Hooks that actually update the current value, reset and value-changing type
transitions establish NonUser; same-state/no-value-change type hooks do not erase origin. Clone begins
with NonUser provenance. These choices need literal validation tests; origin must not be inferred from
dirty=true, arbitrary attribute mutation, or whether an input event happened to be dispatched.

## Selection and replacement operations

Initial public-applicable selection is (0,0,None), even detached, disabled or not rendered. GetSelection
returns null for non-S inputs. The five public selection mutation operations reject those inputs with
InvalidStateError, while Select uses its separate applicability/buffer rule. Script operations on a
disabled or readonly applicable control still work. Editing mutability is a different predicate.

Use UTF-16 code-unit offsets, including positions inside surrogate pairs and around zero-width code
points. Clamp uint inputs before converting to int. SetSelectionRange with end≤start collapses both
to end, not start. SetSelectionStart can increase end; SetSelectionEnd can lower start; direction
setters preserve extent. Whenever relevant value changes, clamp endpoints, then apply any additional
setter-specific movement. Equality includes the full direction enum. Intermediate arithmetic for
replacement lengths/deltas uses checked long arithmetic before allocating a CLR string; do not wrap
uint subtraction or expose a new arbitrary text-length quota.

SetRangeText keeps two real overloads. The one-argument operation takes current public selection; the
explicit overload clamps supplied offsets only after rejecting start>end. Applicability is checked
first, **then dirty value becomes true before that IndexSizeError**. An invalid range therefore affects
later default propagation even though no text changed. WebIDL conversion failures occur earlier in
Browser and do not dirty anything. Preserve this exception order; a generic validate-all-first wrapper
would change behavior. On this range error, retain the old value, edit origin, selection and user
validity: only dirtiness has changed. No value edit or selection notification is manufactured.

Replacement uses the relevant value, not textarea raw offsets. Snapshot original selection, splice
the relevant string with replacement, then apply the mode: inserted span, start, end or preserve.
For Preserve, handle each saved endpoint with the exact ordered branches: if it is greater than the
removed end, add the signed delta; otherwise, if it is greater than the removed start, map the saved
selection start to the removed start and the saved selection end to new end. Endpoints equal to the
removed end therefore snap too; endpoints equal to the removed start remain there. For value "abcdef",
selection (4,4), replacing [1,4) with "X" yields "aXef" and selection (1,2,None). Omitted mode and the
one-argument overload both select this same preserve path.
The final selection-range call has omitted direction, hence None. Do not call the script value setter
as a shortcut and lose the saved selection or add its cursor-to-end behavior. Apply any type-required
sanitization/newline projection at the appropriate value-update boundary, then clamp against the final
relevant value; replacement-length arithmetic still follows the supplied replacement's UTF-16 length.
Add CR/LF replacement fixtures specifically to keep these two lengths from being conflated.

Native operations dispatch no select, selectionchange, beforeinput, input, change, invalid or reset
event and do not focus the element. A successful selection-range operation that changes extent or
direction requests Browser's queued select event; any actual selection change also needs Browser's
selectionchange scheduling, including changes caused by value/default/type/reset hooks.

For those automatic hooks, add a narrowly scoped native data subscription in the final b1 integration
slice: `HtmlTextControlChanges.Observe(Document)` returns an internal disposable
`HtmlTextControlChangeSubscription` with `IReadOnlyList<HtmlTextControlChange> Drain()`.
Each immutable record holds Element, before/after nullable HtmlTextSelection and a bool
SelectionRangeApplied. Record changed **algorithm steps**, not just a top-level before/after diff:
SelectionRangeApplied is true only when that selection-range step itself changed extent/direction.
Automatic relevant-value clamping is a separate false record even when the same operation later calls
the range algorithm. For example, shrinking text to empty can clamp selection to (0,0) and leave the
final range step with nothing to change: schedule selectionchange but no select. Keep the ordered
step records until coherent commit/drain; do not promote all changes to range changes because the
outer operation called the range helper, merge them into a final diff, or coalesce distinct select
requests. No JS object, callback, realm or event name is stored. Document
registers subscriptions weakly; the subscription owns its pending native records; disposal clears
them and registration. With no subscriber, allocate no records/queue. Browser drains at the coherent
native-operation boundary, maps SelectionRangeApplied to select and all records to its coalesced
selectionchange task. Mere initial allocation or clone initialization emits no record. No script or
drain occurs halfway through semantic bookkeeping; adoption preserves native target identity and
Browser owns realm/task delivery. This transport is not a second MutationObserver stream.

## Hooks, parser completion, reset and clone ownership

Before committing any attribute/tree/data change that requires expensive preparation, compute the
new value state and immutable text storage without publishing it. Then finish ordinary DOM links,
attribute values, control state, form hooks and records coherently. Native DOM entry points with no
cancellation token retain their synchronous contract; do not inject cancellation mid-fixup. Explicit
token-bearing value operations may cancel during preparation, before commit. The setRangeText dirty-
before-IndexSizeError case is intentional partial algorithm progress, not failed-preparation rollback.

Required hook sites, independent of observer suppression:

- Element attribute initial batches and append/remove/replace, Attr.Value including equal writes:
  input type/value/multiple; all family constraints needed by sanitizers (b3 min/max/step,
  alpha/colorspace); readonly/disabled/required/length changes invalidate relevant derived facts.
- Node insert/remove/replace-all/fragment drain and direct Text/CDATA Data replacement, parser append,
  split/normalize and clone append: textarea direct-child changes only. Route the shared native
  children-changed point once per specified mutation, not a parallel Browser text observer.
- NodeCloner: copy input value/dirty flag together with b2's checked/dirty-checked/indeterminate and
  textarea raw value/dirty flag at HTML's cloning-step point. Do not copy form owner, user validity,
  public selection, focus/undo history or pending notifications. Adoption preserves control state;
  the destination owns future mutations and derived-state invalidation.

A shallow clean textarea clone retains the copied raw value even when it has no copied children and
its defaultValue is empty. The first subsequent children-changed step can replace raw value because
the copied dirty flag is false. Do not implement clean textarea as a permanent alias to defaultValue:
that erases this clone case. Deep clone insertion follows actual HTML/DOM clone order, not a final
whole-tree patch. Clone/import has fresh native state identities; source state is not modified.

Textarea Reset sets user validity/dirty false and raw value to current child text; selection receives
ordinary relevant-value clamping, not the script setter's unconditional end policy. Input Reset is
the whole input reset algorithm, including b2 checked flags/checkedness, b4 files and current sanitizer;
do not expose a form reset that only resets text members. Browser performs form owner enumeration and
cancelable reset delivery in its assigned integration; native Reset itself is silent.

HTML parser integration owns the call after textarea is removed from the real open-element stack:
`HtmlTreeBuilder.Pop` includes explicit end tag, EOF InText recovery and scheduled multi-pop paths.
Audit non-Pop stack removal paths before claiming coverage. The HTML tree builder/tokenizer retains
its first-LF-after-textarea-start rule; native raw state must not strip that LF again. XML parser calls
the same textarea reset after an HTML-namespace textarea's matching frame pop; handle self-closing
elements at their equivalent completion point. XML does not get HTML's initial-LF suppression.
Flush pending XML text before resetting. A fragment context element is not itself a newly parsed
textarea to reset; parsing a fragment must not overwrite an existing dirty context's value. Markup
replacement then takes effect through actual insertion/children-changed hooks. No later EOF scan of
all elements substitutes for these lifecycle points.

The parser must not introduce O(N²) growth by materializing Text.Data/raw/API strings on every token
or polling slice. Native Text.AppendParsedData currently keeps amortized storage; keep that advantage.
A clean, unobserved parser-built textarea may retain a lazy direct-child projection, invalidated in
O(1), with materialization only on actual reads/clone/parser completion. Once a non-parser operation
needs a snapshot or an endpoint clamp, resolve it coherently before publishing that operation. Plain
append cannot shorten the normalized API value and must not force a full-prefix scan just to clamp.
Cache raw→API normalization per raw-value revision, not per Document.MutationStamp; unrelated DOM
changes must not rematerialize a megabyte textarea. Preserve explicit copied-raw state for the shallow
clone case above. Default reads may cost returned text size; parser appends without reads may not.

## Finite slices and gates

| Slice | Files and bounded completion |
| --- | --- |
| D7b1a classifier | New Dom/Html/HtmlInputTypes.cs plus Html/InputTypeTests.cs only. Complete table, canonical keyword parsing, mode and applicability. Independently dispatchable after this design review; no shared-file hooks, public API or sanitization claim. |
| D7b1b pure text algorithms | New HtmlTextSanitizer.cs, HtmlTextControlAttributes.cs and tests. Six text-type sanitizers, textarea newline/wrap helpers, effective length parsing; no native element-state integration. Keep full b3/b4 prerequisite ledger explicit. |
| D7b1c textarea | HtmlTextAreaState/HtmlTextSelection/working textarea facade, state slot and authorized Element/Node/CharacterNodes/NodeCloner hooks, focused native tests. Requires D7a2's native disabledness for user edits. Coordinator reserves shared files after X4b1/D6/D7 handoff. This can finish independently of input numeric/file work; no input stub is added. |
| D7b1d complete input value core | Shared b1/b2/b3/b4 integration owner, after every sanitizer/value-mode dependency and checked/file transition hook is real. Add HtmlInputState and input facade support, all type transitions/reset/clone. This task cannot silently restrict type mutation to six text states or provide empty fallbacks for b3/b4. |
| D7b1e parser and notifications | Separate HTML/XML parser handoff; native selection-change subscription and integration sites; parse documents/fragments/EOF/self-close and automatic-change tests. Work with H and X owners, not an overlapping parser edit. |
| D7b1f reflection and Browser cutover | B2/native helper owner completes autocomplete and numeric reflection semantics, reviews unsigned/nullable bindings, adds direction setter/setRangeText through generator, native user-edit and event drain adapters. C3 and D7f consume native facts; full validation is still its own family. |

No public promotion in a–e. At f, review the smallest actual consumer-facing surface and API snapshots;
do not publish state fields, transport records or a get-everything options bag by default. Standalone
packed-consumer tests must exercise real value/default/selection/reset behavior without friend access,
including all input value modes and a transition across families, before a general public input view
ships. No runtime, Browser, dependency, generator or snapshot changes accompany this design commit.

Native acceptance uses freshly compiled Release net8.0 and net10.0 tests, not stopwatch assertions:

- Every table row and invalid/missing/case/space type spelling; HTML-namespace controls in XML versus
  uppercase/no-namespace controls; all V/D/O/F transition pairs, both empty/nonempty old values, dirty
  and clean, never-accessed state, same-state writes and correct content-attribute record order.
- Equal and changed value/default assignments, reset after user/script edits, readonly/disabled script
  writes, dirty-before-range-error, fragment changes, clone/import/adopt and shallow clean textarea
  cloned raw value distinct from its empty default. No dirty inference from observers or event logs.
- CR/LF/CRLF across direct text-node boundaries, nested text exclusion, CDATA, comments, parser token
  splits, initial LF suppression exactly once for HTML and never for XML, parser pop/EOF/self-closing
  reset, dirty fragment context preservation, and ordinary mutation bypass paths.
- Nullable/uint-max selection, surrogate-half positions, end<start, per-endpoint setters, direction-only
  changes, non-S getters/errors and select no-op, email internal edit versus public null selection,
  number editing through b3, all replacement modes/equality boundaries, CR/LF replacements and signed
  delta overflow avoidance. Script setters/range replacement ignore maxlength; user policy does not.
- Dirty versus user-origin versus user-validity independence, too-long/short prerequisites, enormous
  semantic limits, wrap Soft/Hard/invalid, exact-width lines and supplementary characters. Literal
  expected strings/offsets, not an AngleSharp differential that normalizes known defects away.
- Dirty user value "ab" with maxlength=1 retains User/tooLong after input defaultValue or textarea
  defaultValue/direct-child edits that leave current value unchanged; equal script SetValue and
  successful setRangeText establish NonUser. Test both saved endpoints exactly at replacement end.
- Email Arrow/Shift/Home/End/select-all reads and changes the shared native editing selection without
  changing value/dirty/origin/user validity, while public selection getters stay null and public setters
  reject it. Cover readonly selection-only movement, no-op versus direction-only notifications and
  rejection after a reentrant type change to a non-b1 editor family.
- A replacement that shrinks to empty changes selection in the automatic clamp but not the final
  selection-range step: drain a false SelectionRangeApplied record, yielding selectionchange without
  select. Also cover multiple changed algorithm steps and retain their order until coherent drain.
- Cancellation checkpoints every at most 256 authored work units in normalization, trim/token scans,
  copying, wrap and child-text collection, including parent/child bookkeeping where traversed. Check
  entry and before commit/return and bracket CLR allocations/copies. Prepared cancellation leaves value,
  dirtiness, selection and notifications unchanged; cancellation never tears apart a committed mutation.
- Work counters for doubled long values and many parser chunks: linear work/allocation when no reads
  demand copies, repeated value/selection reads reuse API cache, unrelated document edits do not bust
  it. Repeated user edits necessarily copy edited string length; do not claim O(1) splices. Subscription
  disposal/GC releases pending nodes and native documents retain no Browser callbacks.

B2 separately tests beforeinput cancellation and reentrant value/type/disabled changes, exact select
and coalesced selectionchange delivery for generated setters and automatic DOM hooks, no script setter
input/change event, user commit timing, and submission hard-wrap versus post-formdata CRLF. Keep the
existing suites green with explicit regression replacements for corrected behavior, update the exact
A1 surface, and record any remaining corpus failures rather than broad exclusions. Benchmark timing
remains gated on idle hardware and comparable complete features; none is authorized by this design.
