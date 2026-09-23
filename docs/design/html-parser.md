# Jint.HtmlParser: owned markup, DOM and CSS infrastructure

Status: proposed design, 2026-09-23. This document introduces no runtime implementation.
Design worktree base: `73f9220b1b48e6da755cc1ae32b058f6e846b599`. Evidence refreshed against the
coordinator's integration base `d78d25137526b4a7b35ccdcfc8522730acd392d1`; current-state statements
below refer to that newer revision, not the design worktree's older checkout.
Implementation work targets `main`.

## 1. Decision and scope

Create a standalone, managed `Jint.HtmlParser` project and NuGet package. It owns the HTML parser,
the mutable document tree used by HTML/XML/SVG, CSS syntax and CSSOM, selector matching, serialization,
and the mutation machinery that makes those facilities coherent. `Jint.Browser` owns JavaScript
bindings, browser scheduling, networking, resource activation, DOM event dispatch, and rendering
approximations. The dependency is `Jint.Browser → Jint.HtmlParser`; the parser has no dependency on
Jint, Jint.DevTools, a JavaScript engine, or AngleSharp.

The user's explicit request supersedes the existing AngleSharp-only principle for this campaign.
It does not waive existing script-visible behavior, performance, identity, resource limits, or
conformance requirements. Update the affected guidance when the implementation switches ownership;
this proposal leaves instructions governing the current runtime intact.

The completion criterion is removal of **all four AngleSharp packages from the shipped browser
dependency graph**, including transitive references through generator/runtime projects. A parser
that reads simple HTML while the browser still needs AngleSharp's CSSOM, node classes or XPath
navigator is an intermediate artifact, not completion. AngleSharp can remain a pinned test/benchmark
oracle in development-only projects.

The public API is new. Do not reproduce AngleSharp interfaces, its configuration/service container,
or its `[DomName]` metadata merely to minimize edits. Preserve web-facing contracts through generated
bindings and explicit semantic adapters. The existing public `Browser`/`Page` surface predominantly
returns snapshots and values, so most CLR migration is internal; a public API snapshot remains a
required gate rather than assuming that no dependency type escaped.

### Package and implementation boundaries

| Component | Owner | Responsibility |
| --- | --- | --- |
| `Jint.HtmlParser/Dom` | New package | Node identity, namespaces, tree/attribute operations, ranges, collections, change tracking |
| `Html` | New package | Tokenizer, tree builder, document/fragment/resumable parsing, character references |
| `Xml` and `Svg` | New package | XML well-formedness, shared-tree construction, SVG document entry and namespace handling |
| `Css` and `Selectors` | New package | CSS tokens/rules/declarations, selector programs, property grammar, cascade inputs |
| `Serialization` and `XPath` | New package | HTML/XML serializers and a navigator over the same nodes |
| `Dom/Generated` and generator | Browser/tooling | WebIDL branding, conversion, shapes and calls into the new native model |
| Event, script, frame, resource and lifecycle algorithms | Browser | Existing Jint event bus and page loop; no second scheduler in the parser |
| CSS environment and layout | Browser | Media state, pseudo-class state, UA rules, resolved values, flat layout and rule-use reporting |

Target `net8.0;net10.0`, matching the browser and its tests. A standalone package does not require a
`netstandard2.0` asset; adding one now would constrain hot-path APIs without a demonstrated consumer.
Use repository signing/versioning/central-package conventions. Enable trimming/AOT analyzers for the
new project and verify a published standalone consumer; promote the browser's AOT claim only after
its own complete native smoke test. No reflection-based service discovery or runtime code generation.

This is document infrastructure, not a renderer. SVG parsing includes XML namespaces, foreign content,
DOM and attribute preservation; it does not promise painting, fonts, filters or animation. Existing
SVG DOM interfaces used by the browser still require explicit bindings and semantic implementations.
HTML also requires MathML foreign-content handling even though no separate MathML entry point was
requested. CSS syntax support is not equivalent to implementing every CSS property's computed value.

## 2. What actually has to move

The integration tree has 194 `Jint.Browser` `.cs` files mentioning AngleSharp (including generated files).
The dependency pins are AngleSharp 1.8.2, AngleSharp.Css 1.1.2, AngleSharp.Xml 1.2.0, and
AngleSharp.XPath 2.0.6. The first two also feed `tools/dom-bindings/pin.json`.

| Current seam | Evidence in this repository | Replacement obligation |
| --- | --- | --- |
| Navigation parser | `Runtime/Parsing/ParserDriver.cs`, `ParserBaton.cs` | Scripting flag, incremental tree, script insertion points, readiness, bounded parsing |
| Navigation input | `Runtime/DocumentFetch.cs` | Bounded byte accumulation, decode, MIME dispatch, URL/response/error preservation |
| XML navigation routing | `PageDocumentFactory.cs`, `DomContentType.cs` | Principal and frame XML/XHTML/+xml routing, SVG document branding |
| Subresources | `PageResourceLoader`, `PageStylingService`, `PageScriptingService` | Discovery and activation of scripts/styles/images/frames; load/error ordering |
| Detached parsing | `Dom/Views/JsDomParser.cs`, `DomConstructors.cs` | Inert HTML/XML documents, parsererror mapping, correct document brands |
| Bindings | `tools/dom-bindings/Jint.Browser.BindingGenerator`, `Dom/Generated/*.g.cs` | Replace assembly metadata input and interface casts, retain checked-in generated shapes |
| Tree state | `DomHostHooks`, `DomNodeMembers`, collections, forms, custom elements | Every insertion, adoption, attribute, reflection, range and traversal operation |
| Detached dynamic markup | `Dom/DynamicMarkupInsertion.cs` | Existing secondary-document open/write/close behavior and document identity |
| Mutations | `Observers/JsMutationObserver.cs`, `MutationObserverLane.cs`, `DevTools/DomNodeTracker.cs` | Shared mutation source, observer options and records, microtask delivery, protocol tracking |
| Attribute hooks | `CustomElementAttributeObserver`, `FileInputAttributeObserver`, `FrameAttributeObserver` | Cover detached nodes, namespaced writes and native mutations, not just connected descendants |
| CSS | `Dom/Views/CssCascade.cs`, `ResolvedStyle.cs`, `JsCssNamespace.cs`, `Runtime/PageRenderDevice.cs`, CSS protocol domain | Selector/property parsing, CSSOM mutation, cascade, supports/media checks, rule-use tracing |
| XPath | `JsXPath.cs`, `DevTools/DomDomain.Events.cs` | Navigator plus node identity; BCL XPath evaluation can remain |
| Consumers of tree/CSS | `Layout`, `Accessibility`, `Extraction`, `Events`, `CustomElements`, `Media`, `FrameWindows` | One new tree, no cross-model node conversion and no stale second state |
| Layout invalidation | `Layout/PageLayout.Invalidation.cs`, `DomFailures.GuardMutation` | Entry/exit mutation scopes, reentrant reads, version saturation and unknown-writer fallbacks |

