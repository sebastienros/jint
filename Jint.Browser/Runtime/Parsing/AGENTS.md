# Agent instructions: the parser driver

> **Read this when:** You are touching `Jint.Browser/Runtime/Parsing/` — how a document is parsed on a thread
> of its own and served by the page loop, how a script, a module, an import map or a style sheet loads, and
> what `document.write` may do.
>
> This is one of the co-located instruction files indexed from the repository-root
> [`AGENTS.md`](../../../AGENTS.md). Read that first, then [`Jint.Browser/Runtime/AGENTS.md`](../AGENTS.md)
> for the page loop, the thread rule and the budgets. Nothing below is repeated in either.

### The parser driver, and the baton

`Runtime/Parsing/` is the parse: `ParserDriver` owns it, `ParserBaton` is the hand-off, `PageResourceLoader`
is what AngleSharp asks for a subresource, `PageScriptingService` is what it asks to run a script, and
`PageModuleScriptLoader` plus `ImportMap` are the module half AngleSharp does not have.

**Why there are two threads.** AngleSharp's parse is an asynchronous method whose every `await` carries
`ConfigureAwait(false)`, and `HtmlDomBuilder.ParseAsync` awaits the task a script's `RunAsync` produces. The
moment anything in that chain genuinely suspends — an external `<script src>` is the first thing that does —
the parse, and the scripting hook with it, resume on a pool thread. Driving it on the loop and blocking would
put the engine and the DOM in two threads' hands with nothing to say so. So the parse runs on a thread of its
own and hands a **baton** back to the loop for everything that needs the engine or the DOM.

**The hand-off is a blocking handshake.** `ParserBaton.RunOnLoop` queues the work, wakes the loop through
`engine.Tasks.Post`, and parks the parser thread on a `ManualResetEventSlim` until the loop has finished it.
That is a stronger form of the design's `RunContinuationsAsynchronously`: the parser cannot resume inline on
the loop thread because it cannot resume at all until the loop releases it. **The whole invariant is that one
property**, and it is why nothing else in the driver needs a lock.

**Timers fire exactly where a browser fires them.** While the parser is tokenizing it holds the baton and the
loop runs nothing, which is right — in a browser the parser *is* the task the event loop is running. While the
loop is fetching a parser-blocking script it holds the baton and pumps `ProcessTasks`, so timers, promise jobs
and animation frames run while the page waits for the network. `ParserBaton.PumpUntil` is that pump, and
`DocumentLoadTests.TimersFireWhileAParserBlockingScriptIsOnItsWay` is the proof (it reads zero without it).
What that pump swallows it reports: a job erupting out of `ProcessTasks` must not end the load, and it must
not vanish either, so it goes to the page recorder — which is the only way an execution constraint aborting
a timer mid-parse is visible at all.

**A script that navigates does not cut the parse short.** `location.href = '…'` takes the navigation gate
the running load still holds and its commit is a mailbox request, and the whole parse is *one* such request
— so every remaining script of the outgoing document runs, the load finishes, and only then is the engine
replaced and its token cancelled. Nothing stops a document mid-parse except closing the page, which cancels
the token every subresource fetch is linked to: the fetch in flight fails, the parse runs out and
`ParserBaton.Serve` stops serving.

**Every turn inside the parse is a turn.** A document load is *one* mailbox request, so `PageLoop` opens one
budget turn around the whole of it — and that turn's deadline is wall-clock, so by the time a slow
`<script src>` has come back it is long gone. Each script therefore takes a nested turn of its own
(`ParserDriver.Execute`, `RunModule`), and so does **each drain of the job queue** the pump runs
(`ParserBaton.Drain`), which is the same rule `PageLoop.Pump` follows for the drains it runs itself. Without
that last one a timer callback that fires late in a slow load is killed for the enclosing turn's exhaustion
rather than its own — `DocumentLoadTests.ATimerThatFiresLateInASlowLoadGetsABudgetOfItsOwn` is that case, and
it fails without the bracket. A budget running out is a `PageErrorKind.BudgetExceeded` and the load goes on,
which is what `PageBudget` promises and what makes a page whose first script is a loop still a page a host
can read.

