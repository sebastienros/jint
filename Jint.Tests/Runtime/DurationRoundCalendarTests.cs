#nullable enable

using System.Text;

namespace Jint.Tests.Runtime;

/// <summary>
/// <c>Temporal.Duration.prototype.round</c> reckons in the calendar of the <c>relativeTo</c> it was given,
/// which is what makes it agree with <c>until</c>, <c>add</c> and <c>total</c> about the same two dates.
/// </summary>
/// <remarks>
/// The defect these pin read the calendar off a <c>PlainDate</c> <c>relativeTo</c> and then wrote
/// <c>"iso8601"</c> into both calendar operations it performs, so every rounding with a non-ISO
/// <c>relativeTo</c> counted ISO years and ISO months under that calendar's name — and past a calendar's
/// range it answered where <c>total</c> and <c>add</c> refused. See
/// <see href="https://github.com/sebastienros/jint/issues/3450"/>, and
/// <see href="https://tc39.es/proposal-temporal/#sec-temporal.duration.prototype.round"/> step 443, which
/// is <c>CalendarDateAdd(calendar, …)</c> for the calendar the <c>relativeTo</c> carries.
/// </remarks>
public class DurationRoundCalendarTests
{
    private static readonly string[] EveryCalendarTemporalReckonsIn =
    [
        "iso8601",
        "chinese", "dangi", "hebrew", "persian",
        "coptic", "ethiopic", "ethioaa", "indian",
        "islamic-umalqura", "islamic-civil", "islamic-tbla",
    ];

    /// <summary>Day counts short and long enough to cross months, leap months and years.</summary>
    private static readonly int[] DayCounts = [1, 30, 60, 365, 400, 1000, 3000, 10000, 20000];

    private static string Evaluate(string expression)
    {
        var engine = new Engine();
        return engine.Evaluate(
            $"(function () {{ try {{ return String({expression}); }} catch (e) {{ return e.constructor.name; }} }})()").AsString();
    }

    /// <summary>
    /// Rounding a duration of days with <c>largestUnit: 'year'</c> and a <c>relativeTo</c> is the same
    /// question <c>until</c> answers between the same two dates, so the two have to give the same duration.
    /// </summary>
    [TestCaseSource(nameof(EveryCalendarTemporalReckonsIn))]
    public void RoundingDaysToYearsIsTheDifferenceTheSameCalendarMeasures(string calendar)
    {
        var failures = new StringBuilder();

        foreach (var days in DayCounts)
        {
            var rounded = Evaluate(
                $"new Temporal.Duration(0, 0, 0, {days})" +
                $".round({{ largestUnit: 'year', relativeTo: Temporal.PlainDate.from('1990-01-01').withCalendar('{calendar}') }})");

            var measured = Evaluate(
                $"(function () {{ var r = Temporal.PlainDate.from('1990-01-01').withCalendar('{calendar}');" +
                $" return r.until(r.add({{ days: {days} }}), {{ largestUnit: 'year' }}); }})()");

            if (!string.Equals(rounded, measured, StringComparison.Ordinal))
            {
                failures.Append(calendar).Append(", ").Append(days).Append(" days: round said ")
                    .Append(rounded).Append(", until said ").Append(measured).AppendLine();
            }
        }

        failures.ToString().Should().BeEmpty();
    }

    /// <summary>
    /// The whole-year part of a rounding and the integer part of a total are the same count of the same
    /// calendar's years. <c>total</c> already reckoned in the <c>relativeTo</c>'s calendar, so this is the
    /// disagreement the issue leads with, stated as an invariant.
    /// </summary>
    [TestCaseSource(nameof(EveryCalendarTemporalReckonsIn))]
    public void TheYearsRoundingTruncatesToAreTheYearsTotalCounts(string calendar)
    {
        var failures = new StringBuilder();

        foreach (var days in DayCounts)
        {
            var relativeTo = $"Temporal.PlainDate.from('1990-01-01').withCalendar('{calendar}')";

            var rounded = Evaluate(
                $"new Temporal.Duration(0, 0, 0, {days}).round(" +
                $"{{ largestUnit: 'year', smallestUnit: 'year', roundingMode: 'trunc', relativeTo: {relativeTo} }}).years");

            var totalled = Evaluate(
                $"Math.trunc(new Temporal.Duration(0, 0, 0, {days}).total({{ unit: 'year', relativeTo: {relativeTo} }}))");

            if (!string.Equals(rounded, totalled, StringComparison.Ordinal))
            {
                failures.Append(calendar).Append(", ").Append(days).Append(" days: round said ")
                    .Append(rounded).Append(" years, total said ").Append(totalled).AppendLine();
            }
        }

        failures.ToString().Should().BeEmpty();
    }

