# Events and workers

Script-visible events use Jint's tree-aware event dispatcher. The supported families include mouse, pointer, keyboard, input, focus, wheel, composition, form, history, page-transition, and before-unload events. Event propagation follows the DOM tree and crosses supported shadow boundaries.

HTML event-handler attributes and properties share one slot, and activation methods produce trusted event sequences. Focus state, `document.activeElement`, `autofocus`, and keyboard focus traversal are implemented without a windowing system.

## Diagnostics and dialogs

```csharp
page.DialogOpened += (_, dialog) =>
{
    dialog.Accepted = true;
    dialog.PromptText = "answer";
};
```

The handler runs on the page thread; do not call back into the page from it. Without a handler, dialogs are dismissed. Dialogs do not block the page as a graphical browser dialog would.

Read script failures and console output from:

```csharp
IReadOnlyList<PageError> errors = page.Errors;
IReadOnlyList<string> messages = page.ConsoleMessages;
```

## Observers and workers

`MutationObserver` delivers at the microtask checkpoint. `IntersectionObserver` and `ResizeObserver` deliver initial entries using the synthetic box model. With no real layout, an observed target is reported once as fully intersecting or at its current synthetic size.

Dedicated `Worker` instances run on package-managed threads and use the page's network and security posture. `Page.Workers` reports the current count. `SharedWorker` and `ServiceWorker` are not supported.
