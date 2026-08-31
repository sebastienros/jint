#nullable enable

using System;
using System.Threading;
using Jint.Native.Temporal;
using Jint.Runtime;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// A bulk month addition in a calendar whose months are reached by walking one year at a time is a CLR
/// loop inside one interpreter step, and these pin that a host's bound reaches inside it.
/// </summary>
/// <remarks>
/// <para>
/// The walk crosses no statement boundary, so nothing in the interpreter's per-statement path is reached
/// while it runs: before <see href="https://github.com/sebastienros/jint/issues/3511"/> a
/// <c>LimitExecutionTime</c> of 100 ms returned an answer two seconds later, a <c>LimitStatements</c> of ten
/// returned one after two, and a token cancelled from another thread was never observed. Every test here
/// therefore asserts that the exception is <em>raised</em>, never how long anything took: the budgets are
/// chosen so that the work is far larger than the bound, and a machine under load only makes them fire
/// sooner.
/// </para>
/// <para>
/// <b>Where the walk lives now.</b> The measurements above were taken on <c>chinese</c>, and the ones after
/// them on <c>hebrew</c>; neither walks any more. Which month lies <em>n</em> lunations away is a closed-form
/// question for <c>chinese</c> and <c>dangi</c>, and so is which month lies <em>n</em> months away on the
/// Metonic cycle for <c>hebrew</c> (<see href="https://github.com/sebastienros/jint/issues/3520"/>). What
/// still walks is the lane a host reaches by installing an <see cref="ICalendarProvider"/> of its own: a
/// calendar only the host can convert has no cycle the engine could count, so its months are stepped a year
/// at a time through the provider's own two conversions, asking each year how many months it holds. That is
/// where a bound has to be proved, and it is a host-facing lane rather than an engine-internal one. The one
/// <c>chinese</c> case kept is the step so large that the closed form declines it and the walk answers after
/// all, which is the only way back into that loop.
/// </para>
/// </remarks>
public class HostTemporalCalendarConstraintTests
{
    /// <summary>
    /// Three hundred thousand months is some twenty-five thousand Hebrew years — three orders of magnitude
    /// past the walk's constraint-check interval and comfortably inside <c>Temporal</c>'s range, so the
    /// engine owes the script a real answer, and the only question is whether it can be interrupted on the
    /// way to it. Under the provider it takes some 70 ms and spends 94 statements on checks, so a budget of five
    /// cannot hold it.
    /// </summary>
    private const string BulkMonthAddition =
        "Temporal.PlainDate.from('2000-01-01').withCalendar('hebrew').add({ months: 300000 }).toString()";

    /// <summary>
    /// Longer, for the budget that has to expire before it can fire: about 900 ms of walking against a
    /// 25 ms bound.
    /// </summary>
    private const string LongerBulkMonthAddition =
        "Temporal.PlainDate.from('2000-01-01').withCalendar('hebrew').add({ months: 3000000 }).toString()";

    /// <summary>A month difference is measured by walking, so the walk is where its cost is too.</summary>
    private const string BulkMonthDifference =
        "Temporal.PlainDate.from('2000-01-01').withCalendar('hebrew')"
        + ".until(Temporal.PlainDate.from('3000-01-01').withCalendar('hebrew'), { largestUnit: 'month' })"
        + ".toString()";

    /// <summary>
    /// Past <c>StepMonths</c>'s ceiling, which no representable date needs, so the year walk answers.
    /// </summary>
    private const string UnsteppableChineseAddition =
        "Temporal.PlainDate.from('2000-01-01').withCalendar('chinese').add({ months: 20000000 }).toString()";

    /// <summary>
    /// An engine whose calendars are answered by a host provider, which is what puts every one of them on
    /// the generic walk.
    /// </summary>
    private static Engine WithProvider(Action<Options> configure)
        => new(options =>
        {
            options.Temporal.CalendarProvider = new HostCalendarProvider();
            configure(options);
        });

    [Test]
    public void AnExecutionTimeoutStopsABulkMonthAddition()
    {
        var engine = WithProvider(options => options.LimitExecutionTime(TimeSpan.FromMilliseconds(25)));

        Assert.Throws<TimeoutException>(() => engine.Evaluate(LongerBulkMonthAddition));
    }

    [Test]
    public void AStatementBudgetStopsABulkMonthAddition()
    {
        var engine = WithProvider(options => options.LimitStatements(5));

        Assert.Throws<StatementsCountOverflowException>(() => engine.Evaluate(BulkMonthAddition));
    }

    [Test]
    public void ACancelledTokenStopsABulkMonthAddition()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var engine = WithProvider(options => options.ObserveCancellation(cancellation.Token));

        Assert.Throws<ExecutionCanceledException>(() => engine.Evaluate(BulkMonthAddition));
    }

    [Test]
    public void ACancelledTokenStopsAMonthDifferenceWalk()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var engine = WithProvider(options => options.ObserveCancellation(cancellation.Token));

        Assert.Throws<ExecutionCanceledException>(() => engine.Evaluate(BulkMonthDifference));
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
        var engine = WithProvider(options => options.LimitStatements(1));

        var result = engine.Evaluate(
            "Temporal.PlainDate.from('2000-01-01').withCalendar('hebrew').add({ months: 13 }).toString()");

        result.AsString().Should().Be("2001-01-18[u-ca=hebrew]");
    }

    /// <summary>
    /// An engine with no constraints answers the bulk addition, and answers it the same as it always did:
    /// the check bounds the walk without changing where it lands, and counting months rather than walking
    /// does not change where it lands either.
    /// </summary>
    [Test]
    public void AnUnboundedEngineStillAnswersTheBulkAddition()
    {
        var engine = new Engine();

        var result = engine.Evaluate(
            "Temporal.PlainDate.from('2000-01-01').withCalendar('hebrew').add({ months: 10000 }).toString()");

        result.AsString().Should().Be("2808-07-09[u-ca=hebrew]");
    }

    /// <summary>
    /// And the calendars the engine reckons itself no longer spend a budget at all: the same three million
    /// months that walks under a provider is arithmetic here, so it fits in a statement budget of two —
    /// where that walk needs nine hundred and forty-eight.
    /// </summary>
    [Test]
    public void AnEngineReckonedCalendarSpendsNoBudgetOnABulkMonthAddition()
    {
        var engine = new Engine(options => options.LimitStatements(2));

        var result = engine.Evaluate(LongerBulkMonthAddition);

        result.AsString().Should().Be("+244556-01-22[u-ca=hebrew]");
    }
}

/// <summary>
/// A host provider that corrects nothing. Installing any provider is what routes every calendar through
/// the generic arithmetic, which walks the two conversions a provider supplies — the walk a host with a
/// calendar of its own is actually exposed to.
/// </summary>
file sealed class HostCalendarProvider : DefaultCalendarProvider;
