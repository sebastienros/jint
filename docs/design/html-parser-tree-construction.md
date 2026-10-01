# H4 HTML tree construction dispatch

Reviewed finite dispatch, 2026-09-23. This supplements
[the architecture](html-parser.md), [H1–H3](html-parser-feature-slices.md),
[native ownership](html-parser-native-followups.md), and the
[trusted construction seam](html-parser-construction.md). H4 consumes reviewed H3 and native seams;
it does not publish `MarkupParser.ParseHtml`, a fragment parser, or a Browser backend.

The result is a resumable, inert document session with an explicitly bounded implementation inventory.
Supported branches perform standard recovery. Reaching an unimplemented branch produces a named
internal milestone result, never a guessed tree or a successful partial document. These stops are
removed by H5–H7 before the standalone HTML facade ships. They are not a strict parsing policy or a
permanent exclusion from the goal of full standard recovery.

## Ownership and prerequisites

| Owner | Files and deliverable |
| --- | --- |
| H4 Sol owner | New `Jint.HtmlParser/Html/TreeConstruction/**`, including `HtmlParserSession`, `HtmlTreeBuilder`, mode rules and doctype classifier; `Jint.Tests.HtmlParser/Html/TreeConstruction/**` and a pinned, bounded fixture subset there |
| H1/H3 owner | Existing `HtmlTokenizer*`, `HtmlInput`, `HtmlToken` and text-mode transitions; H4 reports a needed change instead of editing concurrently |
| Shared integration owner | One `Parsing/HtmlParseOptions.cs`, shared limit documentation, project/signing/API snapshot changes; no feature-local duplicate options or limits |
| Native owner | Document-mode storage, missing-attribute merge and text append seams below, plus their tests; existing Document/Node/Element/Text files remain singly owned |

Do not modify Browser, existing WPT vendor data, native files, or tokenizer internals in the H4 feature
commit. H4's final build runs on merged prerequisites, not a temporary fork of their types. The shared
and native prerequisites are separate small commits before H4 integration; coordinate them with D3b,
templates and D5 so each native write path has one owner. The parser construction seam remains usable
before D5, and acquires D5 behavior through its common native mutation boundary when D5 lands.

Use namespace `Jint.HtmlParser.Html` for session/tree implementation. Retain the native namespace for
options/metadata. No `AngleSharp`, Browser, Jint engine, JavaScript callback, task queue or network
dependency enters this package.

## Exact first session surface

Keep this entire surface internal in H4. Shared owner creates the one option type in `Parsing`; the
eventual public facade promotes that same type after its complete contract is reviewed:

```csharp
// namespace Jint.HtmlParser
internal sealed class HtmlParseOptions
{
    internal bool ScriptingEnabled { get; init; } // false
    internal ParseLimits Limits { get; init; } = ParseLimits.Unbounded;
    internal ParseDiagnosticCollector? Diagnostics { get; init; }
}

// namespace Jint.HtmlParser.Html
internal readonly record struct HtmlDocumentContext(
    bool IsSrcdoc = false, bool CannotChangeMode = false);

internal enum HtmlParseStepKind { NeedInput, Yielded, Complete, MissingFeature }
internal enum HtmlMissingFeature { Tables, Formatting, Select, Templates, Framesets, ForeignContent }

internal readonly struct HtmlParseStep
{
    internal HtmlParseStepKind Kind { get; }
    internal HtmlMissingFeature? MissingFeature { get; }
    internal long Offset { get; } // meaningful only for MissingFeature
}

internal sealed class HtmlParserSession
{
    internal HtmlParserSession(Document document, HtmlParseOptions? options = null,
        HtmlDocumentContext context = default);
    internal Document Document { get; }
    internal void AppendInput(string chunk, bool isFinal = false);
    internal HtmlParseStep Drive(int workQuota, CancellationToken cancellationToken);
    internal long WorkCount { get; }
}
```

