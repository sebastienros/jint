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
