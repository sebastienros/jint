#nullable enable

using System.Diagnostics;
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

        // A wedge ceiling — nothing here asserts how long the spawned agent took, only what it reported — and
        // its exhaustion is stated before the value is read. The five seconds it was ran straight into
        // AsString() on a JS null, so a runner too slow to start a thread inside the budget reported
        // "Expected string but got Null" from inside JsValue: a message about neither the agent nor the
        // budget, which is the diagnosis cost #3297 is about.
        var elapsed = Stopwatch.StartNew();
        JsValue report;
        do
        {
            report = engine.Evaluate("$262.agent.getReport()");
            if (report.IsNull())
            {
                Thread.Sleep(10);
            }
        } while (report.IsNull() && elapsed.Elapsed < TestBudgets.WedgeCeiling);

        report.IsNull().Should().BeFalse($"the spawned agent must report within {TestBudgets.WedgeCeiling}; {elapsed.Elapsed} elapsed");
        report.AsString().Should().Be("timed-out");
    }
}
