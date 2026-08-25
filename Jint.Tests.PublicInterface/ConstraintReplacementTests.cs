#nullable enable

using Jint.Constraints;
using Jint.Runtime;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// Pins what an embedder can observe about the registration semantics of the built-in constraint helpers, as
/// distinct from what the constraints do once registered: calling a helper twice replaces rather than
/// accumulates, and a saturated sentinel registers nothing at all instead of standing for a very large limit.
/// These live in the public-interface suite on purpose: the project references Jint without any internals
/// access, so every configuration knob and every observation below is proven reachable by a third-party host.
/// <para>
/// The registration <em>counts</em> behind these behaviours — including the ones for the internal time
/// constraint, which has no public type to look up — are asserted from inside the assembly by
/// <c>Jint.Tests.Runtime.ConstraintRegistrationTests</c>.
/// </para>
/// </summary>
public class ConstraintReplacementTests
{
    private const string LoopScript = "var n = 0; for (var i = 0; i < 200000; i++) { n += i; } n";

    /// <summary>
    /// The widened interval in the row below. It is not a duration the test is about: the discriminator is
    /// the one-millisecond interval that must no longer be registered, which two hundred thousand iterations
    /// pass by four orders of magnitude on any machine. What this number decides is only whether a healthy
    /// run finishes inside it, so it is a wedge ceiling — thirty seconds was one on an idle box and not on a
    /// runner that has been seen stalling a two-hundred-millisecond wait for a minute (#3358).
    /// </summary>
    private static readonly TimeSpan WidenedInterval = TimeSpan.FromMinutes(10);

    [Fact]
    public void TheLaterTimeoutIsTheOneEnforced()
    {
        // a widened timeout must actually take effect, which it cannot while the earlier, stricter
        // constraint is still registered alongside it
        var engine = new Engine(o => o
            .LimitExecutionTime(TimeSpan.FromMilliseconds(1))
            .LimitExecutionTime(WidenedInterval));

        engine.Evaluate(LoopScript).AsNumber().Should().BeGreaterThan(0);
    }

    [Fact]
    public void ARemovedTimeoutNoLongerApplies()
    {
        // TimeSpan.MaxValue is the "effectively unlimited" spelling, and it removes the earlier registration
        // rather than widening it
        var engine = new Engine(o => o
            .LimitExecutionTime(TimeSpan.FromMilliseconds(1))
            .LimitExecutionTime(TimeSpan.MaxValue));

        engine.Evaluate(LoopScript).AsNumber().Should().BeGreaterThan(0);
    }

    [Fact]
    public void TheLaterStatementLimitIsTheOneEnforced()
    {
        var engine = new Engine(o => o.LimitStatements(5).LimitStatements(1_000_000));

        engine.Constraints.Find<MaxStatementsConstraint>()!.MaxStatements.Should().Be(1_000_000);
        engine.Evaluate(LoopScript).AsNumber().Should().BeGreaterThan(0);
    }

    [Fact]
    public void ASaturatedStatementLimitLeavesTheEngineIndistinguishableFromNoLimit()
    {
        // The documented consequence of the sentinel: this is not "a very large limit", it is the
        // unconstrained engine, tight-loop lane and all. Registering the constraint instead would
        // buy nothing, because the constraint counts statements in an int and so can never reach
        // int.MaxValue.
        var saturated = new Engine(o => o.LimitStatements(int.MaxValue));
        var unlimited = new Engine();

        saturated.Constraints.Find<MaxStatementsConstraint>().Should().BeNull();
        unlimited.Constraints.Find<MaxStatementsConstraint>().Should().BeNull();

        const string Script = "var n = 0; for (var i = 0; i < 50000; i++) { n += i; } n";
        saturated.Evaluate(Script).AsNumber().Should().Be(unlimited.Evaluate(Script).AsNumber());
    }

    [Fact]
    public void ASaturatedStatementLimitAlsoRemovesAnEarlierRealOne()
    {
        var engine = new Engine(o => o.LimitStatements(5).LimitStatements(int.MaxValue));

        engine.Constraints.Find<MaxStatementsConstraint>().Should().BeNull();
        engine.Evaluate(LoopScript).AsNumber().Should().BeGreaterThan(0);
    }

    [Fact]
    public void ASaturatedMemoryLimitLeavesNoConstraintBehind()
    {
        new Engine(o => o.LimitMemory(long.MaxValue)).Constraints.Find<MemoryLimitConstraint>().Should().BeNull();
        new Engine(o => o.LimitMemory(0)).Constraints.Find<MemoryLimitConstraint>().Should().BeNull();
        new Engine(o => o.LimitMemory(1024).LimitMemory(long.MaxValue)).Constraints.Find<MemoryLimitConstraint>().Should().BeNull();

        new Engine(o => o.LimitMemory(4_000_000)).Constraints.Find<MemoryLimitConstraint>().Should().NotBeNull();
    }

    [Fact]
    public void ADefaultCancellationTokenLeavesNoConstraintBehind()
    {
        using var source = new CancellationTokenSource();

        new Engine(o => o.ObserveCancellation(default)).Constraints.Find<CancellationConstraint>().Should().BeNull();
        new Engine(o => o.ObserveCancellation(source.Token)).Constraints.Find<CancellationConstraint>().Should().NotBeNull();
        new Engine(o => o.ObserveCancellation(source.Token).ObserveCancellation(default)).Constraints.Find<CancellationConstraint>().Should().BeNull();
    }

    [Fact]
    public void ASaturatedRecursionDepthIsNoLimitAtAll()
    {
        // A limit that cannot be reached is not a limit: int.MaxValue produces the same engine -1 does,
        // rather than arming the depth tracking that feeds a check which could never fail. The configured
        // value still reads back as it was assigned, so the security report can still name the mistake.
        var saturated = new Options();
        saturated.Constraints.MaxRecursionDepth = int.MaxValue;
        saturated.Constraints.MaxRecursionDepth.Should().Be(int.MaxValue);
        new Options().Constraints.MaxRecursionDepth.Should().Be(-1);

        // and it really does not fail, unlike a small depth
        new Engine(o => o.Constraints.MaxRecursionDepth = int.MaxValue)
            .Evaluate("function f(n) { return n === 0 ? 0 : f(n - 1); } f(100)").AsNumber().Should().Be(0);

        Invoking(() => new Engine(o => o.Constraints.MaxRecursionDepth = 10).Evaluate("function f(n) { return n === 0 ? 0 : f(n - 1); } f(100)"))
            .Should().ThrowExactly<RecursionDepthOverflowException>();
    }
}
