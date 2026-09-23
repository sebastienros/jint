# Basic CSS math fixture inventory

CSS Values and Units Level 4 Editor's Draft, 20 August 2026, sections 10.8–10.13;
CSS Typed OM Level 1 numeric types, checked 23 September 2026. First-stage functions:
`calc`, `min`, `max`, `clamp`. The other 17 Values 4 functions are pending in the
checked `MathFunctionCensusTests`; these tests make no property-validity claim.

Selected upstream WPT specified-value case family:

| Source at WPT revision `2136eb1501a106c42cd8977bb31c81b57b785bc8` | Applicable expected case | Local fixture |
| --- | --- | --- |
| `css/css-values/calc-serialization.html` | `calc(10px + 1em)` serializes `calc(1em + 10px)` | `MathSerializationTests.ExpectedSpecifiedOutput` |
| `css/css-values/calc-serialization.html` | `calc(10px + 1vmin + 10%)` serializes `calc(10% + 10px + 1vmin)` | `MathSerializationTests.ExpectedSpecifiedOutput` |

WPT is licensed under the [3-clause BSD license](https://github.com/web-platform-tests/wpt/blob/master/LICENSE.md).
Only the stated specified-value assertion is adapted. Computed-style and layout cases in
the upstream suite remain outside this internal stage.
