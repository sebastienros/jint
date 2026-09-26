# Selector compiler and matching dispatch contracts

Dispatch contract, 2026-09-23, for C2/C3 and the traversal portion of D4 in
[the architecture](html-parser.md). Follow the existing
[shared contracts and C1 syntax](html-parser-feature-slices.md) and
[native metadata, adoption and template ownership](html-parser-native-followups.md).
This document authorizes no browser cutover or new parser option flags. Compilation, structural
matching, intrinsic HTML state and browser interaction state have separate completion gates.

## Ownership and first dispatches

| Assignment | Exclusive implementation/test files | First deliverable |
| --- | --- | --- |
| D4a traversal owner | New `Jint.HtmlParser/Dom/Traversal/NodeTraversal.cs`; new `Jint.Tests.HtmlParser/Traversal/NodeTraversalTests.cs` | Working internal iterative traversal below; no existing DOM file edits |
| C2a compiler owner | New `Jint.HtmlParser/Css/Selectors/**`; new `Jint.Tests.HtmlParser/Css/Selectors/**` | Working internal compiler, context and specificity; no facade or matching stubs |
| Integration/shared-contract owner | New `Jint.HtmlParser/Parsing/SelectorParseException.cs`; matching shared exception tests; eventual API snapshots | Exact exception below, consumed by C2a; reserve this file before dispatch |
| C2 publication owner | New `Jint.HtmlParser/MarkupParser.Selectors.cs`; visibility changes and API snapshots after implementation | Only implemented public contracts below |

The shared-contract owner supplies the exception before C2a's compilation/test gate. It can remain
internal until publication, as can the context and specificity types. C2a uses their final names and
members with internal visibility; it does not introduce competing temporary option/error types.
`SelectorProgram` and the facade do not exist until matching works. No task edits C1, existing
`Parsing/**`, native metadata/templates or signing files independently. Send required changes to
their existing owner. D4a and C2a can run independently.

D4a adds this exact internal seam in namespace `Jint.HtmlParser`:

```csharp
internal static class NodeTraversal
{
    internal static IEnumerable<Element> DescendantElements(
        Node root, CancellationToken cancellationToken);
    internal static Element? PreviousElementSibling(
        Node node, CancellationToken cancellationToken);
    internal static Element? NextElementSibling(
        Node node, CancellationToken cancellationToken);
}
```

Descendants exclude root and visit ordinary child links in preorder. Implement with first-child,
next-sibling and parent links, without recursion, materializing a subtree, or repeatedly rediscovering
ancestry. Never climb above root. Sibling methods skip non-elements in the indicated direction.
Null required arguments throw `ArgumentNullException`; the iterator validates root when enumeration
begins. All three methods check cancellation before traversal and at least every 256 traversed
links/work steps, including non-element runs and parent-link backtracking. A final ascent through a
deep chain must keep polling even when no new nodes are visited. Enumerables are transient and
single-owner; mutation while enumerating is unsupported.
They retain their root only for enumeration, never through a cache. Queries later materialize results.
Traversal follows neither `TemplateContent` nor its internal `Host`; explicit traversal of that
fragment works. Shadow roots later use their own ordinary tree, not composed/host-including traversal.
No traversal API is added to public `Node` in D4a.

D4a gate: mixed-node preorder, root exclusion, detached trees/fragments, sibling filtering, empty
trees, deep chains without stack overflow, pre-cancellation and cancellation during a long walk.
Include a deterministic deep-chain ascent cancellation test proving that parent-link backtracking
counts toward the polling cadence, including the final ascent after the last descendant.
Add template-boundary coverage when the native template commit lands; do not invent template storage.

## Exact eventual public surface

Context, specificity and program use namespace `Jint.HtmlParser.Css`; the facade and exception use
`Jint.HtmlParser`, matching C1's syntax-result versus common-error placement.

