#nullable enable

using Jint.Native;
using Jint.Runtime;

namespace Jint.Tests.Runtime;

public class Test262AgentManagerTests
{
    [Fact]
    public void SpawnedAgentsCanUseSynchronousWait()
    {
        using var manager = new Test262AgentManager();
        var engine = new Engine();
        var container = engine.Realm.Intrinsics.Object.Construct(Arguments.Empty);
        manager.InstallAgent(engine, container);
        engine.SetValue("$262", container);

        engine.Execute("""
            $262.agent.start(`
                var i32a = new Int32Array(new SharedArrayBuffer(8));
                $262.agent.report(Atomics.wait(i32a, 0, 0, 0));
                $262.agent.leaving();
            `);
            """);

        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        JsValue report;
        do
        {
            report = engine.Evaluate("$262.agent.getReport()");
            if (report.IsNull())
            {
                Thread.Sleep(10);
            }
        } while (report.IsNull() && DateTime.UtcNow < deadline);

        report.AsString().Should().Be("timed-out");
    }
}