The option property's real implementation rejects null Limits, matching A2. Snapshot limits, scripting
context and the collector reference once. Null options selects defaults. H4 does not yet carry BaseUrl:
add the architecture's base-URL field through this same shared owner with actual document-URL/base
resolution consumers before H7/public facade acceptance. Do not add a dormant URL option or use
`System.Uri` as a substitute for the Browser WHATWG URL implementation.

The supplied document must be a fresh empty HTML document (`Kind == Html`, `ContentType == "text/html"`),
owned exclusively by the caller/session. Null throws ArgumentNullException; wrong kind/content type or
nonempty target throws ArgumentException before clearing diagnostics or modifying metadata/tree.
This is a trusted internal creation contract, not arbitrary public parse-into-existing-document support.
The document identity is stable. H4 callers may inspect it between Drive calls but do not mutate it or
register a host. H8 lifts that restriction at defined host boundaries and tests stack/DOM divergence.

The session owns one tokenizer and one tree builder. The builder owns insertion mode, open-element
stack, head/form pointers, frameset-ok state, original text mode, pending token/text index, and any
resumable tree work. The tokenizer owns scanning and preprocessing. No recursive call stack represents
open HTML elements, token reprocessing or an interrupted scan. Helpers may remain private; do not add a
public token-to-tree API or a generic parser framework.

AppendInput is exactly the H1 owned-string feed, including empty nonfinal input, final empty input,
original-unit input limits and append-after-final rejection. Drive requires positive quota. It consumes
pending tree work before asking the tokenizer for another token. Tokenizer NeedInput and Yielded are
not EOF; tokenizer Complete without a tree-processed EOF is an internal invariant failure. Session
Complete means EOF recovery and all tree work finished; subsequent Drive calls return Complete.
Append after completion is invalid. Complete is unrelated to window load or readiness events.

Cancellation and ParseLimitException terminate the session, propagate unchanged, and make later
Drive/AppendInput fail with InvalidOperationException. A MissingFeature result also terminates this
milestone session; subsequent calls cannot continue from a fabricated fallback state. It identifies
the first required missing family and the current token's source Offset. Earlier valid work can remain
in the document, but is expressly not a successful parse result. Preserve the unsupported token until
the result is formed; do not swallow it or count it as completed work.

## Insertion-mode inventory

Implement Initial, BeforeHtml, BeforeHead, InHead, InHeadNoscript, AfterHead, InBody and Text. This
proposal also moves the small AfterBody and AfterAfterBody modes from the broad H6 planning row into
H4, in full, so ordinary explicitly closed documents are complete test cases. H6 still owns all
frameset modes. This is a dispatch refinement requiring the coordinator's acceptance, not a silent
claim that the entire H6 row is complete.

