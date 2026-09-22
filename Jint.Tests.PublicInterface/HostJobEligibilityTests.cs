#nullable enable

using Jint.Native;
using Jint.Runtime;

namespace Jint.Tests.PublicInterface;

public class HostJobEligibilityTests
{
    private sealed class RetirableHost : Host
    {
        public bool Active { get; set; } = true;
        public override bool CanExecuteJob() => Active;
    }

    [Test]
    public void HostJobGuardFencesQueuedAndNewJobsDuringDrain()
    {
        var host = new RetirableHost();
        var engine = new Engine(options => options.UseHostFactory(_ => host));
        engine.SetValue("retire", new Action(() => host.Active = false));
        engine.Execute("""
            var calls = [];
            Promise.resolve().then(() => { retire(); calls.push('current'); Promise.resolve().then(() => calls.push('new')); });
            Promise.resolve().then(() => calls.push('queued'));
            """);
        engine.Tasks.ProcessTasks();
        engine.Evaluate("calls.join(',')").AsString().Should().Be("current");
        engine.Execute("Promise.resolve().then(() => calls.push('later'))");
        engine.Evaluate("calls.join(',')").AsString().Should().Be("current");
    }

    [Test]
    public void HostJobGuardFencesLaterManualPromiseSettlement()
    {
        var host = new RetirableHost();
        var engine = new Engine(options => options.UseHostFactory(_ => host));
        Action<JsValue> resolve = null!;
        engine.SetValue("pending", new Func<JsValue>(() => {
            var registration = engine.Tasks.RegisterPromise();
            resolve = registration.Resolve;
            return registration.Promise;
        }));
        engine.Execute("var called = false; pending().then(() => called = true)");
        host.Active = false;
        resolve(42);
        engine.Tasks.ProcessTasks();
        engine.Evaluate("called").AsBoolean().Should().BeFalse();
    }

    [Test]
    public void RetiredHostDiscardsPostedWorkButDefaultHostExecutesIt()
    {
        var host = new RetirableHost { Active = false };
        using var retired = new Engine(options => options.UseHostFactory(_ => host));
        using var active = new Engine();
        var calls = 0;
        retired.Tasks.Post(() => calls += 10);
        active.Tasks.Post(() => calls++);
        retired.Tasks.ProcessTasks();
        active.Tasks.ProcessTasks();
        calls.Should().Be(1);
    }

    [Test]
    public void RetirementFencesAsyncFunctionContinuation()
    {
        var host = new RetirableHost();
        using var engine = new Engine(options => options.UseHostFactory(_ => host));
        engine.SetValue("retire", new Action(() => host.Active = false));
        engine.Execute("""
            var resumed = false;
            Promise.resolve().then(() => retire());
            (async () => { await Promise.resolve(); resumed = true; })();
            """);
        engine.Tasks.ProcessTasks();
        engine.Evaluate("resumed").AsBoolean().Should().BeFalse();
    }
}
