# Native parser handoff required by Browser

Astra-reviewed implementation sequence, September 25, 2026. The actual consumer is
`Jint.Browser/Runtime/Parsing/ParserDriver.cs` and its document.write path. This refines H8 in
[tree construction](html-parser-tree-construction-followups.md) and the
[resumable parser architecture](html-parser.md). It does not authorize a second DOM or scheduler.

## Input prerequisite: independent of foreign tree construction

The tokenizer owner may change HtmlInput, add HtmlTokenizer.Insertion, and narrowly extend
HtmlTokenizer/HtmlToken plus tests. TreeConstruction/session, DOM and Browser are separate owners.

The internal surface uses opaque tokenizer-owned insertion-point identities:

```csharp
HtmlInsertionPoint CreateInsertionPoint();
void InsertInput(HtmlInsertionPoint point, string text, CancellationToken cancellationToken);
HtmlReadStatus ReadUntil(HtmlInsertionPoint point, int quota, bool allowCData,
    CancellationToken cancellationToken, out HtmlToken token);
void ReleaseInsertionPoint(HtmlInsertionPoint point);
```

`InsertionBoundary` is distinct from NeedInput and EOF. Reject foreign/released identities and
operations during active reads. Preserve ordinary Read/AppendInput and the integrated per-read
CDATA-context latch; an insertion operation does not reset tokenizer state.

A marker sits immediately before the unread tail. Repeated insertions before it preserve write
order; nested scripts can place another marker before their own unread suffix. ReadUntil cannot
look ahead or consume beyond its marker. Deliver accumulated text, retain partial tags, references,
comments, raw/script endings and CRLF preprocessing, then report the boundary **without EOF recovery**.
Final tail closure is separate: insertion before unconsumed final EOF remains possible; actual
emitted EOF or terminal state cannot be reopened.

Use immutable string slices and constant-time splicing, without concatenating the whole input,
copying the unread tail, reparsing or shifting a list proportional to the remaining document.
Inserted characters count once toward the existing input limit. Validate cancellation/limits before
publishing an insertion. Charge marker traversal/cleanup and retain bounded quota/cancellation work.
Offsets remain monotonically consumed expanded-stream offsets, not original-document locations.
Any later original-source attribution must retain its own source-coordinate information.

Acceptance includes all short splits, quotas 1/3/large, repeated and nested writes, preserved outer
tail order, partial tags/entities/comments/script endings, CR then boundary then LF, pending EOF,
empty writes, foreign/stale markers, exact limits, cancellation atomicity and linear many-write work.

## Session handoff: after the H7 owner releases TreeConstruction

Extend the same session with returned host requests carrying unique completion identities. Native
code returns before Browser callbacks; it contains no engine, networking or scheduling. Standalone
sessions remain inert. The owner may add request/frame types and narrow builder text-mode hooks.

The protocol must preserve these distinct transitions:

1. A completed HTML script end tag reaches its boundary before later input is consumed.
2. When required, Browser performs the pre-preparation microtask checkpoint. This precedes the
   builder's script pop and insertion-mode restoration.
3. The builder pops/restores once, saves the previous insertion point, establishes a script marker
   and nesting frame, and returns preparation work with the exact native script identity.
4. Browser completes preparation as finished or pending parsing-blocking work. Inline preparation
   may run script and synchronously call document.write through the returned handoff.
5. A nested pending blocker stops inserted-input driving and returns control to the writing script.
   Do not synchronously fetch and execute it inside that nested document.write call.
6. After nesting returns to zero, Browser handles stylesheet/resource readiness and executes the
   pending blocker through a separate request. Completion restores insertion/nesting state once.

The minimum session operations are CompleteHostRequest(id, outcome), InsertInput(activeFrame, text,
token) and DriveInsertedInput(activeFrame, quota, token), alongside Drive. Reject stale/out-of-order
completion and reentrant/concurrent Drive during native mutation. Returning a host request permits
controlled subsequent entry; it is not permission for callbacks from inside a native mutation.

An unterminated script at EOF is not an execution request. Preserve its required already-started
state and continue EOF processing. Inert templates, fragment modes and no-host entry points retain
their own script semantics; a scripting grammar flag is not execution permission.

Browser retains currentScript save/restore, executable/result metadata, stylesheet readiness, resource scheduling,
page generations, close/navigation cancellation and budgets. During synchronous writes it repeats
cooperative yields without pumping unrelated tasks. Native stacks tolerate permitted host moves
without reconstructing parser state from current DOM parents. Keep ParserBaton until every one of
its remaining responsibilities has an implemented replacement.

