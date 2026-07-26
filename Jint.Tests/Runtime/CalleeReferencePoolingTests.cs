using System;

namespace Jint.Tests.Runtime;

/// <summary>
/// A call whose callee is not a plain <c>obj.method</c> shape rents a <see cref="Jint.Runtime.Reference"/>
/// for the callee — it carries the this-binding and the name used in the "is not a function" messages —
/// and hands it back once the call is dispatched. Every way of leaving the call has to do that, or the
/// pool drains and each further call of that shape allocates a fresh instance.
/// </summary>
public class CalleeReferencePoolingTests
{
    private const int Iterations = 500;

    /// <summary>
    /// The pool holds a small, fixed number of instances, so a workload that rents and returns in
    /// balance stops creating them almost immediately. The exact figure is not the point — the point
    /// is that it must not scale with the number of calls made.
    /// </summary>
    private const int MaxCreations = 32;

    [Fact]
    public void RepeatedHostDelegateCallsReuseTheCalleeReference()
    {
        // Host delegates are the callees the fast-call lane serves, and calling one through a global
        // name is the shape that rents a callee reference — so this is the combination that leaves the
        // lane's early return responsible for the pooled instance.
        var engine = new Engine();
        engine.SetValue("addOne", new Func<int, int>(x => x + 1));

        engine.Execute($"var s = 0; for (var i = 0; i < {Iterations}; i++) {{ s = addOne(i); }}");

        engine.GetValue("s").AsNumber().Should().Be(Iterations - 1 + 1);
        engine._referencePool.CreatedCount.Should().BeLessThanOrEqualTo(MaxCreations);
    }

    [Fact]
    public void RepeatedShortCircuitedOptionalCallsReuseTheCalleeReference()
    {
        // `missing?.()` resolves the callee to undefined and short-circuits, which is another exit that
        // leaves the rented callee reference behind.
        var engine = new Engine();

        engine.Execute($"var o = {{}}; for (var i = 0; i < {Iterations}; i++) {{ var x = o.missing?.(); }}");

        engine._referencePool.CreatedCount.Should().BeLessThanOrEqualTo(MaxCreations);
    }
}
