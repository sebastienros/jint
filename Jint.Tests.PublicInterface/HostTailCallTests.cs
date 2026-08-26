using Jint.Runtime;

namespace Jint.Tests.PublicInterface;

public class HostTailCallTests
{
    [Test]
    public void StrictTailRecursionHonorsRecursionLimit()
    {
        var engine = new Engine(options =>
        {
            options.Constraints.MaxRecursionDepth = 8;
            options.LimitExecutionTime(TimeSpan.FromSeconds(1));
        });

        Invoking(() => engine.Evaluate("""
            "use strict";
            function recurse() {
                return recurse();
            }
            recurse();
            """)).Should().ThrowExactly<RecursionDepthOverflowException>();
    }

    [Test]
    public void StrictTailRecursionWithoutLimitDoesNotConsumeCallStack()
    {
        var result = new Engine().Evaluate("""
            "use strict";
            function sum(n, total) {
                return n === 0 ? total : sum(n - 1, total + n);
            }
            sum(10_000, 0);
            """);

        result.Should().Be(50_005_000);
    }

    [Test]
    public void DistinctTailDelegationDoesNotCountAsRecursion()
    {
        var result = new Engine(options => options.Constraints.MaxRecursionDepth = 0).Evaluate("""
            "use strict";
            function first() {
                return second();
            }
            function second() {
                return 42;
            }
            first();
            """);

        result.Should().Be(42);
    }

    /// <summary>
    /// A recursion that leaves the tail-call trampoline through a non-tail route — here a property
    /// getter, but <c>new</c>, a coercion, a Proxy trap and a host callback are the same shape — and
    /// re-enters it, must still be stopped by <c>LimitRecursion</c>. The native stack grows on every
    /// pass because <c>calc</c>'s read of <c>entity.calc</c> is not in tail position, so nothing else
    /// can stop it: without the limit firing the host process dies on a stack overflow no
    /// <c>catch</c> can see.
    /// <para>
    /// The host re-declaring <c>calc</c> on every pass is load-bearing rather than incidental. The
    /// limit counts occurrences of one function <em>definition</em> on the call stack, so a stable
    /// <c>calc</c> would be counted and would stop this on its own; a freshly parsed one is counted
    /// once per level, which leaves the getter as the only function that repeats — and the getter is
    /// exactly the frame the tail call replaces.
    /// </para>
    /// </summary>
    [Test]
    public void RecursionLimitStillFiresWhenATailCallIsOnThePath()
    {
        var engine = new Engine(options =>
        {
            options.Constraints.MaxRecursionDepth = 20;
            options.LimitExecutionTime(TimeSpan.FromSeconds(10));
        });

        engine.SetValue("load", new Action(() => engine.Execute("function calc() { return entity.calc; }")));
        engine.Execute("""
            "use strict";
            var entity = {
                get calc() {
                    load();
                    return calc();
                }
            };
            """);

        Invoking(() => engine.Evaluate("entity.calc"))
            .Should().ThrowExactly<RecursionDepthOverflowException>();
    }

    /// <summary>
    /// The counterpart to the test above: a tail call that <em>completes</em> must give its
    /// displaced caller back to the recursion budget, or a loop calling one bounded tail delegation
    /// would accumulate against the limit and fail on its second iteration.
    /// </summary>
    [Test]
    public void CompletedTailDelegationDoesNotAccumulateAgainstTheLimit()
    {
        var result = new Engine(options => options.Constraints.MaxRecursionDepth = 0).Evaluate("""
            "use strict";
            function leaf() {
                return 1;
            }
            function helper() {
                return leaf();
            }
            var total = 0;
            for (var i = 0; i < 100; i++) {
                total += helper();
            }
            total;
            """);

        result.Should().Be(100);
    }
}
