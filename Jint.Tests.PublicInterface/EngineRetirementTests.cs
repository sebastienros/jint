#nullable enable

using Jint.Native;
#if NET8_0_OR_GREATER
using Jint.WebApi;
#endif

namespace Jint.Tests.PublicInterface;

public class EngineRetirementTests
{
    [Test]
    public void RetiringInsideAJobDropsQueuedAndNewJobsButLetsTheCurrentJobFinish()
    {
        using var engine = new Engine();
        var calls = new List<string>();
        engine.SetValue("retire", new Action(() => engine.Advanced.Retire()));
        engine.SetValue("record", new Action<string>(calls.Add));
        engine.Execute("""
            Promise.resolve().then(() => { retire(); record('current'); Promise.resolve().then(() => record('new')); });
            Promise.resolve().then(() => record('queued'));
            """);
        engine.Tasks.ProcessTasks();

        calls.Should().Equal("current");
        engine.IsRetired.Should().BeTrue();
        var evaluate = () => engine.Evaluate("1");
        evaluate.Should().Throw<InvalidOperationException>().WithMessage("*retired*");
    }

    [Test]
    public void RetirementAllowsNestedCallsInTheCurrentScript()
    {
        using var engine = new Engine();
        engine.SetValue("retire", new Action(() => engine.Advanced.Retire()));
        engine.SetValue("reenter", new Action(() => engine.Evaluate("1 + 1").AsNumber().Should().Be(2)));

        engine.Execute("class C { field = 1; } retire(); new C(); reenter();");

        engine.IsRetired.Should().BeTrue();
        Invoking(() => engine.Evaluate("1")).Should().Throw<InvalidOperationException>().WithMessage("*retired*");
    }

    [Test]
    public void APostedTaskCannotStartANewScriptAfterRetirement()
    {
        using var engine = new Engine();
        var refused = false;
        engine.Tasks.Post(() =>
        {
            engine.Advanced.Retire();
            Invoking(() => engine.Evaluate("1"))
                .Should().Throw<InvalidOperationException>().WithMessage("*retired*");
            refused = true;
        });

        engine.Tasks.ProcessTasks();

        refused.Should().BeTrue();
    }

    [Test]
    public async Task RetirementLetsAnAlreadyCompletedAsyncResultReturn()
    {
        using var engine = new Engine();
        engine.SetValue("retire", new Action(() => engine.Advanced.Retire()));

        var result = await engine.EvaluateAsync("retire(); Promise.resolve(42)");

        result.AsNumber().Should().Be(42);
        engine.IsRetired.Should().BeTrue();

        using var fromJob = new Engine();
        fromJob.SetValue("retire", new Action(() => fromJob.Advanced.Retire()));
        var jobResult = await fromJob.EvaluateAsync("Promise.resolve().then(() => { retire(); return 42; })");
        jobResult.AsNumber().Should().Be(42);
    }

    [Test]
    public async Task ConcurrentRetirementAndDisposalLeaveTheEngineDisposed()
    {
        for (var i = 0; i < 100; i++)
        {
            var engine = new Engine();
            var retirement = Task.Run(() => engine.Advanced.Retire());
            var disposal = Task.Run(engine.Dispose);

            await Task.WhenAll(retirement, disposal);
            engine.IsDisposed.Should().BeTrue();
        }
    }

    [Test]
    public void RetirementDropsLaterManualPromiseSettlement()
    {
        using var engine = new Engine();
        var calls = 0;
        Action<JsValue> resolve = null!;
        engine.SetValue("pending", new Func<JsValue>(() => {
            var registration = engine.Tasks.RegisterPromise();
            resolve = registration.Resolve;
            return registration.Promise;
        }));
        engine.SetValue("record", new Action(() => calls++));
        engine.Execute("pending().then(() => record())");

        engine.Advanced.Retire();
        resolve(42);
        engine.Tasks.ProcessTasks();

        calls.Should().Be(0);
        engine.Tasks.TimeUntilNextScheduledWork.Should().BeNull();
    }

