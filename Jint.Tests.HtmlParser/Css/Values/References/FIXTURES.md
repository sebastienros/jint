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
