# Extraction

`Jint.Browser` provides text-first alternatives to screenshots:

```csharp
string markdown = await page.MarkdownAsync(mainContentOnly: true, maxLength: 4_000);
string text = await page.TextAsync();
string ax = await page.AccessibilitySnapshotAsync(includeReferences: true);
```

- `MarkdownAsync` returns CommonMark.
- `TextAsync` returns document text with structural line breaks and whitespace processing, but no visual wrapping.
- `AccessibilitySnapshotAsync` returns an indented outline of roles, accessible names, states, and text.

`mainContentOnly` selects the first `<main>`, `[role=main]`, or `<article>` when one exists. `maxLength` of `0` means unlimited. A truncated result ends at a word boundary and includes `[truncated]`.

Extraction reads the DOM and runs no page JavaScript. It is computed on demand and does not maintain a rendered representation.

Accessibility references make a snapshot actionable:

```text
- button "Save" [ref=42]
```

Input methods accept `ref=42` in place of a selector. References remain valid only for the current document.

The accessibility tree cannot determine whether an element is off-screen, clipped, or covered because there is no visual layout. Visibility is based on document and CSS information the package can compute.
