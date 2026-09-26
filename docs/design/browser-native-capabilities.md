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

## Finite HTML attributes and collections

These helpers read current native attributes and ordinary tree links with one bounded read-work
object. They add no parser element properties and allocate no per-element state for attribute reads.
The legacy context-menu assignment and map collection identity use lazy weak-key Browser tables.

* `contentEditable` uses exact ASCII-insensitive enumerated keywords. Whitespace is not trimmed;
  the getter returns a canonical state, `inherit` removes the attribute on setting, and invalid setters
  raise `SyntaxError`. This deliberately corrects the old whitespace normalization. The editor host
  lookup shares the keyword parser but retains its separate form-control routing exclusions.
  `isContentEditable` follows editing-host/eligible-element ancestry in the ordinary tree. Only HTML
  elements and the actual SVG `svg` and MathML `math` roots are eligible. A document's actual design-mode
  flag makes its direct HTML child an editing host, including in an XML document. Detached nodes and
  shadow descendants do not inherit document editing mode through an owner-document reference.
* `spellcheck` recognizes true/empty and false explicitly; missing or invalid values return the permitted
  headless default false, without ancestor lookup in the IDL getter. No spelling provider is installed,
  so `forceSpellCheck()` raises `NotSupportedError`. `accessKeyLabel` is empty because no shortcut is
  assigned. `translate` inherits yes/empty/no through the ordinary tree and defaults to true; foreign
  ancestors are traversed without interpreting their attributes.
* Automatic `draggable` recognizes HTML images and anchors with a present `href`. Object elements
  currently have no actual image representation in the resource model, so a MIME hint cannot make them
  draggable. Explicit true/false override the automatic result.
* Legacy `contextMenu` retains an explicitly assigned native identity across adoption, without changing
  the raw attribute or copying assignment to clones. Clearing assignment restores lookup of the first
  matching ID in the current owner document; it must be an HTML menu. Empty references return null.
* Input `autocomplete` computes the IDL-exposed autofill value from the complete field categories,
  including hidden anchors, section/address/contact scope and terminal `webauthn`. Missing or invalid
  token sequences return empty. Field and credential spellings are retained as the algorithm requires;
  scope keywords are canonicalized. The setter remains raw attribute reflection. This corrects the
  previous partial token handling without installing an autofill or credential provider.
* `map.images` is a stable existing HTMLCollection view containing only associated HTML `img` nodes in
  the current ordinary tree. A hash-name reference is the nonempty literal suffix after the first `#`,
  without trimming or URL decoding. The first HTML map whose ID or name matches wins. Each read resolves
  this map's two names once before scanning images, so duplicates do not cause a map-tree scan per image.
  The view remains live across changes and adoption and does not cross a shadow boundary.

Sources: [contenteditable and editing](https://html.spec.whatwg.org/multipage/interaction.html#attr-contenteditable),
[translate](https://html.spec.whatwg.org/multipage/dom.html#the-translate-attribute),
[draggable](https://html.spec.whatwg.org/multipage/dnd.html#the-draggable-attribute),
[autofill](https://html.spec.whatwg.org/multipage/form-control-infrastructure.html#autofill), and
[hash-name references](https://html.spec.whatwg.org/multipage/common-microsyntaxes.html#rules-for-parsing-a-hash-name-reference).

## Remaining legacy HTML members

The pinned AngleSharp 1.8.2 menu/menuitem types and keygen key type are raw attribute reads, with the
binding's existing string conversion. Their native binding retains that behavior rather than inventing
enumerated defaults. The keygen `type` string stays `keygen`. A command reference uses the current owner
document's first matching ID, which must identify an HTML element; an empty reference returns null.
These queries charge actual attribute, character and ordinary-tree work and finish with host checks.

The pinned keygen labels implementation allocates a NodeList but never populates it. Browser preserves
this unavailable legacy association as a distinct stable empty NodeList for each keygen, allocated on
first demand and projected through the existing NodeList wrapper. Adoption preserves the list identity;
cloning creates a separate list. This does not change the modern labelable-element algorithm or imply
that key generation or legacy label association is implemented.

The pinned marquee loop member is an auto-property starting at zero, independent of the content
attribute. Browser deliberately corrects it to [HTML's finite loop algorithm](https://html.spec.whatwg.org/multipage/obsolete.html#dom-marquee-loop):
the getter parses the current attribute's signed integer prefix and returns a positive count or `-1`.
Only ASCII whitespace is skipped. Out-of-Int32 positive counts return `-1` as an explicit finite
interoperability policy; HTML's prose does not define that boundary. The setter ignores zero and values
below `-1`. A valid positive value or `-1` changes the content attribute only when the parsed current
value differs, so an equivalent prefix keeps its raw spelling and a missing/invalid `-1` keeps its
absence/spelling. The existing RequiredInt32 conversion precedes this setter algorithm. Reads and the
setter's pre-mutation parse check actual digit work, including long zero and overflowing prefixes.
This supplies finite reflection behavior without a rendering animation or eager loop state.
