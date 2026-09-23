# D7 form association, disabledness and control-family dispatch

Preparatory proposal for independent review, 2026-09-23. No implementation is dispatched by this
document. The active shadow owner currently reserves shared native files; coordinate a later handoff.
This implements the intrinsic-state boundary in [the architecture](html-parser.md#intrinsic-html-state-belongs-to-the-native-document),
using [D3 ownership](html-parser-native-followups.md), [D5 mutation boundaries](html-parser-mutations.md),
[D6 shadow relations](html-parser-shadow.md), and [C3 native predicates](html-parser-selectors.md).
Standalone native state is authoritative. Browser supplies interaction, events, registry definitions,
resources and scheduling; it must not retain a second form-owner, checkedness or validity store.

## 1. Source and consumer inventory

Primary rules checked against HTML Living Standard dated 2026-09-22:
[form categories](https://html.spec.whatwg.org/multipage/forms.html#categories),
[association/reset](https://html.spec.whatwg.org/multipage/form-control-infrastructure.html#association-of-controls-and-forms),
[HTML insertion/removal/moving steps](https://html.spec.whatwg.org/multipage/infrastructure.html#html-element-insertion-steps),
[parser association](https://html.spec.whatwg.org/multipage/parsing.html#create-an-element-for-the-token),
[disabled controls](https://html.spec.whatwg.org/multipage/form-control-infrastructure.html#enabling-and-disabling-form-controls:-the-disabled-attribute),
[fieldset](https://html.spec.whatwg.org/multipage/form-elements.html#the-fieldset-element),
[option disabledness](https://html.spec.whatwg.org/multipage/form-elements.html#concept-option-disabled),
[nearest select](https://html.spec.whatwg.org/multipage/form-elements.html#get-the-nearest-ancestor-select),
and [HTML selector states](https://html.spec.whatwg.org/multipage/semantics-other.html#pseudo-classes).
The older AngleSharp surface and Browser workarounds are evidence of required consumers, not substitute
algorithms. Current HTML's option/optgroup rules must not be reduced to older immediate-parent rules.

The [A1 manifest](../../tools/html-parser-inventory/inventory.lock.json) remains the exhaustive member
inventory. This dispatch groups its rows and actual handwritten consumers as follows:

| Consumer | Required shared native facts; retained Browser responsibility |
| --- | --- |
| `Dom/HtmlFormOwner.cs` and generated form IDL attributes | One owner identity, complete ordered membership, external associations, parser associations; borrowed label/legend/option form getters are separate algorithms |
| `Events/FormSubmission.cs`, `Runtime/FormSubmitter.cs`, `Runtime/FormDataConstruction.cs` | Same owners for submitter validation, candidate collection, successful controls and reset; Browser fires submit/reset/invalid/formdata and handles navigation, encodings and files |
| `Events/ActivationBehaviors.cs`, `InputDispatcher.cs`, `TextEditing.cs`, `FocusController.cs` | Native disabledness, radio groups, default button, mutability and value changes; Browser chooses user action, pre-activation rollback and event timing |
| `Runtime/Parsing/PagePseudoClassSelectorFactory.cs` | Native enabled/disabled, checked/unchecked/indeterminate/default, required/optional, validity/range, editing, placeholder and open applicability; environment focus/hover/active/target/visited remains the C3 adapter |
| `Dom/HtmlSelectState.cs`, `HtmlLabelAssociation.cs`, `HtmlDirectionality.cs` | Shared select membership/selectedness, label association, directionality; no second Browser implementation after migration |
| `Events/EventHandlerContentAttributes.cs` | Owner identity for handler scope chains; actual compiled handlers and realms stay Browser |
| `Accessibility/AccessibilityTree.cs` and extraction | Native disabled/checked/selected/value/label facts; ARIA semantics and extraction presentation retain their existing owners |
| `CustomElements/CustomElementRegistry.*`, `CustomElementRecord.cs` | Definition's form-associated fact and native ElementInternals data; Browser owns constructors and reaction queues. Current code records the fact but does not provide complete ElementInternals or entries |
| `Dom/Files/**`, `Runtime/Media/**`, input/selection adapters | Native control state with native file identities/metadata when implemented; JS File wrappers, transport and selected image coordinates remain their Browser owners |

Existing regression basis is `Jint.Tests.Browser/Forms/FormOwnerTests.cs`, `FormTests.cs`,
`FormEntryTests.cs`, `FormDataConstructorTests.cs`, `ImageSubmitTests.cs`, `DirectionNameTests.cs`,
`Dom/LabelAssociationTests.cs`, `Dom/OptionSelectednessTests.cs`, `DomSelectTests.cs`,
`Parsing/PagePseudoClassSelectorTests.cs`, `Events/EditingTests.cs`, `ActivationBehaviorTests.cs`,
`PageInputTests.cs`, `SelectionChangeTests.cs`, and `Files/FileInputSelectionTests.cs`.
The pinned local event-handler lexical-scope WPT also exercises form ownership. Full upstream forms
directories are not all present in the current local WPT selection; adding their pinned cases is
conformance work, not evidence that an absent directory already passed.

## 2. First bounded task: D7a native built-in association and disabledness

One Sol owner implements two sequential commits: **D7a1 association**, then **D7a2 disabledness**.
Use implementation files `Jint.HtmlParser/Dom/Html/HtmlElementState.cs`, `HtmlFormState.cs`,
`HtmlFormAssociation.cs`, `HtmlFormIndex.cs`, `HtmlDisabledness.cs`, `HtmlSelectAncestry.cs`, and focused
`Jint.Tests.HtmlParser/Html/FormAssociationTests.cs`, `FormCollectionTests.cs`, `DisablednessTests.cs`.
Reserve the existing Element/Node/Document/Attr/NodeCloner mutation integration files after D6's owner
releases them. Do not edit Browser, parser insertion modes, signing, A1 locks or public snapshots in
this first task. The coordinator owns those subsequent integrations. No public stubs are introduced.

Keep Element sealed. Namespace is `Jint.HtmlParser`, implementation folder `Dom/Html/`. The view is
lazy and stable per element, with the native element as its only owner; control-specific data is
allocated only for applicable kinds. Exact initial signatures:
The view's GetDisabledState member and disabled/select types below land in D7a2; D7a1 does not add a
stub for them. All association members land as working algorithms in D7a1.

```csharp
// Element: null outside the HTML namespace; no case folding of the stored local name.
internal HtmlElementState? GetHtmlState();

internal sealed class HtmlElementState
{
    internal Element Element { get; }
    internal Element? FormOwner { get; }
    internal HtmlDisabledState GetDisabledState(CancellationToken cancellationToken);
}
internal enum HtmlDisabledState { Inapplicable, Enabled, Disabled }

internal static class HtmlFormState
{
    internal static bool IsFormAssociated(Element element);
    internal static bool IsListed(Element element);
    internal static Element? GetOwner(Element element);
    internal static IReadOnlyList<Element> SnapshotAssociatedElements(Element form,
        CancellationToken cancellationToken);
    internal static IReadOnlyList<Element> SnapshotFormControls(Element form,
        CancellationToken cancellationToken);
    internal static IReadOnlyList<Element> SnapshotFieldsetControls(Element fieldset,
        CancellationToken cancellationToken);
    internal static void ResetOwner(Element element);
    internal static void AssociateFromParser(Element element, Element form);
}
internal static class HtmlDisabledness
{
    internal static HtmlDisabledState GetState(Element element,
        CancellationToken cancellationToken);
    internal static bool IsOptionDisabled(Element option, CancellationToken cancellationToken);
}
internal static class HtmlSelectAncestry
{
    internal static Element? GetNearestSelect(Element element,
        CancellationToken cancellationToken);
}
```

No new public constructor or setter for FormOwner exists. GetHtmlState's view reads the same storage
as the static algorithms; allocating the view cannot initialize a different association. The view
may exist for an ordinary HTML div with FormOwner null/disabled Inapplicable; it does not contain
placeholder Validity/Value members. Static predicates/owner reads on unrelated elements allocate no
state. Later families add only implemented operations to this view before reviewed public promotion.

Null required arguments throw ArgumentNullException. Snapshot receiver must be an HTML-namespace
lowercase `form`/`fieldset`, and IsOptionDisabled requires such an `option`; otherwise ArgumentException.
GetOwner returns null and category predicates false for non-associated elements. ResetOwner and
AssociateFromParser reject non-associated elements with InvalidOperationException; the latter also
requires an HTML form, a fresh unattached built-in element, and matching node documents. The parser
must prove the additional token/context conditions below; the native seam does not invent options
for bypassing them. None of these functions runs Browser code.

Built-in association categories are exact HTML-namespace/local-name matches, regardless of whether
the owner document is HTML or XML: button, fieldset, input, object, output, select and textarea are
listed and form-associated; img is form-associated but not listed. Option, optgroup, label and legend
are not themselves form-associated. SVG/no-namespace/uppercase XML lookalikes do not acquire HTML
control semantics. Form-associated custom elements join these categories through D7g's real upgrade
state; a hyphenated name or a `form` attribute alone does not make a FACE. D7a makes no FACE-completion
claim and is not sufficient to publish all C3 predicates or replace Browser wholesale.

This proposal selects a named standards correction for obsolete `keygen`: it has ordinary unknown
HTML element semantics, not a new native listed/control state. The existing generated HTMLKeygenElement
surface and the keygen row in FormOwnerTests are explicitly assigned to D7h/B1 compatibility review
and cutover tests; they must not disappear unnoticed. Accepting this design approves that native
classification, not an unreviewed removal of a shipped Browser binding. No compatibility flag is added.

## 3. Stored ownership and exact mutation behavior

An associated element stores a nullable owner and parser-inserted flag, initialized to null/false.
GetOwner is a stored-state read, not a traversal or a lazy reset. Browser's current HtmlFormOwner
recomputes from ancestors on every read; copying that approach loses parser-created associations.

ResetOwner implements the current reset algorithm: unset the parser flag first; retain an existing
nearest-ancestor owner only when the algorithm's no-explicit-listed-form condition permits its early
return; otherwise clear the owner and resolve it. For a connected listed element with a **present**
namespace-less `form` attribute, resolve the first element with the identical nonempty ID in its own
ordinary tree, in tree order, then accept that element only if it is an HTML form. A first non-form
duplicate blocks a later form. Missing target, non-form target and `form=""` yield null without falling
back to ancestors. With no such connected-explicit branch, use the nearest ordinary ancestor form.
Thus detached controls ignore explicit form attributes and can use detached ancestor forms. Being in
a shadow tree does not imply disconnected: connectivity is shadow-including, but ID lookup and form
ancestry stay inside the ordinary tree. Neither slots nor template Host links extend that ancestry.

The fieldset's own form attribute does not associate all its descendants to that form. An img ignores
its own form attribute but can retain the parser's association. Owner membership does not imply that
a control is enabled, submittable, valid, resettable, or present in every form-related collection.

Integrate synchronous semantic hooks at the existing D5 mutation boundaries, including the trusted
parser and clone insertion paths, before subsequent native reads and independent of observer record
suppression. Handle attribute append/change/removal/replacement and attached Attr.Value for `form`
and `id`, plus inserting/removing/reordering IDs, ancestor/subtree insertion/removal, cross-document
adoption and future state-preserving move operations. Use the HTML insertion/removing/moving algorithms
at their exact points; they are not interchangeable with an unconditional reset on every write.
Insertion respects the parser-inserted flag. Removal/moving resets an owned element when it is no
longer in the owner's tree. Form-attribute and relevant ID-triggered reset steps remain independent.
An unrelated class/data/text write must not erase a parser association just because a broad document
stamp changed. Equality/no-op attribute handling follows HTML change steps as well as D5 record rules.

Removing a subtree containing both form and associated control can preserve their association;
extracting the control alone can break it. Ancestor moves must process affected associated descendants,
including D6's required shadow-including lifecycle traversal, while resolving each element in its own
ordinary tree. Replacing all/fragment draining/same-node insertion must retain the specified intermediate
semantics, not infer associations only from an eventually equal final tree. Failed hierarchy validation
leaves owner links, indexes and observer queues unchanged. Same-document adoption performs its normal
removal; a node-document change is not itself an instruction to indiscriminately clear all ownership.

Clone/import copies no source form-owner pointer or parser flag. Fresh copied nodes acquire ownership
through their own insertion semantics, including copied forms and explicit associations in the copy's
tree; source state is untouched. The trusted AppendClonedChild path may avoid redundant validity checks
but cannot bypass these semantic steps. Deep templates retain their independent inert tree; shadow-root
and shallow clonable-host cases follow D6's actual copy order and boundaries.

The native parser association seam stores the supplied form and sets the parser flag; it does not
reset immediately. H6/H8 owns the call from create-an-element-for-the-token only when all current
conditions hold: built-in associated element, non-null parser form pointer, not parsing template
contents, no fragment context, no listed explicit form attribute, and intended parent in the form's
tree. The flag is not cleared merely because initial insertion or EOF occurred. ResetOwner clears it
when the specified reset happens. D7a tests call the trusted seam and native insertion directly;
the later parser integration adds malformed-table/form and fragment corpus tests, with no parser
semantics claim from native-only tests.

## 4. Inventories, indexes and disabledness

SnapshotAssociatedElements returns all associated elements in the form's ordinary tree whose stored
owner is that form, in tree order, including img and input type=image. SnapshotFormControls applies
the form.elements filter: listed elements with that owner, excluding input in the Image Button state.
For this filter, D7a1 recognizes a namespace-less type value ASCII-insensitively equal to `image`;
other/missing values do not select that state. This classification requires no unfinished input-value
or validation subsystem and is replaced by the shared complete type-state helper when D7b lands.
SnapshotFieldsetControls returns listed **descendants of the fieldset**, including image inputs and
controls owned by a different form; it neither includes the fieldset itself nor substitutes owner
membership. All snapshots are immutable lists of stable native references, and later mutation cannot
rewrite a returned list. Live form/fieldset collections and RadioNodeList/named access are D4/B2
consumers of these membership rules; returning a snapshot is not a completed live binding.

Root-scoped ID lookup must include an Element tree root itself where applicable and preserve duplicate
order. Maintain an index of controls with explicit `form` references and appropriate ID/order
invalidation; do not scan the whole document once per control. An optimized narrower affected set is
valid only when it produces the same reset/association results as the normative triggers. Indexes
must not replace stored ownership, leak nodes across roots, or outlive detached trees via a global
strong dictionary. A reverse form membership index may accelerate reads, but results still obey
ordinary tree order and the three separate filters above. Ownership change is not a fabricated DOM
MutationRecord; it invalidates native membership/predicate state before the next read.

GetState is HTML's **actually disabled** classification used by :enabled/:disabled. For button, input,
select and textarea, consider the own disabled attribute and every disabled ancestor fieldset's
first-legend exception. Fieldsets use the same ancestor exception plus their own attribute. Optgroup
and option have their own rules below plus their applicable nearest select's control-disabled state.
All other built-in elements return Inapplicable, even `<div disabled>` or `<a disabled>`; enabled is
not the complement of disabled over every element. D7g adds FACE only when its native definition state
establishes applicability. Disabledness never forbids a programmatic checked/value/selected setter.

The fieldset exception concerns its first HTML legend **element child**, not the first element child
of any name, first descendant legend, or first matching querySelector result. Carry the child on the
ancestor path and compare it with that legend. An exemption under an inner fieldset does not exempt
the candidate under an outer disabled fieldset. Namespaced disabled attributes do not participate;
boolean presence counts even with value `"false"`. The IDL disabled getter remains reflection of the
element's own attribute; it must not be wired to this inherited-state result.

IsOptionDisabled is the option-specific predicate needed by selection/entry-list algorithms, distinct
from GetState. Check its own disabled attribute, then walk ordinary ancestors outward: select, hr,
datalist or option is a stopping barrier; the first optgroup decides by its own disabled attribute;
irrelevant wrappers do not stop the search. It does **not** inherit select disabledness. GetNearestSelect
uses the current separate algorithm: datalist/hr/option stop; a second optgroup stops; otherwise find
the nearest select. GetState(option/optgroup) then adds that select's disabled-control state. These
rules work for manually constructed nonconforming DOM as well as parser output. Browser's current
immediate-parent option test and ad hoc ancestor-select loops are migration regressions to correct.

D7a2 may compute disabledness on demand, but reuse a fieldset's first-legend result with proper direct
child-list invalidation. Do not rescan a long fieldset child list for each queried sibling control.
Insertion/removal/legend reorder, own disabled edits, ancestor moves and adoption invalidate relevant
answers. Ordinary parent walks stop at shadow/template boundaries; an external form owner or slotted
position does not transfer disabledness from another ancestor chain. C3 uses GetState without a
Browser; B2 uses the attribute reflection operation for IDL disabled and GetState for applicability.

## 5. Finite downstream family inventory

These are mandatory follow-on families, not part of D7a's first Sol task. Each receives a reviewed
operation/signature dispatch before implementation, adds state/algorithms to the same native owner,
and has its own parser/native/Browser tests. Public promotion happens only with working behavior and
API review. The grouped surface below includes the A1 generated members and handwritten needs; it
does not authorize deleting other A1 members or accepting false for unfinished predicates.

| Slice | Native algorithms / concrete required surface | Dependents and boundary |
| --- | --- | --- |
| D7b1 input text and textarea | Value/defaultValue, dirty value and user-validity state, type/default-state table, sanitization and textarea newline/API-value rules; value, defaultValue, type, name, autocomplete, dirName, readOnly, required, placeholder, minLength/maxLength, size/cols/rows/wrap/textLength; selectionStart/End/Direction, select/setSelectionRange/setRangeText applicability and native offsets; reset/clone rules | B2 text setters and C3 read-write/placeholder; Browser editing and selectionchange timing remain events, but user edits call native setters carrying the specified edit cause |
| D7b2 checkbox/radio | Checked/defaultChecked, dirty checkedness, indeterminate, group membership (tree, nonempty name, form owner, radio type), mutual exclusion on checked/type/name/owner/tree changes, reset and clone; default submit button/implicit submission inventory includes image buttons | C3 checked/unchecked/indeterminate/default and Browser activation rollback use the same state. Preserve HTML checked independently of checkbox indeterminateness; unchecked has the reviewed group-sensitive rules, not generic `!checked` |
| D7b3 numeric/date/time/range | All current input type states, valueAsNumber/valueAsDate, min/max/step and stepUp/stepDown, default step bases, invalid-value and overflow handling, range sanitization/clamping, required/readonly/type applicability, temporal parsing and conversion; no guessed invariant-culture DateTime shortcut | Native constraints and C3 range predicates; generated surface also includes date/time/month/week/datetime-local. Current states are enumerated in a dispatch table, not reduced to number/range |
| D7b4 file/image and remaining input reflection | files identity/list/reset/value restrictions, accept/multiple, image src/alt/dimensions and submit overrides; input states hidden/text/search/tel/url/email/password/date/month/week/time/datetime-local/number/range/color/checkbox/radio/file/submit/image/reset/button all accounted for across b1-b4 | Native state holds no JsFile/Page. Browser owns bytes/transport/picker/user coordinates and file wrappers; intrinsic file-list state and validity cannot remain a second Browser-only truth |
| D7c select/option/optgroup | options and selectedOptions live membership, selectedIndex/value/length/add/remove; selected/defaultSelected/dirtiness, actual Option constructor argument rules, option text/label/value/index, current nearest-select/list barriers and selectedness/reset algorithms; select multiple/size/display-size/type/required; cached nearest select changes, selectedcontent/fallback button text required by current HTML | B2 collections/indexers/Option construction, C3 checked/unchecked/default/required/valid/open applicability; actual picker visibility is environment. Preserve current customizable-select rules, not only historical direct option children |
| D7d buttons/output/fieldset/object | Button type and submit/reset/command applicability, value/name/formAction/formMethod/formEnctype/formNoValidate/formTarget; output value/defaultValue/value-mode flag/htmlFor/reset, fieldset membership; object form and barred-validation behavior; form action/method/enctype/encoding/target/acceptCharset/autocomplete/noValidate reflection | B2 reflection and form preparation; navigation, command/popup activation and events remain Browser. Object browsing-context/data/resource features retain B4/R3 rather than fake native results |
| D7e labels and collections | label.control/form/htmlFor, control.labels, legend.form (immediate fieldset parent), option.form (current nearest select), input.list, meter/progress labels/value/ranges/position; form/fieldset elements, form.length/index/named access, RadioNodeList.value, past-names-map invalidation | D4 live views plus B2 WebIDL identity/named properties. Do not confuse borrowed form getters with association or descendant fieldset membership with ownership |
| D7f validation and form algorithms | Native candidacy/willValidate, custom validity message, all ValidityState flags: valueMissing/typeMismatch/patternMismatch/tooLong/tooShort/rangeUnderflow/rangeOverflow/stepMismatch/badInput/customError/valid; barred rules and aggregate form/fieldset selector validity, range applicability; native reset and successful-control facts, ordered validation candidates | C3 valid/invalid/in-range/out-of-range/required/optional and B2 getters; Browser checkValidity/reportValidity dispatch invalid events, interactive reporting, reset/submit/formdata and entry construction. No eventful CheckValidity call from selectors; built-in messages require a defined native policy, not a permanently empty placeholder |
| D7g form-associated custom elements | Native resolved form-associated definition bit, owner/disabled changes, ElementInternals form/labels/validity/willValidate/validationMessage, setValidity/setFormValue and form state; upgrade/reassociation and native candidate/entry integration | B3 registry/upgrade and formAssociatedCallback/formDisabledCallback/formResetCallback/formStateRestoreCallback scheduling. No constructor/JsValue in native state; must gate full C3/B2 completion, despite current Browser limitations |
| D7h compatibility ledger | HTMLKeygenElement and requestAutocomplete are obsolete A1 surface; keygen native classification correction above needs exact before/after Browser tests and manifest/API disposition. Existing duplicate formEncType/formEnctype spellings require explicit alias handling | B1 review owns any exposed-surface change; an absent modern standard is not permission for an untracked deletion or blanket fake implementation |
| D7i editing/link/open/language-direction | contenteditable enumerated/inherited state (including plaintext-only), designMode, editing hosts and native mutability; href-based link applicability, details/dialog open state and details grouping, select/input open applicability; lang/xml:lang precedence/inheritance, dir/ltr/rtl/auto, bdi/control directionality and slot-aware direction algorithms | C3 and B2 shared facts; Browser focus/hover/active/target/visited history, picker visibility, modal/top-layer interaction, and actual editing/events remain environment. Pure selectors still work on no-host trees; no environment adapter for intrinsic language, dirty flags or disabledness |

Required/optional, readonly, placeholder and validity apply by control/type state, not attribute
presence on arbitrary nodes. The stylesheet compiler's accepted predicate catalogue is not evidence
that these native predicates are implemented. Form and fieldset validity aggregate different sets;
barred elements match neither valid nor invalid merely because a checkValidity-like API returns false.
Pattern/email/URL/numeric/length validation must use the current HTML-defined grammar and applicability,
with a bounded native implementation where input size is untrusted; it cannot delegate to AngleSharp
or an unbounded regular-expression substitute. Each family defines that work before publication.

## 6. Mutation, cancellation, performance and acceptance gates

Use D5's synchronous semantic boundaries, independently of observers, and D6's actual tree relations.
No user callback, Jint value, engine, global strong document registry, or source parser session is kept
in HtmlElementState. Native mutation/state changes invalidate the owning document's relevant state;
cross-document operations invalidate source and destination. Field/value changes introduced by later
families must invalidate selector answers even when no content attribute changed. Saturated cache
stamps always mean invalid. Returned snapshots may retain their nodes; internal indexes must release
removed roots/associations and not preserve obsolete owners after specified reassociation.

GetOwner and category reads are O(1). Snapshot traversals and disabledness/nearest-select queries take
CancellationToken, check entry and before successful return, and poll at most every 256 work steps,
including parent-link backtracking, ID candidates, list copies and snapshot freezing. A canceled read
publishes no partial snapshot and does not reset owners. Internal mutation operations follow the
existing synchronous DOM contract: validate before changes, complete links/owners/indexes/bookkeeping
coherently, and do not throw cancellation halfway through semantic fixups. Parser callers retain their
normal cancellation and ParseLimits boundaries; no per-control quotas, flags or budget resets appear.

Performance gates count work, not stopwatch thresholds: repeated flat appends/owner reads cannot
rebuild a whole-tree ID map per control; collecting N controls cannot scan all N IDs N times; repeated
disabled queries under one unchanged fieldset cannot each rescan its N children for the first legend.
Use iterative traversal, reuse root-local indexes and existing metadata, and make ordinary unrelated
trees pay only cheap category/presence checks. Deep disabled ancestry may require O(depth) work per
individual query; bulk predicate matching may share traversal context without changing answers.
Tests double input sizes to expose repeated whole-tree/prefix work, and test detached-index lifetime.

D7a1 acceptance is native and no-host: nearest nested forms; external form inside another form;
empty/missing/non-form/duplicate targets and changes to the first duplicate; controls before/after
the form; connected/detached transitions; detached element roots; IDs and explicit references within
open/closed roots and outside them; inert template boundaries; img category; fieldset association not
inherited; same/cross-document moves and clone/import; all three snapshot memberships/order and
immutability; SetAttributeNode/Attr.Value/remove/replace/same-value distinctions; hierarchy failure
atomicity. Parser-seam tests prove non-ancestor association, unrelated mutation retention, insertion
flag behavior, removal retaining same-tree owners and subsequent real resets. Parser integration is
a separately reviewed H6/H8 commit with the documented fragment/template exclusions.

D7a2 acceptance covers every applicable built-in and unrelated HTML/SVG/XML lookalikes; disabled
presence values; first direct legend after non-legend siblings; nested legends that are not direct
children; two legends reordered/removed; inner exemption plus outer disabling; disabled fieldset
inside a first legend; namespace-qualified attributes; slotted light controls versus shadow controls;
external owner versus local ancestors; option wrapped under optgroup, stopping barriers, nested
optgroups and disabled select; option-specific versus selector disabledness; clone/adopt and immediate
post-mutation reads. Add deterministic cancellation during final deep ascent and long snapshot copy.

Release tests run fresh on net8.0 and net10.0 with MTP `dotnet test --project
Jint.Tests.HtmlParser/Jint.Tests.HtmlParser.csproj -c Release --framework <tfm>`. Later C3/B2 gates rerun
the Browser regressions named above against native-backed bindings and compare full required A1
surface and pinned WPT census. D7a completion means only its built-in owner/disabledness contract is
ready; all downstream families, FACE, live binding semantics and parser integration remain named
obligations. Do not publish a general state API or mark full C3/B2 complete by returning null, false,
empty validity or empty collections for a family that has not landed.
