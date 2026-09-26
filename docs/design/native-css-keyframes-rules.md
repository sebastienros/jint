# Passive classic keyframes checkpoint

This finite stage starts at native Browser integration `29b69cb5e`, reusing the supports/grouping
child enumeration, ownership and root-only detachment model. It models stylesheet rules only.
Animation execution, animation events, timers, visual layout and timeline-range selectors are not
implemented by this change. No vendored WPT fixture or exclusion changes are included.

The normative sources are [CSS Animations 1 §3](https://drafts.csswg.org/css-animations-1/#keyframes),
[§6.2](https://drafts.csswg.org/css-animations-1/#interface-csskeyframerule),
[§6.3](https://drafts.csswg.org/css-animations-1/#interface-csskeyframesrule), and
[CSSOM rule serialization](https://drafts.csswg.org/cssom/#serialize-a-css-rule).

`CssKeyframesRule` derives directly from `CssRule`, type 7; it does not inherit grouping-rule insertion
or index-based deletion. Its stable ordered live child list contains only `CssKeyframeRule`, type 8.
Both receive real generated CSSOM brands. The checked-in binding contract also supplies the current
CSS Animations 1 `length` and indexed getter through the existing collection wrapper/accessor machinery,
without introducing another wrapper class. `CSSKeyframesRule.prototype` still inherits `CSSRule.prototype`.
The `style` adapter is the same rule-declaration adapter used by ordinary styles, with a `CssRule` owner.

The at-rule prelude consumes one decoded string or custom identifier, rejecting unquoted CSS-wide
keywords, `default` and `none`. The CSSOM `name` setter stores its DOMString directly; it does not parse
the value as an at-rule prelude. Serialization escapes the identifier, quoting reserved and empty names.
Only unprefixed `@keyframes` is a producer; vendor-prefixed at-rules remain ordinary unknown-rule recovery.

The child parser consumes a qualified-rule list, independently of style-block declarations or selector
compilation. The classic key grammar accepts `from`, `to` and exact percentages between 0 and 100 inclusive
in a nonempty comma list. Unitless zero is invalid. Order and duplicate selectors are preserved.
`CssNumber` provides exact decimal range checks and equality, including huge exponents and values
infinitesimally outside the bounds. The output uses bounded decimal/scientific spelling, never expands a
huge negative exponent into a correspondingly huge string, and maps signed zero to `0%`.
Known timeline-range selector shapes remain `R3:keyframes-timeline-range-selectors` when a valid stylesheet
name demands their grammar. Invalid append/find/delete input, including those extension selectors, is
respectively a no-op/null/no-op as required by the CSSOM edit methods. Invalid keyText throws `SyntaxError`.

Append always preserves duplicates. Find and delete select the last rule whose full ordered key list
matches exactly after numeric normalization. Key-list work, exact comparisons, list capacity copies,
delete shifts, traversal and serialized output share `CssValueWork`. Parsing, allocation, attachment and
detachment preparation finish before publication, followed by the final cancellation/constraint checkpoint.
Removed roots lose exposed sheet/parent links; descendants keep historical links. Their private attachment
chain terminates at the removed root, so later edits cannot change the former sheet's stamp.

Declarations retain C1 source lazily in the existing Keyframe context. Important declarations are removed
before retention, including custom properties, so they cannot reappear in source serialization or defeat
later normal declarations. Typed property getters and whole-block CSSOM reads retain their existing explicit
completion contract: supported property demand resolves normally; demanded unsupported grammar remains
pending. This stage does not report raw unresolved declarations as a successful typed CSSOM value read.
`ApplicableStyleRules` never enters definitions or their frames, even inside active media/supports groups.
Serialization and source ranges retain real rules when their value grammars are available, with safe EOF
termination and child ranges excluding the parent's indentation.

The source regression packets are `CssKeyframesRuleTests` and `NativeCssKeyframesBindingTests`. They cover
classic positive/negative/range/exponent grammar, names and setters, ordered duplicate/last-match edits,
important discard, lazy unsupported values, generated brands/descriptors/receiver checks, ranges and
historical links, deep ownership and parse limits, canceled append/find/delete/output work, unchanged
state on rejection, and geometry/computed-style access with unused keyframes. The existing binding blocker
test now demands a timeline-range selector rather than a completed classic keyframe.

Generated `.g.cs` output is an integration dependency. No compiler, test runner, restore or emitter was
run while producing this source packet; main integration owns the serial Release net10 validation slot.
The three WPT dom/events/handler-count variants' unused `@keyframes fade-out` definitions can now be parsed
without claiming that any animation runs. Their actual results remain for integration validation.

Astra review follow-up: number serialization scans every raw exponent digit through charged,
saturating arithmetic instead of an unbounded `Int32.TryParse` pass. A regression with 100,000
leading exponent zeros isolates cancellation during that second scan. The Browser find adapter
now applies the existing rule-stamp work guard across all checkpoints, including the final
checkpoint before indexing the live list. Callback-driven deletion during final and scan checkpoints
must report the native invalidation error. Name/keyText getters snapshot their immutable returned
string before charging output, so callback changes cannot substitute an uncharged return value.
The additional adapter fixture is `NativeCssKeyframesWorkTests`; compilation remains unclaimed.
