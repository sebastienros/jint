#if NET8_0_OR_GREATER
#nullable enable

using System.Diagnostics;
using Jint;
using Jint.Native;
using Jint.Runtime;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// The prioritized task scheduling API seen from outside the assembly: what a host has to write to get it,
/// what it gets when it writes nothing, and how the tasks reach the host's own pump.
/// </summary>
/// <remarks>
/// This project has no <c>InternalsVisibleTo</c>, so nothing here can reach the scheduler's queues directly —
/// which is the point. Everything is asserted through script and through the public options surface, exactly
/// as an embedder would have to.
/// </remarks>
public class WebApiSchedulerTests
{
    private static readonly string[] SchedulerGlobals = ["scheduler", "TaskController", "TaskSignal", "TaskPriorityChangeEvent"];

    /// <summary>
    /// A host-supplied clock, so that a suite exercising the <c>delay</c> option need not sleep.
    /// </summary>
    private sealed class ManualClock : TimeProvider
    {
        private long _timestamp;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => _timestamp;

        public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch.AddTicks(_timestamp);

        internal void Advance(int milliseconds) => _timestamp += milliseconds * TimeSpan.TicksPerMillisecond;
    }

    [Fact]
    public void ADefaultEngineHasNoScheduler()
    {
        var engine = new Engine();

        foreach (var name in SchedulerGlobals)
        {
            engine.Evaluate($"typeof {name}").AsString().Should().Be("undefined");
            engine.Evaluate($"'{name}' in globalThis").AsBoolean().Should().BeFalse();
        }
    }

    [Fact]
    public void UseWebApisInstallsTheScheduler()
    {
        var engine = new Engine(options => options.UseWebApis());

        engine.Evaluate("typeof scheduler").AsString().Should().Be("object");
        engine.Evaluate("typeof scheduler.postTask").AsString().Should().Be("function");
        engine.Evaluate("typeof scheduler.yield").AsString().Should().Be("function");

        foreach (var name in new[] { "TaskController", "TaskSignal", "TaskPriorityChangeEvent" })
        {
            engine.Evaluate($"typeof {name}").AsString().Should().Be("function");
        }

        // The default set is what UseWebApis() means, and it names the scheduler.
        new Options().UseWebApis().WebApi.Features.Should().HaveFlag(WebApiFeatures.Scheduler);
    }

