# Agent instructions: the parser driver

> **Read this when:** You are touching `Jint.Browser/Runtime/Parsing/`: native document parsing, script,
> module, import-map or stylesheet loading, child frames, resource watches or `document.write`.
>
> Read the repository-root [`AGENTS.md`](../../../AGENTS.md) first, then
> [`Runtime/AGENTS.md`](../AGENTS.md) for the page loop and budgets and
> [`Jint.Browser/AGENTS.md`](../../AGENTS.md) for the native foundation and performance rule.

### The parser driver, and the baton

**The native parser and JavaScript run on the page loop.** `ParserDriver.Load` creates a native `Document`,
associates a `DomBrowsingContext`, and drives `HtmlParserSession` synchronously through
`ParserDriver.NativeSession.cs`. There is no dedicated HTML-parser thread. `PageResourceLoader`,
`PageScriptingService`, `PageStylingService`, `PageDocumentFactory` and their external service registrations
are not the integration model.

`ParserBaton` remains the resource-wait pump. Its older cross-thread handshake methods are not the native
session's parsing path; do not infer current scheduling from those methods' historical comments.
**Exactly one page-loop owner touches the engine and mutable tree.**

**A parser yield is not an event-loop turn.** The driver checks constraints around
`Drive(4096, cancellationToken)` / `DriveInsertedInput`, processes native resource records and completed
style elements, and continues on `Yielded`. It does not pump unrelated tasks between tokenizer quotas.
`HostRequest` exposes script preparation/execution, a pending blocking script, an SVG script or a microtask
checkpoint; the driver answers through `CompleteHostRequest`. `CustomElementReactions` is a separate
native yield where the driver drains the registry before continuing.

**Network waits and running scripts have different rules.** An eligible parser-blocking fetch uses
`ParserBaton.PumpUntil`, allowing scheduled tasks and their microtasks while the network is pending.
A fetch initiated inside running script blocks without pumping: otherwise page jobs would execute in the
middle of that script. The same rule applies to script-created styles, images and frames.
Pump failures are reported through the page recorder, never silently swallowed.

**Keep bounded work and identity across every hand-off.** Native parsing accepts cancellation and yields
cooperatively; the driver checks engine constraints, and each script/module and pumped task takes its
`PageBudget` turn. Closing cancels the document's token. Do not describe the tokenizer as an unbounded
external step, or treat a whole navigation as one fixed execution deadline: nested script/task turns and
separately bounded resource waits are intentional. `MaxDomNodes` checks the completed document separately
from the wrapper ceiling.

**A script navigation does not itself abort the current parse.** Navigation still passes through the gate
and commits through the loop mailbox. Preserve document cancellation and request identity when a network
wait pumps callbacks that can supersede a resource.

### Resource notifications without per-node parsing overhead

`ParserDriver.NativeResources` watches a document, connected shadow roots and selected element targets with
native `MutationSubscription`s. `MutationTracking` supplies immutable arrival facts; callbacks enqueue them
and schedule drains. They must not read a changing tree, fetch, parse CSS or run author script inside the
native mutation stack. `ParserDriver.NativeRecovery` preserves pending notification work across failures;
do not replace it with a catch-and-ignore path.

**The broad resource watch sets `OmitInertCharacterRecords`.** A single character node inserted/removed,
or character data changed, outside an HTML/SVG `style` or `script` cannot affect resources. Omitting those
records avoids paying for ordinary parsed text. It is an internal host optimization, not a DOM observer
option; script-visible observers must receive their complete records. The watch also enables
`CaptureHtmlMetaInsertions` for insertion-time default-style facts, rather than reconstructing them from
later tree state.

**Do not eagerly record every parser node's realm.** `DomRealm` associates the document and inert template
owner; `Document.AdoptionObserver` / `INodeAdoptionObserver` captures original brands just before native
adoption. Parsed nodes otherwise use their document's realm on first access. No post-parse creation-realm
tree walk or wrapper construction belongs here.

Resources drain at native parse boundaries and completed binding mutations. Inline styles are installed
when their text is complete; linked styles use request identities so a later change can supersede an older
fetch. Detached active-document images and unstarted scripts have targeted watches. Template content has
its native inert owner document: do not load it as an active page subtree.

### Scripts, modules and readiness

**Native script flags are authoritative.** Preparation reads `AlreadyStarted`, `ParserDocument`,
`ForceAsync` and `PreparationTimeDocument`; moves or clones must not acquire a second independent Browser
started flag. `PrepareNativeScript` checks type, connection, source/text, scripting policy and child-frame
eligibility. `RunNativeClassic` enters a child document's `RealmScope` when needed and brackets
`currentScript` through `Execute`.

The current scheduling is deliberately simpler than a rendering browser's:

- External `defer` and `async` classic scripts fetch sequentially during preparation and execute in the
  deferred list after tokenization, in encounter order. Downloads do not overlap parsing; `async` does not
  mean execute at the instant its response arrives.
- Deferred classic scripts run before the principal document's module pass. `PageModuleScriptLoader` and
  `ImportMap` implement that pass and dynamic imports; child modules/import maps are not supported.
- The first prepared import map is retained. It can be read mid-parse for a classic script's `import()`;
  module scripts run after parsing and see the retained map. Additional maps are reported and ignored.

