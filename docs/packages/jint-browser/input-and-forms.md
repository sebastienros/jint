# Input and forms

`Page.ClickAsync` performs trusted, hit-tested input at the requested element's synthetic-box centre.
It returns `false` without dispatching input if an unrelated element covers that point; descendants of
the requested element remain valid hits. This does not add real rendering geometry or a logical-click fallback.
If focus or pointer listeners move the element during input, the real pointer sequence still completes,
but the result is `false` when the eventual click is outside the requested element. No second activation
is synthesized. A matching click remains successful if its own handler subsequently removes the element.

Host input uses the same trusted input dispatcher as the DevTools Protocol path:

```csharp
await page.FillAsync("#email", "ada@example.org");
await page.TypeAsync("#search", "jint");
await page.PressAsync("Enter");
await page.PressAsync("k", KeyModifiers.Control); // Meta for Command on macOS
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

- `FillAsync` focuses, selects all, and inserts one replacement in text controls. Date, month, week, time and local date-time inputs commit a native sanitized value and fire trusted `input` then `change` events when it changes. Invalid picker values throw `ArgumentException`; disabled, readonly and unsupported controls return `false`.
- `TypeAsync` preserves the current value and dispatches keys one Unicode scalar at a time, including supplementary characters represented by UTF-16 surrogate pairs. Each scalar follows the trusted keyboard and cancelable input sequence for text controls and contenteditable hosts.
- `PressAsync` targets the focused element, or the body when nothing is focused. Its modifier overload accepts the `KeyModifiers` flags `Alt`, `Control`, `Meta` and `Shift` on both keydown and keyup.
- Focused buttons and button-state inputs activate with a trusted click on Enter's keydown or Space's keyup, without pointer events or hit testing. Canceling the key default or click prevents activation; moving focus abandons a pending Space press. Submit buttons retain their name/value and run validation and submit listeners.
- `ClickAsync` scrolls the target into view and runs link, button, checkbox, radio, label, summary, and option activation.
- `SelectAsync` matches an option by value, then by visible text, and fires `input` followed by `change`.
- `SetInputFilesAsync` selects files into an `<input type=file>`, which is what a file picker would do and what clicking one cannot: the files are read into memory, `input.files` becomes a `FileList`, `input.value` becomes a fake Windows path plus the first name, and `input` then `change` fire. An overload takes `PageFile` values for content that has no path. An input without `multiple` keeps only the first file, and passing no files clears the selection.
- `TapAsync` puts one finger down at the centre of the target's box and lifts it: `touchstart`, `touchend`, and then the compatibility mouse events [Touch Events §8](https://w3c.github.io/touch-events/#mouse-events) gives a tap — `mousemove`, `mousedown`, `mouseup`, `click` — so it activates what a click activates. A page that calls `preventDefault()` on the `touchstart`, or on the first `touchmove`, gets none of those four. `TapAsync(x, y)` taps a point instead of an element.

`SetTouchEmulationAsync(enabled, maxTouchPoints)` is the separate question of what the page *detects*: `ontouchstart` and its three siblings on `window`, `document` and elements, `navigator.maxTouchPoints`, and the `(pointer: coarse)` / `(hover: none)` media features. A tap is delivered either way — a page that added a `touchstart` listener hears one — so emulation is what lets a responsive page decide to add one. It belongs to the page and survives navigation; `BrowserOptions.HasTouch` is the same setting one level up, so a page opens as a touch device and its first document parses in one.

`SetViewportSizeAsync(width, height)` resizes an open page and keeps its device scale factor. `SetViewportAsync(viewport)` also sets the scale factor, and `Page.Viewport` returns the current value. The loaded document is updated before the task completes. `innerWidth`, `devicePixelRatio`, `screen`, `visualViewport` and the boxes return the new values. A size change fires `resize` at the window, and then at `visualViewport`. Each `MediaQueryList` whose result changed fires `change`. Stylesheet `@media` rules are evaluated against the new size, and an `<img>` whose `srcset` or `<picture>` now selects another candidate queues a fetch for it. `WaitForIdleAsync` waits for those fetches. CDP's `Emulation.setDeviceMetricsOverride` performs the same update, and the viewport persists across navigation. `BrowserContextOptions.Viewport` and `BrowserContextOptions.HasTouch` set the starting device profile for one context's pages and override `BrowserOptions.Viewport` and `HasTouch`. One browser can therefore drive desktop, tablet and phone contexts together:

```csharp
var phone = await browser.NewContextAsync(new BrowserContextOptions { Viewport = new Viewport(390, 844, 3), HasTouch = true });
var page = await phone.NewPageAsync();
await page.SetViewportSizeAsync(320, 640);
```

`EmulateMediaAsync` is the same kind of setting for the media environment — Playwright's `page.emulateMedia`, and the in-process half of the protocol's `Emulation.setEmulatedMedia`, over the same page state:

```csharp
await page.EmulateMediaAsync(new MediaEmulation { ReducedMotion = "reduce" });
await page.EmulateMediaAsync(new MediaEmulation { ColorScheme = "dark" }); // reduced motion stays on
await page.EmulateMediaAsync(new MediaEmulation { Media = "print", ColorScheme = "" }); // print; scheme back to the default
```

`MediaEmulation` carries `Media` (`screen`, `print`), `ColorScheme` (`light`, `dark`, `no-preference`), `ReducedMotion` (`reduce`, `no-preference`), `ForcedColors` (`active`, `none`) and `Contrast` (`no-preference`, `more`, `less`, `custom`). A property left `null` keeps the override already in force and an empty string clears it; any other keyword is refused with `ArgumentException`. The loaded document moves: every `matchMedia` list whose answer changed fires `change` before the call completes, and stylesheet `@media` rules follow on the next style read. The setting belongs to the page and survives navigation. `BrowserOptions.MediaEmulation` and `BrowserContextOptions.MediaEmulation` are the page's defaults — merged property by property, context over browser — so a page, and a popup it opens, parses its first document in them. They sit beneath the overrides the way an operating-system preference does in a real browser: clearing an override, from `EmulateMediaAsync` or from a protocol client's `Emulation.setEmulatedMedia`, goes back to them.

Methods that name an element return `false` when no suitable target exists.

Color inputs keep their current value, dirty flag and reset behavior in native control state.
The opaque limited-sRGB path accepts CSS names, hex, RGB, HSL and HWB and serializes lowercase
six-digit hex; invalid colors become black. Script assignments do not fire input events. Alpha-enabled
controls, display-P3, system colors and advanced or dependent CSS color functions remain explicitly
unsupported and throw rather than silently substituting black.

`SubmitFormAsync("#form")` runs validation, `submit`, entry-list construction, `formdata`, and GET or POST submission. It returns `null` when nothing matched, validation failed, or submission was cancelled.

Page scripts can use `new FormData(form, submitter)` to read the same entry list without submitting or
validating the form. The optional submitter must be a submit button owned by that form. A bubbling
`formdata` event lets listeners amend the entries; constructing another `FormData` for that same form
inside the listener throws `InvalidStateError`. Standalone engines and workers keep the no-DOM
`FormData()` constructor.

An activated submit input with no `value` attribute submits the UA's default label, `Submit`.
An explicitly empty `value=""` remains empty; the input's `.value` getter still reflects its attribute.

Input uses a deterministic synthetic box model, not visual layout. See [Limitations](./limitations).
