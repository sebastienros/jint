# Native physical inset slice

Authority: [Positioned Layout 3 §3.1](https://drafts.csswg.org/css-position-3/#insets),
[Anchor Positioning 1](https://drafts.csswg.org/css-anchor-position-1/#anchor-pos), and CSSOM resolved values.

Top, right, bottom and left accept auto or one signed length-percentage, including typed numeric
math and unitless zero. All four have initial auto and are noninherited. Existing Keyword, Numeric
and Math values retain declaration ownership, importance, serialization, wide keywords and references.
Font and viewport units compute through the shared numeric path; negative values are not clamped.
Percentages and mixed length-percentage calculations remain computed without a containing-block basis.

Logical insets and inset shorthands retain their named pending boundaries. Anchor and anchor-size
extensions are named pending even within calc(), rather than being mislabeled invalid. Stretch is invalid.

Static position does not erase an inset's computed value. Browser resolved reads retain computed
values for static or no-box elements. Positioned boxes require `C6:positioned-inset`: the flat row
model does not implement positioning and supplies no fabricated zero or viewport percentage basis.
Static reads request no layout measurements. No positioning state or new value kind is introduced.
The current common resolved-value dispatcher owns the inset route. Explicit display:none and
display:contents preserve computed insets before measurement; connection checks reuse its charged
ancestor walk and verify the style before any computed-value early return.

Tests cover all four initial values and grammars, signed values, mixed calculations, inheritance and
var/env, atomic invalid/pending/cancelled replacements, cold static reads, and the positioned dependency.
The existing JQueryDelegatedClickSubmitsAHiddenForm fixture and vendor assets remain unchanged; its
position:absolute/left:-9999em declaration setters are covered directly and await the shared test gate.