    [Test]
    public void RetiredEngineRefusesPostsAndCanBeRetiredAgain()
    {
        using var retired = new Engine();
        using var active = new Engine();
        var calls = 0;
        retired.Advanced.Retire();
        retired.Advanced.Retire();

        var post = () => retired.Tasks.Post(() => calls += 10);
        post.Should().Throw<InvalidOperationException>().WithMessage("*retired*");
        active.Tasks.Post(() => calls++);
        retired.Tasks.ProcessTasks();
        active.Tasks.ProcessTasks();
        calls.Should().Be(1);
    }

    [Test]
    public void RetirementCannotBeReversedByRestoringGlobals()
    {
        using var engine = new Engine();
        var snapshot = engine.Advanced.CaptureGlobalSnapshot();
        engine.Advanced.Retire();

        var restore = () => engine.Advanced.RestoreGlobalSnapshot(snapshot);
        restore.Should().Throw<InvalidOperationException>().WithMessage("*retired*");
        engine.IsRetired.Should().BeTrue();
    }

    [Test]
    public void RetirementFencesAsyncFunctionContinuation()
    {
        using var engine = new Engine();
        var resumed = false;
        engine.SetValue("retire", new Action(() => engine.Advanced.Retire()));
        engine.SetValue("resume", new Action(() => resumed = true));
        engine.Execute("""
            Promise.resolve().then(() => retire());
            (async () => { await Promise.resolve(); resume(); })();
            """);
        engine.Tasks.ProcessTasks();
        resumed.Should().BeFalse();
    }

    [Test]
    public async Task RetirementWakesAnAsyncEvaluationWithNoPromiseTimeout()
    {
        using var engine = new Engine(options => options.Constraints.PromiseTimeout = TimeSpan.Zero);
        var evaluation = engine.EvaluateAsync("new Promise(() => {})");
        evaluation.IsCompleted.Should().BeFalse();

        await Task.Run(() => engine.Advanced.Retire());

        var completed = await Task.WhenAny(evaluation, Task.Delay(TestBudgets.WedgeCeiling));
        completed.Should().BeSameAs(evaluation, "retirement must wake a suspended evaluation even with no timeout");
        await Awaiting(() => evaluation).Should().ThrowAsync<InvalidOperationException>().WithMessage("*retired*");
        var dispose = () => engine.Dispose();
        dispose.Should().NotThrow("the asynchronous reservation must have been released");
    }

    [Test]
    public async Task RetirementFromAnotherThreadLetsTheActiveScriptFinish()
    {
        using var engine = new Engine();
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var completed = false;
        engine.SetValue("hold", new Action(() => {
            entered.Set();
            release.Wait(TestBudgets.WedgeCeiling).Should().BeTrue();
        }));
        engine.SetValue("complete", new Action(() => completed = true));

        var execution = Task.Run(() => engine.Execute("hold(); complete();"));
        try
        {
            entered.Wait(TestBudgets.WedgeCeiling).Should().BeTrue();
            engine.Advanced.Retire();
            engine.IsRetired.Should().BeTrue();
        }
        finally
        {
            release.Set();
        }

        var finished = await Task.WhenAny(execution, Task.Delay(TestBudgets.WedgeCeiling));
        finished.Should().BeSameAs(execution);
        await execution;
        completed.Should().BeTrue();
        var evaluate = () => engine.Evaluate("1");
        evaluate.Should().Throw<InvalidOperationException>().WithMessage("*retired*");
    }

    [Test]
    public async Task RetirementWakesAnIndefiniteHostPump()
    {
        using var engine = new Engine();
        var park = TopLevelPark.Start(engine, Timeout.InfiniteTimeSpan);
        park.WaitUntilOwningTheEngine();

        engine.Advanced.Retire();

        var completed = await Task.WhenAny(park.Completed, Task.Delay(TestBudgets.WedgeCeiling));
        completed.Should().BeSameAs(park.Completed, "retirement must wake a parked host loop");
        await park.Completed;
        park.Reported.Should().BeFalse();
    }

