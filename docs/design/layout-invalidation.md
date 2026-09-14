# Layout invalidation contract

Tracking: [#3698](https://github.com/sebastienros/jint/issues/3698).

## Status

`PageLayout` implements Browser-owned invalidation with the existing AngleSharp 1.8.1 and
AngleSharp.Css 1.1.2 packages. It may retain its lazy size query, cascade and complete layout across
unchanged reads when Jint controls the writers. It does not install a layout `MutationObserver`.
Upstream package updates are not required for this implementation.

The public `Page` API normally keeps native DOM/CSSOM objects internal. Generated mutators and the
manual Browser algorithms are therefore usable interception points. The native-write reproducers from
the earlier audit establish a limitation of a general external observer, not an impossibility of
instrumenting the Browser's own calls. Arbitrary host/native integrations retain query-local behavior.

The upstream proposals remain useful for broader native-write coverage:
[AngleSharp #1349](https://github.com/AngleSharp/AngleSharp/pull/1349) and
[AngleSharp.Css #248](https://github.com/AngleSharp/AngleSharp.Css/pull/248) are ready for review with owner
approval. [AngleSharp #1344](https://github.com/AngleSharp/AngleSharp/pull/1344) introduced the Core token;
[#1347](https://github.com/AngleSharp/AngleSharp/pull/1347) separates construction from mutations.
These are optional future producers. Jint still owns parser/resource lifecycle and environment inputs.

The 2026-09-13 dependency audit used the pinned assemblies and a Core `1.8.2-beta.715` probe. In that
probe `ClassList.Add`, inline `GetStyle().SetProperty` and tree removal advanced the incoming DOM counter;
native checkedness, stylesheet declaration writes and rule insertion did not. The counter alone could
not authorize reuse. No package pin changes accompany the Browser-owned implementation.

PRs #3888, #3908, #3927 and #4066 are merged. Query-local geometry and captured mouse offsets remain the
fallback and the event contract. A mouse event retains captured numbers, not a live cache entry.

## What the pinned dependencies expose

`dotnet-inspect member` over the pinned packages establishes these boundaries:

| Surface | Available mechanism | Gap |
| --- | --- | --- |
| `IDocument` / `INode` | Tree and state reads; document readiness event | No public mutation generation or synchronous general mutation callback |
| `Document.Mutations` | Internal `MutationHost`; public `QueueMutation(Document, MutationRecord)` extension | A consumer cannot replace or intercept the host through the public API; queueing records is not an allocation-free invalidation signal |
| `Node.OnParentChanged` | Protected virtual callback | Parent changes alone do not cover attributes, character data, CSSOM or selector state; existing concrete node factories do not become tracked by subclassing one node |
| `IAttributeObserver.NotifyChange` | Configurable attribute observation | Does not cover tree/character changes or all independent rendering state |
| `ICssStyleSheet` | Rules, insertion/removal and ownership | No general revision covering nested rules, declaration setters, media and disabled state |

`Document.Changed` is the DOM `change` event, not a notification for arbitrary edits. A whole-document
observer allocates records and cannot report all CSSOM or selector-state changes even with synchronous
draining. A complete local integration must also cover nested wrappers (token lists, attributes,
declarations and rule lists), manual native algorithms, parser boundaries and host customization.

## Required contract

A cache belongs to one `PageRuntime` and must be released with its document. It must be invalidated
**synchronously before the next geometry read**, including a read in the same script, listener, parser
callback or custom-element reaction. The producer must publish a change before re-entrant script can
read geometry. Invalidating only at a microtask checkpoint, task boundary or navigation phase is wrong.

The complete identity has independent inputs:

| Input | Changes that must be observed | Test obligation |
| --- | --- | --- |
| Document/tree generation | Insert, remove, replace, move, adopt, shadow/slot changes where the model consumes them | Read, mutate through each supported API, read again in the same turn; old and new documents both stop reusing affected state after adoption |
| Parser/resource lifecycle | HTML construction and resumption, `document.open` / `document.write`, CSS parsing, imported-sheet load completion | A script can read geometry, yield to construction, and read a different tree with the same dependency version; prevent reuse across that boundary |
| Attribute/text generation | Qualified and namespaced attributes, attribute nodes/maps, token-list writes, character data, text replacement | Include `classList`, `setAttributeNS`, `Attr.value`, `textContent` and direct dependency operations |
| Stylesheet generation | Sheet attachment/removal, load completion, nested rule insertion/removal, selector changes, declaration/CSS text changes, media lists, disabled sheets | Warm geometry, change CSSOM without changing DOM, immediately read fresh geometry |
| Environment generation | Viewport, media type/preferences, render-device inputs, document URL where matching depends on it | Same-turn resize/media changes and fragment navigation invalidate matching |
| Selector-state generation | Focus, hover/active, checkedness/selectedness, validity and any other non-attribute state the native matcher consumes | Change state without an attribute mutation and compare to a fresh query |
| Scroll | Scroll position and clamp after content shrinkage | Share document-space boxes only if viewport projection and scroll clamping stay current |

A generation must never wrap into a still-live identity. Saturation must disable reuse or replace the
identity and drop retained entries. Equality is permission to reuse only when **every** input is covered;
unknown state must fall back to a fresh query. No hash of DOM serialization is a substitute: it is linear,
can collide and omits rendering state.

The incoming DOM counter is a signed 64-bit increment, not a saturating revision. Its contract permits
extra increments and gives step size no meaning. The integration must not assume one increment per API
call, and must define its behavior at rollover rather than treating the counter as an ordered timestamp.

## Browser-owned integration

`PageLayout.BeginMutation()` returns an allocation-free scope. The outermost scope invalidates on entry
and exit, including exceptional exits. Geometry queried while a scope is open is always fresh and is not
retained. This covers argument conversions, custom-element reactions, event callbacks and operations
that change some state before throwing. Extra invalidation for no-ops is allowed. `Version` is an opaque,
saturating page-local revision; reaching its limit disables reuse instead of wrapping.

The binding generator emits `DomFailures.GuardMutation` for every setter and for operations outside an
explicit list of reads. Unknown/new operations default to invalidation. Both guard variants share the
existing exception translation and preserve shaped prototypes. Hand-written reflected/ARIA setters,
named-property writes/deletes, file-input synchronization, selection deletion, input editing, activation
and its rollback, form reset and protocol DOM edits enter the same scope. A new native write must do so
as well. Nested wrappers are covered by their own mutating members; DOM expandos are ordinary JS state.

The page checks document identity, media environment, native document URL, focus and pointer-press
state before reusing anything. Replacing the document releases retained work immediately. Sizes and
placement stay in document coordinates; scroll projection and content-shrink clamping remain current.
A complete layout can share the retained size query and cascade, but never causes a single-rectangle
request to eagerly lay out unrelated descendants.

The following paths deliberately keep the existing fresh-query behavior:

- HTML construction, including parser callbacks and resumptions. The whole initial load is suspended;
  generated markup setters and document rewrites enter mutation scopes too. There are no per-node parser
  revision updates, and no revision is reset after construction.
- Any `BrowserOptions.ConfigureEngine` registration. It can install native writers, converters or services
  that bypass Browser bindings. Even a customization that happens to be read-only takes this conservative
  fallback; no public host contract is restricted or replaced by an implicit invalidation obligation.
- `Page.RunOnLoopAsync` callbacks. This internal escape hatch can run arbitrary native writes, so its whole
  callback is suspended. Public `Page.EvaluateAsync` goes through the tracked script bindings instead.
- A document for which the driver starts loading an external stylesheet or frame. Native asynchronous
  attachment is not fully intercepted, so reuse stays disabled for that document, including after load.
- Stylesheets containing `@import`. Imports are detected once per invalidated revision by walking rule
  lists; unchanged cacheable reads do not rescan rules or serialize the DOM. Adding an import invalidates
  the previous eligibility decision. Completion of an imported sheet cannot make the fallback stale.
- Active CSS rule-usage coverage for this document. Recomputing preserves coverage observations, including
  when tracking starts after geometry has already been warmed. Other documents' trackers do not disable it.

These are performance fallbacks, not refusals of functionality. They can be narrowed when an explicit
boundary and regression test prove the relevant native writes are observable. Private reflection, a
second DOM store and local forks of AngleSharp are unnecessary.

The original native-call audit remains relevant to a host that wants unrestricted native writes plus
persistent caching. Such a host needs native dependency versions or a cooperative mutation boundary;
that stronger contract is not silently assumed for the default Browser API.

## Validation and completion

1. Exercise every Browser-owned producer, including no-op and failed writes, native algorithms,
   reentrant callbacks, nested CSSOM and cross-document moves. Verify parsing and unknown native writers
   keep fresh-query behavior. Optional upstream producers need their own dependency tests.
2. Add consumer tests comparing warmed reads with fresh-query geometry for every row above, on net8.0
   and net10.0 in fresh Release builds. Include independent pages and document replacement.
3. Demonstrate reuse for unchanged reads with operation counts; demonstrate that mutation-only pages
   allocate no layout observer records. Follow `Jint.Benchmark/AGENTS.md` for timing claims.
4. Validate event offsets for synthetic events, later input events, movement/detachment, scrolling,
   callback-object getters and redispatch. Only captured numbers may survive a callback unless the
   full revision contract is active.
5. Update `Runtime/AGENTS.md`, `Layout/box-model.md` and this status only when the implementation and its
   tests justify relaxing query-local lifetimes. Link merged PRs in #3698 before closing it.
