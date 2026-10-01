#nullable enable

using Jint.Runtime;

namespace Jint.Tests.Runtime;

public class TaskDrainDeferralTests
{
    private sealed class RecordingBudget : IEventLoopTaskBudget
    {
        internal int Depth { get; private set; }
        internal List<bool> Entries { get; } = [];

        public bool BeginTask(bool isTask)
        {
            Entries.Add(isTask);
            if (!isTask && Depth > 0)
            {
                return false;
            }

            Depth++;
            return true;
        }

        public void EndTask() => Depth--;
    }

    private static Engine CreateEngine(RecordingBudget budget, Action<Options>? configure = null)
    {
        var engine = new Engine(options => configure?.Invoke(options));
        engine.Tasks.ConfigureTaskBudget(budget);
        return engine;
    }

    [Test]
    public void ScopedEvaluationKeepsTheFullCheckpointAndEnclosingBudget()
    {
        var budget = new RecordingBudget();
        using var engine = CreateEngine(budget);
        var order = new List<string>();
        engine.SetValue("record", new Action<string>(value =>
        {
            budget.Depth.Should().Be(1);
            order.Add(value);
        }));
        engine.Tasks.PromiseRejectionTracker += (_, _) =>
        {
            budget.Depth.Should().Be(1);
            order.Add("rejection");
            engine.Execute("Promise.resolve().then(() => record('report reaction'));");
            engine.Tasks.Post(() => order.Add("report task"));
        };
        engine.Tasks.Post(() => order.Add("task"));

        budget.BeginTask(isTask: true);
        try
        {
            using var deferred = engine.Tasks.DeferTaskDrain();
            engine.Execute("""
                Promise.resolve().then(() => record('reaction'));
                Promise.reject('unhandled');
                """);
            order.Should().Equal("reaction", "rejection", "report reaction");
            budget.Depth.Should().Be(1);
            budget.Entries.Should().Equal(true, false);
        }
        finally
        {
            budget.EndTask();
        }

        engine.Tasks.ProcessTasks();
        order.Should().Equal("reaction", "rejection", "report reaction", "task", "report task");
        budget.Depth.Should().Be(0);
    }

    [Test]
    public void NestedScopesKeepTasksDeferredUntilTheOutermostScopeEnds()
    {
        using var engine = CreateEngine(new RecordingBudget());
        var ran = false;
        engine.Tasks.Post(() => ran = true);
        using (engine.Tasks.DeferTaskDrain())
        {
            using (engine.Tasks.DeferTaskDrain())
            {
                engine.Execute("Promise.resolve().then(() => {});");
                ran.Should().BeFalse();
            }

            engine.Execute("0;");
            ran.Should().BeFalse();
        }

        // Releasing the scope does not itself drain; the next ordinary entry does.
        ran.Should().BeFalse();
        engine.Execute("0;");
        ran.Should().BeTrue();
    }

    [Test]
    public void SynchronousReentryKeepsTheOuterParserTaskProtected()
    {
        using var engine = CreateEngine(new RecordingBudget());
        var order = new List<string>();
        engine.SetValue("record", new Action<string>(order.Add));
        engine.SetValue("reenter", new Action(() =>
        {
            using var nested = engine.Tasks.DeferTaskDrain();
            engine.Execute("Promise.resolve().then(() => record('nested reaction'));");
            order.Should().Equal("nested reaction");
        }));
        engine.Tasks.Post(() => order.Add("task"));

        using (engine.Tasks.DeferTaskDrain())
        {
            engine.Execute("reenter(); record('outer script');");
            order.Should().Equal("nested reaction", "outer script");
            engine.Execute("0;");
            order.Should().Equal("nested reaction", "outer script");
        }

        engine.Tasks.ProcessTask();
        order.Should().Equal("nested reaction", "outer script", "task");
    }

