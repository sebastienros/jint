#nullable enable

using System;
using System.Threading;
using Jint.Runtime;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// A bulk month addition in a calendar whose years hold different numbers of months is a CLR walk of one
/// step per calendar year crossed, and these pin that a host's bound reaches inside it.
/// </summary>
/// <remarks>
/// <para>
/// The walk crosses no statement boundary, so nothing in the interpreter's per-statement path is reached
/// while it runs: before <see href="https://github.com/sebastienros/jint/issues/3511"/> a
/// <c>LimitExecutionTime</c> of 100 ms returned an answer two seconds later, a <c>LimitStatements</c> of ten
/// returned one after two, and a token cancelled from another thread was never observed. Every test here
/// therefore asserts that the exception is <em>raised</em>, never how long anything took: the budgets are
/// chosen so that the work is orders of magnitude larger than the bound, and a machine under load only makes
/// them fire sooner.
/// </para>
/// <para>
/// <b>Why <c>hebrew</c>.</b> The measurements above were taken on <c>chinese</c>, which no longer walks at
/// all: which month lies <em>n</em> lunations away is now a closed-form question. <c>hebrew</c> is the walk
/// that remains — twelve months one year and thirteen the next, one step per year either way — so it is
/// where the bound has to be proved. The one <c>chinese</c> case left is the step so large that the closed
/// form declines it and the walk answers after all, which is the only way back into that loop.
/// </para>
/// </remarks>
public class HostTemporalCalendarConstraintTests
{
    /// <summary>
    /// Three hundred thousand months is some twenty-five thousand Hebrew years, four orders of magnitude
    /// past the walk's constraint-check interval and comfortably inside <c>Temporal</c>'s range — so the
    /// engine owes the script a real answer, and the only question is whether it can be interrupted on the
    /// way to it.
    /// </summary>
    private const string BulkHebrewAddition =
        "Temporal.PlainDate.from('2000-01-01').withCalendar('hebrew').add({ months: 300000 }).toString()";

    /// <summary>Longer, for the budget that has to expire before it can fire.</summary>
    private const string LongerBulkHebrewAddition =
        "Temporal.PlainDate.from('2000-01-01').withCalendar('hebrew').add({ months: 3000000 }).toString()";

    /// <summary>A month difference is measured by walking, so the walk is where its cost is too.</summary>
    private const string BulkHebrewDifference =
        "Temporal.PlainDate.from('2000-01-01').withCalendar('hebrew')"
        + ".until(Temporal.PlainDate.from('3000-01-01').withCalendar('hebrew'), { largestUnit: 'month' })"
        + ".toString()";

    /// <summary>
    /// Past <c>StepMonths</c>'s ceiling, which no representable date needs, so the year walk answers.
    /// </summary>
    private const string UnsteppableChineseAddition =
        "Temporal.PlainDate.from('2000-01-01').withCalendar('chinese').add({ months: 20000000 }).toString()";

    [Test]
    public void AnExecutionTimeoutStopsABulkMonthAddition()
    {
        var engine = new Engine(options => options.LimitExecutionTime(TimeSpan.FromMilliseconds(100)));

        Assert.Throws<TimeoutException>(() => engine.Evaluate(LongerBulkHebrewAddition));
    }

    [Test]
    public void AStatementBudgetStopsABulkMonthAddition()
    {
        var engine = new Engine(options => options.LimitStatements(5));

        Assert.Throws<StatementsCountOverflowException>(() => engine.Evaluate(BulkHebrewAddition));
    }

    [Test]
    public void ACancelledTokenStopsABulkMonthAddition()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var engine = new Engine(options => options.ObserveCancellation(cancellation.Token));

        Assert.Throws<ExecutionCanceledException>(() => engine.Evaluate(BulkHebrewAddition));
    }

    [Test]
    public void ACancelledTokenStopsAMonthDifferenceWalk()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var engine = new Engine(options => options.ObserveCancellation(cancellation.Token));

        Assert.Throws<ExecutionCanceledException>(() => engine.Evaluate(BulkHebrewDifference));
    }

    /// <summary>
    /// The lunisolar walk is still reachable for a step no closed form will make, and it is bounded there
    /// too — otherwise the fallback would be a way back to the unbounded loop.
    /// </summary>
    [Test]
    public void ACancelledTokenStopsTheWalkTheClosedFormDeclines()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var engine = new Engine(options => options.ObserveCancellation(cancellation.Token));

        Assert.Throws<ExecutionCanceledException>(() => engine.Evaluate(UnsteppableChineseAddition));
    }

    /// <summary>
    /// The other half of a bound worth having: an ordinary date still gets its answer. A year's worth of
    /// months is one step of the walk, so nothing an everyday script does reaches the check at all — which
    /// is why a statement budget of one leaves this addition alone.
    /// </summary>
    [Test]
    public void AnOrdinaryAdditionIsNotChargedForTheCheck()
    {
        var engine = new Engine(options => options.LimitStatements(1));

        var result = engine.Evaluate(
            "Temporal.PlainDate.from('2000-01-01').withCalendar('hebrew').add({ months: 13 }).toString()");

        result.AsString().Should().Be("2001-01-18[u-ca=hebrew]");
    }

    /// <summary>
    /// An engine with no constraints answers the bulk addition, and answers it the same as it always did:
    /// the check bounds the walk without changing where it lands.
    /// </summary>
    [Test]
    public void AnUnboundedEngineStillAnswersTheBulkAddition()
    {
        var engine = new Engine();

        var result = engine.Evaluate(
            "Temporal.PlainDate.from('2000-01-01').withCalendar('hebrew').add({ months: 10000 }).toString()");

        result.AsString().Should().Be("2808-07-09[u-ca=hebrew]");
    }
}
