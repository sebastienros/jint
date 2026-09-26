# Browser native cutover: resumed implementation

The full production objective resumed after the preserved checkpoint
`233d0aa6a4a0040f2ac221d7d2850ea30a595ba5`. The historical handoff remains in
`browser-native-cutover-handoff.md`; its stop instruction no longer describes
the active task. The integration source `97a77c148` was merged cleanly into
`codex/browser-native-dom-cutover`. Production remains incomplete and unmerged.

## Connected consumers

- Native collection, dataset, label, token-list and reflected-attribute views
  use the existing wrapper families and cache. Live element traversal follows
  native links without a stack allocation. Token lists are associated with the
  actual element and attribute name, preserving SameObject identity.
- Node insertion/replacement captures source creation-realm associations before
  native ownership changes. Ordinary wrapping remains O(1). Native element
  classification now assigns listing/xmp to HTMLPreElement, basefont/rb/rtc to
  HTMLElement, and keygen to HTMLUnknownElement.
- The event wrapper reads native stored slot assignment, including closed roots
  internally. A noncomposed event stops at the shadow root of its first invocation
  target; slotted light-tree events cross the slot's shadow root to its host.
- CharacterData operations delegate to NativeCharacterData after ordered WebIDL
  conversions. Range and traversal bindings use native DomNodeIdentity, including
  Attr. The duplicate Browser TreeWalker and NodeIterator implementations were
  removed. Native traversal filters remain on the same callback/realm lane.
- Constructors and DOMImplementation manufacture native documents and nodes.
  PageRuntime now holds a native document and publishes URL/referrer metadata on
  that document when it is installed or its address changes.
- XPath uses guarded native cursors and guarded native expression preparation
  under the BCL evaluator. The Browser adapter preserves namespace suppression
  independently of native namespaces. The pinned AngleSharp.XPath 2.0.6 assembly
  was inspected: NamespaceURI is empty with ignoreNamespaces, Prefix is retained,
  element Name is its local name, and MoveToFirstNamespace always returns false.
  XPath work checkpoints check engine constraints, and results retain native
  identities rather than cloned DOM nodes.

## Evidence and remaining work

All generated edits came from the Release binding generator. Generation still
reports 163 interfaces, 12 files, and zero diagnostics. `git diff --check` passed.

The most recent completed fresh Release net8.0 Browser build failed with 1,058
unique diagnostics, primarily generated HTML/DOM members and remaining
AngleSharp consumers. This number is a diagnostic census, not a readiness
measure. The final slot wiring was included in that fresh build. It reported
zero warnings and 1,058 errors. No Browser tests have run, and neither a successful Browser build nor behavioral verification is
claimed for this progress commit.

Concrete dependency needs reported to the coordinator:

- Native input checkedness, indeterminate state, value state, option selectedness,
  select resets and constraint validity, shared by bindings, activation, forms
  and selectors. Native type metadata, form association, disabled state and
  textarea state already exist; there is no blanket-false replacement for the
  missing states.
- Validated mutable CSS declarations, sheets, rules and media state. A separate
  native owner is implementing the registry and CSSOM; Browser adapters remain
  here until ownership is explicitly reassigned.
- Native selector live-query state, separately owned. Radio names must remain
  case-sensitive, language must derive from the document/tree rather than host
  Culture, and interaction ancestry must follow the flat tree.
- H8's reviewed parser session script requests and inserted-input protocol,
  separately owned. Browser owns preparation/execution, fetched queues,
  stylesheet readiness, currentScript and scheduling; native script metadata
  must not be duplicated. ParserDriver and document.write still need migration.
- Native work-check hooks for long Range operations to let Browser check its
  task deadline, in addition to cancellation tokens.
- Indexed native attribute access for NamedNodeMap. AttributeCount plus an
  enumerable would make every indexed access linear; caching strong Attr entries
  in a live view would retain removed attributes.

Other production groups still awaiting migration include CustomElements,
Observers, activation/form/input consumers, selection, accessibility/extraction,
DevTools, layout and frames. None has been replaced by a stub or a copied DOM.
The old packages remain referenced until their actual consumers are eliminated.

The current XPath adapter preserves the previous Browser policy of snapshotting
iterator results. Native rejection of empty Text and XMLNS Attr context nodes
still needs explicit Browser compatibility/spec review and tests; do not silently
map such contexts to another node or fall back to AngleSharp.


## Final wrap-up

At the user's renewed wrap-up request, implementation stopped after production
progress commit `eb9379071bfeee38c9f4fd1b550b5a07634327bf`. The following
notes-only commit records the final fresh build above. No broken production
changes were integrated into common, and no PR was created.

The largest diagnostic groups in that final build were:

| File group | Unique diagnostics |
| --- | ---: |
| Generated HTML shapes | 621 |
| Generated DOM shapes | 185 |
| DomHostHooks | 41 |
| ParserDriver | 21 |
| Generated collection accessors | 14 |
| InputDispatcher | 10 |
| Page.Input | 9 |
| FrameWindows | 9 |

All owned build, generator and inspection commands finished. No active process
needs this checkout. Preserve the branch, chat and worktree for recovery.
Before resuming, establish which separately owned native prerequisites were
reviewed and integrated after `97a77c148`, then merge them into this isolated
branch. Continue the actual production port and tests; this checkpoint is not a
ready-to-integrate cutover.

## Resumed integration: native DOM mutations, collections, selectors and Range reads

