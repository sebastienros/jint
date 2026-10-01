# Native supports rule checkpoint


> Historical design: the current [LightPanda-style CSS boundary](https://github.com/sebastienros/jint/blob/main/Jint.HtmlParser/README.md#renderless-css-boundary)
> supersedes typed-value, computation, registration, container and keyframe claims below.
> These are retained design notes, not instructions to restore removed features.

This finite stage adds validated `@supports` at stylesheet, media and supports-group level.
It starts from committed Browser native integration `69ccdb6c2`. Imports, containers, keyframes,
animations and conditional groups inside style rules retain their independent named blockers.
The last case needs the nested declarations/context stage and still reports
`C2:nesting-selector-context`, for both media and supports.

The primary algorithms are [CSS Conditional 3 §6](https://drafts.csswg.org/css-conditional-3/#at-supports),
[its CSSOM interfaces](https://drafts.csswg.org/css-conditional-3/#the-cssconditionrule-interface),
[Conditional 4 selector queries](https://drafts.csswg.org/css-conditional-4/#supports-selector), and
[CSSOM remove a rule](https://drafts.csswg.org/cssom/#remove-a-css-rule).

`CssGroupingRule` owns ordered live children and insert/delete staging. `CssConditionRule` supplies
readonly condition text; media and supports have distinct concrete models and generated brands.
Every rule supplies readonly child enumeration, empty on non-container kinds, so ownership traversal
never assumes that every non-media rule is a style rule.

The strict supports parser consumes the original C1 prelude components, options and invocation work.
It distinguishes an invalid prelude (discard the whole rule; insert reports `SyntaxError`) from valid
false (keep the real child rule tree and raw declaration payloads). The existing `CSS.supports()` API
keeps its separate implied-parentheses retry. Pure capability results are cached, and condition text
retains the specified logical structure with safe lexical EOF termination. All grammar branches are
visited even after a result is determined. Unknown features and unimplemented property or selector
capabilities are false, including ordinary negation semantics; known unfinished child rule grammars
remain explicit completion errors. There is no catch converting cancellation or limits to false.

Applicability visits matching groups in source order. A false supports subtree is excluded before
selector matching and typed declaration resolution. Ordinary nested styles retain their nearest style
parent and are otherwise unchanged. Declaration CSSOM reads and stylesheet serialization keep their
existing typed-resolution contract; this stage does not reinterpret pending value grammars as raw
successful CSSOM reads.

Removal clears only the removed root's exposed links. Retained descendants keep historical sheet and
parent links, while the private attachment chain is severed at the root, so their edits cannot advance
the old sheet's stamp. The bounded detach inventory and cancellation checkpoint remain before
publication. Existing nesting assertions that cleared descendant sheet links are corrected to this
CSSOM contract, already asserted by the media model tests.

`CSSConditionRule.conditionText` is readonly in the living specification. Media mutation tests now
write `media.media.mediaText`. `CSSSupportsRule.matches` is a real readonly member in current
Conditional 3 §7.4 and reflects the cached capability result. The checked-in contract must be emitted
with the normal generator; generated C# is never hand-edited.

The focused regression fixtures are `CssSupportsRuleTests`, `NativeCssSupportsBindingTests`, the
existing media/nesting/model fixtures, and all seven `CssCoverageTests`. Coverage preserves the existing
wire range assertions and adds rejection of the group range and a matched-selector rule in a false
group. No build or test result is claimed here: compiler ownership remains with the integration chat
until it grants a serial Release net10 window.
