# Input and forms

Host input uses the same trusted input dispatcher as the DevTools Protocol path:

```csharp
await page.FillAsync("#email", "ada@example.org");
await page.TypeAsync("#search", "jint");
await page.PressAsync("Enter");
await page.ClickAsync("#save");
await page.SelectAsync("#country", "FR");
await page.SetInputFilesAsync("#avatar", ["/tmp/portrait.png"]);
await page.HoverAsync("#menu");
await page.ScrollToAsync(800);
await page.TapAsync("#save");
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
- `SetInputFilesAsync` selects files into an `<input type=file>`, which is what a file picker would do and what clicking one cannot: the files are read into memory, `input.files` becomes a `FileList`, `input.value` becomes a fake Windows path plus the first name, and `input` then `change` fire. An overload takes `PageFile` values for content that has no path. An input without `multiple` keeps only the first file, and passing no files clears the selection.
- `TapAsync` puts one finger down at the centre of the target's box and lifts it: `touchstart`, `touchend`, and then the compatibility mouse events [Touch Events §8](https://w3c.github.io/touch-events/#mouse-events) gives a tap — `mousemove`, `mousedown`, `mouseup`, `click` — so it activates what a click activates. A page that calls `preventDefault()` on the `touchstart`, or on the first `touchmove`, gets none of those four. `TapAsync(x, y)` taps a point instead of an element.

`SetTouchEmulationAsync(enabled, maxTouchPoints)` is the separate question of what the page *detects*: `ontouchstart` and its three siblings on `window`, `document` and elements, `navigator.maxTouchPoints`, and the `(pointer: coarse)` / `(hover: none)` media features. A tap is delivered either way — a page that added a `touchstart` listener hears one — so emulation is what lets a responsive page decide to add one. It belongs to the page and survives navigation; `BrowserOptions.HasTouch` is the same setting one level up, so a page opens as a touch device and its first document parses in one.

Methods that name an element return `false` when no suitable target exists.

`SubmitFormAsync("#form")` runs validation, `submit`, entry-list construction, `formdata`, and GET or POST submission. It returns `null` when nothing matched, validation failed, or submission was cancelled.

Page scripts can use `new FormData(form, submitter)` to read the same entry list without submitting or
validating the form. The optional submitter must be a submit button owned by that form. A bubbling
`formdata` event lets listeners amend the entries; constructing another `FormData` for that same form
inside the listener throws `InvalidStateError`. Standalone engines and workers keep the no-DOM
`FormData()` constructor.

Input uses a deterministic synthetic box model, not visual layout. See [Limitations](./limitations).
