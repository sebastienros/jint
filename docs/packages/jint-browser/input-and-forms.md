# Input and forms

Host input uses the same trusted input dispatcher as the DevTools Protocol path:

```csharp
await page.FillAsync("#email", "ada@example.org");
await page.TypeAsync("#search", "jint");
await page.PressAsync("Enter");
await page.ClickAsync("#save");
await page.SelectAsync("#country", "FR");
await page.HoverAsync("#menu");
await page.ScrollToAsync(800);
```

Element targets can be CSS selectors or `ref=` values produced by an accessibility snapshot:

```csharp
var snapshot = await page.AccessibilitySnapshotAsync(includeReferences: true);
await page.ClickAsync("ref=42");
```

References belong to one document and stop resolving after navigation.

- `FillAsync` focuses, selects all, and inserts one replacement.
- `TypeAsync` preserves the current value and dispatches keys one character at a time.
- `PressAsync` targets the focused element, or the body when nothing is focused.
- `ClickAsync` scrolls the target into view and runs link, button, checkbox, radio, label, summary, and option activation.
- `SelectAsync` matches an option by value, then by visible text, and fires `input` followed by `change`.

Methods that name an element return `false` when no suitable target exists.

`SubmitFormAsync("#form")` runs validation, `submit`, entry-list construction, `formdata`, and GET or POST submission. It returns `null` when nothing matched, validation failed, or submission was cancelled.

Input uses a deterministic synthetic box model, not visual layout. See [Limitations](./limitations).