    [Fact]
    public void TheFeatureCanBeAskedForOnItsOwn()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Scheduler));

        foreach (var name in SchedulerGlobals)
        {
            engine.Evaluate($"typeof {name}").AsString().Should().NotBe("undefined");
        }

        // ... and a host that asked for something else does not get it.
        var console = new Engine(options => options.UseWebApis(WebApiFeatures.Console));
        console.Evaluate("typeof scheduler").AsString().Should().Be("undefined");
    }

    [Fact]
    public void AGlobalTheHostRegisteredItselfWins()
    {
        var engine = new Engine(options => options
            .Configure(e => e.SetValue("scheduler", "host's own"))
            .UseWebApis());

        engine.Evaluate("scheduler").AsString().Should().Be("host's own");
    }

    [Fact]
    public void AShadowRealmHasNoScheduler()
    {
        var engine = new Engine(options => options.UseWebApis());

        foreach (var name in SchedulerGlobals)
        {
            engine.Evaluate($"new ShadowRealm().evaluate('typeof {name}')").AsString().Should().Be("undefined");
        }
    }

    [Fact]
    public void TasksRunInPriorityOrderOnTheHostsOwnPump()
    {
        var engine = new Engine(options => options.UseWebApis());

        var order = new List<string>();
        engine.SetValue("record", new Action<string>(order.Add));

        // Nothing has run yet when Execute returns? It has: Execute drains the loop once the script is done.
        engine.Execute("""
            scheduler.postTask(() => record('background'), { priority: 'background' });
            scheduler.postTask(() => record('visible'));
            scheduler.postTask(() => record('blocking'), { priority: 'user-blocking' });
            """);

        order.Should().Equal("blocking", "visible", "background");
    }

    [Fact]
    public void AHostSuppliedClockDrivesTheDelayOption()
    {
        var clock = new ManualClock();
        var engine = new Engine(options => options.UseWebApis(webApi => webApi.Timers.TimeProvider = clock));

        var ran = false;
        engine.SetValue("mark", new Action(() => ran = true));

        engine.Execute("scheduler.postTask(mark, { delay: 100 });");
        ran.Should().BeFalse();

        // The engine is pumped, but the host's clock has not moved.
        engine.Advanced.ProcessTasks();
        ran.Should().BeFalse();

        clock.Advance(100);
        engine.Advanced.ProcessTasks();
        ran.Should().BeTrue();
    }

    [Fact]
    public void AHostsOwnLoopIsEnoughToRunAChainOfTasks()
    {
        var engine = new Engine(options => options.UseWebApis());

        var done = false;
        engine.SetValue("done", new Action(() => done = true));

        engine.Execute("""
            scheduler.postTask(async () => {
                await scheduler.yield();
                await scheduler.postTask(() => {}, { delay: 20 });
                done();
            }, { priority: 'user-blocking' });
            """);

        var deadline = Stopwatch.StartNew();
        while (!done && deadline.Elapsed < TimeSpan.FromSeconds(5))
        {
            // Nothing but the host's own pump: no engine thread, no background timer.
            engine.Advanced.ProcessTasks();
            Thread.Sleep(5);
        }

        done.Should().BeTrue();
    }

    [Fact]
    public void APostedTaskIsAnOrdinaryPromiseAHostCanUnwrap()
    {
        var engine = new Engine(options => options.UseWebApis());

        engine.Evaluate("scheduler.postTask(() => 6 * 7)").UnwrapIfPromise().AsNumber().Should().Be(42);
    }

    [Fact]
    public async Task APostedTaskCanBeAwaitedThroughEvaluateAsync()
    {
        var engine = new Engine(options => options.UseWebApis());

        var result = await engine.EvaluateAsync("""
            (async () => {
                const value = await scheduler.postTask(() => 'ready', { delay: 5 });
                await scheduler.yield();
                return value;
            })()
            """);

        result.AsString().Should().Be("ready");
    }

    [Fact]
    public void PostTaskReportsEveryFailureAsARejection()
    {
        var engine = new Engine(options => options.UseWebApis());

        // A promise-returning operation never throws synchronously, so a host calling Evaluate gets a promise
        // back even for an argument the engine refuses outright.
        var rejected = engine.Evaluate("scheduler.postTask('not a function')");
        Assert.Throws<PromiseRejectedException>(() => rejected.UnwrapIfPromise());

        // The same for an abort, which carries whatever reason the script chose.
        var aborted = engine.Evaluate("""
            (() => {
                const controller = new TaskController();
                const promise = scheduler.postTask(() => 'never', { signal: controller.signal });
                controller.abort('stopped');
                return promise;
            })()
            """);

        var exception = Assert.Throws<PromiseRejectedException>(() => aborted.UnwrapIfPromise());
        exception.RejectedValue.AsString().Should().Be("stopped");
    }

    [Fact]
    public void ATaskScheduledBeforeARestoreNeverRunsAfterIt()
    {
        var clock = new ManualClock();
        var engine = new Engine(options => options.UseWebApis(webApi => webApi.Timers.TimeProvider = clock));

        var ran = false;
        engine.SetValue("mark", new Action(() => ran = true));

        var snapshot = engine.Advanced.CaptureGlobalSnapshot();

        engine.Execute("""
            scheduler.postTask(mark, { priority: 'background', delay: 50 });
            scheduler.postTask(mark, { delay: 50 });
            """);

        engine.Advanced.RestoreGlobalSnapshot(snapshot);

        clock.Advance(1000);
        engine.Advanced.ProcessTasks();

        ran.Should().BeFalse();

        // ... and the pooled engine schedules normally in its next cycle.
        engine.SetValue("mark", new Action(() => ran = true));
        engine.Execute("scheduler.postTask(mark);");
        ran.Should().BeTrue();
    }

    [Fact]
    public void OneOptionsInstanceGivesEachEngineItsOwnQueues()
    {
        var options = new Options().UseWebApis();

        var first = new Engine(options);
        var second = new Engine(options);

        var order = new List<string>();
        first.SetValue("record", new Action<string>(s => order.Add("first:" + s)));
        second.SetValue("record", new Action<string>(s => order.Add("second:" + s)));

        // Each engine's tasks are its own: nothing the first schedules can reach the second.
        first.Execute("scheduler.postTask(() => record('a'), { priority: 'background' });");
        second.Execute("scheduler.postTask(() => record('b'), { priority: 'user-blocking' });");

        order.Should().Equal("first:a", "second:b");
    }

    [Fact]
    public void AControllerTheHostKeepsCanStopScriptScheduledWorkLater()
    {
        var clock = new ManualClock();
        var engine = new Engine(options => options.UseWebApis(webApi => webApi.Timers.TimeProvider = clock));

        var order = new List<string>();
        engine.SetValue("record", new Action<string>(order.Add));

        // The controller stays in the host's reach as an ordinary JsValue, so a host can abort a batch of
        // script-scheduled work between its own calls into the engine.
        var controller = engine.Evaluate("globalThis.controller = new TaskController({ priority: 'background' })");
        controller.IsObject().Should().BeTrue();

        engine.Execute("""
            scheduler.postTask(() => record('governed'), { signal: controller.signal, delay: 5 })
                .catch(e => record('rejected:' + e.name));
            scheduler.postTask(() => record('plain'), { priority: 'user-visible' });
            """);

        // The undelayed task has already run — Execute drains the loop — and the delayed one is still waiting
        // on the host's clock.
        order.Should().Equal("plain");

        engine.Execute("controller.abort();");

        clock.Advance(50);
        engine.Advanced.ProcessTasks();

        order.Should().Equal("plain", "rejected:AbortError");
    }
}
#endif
