#nullable enable

using Jint.Native;

namespace Jint.Tests.Runtime;

/// <summary>
/// What a successful <c>RegExp.prototype.exec</c> allocates, measured on the engine thread over a warmed loop. Each
/// ceiling sits between the figure before the work it pins and the figure after it, so it fails if the work is undone
/// and is not a timing: a byte count is the same on a loaded machine as on an idle one. If an unrelated change moves
/// what exec allocates by more than the margin, move the ceiling with it.
/// </summary>
// The counter is per thread, so other fixtures cannot add to it, but a forced collection in one of them would still
// disturb a warmed loop; run without other fixtures in flight, as the other allocation pins do.
[NonParallelizable]
public class RegExpExecAllocationTests
{
    private const int Warmup = 500;
    private const int Iterations = 2000;

    private static long BytesPerExec(string regex, string subject)
    {
        using var engine = new Engine();
        var exec = engine.Evaluate("RegExp.prototype.exec");
        var target = engine.Evaluate(regex);
        JsValue[] arguments = [subject];

        for (var i = 0; i < Warmup; i++)
        {
            engine.Call(exec, target, arguments);
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < Iterations; i++)
        {
            engine.Call(exec, target, arguments);
        }

        return (GC.GetAllocatedBytesForCurrentThread() - before) / Iterations;
    }

    // The result array gets index, input and groups by storing one descriptor each, not by going through
    // [[DefineOwnProperty]], which built a transient descriptor to describe the define and a second one to store
    // (about 650 bytes per exec of a one-element result before, about 550 after).
    [Test]
    public void AnExecResultStoresOneDescriptorPerNamedProperty()
    {
        var bytes = BytesPerExec("/b/", "abc");
        bytes.Should().BeLessThan(600);
    }
}
