# Basic CSS math fixture inventory

CSS Values and Units Level 4 Editor's Draft, 20 August 2026, sections 10.8–10.13;
CSS Typed OM Level 1 numeric types, checked 23 September 2026. Implemented functions:
`calc`, `min`, `max`, `clamp`, `round`, `mod`, `rem`. The other 14 Values 4 functions are pending in the
checked `MathFunctionCensusTests`; these tests make no property-validity claim.

`MathSteppedTests` uses authored CSS Values 4 §10.3.1 fixtures for all five rounding
strategies, the mod/rem sign and special-value tables, and §6 line-width snapping.
The line-width cases require a supplied device pixel size; they are draft-new
fixtures and are not claimed as passing cases from the older WPT pin below.

Selected upstream WPT specified-value case family:

| Source at WPT revision `2136eb1501a106c42cd8977bb31c81b57b785bc8` | Applicable expected case | Local fixture |
| --- | --- | --- |
| `css/css-values/calc-serialization.html` | `calc(10px + 1em)` serializes `calc(1em + 10px)` | `MathSerializationTests.ExpectedSpecifiedOutput` |
| `css/css-values/calc-serialization.html` | `calc(10px + 1vmin + 10%)` serializes `calc(10% + 10px + 1vmin)` | `MathSerializationTests.ExpectedSpecifiedOutput` |

WPT is licensed under the [3-clause BSD license](https://github.com/web-platform-tests/wpt/blob/master/LICENSE.md).
Only the stated specified-value assertion is adapted. Computed-style and layout cases in
the upstream suite remain outside this internal stage.
