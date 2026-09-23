# Basic CSS math fixture inventory

CSS Values and Units Level 4 Editor's Draft, 20 August 2026, sections 10.8–10.13;
CSS Typed OM Level 1 numeric types, checked 23 September 2026. Implemented functions:
`calc`, `min`, `max`, `clamp`, `round`, `mod`, `rem`, `abs`, `sign`, `sin`, `cos`, `tan`,
`asin`, `acos`, `atan`, `atan2`. The other five Values 4 functions are pending in the
checked `MathFunctionCensusTests`; these tests make no property-validity claim.

`MathTrigonometricTests` adapts the numeric assertions and specified serialization
cases below from WPT at the pinned revision. It also has authored CSS Values 4
§§10.4–10.4.1 fixtures for degree cardinals, radian separation, domains,
percentage dependencies, and the full signed-zero/infinity `atan2` table. The
table's −180deg entries intentionally follow the detailed §10.4.1 table at the
negative X-axis, including the disputed lower endpoint; these are authored
spec fixtures and are not credited to WPT.

`MathSignTests` uses authored CSS Values 4 §10.6 fixtures for grammar, result typing,
percentage and relative-unit dependencies, IEEE signed zero, infinities and NaN.
No computed percentage basis is supplied by this specified-value stage.

`MathSteppedTests` uses authored CSS Values 4 §10.3.1 fixtures for all five rounding
strategies, the mod/rem sign and special-value tables, and §6 line-width snapping.
The line-width cases require a supplied device pixel size; they are draft-new
fixtures and are not claimed as passing cases from the older WPT pin below.

Selected upstream WPT specified-value case family:

| Source at WPT revision `2136eb1501a106c42cd8977bb31c81b57b785bc8` | Applicable expected case | Local fixture |
| --- | --- | --- |
| `css/css-values/calc-serialization.html` | `calc(10px + 1em)` serializes `calc(1em + 10px)` | `MathSerializationTests.ExpectedSpecifiedOutput` |
| `css/css-values/calc-serialization.html` | `calc(10px + 1vmin + 10%)` serializes `calc(10% + 10px + 1vmin)` | `MathSerializationTests.ExpectedSpecifiedOutput` |
| `css/css-values/acos-asin-atan-atan2-serialize.html` | `acos(1)` → `calc(0deg)`, `asin(0.5)` → `calc(30deg)`, `atan2(infinity, infinity)` → `calc(45deg)`, and other listed specified cases | `MathTrigonometricTests.PinnedWptSpecifiedSerialization` |
| `css/css-values/acos-asin-atan-atan2-computed.html` | `atan2(1,-1)` → `135deg`, `atan2(-1,1)` → `-45deg`, `asin(sin(0.25turn))` → `90deg`, and other listed resolved numeric cases | `MathTrigonometricTests.PinnedWptResolvedSamples` |

WPT is licensed under the [3-clause BSD license](https://github.com/web-platform-tests/wpt/blob/master/LICENSE.md).
Only the stated specified-value assertion is adapted. Computed-style and layout cases in
the upstream suite remain outside this internal stage.
