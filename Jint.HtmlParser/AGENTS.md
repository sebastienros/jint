# Agent instructions: the native markup parser

> Read this before touching `Jint.HtmlParser/`, its tests, generated vocabularies or package contracts.
> Read the repository [AGENTS.md](../AGENTS.md) first. The [contributor guide](CONTRIBUTING.md) contains
> implementation recipes; the [package README](README.md) owns embedding usage and threading limits.

## Ownership and public contracts

The dependency is Browser → parser. Never reference Jint, Browser or AngleSharp from the parser;
AngleSharp is a benchmark comparison control only. Native nodes own identity, tree/attribute links,
namespaces, template/shadow relations, mutation records and intrinsic HTML control state. Browser owns
script realms, WebIDL wrappers, scheduling, networking and synthetic layout. Do not add a second DOM
store or eager per-node Browser work. Hooks are engine-free interfaces/delegates with a cheap unused guard.
[The Browser integration contract](CONTRIBUTING.md#browser-integration-contract) names the reviewed seams.

Default new members to the narrowest visibility. ACC-06 in the [acceptance tracker](../docs/design/html-parser-completeness.md)
is open: public snapshots and internal production use do not freeze the public API. Preserve the experimental
package-version policy and Browser's exact packed dependency on that calculated version. Positional
record structs are public layouts, not an incidental refactor. Review changed public and Browser-internal
surface snapshots and run the unsigned packed consumer; see [acceptance checks](CONTRIBUTING.md#verification-and-acceptance).

## Parsing, limits and failure boundaries

Implement current WHATWG HTML/DOM and applicable XML/CSS algorithms; cite the section being changed.
The pinned html5lib corpus is regression evidence, not authority over later normative changes. HTML
recovers malformed markup; XML is strict; fragments belong to the context owner document without
replacing its existing children. Scripting-enabled HTML changes grammar without executing script.
No native parse or serializer fetches resources or resolves external XML entities.

Do not replace finite XML entity defaults with `Unbounded`: `new ParseLimits()` caps replacement units
at 10,000,000, while zero explicitly disables an individual bound. Other parse bounds default to zero;
`MaxNestingDepth` bounds CSS nesting, not DOM depth. Preserve `ParseLimitException` separately from
syntax diagnostics and bounded collectors. Browser supplies its own budgets and constraint-poll seams.
Every long path must charge cooperative work and honor cancellation, including XML expansion and copies.
Do not catch host cancellation/constraint failures as recoverable syntax errors.

Keep tree walks, serialization, cloning, selectors and XPath navigation iterative for hostile depth.
Sessions retain only owned input/state across yields; borrowed arrays and reusable tokenizer buffers
must not escape as immutable DOM values. Preserve original UTF-16 offsets, atomic mutation boundaries,
mutation-stamp checks and coherent cancellation/notification failure. Serialization limits must not
return partial output. XPath cancellation brackets BCL work and is not a hard timeout inside it.

## Demand and threading

Document construction performs grammar-required repair and control history. Embedded CSS, SVG values,
selectors and script text remain uninterpreted until the consumer explicitly demands them. Public CSS
methods return immutable syntax; internal CSSOM follows the [renderless text boundary](CONTRIBUTING.md#renderless-css-boundary).
Do not restore eager typed property evaluation or let one unsupported nested rule poison a whole cascade.

After safe publication, immutable links/names/data/attribute inspection and independent serialization
may run concurrently. DOM string materialization uses a separate atomically published reference cache;
never rewrite a multi-field source slice from a getter. Mutation, parsing sessions, live ranges/iterators,
subscriptions and mutable state/index initialization require synchronization with all readers. Selector/id
queries are among those stateful operations. Browser trees retain page-loop affinity. The precise
[threading contract](README.md#threading) limits which apparent reads may overlap.

## Verification and generated data

Read [the main test instructions](../Jint.Tests/AGENTS.md) before writing tests. Standalone semantics
belong in `Jint.Tests.HtmlParser`; Browser scheduling/bindings belong in `Jint.Tests.Browser`.
Linked Browser cascade partials must also compile against the parser alone. Snapshot changes are
review decisions, not automatic acceptance. Run Release with fresh builds and relevant framework coverage.

Do not hand-edit generated lookups or vendored upstream cases. The contributor guide has
[regeneration commands](CONTRIBUTING.md#regeneration), [corpus checks](CONTRIBUTING.md#parser-corpora)
and [fresh-feed/AOT probes](CONTRIBUTING.md#packed-consumers-and-native-aot).
Exclusions must match exact failing cases with reasons and expected trees; stale passing exclusions fail.
Read [benchmark instructions](../Jint.Benchmark/AGENTS.md) before running or quoting any timing.