    [Test]
    public async Task RetirementWakesAnIndefiniteAsyncHostPump()
    {
        using var engine = new Engine();
        var waiting = engine.Tasks.WaitForScheduledWorkAsync(Timeout.InfiniteTimeSpan);
        waiting.IsCompleted.Should().BeFalse();

        engine.Advanced.Retire();

        var completed = await Task.WhenAny(waiting, Task.Delay(TestBudgets.WedgeCeiling));
        completed.Should().BeSameAs(waiting);
        (await waiting).Should().BeFalse();
    }

#if NET8_0_OR_GREATER
    [Test]
    public void RetirementReleasesSharedLocks()
    {
        var manager = new LockManager();
        using var holder = new Engine(options => options.UseWebApis().UseWebLocks(manager));
        using var waiter = new Engine(options => options.UseWebApis().UseWebLocks(manager));
        holder.Execute("navigator.locks.request('resource', () => new Promise(() => {}));");
        waiter.Execute("var granted = false; navigator.locks.request('resource', () => { granted = true; });");
        waiter.Evaluate("granted").AsBoolean().Should().BeFalse();

        holder.Advanced.Retire();
        holder.Tasks.ProcessTasks();
        waiter.Tasks.ProcessTasks();

        waiter.Evaluate("granted").AsBoolean().Should().BeTrue();
    }

    [Test]
    public void RetirementInsideAJobReleasesSharedLocksAfterTheJobReturns()
    {
        var manager = new LockManager();
        using var holder = new Engine(options => options.UseWebApis().UseWebLocks(manager));
        using var waiter = new Engine(options => options.UseWebApis().UseWebLocks(manager));
        holder.SetValue("retire", new Action(() => holder.Advanced.Retire()));
        holder.Execute("navigator.locks.request('resource', () => new Promise(() => {}));");
        waiter.Execute("var granted = false; navigator.locks.request('resource', () => { granted = true; });");

        holder.Execute("Promise.resolve().then(() => retire());");
        holder.Tasks.ProcessTasks();
        waiter.Tasks.ProcessTasks();

        holder.IsRetired.Should().BeTrue();
        waiter.Evaluate("granted").AsBoolean().Should().BeTrue();
    }

    [Test]
    public void RetirementAtAMicrotaskCheckpointReleasesResourcesAfterTheTask()
    {
        var manager = new LockManager();
        using var retired = new Engine(options => options.UseWebApis().UseWebLocks(manager));
        using var other = new Engine(options => options.UseWebApis().UseWebLocks(manager));
        retired.SetValue("retire", new Action(() => retired.Advanced.Retire()));
        retired.Execute("""
            const { port1, port2 } = new MessageChannel();
            port1.addEventListener('message', () => queueMicrotask(() => retire()));
            port1.addEventListener('message', () => {
                navigator.locks.request('shared', () => new Promise(() => {}));
                setTimeout(() => {}, 0);
            });
            port1.start();
            port2.postMessage('go');
            """);

        retired.Tasks.ProcessTasks();
        other.Execute("var got = false; navigator.locks.request('shared', () => { got = true; });");
        for (var i = 0; i < 20; i++)
        {
            retired.Tasks.ProcessTasks();
            other.Tasks.ProcessTasks();
        }

        retired.IsRetired.Should().BeTrue();
        other.Evaluate("got").AsBoolean().Should().BeTrue();
        retired.Tasks.TimeUntilNextScheduledWork.Should().BeNull();
    }

    [Test]
    public void RetirementInsideScriptDropsPendingIdleCallback()
    {
        using var engine = new Engine(options => options.UseWebApis());
        var idleCalls = 0;
        engine.SetValue("onIdle", new Action(() => idleCalls++));
        engine.SetValue("retire", new Action(() => engine.Advanced.Retire()));

        engine.Execute("requestIdleCallback(onIdle); retire();");
        engine.Tasks.ProcessTasks();

        idleCalls.Should().Be(0);
    }

    [Test]
    public void RetirementInsideScriptSuppressesPendingUnhandledRejectionEvent()
    {
        using var engine = new Engine(options => options.UseWebApis());
        var notifications = 0;
        engine.SetValue("onRejection", new Action(() => notifications++));
        engine.SetValue("retire", new Action(() => engine.Advanced.Retire()));

        engine.Execute("addEventListener('unhandledrejection', onRejection); Promise.reject('ended'); retire();");
        engine.Tasks.ProcessTasks();

        notifications.Should().Be(0);
    }
#endif
}