**The residual, stated because it is the one thing a budget does not reach.** `Serve` holds the loop for the
whole parse, so while the baton is in the parser's hands the loop runs nothing and an execution constraint
cannot fire. Every fetch is bounded by `BrowserOptions.SubresourceTimeout` and every script and every drain
takes a turn, which leaves AngleSharp's tokenizer as the only unbounded step. There is still no *total*
budget on the parse — `PageLoop`'s turn is the enclosing one, and nothing re-arms it for the parse as a whole
— so what a wedged parser thread costs is bounded by the page's own token instead: closing the page ends
`Serve`, abandons the parse and fails the parser's next hand-off, so `Page.CloseAsync` is never held by a
parse that will not finish.

**Every fetch the parser asks for finishes before the loader returns.** `PageResourceLoader.FetchAsync` hands
the baton over, the loop fetches while pumping, and AngleSharp receives an already-completed `IDownload` — so
the parse never suspends and the thread it runs on never changes. `ParserBaton.ParserHopped` is the check
rather than the belief: a change of parser thread means a step suspended where none was expected, and it
becomes a page error. **A fetch a *script* triggered blocks instead of pumping**, because pumping from inside
a running script would run the page's jobs in the middle of one; that is the price of an inserted
`<script src>`, and it is stated in `ParserDriver.Fetch`. One shape of inserted script does not run at all:
AngleSharp prepares one when it is *inserted*, so `el.src = '…'` **after** `appendChild` is never fetched —
set the source first, which is what a page does anyway.

**Who runs what.** A classic script — inline, external, `defer`, `async` — is prepared and ordered by
AngleSharp, which is what buys parser-blocking, document order, the deferred queue and the `document.write`
insertion point without re-implementing any of them. `PageScriptingService.SupportsType` answers `false` for
`module`, `importmap` and every unknown type, so AngleSharp never prepares them and the driver runs the
modules itself after the parse — which is where HTML puts them anyway. Four scheduling divergences follow and
each is deliberate: a `defer`/`async` script's *download* is not overlapped with the parse; an `async` script
executes in document order at the end of the parse rather than the instant its fetch lands; a deferred classic
script runs before a module script that precedes it in the document, because they are two queues rather than
HTML's one; and the first import map found anywhere applies to every module, because none of them could have
resolved before the parse ended anyway.

**`document.readyState` is the page's shadow.** `PageRuntime.ReadyState` moves `loading` → `interactive` →
`complete` and `ParserDriver.SetReadyState` fires the `readystatechange` that goes with each, because
`Document.ReadyState`'s setter is protected and unreachable from outside AngleSharp's assembly. AngleSharp's
own value is read at exactly one point — `ObserveReadiness`, on the way into a script, which is the only way
to see the moment it starts the deferred queue. `DOMContentLoaded` (bubbling, at the document) follows the
module scripts; `complete` and then `load` and `pageshow` (at the window) follow every subresource, which is
the order HTML gives and the reason a `load` listener reads `"complete"`.

**What is not fetched is recorded, not skipped.** An `<img>`, a frame's document, a non-stylesheet `<link>`:
there is no rendering to need them, so the reference goes into `Page.Requests` with a
`PageRequest.NotFetchedReason` and no socket is opened. A refusal and a failure are both a download that
completes with a `null` response, which is the shape AngleSharp's own processors already test for; the `load`
and `error` a *page* hears are dispatched through Jint's dispatcher, because AngleSharp's go into its own
listener lists. `integrity` and `crossorigin` are accepted and ignored, and say so here rather than in a
sentence nobody reads.

**`document.write` after the parse is refused.** During one it is AngleSharp's own call and it is right — its
writable text source inserts at the parser's index and the script processor restores the index afterwards, so
the written markup is the next thing the tokenizer reads. Afterwards HTML implies `document.open()`, which
AngleSharp implements by unloading through its own browsing context on the calling thread and rebuilding the
document behind the page's back; `PageHostHooks.Write` answers with a page error naming it instead.
