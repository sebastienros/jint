# D7b2 implementation ownership and integration handoff

Reviewed common native source at b8329b51f7e7d82953b4f99cf05a1f7a3326cd0d and docs/design/html-parser-checkable-state.md. No production changes or performance measurements.

## Verdict

The substantive native design is ready. The linked parent design is now approved with this ownership amendment; implementation must use the current D6/H8 ownership split below. Do not dispatch the old document literally as authority to edit every shared file. Native component completion is useful independently of the full input facade, but does not complete Browser input semantics or authorize a public partial Input API.

Living HTML checks support the three flags, checked attribute presence, clone/reset behavior, exact-name ordinary-root/form-owner grouping, equal checked/name assignment exclusion, and connected insertion triggers. The disconnected multiple-checked case must remain representable. Current-type canceled activation and dispatch-local records remain required. Sources: https://html.spec.whatwg.org/multipage/input.html#the-input-element and https://html.spec.whatwg.org/multipage/infrastructure.html#becomes-connected .

The root-wide explicit-form reset and intermediate owner-null requirement remain supported by https://html.spec.whatwg.org/multipage/form-control-infrastructure.html#association-of-controls-and-forms . Preserve the unrelated div ID regression in the design; final-owner equality is not a substitute for the specified owner stores.

**Ownership released on continuation from `0b9b921de`:** D6r6 and H8 are reviewed, integrated,
validated and archived. The new D7b2 implementation owner has the explicit transfer of Element.cs,
HtmlElementState.cs and NodeCloner.cs for its concrete hooks, along with the released native lifecycle
files below. No separate H8 hook implementation is needed. Preserve all merged D6/H8 behavior.

## Exact ownership and integration sequence

1. Wait for D6r6 exact-source integration and explicit release of Node.cs/Document.cs/CharacterNodes.cs. Use that common commit as the worker base; preserve notification scopes and weak-registration/normalization corrections.
2. Dedicated D7b2 owner: new Dom/Html/HtmlInputCheckedState.cs, HtmlCheckableState.cs, HtmlRadioGroupIndex.cs, HtmlCheckednessAlgorithms.cs, HtmlInputStateChanges.cs and bounded work probe; native tests. Reserve Dom/Html/HtmlFormAssociation.cs, HtmlFormIndex.cs and HtmlFormState.cs as needed; Dom/Attr.cs; released Dom/Node.cs and Document.cs only where required. Avoid CharacterNodes.cs unless a demonstrated hook needs it. Do not edit Browser or HTML/XML parser algorithms.
3. H8 retains HtmlElementState.cs, Element.cs and NodeCloner.cs. D7 owner supplies the concrete calls/ordering below; H8 installs them in one narrow, separately reviewed hook commit against the real component code. Alternatively the coordinator explicitly transfers those three files after H8 checkpoints. No simultaneous edits, no second CWT/state store, no inert stub hook to make common compile.
4. Keep dependency commits isolated until the complete component and hooks compile and pass together. The user's intermediate uncompilable-work permission is not a common integration gate waiver.

H8-owned hooks:
- HtmlElementState: one lazy CheckedState component and nonallocating ExistingCheckedState access. Preserve textarea/script fields. Every actual HTML input can retain the flags, regardless of type; never infer current state from checked presence after initialization.
- Element: send namespace, local name, before/after presence and operation identity from append/remove/replace and parser merge; attached Attr.Value uses the same coordinator. A replacement of a present checked Attr is not removal plus addition. Same-object SetAttributeNode remains the DOM no-op.
- Element.InitializeParsedAttributes: finalize input initialization only after the entire duplicate-free batch is published, with no intermediate attribute-order-dependent exclusion. Handle empty batches and XML effective/defaulted attributes. Do not repeat ordinary per-attribute initialization during clone.
- NodeCloner.CopySingle: after CopyAttributesFrom and before child insertion, copy all checked-state flags from the original. Preserve IsValue, textarea and script clone behavior. No source group/index handle may be copied.
- Parser code already initializes attributes before HtmlFormAssociation.AssociateFromParser and native insertion. H8 verifies this ordering rather than creating parser-only checkable state.

## Concrete current-code corrections required

HtmlFormAssociation.ResetOwner currently assigns state.Owner directly three times; AssociateFromParser assigns directly once. Centralize stores and notify only actual old/new identity changes, including the specified null intermediate store. ParserInserted remains part of the existing owner algorithm, not radio membership logic.

