# HTML tree construction after H4: bounded dispatch contracts

Design only, 2026-09-23. Refines H5–H8 in [the architecture](html-parser.md) and consumes
[the H4 session contract](html-parser-tree-construction.md),
[trusted native construction](html-parser-construction.md), and
[native ownership](html-parser-native-followups.md). No implementation, new public entry point,
or Browser switch is authorized by this document alone.

The implementation inspected is H4 commit `115caab7e`, under `Html/TreeConstruction/`.
Its quota, CR and index-cost review fixes are prerequisites, not conventions to preserve from that
unfixed snapshot. Start the next implementation from the integrated, reviewed H4 revision. Do not
refactor its core concurrently with its owner or remove its terminal missing-family stops early.

The algorithm reference is the [living HTML parser](https://html.spec.whatwg.org/multipage/parsing.html),
updated 2026-09-22 when inspected. Record the actual spec revision and corpus commit in each family's
fixtures. Notable current rules: select uses body/table algorithms, PI has its own tokens/nodes,
fragment parsing uses a root insertion target, and template parsing includes shadow and content-patch
branches. Historical tree corpora and pinned AngleSharp remain comparison evidence; neither overrides
a verified normative change. Keep named old/new fixtures for those differences.

## 1. Dispatch order and ownership

These are separate reviewable changes, not parallel edits to the same partial builder. The first two
table changes are the immediate next sequence. Formatting follows them; foreign content and fragments
follow their dependencies. Review every implementation with Astra High; Sol owns fixes. No new task
or agent is created by this design.

| ID | Finite deliverable | Depends on / remaining stops |
| --- | --- | --- |
| H5a | Table structure: table/caption/column-group/body/row/cell modes, implied structure, exact scopes/reset, real formatting markers | Corrected H4. Keep `Tables` stops at pending-table-text/foster operations until H5b; keep other families. |
| H5b | Pending table text, foster placement, shared insertion-location use, table/body interaction | H5a. Removes `Tables` only after every table branch is accounted for; dependent Formatting/Select/Templates/ForeignContent stops still propagate. |
| H6a | Active formatting storage, entry equivalence/Noah's Ark, reconstruction and marker-scoped object/applet/marquee rules | H5b. Keep `Formatting` on adoption-agency-required branches; do not fake ordinary pop recovery. |
| H6b | Complete adoption agency, anchor/nobr rules and all formatting end tags | H6a, real native moves. Removes `Formatting`. |
| H6c | Current select/option/optgroup and affected input/hr/table rules | H5b, H6b. Removes `Select` for document parsing; fragment-context cases are exercised at H7b. |
| H6d | Ordinary templates, template mode stack and inert contents ownership | H5b/H6b/H6c, native TemplateContent. Keeps `Templates` for applicable shadow/patch operations until H6f. |
| H6e | Frameset construction and all frameset tail modes | Corrected H4; integrate sequentially with the current builder. Removes `Framesets`. H4 already owns AfterBody/AfterAfterBody. |
| H6f | Applicable declarative-shadow and template content-patching branches | H6d plus actual D6/native template metadata and patch algorithms. Removes remaining `Templates`; no opt-out substitutes for missing behavior. |
| H7a | SVG/MathML foreign dispatch, integration points, name/attribute adjustments, dynamic CDATA context | H6 document families; H1/H3 tokenizer prerequisite. Removes `ForeignContent`. |
| H7b | Contextual HTML fragments using the same builder; context bootstrap, target ownership, parser mode inheritance | H7a, native contexts, H3 initial-fragment-state prerequisite. No public facade yet. |
| H7c | Full tree-construction census, public HTML facade/options and packed-consumer gate | All preceding families and shared metadata/script-state prerequisites. Removes every internal MissingFeature result from paths reachable by public HTML APIs. |
| H8/R | Executable parser requests, inserted-input frames and Browser scheduling integration | Complete tree semantics plus D5/B3 and actual host protocol. Standalone inert parsing is not Browser replacement. |

Primary implementation ownership remains `Jint.HtmlParser/Html/TreeConstruction/**` and
`Jint.Tests.HtmlParser/Html/TreeConstruction/**`. Add cohesive partials as each real algorithm lands:
`HtmlTreeBuilder.Tables.cs`, `.Insertion.cs`, `.Formatting.cs`, `.AdoptionAgency.cs`, `.Select.cs`,
`.Templates.cs`, `.Framesets.cs`, `.Foreign.cs` and `.Fragments.cs`. These names reserve locations,
not empty files to create now. The active family owner alone makes its needed changes to `.cs`,
`.Body.cs`, `.Modes.cs` and `HtmlParserSession.cs`; later owners consume the merged result.

H1/H3 retains tokenizer/input ownership. The native owner retains Document/Element/Node/Text,
TemplateContent, shadow/patch slots, mutation records and intrinsic script/control state. The shared
owner retains `Parsing/HtmlParseOptions.cs`, limits, project/API snapshots and document URL metadata.
An implementation reports a missing seam and requests that owner's small prerequisite change;
it does not copy native types or add a feature-local options/exception type.

## 2. Session and continuation invariants

Keep the existing internal `HtmlParserSession(Document, HtmlParseOptions?, HtmlDocumentContext)`,
`AppendInput`, `Drive`, `Document`, `WorkCount` and four step kinds. No new public token visitor,
pluggable insertion-mode registry, host callback property or best-effort parsing flag. Current
`HtmlMissingFeature` families suffice for these intermediate gates: a family can remain reachable
at a narrower unimplemented branch after other branches are implemented. Each stop fixture records
the exact branch, token offset and intact pre-branch state. A stop remains terminal and never becomes
a successful partial document.

The session is single-owner. H5–H7 callers may read the tree between Drive calls but cannot mutate it;
H8 defines the later host mutation boundary. The builder's open-element stack is parser state, not
a path reconstructed from DOM parents. Foster and adoption-agency placement deliberately make the
two disagree even without a host. Existing head/form pointers retain native identity.

For every unbounded algorithm, save the stage, cursor and working identities needed to resume after
a yield. Reprocessing is another state transition on the same token, not recursive dispatch and not
a fresh token. Do not restart a scan or mutation sequence on quota 1. No fixed number of dispatch
passes may stand in for termination once nested template EOF and adoption loops are supported.
Use the corrected H4 progress/budget protocol rather than changing its overshoot policy here.

Charge scanner and tree work once through the shared budget: list/stack scans, text classification,
owned-buffer copies, attribute snapshots, index maintenance, reconstruction and moves. Poll at the H4
bounded cadence, including backwards searches and cleanup. Whole native commits finish their semantic
bookkeeping before yielding; a quota is not an instruction to expose half-linked nodes. Runtime
allocation/copy and atomic native mutation retain H4's documented cooperative overshoot exception.
Do not hide an unbounded authored loop behind a single charged operation.

The continuation must distinguish token consumed, token delegated, token reprocessed and work pending.
Preserve the actual insertion mode when applying another mode's rules; restore a temporary foster
flag only after the delegated operation really completes, including yields. Self-closing acknowledgement
and diagnostics are emitted once at their algorithm points. EOF is a real token: flushing pending text,
closing templates and finishing stack work must complete before the session reports Complete.

H4 routes character runs separately through `ProcessCharacters`; non-character `Dispatch` changes alone
cannot implement a new mode. Wire each family's character handling through the same mode, foreign-rule,
insertion-location and continuation decisions. No new table/foreign mode may silently inherit the old
InBody character path. Exercise both routes and transitions between them, including a retained
non-character token that triggers a pending-table-text flush.

Cancellation and limits still terminate the session. Earlier coherent tree mutations can remain visible;
there is no parse-wide rollback. MissingFeature is implementation inventory, not an HTML parse error.
Keep the one collector clear at session construction and original token-start diagnostic anchoring,
including buffered text. Decoded text indices are not original source offsets.

## 3. H5a: table structure without fabricated formatting or foster behavior

Implement `InTable`, `InCaption`, `InColumnGroup`, `InTableBody`, `InRow` and `InCell`, plus the InBody
table start/end rules and exact reset/scope helpers they require. `InTableText` lands with H5b when it
works. H5a stops with `Tables` before starting an unimplemented table-text/foster operation; it can
complete real structural cases such as `<table><tr><td>x<td>y</table>` without pretending every malformed
table is supported.

The finite inventory includes implied colgroup/tbody/tr, nested table recovery, clear-back-to-table/
table-body/row context, caption closure, cell closure, mismatched table-family end tags, hidden inputs,
forms, head-routed script/style/template tokens, whitespace in column groups, and table EOF delegation.
Every switch arm is either implemented, an applicable dependent-family stop, or an explicitly mapped
H5b stop. A tag-name-wide stop is incorrect when the current mode is required to ignore that token.

Caption and cell entry/exit need actual formatting markers before formatting elements are enabled.
Introduce one private ordered formatting list in `.Formatting.cs`, with an explicit marker entry and
real push-marker/clear-through-last-marker operations. A private closed entry base with a marker subtype
is sufficient at H5a; H6a adds the element-entry subtype. Do not put dummy elements into the list, use
the open-element stack as the formatting list, or add an empty reconstruction method. No formatting
element can enter this list until H6a removes the corresponding stop, so the marker-only invariant is
testable. Clearing markers is meaningful even when no formatting elements have been admitted.

Expand H4's currently reachable scope facts for new table boundaries. Scope, list-item scope, button
scope, table scope and the three clear-stack algorithms are distinct predicates. Classification uses
namespace plus local name. The first matching target cannot be found across a nearer boundary; foreign
integration-point boundaries join the same representation at H7a. Keep `select`'s current ordinary-scope
semantics; do not add the obsolete select-scope algorithm from an old tree builder.

One tree owner maintains indexes on push, pop, middle removal and replacement. Keep stable element
identity distinct from list indices. New table/template boundaries cannot invalidate cached H4 name or
special-element positions. Tests check the optimized answer against the literal stack rule on generated
small states, and deterministic work counts on long absent-name/scope cases. Do not rebuild every index
from the root for each ordinary push/pop or rescan the whole stack to prove a common name absent.

H5a acceptance: native trees for complete/omitted table structure; caption/column/body/row/cell transitions;
scope boundaries; marker balance; unexpected end/start tokens; comments and real PI nodes in these modes;
hidden-input/form exceptions; EOF; source diagnostics; split/quotas 1, 3 and large. Add literal terminal
stop tests for pending table characters and generic foster branches, and keep every other missing-family
test applicable through table delegation. No production facade is introduced.

## 4. H5b: pending table text and one insertion-location algorithm

Implement the actual pending-table-character list and `InTableText`. Tokenizer text batches and input
chunk boundaries are not logical table-text-run boundaries. Preserve all pending characters until the
next non-character token establishes the flush point; a nonfinal NeedInput or cooperative Yielded is
not a flush. A later nonwhite character affects the placement of preceding buffered whitespace.

This is a **specified deferred tree operation**, unlike an arbitrary private text buffer in ordinary
InBody. H4's visibility invariant is refined accordingly: committed tree work is visible at each return,
but pending table characters are explicitly not inserted yet. Do not publish whitespace in the table
and relocate it later merely to make an intermediate tree look full. Text already committed during a
resumable flush is visible; uncommitted suffixes remain pending, with their classification fixed.

Use owned segments or an amortized buffer, original token offsets, an accumulated nonwhite flag and
saved flush segment/character indices. Drop NULs at the prescribed point with diagnostics. Retain the
non-character trigger token while flushing, then restore the saved insertion mode and reprocess it once.
Do not read a second token over that trigger, duplicate an error on every resume, or repeatedly concatenate
the entire pending prefix. Release drained segments. The buffer can grow to a whole contiguous table-text
run; document that necessary retention rather than claiming input-size-independent memory.

Centralize the builder's insertion destination in `.Insertion.cs`:

```csharp
// Private tree-builder value, not a public DOM API.
private readonly record struct InsertionLocation(Node Parent, Node? Before);
```

The appropriate-place algorithm takes the current/override target and current foster state. It locates
the last relevant template/table on the **parser stack**, checks the table's actual DOM parent, and
returns parent plus optional reference child. Without a table it uses the stack root. A template target
is adjusted to its real contents or later patch target. H5 can only encounter templates after H6d;
keep that dependency explicit instead of returning the template element as the insertion parent.
Current HTML specifies both an appropriate place and an adjusted insertion location; the latter adds
fragment root-target behavior at H7b. Preserve that ordering rather than multiplying special insertion
paths for elements, text, comments and PI.

Create a fresh node with the **resolved destination's node document**. Existing `AppendParsedChild`
is valid only for fresh append with no reference child. Use real insertion/move APIs when inserting
before a table or reparenting existing nodes. If a native path cannot meet the established cancellation
or complexity contract, request a narrowly proved native prerequisite; do not call clone-only linking
or add skip-validation/suppress-observer switches. D5 bookkeeping applies to every committed insertion
and move once integrated.

For character insertion, coalesce with the Text immediately preceding the insertion location, not
unconditionally the current node's last child. Fostered text can join text before the table across
multiple flushes, preserving identity and order. Native `AppendParsedData` remains the accumulation
primitive; no growing `Data += suffix`. Comments/PI follow their own mode-selected placement and are
not fostered simply because adjacent text was fostered.

H5b acceptance includes whitespace-only versus mixed table runs across every short split, CR/CRLF and
references from H1, NUL, EOF-triggered flush, fostered elements/text, repeated runs coalescing before a
table, nested tables, missing-parent/override-target location helper cases, hidden input/form exceptions,
and delegated Text-mode restoration. Inspect the partial tree across NeedInput and quota-1 flushes.
Run deterministic cancellation within classification, stack lookup and insertion sequences; already
committed nodes/records remain coherent. Template/table and host-moved-table integration fixtures join
H6d/H8 respectively; a helper test does not claim host mutation support now.

## 5. H6a/H6b: active formatting and adoption agency

### H6a: list storage and reconstruction

Add formatting-element entries to H5a's one list. An entry owns the native element reference and the
original creation-token information needed to recreate it, including an owned attribute snapshot.
Do not reconstruct from today's live attributes after later host mutation. Copy or prove ownership of
the `HtmlToken.Attributes` collection; its `IReadOnlyList` type alone does not prove immutability.
Release snapshots when entries are removed and preserve source attribute order during recreation.

Implement marker-bounded lookup, entry replacement, membership and the exact equivalent-entry limit
(Noah's Ark). Equivalence includes namespace/name and attributes as the standard specifies, not just
the tag name, identity or attribute count. Attribute order is not an equality shortcut. Use a collision-
checked key/index if needed to avoid quadratic repeated scans; no hash is accepted as equality by itself.

Reconstruction scans back to a marker or an entry already open, then recreates entries forward in order.
Each recreated element is a new native identity, inserted through the resolved location and substituted
in the formatting list. Keep the DOM tree and open stack/list changes synchronized across yields.
Reconstruction can itself create elements and exceed MaxNestingDepth; validate before allocation/push.
Do not count a marker as an open element or treat formatting-list length as tree depth.

Audit every InBody reconstruction call site, including ordinary text, whitespace, plaintext, voids,
button, generic starts, and later select/foreign entry. The existing text batching path must call it
before accepting the affected run, not after characters were appended under the wrong parent.
Applet/marquee/object enter and leave their real marker scopes in this change.

Enable ordinary formatting starts `b`, `big`, `code`, `em`, `font`, `i`, `s`, `small`, `strike`, `strong`,
`tt`, `u`. Keep `a`/`nobr` specialized starts and formatting end-tag branches terminal `Formatting`
until H6b; this avoids a half-implemented adoption path. H6a can complete reconstruction cases such as
formatting carried across an implicitly closed paragraph and EOF. Existing ordinary tokens that do
not require adoption continue normally. No generic end-tag fallback for a stopped formatting end tag.

Acceptance: markers isolate cell/caption/object content; reconstruction after implicit closure;
equivalent entries with reordered versus different attributes; element replacement identity; no crossing
the last marker; deep reconstruction under quota 1; cancellation while scanning/copying; no duplicate
node after resume. Keep concrete adoption-stop fixtures. This change makes no claim to full formatting.

### H6b: the adoption agency as a resumable algorithm

Implement the current [adoption-agency algorithm](https://html.spec.whatwg.org/multipage/parsing.html#adoption-agency-algorithm)
in a dedicated partial. Preserve its bounded outer loop, unbounded searches and inner loop as separate
concepts. The algorithm's outer iteration limit is not a parser work quota. Store subject, outer and
inner counters, formatting element, common ancestor, furthest block, bookmark, current/last node,
and the current stage across Drive calls. Recomputing those from a changed DOM after yielding is wrong.

Cover all exits: current-node shortcut; no list entry; entry not open; entry out of scope; no furthest
block; and the full rearrangement. Keep list/stack membership and namespaces exact. The inner-loop
threshold removes entries as specified; it is not permission to truncate the search or stop moving nodes.
Recreation uses the entry's saved token. Bookmark movement and replacement must remain correct when
list entries before it are removed. Middle stack insertion/removal updates every consumed H4/H5 index.

Reparent existing nodes, preserving identity, via native operations. Child transfer is a saved cursor
over actual children, not a clone-and-replace. Foster placement of the last node uses the same resolved
location algorithm with the correct override target. One complete native move may be an atomic commit;
the whole adoption algorithm is not an uninterruptible transaction. Resume without repeating a removal,
duplicating observer records, changing an already-consumed bookmark, or recreating the same element.

Then enable anchor/nobr starts and formatting end tags together. Fixtures include the standard
`<b><i></b></i>` and `<b><p></b></p>` shapes, nested anchors, repeated nobr, more-than-three inner entries,
markers and tables, no-furthest-block cases, stale formatting entries and deep furthest-block searches.
Use independently specified trees and identity assertions. Add quota/split permutations and deterministic
cancellation at pre/post-move boundaries; D5 record assertions run when its native seam is present.
Remove `Formatting` only after every former stop has a real implementation and regression.

## 6. H6c: current select handling, not historical insertion modes

Use [the current InBody rules](https://html.spec.whatwg.org/multipage/parsing.html#parsing-main-inbody)
and affected table rules. Do not create `InSelect`, `InSelectInTable` or a legacy select-scope predicate.
Current select content does not use the old broad whitelist that discards all unfamiliar descendants.

The finite inventory is select start/end, option/optgroup starts and end recovery, reconstruction,
implied ends, nested select, input, hr, and table-related select interactions. A nested select in scope
closes the prior select while ignoring the new token as specified. Input distinguishes a select fragment
context from a document select in scope. Option/optgroup behavior differs when a select is in scope
versus when they appear elsewhere. `</select>` follows the current block-end scope path. Do not change
textarea or generic body behavior to an old library's select fallback.

Test ordinary markup inside option/select, option/group closure through intervening elements, nested
select and input, hr, select in tables/cells, formatting around/within select, comments/PI, malformed
end tags and EOF. Include independently derived current trees next to named historical corpus deltas.
H7b adds select-context fragment rows before public completion. This task constructs the tree; selectedness,
form ownership, reflection, validity and JS option collections remain D7/B2 responsibilities and are
required at their integration gate. A parser flag cannot substitute for native select state.

## 7. H6d/H6f: templates, inert ownership and applicable newer branches

H6d implements ordinary template start/end handling, InTemplate, and the stack of template insertion
modes. Each template entry adds the actual formatting marker and template mode, updates frameset state,
and routes insertions to the existing native `TemplateContent`. End/EOF cleanup uses thorough implied
end tags, stack/list cleanup and reset-insertion-mode in a resumable loop. Nested EOF cannot be limited
to H4's old small dispatch-pass guard.

Template content is a separate fragment with its native inert owner and host association; it is not
ordinary children of the template element. Resolve the destination before creating nodes/attributes.
Nested templates use the correct contents-owner document; shallow/deep clone/adopt/import continue
to use native ownership rules. The parser neither invents another inert document pool nor overwrites
the native fragment identity. Head/body merges and form-pointer behavior obey the parsing-template-
contents predicate, including a later template fragment context, not only a local-name test on Current.

Template dispatch into table/column/body/row modes must update the current template mode stack entry
and reprocess the same token exactly once. Foster placement chooses the last template/table in the
stack and then the template's real target. Template nesting depth is constrained through actual open
elements; the mode stack must not get a separate inconsistent limit.

H6d fixtures: templates in head/body/table, implied table structures inside templates, nested templates,
form-pointer isolation, ignored html/body merges, formatting markers, template EOF recovery, identity/
owner/host links, comments/PI and script inertness. Shadow/patch applicability remains a named Templates
stop until H6f. Test that stop before side effects; do not accept their attributes and silently apply
ordinary template semantics when the current algorithm selects a different branch.

H6f is a separate finite implementation gate, with two native-owned prerequisites:

| Branch | Required real native state and acceptance |
| --- | --- |
| Declarative shadow template | D6 shadow-root ownership/host links and the current parser metadata (mode, declarative state, relevant flags/registry behavior); exact eligibility and fallback; parser-allowed versus disallowed contexts; contents redirected to the created shadow root. No pretend ordinary DocumentFragment standing in for a ShadowRoot. |
| Template with `for` content patching | The current prepare-content-patching operation and template insertion target/start/end markers; scope/target resolution, success/failure, adjusted insertion before the end marker, marker cleanup and moved/missing-marker cases. No string replacement or detached fragment append as an approximation. |

Current [InHead template handling](https://html.spec.whatwg.org/multipage/parsing.html#parsing-main-inhead)
contains both branches, and the appropriate-place algorithm consumes their target state. The attribute
is `for`, not an invented `patchfor`. Keep the parser's allow-declarative-shadow-roots state internal and
chosen by the actual calling algorithm; do not publish a general flag as a workaround for absent D6.
In contexts where the standard itself selects ordinary template behavior, that behavior is valid.
Where a branch applies, absence of native support is a completion blocker.

The exact native member names are finalized by that owner together with working algorithms/tests;
H5 does not add dormant patch/shadow fields now. H6f does not expand to executing script or running a
custom-element registry in the standalone parser. Browser-hosted effects use the later H8/B3 protocol.
No remaining Templates stop can be hidden behind the public ParseHtml facade.

## 8. H6e: framesets and tails

Implement InFrameset, AfterFrameset and AfterAfterFrameset; AfterBody/AfterAfterBody are retained H4
algorithms. Cover the AfterHead frameset path, InBody conditional body replacement, frameset-ok changes,
frame void insertion/acknowledgement, nested framesets, whitespace, comments/PI, noframes text routing,
end tags and EOF. An ignored frameset token does not start fetching a frame. Body replacement must
remove the actual body through native semantics while preserving parser stack rules; no whole-document
reparse. Tests include frameset-ok false, mixed tails, inert noframes and partial returns. Child-frame
navigation/realms/load events remain R3.

## 9. H7a: foreign content and tokenizer context

Foreign dispatch is a per-token decision using the adjusted current node, namespace, MathML text
integration points, HTML integration points and the token kind/name. It is not a mode entered permanently
at the string `svg`. Maintain original creation-token facts required by integration-point rules, including
annotation-xml encoding; later live attribute edits are not a substitute for those parser facts.

Implement the full HTML foreign-content branch table: NUL/replacement behavior, text/frameset state,
comments/PI/doctype, HTML breakout starts (including conditional font attributes), namespace-specific
element creation, self-closing acknowledgement, SVG script completion and foreign end-tag scanning /
HTML fallback. Use namespace-aware special/scope tables. Do not apply HTML lowercasing to adjusted SVG
local names after creation or match all end tags with XML case sensitivity.

Include the complete specified SVG element/attribute case adjustment tables, MathML `definitionURL`,
and foreign attribute namespace/prefix mappings (XML, XMLNS, XLink). They are finite spec data, with
table coverage tests and provenance. `xmlns` does not rebind HTML's parser namespaces like an XML parser.
Unknown foreign names remain names in the chosen namespace. Use trusted native factories with the
resolved components and source attribute order; no reparse through XML and no serializer round-trip.

H1/H3 prerequisite: the tokenizer currently snapshots readonly `_allowCData`. Add an internal
between-token context update, owned by H1/H3, which changes CDATA eligibility without resetting text
state, source offsets or pending input. After tree processing finishes a token and changes parser context,
the session derives eligibility from the adjusted current node **before starting the next token**.
Resuming that same token after NeedInput or Yielded retains the established context; do not call the
setter indiscriminately before every tokenizer read. Reject updates while tokenization of a token is
suspended. CDATA
eligibility follows the foreign adjusted-node test; it is not simply the same boolean as “next token
uses foreign tree rules.” Integration points can select HTML rules while retaining a foreign current
node. Foreign CDATA contributes Text; this HTML path does not manufacture XML CDataSection nodes.

Acceptance: SVG/MathML roots and nested switches, foreignObject/desc/title, MathML text integration
points and mglyph/malignmark exceptions, annotation-xml encodings and svg child exception, breakout tags,
adjusted names/attributes, self-closing SVG versus HTML, CDATA versus HTML bogus-comment behavior, script,
NUL, end-tag mismatch and EOF. Test every short split including CDATA open/close and namespace transitions,
quota-1 scans, diagnostics, and native owner/identity. XML `ParseSvg` remains a separate frontend with
different case/well-formedness behavior.

## 10. H7b: fragment bootstrap and result ownership

Add a private/internal factory on the **same session** for fragments rather than parsing a wrapper
document string. The eventual standalone contract stays
`ParseHtmlFragment(string source, Element context, HtmlParseOptions?, CancellationToken)` and returns
a detached DocumentFragment without modifying context children. Null context/source fails before parsing.
There is no public artificial-context tag-name/namespace tuple or parse-into-existing-node overload.

Follow the current [HTML fragment algorithm](https://html.spec.whatwg.org/multipage/parsing.html#html-fragment-parsing-algorithm).
It has a private HTML parser document and synthetic html stack root, but an explicit **root insertion
target** that is the returned fragment. That fragment belongs to the calling target's node document.
Do not build the visible result under a temporary html root and adopt/drain it afterwards: create actual
result nodes with the resolved destination's owner from the beginning. The synthetic root is parser
scaffolding and is never returned. This also handles XML/SVG-owned context elements without changing
their owner document kind.

Bootstrap the context element, its fake creation-token attributes, mode inherited from its document,
template insertion mode when applicable, nearest form ancestor (including context itself), and tokenizer
state from namespace/local name and scripting grammar. The context is not pushed as an extra open element
or inserted into the returned fragment. Reset-insertion-mode and adjusted-current-node use it at the
specified stack-root boundary. The public Element-context call uses that element as target; Browser's
template-content and shadow-root callers need an internal real-target path so their fragment owner and
host/context selection remain correct.

Count the synthetic open html root as depth 1, consistent with the existing stack limit. Count actual
subsequent pushes, including void/reconstructed elements; do not charge the external ancestor chain,
context element or returned fragment as open elements. Ancestor/context-attribute scans still consume
work and poll cancellation. Copy only needed context facts; do not mutate context metadata or reclassify
the external document's quirks mode from fragment doctypes.

### Required H3 initialization extension

Current `SetTextMode` requires a nonempty appropriate-end-tag name for RCDATA/RAWTEXT/ScriptData.
That contract fits a document start tag but cannot represent the initial fragment tokenizer state.
H3 supplies one initial-only operation, `InitializeFragmentTextMode(HtmlTextMode mode)`, accepted only
before any read/token emission. It sets the initial mode with **no emitted start tag and no appropriate
end-tag name**. Keep later ordinary `SetTextMode` behavior intact. A fake context token is not a token
emitted by this tokenizer. Test textarea/title/style/script fragment strings beginning with their
apparent closing tag: they must not escape the initial text context by an invented prior emission.
No name sentinel or tokenizer input rewriting is used.

### Scripting and side-effect checkpoint

Current HTML distinguishes Normal, Disabled, Inert and Fragment scripting modes. Keep the user-facing
`ScriptingEnabled` meaning as the standalone noscript/grammar choice; it is never execution permission.
At the internal integration boundary, false selects Disabled and true selects inert parsing for the
standalone API. Introduce the real internal scripting-mode state only with its consumers and native
script flags, through the shared/native owners. Do not collapse Browser contextual fragments, innerHTML,
DOMParser and document parsing into one boolean or globally mark every script already-started.

H7b itself performs no JS/network/event work. H8/B3/R supplies any host requests prescribed by active
Browser modes. Correct inert/fragment script flags must survive later adoption/insertion. A detached
standalone parse is not a custom-element activation path. Base URL/document metadata is supplied by
the shared owner and the architecture's actual URL consumer before public promotion; no `System.Uri`
substitute or unused BaseUrl option.

Fragment matrix: HTML body/html/head; table/tbody/tr/td/caption/colgroup; select/option/optgroup; template
and its inert owner; form ancestry; title/textarea/style/script/noscript/plaintext; SVG/MathML and integration
points; all document modes; empty and malformed input. Check original context children/attributes/owner
remain unchanged, returned identity/owner/namespace, no wrapper leakage, and exact split/quota equivalence.
Browser wrappers such as Range contextual fragment can have additional context-selection rules; test
those at B4/H8 rather than guessing them inside this standalone method.

## 11. Test census, publication and Browser checkpoints

Every family adds cases to the existing tree-construction harness with stable fixture IDs, corpus/spec
revision, requested context/scripting mode, and independently expected native trees. Extend the harness
to represent template contents, namespaces, ordered attributes, PI targets/data, document mode and later
shadow roots. Serialized HTML alone cannot prove these properties or retained node identity.

Track selected cases, passing parses, named milestone stops, expected normative deltas and unresolved
failures separately. A MissingFeature test passing is not a successful parse in the corpus count. Never
accept the implementation's output as a snapshot without reviewing the intended algorithm/tree. Historical
PI/select fixtures with outdated expectations need named current replacements, not broad exclusions.
Do not modify the existing WPT vendor pin or exclusions as part of an individual tree-family change.

Short cases run at every input split; longer cases use fixed partitions and quota 1/3/large. Compare
tree and diagnostic ordering under supported anchoring guarantees. Test NeedInput versus EOF, pending
CR before LF, terminal errors, exactly-at/over limits, and cancellation at saved-stage/commit checkpoints.
Use deterministic work/copy instrumentation to catch rescans and quadratic accumulation; no stopwatch
thresholds or benchmark claims in these functional changes. When native D5 is present, add parser-move,
text append and template destination record/old-value coherence tests through its real hooks.

Run relevant tests freshly compiled in Release, including both project TFMs:

```sh
dotnet test -c Release --project Jint.Tests.HtmlParser/Jint.Tests.HtmlParser.csproj
```

H7c promotes `HtmlParseOptions` and the two architecture-approved HTML methods only when all ordinary,
foreign, template, patch/shadow-applicability and fragment paths have defined working behavior. Prove
with a packed external consumer that empty HTML implies the correct document, malformed HTML recovers,
options/limits/cancellation/diagnostics and result ownership work, and script/network/events stay inert.
No public API leaks MissingFeature, falls back to AngleSharp, or silently returns a truncated tree.
HTML serialization and selector/native intrinsic behavior have their own gates and still block G0 when
required by the architecture; publishing HTML alone is not completing the package.

H8 extends this same session only with a working returned-request protocol, unique completion identities,
script boundaries and inserted-input frames. It must retain unread tails and pending EOF, support nested
write-then-query and secondary-document open/write/close, and tolerate authorized host DOM moves without
rebuilding parser stacks from DOM parents. AppendInput is tail input, never document.write. Active user
code runs after Drive returns, not inside a native tree mutation. No empty HostAction enum/callback seam
is added by H5–H7 just to reserve future work.

R1/R2 then prove real parser blockers, stylesheet blockers, async/defer/module/import-map ordering and
readiness using controlled resource completions; R3/B3/B4 prove dynamic activation, frames, custom-element
reactions and DOM fragment entry points. Source consumers include `Runtime/Parsing/ParserDriver.cs`,
`Dom/DynamicMarkupInsertion.cs`, `Dom/Views/JsDomParser.cs`, and the existing script/style/document-load
tests. A tree-complete result is not a page-load event or permission to pump unrelated tasks.

The final objective remains **G0 standalone API completeness, G1 full Browser replacement/parity and
paired benchmarks, then G2 removal of production AngleSharp dependencies**. Preserve the agreed corpus,
equal-work benchmark comparisons and benchmark instructions; measure only once equivalent semantics
exist. No family-count milestone, small green subset, or inert parser can substitute for that gate.

## 12. Immediate bounded handoff

After H4 review fixes are integrated, assign **H5a only**: table structural modes and helpers, real
formatting markers, exact remaining stop branches and its fixtures. Consume existing session/native
seams and coordinate core-file ownership; no tokenizer/native/public/Browser edits in that feature
commit. Return the implementation, current branch census and Release results for Astra High review.
Then assign H5b against the reviewed H5a state. Keep H6/H7 contracts in this document as subsequent
bounded handoffs, not instructions to implement them opportunistically in the table commit.
