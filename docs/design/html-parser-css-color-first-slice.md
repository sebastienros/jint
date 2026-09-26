# First internal shared-color slice

Approved finite V0d1 continuation, 2026-09-26. This implements shared color specified-value
parsing and narrow `color`/`background-color` consumers, not Browser cutover, computed cascade,
painting, wide-gamut conversion, or completion of the full color/CSSOM gate.

## Scope and primary sources

- Color 4 editor draft, 13 September 2026: https://drafts.csswg.org/css-color-4/
  §§4–8 grammar (absolute rgb/rgba, hsl/hsla, hwb; complete named/hex/current/system colors,
  including deprecated system keywords; exact `color(srgb …)` grammar as the round-trip companion
  required by §16.2.2 for missing RGB channels), §15 declared-value rules, §16 CSS serialization.
- Color 5 current editor draft: https://drafts.csswg.org/css-color-5/#color-syntax
  Its additional functions and relative `from` syntax remain named obligations in this slice.
- CSS Backgrounds 3: https://drafts.csswg.org/css-backgrounds-3/#background-color
  `background-color` initial transparent, non-inherited; Color 4 `color` initial canvastext,
  inherited. Only ordinary style/keyframe contexts are registered.
- Existing V0 numeric/math/reference work contracts remain authoritative. Parse C1 once;
  preserve original coordinates, maximum nesting depth, work/cancellation and deferred identity.

## Representation and acceptance

Internal immutable color kind/space/channel discriminants retain contextual keyword identity,
original typed numeric/math channels, missing components and alpha. No accepted raw string or
universal RGBA byte tuple. Named-color data is authored from the spec table with recorded provenance.
Default results have no readable payload. The shared matcher distinguishes Match, NoMatch and named
RequiresLaterGrammar; the property dispatcher maps these to Valid, Invalid and UnimplementedGrammar.

Absolute legacy comma and modern space/slash functions validate exact arity, separator mode,
legacy RGB channel-type consistency, modern missing components and production-specific numeric
alternatives. Existing typed math parsing is reused. A bounded iterative evaluator resolves
remaining environment-independent arenas using the existing math operation implementations.
Environment-dependent coordinates yield `color:channel-environment`, never a guessed color or CSS
invalidity. Original math payloads remain available even where declared serialization resolves them.

Specified serialization preserves lowercase authored named/system/transparent/currentcolor identity.
Other sRGB notations obey Color 4's declared-value resolution/serialization algorithms. Missingness
is retained according to the applicable conversion and modern serialization rules. Literal ranges,
calculation ranges and legacy/modern alpha serialization are separate decisions. Ordinary numbers
reuse CssMathSerializer.SerializeFiniteNumber; color alpha uses Color 4's precision/rounding rules.

Known unfinished forms remain named blockers: lab/lch/oklab/oklch, other color() spaces/profiles, relative color forms,
color-mix(), light-dark(), contrast-color(), device-cmyk(). Unknown functions/identifiers are NoMatch.
An unfinished grammar blocks staged declaration mutation atomically. References are analyzed before
color matching by the existing V0 consumer; no separate substitution grammar or fake resolution.

## Verification and ownership

New production files under Css/Values/Colors and matching tests, plus narrow property
metadata/dispatch/result payload changes only. No Browser, DOM, syntax, selector or sheet edits.
Test named-table census and every entry, hex arities/escapes, exact positive/negative function grammar,
missing channels, hue/ranges/normalization, specified serialization and reparse, math/nonfinite values,
unknown-versus-pending forms, deferred values, atomic declaration mutation, original quotas/nonzero
spans, hostile wide/deep/long values and deterministic cancellation during actual post-C1 work.
Fresh Release CSS tests on net8/net10 and unchanged public API snapshots gate this slice.

Declared `color(srgb …)` remains in that notation with extended-range channels; math keeps its
specified wrapper and percentage type. RGB/HSL/HWB declared values use §15.1 historical resolution.
Floating point alpha uses §16.1.1’s non-byte branch with six decimal places and ties towards +∞; hex
alpha therefore preserves its normalized binary64 coordinate rather than adopting a byte tuple.
Current WPT Color 4 parsing fixtures still serialize missing RGB/HSL/HWB as zero and predate the
current draft’s §16.2.2 preservation rule. Original tests pin the current draft for that divergence.

The §15.1 illustrative `hsl(38.82 …)` output rounds inconsistently with §7.1: the normative
conversion yields green 38.82 / 60 × 255 = 164.985. The original fixture tests that algorithm
result, rather than copying the example’s 165.2. Input token limits count the `calc(` token
including its opening parenthesis; child parsing retains that original limit.

The finite inventory is 148 named RGB rows, 19 current system identities and 23 deprecated ones.
The original fixture freezes the independent sorted named table with SHA-256
`99935F50BEF1574B1A467344006246E1DB7929F6AB0B282D17CBAF74B37165F1`.
Color matching does not initialize a property registry/model: the narrow declaration consumer
requests it after V0 analysis. Component matching keeps only the finite grammar prefix of a
hostile wide list, while C1 retains and enforces the original full-source limits.

HSL conversion applies each bounded channel coefficient before multiplying saturation and
lightness. This avoids an overflowing amplitude turning a zero coefficient into NaN; the
zero coefficient contributes no amplitude even at extreme finite literal or calculated values.
`round(line-width, …)` is device-pixel dependent even with absolute lengths; a surviving typed
node returns `color:channel-environment`, just like a surviving relative dimension.
