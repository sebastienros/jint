# Native cursor keyword slice


> Historical design: the current [LightPanda-style CSS boundary](https://github.com/sebastienros/jint/blob/main/Jint.HtmlParser/README.md#renderless-css-boundary)
> supersedes typed-value, computation, registration, container and keyframe claims below.
> These are retained design notes, not instructions to restore removed features.

Authority: [CSS UI 4 §5.1.1](https://drafts.csswg.org/css-ui-4/#cursor).

The completed cursor property accepts all 36 predefined keywords as one decoded, ASCII-case-insensitive
identifier, serialized in lowercase through the existing Keyword value. The initial value is auto,
the property is inherited, and keyword computation retains the specified value, including auto.
Shared CSS-wide keywords, variable/environment substitution, invalid-at-computed-value handling,
and the normal iterative inheritance cache remain in use.

URL tokens, url() and image-set() cursor families, including their hotspot coordinates, retain the
explicit `V6:cursor-images` pending boundary. No image prefix is dropped to publish a fallback keyword.
This slice does not load images or add painting, geometry, hit-testing, or device behavior.

Tests cover the complete keyword set, case/escapes/comments, initial and inherited computation,
live CSSOM and computed enumeration, var/env and invalid substitution, invalid and pending atomic
replacement, and cancellation during both a tokenized family walk and a setter transaction.
The existing cursor binding is reused without emitter changes.