```csharp
public sealed class SelectorParseContext
{
    public SelectorParseContext(
        IEnumerable<KeyValuePair<string, string>>? namespaceBindings = null,
        ParseLimits? limits = null);
    public IReadOnlyDictionary<string, string> NamespaceBindings { get; }
    public ParseLimits Limits { get; }
}

public readonly struct SelectorSpecificity : IEquatable<SelectorSpecificity>,
    IComparable<SelectorSpecificity>
{
    public SelectorSpecificity(int idCount, int classCount, int typeCount);
    public int IdCount { get; }
    public int ClassCount { get; }
    public int TypeCount { get; }
    public int CompareTo(SelectorSpecificity other);
    public bool Equals(SelectorSpecificity other);
    public override bool Equals(object? obj);
    public override int GetHashCode();
    public static bool operator ==(SelectorSpecificity left, SelectorSpecificity right);
    public static bool operator !=(SelectorSpecificity left, SelectorSpecificity right);
}

public sealed class SelectorParseException : Exception
{
    public string Code { get; }
    public long Offset { get; }
    internal SelectorParseException(string code, long offset);
}

// MarkupParser
public static SelectorProgram ParseSelector(string source,
    SelectorParseContext? context = null, CancellationToken cancellationToken = default);

public sealed class SelectorProgram
{
    public SelectorSpecificity MaximumSpecificity { get; }
    public bool Matches(Element element, Node? scopingRoot = null,
        CancellationToken cancellationToken = default);
    public bool TryMatch(Element element, out SelectorSpecificity specificity,
        Node? scopingRoot = null, CancellationToken cancellationToken = default);
    public Element? Closest(Element element, CancellationToken cancellationToken = default);
    public Element? QuerySelector(Node root, CancellationToken cancellationToken = default);
    public IReadOnlyList<Element> QuerySelectorAll(Node root,
        CancellationToken cancellationToken = default);
}
```

No public program constructor, AST, callback, predicate registry, relative-selector mode or engine
binding. Null source/element/root throws `ArgumentNullException`. Null context and null limits select
immutable defaults. Context construction enumerates bindings once into an ordinal-keyed dictionary
and exposes an immutable read-only wrapper, never the caller's collection. Duplicate ordinal keys
throw `ArgumentException`; null keys or values throw `ArgumentException`. URI values are namespace
identifiers, not validated/fetched URLs; empty URI means no namespace. Nonempty keys are already
decoded prefix strings, not source spellings to tokenize. Empty key declares a default element
namespace; missing empty key means no declared default. Store the immutable `ParseLimits` reference;
no options or dictionary mutators exist. Caller enumeration exceptions propagate normally.

