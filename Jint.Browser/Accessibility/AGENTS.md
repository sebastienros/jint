# Agent instructions: accessibility and extraction

> **Read this when:** You are touching `Jint.Browser/Accessibility/` or `Jint.Browser/Extraction/` — the
> accessibility tree, roles and names, or the text and markdown a page is read as.
>
> This is one of the co-located instruction files indexed from the repository-root [`AGENTS.md`](../../AGENTS.md).
> Read that first, then [`Jint.Browser/AGENTS.md`](../AGENTS.md) for the package's principle. Nothing below is
> repeated in either.

### Accessibility and extraction have no layout

`Accessibility/` computes an accessibility tree over AngleSharp's DOM and `Extraction/` renders the same
document as text or CommonMark. Both are pure C# over `IDocument`/`IElement`; neither touches an engine, and
that is why they were built before the page runtime existed. The consumers are the CDP `Accessibility` domain,
the custom `Jint.getMarkdown`/`getText`/`getAccessibilitySnapshot` domain, and the MCP server's `snapshot`.

**Three things a browser answers from its layout tree are answered from somewhere else, and every one is a
place where this can be wrong.**

- **Hidden** is `ElementVisibility`: the `hidden` content attribute, `aria-hidden="true"`, and `display:none`
  / `visibility:hidden|collapse` from the cascade — `IElement.ComputeCurrentStyle()`, which resolves author
  sheets and the UA sheet — falling back to the `style` content attribute alone when `AngleSharp.Css` is not
  registered. It cannot know that an element is off screen, clipped, covered or zero-sized. Two asymmetries
  are deliberate: `display:none` takes its subtree with it while `visibility:hidden` does not (CSS inherits
  `visibility`, so a `visibility:visible` descendant comes back), and `aria-hidden` removes a node from the
  accessibility tree while changing nothing about the rendering — so the extractors ask
  `RenderingReasonFor`, which ignores it, and only the tree asks `ReasonFor`, which does not.
- **Block-level** is `HtmlDisplay`, HTML's suggested rendering rather than a used display, and it is the
  table that decides — not the cascade. The cascade only wins where it *differs* from the table, which is
  what makes `<span style="display:block">` a block and stops AngleSharp's incomplete default sheet from
  calling every `<section>` inline.
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

The four fixture pages under `Jint.Tests.Browser/Accessibility/Golden/` are rendered three ways each and the
output is checked in. **`JINT_BROWSER_GOLDEN=update` rewrites them**, the same discipline `JINT_SPEC_ANCHORS`
and `JINT_DOM_BINDINGS` use: the diff is the artefact, so a change to what an agent reads has to be looked at.

Divergences that are **AngleSharp's**, found by this work, to be reported upstream rather than patched here:

| What | The standard | AngleSharp.Css 1.0.2 |
| --- | --- | --- |
| `el.ComputeCurrentStyle()` without the CSS services | an empty declaration, or a documented failure | throws `InvalidOperationException("Sequence contains no elements")`, which is why every call goes through `Dom/Views/CssCascade` and why `ElementVisibility` latches on a refusal — one throw per document rather than one per node of a tree walk. **It latches only while the cascade has never answered**: a refusal after one has is about *this* element's own declarations (`width: 20ch` is a unit AngleSharp.Css cannot convert, and ordinary modern CSS), and latching there would take the page's `display: none` rules down with it |
| the default style sheet's `display` rules | HTML's rendering section gives `display: block` to `section`, `article`, `nav`, `aside`, `header`, `footer`, `main`, `figure`, `figcaption`, `details`, `summary`, `dialog`, `hgroup` | no rule at all, so every one of them computes to nothing and reads as inline |
| `[hidden] { display: none }` | in HTML's rendering section | absent, so `<div hidden>` computes `display: block` |
| `textarea { white-space: pre-wrap }` | in HTML's rendering section | absent, though `pre { white-space: pre }` is there |
| `CssMediaQueryList.matches` | evaluate the query against the device and answer | a stub: `ComputeMatched` returns `false` for every query, so a page asking whether it is on a narrow screen is always told no — which is why `Runtime/MediaQuery` exists at all |
| Media Queries Level 5's preference features | `prefers-color-scheme`, `prefers-reduced-motion`, `prefers-contrast`, `forced-colors`, `hover`, `pointer`, `scripting`, `color-gamut` are media features the cascade evaluates | not modelled: `IRenderDevice` has no member for any of them, so an `@media` rule naming one can never match and `Runtime/PageMediaEnvironment` answers them itself |
| a longhand nothing declared, through `getComputedStyle` | CSSOM's *resolved value*: every supported longhand answers, and a property nothing declared answers its initial value — `visibility` is `visible` | the empty string. **This is the one that stops an automation client.** Playwright's actionability check ends in `style.visibility !== "visible"`, so it reads every element of every page as hidden: `IsVisibleAsync` is false for an element with a real 1280×16 box, an unforced `ClickAsync` or `WaitForSelectorAsync` waits out its timeout, and the ARIA role engine drops the element as hidden. `Jint.Tests.Browser/DevTools/PlaywrightCourseTests` drives past it with `Force` and `IncludeHidden` and pins the reason; the standing decision (`Views/ComputedStyleTests`) is to record this rather than keep an initial-value table here, and what is new is that a supported client is unusable without one |
| the selector parser on `:has(*,:jqfake)` | a parse failure the caller can act on | `CssSelectorConstructor.HasFunctionState.Produce()` dereferences null, so the failure is a `NullReferenceException` rather than the `DomException` every other bad selector raises. jQuery 3.7 asks for exactly that selector inside a `try` during its support detection, so an unwrapped binding refuses to load jQuery at all — `Dom/DomSelectorMembers` contains both shapes and answers the `SyntaxError` the standard names |

One more, in AngleSharp itself rather than in `AngleSharp.Css`:

| What | The standard | AngleSharp 1.7.2 |
| --- | --- | --- |
| `IHtmlElement.IsContentEditable` on `<div contenteditable>` | `true`: HTML's [`contenteditable`](https://html.spec.whatwg.org/multipage/interaction.html#attr-contenteditable) is an enumerated attribute whose `true` keyword has the **empty string** as its other spelling, which is how nearly every page in the world writes it | `false` — the attribute is mapped through an enumeration that does not admit the empty string, so only `contenteditable="true"` reads as editable. `Events/ContentEditing.HostOf` computes the state itself for the editor and for focusability; the script-visible `el.isContentEditable` is still AngleSharp's answer, because that member is the binding forwarding it |
| `Node.getRootNode()` | DOM §4.4: `Node getRootNode(optional GetRootNodeOptions options = {})` | absent — there is no `[DomName("getRootNode")]` anywhere in the assembly, so nothing could generate it. `Dom/DomNodeMembers` declares it over `INode.Parent`, and it is not a corner: Playwright's injected script calls it on every element it touches |
