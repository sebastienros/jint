using Jint.Browser;
using Jint.Browser.Runtime;
using Jint.Constraints;
using Jint.Runtime;
using Jint.Tests.Browser.DevTools;

namespace Jint.Tests.Browser.Runtime;

public sealed class PageTaskBudgetTests
{
    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(30);

    [Test]
    public async Task SeparateQueuedTasksReceiveSeparateBudgets()
    {
        var clock = new BudgetClock();
        await using var browser = new global::Jint.Browser.Browser(Options(clock));
        var page = await browser.NewPageAsync();
        var completed = 0;

        await page.RunOnLoopAsync(engine =>
        {
            for (var i = 0; i < 3; i++)
            {
                engine.Tasks.Post(() =>
                {
                    clock.Advance();
                    engine.Constraints.Check();
                    completed++;
                });
            }

            return true;
        });

        (await page.WaitForIdleAsync(Bound)).Should().BeTrue();
        completed.Should().Be(3);
        page.Errors.Should().BeEmpty();
    }

    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    public async Task SeparateQueuedCommandsReceiveSeparateBudgets(bool precedingTask, bool firstCommandOverruns)
    {
        var clock = new BudgetClock();
        await using var session = await PageSession.CreateAsync(options: Options(clock));
        var page = await session.NewPageAsync();
        var sessionId = await session.AttachAsync(await session.TargetForAsync(page));
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        var held = page.RunOnLoopAsync(engine =>
        {
            if (precedingTask)
            {
                engine.Tasks.Post(() =>
                {
                    clock.Advance();
                    engine.Constraints.Check();
                });
            }

            entered.SetResult();
            release.Wait(Bound).Should().BeTrue();
            return true;
        });

        Task<System.Text.Json.JsonElement>[] commands;
        try
        {
            await entered.Task.WaitAsync(Bound);
            commands = Enumerable.Range(0, 3)
                .Select(i => session.SendAsync("Runtime.evaluate",
                    firstCommandOverruns && i == 0
                        ? """{"expression":"spend(); spend(); 42","returnByValue":true}"""
                        : """{"expression":"spend(); 42","returnByValue":true}""", sessionId))
                .ToArray();
        }
        finally
        {
            release.Set();
        }

        await held.WaitAsync(Bound);
        var replies = await Task.WhenAll(commands).WaitAsync(Bound);
        for (var i = 0; i < replies.Length; i++)
        {
            var reply = replies[i];
            if (firstCommandOverruns && i == 0)
            {
                reply.GetProperty("error").GetProperty("message").GetString().Should().Be("Command timed out");
                continue;
            }

            reply.TryGetProperty("error", out var error).Should().BeFalse("command failed: {0}", error);
            reply.GetProperty("result").GetProperty("result").GetProperty("value").GetInt32().Should().Be(42);
        }

        page.Errors.Should().BeEmpty();
    }

    [TestCase("Promise.resolve().then")]
    [TestCase("queueMicrotask")]
    public async Task ATaskAndItsMicrotaskCheckpointShareOneBudget(string enqueue)
    {
        var clock = new BudgetClock();
        await using var browser = new global::Jint.Browser.Browser(Options(clock));
        var page = await browser.NewPageAsync();
        await page.RunOnLoopAsync(engine =>
        {
            engine.Tasks.Post(() => engine.Execute(
                $$"""spend(); {{enqueue}}(() => { spend(); globalThis.escaped = true; });"""));
            // Already queued before the first task creates its reaction. It must not split the checkpoint.
            engine.Tasks.Post(() => engine.SetValue("later", true));
            return true;
        });

        (await page.WaitForIdleAsync(Bound)).Should().BeTrue();
        page.Errors.Should().ContainSingle().Which.Kind.Should().Be(PageErrorKind.BudgetExceeded);
        (await page.EvaluateAsync<bool>("typeof escaped === 'undefined' && later")).Should().BeTrue();
    }

    [TestCase("Promise.resolve().then")]
    [TestCase("queueMicrotask")]
    public async Task RecursiveMicrotasksCannotRenewTheirBudget(string enqueue)
    {
        var clock = new BudgetClock();
        await using var browser = new global::Jint.Browser.Browser(Options(clock));
        var page = await browser.NewPageAsync();
        await page.RunOnLoopAsync(engine =>
        {
            engine.Tasks.Post(() => engine.Execute(
                $$"""
                  globalThis.calls = 0;
                  function spin() { spend(); calls++; {{enqueue}}(spin); }
                  {{enqueue}}(spin);
                  """));
            return true;
        });

        (await page.WaitForIdleAsync(Bound)).Should().BeTrue();
        page.Errors.Should().ContainSingle().Which.Kind.Should().Be(PageErrorKind.BudgetExceeded);
        (await page.EvaluateAsync<int>("calls")).Should().Be(1);
    }

    [Test]
    public async Task AMailboxRequestKeepsItsBudgetThroughItsMicrotasks()
    {
        var clock = new BudgetClock();
        await using var browser = new global::Jint.Browser.Browser(Options(clock));
        var page = await browser.NewPageAsync();

        var act = () => page.EvaluateAsync("spend(); Promise.resolve().then(() => spend());");
        await act.Should().ThrowAsync<TimeoutException>();
        page.Errors.Should().BeEmpty();
        (await page.EvaluateAsync<int>("42")).Should().Be(42);
    }