`PageRuntime.ReadyState` / the child `DomRealm.ReadyState` are Browser lifecycle state.
`SetReadyState` fires `readystatechange` when the value changes. After parsing and deferred classics,
`FinishLoad` completes upgrades and modules, fires document `DOMContentLoaded`, finishes frame/resource
events, then sets `complete` and fires window `load` and `pageshow`. Keep callback checkpoints and budgets;
queueing an event at fetch completion alone can expose a resource whose DOM/CSSOM state is not installed yet.

### XML and inert documents

`DomContentType.IsXml` selects `DocumentKind.Xml` for XML MIME types, including XHTML, SVG and other
`+xml` types. `XmlDocumentParser` builds the actual native document; no external factory remapping or
dependency XML package is involved. Text navigations already arrive as the HTML `<pre>` skeleton from
`DocumentFetch`, while markup/XML preserve the decoded response's type.

**The XML parse has no HTML script host-request sequence.** XML document scripts are not executed by the
HTML session driver; keep that limitation distinct from `DOMParser`, whose documents must be inert.
`DOMParser` uses a scripting-disabled HTML session or the native XML parser without this resource driver.
It converts XML syntax failure to a `parsererror` document, not cancellation or resource-limit failure.
All Browser XML document and fragment paths connect the native parser's bounded work polls to
`Engine.Constraints.Check`; those polls observe the enclosing turn without re-arming it or pumping tasks.
`BrowserXmlParsing` uses `ExecuteWithMemoryAccounting` to charge native allocations to that same turn,
including script-free navigation.

### Resource loading and completion

`ParserDriver.FetchBytes` / `SubresourceFetch` use the page network position, redirect policy and
`MaxSubresourceBytes` / `SubresourceTimeout`. `about:blank` and `data:` need no socket; the latter reuses
`Jint/WebApi/Fetch/DataUrl.cs`, still checks the size ceiling and creates no network request-log entry.
`ResponseUrl` preserves the fragment for script locations and nested documents.
Unsupported resource kinds are recorded with `PageRequest.NotFetchedReason`; `integrity` and `crossorigin`
are not enforced by this loader.

**Stylesheet completion follows CSS processing.** `NativeCssStyleSheets` parses/attaches the sheet before
the driver queues `load`. Failure records an error rather than a success-shaped sheet. Import processing
uses `ParserDriver.NativeCssImports`. Outstanding resource events delay window `load`, including resources
inserted by earlier completion handlers. If `currentScript` is still set, delivery is deferred until the
outer script restores it.

**Images are header reads, not decoded bitmaps.** `Media/ImageSourceSet` chooses the URL against page
viewport/media state, `PageImages` stores request status, and `ImageHeader` reads intrinsic sizes from
supported containers. `img.decode()` observes availability rather than decoding pixels.
`MaxImageRequests` bounds attempts; zero records references without fetching or firing completion events.
Image events wait until tokenization ends, so a following script can register its handler; stylesheet
events may arrive in a parser network wait. `loading=lazy` is eager in this layout-free browser.
No header can establish pixel validity, EXIF orientation or animation frames; do not claim those.

### Frames

**Browser creates and owns `DomBrowsingContext`s.** `LoadFrame` processes iframe sources, builds a native
child document and parses it with the same driver. Missing/empty `src` produces `about:blank`, `srcdoc`
produces `about:srcdoc`, and network sources use the page's resource limits. `MaxFrameDocuments` is checked
before all these creation paths, including `srcdoc`; zero declines them.

Same-origin classic scripts with no sandbox-blocked scripting run in the child's `RealmScope`.
`FrameWindows` gives each document its own global, intrinsics, DOM/event constructors, current script,
readiness and window event target. A replaced document keeps its old creation brands. Creator origin/base
URL and sandbox-origin facts are captured before a fetch can pump; do not recompute them from a later
owner state.

`FinishFrame` delivers child readiness, `DOMContentLoaded`, nested frame loads, `complete`, `load` and
`pageshow`. Initial child completion is before principal window `load`; source mutations use the deferred
resource-event lane. Native callbacks do not enter the engine directly.

Cross-origin WindowProxy access, full sandbox policy, child module loading and independent child browser
services remain limited. Child Web APIs use page network configuration. Child location writes refuse
rather than navigating the wrong document or silently succeeding; legacy `<frame>` is not the iframe lane.

### Dynamic markup insertion

During HTML parsing, `ParserDriver.Write` inserts into the active native `HtmlScriptFrame` with
`InsertInput` and drives inserted input when the parser is not paused. The session owns insertion-point
and nesting state; Browser must not replace it with concatenation and whole-document reparsing.

After parsing, `DomHostHooks.TargetOf` distinguishes displayed from secondary documents. Replacing a
displayed page/frame document through `open` or `write` remains a no-op with a recorded page error; `close`
does nothing for a displayed document. Unloading, engine replacement and navigation cannot be smuggled
into a DOM setter. XML rejects dynamic markup insertion with `InvalidStateError`.

For a secondary document, `Dom/DynamicMarkupInsertion` keeps a native `HtmlParserSession` across writes.
`open` clears the existing document and sets `loading`; successive writes preserve nodes and tokenizer
state; `close` finalizes the session and sets `complete`. It runs no script and synthesizes no page load
events. Exact insertion-point edge cases beyond the headless parity target may remain unsupported, but
preserving the existing document/node identities is not optional.