Acceptance includes write-then-query, existing native/wrapper identity, nested inline scripts,
nested external blockers, pending EOF, split writes, host removal/adoption of open elements, stale
completions, cancellation during resource waits and secondary-document open/write/close.

The [document.write steps](https://html.spec.whatwg.org/multipage/dynamic-markup-insertion.html#document-write-steps)
stop at the insertion point or a tree-construction abort. The
[text insertion mode](https://html.spec.whatwg.org/multipage/parsing.html#parsing-main-incdata)
defines the checkpoint, nesting, blocker and EOF distinctions above. Recheck the living algorithms
at implementation review, retaining explicit tests for any deliberate Browser divergence.

## H8 state and ownership clarification

The current `HtmlElementState` has no script state, `NodeCloner` has no script cloning step, and
`InText` simply pops on EOF/end tags. H8 must supply the following intrinsic state rather than
reconstructing it from attributes, a Browser wrapper, or current tree ancestry. Add an internal
`HtmlScriptState` lazily owned by `HtmlElementState` for HTML-namespace `script` elements:

- `Document? ParserDocument`, initially null; derive `ParserInserted` from its non-nullness.
- `Document? PreparationTimeDocument`, initially null.
- `bool AlreadyStarted`, initially false, and `bool ForceAsync`, initially true.

These are native identity state, shared by parser and Browser preparation. They contain no engine,
callback, task, resource handle, or session reference. Browser owns the script kind, prepared source,
fetch/result, readiness and execution queues; it must not maintain a competing copy of these four
fields. Preparation may update the native fields while the session is suspended at its request.

HTML parser creation initializes parser-document/force-async **before** inserting the new element.
The parser document is the session's document, even when the adjusted destination has an inert
template owner or has been adopted by a host. Preserve destination ownership independently.
At EOF in HTML script text mode, set already-started before popping; do not emit preparation work.

The [script processing model](https://html.spec.whatwg.org/multipage/scripting.html#script-processing-model)
requires clone/import to copy already-started only. Add that step in `NodeCloner.CopySingle`, including
scripts reached through templates and clonable shadows. Do not copy parser/preparation documents,
pending work, or execution results. Preserve ordinary attribute-copy effects: an `async` attribute on
the copy clears its initial force-async flag. Adoption retains the original state object and document
identities; it does not reinitialize or retarget them.

There is one narrow additional `Element` hook: adding a null-namespace `async` attribute to an HTML
script clears force-async, including an add followed by removal before preparation. Removing it does
not restore force-async. Cover parser bulk initialization and clone attribute copying without adding
a scan to unrelated elements. Existing `Attr.Value` changes need no new addition hook. This does not
authorize changes to the separately owned `Node`, `Document`, or `CharacterNodes` files, or introduce
native callbacks for dynamic script execution. Browser handles dynamic preparation through its real
mutation/insertion integration.

### Preparation is not an unconditional already-started assignment

Browser must apply [prepare the script element](https://html.spec.whatwg.org/multipage/scripting.html#prepare-the-script-element)
at the returned preparation request, reading live attributes and child text then. It first honors
already-started, saves and clears parser-document, and applies the force-async transition. Empty,
disconnected, and unsupported-type early returns leave different state from a script that reached
the commitment steps. Only at those steps does it restore saved parser-document/clear force-async,
set already-started and capture preparation-time document, before checking document mismatch and
scripting permission. The native session must not preemptively set already-started on every end tag
or suppress a request merely because the element moved. This ordering permits a failed preparation
to become eligible after a later mutation.

The [execution algorithm](https://html.spec.whatwg.org/multipage/scripting.html#execute-the-script-element)
checks preparation-time document against the current node document. Removing a prepared element
alone is not an equivalent veto. A pending blocker that is adopted or removed continues to block its
original parser until that parser's readiness conditions are satisfied; adoption does not silently
discard the wait. Browser then applies execution's document check. A request therefore retains the
exact script and parser-document identities, never a lookup by current document or DOM position.

### Session frames, the pending slot, and aborts

Use an explicit continuation/frame stack; do not keep only one outstanding request field. Each frame
holds its unique identity, phase, script, saved insertion point, active marker, and entry nesting
level. A returned preparation request remains outstanding while its host executes inline code and
drives nested writes. Child requests complete before their parent, and each pop/restore happens once.
Repeated `Drive` while host work is required must not consume input or manufacture another identity.

Keep **one pending parsing-blocking slot for this parser document**, plus a distinct in-flight
wait/execute continuation. A child preparation can fill the slot while the outer preparation is
still outstanding; returning `Finished` for the outer request must not clear that child-created slot.
While a nested blocker pauses parsing, later writes by the outer script may still insert at its
restored marker, but must not tokenize. After nesting unwinds to zero, take and clear the pending slot
before returning the wait request. Tokenization stays blocked during this wait even though the slot
is now empty. An executing blocker may create a new pending blocker; process that slot after execution
unwinds instead of losing it when completing the previous request.

Expose the current nesting level in the preparation context so Browser can distinguish an inline
stylesheet blocker at level one from nested inline preparation. Host completion identifies the
pending script explicitly when needed; a plain boolean must not overwrite a pending script created
by nested driving. Readiness belongs to Browser, but only a matching completion may advance the
session from wait to execution. Waiting uses the original parser document's stylesheet state.

Add a terminal abort/invalidation operation usable after `document.open`, cancellation, or document
replacement. It invalidates every frame/completion identity and releases insertion markers without
replaying tree operations. A pre-preparation microtask checkpoint can itself invalidate the session:
check this before popping or preparing. Reject completion after abort, foreign IDs, duplicate or
out-of-order completion, and entry during an active native `Drive`. No DOM version check should reject
ordinary permitted host mutation; retain open-element identities and use the existing live adjusted
insertion-location algorithms on continuation. These transitions are constant-time per frame; any
bulk abort cleanup must use the existing cooperative work/cancellation policy.

### Scripting modes and SVG are distinct

Current HTML defines [Normal, Disabled, Inert and Fragment scripting modes](https://html.spec.whatwg.org/multipage/parsing.html#parser-scripting-mode).
The existing `ScriptingEnabled` grammar boolean cannot encode all four. Introduce an internal mode
at the session/builder seam, keeping public options unchanged. Every mode except Fragment sets
parser-document; all parser-created HTML scripts start with force-async false; Inert also sets
already-started at creation. Disabled controls `noscript` grammar. Future fragment callers must select
their algorithm's mode explicitly. No-host execution permission is separate from that mode, and
ordinary template descendants must not all be marked already-started merely because they are in a
template. Standalone operation returns no Browser requests and must document/test its inert execution
policy without conflating these metadata distinctions. H8 does not implement the unrelated fragment
entrypoints or a new public scripting option.

`HtmlTreeBuilder.Foreign` currently only pops SVG scripts. The
[foreign-content script branch](https://html.spec.whatwg.org/multipage/parsing.html#parsing-main-inforeign)
needs a distinct SVG host-processing request, used for both `</script>` and self-closing `<script/>`.
Pop first, establish the insertion marker/nesting frame, and set parser-pause throughout SVG host
processing. Writes can insert but cannot reenter tokenization; unwind nesting and restore the saved
insertion point afterward. Do not run the HTML pre-pop checkpoint/preparation branch for SVG or try
to obtain its state through HTML-only `GetHtmlState()`. Browser owns SVG's preparation/execution
integration; native H8 supplies this actual boundary and identity, not a fabricated HTML script.

Additional acceptance: clone/import of started and unstarted scripts (including template/shadow);
async-add/remove state; EOF already-started; empty/unsupported/disconnected preparation retries;
mutation at the pre-pop checkpoint; adopt before preparation and while blocked; removal after
preparation; nested inline write followed by an external blocker and another outer write; a blocker
that writes another blocker; stale completion after open/abort; and SVG writes that insert without
tokenizing until processing returns. Exercise quotas 1/3/large and chunk boundaries, and verify host
requests, marker restoration, and nesting transitions occur exactly once across cooperative yields.

## Script source-coordinate integration

Browser compilation needs the one-based line immediately after the accepted script start tag, not
its opening line or the tokenizer's current expanded offset. Add internal source kind/unit identity,
original UTF-16 offset, line and column; preserve existing expanded offsets independently. Primary
AppendInput chunks share one cursor; each inserted unit has its own cursor, shared by split input
nodes. Track CR/LF/CRLF across chunk boundaries. Capture the post-tag anchor through pending-token
and quota suspension, and classify source-unit crossings in both the start tag and script content.
Store nullable metadata on HtmlScriptState before insertion; existing clone behavior leaves it unset,
while adoption preserves identity and metadata. No Engine or source-string ownership belongs there.
Browser keeps columns script-relative and uses document-relative lines only for primary-origin code;
generated/mixed code uses explicit script-relative reporting. HTML error-location extraction is
implementation-defined: https://html.spec.whatwg.org/multipage/webappapis.html#extract-error-information .
SVG source metadata remains explicitly unavailable in this finite seam; do not invent HTML positions.
Tests cover multiline tags, all newline chunk splits, quotas, nested inserted input and resumed primary
input, mixed tags/content, pending tokens, clone and adoption. Browser error-line tests remain an
integration gate. Implementation owns HTML input/tokenizer/tree construction and HtmlScriptState only.
