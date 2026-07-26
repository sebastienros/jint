#nullable enable
using Jint.Constraints;

namespace Jint.Tests.Runtime;

/// <summary>
/// Pins the registration semantics of the built-in constraint helpers, as distinct from what the
/// constraints do once registered: which values register a constraint at all, and that calling a
/// helper twice replaces rather than accumulates.
/// </summary>
public class ConstraintRegistrationTests
{
    private static List<Constraint> Register(Action<Options> configure)
    {
        var options = new Options();
        configure(options);
        return options.Constraints.Constraints;
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
}
