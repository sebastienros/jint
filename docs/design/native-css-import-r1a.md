# Native stylesheet imports: finite R1a

Specification: [CSS Cascade §2](https://drafts.csswg.org/css-cascade-5/#at-import)
and [CSSOM CSSImportRule](https://drafts.csswg.org/cssom/#the-cssimportrule-interface).

## Native contract

- `CssImportRule` is rule type 3, with decoded specified `Href`, one stable live
  `Media`, and nullable `StyleSheet`. It is a statement, never a grouping rule.
- R1a accepts string/URL plus the existing media-query grammar. `layer`, `layer()`
  and `supports()` remain named `R1:import-prelude-*` completion blockers.
  Namespace remains separate `R1:namespace` debt. Unsupported import conditions
  are never stripped or interpreted as unconditional loads.
- Stylesheet parsing discards invalid and nested imports. A successfully projected
  non-import rule closes import placement; invalid selector recovery does not.
  Insertion checks index bounds, parses exactly one rule, checks hierarchy, then
  mutates. Grouping-rule insertion rejects imports after parsing.
- Serialization writes only `@import url("specified href") media;`; it never
  flattens the imported sheet or reads its declarations. Parent ranges cover
  parent rules only.
- `SetStyleSheet(child, finalSourceUrl, finalBaseUrl, work)` publishes the real
  child identity once after bounded validation. A published child cannot be
  replaced or cleared; replace the parent generation instead. Each occurrence needs its own child;
  a previously owned sheet and a cycle are rejected. Browser supplies the final
  response URL, including redirects. The child attachment has `OwnerNode = null`
  and `ImportOwner = actual rule`. A child must still be private when published:
  its initial empty Media is replaced before any Browser wrapper exposure. The child shares the rule's live `Media`.
- Removing/replacing a parent rule detaches its exposed parent links, retaining
  historical rule-to-child ownership. Retained child rules still name that child.
  Changes through the detached import no longer notify the old root.

## Browser handoff

`CssImportPrelude.MayContainImport(source, options, work, cancellationToken)` is
an incremental tokenizer scan, with no syntax parser, token list, selector
compiler, declaration model or AST. It ignores comments, string/URL contents and
nested imports. Escaped at-keywords are decoded by the tokenizer. It keeps scanning
past apparent qualified rules because invalid selectors can recover before a legal
import. A false result proves no candidate top-level import; true is only a hint.
Token, character, depth and cancellation bounds apply. A zero-import completed
resource must keep `Sheet == null` until actual CSSOM/cascade demand.

For candidate resources, the shared Browser `EnsureSheet` materializes **one**
real sheet and discovers actual `CssImportRule` identities from its rule list.
Do not discover imports through serialization or declaration access. Keep fetch
requests, URL cycle detection, generation cancellation, referrers, completion,
redirects and DevTools accounting in Browser. Native code has no network or engine
callbacks. The loader must check that the resource/rule generation is still current
before publishing children; it must not clear retained historical wrappers on detach.

`ApplicableStyleRules` walks child sheets iteratively at the import position,
respecting inherited sheet media and disabled state. The root input's cascade
origin applies to every returned rule. No child flattening changes CSSOM identity.
Query consumers must capture and verify **every** revision from
`root.ImportedStyleSheets(work)`, including disabled or nonmatching children.
Child mutations intentionally do not recursively bubble sheet stamps. The list
includes the root and protects traversal from repeated identities.
Use `rule.ParentStyleSheet?.EffectiveOwnerNode(work)?.TreeShadowRoot` for author
scope; a child's direct `OwnerNode` is necessarily null. All import graph and scope
walks are iterative, charge shared work and terminate on repeated identities.

## Integration

This branch starts at reviewed `309ca9a41`. Passive keyframes commits
`7088c9536` and `d1a341ab6` independently touch the rule enum, BuildShallow,
BuildRule and serializer. Preserve both sets of cases during integration; do not
copy/cherry-pick keyframes into this native import branch. Browser must update old
fixtures that expect an ordinary import to be a completion blocker. Compiler,
routine Release/net10 tests and emitter runs belong exclusively to integration.
