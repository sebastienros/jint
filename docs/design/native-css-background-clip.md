# Native background-clip slice

Authority: [CSS Backgrounds 4 §2.8](https://drafts.csswg.org/css-backgrounds-4/#the-background-clip).

This declaration/computation slice accepts a comma list of border-box, padding-box, content-box,
border-area, text, or the unordered border-area/text union, canonically `border-area text`.
Lists retain every layer, including repetitions and their order, with canonical comma-space
separators. Computation does not pad or truncate the list to a background-image count.
The initial value is border-box and the property is noninherited; explicit inheritance and
shared CSS-wide/reference paths remain available.

`KeywordList` owns readonly validated Keyword components and cached canonical text. Parsing
walks existing significant components iteratively, charges tokens/comparisons/copies/output,
and checks cancellation before publishing the value. There is no additional layer-count cap;
existing syntax limits and bounded work apply. Invalid unions, commas, nonkeyword layers and
embedded wide keywords reject the whole replacement without mutating the old declaration.

The background shorthand retains its named pending boundary. This slice adds no painting,
layout, device, or Browser-context behavior; clip reads demand no font or geometry metric.
Tests cover all singles/unions, decoded escapes/case/comments, many repeated layers, storage
ownership, transactional invalid/cancelled writes, var/env/invalid-substitution/default/inheritance,
live computed list equality, and jQuery's clone-and-clear bootstrap probe. The existing
backgroundClip binding is reused without emitter changes.
