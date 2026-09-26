# Internal CSS property core checkpoint

This stage implements property parsing only. It does not publish validated CSSOM or enable Browser
cutover. The public parser facade, syntax editors, selector implementation and public snapshots are
unchanged. The authoritative scope remains `html-parser-cssom.md` sections 5–9 and its exact appendix.

## Implemented stage

`Css/Values/Properties/CssPropertyCatalog.cs` records all 433 pinned registrations with their V1–V9
family obligations. Pending rows are obligations, not metadata or declarations of supported contexts.
`CssPropertyRegistry` contains real initial/inheritance/shorthand metadata only for the completed core:
`display`, `visibility`, `opacity`, `position`, `pointer-events`, `box-sizing`, `z-index`, `overflow`,
`overflow-x`, and `overflow-y`. These entries are available in style/keyframe contexts. Other context
uses remain explicit `context-audit` blockers. V9 descriptor-only names are unsupported in ordinary
style/keyframe declarations. `all` has its own named V0 reset blocker outside the pinned 433.

`CssPropertyParser.Parse(name, text, context, options, cancellationToken)` is the standalone text
entry. The component-consuming overload accepts `CssReferenceInput` plus a shared `CssValueWork`.
Its result distinguishes `Valid`, `Deferred`, `Invalid`, `UnsupportedProperty`, and
`UnimplementedGrammar`; the default result has no readable payload. Accepted values retain numeric,
math or reference payloads, rather than an arbitrary unvalidated string. Overflow has a parsed pair;
expansion belongs to the next declaration stage. CSS-wide keywords and custom/reference values use
existing V0 implementations. Out-of-range opacity specified values are retained. Integer math retains
specified calculations; computed rounding belongs to evaluation. Overflow `overlay` is the current
standard's legacy alias of `auto`.

The authorized narrow `CssReferenceInput.FromComponents(source, components, maxNestingDepth)` factory
consumes the immutable priority-stripped C1 list with original source coordinates. It does not
retokenize, rebase spans or reset source quotas. Nested var/env regression tests exercise a declaration
beginning at a nonzero source offset. The source argument must remain the original source because V0c
uses lexical source offsets. Before retaining individual values from large sheets, the reference owner
still needs a source-slice/origin representation so a small deferred value does not retain a whole sheet.

The review correction removes only boundary CSS whitespace tokens from retained value slices, keeping
NBSP, other non-ASCII identifier content, escaped whitespace and escape terminators intact. Literal
opacity percentages serialize as equivalent numbers (Color 4 §17); integer z-index literals serialize
their canonical decimal digits directly without double conversion or rounding.

Fresh Release focused tests after correction: 61 pass on net8.0 and 61 pass on net10.0. These fixtures cover exact positive
and negative grammars, serialization, escaped/case keywords, metadata, distinction of pending/unknown,
reference payloads/origins and cancellation. This is a focused internal gate, not full conformance.

## Resume in finite stages

1. Complete the context/standards audit of the pending catalog rows; add real family validators and
   descriptor contexts. No pending row may be treated as invalid or fabricated support. Extend the
   catalog check to compare each assigned name/group against the design appendix, not only its count.
2. Implement a new internal validated `CssDeclarationBlock`: ordered identities, duplicate importance,
   atomic set/remove/cssText, invalid-priority ordering, retained custom token semantics, shorthand
   expansion/serialization, explicit pending shorthand values and mutation stamps. Consume C1 once;
   reject completion blockers explicitly, without silently dropping them as invalid CSS.
3. Implement validated sheet/rule/live-list identities, strict insert/delete DOM errors and recovery,
   typed style-selector mutation, rule-specific serialization and attached ancestor stamps. Separate
   exposed parent links from attachment propagation and test retained detached descendants. Known
   unfinished rule grammars must remain named blockers. No public facade publication yet.
4. Add genuine media-query grammar and mutable media lists. Other R1–R7 grammars remain separate
   obligations. Nested selectors/declarations need the C2 nesting/scope context, currently absent from
   `SelectorParseContext`; do not simulate it with forgiving or relative selector compilation.

Use the existing selector compiler read-only. Its internal Worker can consume C1 prelude components;
its text convenience entry retokenizes and must not be used per stylesheet child. Selector canonical
serialization needs an agreed seam before native rule serialization can claim completion. Shared
C1 lexical serialization helpers are currently private; coordinate a bounded component serializer
rather than creating another tokenizer or copying a second token grammar.
