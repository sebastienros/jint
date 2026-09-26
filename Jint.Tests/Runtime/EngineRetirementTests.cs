#nullable enable

using Jint.Runtime;

namespace Jint.Tests.Runtime;

public class EngineRetirementTests
{
    [Test]
    public void RetirementFencesBothTaskAndMicrotaskQueues()
    {
        using var engine = new Engine();
        engine.Tasks.ConfigureTaskBudget(new TaskBudget());
        var calls = 0;
        engine.SetValue("retire", new Action(() => engine.Advanced.Retire()));
        engine.SetValue("record", new Action(() => calls++));
        engine.Tasks.Post(() => engine.Execute("Promise.resolve().then(() => retire()); Promise.resolve().then(() => record())"));
        engine.Tasks.Post(() => engine.Execute("record()"));
        engine.Tasks.ProcessTasks();
        calls.Should().Be(0);
    }

    private sealed class TaskBudget : IEventLoopTaskBudget
    {
        public bool BeginTask(bool isTask) => true;
        public void EndTask() { }
    }
}
