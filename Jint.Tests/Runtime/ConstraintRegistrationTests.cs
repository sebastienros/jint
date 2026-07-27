#nullable enable
using Jint.Constraints;

namespace Jint.Tests.Runtime;

/// <summary>
/// Pins the registration <em>counts</em> of the built-in constraint helpers, as distinct from what the
/// constraints do once registered: which values register a constraint at all, and that calling a
/// helper twice replaces rather than accumulates. Counting needs the engine's internal constraint list —
/// nothing public enumerates it, and the time constraint has no public type to look up either — which is
/// why this file stays inside the assembly.
/// <para>
/// What an embedder can observe of the same behaviour is pinned from outside by
/// <c>Jint.Tests.PublicInterface.ConstraintReplacementTests</c>.
/// </para>
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
}