    /// <summary>
    /// A <c>ZonedDateTime</c> <c>relativeTo</c> always passed its own calendar through, which is why the
    /// same rounding gave two answers depending on which kind of <c>relativeTo</c> it was handed.
    /// </summary>
    [TestCase("chinese")]
    [TestCase("hebrew")]
    [TestCase("islamic-civil")]
    public void APlainRelativeToAndAZonedOneInTheSameCalendarRoundAlike(string calendar)
    {
        var plain = Evaluate(
            "new Temporal.Duration(0, 0, 0, 10000).round({ largestUnit: 'year', relativeTo: " +
            $"Temporal.PlainDate.from('1990-01-01').withCalendar('{calendar}') }})");

        var zoned = Evaluate(
            "new Temporal.Duration(0, 0, 0, 10000).round({ largestUnit: 'year', relativeTo: " +
            $"Temporal.ZonedDateTime.from('1990-01-01T00:00:00[UTC][u-ca={calendar}]') }})");

        plain.Should().Be(zoned);
    }

    /// <summary>
    /// Thirteen months is a whole year in a lunisolar leap year and a year and a month in the ISO
    /// calendar, so a rounding that counts ISO months answers the wrong one under the calendar's name.
    /// </summary>
    /// <remarks>
    /// 2023-01-22 is day 1 of the first month of the Chinese year 2023, which carries a leap month and
    /// therefore thirteen months in all — <c>monthsInYear</c> reports 13 for it.
    /// </remarks>
    [Test]
    public void ThirteenMonthsIsAWholeYearInALunisolarLeapYear()
    {
        Evaluate("Temporal.PlainDate.from('2023-01-22').withCalendar('chinese').monthsInYear")
            .Should().Be("13");

        Evaluate(
            "new Temporal.Duration(0, 13).round({ largestUnit: 'year', relativeTo: " +
            "Temporal.PlainDate.from('2023-01-22').withCalendar('chinese') })")
            .Should().Be("P1Y");

        // The same thirteen months against an ISO relativeTo, which is what the defect answered for both.
        Evaluate("new Temporal.Duration(0, 13).round({ largestUnit: 'year', relativeTo: '2023-01-22' })")
            .Should().Be("P1Y1M");
    }

    /// <summary>
    /// The report from the issue. A span that leaves the Chinese calendar's range has no answer in it, and
    /// the three members that reckon in the calendar have to say so together.
    /// </summary>
    /// <remarks>
    /// This asserts that they <em>agree</em>, not which way: whichever range the engine can reckon the
    /// calendar over, a span past its end is refused by all three or answered by all three. The
    /// <c>add</c> arm adds <em>years</em>, because that is the component
    /// <see href="https://tc39.es/proposal-temporal/#sec-temporal-calendardateadd">CalendarDateAdd</see>
    /// reckons in the calendar — days and weeks are ISO days whatever the calendar is, and
    /// <c>add({ days: 200000 })</c> therefore never asks it anything.
    /// </remarks>
    [Test]
    public void ASpanPastTheCalendarsRangeIsRefusedByRoundExactlyWhenItIsRefusedByTotalAndAdd()
    {
        const string RelativeTo = "Temporal.PlainDate.from('1990-01-01').withCalendar('chinese')";

        var rounded = Evaluate(
            $"new Temporal.Duration(0, 0, 0, 200000).round({{ largestUnit: 'year', relativeTo: {RelativeTo} }})");
        var totalled = Evaluate(
            $"new Temporal.Duration(0, 0, 0, 200000).total({{ unit: 'year', relativeTo: {RelativeTo} }})");
        var added = Evaluate($"{RelativeTo}.add({{ years: 547 }})");

        var kinds = new[] { rounded, totalled, added }
            .Select(r => string.Equals(r, "RangeError", StringComparison.Ordinal))
            .Distinct()
            .Count();

        kinds.Should().Be(
            1,
            "round, total and add reckon in the same calendar over the same span, but round said {0}, total said {1} and add said {2}",
            rounded,
            totalled,
            added);
    }

    /// <summary>
    /// An ISO <c>relativeTo</c> is the case the hardcoded calendar happened to be right for, and nothing
    /// about it moves.
    /// </summary>
    [Test]
    public void AnIsoRelativeToRoundsExactlyAsItDid()
    {
        Evaluate("new Temporal.Duration(0, 0, 0, 10000).round({ largestUnit: 'year', relativeTo: '1990-01-01' })")
            .Should().Be("P27Y4M18D");

        Evaluate("new Temporal.Duration(0, 0, 0, 10000).total({ unit: 'year', relativeTo: '1990-01-01' })")
            .Should().Be("27.378082191780823");
    }
}