Resolve named prefixes at compile time using ordinal comparison. Explicit wildcard `*|` never needs
a binding; explicit `|` selects no namespace. Unprefixed type/universal selectors use the declared
default when one exists, otherwise any namespace; implicit universal selectors follow the grammar's
default-namespace rules as well. Unprefixed attributes select no namespace regardless of the default.
Do not infer declarations from DOM `xmlns` attributes. Browser DOM queries use the empty context;
stylesheet compilation later supplies stylesheet namespace declarations. Escaped identifier prefixes
are decoded before lookup; wildcard syntax and an escaped identifier are distinct token productions.
Retain implicit versus explicit universal selectors in functional arguments. With default namespace
`urn:a`, `*|*:is(.x)` can match an `urn:b` element of class x, whereas `*|*:is(*.x)` cannot. Preceding
compounds such as `.a` in `:is(.a > .b)` retain the normal default-namespace requirement; the exception
is for the subject compound without an explicit type/universal selector. A virtual featureless
fragment scope must not be excluded by applying an ordinary element namespace test to it. Include
these cases in the compiler representation and evaluator gates; see
[the matches-any pseudo-class](https://drafts.csswg.org/selectors/#matches) and
[Selectors' data model](https://drafts.csswg.org/selectors/#data-model).

Specificity constructors reject each negative argument with `ArgumentOutOfRangeException` naming
that argument. Default is `(0,0,0)`. Compare lexicographically, without subtraction overflow; equality
and hashing use all three counts. Compiler-only addition saturates each component at `int.MaxValue`,
never carries into another component. No public arithmetic operators or packed integer encoding.
`MaximumSpecificity` is the static list maximum; `TryMatch` returns the maximum among matching
top-level branches and sets zero on no match. `:is/:not/:has` use the static maximum of surviving
arguments, `:where` contributes zero, and nth-child/nth-last-child add one class count plus the `of`
maximum. This distinction is required by `Jint.Browser/Dom/Views/CssCascade.cs`'s `TryMatch` consumer.
[Selectors specificity](https://drafts.csswg.org/selectors/#specificity) defines the calculation.

## C2a: compiler, errors and limits

Add `internal static SelectorCompiler.Compile(string source, SelectorParseContext? context,
CancellationToken cancellationToken)` returning an internal sealed `CompiledSelector`. Its
`MaximumSpecificity` is an internal getter of the type above. Its private immutable representation
contains ordered top-level branches, resolved namespace tests, decoded identifiers/strings,
combinators, functional child programs, exact An+B operands, and explicit predicate kinds. Keep
source spans needed by errors and tests. Representation layout is compiler-private, not a public AST.
Use heap-backed explicit stacks for nesting and specificity calculation, not recursive descent over
unbounded nested functions. No code-generated delegates capturing documents or engine state.
`SelectorCompiler` and `CompiledSelector` use namespace `Jint.HtmlParser.Css.Selectors`; context
and specificity keep their eventual `Jint.HtmlParser.Css` namespace while internal.

Consume the existing C1 `CssSyntaxParser.ParseComponentValues()` with `CssParseOptions.Limits` set
from the context and no diagnostic collector. This preserves original `CssSourceSpan.Start/Length`,
decoded `CssToken.Text`, raw `NumberText`, `IsInteger`, `IsIdHash`, functions and simple blocks.
Use C1's ordinary lane; UnicodeRange descriptor retokenization does not apply to selectors. Keep
the source available during compilation when token adjacency/raw spelling is necessary; comments
are not whitespace and gaps between spans do not automatically create descendant combinators.
Do not concatenate normalized tokens into a second source with different offsets. C1 tokenization
and component EOF recovery remain effective; selector grammar still rejects malformed productions.

The compiler implements an ordinary complex-selector list at top level. Only `:has` consumes a
relative list; a leading combinator elsewhere is invalid. `:is/:where` use forgiving lists; invalid
branches contribute neither a match nor specificity, and an empty surviving list is valid and matches
nothing. `:not/:has` and nth `of` lists are strict. Nested `:has` and forbidden pseudo-elements are
invalid in their grammar contexts. A forgiving ancestor may discard the containing invalid branch.
Unknown names fail outside forgiving recovery, except the named compatibility production below;
they never become arbitrary false predicates.
[Selectors parsing and grammar](https://drafts.csswg.org/selectors/#grammar) are authoritative.

Implement [Selectors' normative compatibility productions](https://drafts.csswg.org/selectors/#compat):
unknown nonfunctional pseudo-elements whose decoded names start with `-webkit-`, ASCII
case-insensitively, parse successfully and match nothing. Give them an explicit compatibility
predicate kind; this does not admit unknown functional `::-webkit-x()` or unknown pseudo-classes.
Grammar restrictions on pseudo-elements still apply inside real-selector arguments. Recognize
`:-webkit-autofill` as an alias of `:autofill`, sharing its state predicate. Test escaped/mixed-case
names, `#target, ::-webkit-unknown` retaining the target branch, functional rejection, and strict versus
forgiving contexts. Later selector serialization lowercases these unknown compatibility names.

Use these stable error codes: `selector/invalid-syntax`, `selector/undeclared-prefix`,
`selector/unsupported-construct`. Offset is the first offending token's original zero-based UTF-16
start; use the prefix token for undeclared prefixes, the pseudo name token for unsupported pseudos,
and original input length when a required token is missing at EOF. An empty/whitespace-only list is
invalid at EOF. Invalid grammar placement uses invalid-syntax even when that predicate is otherwise
supported. Exception messages are invariant useful descriptions, never the whole input. No selector
diagnostic collector or warning callback is added. Discarded forgiving errors do not escape.

Shared bounds remain inclusive with zero unbounded: input counts original string UTF-16 units;
token counts raw atomic C1 tokens, including delimiters; nesting counts C1 open functions/blocks
starting at one. Do not charge the compiler's work-stack frames as additional CSS nesting or charge
input twice. Entity-expansion limits are irrelevant here. Limit exceptions and cancellation escape
unchanged, including from inside forgiving lists; never catch them as invalid selector branches.
Check cancellation at entry, during long compiler/token/list scans at least every 256 work items,
and before returning. A program stores no cancellation token, context, collector or original full
source after compilation; copy needed names/operands/spans. Limits govern compilation, not later
tree size or matching work. Exact numeric An+B interpretation must not round through `double` or
overflow on large spellings; clamp only where equivalence for all realizable sibling indices is proved.

All required accepted predicate kinds below compile in C2a, including later D7/environment predicates.
This is syntax recognition, not a claim of implemented matching. Tests inspect internal representation
and errors; there are no temporary public `Matches` methods or blanket false state implementations.
C2a gate includes every required grammar family, nested forgiving/strict boundaries, namespace
ownership/default distinctions, specificity, original offsets through escapes/CRLF/comments,
EOF recovery, exact/over-limit cases, huge numeric operands and deeply nested cancellation.

## Required construct inventory

This is the minimum replacement surface, not an instruction to reject other standard constructs
already supported by the pinned dependency. C2a records an explicit accepted pseudo catalogue and
grammar tests; unknown names do not inherit acceptance from a generic identifier production.

| Family | Required constructs/evidence |
| --- | --- |
| Names and relationships | Type/universal, ID/class, namespace forms, lists; descendant, `>`, `+`, `~`, `||`; presence and `=`, `~=`, `|=`, `^=`, `$=`, `*=` attributes with `i`/`s` modifiers. `SelectorSyntaxTests`, `EmptyNamespaceSelectorTests` and vendored `dom/nodes/selectors.js`. |
| Lexical compatibility | Escaped names/commas/pipes, comments at token boundaries, quoted strings and escaped LF/CR/CRLF/FF continuations, recoverable unclosed attributes/functions/comments. `SelectorStringContinuationTests`, `SelectorSyntaxTests`. |
| Logical and structural | `:is`, its accepted legacy `:matches` form, `:where`, `:not`, relative `:has`, `:scope`, `:root`, `:empty`; first/last/only child/type; nth-child, nth-last-child, nth-of-type, nth-last-of-type and child `of` lists; `:lang`, `:dir`. `ForgivingSelectorTests` and vendored selector table/pseudo-class suite. C2c owns logical forms. |
| Native state | `:any-link`, `:link`, `:visited`, `:checked`, `:unchecked`, `:indeterminate`, `:default`, `:enabled/:disabled`, `:required/:optional`, `:valid/:invalid`, `:in-range/:out-of-range`, `:read-only/:read-write`, `:placeholder-shown`, `:open/:closed`. `PagePseudoClassSelectorFactory`, pinned default factory and `Parsing/PagePseudoClassSelectorTests`; C3a/D7 owns intrinsic evaluation. |
| Environment state | `:hover`, `:active`, `:focus`, `:focus-within`, `:focus-visible`, `:target`. Same factory/tests and vendored selector table. Also `:autofill` and its required `:-webkit-autofill` alias use one environment state predicate; no-host has no user-agent autofill state. |
| Pseudo-elements | Pinned identifier catalogue: before, after, selection, footnote-call, footnote-marker, first-line, first-letter, content, checkmark, picker-icon; legacy single-colon forms only where allowed. Also constructor-owned `::picker(select)` and `::slotted(...)`, whose current parser debt is recorded in `WptBrowserExclusions`. C2a owns parsing and C2b owns correct element-subject nonmatching. |
| Shadow predicates | Pinned `:host` and `:host-context(...)`, plus current-standard functional `:host(...)`. C2a owns recognition; native shadow ownership/evaluation is a separately gated D7 shadow assignment consumed by C3a. |
| Column predicates | `||`, `:nth-col(...)`, `:nth-last-col(...)` are accepted by the pinned parser; C2a owns grammar, C2d owns table-semantic evaluation. |
| Compatibility production | Unknown nonfunctional `::-webkit-*` pseudo-elements, including decoded escapes and mixed case, parse and match nothing; unknown functional forms remain invalid. This is the explicit normative exception above, not a general unknown-name fallback. |
| Deliberate legacy rejection | Pinned nonstandard `:shadow` and `:contains(...)` are rejected as the explicit standards correction below. C2a owns rejection/recovery tests; browser cutover owns SyntaxError regression tests. No matcher or feature flag is added. |

Repository paths above are under `Jint.Tests.Browser/` unless qualified; the vendor table is
`Jint.Tests/Wpt/Vendor/dom/nodes/selectors.js`. Existing JS entry points also include
`webkitMatchesSelector`, element/detached-element/document/fragment queries and closest. Bindings
preserve receiver checks, one-time string coercion and SyntaxError translation; these are later
browser integration work, not compiler options. The catalogue includes the defaults delegated to by
the page factory, verified against AngleSharp 1.8.2 commit
`35b26db83557a6a74ce286907833e3b17806d9eb` (`DefaultPseudoClassSelectorFactory`,
`DefaultPseudoElementSelectorFactory`, and `CssSelectorConstructor`'s functional entries).
Any additional current construct discovered during migration gets an explicit grammar/predicate and
owner, not silent loss under a smaller whitelist. Remaining WPT exclusions are debt, not desired semantics.

The reviewed compatibility decision is to reject legacy `:shadow` and `:contains(...)`, rather than
carry these nonstandard AngleSharp extensions into the new API. The pinned implementations tested
`ShadowRoot != null` and delegated to `TextContainsMatcher`, respectively. The browser, tests,
fixtures and vendored WPT search found no selector consumers of these extensions; broad `shadow`
hits were Tailwind class names. Record this before/after change in the browser cutover: formerly
accepted dependency extensions become SyntaxError through the browser failure guard. C2a emits
`selector/unsupported-construct` at the pseudo name outside forgiving recovery; strict lists fail,
while `:is/:where` discard the invalid branch. Test decoded escaped and mixed-case names, strict
lists, forgiving parents and browser error translation. Do not introduce legacy aliases or flags.

`:unchecked` is different: it is a current [Selectors predicate](https://drafts.csswg.org/selectors/#checked),
owned by D7/C3a. A checkbox matches only when unchecked and not indeterminate; a radio matches only
when unchecked and its live radio group contains a checked radio; an option matches when it is in
a select and is not selected. Other elements do not match; disabledness alone does not exclude a
candidate. Do not implement this as generic negation of `:checked` or retain obsolete menuitem
behavior. Preserve existing HTML-specific `:checked` behavior independently of indeterminateness,
per [HTML selector rules](https://html.spec.whatwg.org/multipage/semantics-other.html#selector-checked);
adding unchecked does not rewrite checked. Fixtures cover checked/unchecked/indeterminate checkboxes,
radio groups with/without a checked member, live group/form-owner mutations, selected/unselected
options within and outside select, disabled candidates and unrelated elements.

`||` has existing valid-syntax tests. It moved to
[Selectors 5's column combinator](https://drafts.csswg.org/selectors-5/#column-combinator); column
membership follows document-language table semantics, not rendered layout. Keep it in the compiler
and assign actual matching below. Do not infer full matching from a successful parse.

## Matching contracts and finite commits

A branch whose subject is a pseudo-element does not match its originating Element. Thus
`#target, ::before` still returns the ordinary target while `#target::before` does not return that
Element. Preserve pseudo-element syntax/argument validation even though these element-only APIs
produce no pseudo-element results; do not strip pseudo-elements before evaluating a branch.

`Matches`/`TryMatch` default the scoping root to the tested element. An explicit root controls scope
interpretation, not query-result filtering. `Closest` checks self then element ancestors and retains
the original element as scope. Queries use the supplied root as scope and return strict element
descendants, in preorder, deduplicated by identity. The subject must be in that subtree, but ancestor
parts of a selector may match outside it. Document scope uses the document element; fragment scope
is a virtual featureless root, not its owner document or host. Virtual roots can anchor relationships
but cannot be result elements. Preserve ordinary tree/shadow boundaries. See
[DOM scope matching](https://dom.spec.whatwg.org/#scope-match-a-selectors-string) and
[Selectors scope](https://drafts.csswg.org/selectors/#the-scope-pseudo).

QuerySelector stops at the first match; QuerySelectorAll returns an immutable static list, empty
when nothing matches. Do not expose a mutable backing array/list through a cast. Later mutations
change node state, not membership/order in an earlier result. Leaf Node roots yield empty queries;
browser exposure remains limited to the DOM interfaces that own these methods. Evaluation reads
current tree/document/native state on every call. Programs can run concurrently on independently
owned trees; concurrent mutation of a tree remains unsupported. No result or node cache lives on
the compiled program. Cancellation occurs at entry, within expensive traversal/backtracking scans
at bounded intervals, and before returning; it never returns a partial list.

| Commit | Concrete implementation and gate |
| --- | --- |
| C2b structural evaluator | Internal matcher over D4a links: names/attributes/namespaces, four ordinary combinators, structural/nth predicates, query ordering/identity, scope and closest. Test HTML versus XML/SVG casing, quirks ID/class rules, ancestor matches outside query root, virtual fragment scope, post-mutation answers and static result immutability. Native document-mode storage belongs to the metadata/HTML owner; do not add a selector-mode flag or infer quirks from MIME/root name. This dependency must land before its matching gate. |
| C2c relational evaluator | Iterative evaluation/backtracking for logical functions, relative `:has`, and nth filtered sibling lists; specificity of matching list branches. Gate sibling-relative and descendant-relative has, nested forgiving functions, strict failures, large/deep inputs, cancellation in unsuccessful scans and no recursion overflow. No arbitrary depth-64 fallback. |
| C2d columns | Implement table column membership including spans against native structure and dedicated fixtures. Until then column programs are compiler-only, not a completed public matching feature. |
| C3a/D7 intrinsic predicates | Native algorithms for language/direction, links, editing, control applicability, form ownership/defaults, checked/selectedness, disabled fieldset/first legend, radio groups, validity/range, placeholder and open state. Split by the D7 control families; use one native answer across selectors and reflected APIs. Gate standalone documents without a browser, detached controls, mutations and aggregate form/fieldset behavior. |
| C3b environment | Internal evaluation-time adapter for hover/activation/focus/focus-within/focus-visible/URL target. Browser owns interaction records and URL target resolution; programs retain none. No-host means no interaction/URL target, while intrinsic state still works. Explicit privacy/no-match policies such as visited are named tested behavior, never an unknown-predicate fallback. |
| C2 publication/integration | Publish the exact implemented API, facade and snapshots only when all accepted predicate kinds have their required evaluator or explicit standards/environment semantics. Packed-consumer tests, all existing selector entry points, relevant WPT cases and cascade matched-branch specificity must pass or have separately reviewed exact debt. No partial API whose accepted selector later throws NotImplementedException or silently returns false. |

C2b and C2c remain internal working features while later state dependencies are incomplete. Each
has focused real semantic tests; completing one does not imply all matching is complete. Internal
test entry points reject an unimplemented predicate explicitly before evaluation instead of
silently accepting it; remove that staging guard before publication. Do not ship a public reduced
grammar to conceal the missing evaluator. The query gate includes existing directionality and
slotted parsing debt, and explicitly records any living-standard versus vendored-corpus changes.

Use explicit matching/work stacks and clear transient references before pooling. No regex grammar,
recursive DOM traversal, global browsing-context selector cache, host callbacks captured into a
program, or eager whole-tree materialization for Matches/QuerySelector. Indexes are optional later
optimizations requiring document mutation and intrinsic/environment-state invalidation; do not add
them before traversal correctness. Cascade must use TryMatch's effective specificity, not
MaximumSpecificity. Run appropriate Release tests on net8.0/net10.0 using MTP `--project`, and update
public snapshots only when actual public members land.