Use the current [HTML tree-construction rules](https://html.spec.whatwg.org/multipage/parsing.html#tree-construction)
as the algorithm reference. Cite precise anchors in implementation, and record the spec commit/date
used in fixture provenance. In particular, current HTML includes PI tokens and revised select handling;
historical html5lib expectations are test evidence, not permission to restore obsolete algorithms.

The first implementation's supported InBody partition is exact:

| Branch family | H4 acceptance |
| --- | --- |
| Characters, comments, PI, doctype, EOF | All branches, including NUL, whitespace classification, recovery and frameset-ok changes |
| html/body and head-routed metadata/text tags | Missing-attribute merges into existing html/body; head pointer routing; repeated/stray tokens and normal recovery |
| Block group, headings, pre/listing, form, li/dd/dt, p, button | Their dedicated start/end rules, applicable scopes, implied end tags, form pointer and leading-newline handling |
| area/br/embed/img/keygen/wbr, input, param/source/track, hr, image | Dedicated void/rewrite branches, including hidden-input frameset behavior and end-tag br recovery |
| title/textarea, style/xmp/iframe/noembed/noframes/noscript, script, plaintext | State-specific H3 interaction, conditional noscript rules, inert script structure, Text-mode EOF handling |
| rb/rtc/rp/rt and ordinary ruby | Ruby implied-end handling; these do not require an active-formatting implementation |
| Stray table-family/frame/head starts that the InBody rule ignores | Perform that specified ignore/error branch; do not report missing solely because a token name is table-related |
| Other ordinary/unknown start and end tags | The actual generic branches, with complete special-element classification and scope/stop rules |

Partition the current spec's cases before the generic branch. A missing specialized case must never
fall into generic insertion. The H4 empty active-formatting invariant is justified by refusing every
operation that would populate that list; reconstruction on that proven-empty list requires no fake
implementation. Do not introduce a public or internal mode switch disabling formatting recovery.

| First required operation outside H4 | Milestone result / owner |
| --- | --- |
| Enter table handling, including the InBody table start branch | Tables / H5; stop before its paragraph close, insertion or stack mutation |
| Start or end of a/b/big/code/em/font/i/nobr/s/small/strike/strong/tt/u; applet/marquee/object branches that require formatting markers | Formatting / H6, before the branch modifies tree or formatting state |
| Dedicated select/option/optgroup branches | Select / H6; current spec implements these within body/table rules, not historical InSelect/InSelectInTable modes |
| Template start/end handling that requires template algorithms | Templates / H6; includes head-routed cases |
| A frameset branch requiring frameset construction or mode changes | Framesets / H6; already-specified ignore branches can still complete normally |
| MathML/SVG creation or foreign-content dispatch | ForeignContent / H7, before creating an incorrectly HTML-namespaced element |

This table gates operations after mode dispatch, not globally by token spelling: `<b>` inside script
text is text, and a token ignored by a supported mode remains ignored. Mode delegation and reprocessing
must preserve the actual insertion mode unless the delegated rule changes it. An insertion-mode
dispatcher default is an internal invariant error, never "use InBody". Unknown HTML tag names still
use the standard generic branch after all named cases have been classified.

Within H4, cover implicit html/head/body creation at EOF as well as on ordinary input; misplaced head
content must use the saved head identity. Do not rebuild the stack from ParentNode. AfterHead's temporary
head-stack entry must be removed even when the delegated rule starts text parsing. Close/pop operations
change parser state, not DOM parentage. Self-closing syntax on an HTML non-void element does not close
it: acknowledge only on the branches that do so, and report an unacknowledged flag once per start token,
not once per reprocessing pass.

## Tokenizer transitions and character runs

H4 consumes the reviewed H3 `SetTextMode(HtmlTextMode, appropriateEndTagName)` contract. Apply a mode
change immediately after handling the triggering start token and before the next tokenizer Read.
Do not prefetch a later token to decide an insertion rule. Appropriate end-tag names come from the
tree algorithm, not a guess in the tokenizer. Generic RCDATA/RAWTEXT and script paths save/restore the
actual insertion mode; plaintext keeps InBody rather than pretending it has a closing tag. EOF in Text
must pop/restore/reprocess once. In H4 scripts are inert regardless of ScriptingEnabled; that flag
selects grammar, especially noscript, and is not execution authorization.

Tokenizer text chunks are arbitrary batches of character tokens. The builder retains a character index
and processes homogeneous runs only where the same rule applies. A single chunk containing leading
spaces then a letter may cross Initial, BeforeHtml, BeforeHead, InHead and InBody. Do not classify an
entire text chunk from its first character. The pre/listing/textarea newline-ignore rule consumes at
most the immediately following LF character, even across NeedInput/Yielded, and never discards a whole
text chunk. CRLF normalization stays solely in H1. Entity output is already decoded.

PI tokens create native ProcessingInstruction nodes with case-preserved target and data; they are not
comments or text and do not force implied html/head/body where the corresponding comment-like
placement does not. Use the insertion destination selected by the mode. Initial/BeforeHtml and the
document tail differ from in-element placement. Tokenizer recovery owns disallowed/invalid PI targets;
the builder does not reinterpret them using obsolete blanket bogus-comment behavior. Valid H1 PI
targets already satisfy the current native PI factory grammar. Test these real factories together.

H7 owns the between-token CDATA-context setter and foreign-content dispatcher. H4 keeps AllowCData
false and stops before entering foreign content; seeing an svg spelling is not a tokenizer heuristic.

## Native construction and metadata

Determine an insertion location as a parent plus optional reference child before creating a node.
Create with that parent's OwnerDocument (or the parent itself for Document), never unconditionally
with the session document. Consume CreateParsedElement, InitializeParsedAttributes and AppendParsedChild
under their existing freshness proofs. HTML token attributes initially have null namespace/prefix;
an `xmlns` or colon-bearing name is not XML namespace resolution. H7 performs foreign adjustments later.
Repeated html/body tokens operate on published elements and require this additional native-owned seam:

```csharp
// Element; published receivers are allowed.
internal void AddMissingParsedAttributes(ReadOnlySpan<ParserAttribute> attributes,
    CancellationToken cancellationToken);
```

Incoming attributes are already tokenized, namespace-resolved and duplicate-free. Match by namespace
and local name. Preserve every existing Attr identity, value and position; append only missing keys
in incoming source order, with the receiver's owner document and OwnerElement. Do not retain the span.
Use one indexed pass over existing attributes and one pass over incoming attributes, with bounded
cancellation polling during scans and between committed attributes. Each committed addition performs
ordinary attribute semantic updates, stamp invalidation and D5 record production. Cancellation may
leave a prefix of complete additions, never an attribute with incomplete ownership or bookkeeping.

This seam bypasses public name validation, not mutation semantics. For example, in
`<html><html =x=y>` the second token has the valid HTML attribute name `=x`, which a public attribute
factory rejects. Preserve tokenizer-produced colon-bearing and `xmlns` names with null namespace;
do not infer namespace bindings. Do not substitute a loop of SetAttributeNode calls that repeatedly
scans the existing list. Native tests cover these names, mixed existing/missing keys, identity/value/
order preservation, cancellation, and ordinary observer records once D5 is available.

Fresh append is legal only when referenceChild is null and the existing native seam's preconditions
hold. Later foster/reconstruction/adoption-agency moves use real insert/remove/adopt paths, not clone
linking or arbitrary observer suppression. Template destination adjustment will select TemplateContent
and its inert owner before creation. H4 cannot exercise parsed template entry yet; that is a named H6
acceptance dependency, not permission to append template content as ordinary children. Intrinsic
TemplateContent creation, host-inclusive cycle checks and ownership remain native responsibilities.

Native owner adds this narrowly scoped internal metadata in `Dom`, before H4 and C2b consume it:

```csharp
internal enum DocumentMode { NoQuirks, Quirks, LimitedQuirks }
// Document
internal DocumentMode Mode { get; private set; } // NoQuirks for native factories
internal void SetParserMode(DocumentMode mode);
```

SetParserMode is a trusted internal write, with native cache/stamp invalidation when relevant; it is
not a public user-selectable parsing flag. Clone copies Mode, including shallow clones. Import/adoption
retain destination document mode; inserting/removing/replacing a doctype does not recompute it. XML
factories remain NoQuirks. Selector code reads this one source of truth rather than guessing from MIME
or the current doctype. Browser will map Quirks to BackCompat and both other modes to CSS1Compat.

H4 owns the full Initial-mode doctype classifier, including the standard legacy identifier tables,
ASCII comparisons, force-quirks, and missing-versus-empty identifier distinctions. Do not implement
only the modern html doctype and leave old identifiers as NoQuirks until H7. Evaluate from the token
before missing identifiers are normalized to empty strings in the native DocumentType. Test every
classification condition, not just the three enum values. IsSrcdoc and CannotChangeMode are internal
caller context used by this algorithm, not public recovery options. A cannot-change session preserves
the supplied mode. Native factories' NoQuirks default and an empty parsed document's mode need not agree.
H7 adds fragment inheritance/context combinations to this already-correct classifier.

### Coherent, linear text accumulation prerequisite

The current string-only Text.Data plus `text.Data += chunk` copies a growing prefix for every H1 text
chunk. This is quadratic even for one large paragraph, and quota-1 Drive makes it worse. Holding a
private tree-builder buffer until EOF hides characters from Document between Drive calls. Neither
approach satisfies this session contract.

Native owner supplies a real append path before H4 integration:

```csharp
// Text; native implementation and tests, not a second parser-owned text model
internal void AppendParsedData(ReadOnlySpan<char> data, CancellationToken cancellationToken);
```

Back Text with appendable storage only when this path is used, with a cached immutable Data string
materialized on demand. Data always reads the complete current value; Data assignment replaces the
storage and invalidates the cache. Appending after a Data read keeps the appendable storage instead of
rebuilding from the previously materialized prefix. Clone/import obtains an owned value with normal
identity semantics. Do not expose builders, writable arrays, span lifetimes or source slices retaining
entire input segments. Keep ordinary factory/setter behavior unchanged.

The append goes through native character-data invalidation/semantic and D5 record production. It
captures an old string only when a matching observer needs OldValue. Empty append is not requested by
the tree builder. Each bounded slice is cancellation-atomic for this published node: when cancellation
escapes, either its value is unchanged or the full slice is committed, including cache invalidation,
semantic updates, stamps and D5 records. Preparation and growth of uncommitted storage may poll;
after logical commit, finish all required bookkeeping before the next cancellation poll. Do not leave
an appended buffer visible through a stale cached Data string or omit a record for a committed change.
The fresh-unpublished attribute initializer's partial-initialization/discard policy does not apply here.
Native tests inject cancellation during preparation and at commit boundaries after a cached Data read;
when D5 exists, include an OldValue observer and verify the value/cache/stamp/record outcome together.

Native append polls cancellation around growth/copy and bounded authored work subject to that commit
rule; the tree builder passes bounded slices and charges their length plus reported/known copy work. A Data read
may require one unavoidable contiguous allocation; repeated host reads or requested observer old-value
snapshots can necessarily copy successive prefixes. Do not claim linearity for that inherently
materialized output workload. The unattended, unobserved parse must accumulate in amortized linear work.

The builder coalesces with the Text immediately preceding the resolved insertion location, preserving
its identity. It does not create adjacent Text siblings merely because Read returned another chunk or
Drive yielded. Each Drive return exposes all character work already accepted by the tree builder;
there is no hidden tree text to flush later. Any tokenizer-owned partial text remains tokenizer work.
This keeps future host-action boundaries coherent without forcing a whole growing text copy at every
quota boundary. D5 owns the exact mutation record projection for bulk append, using the same native
character-data semantics as other writes.

## Work, limits and diagnostics

Share one cooperative Drive budget across scanner and tree work. Subtract tokenizer WorkCount deltas;
do not hand every Read a fresh full quota and let unlimited tree work run between reads. Tree work
counts character inspection/copy, attribute preparation, stack search/pop and token reprocessing. Use
saturating monotonic accounting. A supported token must either finish, leave a saved continuation or
consume budget; quota 1 must make progress without restarting a scan from its beginning.

Explicit unbounded parser-owned tree loops poll cancellation within at most 4096 work units and save
progress when quota expires. Existing coherent native DOM mutations are atomic commit boundaries:
cancellation is observed before and after the commit, which may exceed the remaining quota. Do not
poll or yield halfway through range/iterator/slot/form/observer repair. Parser WorkCount records one
boundary unit and excludes opaque native mutation internals; do not invent per-descendant accounting
or claim a guaranteed latency ceiling. Parser-owned searches, snapshots and stack/root walks remain
charged and resumable. This is the explicit H6f amendment recorded in
[the promotion checkpoint](html-parser-html-promotion.md#atomic-native-mutation-boundary-amendment).
Native attribute preparation and unavoidable CLR allocation/copy operations retain before/after
polling and cooperative overshoot caveats. No hard latency or `delta <= quota + constant` promise is
introduced. Avoid repeated materialization/restarts. Any exception escaping a native boundary faults
the parser, preserves the original exception, and prevents replay; a committed DOM is not rolled back.

MaxInputCharacters and MaxTokenCharacters retain A2/H1 meanings. MaxNestingDepth now also bounds the
number of elements on the HTML open-element stack, including implied html/head/body and temporary head
entries. Check before each push/allocation that would exceed the limit; first element depth is 1.
Immediate-pop void elements still pass through that push. Popping does not refund input/token work.
H7 must explicitly define fragment bootstrap accounting before adding its entry. Shared owner updates
the existing limit's documentation; no HTML-specific duplicate limit or Browser MaxDomNodes mapping.
New cumulative node/work limits are not added speculatively in H4.

Adversarial plain nesting must not turn fresh append into ancestor walks. Repeated block starts must
not rescan the whole stack just to prove no p-in-button-scope exists: maintain the needed scope facts
or indexes, updated with stack changes. Long unmatched end-tag/scope searches must be cancellable and
reviewed for repeat-scan amplification; use name/special-boundary indexes where repeated absent-name
searches would otherwise be quadratic. Do not replace specification stop conditions with a plain
"find matching name" search. Maintain only indexes consumed by H4, extending them with later modes.

The tokenizer clears the supplied diagnostic collector once at session creation; the tree builder
shares it without clearing. Ordinary HTML parse errors are bounded diagnostics and recovery, not
exceptions. Use stable `html/tree-*` codes for the unnamed tree-construction errors and separately test
self-closing acknowledgement, missing/invalid doctype, unexpected token/NUL and EOF in text. MissingFeature
is an implementation inventory result, not a source parse error in that collector.

Tree diagnostics are anchored to the currently supplied HtmlToken.Offset, an original UTF-16 source
position. For text chunks, do not invent `Offset + decodedIndex`: references and newline normalization
make that false. H4 promises token-start anchoring, not individual decoded-character source maps.
Text grouping can change that anchor under different partitions; compare tree-error codes/order and
exact non-text offsets, not fabricated per-character offsets. Any later stronger source-map guarantee
requires a reviewed tokenizer contract rather than hidden rescanning of discarded input.

## Browser and future session handoff

The current consumers were inspected, rather than inferred from the standalone facade:

- `Runtime/Parsing/ParserDriver.cs` registers scripting according to PageRuntime.ScriptingEnabled,
  drives AngleSharp's incremental script/resource behavior, and checks MaxDomNodes on the final tree.
- `Dom/Views/JsDomParser.cs` uses a separate inert, scripting-disabled HTML document and routes XML MIME
  inputs separately. H4 does not replace this entry with a partial parser.
- `Dom/DynamicMarkupInsertion.cs` currently reparses accumulated secondary-document writes while
  retaining document identity; replacing that identity-losing child rebuild is an H8 requirement.
- Fragment consumers include innerHTML, insertAdjacentHTML and contextual fragments; context namespace,
  local name, form/template ancestry and script treatment cannot be reduced to an empty body parse.

H4's no-host script path never fetches, executes, pumps a microtask, fires an event, or schedules a task.
Maintain distinct tree operations at element creation/insertion and script-text completion so H8 can
stop there; do not add unimplemented HostAction variants or callback properties in this first commit.
Future script state (parser document, force-async, already-started) must be native/intrinsic or in the
reviewed Browser adapter owner, never inferred solely from seeing a script node in the tree.

Before H8 permits active Browser parsing, extend this same session with returned host requests and
single-completion request identities. The outer Drive must return before user code executes. A script
boundary retains the unread tokenizer tail and a saved insertion position, including before pending
EOF. H1's AppendInput appends to the stream tail and is not document.write. H8 must extend the owned
input cursor with insertion frames/markers, not parse writes as detached fragments, concatenate/reparse
the whole source, or defer write visibility until the writing script returns.

DriveInsertedInput is future H8 work: it drives eligible inserted characters synchronously to the
frame's marker or a defined parser blocker. Split writes may leave partial tokens, and nested written
scripts get nested insertion frames. Reentry into an active tree mutation is invalid; resuming after a
returned host request is allowed by that protocol. A cooperative yield during a running JS write is
not authorization to pump unrelated tasks. H8 tests pending EOF, restored outer tails, nested writes,
write-then-query, host tree mutation and secondary-document close before claiming the protocol works.
Browser retains resource ordering, stylesheet blockers, async/defer/modules, readiness and generations.

## Finite follow-ups and acceptance

H5 owns table entry, all table/caption/column/row/cell modes, pending table text, foster insertion and
associated reset/scope helpers. It removes Tables stops, but must propagate remaining Formatting,
Templates or Select stops rather than bypassing those algorithms. H6 then completes formatting/list
reconstruction/adoption agency, current select rules, templates and framesets (including frameset tails).
Split H6 into those named families if needed; its tests cover interactions with H5. H7 completes foreign
content, contextual fragments and context-sensitive mode/quirks cases; it supplies context to the same
builder and tokenizer, with real native template destinations. No recovery family is silently deferred
past the public HTML facade gate. Current template patch/shadow algorithms require their separately
owned native prerequisites and explicit accounting before full current-spec conformance is claimed.

H4 acceptance requires independently specified trees including namespace, ordered attributes, node
identity, PI/comment/doctype placement, text and document mode:

1. Empty/whitespace/comment/PI-only documents; omitted html/head/body; explicit complete documents;
   misplaced head content; duplicate html/body attribute merging; stray end tags and EOF recovery.
2. Every supported InBody family above, including p/list/heading/form/button recovery, void tags,
   unacknowledged non-void self-closing flags, unknown elements and generic end-tag stop conditions.
3. H3 integration for title/textarea/style/script/plaintext and noscript under both grammar settings;
   leading-LF handling, false end-tag candidates, script escaped states, rawtext entities and EOF.
4. All doctype classifier conditions, missing versus empty identifiers, ASCII case, srcdoc/mode-lock
   context, persisted native mode and clone/import behavior. No parser-local mode shadow.
5. A literal stop fixture for each MissingFeature family, checking result/offset, pre-branch tree,
   terminal lifecycle, and absence of a success result or generic wrong-namespace/mode fallback.
6. Every input split for short cases plus deterministic partitions and quotas 1/3/large. Assert equal
   final native trees and one coalesced Text identity per insertion run; inspect partial trees between
   Drive calls. Include mixed whitespace/nonwhite text in one token and nonfinal input exhaustion.
7. Input/token/depth limits at and one beyond their boundaries, invalid quota, final/complete misuse,
   pre-cancel and deterministic cancellation during attributes, text and deep stack scans. No timers.
8. Deep ordinary/block nesting and large text/attribute fixtures with deterministic work/copy counters;
   bound fresh construction and unobserved accumulation by operation counts, not stopwatch claims.

Vendor a bounded, pinned tree-construction subset with license/source/case IDs and a census of executed
versus unsupported cases. Keep exact historical/current-spec disagreements annotated, especially PI
and select changes, with independent replacements. Unsupported cases do not count as passing. Run
Release tests against both net8.0 and net10.0 using MTP --project and fresh builds. H4 changes no public
API snapshots except separately reviewed native/shared additions, and makes no benchmark claims.
