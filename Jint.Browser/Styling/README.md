# Native styling boundary

Browser follows the [renderless CSS boundary](../../Jint.HtmlParser/README.md#renderless-css-boundary).
`NativeCssStyleSheets.Install` stores owner identity, source/base URLs, source text and resource
revision. It does not parse inline attributes, run the cascade or perform layout. `NativeCssParsing`
owns demand-driven rule/media caches; inactive bodies stay raw until CSSOM inspection or matching
requires them. Imports load without parsing unrelated rule bodies.

`NativeCssQuery` memoizes matching sources and requested text values for one synchronous query.
It checks document, resource, imported-sheet graph, inline-block and Browser read witnesses.
Matching occurs once per element/query, including native shadow scope and slot/host inheritance.
No state is attached to native nodes. Origin, importance, encapsulation, inline style, layer rank,
specificity and source order select declarations; inherited properties and catalog defaults fill gaps.
Explicit revert/revert-rule/revert-layer values retain cascade rollback.

Values are text, not typed grammars. Custom properties inherit declared text. Ordinary values may
use bounded textual `var()` substitution with fallbacks (depth 32; expansion limit 1,000,000 characters).
It is not token substitution: no declaration-scope resolution, deferred shorthand evaluation, typed
functions, URL resolution or unit computation. Colors stay declared text in the cascade; `ResolvedStyle`
serializes color longhands as `rgb()`/`rgba()` only at the CSSOM and DevTools read boundary. A missing/cyclic expansion defaults
the property rather than selecting an earlier declaration. Substitution-produced rollback keywords
remain text. Simple display blockification stays; overflow axes no longer compute together.

The data catalog supplies a small initial-value table and inherited flags; other known names have
empty initial text. Only layout-used shorthands expand by whitespace splitting. `ResolvedStyle`
returns synthetic box width/height where available and text otherwise. Text extraction interprets
white-space keywords locally. DevTools and CSSOM consume the same values and native rule identities.

Style, media, supports, layer, import and font-face rules retain real models and mutation identities.
Other at-rules are plain CSSRule objects preserving cssText and never entering the cascade.
Font-face descriptors remain passive text. CSS.supports tests known/custom names and nonempty values,
with the existing condition/selector parser, not property grammars.

Keep `CssValueWork` and selector work checks in surviving loops. Cancellation and reentrant edits
must not publish obsolete state. Computed declarations stay read-only and live across later reads;
borrowed cross-realm mutator exception branding remains separate binding work.
