#nullable enable

using Jint.Runtime;

namespace Jint.Tests.Runtime;

public class HostJobEligibilityTests
{
    [Test]
    public void RetirementFencesBothTaskAndMicrotaskQueues()
    {
        var host = new RetirableHost();
        using var engine = new Engine(options => options.UseHostFactory(_ => host));
        engine.Tasks.ConfigureTaskBudget(new TaskBudget());
        engine.SetValue("retire", new Action(() => host.Active = false));
        engine.Execute("var called = false");
        engine.Tasks.Post(() => engine.Execute("Promise.resolve().then(() => retire()); Promise.resolve().then(() => called = true)"));
        engine.Tasks.Post(() => engine.Execute("called = true"));
        engine.Tasks.ProcessTasks();
        engine.Evaluate("called").AsBoolean().Should().BeFalse();
    }

    private sealed class RetirableHost : Host
    {
        public bool Active { get; set; } = true;
        public override bool CanExecuteJob() => Active;
    }

    private sealed class TaskBudget : IEventLoopTaskBudget
    {
        public bool BeginTask(bool isTask) => true;
        public void EndTask() { }
    }
}
