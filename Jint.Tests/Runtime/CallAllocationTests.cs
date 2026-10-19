using Jint.Native;
using Jint.Runtime;

namespace Jint.Tests.Runtime;

// Forced collections affect the whole process, so run without other fixtures in flight.
[NonParallelizable]
public class CallAllocationTests
{
    [TestCase(0)]
    [TestCase(3)]
    public void WarmEmptyCallDoesNotAllocate(int arity)
    {
        using var engine = new Engine();
        var callable = engine.Evaluate("(function (a, b, c) {})");
        var arguments = new JsValue[arity];
        for (var i = 0; i < arguments.Length; i++)
        {
            arguments[i] = JsValue.Undefined;
        }
        Action empty = static () => { };
        Action call = () => engine.Call(callable, JsValue.Undefined, arguments);
        Action allocate = static () => GC.KeepAlive(new byte[1024]);

        Measure(empty);
        Measure(call);
        Measure(allocate);
        var baseline = Measure(empty);
        var calls = Measure(call);
        var positive = Measure(allocate);

        baseline.Should().Be(0);
        calls.Should().Be(0);
        positive.Should().BeGreaterThanOrEqualTo(1024 * 1000);
    }

    private static long Measure(Action action)
    {
        Collect();
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; i++)
        {
            action();
        }
        Collect();
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    private static void Collect()
    {
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true);
    }

    [Test]
    public void NestedCallPreservesOuterBudgetAndRecoversAfterThrow()
    {
        using var engine = new Engine(options => options.LimitStatements(100));
        var inner = engine.Evaluate("(function () { return 7; })");
        var calls = 0;
        engine.SetValue("reenter", new Action(() =>
        {
            if (++calls > 1000)
            {
                throw new InvalidOperationException("Nested call reset the outer budget");
            }
            engine.Call(inner).Should().Be(7);
        }));
        var outer = engine.Evaluate("(function () { while (true) reenter(); })");

        Invoking(() => engine.Call(outer)).Should().ThrowExactly<StatementsCountOverflowException>();
        calls.Should().BeLessThan(100);
        engine.Call(inner).Should().Be(7);
        Invoking(() => engine.Call(JsValue.Null)).Should().Throw<ArgumentException>();
        engine.Call(inner).Should().Be(7);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void CallPreservesReceiverAndArguments(bool strict)
    {
        using var engine = new Engine(options => options.Strict = strict);
        var callable = engine.Evaluate("(function (a, b) { return this.base + a + b; })");
        var receiver = engine.Evaluate("({ base: 10 })");
        JsValue[] arguments = [2, 3];

        engine.Call(callable, receiver, arguments).Should().Be(15);
    }
}