The wrap-up above is historical; work resumed after merging common `0b9b921de`,
then the reviewed attribute-index/CSS-source and Range-read APIs through
`2b6cc11f2`.

Production changes in this checkpoint:

- Native ParentNode append/prepend/replaceChildren and ChildNode before/after/
  replaceWith use native mutation bookkeeping. The Node-or-string union preserves
  native Attr identity through ordered fragment construction: a later Attr refusal
  leaves preceding argument nodes moved, rather than preflight-rejecting the union.
- Native Element attribute-node methods, adjacent insertions, reflected id/class/
  slot, child/sibling reads and public assignedSlot. Closed shadow slots are hidden
  only by the public getter; event paths still use the actual native assignment.
- Live SameObject immediate-child HTMLCollection and NamedNodeMap views, using
  native `Element.GetAttributeAt(uint)` and the existing canonical Attr wrapper.
- DOM selector methods now use the actual native compiler/matcher and data-only
  SelectorEnvironment. Focus and pointer seeds come from the actual interaction
  store (`BrowserEventRealm.Of(engine)`), independently of wrapper creation realm.
  Document target identity starts null and is selected at navigation boundaries,
  never on query; ID mutations cannot silently retarget `:target`. Initial load,
  fragment navigation and same-document traversal are wired to the selection step.
  The native traversal charges non-element visits and ascents too. Target selection
  follows HTML's scroll-to-fragment identity rule; ancestor revealing, fragment
  focus and scrolling remain work for the runtime migration.
- All six native readonly Range operations receive the Browser deadline check
  adapter and cancellation token after WebIDL conversions. The stringifier no
  longer supplies a default token. Synchronous content mutations make no new
  cancellation guarantee.

Generator: Release regeneration succeeded with 163 interfaces, 12 output files,
zero generator diagnostics. Fresh Release net8 Browser build failed with **974
unique diagnostics, zero warnings**, in 19.58 seconds. No Browser runtime tests
can run yet. This is an isolated, incomplete production cutover, not an integration
claim. The remaining Range clone-reaction diagnostic is the still-unported custom
registry consumer, not a reason to omit reactions.

Remaining native seam: selector compilation has cancellation polling but no host
work callback; Browser checks host constraints before/after compilation. Matching
and navigation target traversal already poll the host deadline during work.

### Exclusive Browser leaf group available for parent dispatch

Relinquish all implementation files under `Jint.Browser/Accessibility/` and
`Jint.Browser/Extraction/`, plus focused tests under
`Jint.Tests.Browser/Accessibility/` and `Jint.Tests.Browser/Extraction/` (including
those directories' fixture/golden helpers). Those files have no local edits here.
The recipient owns native production conversion within those paths and reports
missing native state instead of inserting false predicates or substitutes.

Do not edit shared DomRealm/wrapper/cache, generated contract or files, Events,
Layout, CustomElements/Observers, Runtime/Parsing or Page navigation, or DevTools
from that leaf group. Parent reviews and merges its commit back into this branch.
The main Browser owner retains those shared seams and the parser scheduler.

## Continued shared-consumer checkpoint

After the previous commit, public assignedSlot was corrected to the bounded native
`FindSlot(..., openOnly: true, hostCheckpoint, cancellation)` query. Event paths
continue to use stored assignment. Navigation target resolution now scans native
attribute indexes and performs polled ordinal comparisons, including attribute
names, rather than hiding an unbounded attribute scan/string comparison after a
bounded node traversal. URL parsing and percent decoding still have only boundary
checks; their existing shared helpers have no work hook. No second decoder was
introduced. Selector compilation retains the separate missing host-work seam.

Additional production ports:

- HTMLScriptElement's force-async getter/setter uses the native script identity
  slot; charset/type/integrity/defer use native reflected attributes and text uses
  native child mutations. No competing script metadata was added to Browser.
- HTML/XML markup getters and Page.ContentAsync use native serializers with host
  work checks and cancellation. XMLSerializer also passes those checks. Remaining
  old DevTools serialization callers must be migrated with their native trackers.
- Selection owns a native DomRange and its native weak change subscription.
  Boundary edits and native endpoint repairs schedule the existing coalesced task;
  notification bookkeeping never invokes a listener from inside native mutation.
  Replacement disconnects the token, and browsing-context disposal clears its sink.
- ARIA explicit references and their IDL projection use native Element identities.
  Accessibility's engine-free reader is `AriaElementReferences.Explicit(Element,
  string)`. `BrowserEventRealm.FocusedElementOf(Document?)` reads the actual focus
  interaction store through a weak document association without creating or
  touching an Engine; there is no second focus-value field for the leaf consumer.

The parent dispatched Accessibility/Extraction from `92732c130` in separate
worktree `68c5`; those implementation and matching test directories remain
untouched here. Shared CssCascade remains unported because actual validated native
CSS declarations/cascade still require implementation; do not return invented
computed values to satisfy that consumer.

Latest fresh Release net8 Browser build: failed, **954 unique diagnostics, zero
warnings**, 1.00 seconds. No Browser tests ran. Generator regeneration succeeded
for the same 163 interfaces and 12 files with zero diagnostics.

Parser prerequisites newly confirmed by actual API inspection: no HTML/XML
context-sensitive fragment entry point is present for markup setters, and H8 host
requests expose neither script start-tag location nor input attribution (request
steps have their default zero offset). These need real producer APIs; a whole-
document parse/reparse or a dummy script line is not a replacement. The scheduler
port remains owned here and incomplete.