Generated code currently consumes AngleSharp-specific interfaces, extension methods, enums, nullability,
collection accessors and legacy named properties. Renaming namespaces is insufficient. Build a generated
inventory of every emitted member and every manually consumed native member before migration starts.
Each inventory row must name its new owner, implementation task, regression tests and disposition of
the old workaround. The existing divergence register is evidence to migrate, not behavior to discard.

The current document fetch already reads a bounded stream into a byte array, decodes it, and hands a
complete string to the parser. The separate parser thread exists because AngleSharp's async parsing
and scripting hooks can resume on pool threads; a blocking baton enforces exclusive DOM ownership.
This is a scheduling adaptation, not evidence that network streaming is already supported.
`FetchedDocument` now carries `Markup` and `ContentType`: top-level XML navigation and XHTML/+xml
routing already work through `PageDocumentFactory`. XML navigation scripts remain inert, a documented
browser divergence. The migration must preserve that routing and separately account for script support.
Current selector fixes in `DomSelectors`/`DomForgivingSelectors`, namespace/element factories and
processing-instruction attributes also belong in the inventory; do not regress them to an older baseline.

## 3. Recovery, strictness and what Lightpanda teaches

### Conformance is not rejection of malformed input

The default must implement specified HTML error recovery. Missing end tags, foster parenting and
misnested formatting have observable results. Rejecting them is an authoring validator policy, not
more faithful browser parsing. CSS similarly recovers at defined boundaries, with different rules for
different grammar contexts. WPT tests observable algorithms; it is not an XML-style cleanliness test
for HTML. See the [HTML parsing standard](https://html.spec.whatwg.org/multipage/parsing.html#parse-errors),
[CSS error handling](https://drafts.csswg.org/css-syntax-3/#error-handling), and the repository's
`Jint.Tests.Browser/Wpt/AGENTS.md` for its real-harness and exclusion rules.

Use these separate concepts:

| Concept | Proposed behavior |
| --- | --- |
| HTML/CSS recovery | One standard algorithm, always the default |
| Diagnostics | Optional bounded collection of error codes and source positions; does not change the tree |
| `RejectParseErrors` | Optional standalone validation policy; aborts at a reported HTML/CSS parse error, never selected by Browser/WPT |
| XML well-formedness | Required; failure is a parse failure, not HTML recovery |
| HTML quirks mode | Determined by doctype and parsing context, not a user strictness toggle |
| Missing implementation | Explicit capability/debt entry and failing or excluded test; never silently successful parsing |
| Headless approximations | Browser policy, independently documented, such as synthetic geometry; not a parser option |

`RejectParseErrors` is not full authoring conformance validation: many authoring violations are not
tokenizer/tree-builder parse errors. Do not name it `StrictConformance`. Ship diagnostics first; add
rejection only with a demonstrated validation consumer. A `ParseSvg` call uses XML rules; an inline
`<svg>` in HTML uses HTML's foreign-content rules. There is no input sniffing that silently swaps them.

The browser and WPT must use the same parser defaults. A future browser conformance profile may control
identified approximations, but only through a published list of concrete behavior changes with tests
under both profiles. This design does not introduce a catch-all lenience mode.

### Lightpanda source findings

Inspected source commit: [`1dd33ee7ec779033bf8d3b4707277deddf667202`](https://github.com/lightpanda-io/browser/tree/1dd33ee7ec779033bf8d3b4707277deddf667202).
These are observations of that revision, not claims about every release or proof of conformance.

| Observation and primary source | Design consequence |
| --- | --- |
| Rust wrapper uses html5ever 0.40.0 and xml5ever 0.40.0; HTML document and streaming constructors use default parser options. [Manifest](https://github.com/lightpanda-io/browser/blob/1dd33ee7ec779033bf8d3b4707277deddf667202/src/rust/html5ever/Cargo.toml), [wrapper](https://github.com/lightpanda-io/browser/blob/1dd33ee7ec779033bf8d3b4707277deddf667202/src/rust/html5ever/lib.rs) | HTML recovery comes from a standards-oriented parser, not a home-grown globally tolerant grammar. Keep a real tree builder. |
| HTML `parseErrorCallback` ignores diagnostics; XML callback marks errors except two message-prefix cases. [Parser callbacks](https://github.com/lightpanda-io/browser/blob/1dd33ee7ec779033bf8d3b4707277deddf667202/src/browser/parser/Parser.zig) | Diagnostics can be cheap when disabled. Do not copy message-based XML exceptions without normative and test evidence. |
| `Streaming.read` guards reentrant feeds and queues input; `done` warns and drops writes made during finish. [Streaming implementation](https://github.com/lightpanda-io/browser/blob/1dd33ee7ec779033bf8d3b4707277deddf667202/src/browser/parser/Parser.zig) | Reentrancy needs an explicit protocol. These choices are not an oracle for `document.write` timing. |
| Pending text accumulates chunks and flushes before operations that expose the tree; comments identify an earlier quadratic concatenation problem. [Text accumulation](https://github.com/lightpanda-io/browser/blob/1dd33ee7ec779033bf8d3b4707277deddf667202/src/browser/parser/Parser.zig) | Coalesce parser text without repeated immutable-string concatenation; flush before script/reaction observation. |
| `units.zig` distinguishes its permissive attribute lexer from fail-closed media queries. [Unit parser](https://github.com/lightpanda-io/browser/blob/1dd33ee7ec779033bf8d3b4707277deddf667202/src/browser/css/units.zig) | Share lexical utilities, not permissiveness across unrelated grammar contexts. |

The source also provides a tokenizer-only preload scan and periodic termination checks. Both are
useful architectural ideas; a preload scan is optional later work, and its result must remain a hint.
Cancellation in Jint must also cover long tokens and tree-builder loops, not only node append counts.

This evidence supports selective, explicit engineering tradeoffs. It does **not** establish that
Lightpanda has a universal strict/lenient switch, nor that copying its shortcuts preserves WPT behavior.
Use its failure cases as test ideas and its architecture as inspiration. Implement from specifications
and independently written tests rather than copying its AGPL-licensed implementation into this package.

## 4. Native API contract

The following is the proposed public shape, not existing compiling code. Start with the smallest
surface used by standalone parsing consumers. Parser internals and browser integration hooks remain
internal with a signed friend grant until an external consumer demonstrates that they need promotion.

```csharp
using Jint.HtmlParser;

Document html = MarkupParser.ParseHtml("<p>Hello <b>world</b>");
Document xml = MarkupParser.ParseXml("<items><item id='a'/></items>");
Document svg = MarkupParser.ParseSvg("<svg xmlns='http://www.w3.org/2000/svg'/>");
CssStyleSheet css = MarkupParser.ParseCss("p { color: red }");
DocumentFragment fragment = MarkupParser.ParseHtmlFragment("<td>x", tableRow);
CssDeclarationBlock style = MarkupParser.ParseCssDeclarations("color:red; width:2em");

Element? first = html.QuerySelector("p > b");
string text = first?.TextContent ?? "";
string serialized = MarkupSerializer.ToHtml(html);
```

Proposed signatures:

```csharp
public static class MarkupParser
{
    public static Document ParseHtml(string source, HtmlParseOptions? options = null,
        CancellationToken cancellationToken = default);
    public static DocumentFragment ParseHtmlFragment(string source, Element context,
        HtmlParseOptions? options = null, CancellationToken cancellationToken = default);
    public static Document ParseXml(string source, XmlParseOptions? options = null,
        CancellationToken cancellationToken = default);
    public static Document ParseSvg(string source, XmlParseOptions? options = null,
        CancellationToken cancellationToken = default);
    public static CssStyleSheet ParseCss(string source, CssParseOptions? options = null,
        CancellationToken cancellationToken = default);
    public static CssDeclarationBlock ParseCssDeclarations(string source,
        CssParseOptions? options = null, CancellationToken cancellationToken = default);
}
```

`null` source/context throws `ArgumentNullException`; empty HTML is a valid empty HTML document;
empty XML/SVG fails. HTML returns its implied `html/head/body`, doctype and document mode. Parsing never
fetches, executes script or fires DOM events. Fragment parsing uses the supplied element's document,
namespace, local name, form/template ancestry and relevant flags; it returns a detached fragment without
replacing the context's children. It must handle table/select/script/style/template/SVG/MathML contexts.

`ParseSvg` parses an XML document and requires an `svg` root in the SVG namespace; `<svg/>` with no
namespace is not silently repaired. Consumers wanting HTML's inline SVG recovery call `ParseHtml` or
`ParseHtmlFragment` with an SVG context. XML fragments are a separate subsequent entry point with
namespace-context requirements, not HTML fragments with another name. CSS document and declaration
entry points select their grammar explicitly. Single-rule parsing is internal initially for CSSOM.

Options are small immutable values, snapshotted at session creation. No builder/container chain and
no per-element factories. `HtmlParseOptions` contains base URL, the HTML scripting flag (default false;
affects `noscript`, does not execute code), shared `ParseLimits`, and an optional diagnostic collector.
Declarative-shadow behavior is selected by browser call context through internal creation parameters,
not exposed as a dozen interacting public booleans. XML and CSS options contain only applicable fields.
Base URL metadata is not authorization to fetch anything. Browser URL resolution continues to use the
repository's WHATWG URL implementation; the standalone package must not pretend BCL URI resolution
is automatically browser-equivalent.

Limits cover input characters, created nodes, token/attribute text, open-element depth, CSS nesting,
XML entity expansion, and accumulated inserted input. Zero means unbounded where consistent with the
repository. Ordinary standalone parsing defaults to no arbitrary document-size ceiling; the browser
passes its configured bounds. Cancellation is always available. Exhaustion is a distinct
`ParseLimitException` with a stable limit kind and observed count; XML syntax errors are
`MarkupParseException` with code/position. Cancellation remains `OperationCanceledException`.
Malformed HTML/CSS recovers by default. Infrastructure exceptions are never relabeled parse errors.

Diagnostics are optional, capped, and use stable codes plus UTF-16 offsets for string inputs; line and
column calculation is enabled only when requested. The collector reports truncation. A byte decoder
later adds byte offsets without redefining existing offsets. Dynamic input needs source-segment identity
as well as offset. Diagnostic collection must not retain the entire input through a tiny record.

### Nodes, storage and ownership

Use ordinary GC-owned reference objects with stable identity: `Node`, `Document`, `Element`, `Text`,
`Comment`, `DocumentType`, `DocumentFragment`, and XML processing-instruction/CDATA nodes. `Document.Kind`
distinguishes HTML/XML; SVG is an XML document with SVG elements, not a second tree implementation.
Elements carry namespace/local-name/prefix and an internal known-element discriminator. Avoid a public
class for every HTML tag; bindings select web interface brands from that discriminator and semantic state.

Start with parent/first/last/previous/next links, child count, and a compact ordered attribute store
that promotes to an index only when profitable. Measure before selecting thresholds. Typical small
elements should not allocate dictionaries, observer lists or property bags. Intern known names statically;
use document/session-scoped atoms for unknown names, never an immortal process-wide user-string table.

Owned text strings allow input buffers to be released. Tokens use borrowed spans only within a drive
call; any token retained across a pause owns its data. Pool scratch buffers/stacks, never externally
reachable nodes. Do not require `Document.Dispose()` to make its nodes valid or invalidate a node when
its parse session ends. This accommodates detached nodes, adoption, JS wrappers and retained ranges.
An arena that retains a whole page through one detached node is not the initial storage design.

Documents and parse sessions are single-owner mutable objects. Independent documents may be parsed on
different threads; concurrent access to one document requires the host to serialize it. Public read-only
snapshots are explicitly named. `QuerySelectorAll` returns a new static result; `Children`/`ChildNodes`
are live views. Collection caches may use mutation versions but must not cache wrappers by live index.
Adoption preserves node identity and moves owner-document state while creation-realm identity remains
the browser wrapper's responsibility. Cloning creates new identity.

## 5. Resumability first; transport streaming later

A complete string is the initial input API and initial browser transport. The parser core is nevertheless
resumable from its first tree-building milestone. Tokenizer state, open elements, formatting list,
template modes, pending table text, character-reference state and insertion points belong to the
session, not the CLR call stack. No await occurs inside tree mutation.

| Approach | Benefits | Cost | Decision |
| --- | --- | --- | --- |
| One-shot string parser | Simplest caller, contiguous scanning | Cannot expose partial DOM at parser-blocking scripts if it builds the whole tree first | Public convenience only, drives the resumable core |
| Resumable string parser | Correct script boundaries, insertion and cancellation; matches current transport | Explicit state machine and host protocol | Required first architecture |
| Incremental UTF-16 chunks | Tests chunk boundaries and bounds retained input | Carry partial tokens/CRLF/surrogates and input ownership | Core contract tested early; public API can wait |
| Incremental network bytes | Earlier first-script latency, less input duplication | Encoding prescan/restart, transport backpressure and lifecycle changes | Separate phase after browser parity |

Proposed internal protocol:

```text
CreateDocumentSession(document, parsingContext, limits)
AppendInput(ReadOnlyMemory<char>, isFinal)     // owned or copied under an explicit lease
Drive(workQuota, cancellationToken) -> ParseStep

ParseStep = NeedInput | Yielded | HostAction(requestId, action) | Complete
HostAction = PrepareScript | ConstructCustomElement | ProcessTreeEffect
CompleteHostAction(requestId, result)
InsertAtInsertionPoint(text, insertionFrame)
DriveInsertedInput(insertionFrame, cancellationToken)
Abort(reason)
```

This protocol is a design contract, not a requirement to expose all actions as heap objects. Use a
small tagged result and reusable request storage where measurement supports it. `Drive` returns only
with a coherent tree and flushed text. A request is completed once; stale IDs, driving while the same
frame is running, or appending after final completion are rejected. Final network input does not mean
EOF has already been consumed: scripts can still insert before pending EOF. `Complete` occurs only
when EOF and all parser work have been handled, and differs from the browser's window `load`.

On `PrepareScript`, Browser decides whether execution blocks parsing, starts or reuses the fetch,
and runs JavaScript only on `PageLoop`. A returned parse step releases the parser's CLR stack before
user code runs. Async/defer/module scheduling, stylesheet blocking, import maps and ready-state
transitions belong to a browser script coordinator. They do not belong to a generic syntax parser.
Tree-effect hooks cover script/style/link/image/frame activation and relevant mutations, including
`src` set after insertion. Inert parser entry points install no browser activation host.

### `document.write` is a synchronous insertion protocol

Appending writes to the end of the source, parsing them as a detached fragment, or waiting until the
outer script returns is wrong. A script can write an element and query it in the very next statement.
Nested written scripts may themselves write. The parser needs saved/restored insertion frames and
the HTML parser-pause/script-nesting rules. The [dynamic markup specification](https://html.spec.whatwg.org/multipage/dynamic-markup-insertion.html#document.write())
is authoritative; a generic reentrancy lock cannot substitute for those rules.

When a parser script is executing, the outer `Drive` has already returned. Browser's write hook inserts
before the saved unread tail and drives eligible inserted input synchronously up to that frame's
marker or a specified blocking boundary. Nested host requests get nested frames, with an explicit depth
limit. The same active parser frame is never entered recursively while its tree mutation is running.
Do not pump arbitrary tasks/microtasks simply because the parser yielded inside running JavaScript.
Reactions and checkpoints happen at their specified algorithm boundaries. Split writes may intentionally
leave a partial token that later writes complete.

Initial migration preserves the current explicit refusal of post-parse implicit `document.open` on a
displayed document while providing the core machinery for it. Secondary HTML documents already implement
open/write/close through `DynamicMarkupInsertion`, reparsing accumulated input and replacing children
after each write. Preserve that supported surface, then replace its identity-losing reparse with the
resumable session. A separate task implements displayed-document replacement/listener clearing, new
parser generations and lifecycle behavior. Do not claim complete dynamic-markup conformance until that
task passes its WPT cases. XML documents continue rejecting these operations.

Parser CPU quotas return `Yielded` only at safe states; they let the loop observe close/cancellation
without allowing unrelated DOM operations halfway through a mutation. The host decides which browser
tasks may run at a parsing yield. Poll input scans as well as emitted tokens and long tree algorithms,
so a huge unterminated comment cannot evade cancellation. Limits count parser-created nodes before
allocation/attachment rather than counting only the finished document. Keep browser wrapper limits
separate; replacing the existing two-count contract requires a separately reviewed API change.

### Byte streaming has a separate acceptance gate

Change `FetchedDocument` to a bounded byte source only after resumability works with complete strings.
Networking produces bytes without touching DOM or Jint values; the page loop owns decoding/session
advance, or receives decoded immutable chunks from a stateful decoder. Backpressure caps outstanding
chunks. Preserve response metadata, redirects, cookies, MIME behavior and close/cancellation semantics.
Use encoding-label and HTML sniffing rules, including BOM/header/meta precedence and restart constraints.
No externally visible script executes twice because a decoder restarted. Test multibyte splits, CRLF
splits, surrogate pairs, EOF in every token state, early cancellation and redirects before committing.
Do not advertise a byte-stream API that assumes each network chunk is independently valid UTF-8.

## 6. Mutation and event integration

Every write path goes through the same DOM algorithms: parser attachment/reparenting, host methods,
binding methods, `Attr.Value`, namespaced attributes, `TextContent`, `innerHTML`, replacement, clone,
adoption, shadow/template operations and CSSOM mutation. Tree links and attribute storage are not
publicly writable. Fast parser insertions may skip redundant validation when their preconditions hold,
but cannot skip required semantic hooks, collection invalidation, range updates or observable records.

Use three separate mechanisms, with explicit ownership:

1. **Synchronous internal semantic hooks.** Narrow before/after operation hooks update live ranges,
   iterators, form state, slotting, style versions and browser state before the next read. Attribute
   hooks run even on detached elements and receive namespace/local name and old/new values. Custom
   element reaction scopes are supplied by Browser; the DOM core does not run arbitrary JS itself.
2. **Mutation record production.** Per-node observer registration lazily records the DOM-standard
   child-list/attribute/character-data information. It matches ancestors, filters, old-value requests
   and transient registrations; replace-all and fragments produce their specified record structure.
   It does not flatten records into one generic “document changed” event.
3. **Host scheduling and delivery.** A cheap internal “pending observers” signal schedules one Browser
   microtask. `MutationObserverLane` retains its delivery job, ordering and exception policy. Protocol
   mutation consumers use the same source, and notifications occur on the page loop. CSS changes carry
   separate style versions/notifications; DOM MutationObserver does not invent CSSOM records.

For standalone .NET consumers, expose a disposable pull subscription:
`document.ObserveMutations(target, options)` returns `MutationSubscription` with `TakeRecords()` and
`Disconnect()`. Records contain stable node references and immutable added/removed snapshots. No
hidden thread or event loop is started. The browser adds its own callback scheduling over this machinery.
Draining one subscription never drains another. Disconnect clears its registrations and records;
records retain removed nodes only while queued or retained by a consumer. Add callback convenience
only with a documented scheduling/reentrancy contract.

With no observer and no browser host, mutation recording should require an inexpensive empty-state
check and no record allocation. Internal change stamps are still maintained as needed for correctness.
Observer old strings and added/removed lists are materialized only when an interested observer needs
them. Detaching a subtree must not lose the transient registrations required until the next delivery.

DOM event dispatch stays in `Jint.WebApi.Events`/`Jint.Browser.Events`. Native nodes provide parent,
shadow-root/slot, ownership and activation state to the existing dispatcher. Preserve capture/bubble,
retargeting, once/passive/abort behavior, listener identity, handler ordering and default-action rollback.
Mutation observation is not an event bus. The new package does not emit legacy mutation events or
fire `load` just because it finished tokenizing. Browser translates parser/resource milestones to its
existing readiness/resource event algorithms.

## 7. XML, SVG, selectors and CSS are migration work

### XML and SVG

Prototype a `System.Xml.XmlReader` frontend that builds the new nodes directly, retaining one DOM.
Keep that backend internal so replacement is possible if browser parity tests expose an unbridgeable
semantic mismatch. Preserve whitespace/comments/CDATA/processing instructions, qualified names,
namespace declarations, doctype and XML document metadata. XML input is never routed through HTML.
Use no external resource resolver; never initiate network/file access as a side effect of parsing.
Internal DTD/entity support and finite expansion limits must be explicitly tested, not equated with
“DTD disabled”. See the [BCL reader settings](https://learn.microsoft.com/en-us/dotnet/api/system.xml.xmlreadersettings?view=net-10.0).

Keep the direct API's syntax failure distinct from Browser's `DOMParser` parsererror document. Test
all four XML MIME types, including XHTML routing, before deleting AngleSharp.Xml. SVG fragment handling
and HTML foreign-content integration share node/namespace storage but not tokenizer error policy.
Separate tasks implement SVG typed attributes/lists and existing interface brands where the binding
inventory requires them. No rasterization dependency is introduced.

Implement a new `XPathNavigator` over these nodes and retain BCL XPath evaluation. Test namespace axes,
attribute identity, document order and DOM XPath result conversions. Existing `ignoreNamespaces: true`
behavior is a documented browser divergence, not a native DOM invariant. Preserve it during the backend
swap or fix it in an independently tested change; do not make all standalone XPath queries ignore
namespaces to match one browser workaround. Mutation stamps enable proper iterator invalidation later.

### CSS and selectors

Implement a CSS Syntax tokenizer and component-value/rule parser before typed property grammars.
Preserve custom property spelling/token content and distinguish unknown rules from syntactically invalid
rules. The internal syntax representation can preserve opaque nodes even where CSSOM exposure omits
unsupported constructs. Declaration parsing, stylesheet parsing, CSSOM `insertRule`, selector parsing
and `CSS.supports` have different failure contracts and must select them deliberately.

Compile selectors into immutable engine-free programs. A program is reusable across documents; matching
receives document mode, namespace rules and a host state view for focus/hover/checked/validity/target.
It never retains a node or engine in a shared cache. Cover scope, relative selectors, escape processing,
forgiving lists only where the grammar permits them, pseudo specificity and shadow boundaries.
Use right-to-left matching and candidate indexes only after correct traversal baselines exist. Complex
selectors and invalid input must be bounded; regex is not the parser.

The replacement cascade must cover the browser's present supported surface: selector specificity,
source order, origins/importance, inheritance, initial/unset/revert behavior, shorthand expansion,
custom properties and `var()` cycles/fallback, conditional rules, inline style, stylesheet enablement,
imports, media/preferences and CSSOM writes. The inventory determines the exact property/rule set.
Advanced features such as layers/nesting/container queries cannot be accepted as implemented merely
because the syntax parser can retain their tokens. Mark their individual capability and tests.

`CssCascade`, `ResolvedStyle` and flat layout retain one authoritative path for computed/resolved
values. Port UA defaults and current corrections; preserve CSS rule identity and source association
for the protocol and rule-use coverage. Syntax objects, declaration blocks and stylesheets are usable
without a Browser. Environment-dependent evaluation takes an explicit context, not global process
state. Preserve the existing Browser-owned invalidation contract in `PageLayout.Invalidation`: mutation
scopes clear on entry/exit and prohibit cache reuse during reentrant callbacks, with explicit fallbacks
for unknown writers and asynchronous attachment. Native change stamps can later replace conservative
invalidation only with proof that every write path is covered. Keep CSSOM invalidation distinct from
DOM mutation records. See `docs/design/layout-invalidation.md`.

## 8. Binding and browser migration strategy

Keep one backend active per page. Do not mirror AngleSharp and native trees in a live page: wrapper
identity, adoption, mutations and selectors would immediately have two possible answers. During
development both implementations may exist in separate test fixtures or builds. No runtime backend
switch is required in the final public API.

Replace AngleSharp reflection metadata with a checked-in WebIDL-derived manifest plus a typed native
mapping. Pin upstream IDL/spec inputs and document generated versus curated fields. The manifest
describes names, inheritance, mixins, overloads, nullability, conversions, enum strings, exposure,
legacy indexed/named behavior and property attributes. Native mappings select semantic helper methods
and interface brands without requiring one CLR interface per WebIDL interface.

Continue generating `JsObjectShape`s, registry/type dispatch and collection accessors in tooling;
continue checking generated output for staleness. Never hand-edit `.g.cs`. Keep wrapper identity in the
engine's weak table keyed on the new object and prototype/constructor state per realm. Cross-document
adoption cannot rebrand already-created wrappers. Add generated surface diffs so lost members are
reviewable rather than silently skipped as “unsupported conversion”.

Migration order is native algorithms → generated adapters → leaf consumers → page orchestration.
Compile an alternate browser test build against the new backend while default shipping remains
AngleSharp. This can be a temporary build property and separate output path; it must not add a public
configuration matrix. If temporary adapters help leaf conversion, they wrap the new DOM and live in
Browser, not in the new package as public AngleSharp-compatible contracts. Delete them when unused.

Port form-control state, element reflection, live collections and resource activation explicitly;
these are currently supplied in part by AngleSharp and do not appear by replacing `INode`. Likewise
frame context creation must become Browser-owned. The existing thread-safe `Page` API and snapshot
boundary remain. Remove `ParserBaton` only when the new coordinator owns all nested scripts, resource
waits, frames and task budgets; not when one inline script happens to work.

## 9. Correctness gates

1. **Native parser corpus.** Pin and vendor applicable [html5lib tokenizer/tree-construction data](https://github.com/html5lib/html5lib-tests)
   with licensing/provenance. Assert trees including namespaces, attributes, comments, doctypes and
   template content, not only serialized text. Split each short fixture at every boundary, and use
   deterministic partition sampling for large ones. Run scripting-enabled/disabled and fragment contexts.
2. **Mutation semantics.** Test direct .NET mutations and JS bindings over the same algorithms: adoption,
   replace-all, fragments, namespaced attributes, removed-subtree observation, repeated observe,
   disconnect/takeRecords, old values, ranges, iterators and collection liveness. Assert microtask order
   against promises and custom-element reactions, not just record counts.
3. **Browser parity.** Run the complete existing `Jint.Tests.Browser` suite against each backend with
   identical fixture inputs. Preserve framework fixtures and actual Puppeteer/Playwright/CDP paths,
   not merely standalone tree snapshots. Exercise DOMParser, CSS supports/media, XPath and SVG separately.
4. **WPT.** Use the existing pinned corpus and upstream harness. No WPT-only parser setting, altered
   vendor files, lowered registration minima, widened globs or raised failure ceilings to absorb
   migration regressions. Record exact failing subtests and the responsible task. A newly passing
   test narrows its exclusion and refreshes the census; harness errors invalidate the run.
5. **Scheduling.** Controlled local-server tests for parser blockers, delayed CSS/scripts, async/defer/
   modules, nested writes, write-then-query, scripts at EOF, mutation during custom constructors,
   nested frames, source changes after insertion, navigation/close races and stale completions.
   Specify event/checkpoint traces and partial-DOM assertions. Timeouts are wedge ceilings, not proof
   of the required ordering.
6. **Robustness.** Deterministic fuzzing and differential comparisons, with minimized repros for
   disagreements. AngleSharp is a useful comparator but standards/WPT decide discrepancies. Test
   deep malformed input, huge tokens/entities, repeated fragments, observer retention and cancellation.
   No stack recursion proportional to adversarial nesting; check for superlinear hot cases explicitly.
7. **Shipping.** Release build/test both frameworks, public API snapshots, host-contract verification
   where applicable, generated bindings, native standalone and browser-tool smoke tests, dependency
   graph/packed NuGet checks, and all existing browser consumers. Parsing must not fetch or run script.

Do not claim full WPT or browser conformance from a selected corpus. Track implementation progress by
the same case IDs and capabilities across default browser, proposed backend and standalone tests.

## 10. Comparable performance evidence

No speedup is claimed by this proposal. Create benchmarks before optimizing or switching the default.
Use the repository's BenchmarkDotNet configuration, Release builds, production tiering/PGO, an idle
machine, `JINT_BENCH_MODE=gate` for quoted comparisons, and paired alternating worktree runs. Never quote
`--job short`. Use at least six paired rounds for small effects; keep control rows and report intervals,
per-launch variance and inconclusive results according to `Jint.Benchmark/AGENTS.md`.

| Benchmark family | Equal work and output | Measures |
| --- | --- | --- |
| HTML document/fragment | Same string, context, scripting flag, recovery and DOM retention | Time, throughput, bytes allocated, GC |
| Malformed/adversarial HTML | Equal semantic output, increasing sizes, same limits | Scaling, peak memory, cancellation responsiveness |
| Text-heavy and script-heavy | Large text and many `<` characters; tree read after parsing | Copy amplification and retained memory |
| XML/SVG | Same well-formedness/namespace/entity settings and equivalent DOM | Parser plus tree cost, no rendering |
| CSS | Same rules/declarations/property support and CSSOM access | Parse, validation and allocation separately |
| Selectors | Same DOM, compiled versus parse-and-match separately | Matching, query results, cache lifetime |
| Mutation | No observers; one observer; subtree/old-value observers; native and JS paths | Record cost, live views, detached-node retention |
| Browser navigation | Same local responses, scripts/CSS, viewport and JS backend | Total load, first-script/readiness latency, thread/memory footprint |
| Byte-stream phase | Same bytes, encoding, chunk schedule and semantic result | Peak input retention and time-to-first-script |

Pin the four current AngleSharp versions and record runtime/OS/CPU/corpus hashes. Parser-only comparison
must exclude Jint on both arms. Browser comparison uses Jint on both arms and includes the full equivalent
CSS/DOM behavior. Do not compare token-only parsing with tree construction, inert parsing with executing
scripts, or omit styles/observers from only the new arm. Validate outputs outside measured intervals and
consume a result from every measured invocation. Fresh parsing gets a fresh document; repeated queries
may reuse an immutable fixture when both sides do. Separate cold construction from warm parser reuse.

Keep one engine/page per workload, and amortize cheap `Page.EvaluateAsync` rows with workload-specific
in-script loops as existing browser benchmarks do. Peak retained memory requires a separate process/
heap measurement; BDN allocated bytes alone cannot show retention through detached nodes or source slices.

The switch gate is correctness plus no unresolved statistically supported regression on the agreed
representative suite. Before measurement, identify throughput/allocation priorities and tolerances with
the maintainer; do not invent a promised “2x” speedup. Improvements to one row do not excuse regressions
hidden by a geometric mean. Investigate a confirmed regression or document a concrete accepted tradeoff.

## 11. Phased implementation and bounded tasks

Task IDs are intended for independent, reviewable feature changes, not permission to start additional
agents. Each task owns a finite algorithm/input set, its tests and its benchmark rows. Public signature
changes require API review before parallel implementation. The table names dependency completion, not
just a branch containing unfinished scaffolding.

| ID | Deliverable and acceptance boundary | Depends on |
| --- | --- | --- |
| A1 | Machine-readable native/binding usage inventory and current API/WPT baseline; every AngleSharp use assigned | None |
| A2 | Standalone project, public parsing contract snapshots, limits/errors, package/native smoke consumer | A1 |
| A3 | Equivalent AngleSharp benchmark fixtures and semantic output comparers; no performance claims yet | A1 |
| D1 | Node links/identity/namespaces and insert/remove validity; detached trees and document invariants | A2 |
| D2 | Ordered attributes, `Attr` identity, namespace setters and text/comment/PI/doctype nodes | D1 |
| D3 | Adopt/import/clone/replace-all plus fragment/template ownership tests | D1, D2 |
| D4 | Live collection, static result and traversal primitives; mutation-version correctness | D3 |
| D5 | Observer registration/filtering/records, transient registrations and pull subscriptions | D3 |
| D6 | Range and iterator mutation fixups, shadow-root/slot tree primitives | D3, D5 |
| H1 | Resumable input cursor, chunk ownership, diagnostics, cancellation and tokenizer corpus runner | A2 |
| H2 | Data/tag/attribute/comment/doctype tokens and character references; every split boundary | H1 |
| H3 | RCDATA/rawtext/script-data/plaintext states and EOF transitions; linear text accumulation | H2 |
| H4 | Initial/before-html/head/body/text insertion modes and implied elements | H3, D3 |
| H5 | Table/caption/column/row/cell modes and foster parenting | H4 |
| H6 | Formatting reconstruction/adoption agency, select/template/frameset and after-body modes | H4, H5 |
| H7 | SVG/MathML foreign content, integration points, fragment contexts and quirks cases | H6 |
| H8 | Host pauses, insertion frames, nested write-then-query, secondary-document incremental writes and pending EOF fixtures | H6, D5 |
| X1 | XML frontend decision spike, XML corpus, entity/DTD policy and namespace-preserving tree build | D3 |
| X2 | `ParseSvg`, SVG/HTML distinction, DOMParser error mapping fixtures and SVG native-state inventory | X1, H7 |
| X3 | HTML/XML serialization and round-trip invariants | H7, X1 |
| X4 | XPath navigator and node-result conversion tests | X1, D4 |
| C1 | CSS tokens/component values, rules and declaration recovery; CSS syntax corpus | A2 |
| C2 | Selector grammar/programs, specificity, namespace/scope and static-state matching | C1, D4 |
| C3 | Selector dynamic-state adapter and HTML pseudo-class parity | C2, B2 |
| C4 | CSSOM rules/declarations, mutation/versioning, serialization and insert/delete contracts | C1, D5 |
| C5 | Property grammar inventory and finite property groups; shorthands/custom values/supports | C4 |
| C6 | Cascade origins/order/inheritance/variables plus UA sheets and media evaluation | C2, C5 |
| B1 | New binding manifest/emitter, surface-diff and staleness tests; Node/Element vertical slice | A1, D2 |
| B2 | Form-control state, reflection and collection adapters, split by controls/table/document families | B1, D4 |
| B3 | Event-path/activation, custom-element reaction hooks, mutation microtask and DevTools observers | B1, D5, D6 |
| B4 | Detached DOMParser/SVG/XMLSerializer/XPath bindings; cross-realm adoption/wrapper preservation | B1, X2, X3, X4 |
| B5 | CSS bindings/computed style/media/protocol coverage and flat-layout adapter | B1, C3, C6 |
| R1 | Page-loop parser coordinator for inline/external blockers and styles; cancellation/turn budgets | H8, B3 |
| R2 | Ordered defer/module/import-map and async scheduling, readiness/resource event traces | R1, B5 |
| R3 | Dynamic script/style/image/frame activation and nested frame document/realm ownership | R2, B2, B4 |
| R4 | Extraction/accessibility/layout/protocol leaf migration and complete alternate-backend browser suite | B4, B5, R3 |
| R5 | Displayed-document post-parse open/write/close lifecycle and listener-reset behavior | R3 |
| G1 | Full existing corpus/framework/client parity, benchmark gate, dependency/package audit; switch default | R4, A3 |
| G2 | Remove temporary backend build, AngleSharp production references, old metadata pin and baton; update docs/instructions | G1 |
| S1 | Bounded byte transport, encoding sniff/decode and chunk backpressure with no repeated script execution | G1, H8 |
| S2 | Optional public stream overloads after ownership/cancellation API review and streaming comparison | S1 |

`C5`, `B2`, `R3` and `R4` are task families: before assigning them, split their A1 inventory into named,
finite groups with independent regression sets. For example C5 has separate color/display/visibility,
box lengths/shorthands, font/text, and custom-value groups; R3 separates dynamic scripts, styles,
images and frames. A task named only “implement CSS” or “replace DOM” is not bounded enough to start.

Phases and exit criteria:

1. **Inventory and contracts (A).** Prove how completion will be measured before building the replacement.
2. **Standalone vertical slice (D/H beginnings, X/C in parallel where independent).** Parse/query/serialize
   a real document with native mutation records. Package remains explicitly experimental.
3. **Native completeness for current browser surface (D/H/X/C).** Standard recovery corpus passes;
   missing CSS/SVG/DOM capabilities are named rather than silently accepted.
4. **Binding and runtime integration (B/R).** Keep the shipping backend until the alternate build passes
   its full parity gates. The longest dependencies are CSS semantics, element state and script scheduling,
   not scanning characters.
5. **Switch and cleanup (G).** Remove all production AngleSharp references and obsolete workarounds only
   after tests demonstrate their replacement. Preserve benchmark comparators in development tooling.
6. **Extended conformance and streaming (R5/S if not landed earlier).** Clearly label remaining behavior
   gaps; the dependency replacement can finish while independently tracked conformance work continues.

Expect a multi-month campaign for the present browser surface, not a one-task parser rewrite. An initial
planning allowance is roughly 6–12 engineer-months across native algorithms, CSS/DOM parity, generator
work and migration, with low confidence until A1 and the standalone vertical slice finish. Independent
work can reduce elapsed time but not the integration dependencies. Full CSS/SVG/rendering conformance
is outside that estimate. Re-estimate from completed task families; do not equate a packaged prototype
with a browser ready to switch.

## 12. Decisions to revisit with evidence

The recommended defaults are decided above so implementation can proceed: managed package, modern
framework floor, standard recovery, inert standalone APIs, one DOM, resumable string core and Browser-owned
execution. Three choices need measured checkpoints, not open-ended up-front configuration: XML reader
backend viability (X1), compact attributes/node representation (D/A3), and CSS property scope from the
actual consumer inventory (A1/C5). Byte streaming is deliberately independent of the first browser switch.

A successful prototype should demonstrate nested `document.write`, native and JS observation of the
same mutation, CSS query/cascade agreement, and equivalent AngleSharp comparison on both well-formed
and malformed fixtures. Those tests determine whether the architecture can replace the dependency;
parsing a static page alone does not.
