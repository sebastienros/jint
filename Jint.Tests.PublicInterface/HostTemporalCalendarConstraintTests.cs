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
/// The walk crosses no statement boundary, so nothing in the interpreter's per-statement path is reached
/// while it runs: before <see href="https://github.com/sebastienros/jint/issues/3511"/> a
/// <c>LimitExecutionTime</c> of 100 ms returned an answer two seconds later, a <c>LimitStatements</c> of ten
/// returned one after two, and a token cancelled from another thread was never observed — each measured on
/// this same script. Every test here therefore asserts that the exception is <em>raised</em>, never how long
/// anything took: the budgets are chosen so that the work is orders of magnitude larger than the bound, and
/// a machine under load only makes them fire sooner.
/// </remarks>
public class HostTemporalCalendarConstraintTests
{
    /// <summary>
    /// Three hundred thousand months is some twenty-four thousand lunisolar years, which is four orders of
    /// magnitude past the walk's constraint-check interval and comfortably inside <c>Temporal</c>'s range —
    /// so the engine owes the script a real answer, and the only question is whether it can be interrupted
    /// on the way to it.
    /// </summary>
    private const string BulkChineseAddition =
        "Temporal.PlainDate.from('2000-01-01').withCalendar('chinese').add({ months: 300000 }).toString()";

    private const string BulkHebrewAddition =
        "Temporal.PlainDate.from('2000-01-01').withCalendar('hebrew').add({ months: 300000 }).toString()";

    /// <summary>A month difference is measured by walking, so the walk is where its cost is too.</summary>
    private const string BulkChineseDifference =
        "Temporal.PlainDate.from('2000-01-01').withCalendar('chinese')"
        + ".until(Temporal.PlainDate.from('3000-01-01').withCalendar('chinese'), { largestUnit: 'month' })"
        + ".toString()";

    [Test]
    public void AnExecutionTimeoutStopsABulkMonthAddition()
    {
        var engine = new Engine(options => options.LimitExecutionTime(TimeSpan.FromMilliseconds(100)));

        Assert.Throws<TimeoutException>(() => engine.Evaluate(BulkChineseAddition));
    }

    [Test]
    public void AStatementBudgetStopsABulkMonthAddition()
    {
        var engine = new Engine(options => options.LimitStatements(5));

        Assert.Throws<StatementsCountOverflowException>(() => engine.Evaluate(BulkChineseAddition));
    }

    [Test]
    public void ACancelledTokenStopsABulkMonthAddition()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var engine = new Engine(options => options.ObserveCancellation(cancellation.Token));

        Assert.Throws<ExecutionCanceledException>(() => engine.Evaluate(BulkChineseAddition));
    }

    /// <summary>
    /// <c>hebrew</c> walks the same loop for a different reason — twelve months one year and thirteen the
    /// next — so it is bounded by the same check rather than by anything lunisolar.
    /// </summary>
    [Test]
    public void ACancelledTokenStopsABulkMonthAdditionInHebrew()
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

        Assert.Throws<ExecutionCanceledException>(() => engine.Evaluate(BulkChineseDifference));
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
            "Temporal.PlainDate.from('2000-01-01').withCalendar('chinese').add({ months: 13 }).toString()");

        result.AsString().Should().Be("2001-01-19[u-ca=chinese]");
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
            "Temporal.PlainDate.from('2000-01-01').withCalendar('chinese').add({ months: 10000 }).toString()");

        result.AsString().Should().Be("2808-07-09[u-ca=chinese]");
    }
}
