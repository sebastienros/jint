#nullable enable

using Jint.Runtime;

namespace Jint.Tests.Runtime;

public sealed class TaskStartTests
{
    private sealed class Budget : IEventLoopTaskBudget
    {
        internal int Depth { get; private set; }
        public bool BeginTask(bool isTask)
        {
            if (Depth > 0) return false;
            Depth++;
            return true;
        }
        public void EndTask() => Depth--;
    }

    [Test]
    public void OrdinaryEngineKeepsItsFifoWithoutInvokingTheBudgetedHook()
    {
        using var engine = new Engine();
        var order = new List<int>();
        engine.Tasks.ConfigureTaskStart(() => throw new InvalidOperationException("unused"));
        engine.Tasks.Post(() => order.Add(1));
        engine.Tasks.Post(() => order.Add(2));
        engine.Tasks.ProcessTasks();
        order.Should().Equal(1, 2);
    }

    [Test]
    public void TaskAndItsReactionsRunAfterTheHookInsideTheirBudget()
    {
        using var engine = new Engine();
        var budget = new Budget();
        engine.Tasks.ConfigureTaskBudget(budget);
        var order = new List<string>();
        engine.Tasks.ConfigureTaskStart(() =>
        {
            budget.Depth.Should().Be(1);
            order.Add("start");
        });
        engine.SetValue("record", new Action<string>(value =>
        {
            budget.Depth.Should().Be(1);
            order.Add(value);
        }));
        engine.Tasks.Post(() => engine.Execute("record('task'); Promise.resolve().then(() => record('reaction'))"));
        engine.Tasks.ProcessTask();
        // A nested evaluation leaves its reactions to the active pump's existing checkpoint.
        order.Should().Equal("start", "task", "reaction");
        budget.Depth.Should().Be(0);
    }

    [Test]
    public void AStandaloneCheckpointFailureRetainsItsReactionsForTheNextHealthyEntry()
    {
        using var engine = new Engine();
        var budget = new Budget();
        engine.Tasks.ConfigureTaskBudget(budget);
        var failure = new InvalidOperationException("pending native failure");
        engine.Tasks.ConfigureTaskStart(() => throw failure);
        var reactions = 0;
        engine.SetValue("record", new Action(() => reactions++));
        Caught.Exception(() => engine.Execute("Promise.resolve().then(() => record())"))
            .Should().BeSameAs(failure);
        reactions.Should().Be(0);
        budget.Depth.Should().Be(0);
        engine.Tasks.ConfigureTaskStart(() => budget.Depth.Should().Be(1));
        engine.Tasks.ProcessTask();
        reactions.Should().Be(1);
        budget.Depth.Should().Be(0);
    }

    [Test]
    public void ATaskStartFailureUnwindsItsBudgetAndLeavesLaterTasksAvailable()
    {
        using var engine = new Engine();
        var budget = new Budget();
        engine.Tasks.ConfigureTaskBudget(budget);
        var failure = new InvalidOperationException("pending native failure");
        engine.Tasks.ConfigureTaskStart(() => throw failure);
        var order = new List<int>();
        engine.Tasks.Post(() => order.Add(1));
        engine.Tasks.Post(() => order.Add(2));
        Caught.Exception(engine.Tasks.ProcessTask).Should().BeSameAs(failure);
        order.Should().BeEmpty();
        budget.Depth.Should().Be(0);
        engine.Tasks.ConfigureTaskStart(() => budget.Depth.Should().Be(1));
        engine.Tasks.ProcessTask();
        order.Should().Equal(2);
        budget.Depth.Should().Be(0);
    }

    [Test]
    public void AnOriginalScriptFailureDoesNotRunPendingWorkDuringItsUnwind()
    {
        using var engine = new Engine();
        var budget = new Budget();
        engine.Tasks.ConfigureTaskBudget(budget);
        var hooks = 0;
        engine.Tasks.ConfigureTaskStart(() =>
        {
            hooks++;
            throw new InvalidOperationException("pending native failure");
        });
        var reactions = 0;
        engine.SetValue("record", new Action(() => reactions++));
        Caught.Exception(() => engine.Execute("Promise.resolve().then(() => record()); throw new Error('original script failure')"))
            .Should().BeOfType<JavaScriptException>().Which.Message.Should().Contain("original script failure");
        hooks.Should().Be(0);
        reactions.Should().Be(0);
        budget.Depth.Should().Be(0);
        engine.Tasks.ConfigureTaskStart(() => budget.Depth.Should().Be(1));
        engine.Tasks.ProcessTask();
        reactions.Should().Be(1);
        budget.Depth.Should().Be(0);
    }

#if NET8_0_OR_GREATER
    [Test]
    public void AnIdleCallbackStartsInsideItsBudgetAndRetainsItsReactionCheckpoint()
    {
        using var engine = new Engine(options => options.UseWebApis());
        var budget = new Budget();
        engine.Tasks.ConfigureTaskBudget(budget);
        var order = new List<string>();
        engine.SetValue("record", new Action<string>(value =>
        {
            budget.Depth.Should().Be(1);
            order.Add(value);
        }));
        using (engine.Tasks.DeferTaskDrain())
        {
            engine.Execute("requestIdleCallback(() => { record('idle'); Promise.resolve().then(() => record('reaction')) })");
        }
        engine.Tasks.ConfigureTaskStart(() =>
        {
            budget.Depth.Should().Be(1);
            order.Add("start");
        });
        engine.Tasks.ProcessTask();
        order.Should().Equal("start", "idle", "reaction");
        budget.Depth.Should().Be(0);
    }
#endif
}
