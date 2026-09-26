# Browser media, canvas and dialog capabilities

The native tree stores markup without constructing playback, rendering or interaction state. Browser owns
these capabilities on demand. `BrowserMediaState` and `BrowserDialogState` are weakly keyed by the actual
native element. They hold no engine or parser callback. Adoption preserves an element's state; cloning
creates a new identity and therefore starts with initial state.

The pinned AngleSharp integration registered no media decoder or canvas renderer. Its controller-null
setters generally did nothing, `play()` returned undefined, canvas context selection returned null,
and encoding returned an empty string. These were backing stubs, rather than implemented playback or
rendering. The native cutover makes the boundary explicit:

* Canvas context selection returns null and the legacy support query returns false. `toDataURL` and the
  nonstandard `setContext` raise `NotSupportedError`. No rendering-context object from AngleSharp is retained.
  Raising an error instead of returning the previous empty string is a deliberate behavior correction.
* Media starts at `HAVE_NOTHING`, `NETWORK_EMPTY`, paused, duration NaN, zero intrinsic video dimensions,
  and no buffered, seekable or played ranges. Script-written volume, mute, playback rates and requested
  start time survive reads. Mute defaults follow the current unnamespaced `muted` attribute until script
  sets the mute state. Volume/rate changes queue real Jint events. Reverse playback is unsupported.
  `canPlayType` returns the empty string and `play()` returns a promise rejected with `NotSupportedError`.
  No request is made by those queries, setters, context operations or playback refusal.
  An explicit `load()` selects the current null-namespace `src`, or the first source child, and calls
  the existing document resource owner. The selected URL is captured once on the page loop. A lazy
  source observer cancels a direct source on its own `src` changes. Source-child selection queues a
  bounded recheck; unrelated descendants and appended tracks do not cancel the selected request.
  Repeated loads cancel the previous operation and invalidate its queued resource events. Completion
  returns to the loop and checks operation identity, owner document, source and
  base URL before publishing a media error. Receiving bytes still yields `MEDIA_ERR_SRC_NOT_SUPPORTED`
  because no decoder exists; a failed request yields `MEDIA_ERR_NETWORK`. Disposal cancels transport and
  releases the observer. A document with no resource owner explicitly refuses a non-empty request.
  Absence of a source stays `NETWORK_EMPTY`; a present empty, malformed or disallowed source resets the
  previous load and becomes `MEDIA_ERR_SRC_NOT_SUPPORTED`. Known source setup failures use a dedicated
  resource-owner exception; host and constraint failures propagate. Completion checks precede state
  commit, and a failed check leaves coherent empty state after releasing the operation.
* Track lists are stable empty objects; there is no decoder output or invented text track. `addTextTrack`,
  the nonstandard controller and start-date capabilities explicitly refuse. Empty immutable range objects
  are reused within each realm, which differs from HTML's fresh range snapshots but cannot expose stale data.
* Ordinary dialog `show()` and `close()` use the native `open` attribute and a lazy return value. Opening
  fires cancellable `beforetoggle`; closing fires non-cancellable `beforetoggle`; both queue a coalesced
  `toggle` event carrying the actual old/new states. Closing queues a non-bubbling `close` event. These
  events use the Jint dispatcher. The internal ToggleEvent prototype exposes `oldState`, `newState` and
  null `source`; this does not install or claim a global ToggleEvent constructor.
* Dialog focus uses the existing Browser focus model: a light-tree focusable descendant, preferring
  autofocus, or the dialog itself. The existing focus model may decline the dialog itself and has no
  rendering or scroll-container focus delegate. Previous focus is restored only while focus remains
  inside the closing dialog. There is no popover/close-watcher integration, top layer, document blocking
  or inert modal subtree. `showModal()` raises `NotSupportedError` without changing `open`, deliberately
  correcting the old silent attribute-only success. Form submission with `method=dialog` remains outside
  this capability group.

Algorithm references: [HTML media elements](https://html.spec.whatwg.org/multipage/media.html#media-elements),
[canvas](https://html.spec.whatwg.org/multipage/canvas.html#the-canvas-element), and
[dialogs](https://html.spec.whatwg.org/multipage/interactive-elements.html#the-dialog-element).
