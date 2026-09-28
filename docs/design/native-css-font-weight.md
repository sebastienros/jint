# Native font-weight slice


> Historical design: the current [LightPanda-style CSS boundary](../../Jint.HtmlParser/README.md#renderless-css-boundary)
> supersedes typed-value, computation, registration, container and keyframe claims below.
> These are retained design notes, not instructions to restore removed features.

Authority: [CSS Fonts 4 §2.2](https://drafts.csswg.org/css-fonts-4/#font-weight-prop)
and [§2.2.1](https://drafts.csswg.org/css-fonts-4/#relative-weights).

| Contract | Disposition |
| --- | --- |
| Name/context | `font-weight`, ordinary style and keyframe only; no aliases |
| Literal grammar | `normal`, `bold`, `bolder`, `lighter`, decimal number in inclusive [1,1000] |
| Literal range validation | Exact `CssNumber` comparisons before finite numeric conversion |
| Calculations | Existing typed Number math, percentages forbidden, range [1,1000]; computed calculations clamp to range |
| Shared syntax | Existing CSS-wide keyword and deferred substitution paths |
| Metadata | Initial `normal`, inherited, no shorthand or reset-only members |
| Specified serialization | Keywords preserved canonically; existing finite-number/math serialization |
| Computation | `normal`/`bold` become 400/700; relative weights use the fractional parent table, root basis 400 |
| Dependency | Parent computed weight, walked iteratively only when inheritance or relative weight requires it |
| Pending | Font-face descriptors/ranges, font shorthand, other typography; no font selection or painting claim |

Fixtures: `FontWeightPropertyGrammarTests`, `NativeCssFontWeightTests` in the
native suite, and `Views.NativeCssFontWeightTests` in Browser. They cover exact
boundaries, invalid types/arity, specified versus computed values, inheritance,
substitution, deep chains and cancellation. Existing Browser computed `bold`
expectations require explicit migration to `700`; specified CSSOM remains `bold`.
