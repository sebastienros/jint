# The flat box model: one geometry, recomputed per query

> **Read this when:** You are touching `Jint.Browser/Layout/` — the row rule, the hit test, the flex rows,
> the cascade traversal, the virtual scroll, or the DOM members that expose any of them.
>
> This is the recipe half of a trap stated in
> [`../Runtime/AGENTS.md`](../Runtime/AGENTS.md#the-flat-box-model-and-the-one-number-it-is-built-from)
> beside the page runtime, which carries the thread rule every one of these queries runs under. Read that
> first — nothing here is repeated there, and the repository-root [`AGENTS.md`](../../AGENTS.md) carries
> the build and test commands and the branch to target.

### The row rule, and everything derived from it

`Layout/FlatLayout` is the whole of what stands in for a layout engine, and its rule is one sentence: **every
rendered element gets an ordinal in tree order and owns the row `[i·R, (i+1)·R)`, with `R = 16`**. Its box
starts at that row and is `R × (1 + rendered descendants)` tall and the viewport wide, so boxes nest exactly
as the tree does and never straddle. Design doc §8 is the statement of intent; this is what was built.

**One model answers both sides, and that is the point.** `Element.getBoundingClientRect`,
`document.elementFromPoint`, `DOM.getBoxModel`, `DOM.getContentQuads`, `DOM.getNodeForLocation` and
`Input.dispatchMouseEvent(x, y)` are all this class, so a client that reads a box, clicks its centre and asks
what was hit is told one consistent story rather than three approximations that disagree. Three consequences
fall out of the row rule and every one of them is load-bearing:

- **The hit test is a division.** The deepest box containing a point is always the owner of the row the point
  falls in, because a descendant's rows all come after its ancestor's first one. So the centre of a leaf hits
  the leaf, the centre of a container hits a descendant — as a browser does — and the click bubbles back up.
- **The rendered set is HTML's minus what a rendering would have needed.** `<head>` and its subtree, a
  `<script>`, `<style>`, `<template>` or `<noscript>` wherever it sits, and whatever R7's `ElementVisibility`
  calls not rendered — the `hidden` content attribute, `display: none`, `visibility: hidden|collapse` from the
  cascade. `aria-hidden` deliberately does **not** remove a box, which is why the question asked is
  `RenderingReasonFor` and not `ReasonFor`. An element with no box answers zeros, no client rectangles, and
  `-32000` rather than a box of zeros over the protocol: a client reads zeros as a real box at the origin.
- **An excluded element takes its subtree with it**, which is right for `display: none` and wrong for
  `visibility: hidden`, whose `visibility: visible` descendant CSS lets escape. A model whose boxes are rows
  cannot give a descendant a row inside a parent that has none, and the nesting is what the hit test rests on.

**Single-line horizontal flex rows partition their containing width.** `Layout/FlexRow` reads AngleSharp's
computed display, direction, basis, growth, shrinkage and cross-axis alignment. The synthetic intrinsic
size is still a row, not measured text; wrapping, gaps, margins, min/max sizes, main-axis justification, ordering and positioned
layout remain unmodeled. A row shares vertical space instead of stacking full-width controls, so a trailing
button no longer owns its parent's centre. DOM rectangles, hit testing, offsets and resize measurements
use the same boxes. Documents without these rows keep the existing ordinal hit-test path.

**It is recomputed per query and never cached across queries.** A cache needs an invalidation signal, and the only one
available is an AngleSharp `MutationObserver` over the whole document — which would make every DOM mutation on
every page pay for mutation records whether or not anything ever asks for a box. Within that synchronous
walk, `CssCascade.Traversal` shares the style collection and raw parent cascades: calling
`ComputeCurrentStyle` separately for every element rematches every ancestor, which made a nested admin form
expensive at every step of Playwright's actionability checks. AngleSharp still owns matching, specificity,
inheritance and value computation. Individual style queries use Css 1.1.0's native computed-style API,
including its cycle-safe custom-property resolution. **The traversal still needs `Dom/Views/CustomProperties`
(#3851)**: the native computed-parent overload is internal, and calling the public entry per element would
rematch every ancestor. The public bulk renderer instead eagerly recurses through the whole document and
cannot accept this traversal's style collection or isolate per-element failures. Raw ordinary declarations
preserve the existing child-relative lengths; custom properties inherit resolved values. Invalid inherited
consumers use the parent's computed value rather than the native initial fallback. Unresolved explicit
`inherit` retains the ancestor-walk compatibility path. Nothing survives the query, so same-turn CSSOM
writes, `classList`, control state and media changes need no invalidation.

**The scroll is virtual, and it is the only state.** `Layout/PageLayout` holds a `scrollY` clamped to the
document, and every viewport-relative answer subtracts it; `scrollX` stays zero because horizontal overflow
has no scroll range in this model. `window.scrollTo`/`scrollBy`/`scroll`, `element.scrollIntoView`,
`DOM.scrollIntoViewIfNeeded` and a wheel event all set it, and `window.scrollY`, `pageYOffset` and
`document.scrollingElement.scrollTop` read it. That is what lets a client whose click path insists on "scroll
it into view, then check the box is inside the viewport" — Playwright's does — succeed on a long page. A
change queues one `scroll` at the document per turn, on the engine's own queue.

**Only the scrolling element scrolls**: `scrollTop` on `document.scrollingElement` is the page's offset and
writing it moves the page; on anything else it reads zero and a write is ignored. `scrollIntoView` aligns
the **whole bounding box**, not only its first row: exposing only that row can leave every actionable
descendant outside the viewport. `nearest` leaves a box spanning both viewport edges in place.

**The DOM-side members are `overrides.json` `additions` entries**, with their bodies in `Layout/LayoutMembers`
— `getBoundingClientRect`, `getClientRects`, the `client*`/`scroll*` metrics, `scrollIntoView`, `HTMLElement`'s
`offset*` family, `document.elementFromPoint`/`elementsFromPoint`/`scrollingElement`. **Never hand-edit a
`.g.cs`**; regenerate with `JINT_DOM_BINDINGS=update`. A rectangle is a plain object shaped like `DOMRect`
rather than an instance of one (`Layout/DomRects` says why), and `IntersectionObserver` and `ResizeObserver`
entries now carry real numbers through the same factory — which is what
[`../AGENTS.md`](../AGENTS.md)'s observer section promised when they were zeros. `Range.getBoundingClientRect`
stays zeros: this model gives an *element* a row, and a range is a pair of positions inside text nothing here
measures.
