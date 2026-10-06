# Agent instructions: accessibility and extraction

> **Read this when:** You are touching `Jint.Browser/Accessibility/` or `Jint.Browser/Extraction/` — the
> accessibility tree, roles and names, or the text and markdown a page is read as.
>
> This is one of the co-located instruction files indexed from the repository-root [`AGENTS.md`](../../AGENTS.md).
> Read that first, then [`Jint.Browser/AGENTS.md`](../AGENTS.md) for the package's principle. Nothing below is
> repeated in either.

### Renderless CSS input

The CSS engine intentionally stores text, not typed computed values. Extraction reads explicit
white-space-collapse text or interprets white-space keywords for pre/pre-wrap/pre-line behavior;
do not rebuild the removed typography grammar. Visibility still uses the shared text cascade.
The native DOM and text cascade are described in
[the Browser instructions](../AGENTS.md#where-the-cascade-diverges-from-cssom).

### Accessibility and extraction have no layout

`Accessibility/` computes an accessibility tree over the native DOM and `Extraction/` renders the same
document as text or CommonMark. Both are pure C# over `Jint.HtmlParser.Document`/`Element`; neither touches an engine, and
that is why they were built before the page runtime existed. **Two helpers under `Dom/` are read from here and
neither breaks that**, because both take an `Element` and nothing else: `Dom/Views/CssCascade`, and
`Dom/AriaElementReferences`, which is the *engine-free half* of ARIA's element reflection — a relationship a
page made with `el.ariaLabelledByElements` writes the empty string to the content attribute and holds the
elements by reference, so an accessible name computed from the attribute alone would miss exactly the case a
page went out of its way to express. What that file must never grow is a realm: `DomRealm.Of(engine)` creates
one if there is none, so asking the binding proper would let a name computation **construct a realm**, and
`Jint.Tests.Browser/Accessibility/PageFixture` has no engine on purpose. The consumers are the CDP `Accessibility` domain,
the custom `Jint.getMarkdown`/`getText`/`getAccessibilitySnapshot` domain, and the MCP server's `snapshot`.

**Three things a browser answers from its layout tree are answered from somewhere else, and every one is a
place where this can be wrong.**

- **Hidden** is `ElementVisibility`: the `hidden` content attribute, `aria-hidden="true"`, and `display:none`
  / `visibility:hidden|collapse` from the shared native text cascade, with an inline-style fallback
  when the query cannot answer. It cannot know that an element is off screen, clipped, covered or zero-sized. Two asymmetries
  are deliberate: `display:none` takes its subtree with it while `visibility:hidden` does not (CSS inherits
  `visibility`, so a `visibility:visible` descendant comes back), and `aria-hidden` removes a node from the
  accessibility tree while changing nothing about the rendering — so the extractors ask
  `RenderingReasonFor`, which ignores it, and only the tree asks `ReasonFor`, which does not.
- **Block-level** is `HtmlDisplay`, HTML's suggested rendering rather than a used display, and it is the
  table that decides — not the cascade. The cascade only wins where it *differs* from the table, which is
  what makes `<span style="display:block">` a block while retaining HTML's suggested rendering for `<section>`.
- **`innerText`** is therefore the text of the document, not the text of a rendering of it: the required
  line breaks, the `<br>`s, the cell tabs and the white-space processing are all there, but nothing wraps,
  so a paragraph is one line however wide it would have been.

Three simplifications in the name computation are worth knowing before reading a wrong name as a bug: CSS
generated content (`::before`, `::after`, `::marker`) contributes nothing, `text-transform` is not applied,
and SVG `<title>`/`<desc>` children are not read. Everything else of accname 1.2 — 2A through 2I, the
recursion, the visited guard, the flattening — is the algorithm as written. HTML-AAM's mapping table is
implemented in full with one blanket simplification: where it names a computed role that is not a WAI-ARIA
role (`html-abbr`, `html-audio`, `keyboard`, `variable` and their kind) the element maps to `generic`.

`AccessibilityOptions` has three presets and they are not interchangeable: `Default` is the pruned tree,
`Snapshot` adds the text between the nodes (which is what `AccessibilitySnapshot.Render` needs to say
anything at all), and `Full` is what `Accessibility.getFullAXTree` answers with. A snapshot states each
string once — text that is already a node's accessible name is not published again as a text node.

**A snapshot owns one visibility cascade**, shared by its tree walk and accessible-name computations.
The builder and cascade live only for that synchronous query; the next snapshot rebuilds them so same-turn
CSSOM, class, attribute and media changes remain visible. A stand-alone name computation can still use
an uncached visibility resolver.

The four fixture pages under `Jint.Tests.Browser/Accessibility/Golden/` are rendered up to three ways each and the
output is checked in. **`JINT_BROWSER_GOLDEN=update` rewrites them**, the same discipline `JINT_SPEC_ANCHORS`
and `JINT_DOM_BINDINGS` use: the diff is the artefact, so a change to what an agent reads has to be looked at.

Current renderless boundaries belong to Browser, not to a second DOM or CSS implementation:

- `CssCascade` adapts native text-valued queries. A refusal latches only before the cascade has ever
  answered; an element-specific failure must not disable visibility rules for the rest of the tree.
- `HtmlDisplay` supplies HTML's suggested display and whitespace defaults. Declared overrides still win.
- `ResolvedStyle` supplies the finite initial and used-value policy documented in the Browser instructions.
- `ContentEditing` implements the enumerated `contenteditable` state and the shared editing-host policy.
- `DomNodeMembers` implements `getRootNode` over native parent links, including the composed-root option.

Keep golden extraction and accessibility outputs in sync with any observable change to these policies.
