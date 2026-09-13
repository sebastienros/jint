# Layout invalidation contract

Tracking: [#3698](https://github.com/sebastienros/jint/issues/3698).

## Status

The shipped geometry remains query-local. This document specifies the integration needed before a
layout or cascade may survive a query. It does not claim that a cache or a mutation generation exists.

The audit on 2026-09-13 used the pinned AngleSharp 1.8.1 and AngleSharp.Css 1.1.2 assemblies. PRs #3888,
#3908 and #3927 are merged. The former shares placement and cascade work within a query; the latter two
preserve scrolling and first-hit-test event coordinates. None supplies cross-query invalidation.

Upstream has since merged [AngleSharp #1344](https://github.com/AngleSharp/AngleSharp/pull/1344), at
`eb3925b9dfc0731cf249e82b13b7bb19a2022db8`. Its upcoming 1.8.2 adds public `Document.MutationVersion`,
including parser, tree, attribute and character-data writes. Stable 1.8.2 did not resolve during this
audit, but `1.8.2-beta.715` does and its assembly exposes this property. Consume and test that producer;
a duplicate DOM revision implementation is unnecessary.
It explicitly excludes extension-owned stylesheet state and is not a complete layout revision.

Jint PR [#4066](https://github.com/sebastienros/jint/pull/4066) handles the remaining mouse-offset cases
independently: single-element placement before the first listener avoids a complete layout, and no
listener means no measurement. A cache is no longer a prerequisite for that correction.

## What the pinned dependencies expose

`dotnet-inspect member` over the pinned packages establishes these boundaries:

| Surface | Available mechanism | Gap |
| --- | --- | --- |
| `IDocument` / `INode` | Tree and state reads; document readiness event | No public mutation generation or synchronous general mutation callback |
| `Document.Mutations` | Internal `MutationHost`; public `QueueMutation(Document, MutationRecord)` extension | A consumer cannot replace or intercept the host through the public API; queueing records is not an allocation-free invalidation signal |
| `Node.OnParentChanged` | Protected virtual callback | Parent changes alone do not cover attributes, character data, CSSOM or selector state; existing concrete node factories do not become tracked by subclassing one node |
| `IAttributeObserver.NotifyChange` | Configurable attribute observation | Does not cover tree/character changes or all independent rendering state |
| `ICssStyleSheet` | Rules, insertion/removal and ownership | No general revision covering nested rules, declaration setters, media and disabled state |

`Document.Changed` is the DOM `change` event, not a notification for arbitrary edits. A wrapper-only
counter misses direct dependency writes, parser insertions and operations on dependency-owned token
lists or attribute maps. A whole-document `MutationObserver` allocates records and delivers them later;
even flushing records before a read would not cover CSSOM or selector state.

## Required contract

A cache belongs to one `PageRuntime` and must be released with its document. It must be invalidated
**synchronously before the next geometry read**, including a read in the same script, listener, parser
callback or custom-element reaction. The producer must publish a change before re-entrant script can
read geometry. Invalidating only at a microtask checkpoint, task boundary or navigation phase is wrong.

The complete identity has independent inputs:

| Input | Changes that must be observed | Test obligation |
| --- | --- | --- |
| Document/tree generation | Insert, remove, replace, move, adopt, parser construction, shadow/slot changes where the model consumes them | Read, mutate through each supported API, read again in the same turn; old and new documents both stop reusing affected state after adoption |
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

## Integration design

Prefer dependency-owned revisions at the mutation primitives rather than a second DOM store. Consume
the incoming DOM generation after verifying native tree/attribute/text writes, including parser and
token-list fast paths. CSSOM invalidation belongs at the stylesheet/rule/declaration/media setters and
must propagate from nested objects to their owning sheet. Revisions should require no mutation record,
callback allocation or engine reference. A plain document without a layout consumer should pay at most
the documented revision bookkeeping cost.

Jint supplies the environment and browser state revisions on its page loop. A page-local cache may then
retain the native cascade traversal and geometry for a complete identity, clearing them together when
any component changes. Do not retain one traversal across an unclassified host callback. The consumer
must use the same placement algorithm for rectangles, hit tests, offsets and resize measurements.

If supported hooks cannot express the producers, the choices are a dependency change consumed through
a package update or a maintained local dependency integration. Private reflection and a partial wrapper
counter do not establish this contract. Record the chosen ownership and package/build consequences in
the implementation PR before introducing reuse.

## Validation and completion

1. Add dependency tests for each revision producer, including no-op and failed writes, direct CLR
   operations, re-entrant callbacks and cross-document moves.
2. Add consumer tests comparing warmed reads with fresh-query geometry for every row above, on net8.0
   and net10.0 in fresh Release builds. Include independent pages and document replacement.
3. Demonstrate reuse for unchanged reads with operation counts; demonstrate that mutation-only pages
   allocate no layout observer records. Follow `Jint.Benchmark/AGENTS.md` for timing claims.
4. Validate event offsets for synthetic events, later input events, movement/detachment, scrolling,
   callback-object getters and redispatch. Only captured numbers may survive a callback unless the
   full revision contract is active.
5. Update `Runtime/AGENTS.md`, `Layout/box-model.md` and this status only when the implementation and its
   tests justify relaxing query-local lifetimes. Link merged PRs in #3698 before closing it.
