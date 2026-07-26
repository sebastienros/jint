#nullable enable
using Jint.Constraints;
using Jint.Runtime;

namespace Jint.Tests.Runtime;

/// <summary>
/// Pins the registration semantics of the built-in constraint helpers, as distinct from what the
/// constraints do once registered: which values register a constraint at all, and that calling a
/// helper twice replaces rather than accumulates.
/// </summary>
public class ConstraintRegistrationTests
{
    /// <summary>
    /// The constraints an engine built from these options actually ends up with. Asking the engine
    /// rather than the options is what makes this independent of <em>how</em> a helper registered its
    /// constraint: the built-in helpers register a factory so that each engine gets its own instance,
    /// while a directly registered instance is shared, and both arrive here the same way.
    /// </summary>
    private static IReadOnlyList<Constraint> Register(Action<Options> configure)
    {
        var options = new Options();
        configure(options);
        return new Engine(options)._constraints;
    }

    private static int CountOf<T>(Action<Options> configure) where T : Constraint
    {
        return Register(configure).Count(c => c is T);
    }

    [Theory]
    [InlineData(int.MinValue)]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(int.MaxValue)]
    public void MaxStatementsRegistersNothingForValuesThatCannotExpressALimit(int maxStatements)
    {
        CountOf<MaxStatementsConstraint>(o => o.MaxStatements(maxStatements)).Should().Be(0);
    }

    [Fact]
    public void MaxStatementsWithNoArgumentRegistersNothing()
    {
        // the parameter defaults to 0, which means unlimited - not "no statements allowed"
        CountOf<MaxStatementsConstraint>(o => o.MaxStatements()).Should().Be(0);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(1000)]
    [InlineData(int.MaxValue - 1)]
    public void MaxStatementsRegistersOneForARealLimit(int maxStatements)
    {
        CountOf<MaxStatementsConstraint>(o => o.MaxStatements(maxStatements)).Should().Be(1);
    }

    [Fact]
    public void MaxStatementsReplacesAndCanBeCleared()
    {
        Register(o => o.MaxStatements(10).MaxStatements(20))
            .OfType<MaxStatementsConstraint>().Should().ContainSingle().Which.MaxStatements.Should().Be(20);

        CountOf<MaxStatementsConstraint>(o => o.MaxStatements(10).MaxStatements(int.MaxValue)).Should().Be(0);
    }

    [Theory]
    [InlineData(long.MinValue)]
    [InlineData(-1L)]
    [InlineData(0L)]
    [InlineData(long.MaxValue)]
    public void LimitMemoryRegistersNothingForValuesThatCannotExpressALimit(long memoryLimit)
    {
        CountOf<MemoryLimitConstraint>(o => o.LimitMemory(memoryLimit)).Should().Be(0);
    }

    [Fact]
    public void LimitMemoryReplacesAndCanBeCleared()
    {
        CountOf<MemoryLimitConstraint>(o => o.LimitMemory(1024).LimitMemory(2048)).Should().Be(1);
        CountOf<MemoryLimitConstraint>(o => o.LimitMemory(1024).LimitMemory(long.MaxValue)).Should().Be(0);
    }

    private static int TimeConstraintCount(Action<Options> configure)
    {
        // TimeConstraint is internal, so it is counted by exclusion rather than by type
        return Register(configure).Count(c => c is not MaxStatementsConstraint and not MemoryLimitConstraint and not CancellationConstraint);
    }

    [Fact]
    public void TimeoutIntervalRegistersNothingForIntervalsThatCannotExpressALimit()
    {
        TimeConstraintCount(o => o.TimeoutInterval(TimeSpan.Zero)).Should().Be(0);
        TimeConstraintCount(o => o.TimeoutInterval(TimeSpan.FromSeconds(-1))).Should().Be(0);
        TimeConstraintCount(o => o.TimeoutInterval(TimeSpan.MaxValue)).Should().Be(0);
    }

    [Fact]
    public void TimeoutIntervalReplacesInsteadOfAccumulating()
    {
        // the second call must win outright; two live time constraints would silently leave the
        // stricter of the two in charge
        TimeConstraintCount(o => o.TimeoutInterval(TimeSpan.FromSeconds(1))).Should().Be(1);
        TimeConstraintCount(o => o.TimeoutInterval(TimeSpan.FromSeconds(1)).TimeoutInterval(TimeSpan.FromSeconds(5))).Should().Be(1);
        TimeConstraintCount(o => o.TimeoutInterval(TimeSpan.FromSeconds(1)).TimeoutInterval(TimeSpan.MaxValue)).Should().Be(0);
    }

    [Fact]
    public void TheLaterTimeoutIsTheOneEnforced()
    {
        // a widened timeout must actually take effect, which it cannot while the earlier, stricter
        // constraint is still registered alongside it
        var engine = new Engine(o => o
            .TimeoutInterval(TimeSpan.FromMilliseconds(1))
            .TimeoutInterval(TimeSpan.FromSeconds(30)));

        engine.Evaluate("var n = 0; for (var i = 0; i < 200000; i++) { n += i; } n").AsNumber().Should().BeGreaterThan(0);
    }

    [Fact]
    public void ARemovedTimeoutNoLongerApplies()
    {
        var engine = new Engine(o => o
            .TimeoutInterval(TimeSpan.FromMilliseconds(1))
            .TimeoutInterval(TimeSpan.MaxValue));

        engine.Evaluate("var n = 0; for (var i = 0; i < 200000; i++) { n += i; } n").AsNumber().Should().BeGreaterThan(0);
    }

    [Fact]
    public void CancellationTokenRegistersNothingForTheDefaultToken()
    {
        CountOf<CancellationConstraint>(o => o.CancellationToken(default)).Should().Be(0);
    }

    [Fact]
    public void CancellationTokenReplacesAndCanBeCleared()
    {
        using var source = new System.Threading.CancellationTokenSource();
        using var other = new System.Threading.CancellationTokenSource();

        CountOf<CancellationConstraint>(o => o.CancellationToken(source.Token)).Should().Be(1);
        CountOf<CancellationConstraint>(o => o.CancellationToken(source.Token).CancellationToken(other.Token)).Should().Be(1);
        CountOf<CancellationConstraint>(o => o.CancellationToken(source.Token).CancellationToken(default)).Should().Be(0);
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
    public void ASaturatedRecursionDepthStillEnablesDepthTracking()
    {
        // LimitRecursion is deliberately documented as the exception: any non-negative depth turns
        // the check on, so a saturated value costs enforcement without ever failing.
        new Engine(o => o.LimitRecursion(int.MaxValue)).Options.Constraints.MaxRecursionDepth.Should().Be(int.MaxValue);
        new Engine().Options.Constraints.MaxRecursionDepth.Should().Be(-1);

        // and it really does not fail, unlike a small depth
        new Engine(o => o.LimitRecursion(int.MaxValue))
            .Evaluate("function f(n) { return n === 0 ? 0 : f(n - 1); } f(100)").AsNumber().Should().Be(0);

        Invoking(() => new Engine(o => o.LimitRecursion(10)).Evaluate("function f(n) { return n === 0 ? 0 : f(n - 1); } f(100)"))
            .Should().ThrowExactly<RecursionDepthOverflowException>();
    }
}
