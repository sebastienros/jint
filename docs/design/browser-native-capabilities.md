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
