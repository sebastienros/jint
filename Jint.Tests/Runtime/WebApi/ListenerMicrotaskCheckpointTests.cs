#if NET8_0_OR_GREATER
#nullable enable

using Jint.Runtime;

namespace Jint.Tests.Runtime.WebApi;

/// <summary>
/// The microtask checkpoint an event listener returns to — WebIDL's <i>call a user object's operation</i>
/// runs HTML's <i>clean up after running script</i>, which performs one whenever the JavaScript execution
/// context stack is empty.
/// <para>
/// https://html.spec.whatwg.org/multipage/webappapis.html#clean-up-after-running-script
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// The distinction every test here turns on is <b>who started the dispatch</b>. A dispatch entered from a
/// task — a <c>FileReader</c> step, an <c>XMLHttpRequest</c> completion, a host <c>dispatchEvent</c>, a
/// click from a protocol client — leaves the stack empty each time a listener returns, so a promise
/// reaction the first listener queued runs before the second listener starts. A dispatch a script started
/// (<c>target.dispatchEvent(e)</c>, <c>el.click()</c>) has that script on the stack, so there is no
/// checkpoint and every reaction waits for the end of the turn.
/// </para>
/// <para>
/// <c>Jint.Tests.Runtime.WebApi.XmlHttpRequestTests.AMicrotaskQueuedByTheLoadListenerRunsBeforeLoadend</c>
/// is the same rule at the place sebastienros/jint#3668 was reported from, and
/// <c>TreeDispatchTests.AHostDispatchOverATreeCheckpointsBetweenTwoListeners</c> is it over a path.
/// </para>
/// </remarks>
public class ListenerMicrotaskCheckpointTests
{
    /// <summary>
    /// The log lives on the CLR side, because half of what these tests assert is the order of things that
    /// happen <i>before</i> the entry that would drain the loop returns — and reading a JavaScript array
    /// back with <c>Evaluate</c> would drain it first.
    /// </summary>
    private static Engine CheckpointEngine(List<string> log)
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Events));
        engine.SetValue("record", new Action<string>(entry => log.Add(entry)));
        return engine;
    }

    private static void TwoListenersOnATarget(Engine engine)
    {
        engine.Execute("""
            globalThis.target = new EventTarget();
            globalThis.ev = new Event('ping');
            target.addEventListener('ping', () => {
                record('first');
                Promise.resolve().then(() => record('microtask'));
            });
            target.addEventListener('ping', () => record('second'));
            """);
    }

    /// <summary>
    /// A host <c>dispatchEvent</c>: the built-in is invoked directly, so no script frame is pushed and the
    /// execution context stack is empty when each listener returns.
    /// </summary>
    private static void DispatchFromTheHost(Engine engine)
    {
        var dispatchEvent = engine.Evaluate("EventTarget.prototype.dispatchEvent");
        engine.Call(dispatchEvent, engine.GetValue("target"), [engine.GetValue("ev")]);
    }

    [Test]
    public void AHostDispatchRunsTheFirstListenersMicrotaskBeforeTheSecondListener()
    {
        var log = new List<string>();
        var engine = CheckpointEngine(log);
        TwoListenersOnATarget(engine);

        DispatchFromTheHost(engine);

        // The checkpoint is inside the dispatch, so the reaction has already run when dispatchEvent returns.
        log.Should().Equal("first", "microtask", "second");
    }

    /// <summary>
    /// The negative case, and the one that must not change: a script on the stack means no checkpoint, so
    /// both listeners run before the reaction the first one queued.
    /// </summary>
    [Test]
    public void AScriptDispatchRunsEveryListenerBeforeTheMicrotask()
    {
        var log = new List<string>();
        var engine = CheckpointEngine(log);
        TwoListenersOnATarget(engine);

        // Execute drains the loop on its way out, so the reaction is in the log — behind both listeners.
        engine.Execute("target.dispatchEvent(ev);");

        log.Should().Equal("first", "second", "microtask");
    }

    /// <summary>
    /// A dispatch a script started from inside a host entry is still a script dispatch: what decides is the
    /// stack, not which side of the boundary the call came from.
    /// </summary>
    [Test]
    public void ADispatchFromAScriptFunctionAHostInvokedIsNotCheckpointed()
    {
        var log = new List<string>();
        var engine = CheckpointEngine(log);
        TwoListenersOnATarget(engine);
        engine.Execute("globalThis.fire = () => target.dispatchEvent(ev);");

        engine.Invoke(engine.GetValue("fire"));

        // Nothing drained yet — the arrow function is what dispatched, so there was no checkpoint.
        log.Should().Equal("first", "second");

        engine.Tasks.ProcessTasks();
        log.Should().Equal("first", "second", "microtask");
    }

    /// <summary>
    /// A single listener is checkpointed too, which is the half that makes two events fired from one task
    /// order correctly: the reaction runs before <c>dispatchEvent</c> returns rather than at the end of the
    /// turn.
    /// </summary>
    [Test]
    public void ASingleListenerIsCheckpointedWhenTheDispatchCameFromTheHost()
    {
        var log = new List<string>();
        var engine = CheckpointEngine(log);
        engine.Execute("""
            globalThis.target = new EventTarget();
            globalThis.ev = new Event('ping');
            target.addEventListener('ping', () => {
                record('listener');
                Promise.resolve().then(() => record('microtask'));
            });
            """);

        DispatchFromTheHost(engine);

        log.Should().Equal("listener", "microtask");
    }

    /// <summary>
    /// Two events fired from one host entry: everything the first event's listener queued has run before the
    /// second event is dispatched, which is the ordering <c>load</c> then <c>loadend</c> needs.
    /// </summary>
    [Test]
    public void TwoEventsFiredFromOneHostEntryAreSeparatedByTheCheckpoint()
    {
        var log = new List<string>();
        var engine = CheckpointEngine(log);
        engine.Execute("""
            globalThis.target = new EventTarget();
            globalThis.first = new Event('one');
            globalThis.second = new Event('two');
            target.addEventListener('one', () => {
                record('one');
                Promise.resolve().then(() => record('microtask'));
            });
            target.addEventListener('two', () => record('two'));
            """);

        var dispatchEvent = engine.Evaluate("EventTarget.prototype.dispatchEvent");
        var target = engine.GetValue("target");
        engine.Call(dispatchEvent, target, [engine.GetValue("first")]);
        engine.Call(dispatchEvent, target, [engine.GetValue("second")]);

        log.Should().Equal("one", "microtask", "two");
    }

    /// <summary>
    /// A whole <c>await</c> resumption is what the checkpoint has to cover, because that is what an
    /// <c>EventWatcher</c> is built out of: the listener resolves a promise, and the async function waiting
    /// on it has to reach its next <c>await</c> before the next listener runs.
    /// </summary>
    [Test]
    public void AnAwaitResumedByTheFirstListenerReachesItsNextAwaitBeforeTheSecondListener()
    {
        var log = new List<string>();
        var engine = CheckpointEngine(log);
        engine.Execute("""
            globalThis.target = new EventTarget();
            globalThis.ev = new Event('ping');
            let arrived;
            const waiting = new Promise(resolve => { arrived = resolve; });

            (async () => {
                await waiting;
                record('resumed');
                await null;
                record('resumed-again');
            })();

            target.addEventListener('ping', () => { record('first'); arrived(); });
            target.addEventListener('ping', () => record('second'));
            """);

        DispatchFromTheHost(engine);

        log.Should().Equal("first", "resumed", "resumed-again", "second");
    }

    /// <summary>
    /// A listener that throws is still cleaned up after: WebIDL performs the checkpoint on its way out of
    /// <i>call a user object's operation</i> whether the call completed or threw, before <i>inner
    /// invoke</i>'s step 2.10 reports it.
    /// </summary>
    [Test]
    public void AListenerThatThrowsIsStillCheckpointed()
    {
        var log = new List<string>();
        var engine = CheckpointEngine(log);
        engine.Execute("""
            globalThis.target = new EventTarget();
            globalThis.ev = new Event('ping');
            target.addEventListener('ping', () => {
                record('first');
                Promise.resolve().then(() => record('microtask'));
                throw new Error('boom');
            });
            """);

        // With no diagnostics sink the throw erupts from the dispatch, which is the EventTarget contract.
        var dispatchEvent = engine.Evaluate("EventTarget.prototype.dispatchEvent");
        Assert.Throws<JavaScriptException>(() => engine.Call(dispatchEvent, engine.GetValue("target"), [engine.GetValue("ev")]));

        log.Should().Equal("first", "microtask");
    }

    /// <summary>
    /// A dispatch entered from an event-loop job is the case sebastienros/jint#3668 reports: a
    /// <c>FileReader</c> step fires <c>load</c> with the stack empty, so the checkpoint falls between the
    /// two listeners registered for it.
    /// </summary>
    [Test]
    public void ADispatchFromAJobRunsTheFirstListenersMicrotaskBeforeTheSecond()
    {
        var log = new List<string>();
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Files));
        engine.SetValue("record", new Action<string>(entry => log.Add(entry)));

        engine.Execute("""
            const reader = new FileReader();
            reader.addEventListener('load', () => {
                record('load-1');
                Promise.resolve().then(() => record('microtask'));
            });
            reader.addEventListener('load', () => record('load-2'));
            reader.readAsText(new Blob(['x']));
            """);

        for (var i = 0; i < 12; i++)
        {
            engine.Tasks.ProcessTasks();
        }

        log.Should().Equal("load-1", "microtask", "load-2");
    }

    /// <summary>
    /// A <c>queueMicrotask</c> callback is a microtask, so the checkpoint runs it exactly as it runs a
    /// promise reaction — https://html.spec.whatwg.org/multipage/timers-and-user-prompts.html#dom-queuemicrotask
    /// queues one, and HTML's <i>perform a microtask checkpoint</i> runs the queue rather than one kind of
    /// entry on it (sebastienros/jint#3734).
    /// </summary>
    [Test]
    public void AQueueMicrotaskCallbackRunsInTheCheckpointToo()
    {
        var log = new List<string>();
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Events | WebApiFeatures.Timers));
        engine.SetValue("record", new Action<string>(entry => log.Add(entry)));

        engine.Execute("""
            globalThis.target = new EventTarget();
            globalThis.ev = new Event('ping');
            target.addEventListener('ping', () => {
                record('first');
                queueMicrotask(() => record('microtask'));
            });
            target.addEventListener('ping', () => record('second'));
            """);

        DispatchFromTheHost(engine);

        log.Should().Equal("first", "microtask", "second");
    }

    /// <summary>
    /// Every microtask at the head, not merely the first: a <c>queueMicrotask</c> callback that queues a
    /// reaction, and a reaction that queues a <c>queueMicrotask</c>, are all one checkpoint's work.
    /// </summary>
    [Test]
    public void TheCheckpointDrainsMicrotasksTheMicrotasksThemselvesQueue()
    {
        var log = new List<string>();
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Events | WebApiFeatures.Timers));
        engine.SetValue("record", new Action<string>(entry => log.Add(entry)));

        engine.Execute("""
            globalThis.target = new EventTarget();
            globalThis.ev = new Event('ping');
            target.addEventListener('ping', () => {
                record('first');
                queueMicrotask(() => {
                    record('micro-1');
                    Promise.resolve().then(() => {
                        record('reaction');
                        queueMicrotask(() => record('micro-2'));
                    });
                });
            });
            target.addEventListener('ping', () => record('second'));
            """);

        DispatchFromTheHost(engine);

        log.Should().Equal("first", "micro-1", "reaction", "micro-2", "second");
    }

    /// <summary>
    /// And the half that must not change: the checkpoint still stops at a <i>task</i>. Jint has one queue
    /// where HTML has a microtask queue and a set of task queues, so a microtask queued behind a task cannot
    /// be run without reordering the queue - it waits for the turn's own drain, exactly as it did before the
    /// classification existed.
    /// </summary>
    [Test]
    public void TheCheckpointStopsAtATaskEvenWhenAMicrotaskIsQueuedBehindIt()
    {
        var log = new List<string>();
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Events | WebApiFeatures.Timers));
        engine.SetValue("record", new Action<string>(entry => log.Add(entry)));
        engine.SetValue("postTask", new Action(() => engine.Tasks.Post(() => log.Add("task"))));

        engine.Execute("""
            globalThis.target = new EventTarget();
            globalThis.ev = new Event('ping');
            target.addEventListener('ping', () => {
                record('first');
                postTask();
                queueMicrotask(() => record('microtask'));
            });
            target.addEventListener('ping', () => record('second'));
            """);

        DispatchFromTheHost(engine);

        log.Should().Equal("first", "second");

        engine.Tasks.ProcessTasks();
        log.Should().Equal("first", "second", "task", "microtask");
    }

    /// <summary>
    /// The fast path: a dispatch with nothing queued behind it neither runs nor observes a job, whichever
    /// side started it.
    /// </summary>
    [Test]
    public void ADispatchWithNothingPendingIsUnaffected()
    {
        var log = new List<string>();
        var engine = CheckpointEngine(log);
        engine.Execute("""
            globalThis.target = new EventTarget();
            globalThis.ev = new Event('ping');
            target.addEventListener('ping', () => record('first'));
            target.addEventListener('ping', () => record('second'));
            """);

        DispatchFromTheHost(engine);

        log.Should().Equal("first", "second");
    }
}
#endif
