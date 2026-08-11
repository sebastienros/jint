using Jint.Runtime;

namespace Jint.Tests.PublicInterface;

public class HostTailCallTests
{
    [Fact]
    public void StrictTailRecursionHonorsRecursionLimit()
    {
        var engine = new Engine(options => options
            .LimitRecursion(8)
            .TimeoutInterval(TimeSpan.FromSeconds(1)));

        Invoking(() => engine.Evaluate("""
            "use strict";
            function recurse() {
                return recurse();
            }
            recurse();
            """)).Should().ThrowExactly<RecursionDepthOverflowException>();
    }

    [Fact]
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

    [Fact]
    public void DistinctTailDelegationDoesNotCountAsRecursion()
    {
        var result = new Engine(options => options.LimitRecursion(0)).Evaluate("""
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
}
