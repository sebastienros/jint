# Native CSS contract receivers

The generated CSS contract uses the native semantic models: style and media rules,
their actual stylesheet and rule lists, media lists, and the Browser declaration
adapters shared by specified and computed styles. `MediaQueryList` uses its existing
runtime receiver and receiver check. Inline declarations and stylesheet discovery
remain demand driven.

The native semantic producer rejects recognized unsupported rule grammar before
publishing a replacement or insertion. Unsupported CSS interfaces retain their
contract names, prototypes and descriptors but use `NativeCssUnavailable`, an
abstract Browser-only marker with a private constructor and no instances or
subclasses. Their own members check that receiver before the defensive named
unsupported refusal. Borrowing such a member onto a real style or media rule is
therefore an illegal invocation, rather than a fabricated semantic answer.

The validated producer also rejects nested rule grammar before publishing a style
rule. The legacy `CSSStyleRule.cssRules` and `rules` aliases expose a stable empty
native rule list per owner. They do not admit nested insertion or share a list
between distinct owners.

CSS mutations use the native Browser adapters with per-invocation work and
cancellation, rather than ambient callbacks on parser models. All existing
interface names, member descriptors, arities, constants and prototype parent links
are preserved by this migration.