    [Test]
    public async Task SeparateTasksReceiveSeparateAllocationBudgets()
    {
        await using var browser = new global::Jint.Browser.Browser(new BrowserOptions { MemoryLimit = 4_000_000 });
        var page = await browser.NewPageAsync();
        var completed = 0;
        await page.RunOnLoopAsync(engine =>
        {
            for (var i = 0; i < 3; i++)
            {
                engine.Tasks.Post(() =>
                {
                    GC.KeepAlive(new byte[3_000_000]);
                    engine.Constraints.Check();
                    completed++;
                });
            }

            return true;
        });

        (await page.WaitForIdleAsync(Bound)).Should().BeTrue();
        completed.Should().Be(3);
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task ATaskAndItsMicrotasksShareTheirAllocationBudget()
    {
        await using var browser = new global::Jint.Browser.Browser(new BrowserOptions { MemoryLimit = 4_000_000 });
        var page = await browser.NewPageAsync();
        await page.RunOnLoopAsync(engine =>
        {
            engine.SetValue("allocate", () =>
            {
                GC.KeepAlive(new byte[3_000_000]);
                engine.Constraints.Check();
            });
            engine.Tasks.Post(() => engine.Execute(
                "allocate(); Promise.resolve().then(() => { allocate(); globalThis.escaped = true; });"));
            return true;
        });

        (await page.WaitForIdleAsync(Bound)).Should().BeTrue();
        page.Errors.Should().ContainSingle().Which.Kind.Should().Be(PageErrorKind.BudgetExceeded);
        (await page.EvaluateAsync<bool>("typeof escaped === 'undefined'")).Should().BeTrue();
    }

    [Test]
    public async Task ATasksMicrotasksRunBeforeTheNextQueuedTask()
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.RunOnLoopAsync(engine =>
        {
            engine.Tasks.Post(() => engine.Execute(
                "globalThis.order = ['task']; Promise.resolve().then(() => order.push('microtask'));"));
            engine.Tasks.Post(() => engine.Execute("order.push('next task')"));
            return true;
        });

        (await page.WaitForIdleAsync(Bound)).Should().BeTrue();
        (await page.EvaluateAsync<string>("order.join(',')")).Should().Be("task,microtask,next task");
        page.Errors.Should().BeEmpty();
    }

    [TestCase("setTimeout")]
    [TestCase("requestIdleCallback")]
    public async Task ScheduledCallbacksReceiveSeparateBudgets(string schedule)
    {
        var clock = new BudgetClock();
        await using var browser = new global::Jint.Browser.Browser(Options(clock));
        var page = await browser.NewPageAsync();
        await page.RunOnLoopAsync(engine =>
        {
            engine.Tasks.Post(() => engine.Execute(
                $$"""
                  globalThis.completed = 0;
                  for (let i = 0; i < 3; i++) {
                    {{schedule}}(() => { spend(); completed++; });
                  }
                  """));
            return true;
        });

        (await page.WaitForIdleAsync(Bound)).Should().BeTrue();
        (await page.EvaluateAsync<int>("completed")).Should().Be(3);
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task AnIdleCallbackAndItsMicrotasksAreBounded()
    {
        var clock = new BudgetClock();
        await using var browser = new global::Jint.Browser.Browser(Options(clock));
        var page = await browser.NewPageAsync();
        await page.RunOnLoopAsync(engine =>
        {
            engine.Tasks.Post(() => engine.Execute(
                "requestIdleCallback(() => { spend(); queueMicrotask(() => { spend(); globalThis.escaped = true; }); });"));
            return true;
        });

        (await page.WaitForIdleAsync(Bound)).Should().BeTrue();
        page.Errors.Should().ContainSingle().Which.Kind.Should().Be(PageErrorKind.BudgetExceeded);
        (await page.EvaluateAsync<bool>("typeof escaped === 'undefined'")).Should().BeTrue();
    }

    [TestCase(false)]
    [TestCase(true)]
    public void BothLanesDiscardWorkFromAnEndedEvaluationCycle(bool microtask)
    {
        using var engine = new Engine();
        PageBudget.For(engine, new BrowserOptions());
        var snapshot = engine.Advanced.CaptureGlobalSnapshot();
        var registration = engine.CaptureEventLoopRegistration();
        var kind = microtask ? EventLoopJobKind.Microtask : EventLoopJobKind.Task;
        var calls = new List<string>();
        engine.AddToEventLoop(() => calls.Add("queued before restore"), registration, kind);

        engine.Advanced.RestoreGlobalSnapshot(snapshot);
        engine.AddToEventLoop(() => calls.Add("arrived after restore"), registration, kind);
        engine.AddToEventLoop(() => calls.Add("fresh"), kind);
        engine.Tasks.ProcessTasks();

        calls.Should().Equal("fresh");
    }

    private static BrowserOptions Options(BudgetClock clock)
    {
        return new BrowserOptions().ConfigureEngine(options =>
        {
            options.RemoveConstraints(static constraint => constraint is OperationDeadlineConstraint);
            options.AddConstraint(() => new OperationDeadlineConstraint(clock));
            options.Configure(engine => engine.SetValue("spend", () =>
            {
                clock.Advance();
                engine.Constraints.Check();
            }));
        });
    }

    private sealed class BudgetClock : TimeProvider
    {
        private long _ticks = 1;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => _ticks;

        internal void Advance() => _ticks += TimeSpan.FromSeconds(4).Ticks;
    }
}
