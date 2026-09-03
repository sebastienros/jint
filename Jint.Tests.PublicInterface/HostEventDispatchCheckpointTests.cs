#if NET8_0_OR_GREATER
#nullable enable

using Jint;
using Jint.Native;
using Jint.Runtime;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// What an embedder that dispatches an event itself sees of the microtask checkpoint a listener returns to —
/// https://html.spec.whatwg.org/multipage/webappapis.html#clean-up-after-running-script.
/// </summary>
/// <remarks>
/// This is observable behaviour with no signature attached to it, and it changed
/// (<see href="https://github.com/sebastienros/jint/issues/3668">#3668</see>), so it is pinned from outside
/// the assembly: a promise reaction an event listener queues now runs before the next listener of that
/// dispatch, and only when the dispatch was entered with no script on the stack. The engine-internal
/// statement of the same rule is <c>Jint.Tests.Runtime.WebApi.ListenerMicrotaskCheckpointTests</c>.
/// </remarks>
public class HostEventDispatchCheckpointTests
{
    private static Engine Target(List<string> log)
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Events));
        engine.SetValue("record", new Action<string>(log.Add));
        engine.Execute("""
            globalThis.target = new EventTarget();
            target.addEventListener('ping', () => {
                record('first');
                Promise.resolve().then(() => record('microtask'));
            });
            target.addEventListener('ping', () => record('second'));
            """);
        return engine;
    }

    /// <summary>The dispatch a host performs itself: nothing of its own is on the stack.</summary>
    private static void Dispatch(Engine engine)
    {
        var dispatchEvent = engine.Evaluate("EventTarget.prototype.dispatchEvent");
        engine.Call(dispatchEvent, engine.GetValue("target"), [engine.Evaluate("new Event('ping')")]);
    }

    [Test]
    public void AHostDispatchRunsAListenersReactionBeforeTheNextListener()
    {
        var log = new List<string>();
        var engine = Target(log);

        Dispatch(engine);

        log.Should().Equal("first", "microtask", "second");
    }

    [Test]
    public void AScriptDispatchStillRunsEveryListenerFirst()
    {
        var log = new List<string>();
        var engine = Target(log);

        engine.Execute("target.dispatchEvent(new Event('ping'))");

        log.Should().Equal("first", "second", "microtask");
    }

    /// <summary>
    /// A host that wants the old grouping queues the deferred half as a <i>task</i>: a checkpoint runs
    /// microtasks and never a task, so <c>Tasks.Post</c> is what still lands behind every listener.
    /// </summary>
    [Test]
    public void WorkPostedAsATaskStillRunsAfterTheWholeDispatch()
    {
        var log = new List<string>();
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Events));
        engine.SetValue("record", new Action<string>(log.Add));
        engine.SetValue("defer", new Action(() => engine.Tasks.Post(() => log.Add("task"))));
        engine.Execute("""
            globalThis.target = new EventTarget();
            target.addEventListener('ping', () => { record('first'); defer(); });
            target.addEventListener('ping', () => record('second'));
            """);

        Dispatch(engine);
        log.Should().Equal("first", "second");

        engine.Tasks.ProcessTasks();
        log.Should().Equal("first", "second", "task");
    }

    /// <summary>
    /// The budget the host entry armed keeps applying to what the checkpoint runs: the checkpoint is a
    /// continuation of the run in progress, not a new one, so a statement limit the dispatch is already
    /// spending is not refilled for the reaction.
    /// </summary>
    [Test]
    public void TheCheckpointSpendsTheDispatchsOwnStatementBudget()
    {
        var engine = new Engine(options => options
            .UseWebApis(WebApiFeatures.Events)
            .LimitStatements(60));

        engine.Execute("""
            globalThis.target = new EventTarget();
            target.addEventListener('ping', () => {
                Promise.resolve().then(() => { for (var i = 0; i < 500; i++) { } });
            });
            """);

        var dispatchEvent = engine.Evaluate("EventTarget.prototype.dispatchEvent");
        Assert.Throws<StatementsCountOverflowException>(
            () => engine.Call(dispatchEvent, engine.GetValue("target"), [engine.Evaluate("new Event('ping')")]));
    }
}
#endif
