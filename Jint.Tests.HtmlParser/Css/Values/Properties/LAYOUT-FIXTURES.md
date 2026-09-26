# Native layout property grammar slice

Primary editor drafts checked 2026-09-25:

- CSS Sizing 3 §3.1/3.2: https://drafts.csswg.org/css-sizing-3/#preferred-size-properties
  Width/height, auto/intrinsic/stretch keywords, nonnegative length-percentage atoms,
  typed math with destination range, fit-content(length-percentage).
- Flexbox 1 §§5.1–5.3/7.1/7.2: https://drafts.csswg.org/css-flexbox-1/#flex-property
  Flex-direction/wrap, nonnegative grow/shrink, basis (including content), flex/flex-flow
  expansion, ordered factor group, unitless-zero disambiguation, omitted zero-length basis.
- Alignment 3 §§4.1/4.2/6/7: https://drafts.csswg.org/css-align-3/#align-items-property
  Four item/self longhands, safe/unsafe, baseline modifiers, justify-only left/right,
  justify-items legacy unordered pair, place-items/place-self copying and expansion.
- Writing Modes 3 §2.1: https://drafts.csswg.org/css-writing-modes-3/#direction
  Direction ltr/rtl and inherited initial metadata.
- CSSOM §6.6.1: https://drafts.csswg.org/cssom/#css-declaration-blocks
  Generalized longhand expansion/reconstruction, common priority, CSS-wide keyword
  rules, remove/reset and shared pending shorthand identity.

Fixtures are independently authored in LayoutPropertyGrammarTests and CssLayoutDeclarationTests.
This is an internal specified-value grammar gate, not layout or full WPT conformance. Existing
finite numeric conversion and six-decimal serialization policy remains in force. Anchor-size()
and calc-size() are explicit named sizing blockers. Other catalog properties (including min/max
sizing, content alignment and place-content) and non-style/keyframe contexts remain pending.
No unsupported function receives an arbitrary raw token payload. Custom properties and var/env
continue through the existing V0 reference consumer before family dispatch.
