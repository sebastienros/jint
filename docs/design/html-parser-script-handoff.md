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

Browser retains currentScript save/restore, metadata, stylesheet readiness, resource scheduling,
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