    [Test]
    public void AScriptThrowRestoresTheOuterScopeWithoutDrainingTasks()
    {
        using var engine = CreateEngine(new RecordingBudget());
        var taskRan = false;
        engine.Tasks.Post(() => taskRan = true);

        var exception = Caught.Exception(() =>
        {
            using var deferred = engine.Tasks.DeferTaskDrain();
            engine.Execute("throw new Error('script failed');");
        });

        exception.Should().BeOfType<JavaScriptException>();
        taskRan.Should().BeFalse();
        engine.Execute("0;");
        taskRan.Should().BeTrue();
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ExplicitPumpsBypassDeferralForTheirCallOnly(bool singleTask)
    {
        using var engine = CreateEngine(new RecordingBudget());
        var order = new List<string>();
        engine.Tasks.Post(() =>
        {
            order.Add("first");
            using var nested = engine.Tasks.DeferTaskDrain();
            engine.Execute("Promise.resolve().then(() => {});");
            order.Should().Equal("first");
        });
        engine.Tasks.Post(() => order.Add("second"));

        using (engine.Tasks.DeferTaskDrain())
        {
            if (singleTask)
            {
                engine.Tasks.ProcessTask();
                order.Should().Equal("first");
            }
            else
            {
                engine.Tasks.ProcessTasks();
                order.Should().Equal("first", "second");
            }

            engine.Tasks.Post(() => order.Add("later"));
            engine.Execute("0;");
            order.Should().NotContain("later");
        }

        engine.Tasks.ProcessTasks();
        order.Should().Equal("first", "second", "later");
    }

    [Test]
    public void ManualPromiseInlineSettlementHonorsDeferral()
    {
        using var engine = CreateEngine(new RecordingBudget());
        var (promise, resolve, _) = engine.Tasks.RegisterPromise();
        var settled = false;
        var taskRan = false;
        engine.SetValue("promise", promise);
        engine.SetValue("settled", new Action(() => settled = true));
        engine.Execute("promise.then(settled);");
        engine.Tasks.Post(() => taskRan = true);

        using (engine.Tasks.DeferTaskDrain())
        {
            resolve(42);
            settled.Should().BeFalse();
            taskRan.Should().BeFalse();
            engine.Tasks.ProcessTask();
            taskRan.Should().BeTrue();
            settled.Should().BeFalse();
            engine.Tasks.ProcessTask();
            settled.Should().BeTrue();
        }
    }

    [Test]
    public void AnOrdinaryEngineIgnoresTheInternalScope()
    {
        using var engine = new Engine();
        var taskRan = false;
        var settled = false;
        var (promise, resolve, _) = engine.Tasks.RegisterPromise();
        engine.SetValue("promise", promise);
        engine.SetValue("settled", new Action(() => settled = true));
        engine.Execute("promise.then(settled);");

        using var deferred = engine.Tasks.DeferTaskDrain();
        engine.Tasks.Post(() => taskRan = true);
        engine.Execute("0;");
        taskRan.Should().BeTrue();
        resolve(42);
        settled.Should().BeTrue();
    }

#if NET8_0_OR_GREATER
    private sealed class FrozenClock : TimeProvider
    {
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => 0;
        public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch;
    }

    [Test]
    public void ScopedEvaluationDefersTimerPromotionAndIdleCallbacks()
    {
        var budget = new RecordingBudget();
        using var engine = CreateEngine(budget, options => options.UseWebApis(webApi =>
            webApi.Timers.TimeProvider = new FrozenClock()));
        var order = new List<string>();
        engine.SetValue("record", new Action<string>(order.Add));

        using (engine.Tasks.DeferTaskDrain())
        {
            engine.Execute("""
                setTimeout(() => record('timer'), 0);
                requestIdleCallback(() => record('idle'));
                Promise.resolve().then(() => record('reaction'));
                """);
            order.Should().Equal("reaction");
            budget.Entries.Should().Equal(false);

            engine.Tasks.ProcessTask();
            order.Should().Equal("reaction", "timer");
            engine.Execute("0;");
            order.Should().Equal("reaction", "timer");
            engine.Tasks.ProcessTask();
            order.Should().Equal("reaction", "timer", "idle");
        }

        budget.Depth.Should().Be(0);
    }
#endif

    [TestCase(false)]
    [TestCase(true)]
    public void FailedCheckpointRestoresDeferralAndBudget(bool cancel)
    {
        using var cancellation = new CancellationTokenSource();
        var budget = new RecordingBudget();
        using var engine = CreateEngine(budget, options => options.ObserveCancellation(cancellation.Token));
        // A raw microtask lets an ordinary host failure escape the checkpoint rather than becoming
        // a rejected promise. Cancellation must escape either kind of job as a fatal constraint.
        engine.AddToEventLoop(() =>
        {
            if (cancel)
            {
                cancellation.Cancel();
                engine.Execute("while (true) {}");
            }

            throw new InvalidOperationException("checkpoint failed");
        }, EventLoopJobKind.Microtask);
        var taskRan = false;
        engine.Tasks.Post(() => taskRan = true);

        var exception = Caught.Exception(() =>
        {
            using var deferred = engine.Tasks.DeferTaskDrain();
            engine.Execute("0;");
        });

        if (cancel)
        {
            exception.Should().BeOfType<ExecutionCanceledException>();
        }
        else
        {
            exception.Should().BeOfType<InvalidOperationException>();
        }

        budget.Depth.Should().Be(0);
        taskRan.Should().BeFalse();
        engine.RunAvailableContinuations();
        taskRan.Should().BeTrue();
        budget.Depth.Should().Be(0);
    }
}
