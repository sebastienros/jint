# CSS substitution analysis fixtures

All assertions in these tests are authored against CSS Variables Level 1 §§2–4,
CSS Values Level 5 Appendix A, CSS Environment Variables Level 1 §3, and
CSS Syntax Level 3, as reviewed on 2026-09-23. They exercise this internal
syntax analyzer, not computed style or a browser's CSSOM.

The repository WPT pin is `2136eb1501a106c42cd8977bb31c81b57b785bc8`.
No assertion is claimed as adapted from that pin: direct access to files at
the exact revision was unavailable during this implementation. The current
`css/css-variables/variable-reference.html` was inspected for historical
case shapes, but its current contents are not asserted to match the pin.
Dynamic-name, short-circuit and spread cases are draft-new authored fixtures.

## C6s execution fixtures (2026-09-25)

The seven `Substitution*Tests.cs` files are independently authored execution tests.
No assertion in this addition is claimed as copied or adapted from a pinned WPT file.
Primary sources consulted for this implementation and review:

- [CSS Variables 1 §3](https://drafts.csswg.org/css-variables-1/#using-variables):
  decoded, case-sensitive names, missing versus guaranteed-invalid versus empty values,
  selected fallback, inherited computed leaves, and animation-taint eligibility.
- [CSS Values 5 Appendix A](https://drafts.csswg.org/css-values-5/#substitute-arbitrary-substitution-functions):
  active context cycles, root context guarding, whole-invocation early spreads,
  post-early argument grammar before branch selection, wrapper removal before normal
  header substitution, ordinary-container traversal, and lazy ordinary fallbacks.
  The provisional-wrapper regression/control pairs were authored during independent
  review against steps 2.1–2.4, rather than copied from the historical WPT corpus.
- [CSS Values 5 expansion policy](https://drafts.csswg.org/css-values-5/#long-substitution):
  the reviewed internal ceilings of 65,536 lexical occurrences and 1,048,576 UTF-16
  spelling units. Exact-edge, one-past, repeated sharing, EOF closure, fan-out, alias
  depth and cancellation cases test this implementation policy, not a universal UA limit.
- [CSS Env 1 §3](https://drafts.csswg.org/css-env-1/#using):
  decoded names and complete ordered nonnegative integer index vectors, including
  authored huge-integer, signed-zero, canonical-duplicate and absent-dimension cases.
- [CSS Syntax 3 component values](https://drafts.csswg.org/css-syntax-3/#component-value):
  direct component projection, unchanged token payloads, token boundaries, retained
  ordinary containers and synthetic EOF closers.

Consumer tests feed projected components directly into the existing primitive and math
parsers. They do not serialize or reparse the replacements. Source-origin, immutable
snapshot and segment-ownership assertions are authored tests of the reviewed C6s
adapter contract. Pending-feature assertions preserve V0c1's conservative source gate;
they are implementation capability boundaries, not claims of CSS invalidity.
