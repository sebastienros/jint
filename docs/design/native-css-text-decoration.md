# Native text-decoration slice


> Historical design: the current [LightPanda-style CSS boundary](https://github.com/sebastienros/jint/blob/main/Jint.HtmlParser/README.md#renderless-css-boundary)
> supersedes typed-value, computation, registration, container and keyframe claims below.
> These are retained design notes, not instructions to restore removed features.

Authorities: [Text Decoration 4 §§2.1–2.6](https://drafts.csswg.org/css-text-decor-4/#text-decoration-property),
[Borders 4 line-width](https://drafts.csswg.org/css-borders-4/#typedef-line-width),
[Values 4 component combinators](https://drafts.csswg.org/css-values-4/#component-combinators), and
[Color 4 resolved values](https://drafts.csswg.org/css-color-4/#resolving-color-values).

This finite slice implements all four noninherited longhands and the shorthand for ordinary and
keyframe declarations. Initial values are line none, thickness auto, style solid and color
currentcolor. Line accepts all 15 nonempty, nonduplicated subsets of underline/overline/line-through/blink
in that canonical order; none, spelling-error and grammar-error are exclusive alternatives.
Style accepts solid/double/dotted/dashed/wavy; color reuses the completed typed color grammar.

Thickness uses the current signed length-percentage production, plus auto/from-font and the
current Borders 4 hairline/thin/medium/thick keywords. It does not reuse padding's nonnegative
range or computation clamp. Percentages are retained and em/rem use actual element/root font
sizes. Text Decoration 4's computed “as specified” keyword contract retains line-width keywords;
no device thickness, font selection, layout or painting metrics are invented.

The shorthand consumes up to seven existing components, once each, with at most one contiguous
line group and one group for each remaining longhand. It serializes all four groups in canonical
line/thickness/style/color order, resets omitted groups, and preserves shared CSS-wide, deferred,
priority and transactional behavior. Underline offset and position are not reset or completed.

Computed decoration-color retains currentcolor as a typed dependency for explicit inheritance.
Its returned text and the computed shorthand resolve through the requesting element's actual
color, without reparsing text or destroying that retained value. Ordinary descendants still have
line none: graphical decoration propagation is independent of computed-value inheritance and
is outside this slice. The corrected Browser fixture proves nearest-parent inheritance,
direct-root explicit inheritance, variable reevaluation and empty computed cssText.

Fixtures cover line subsets/exclusivity, typed thickness/keywords, group order and contiguity,
reset/priority/deferred identity, partial overrides/removal, CssText roundtrips, invalid substitution,
resolved color/inheritance/mutation, cancellation preserving the prior declaration, and actual
Browser shorthand/longhand agreement. No public APIs or generic value kinds are added.
