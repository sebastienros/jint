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

    [Fact]
    public void TheLaterTimeoutIsTheOneEnforced()
    {
        // a widened timeout must actually take effect, which it cannot while the earlier, stricter
        // constraint is still registered alongside it
        var engine = new Engine(o => o
            .TimeoutInterval(TimeSpan.FromMilliseconds(1))
            .TimeoutInterval(TimeSpan.FromSeconds(30)));

        engine.Evaluate(LoopScript).AsNumber().Should().BeGreaterThan(0);
    }

    [Fact]
    public void ARemovedTimeoutNoLongerApplies()
    {
        // TimeSpan.MaxValue is the "effectively unlimited" spelling, and it removes the earlier registration
        // rather than widening it
        var engine = new Engine(o => o
            .TimeoutInterval(TimeSpan.FromMilliseconds(1))
            .TimeoutInterval(TimeSpan.MaxValue));

        engine.Evaluate(LoopScript).AsNumber().Should().BeGreaterThan(0);
    }

    [Fact]
    public void TheLaterStatementLimitIsTheOneEnforced()
    {
        var engine = new Engine(o => o.MaxStatements(5).MaxStatements(1_000_000));

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
        var saturated = new Engine(o => o.MaxStatements(int.MaxValue));
        var unlimited = new Engine();

        saturated.Constraints.Find<MaxStatementsConstraint>().Should().BeNull();
        unlimited.Constraints.Find<MaxStatementsConstraint>().Should().BeNull();

        const string Script = "var n = 0; for (var i = 0; i < 50000; i++) { n += i; } n";
        saturated.Evaluate(Script).AsNumber().Should().Be(unlimited.Evaluate(Script).AsNumber());
    }

    [Fact]
    public void ASaturatedStatementLimitAlsoRemovesAnEarlierRealOne()
    {
        var engine = new Engine(o => o.MaxStatements(5).MaxStatements(int.MaxValue));

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

        new Engine(o => o.CancellationToken(default)).Constraints.Find<CancellationConstraint>().Should().BeNull();
        new Engine(o => o.CancellationToken(source.Token)).Constraints.Find<CancellationConstraint>().Should().NotBeNull();
        new Engine(o => o.CancellationToken(source.Token).CancellationToken(default)).Constraints.Find<CancellationConstraint>().Should().BeNull();
    }

    [Fact]
    public void ASaturatedRecursionDepthStillEnablesDepthTracking()
    {
        // LimitRecursion is deliberately documented as the exception: any non-negative depth turns
        // the check on, so a saturated value costs enforcement without ever failing.
        new Options().LimitRecursion(int.MaxValue).Constraints.MaxRecursionDepth.Should().Be(int.MaxValue);
        new Options().Constraints.MaxRecursionDepth.Should().Be(-1);

        // and it really does not fail, unlike a small depth
        new Engine(o => o.LimitRecursion(int.MaxValue))
            .Evaluate("function f(n) { return n === 0 ? 0 : f(n - 1); } f(100)").AsNumber().Should().Be(0);

        Invoking(() => new Engine(o => o.LimitRecursion(10)).Evaluate("function f(n) { return n === 0 ? 0 : f(n - 1); } f(100)"))
            .Should().ThrowExactly<RecursionDepthOverflowException>();
    }
}