AttributeChanged currently returns for oldValue == newValue. Equal name/form sets need their actual algorithm triggers, while equal ID writes must be assessed as ID changes rather than blindly treated as changes. Separate change detection from operation classification.

HtmlFormIndex currently indexes only nonempty explicit form values and exposes Referencing(id) from a HashSet. Add complete listed-control/form-presence candidate enumeration, including empty values; reset all required candidates for qualifying ID changes/ID-bearing insertion/removal. Preserve deterministic specified operation order. Do not iterate HashSet order where it can choose a checked winner.

HtmlFormAssociation.ShadowIncludingAssociated currently pushes a shadow root before pushing light children, so the LIFO stack visits light children first. Before sharing it for checkable insertion steps, use actual shadow-including preorder (host, shadow subtree, then ordinary descendants), or supply a separate correctly ordered traversal within the same lifecycle pass. Template contents do not become connected with their host.

Node removal/insertion hooks must keep roots and membership correct even when no form association changes, including detached moves and attached shadow roots. The present form early-exit conditions are not automatically sufficient for radio rekeying. Retire an old detached-root index and clear direct handles before it can retain moved members.

## Performance and cancellation boundaries

Retain the design's root-owned incremental index, member handles, checked-member set and required count. Hot facts must avoid ancestor climbs, name rehashes, snapshots and MutationStamp-wide rebuilds. Selection visits currently checked peers only. Include long-name hashing/equality in bounded preparation work; a built-in ordinal dictionary's uninterruptible hash of a huge name cannot itself establish a 256-unit cancellation guarantee. No query repairs duplicate checked flags.

ID-triggered F-candidate resets have their own required cost. Do not claim O(affected subtree) for them. Charge owner resolution and transient group moves honestly. Prepare allocations/cancellation before exclusion and publish consistent flag/index/revision changes; native parser/mutation callbacks must not throw halfway through peer updates.

## Consumer handoff and gates

Browser currently reads AngleSharp IsChecked/IsDefaultChecked/IsIndeterminate in generated HTMLInputElement accessors; ActivationBehaviors uses wrapper-keyed snapshots and peer IDL writes; FormSubmitter reads IsChecked; PagePseudoClassSelectorFactory has case-insensitive radio-name matching. Port these in the Browser owner's work to this single store, never copy flags into a Browser cache.

Clarify the design's final B2/B3 row: Browser may wire the completed internal checked-state slice via its existing native IVT grant before an entire Input facade is public. Full cutover acceptance still waits for real value/reset/type families, event behavior and all consumers; no fallback/default stub or partial-public Input promise. Full reset must call the checkedness step in its specified place, not present it as the whole input reset.

Native gates: all flag/attribute/type/namespace matrices from the existing design; independent slow group oracle over mutations; exact unrelated-ID f→null→f case; multi-checked detached/connection ordering; parser batch/XML defaults; clone/import/adopt/template/shadow behavior; canceled preparation/committed insertion coherence; deterministic dense-group/distinct-name/repeated-append/ID-reset work; retained-old-document weak lifetime. Fresh Release net8/net10 focused and broad non-corpus tests.

WPT repository pin is 6c7127bdd9f2cc6a3668fd9791757843e09d5a9e (Jint.Tests/Wpt/Vendor/README.md). radio.html/checkbox.html referenced by the design are not locally vendored at their upstream paths; indeterminate-radio.html is vendored. Resolve source tests at that exact pin and record differences from living HTML before counting evidence. Authored native fixtures are not WPT passes. Browser activation/disabled/trusted-vs-synthetic/nested-dispatch acceptance is a separate owner gate.

### Reset-candidate ordering refinement

A root may cache an immutable tree-ordered reset-candidate array. Membership changes and moves of
candidates invalidate it; unrelated ID/name/value writes and unrelated subtree changes do not. A dirty
array can be rebuilt by one charged ordinary preorder, O(N + F), instead of repeated tree comparisons.
Repeated ID-triggered resets then enumerate O(F) cached candidates plus required owner/group work.
Drop an invalidated array immediately so removed controls are not retained. Do not use a document-wide
mutation stamp as the invalidation key. Verify fixed F with increasing unrelated N, candidate moves,
equal reference writes, stable snapshots and removed-control lifetime. This is an explicit ordering
rebuild cost, not permission to scan every unrelated node for every ID write.
