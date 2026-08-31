#nullable enable

using System.Text;
using Jint.Native.Temporal;

namespace Jint.Tests.Runtime;

/// <summary>
/// A backing <see cref="System.Globalization.Calendar"/> whose table stops part-way through one of its
/// own years reports that part as the whole year. These pin that the engine does not believe it, and that
/// a month addition therefore stays additive across the end of the table.
/// </summary>
/// <remarks>
/// <para>
/// <c>PersianCalendar</c> is cut off by <c>DateTime</c> rather than by its own data: its
/// <c>MaxSupportedDateTime</c> is ISO 9999-12-31, which falls inside Persian 9378, and every question
/// about that year comes back truncated — ten months rather than twelve, 289 days rather than 365, and a
/// tenth month thirteen days long rather than thirty. A step that stopped inside that month had its day
/// clamped to the thirteenth and carried the loss forward, while a step that flew over the month kept it,
/// so eight months in one go and eight one at a time landed a day apart
/// (<see href="https://github.com/sebastienros/jint/issues/3523"/>).
/// </para>
/// <para>
/// <b>How additivity is asserted.</b> Only from a day no month can be short of. Persian months hold 29 to
/// 31 days and Hebrew months 29 or 30, so a start on day 29 or earlier is never clamped by any month it
/// can land on, and bulk and stepped addition have to agree exactly. Above that they legitimately diverge
/// in every calendar — ISO 2024-01-31 plus two months is March 31, and one month twice is March 29 — so
/// those starts are skipped rather than asserted.
/// </para>
/// </remarks>
public class CalendarBandEdgeTests
{
    /// <summary>The issue's own repro: eight months in one step, and eight single steps.</summary>
    [Test]
    public void EightMonthsInOneStepAndEightSingleStepsAgreeAtTheEndOfThePersianTable()
    {
        var engine = new Engine();

        var bulk = engine.Evaluate(
            "Temporal.PlainDate.from('9999-06-01').withCalendar('persian').add({ months: 8 }).toString()").AsString();

        var stepped = engine.Evaluate(
            """
            (() => {
                let d = Temporal.PlainDate.from('9999-06-01').withCalendar('persian');
                for (let i = 0; i < 8; i++) { d = d.add({ months: 1 }); }
                return d.toString();
            })()
            """).AsString();

        stepped.Should().Be(bulk);
    }

    /// <summary>
    /// Every day across the last thirty years the table covers and the first thirty past it, stepped by
    /// one month at a time and in one go, forwards and backwards.
    /// </summary>
    /// <remarks>
    /// The window is dense rather than sampled on purpose: the seam is one day wide, and a stride wide
    /// enough to be cheap is a stride wide enough to step over it.
    /// </remarks>
    [TestCase("persian", 9969, 62)]
    [TestCase("hebrew", 1553, 62)]
    [TestCase("hebrew", 2209, 62)]
    public void ABulkMonthAdditionMatchesSingleStepsAcrossABandEdge(string calendar, int firstIsoYear, int isoYears)
    {
        var failures = new StringBuilder();
        var cases = 0;
        var failed = 0;
        var skipped = 0;

        var first = TemporalHelpers.IsoDateToDays(firstIsoYear, 1, 1);
        var last = TemporalHelpers.IsoDateToDays(firstIsoYear + isoYears, 1, 1);

        for (var epochDay = first; epochDay < last; epochDay++)
        {
            var start = TemporalHelpers.DaysToIsoDate(epochDay);
            if (NonIsoCalendars.IsoToCalendarDate(calendar, in start).Day > 29)
            {
                skipped++;
                continue;
            }

            foreach (var sign in Signs)
            {
                var stepped = start;
                for (var months = 1; months <= 13; months++)
                {
                    cases++;
                    stepped = NonIsoCalendars.CalendarDateAdd(calendar, in stepped, 0, sign, "constrain");
                    var bulk = NonIsoCalendars.CalendarDateAdd(calendar, in start, 0, sign * months, "constrain");

                    if (bulk == stepped)
                    {
                        continue;
                    }

                    failed++;
                    if (failures.Length < 2000)
                    {
                        failures.Append(Show(start)).Append(sign < 0 ? " - " : " + ").Append(months)
                            .Append(" months: ").Append(Show(bulk)).Append(" in one go, ").Append(Show(stepped))
                            .Append(" one at a time").AppendLine();
                    }
                }
            }
        }

        cases.Should().BeGreaterThan(500_000, "the window has to be dense enough to contain the seam");
        skipped.Should().BeGreaterThan(0, "days a short month can clamp are deliberately not asserted");
        failures.ToString().Should().BeEmpty("{0} of {1} cases disagreed, the first of them being", failed, cases);
    }

    /// <summary>
    /// A difference measured across the edge and added back reaches where it was measured to, in every
    /// unit a date difference can be largest in.
    /// </summary>
    [TestCase("persian", "9999-01-15", "+010000-07-20")]
    [TestCase("persian", "9999-06-01", "+010000-02-01")]
    [TestCase("persian", "9998-11-30", "+010001-04-09")]
    [TestCase("persian", "9999-12-31", "+010000-01-02")]
    [TestCase("hebrew", "1582-06-14", "1584-02-02")]
    [TestCase("hebrew", "2239-01-15", "2241-08-08")]
    public void ADifferenceAcrossABandEdgeAddsBackToWhereItWasMeasuredTo(string calendar, string one, string two)
    {
        var engine = new Engine();

        foreach (var largestUnit in LargestUnits)
        {
            var script =
                $$"""
                (() => {
                    const a = Temporal.PlainDate.from('{{one}}').withCalendar('{{calendar}}');
                    const b = Temporal.PlainDate.from('{{two}}').withCalendar('{{calendar}}');
                    return a.add(a.until(b, { largestUnit: '{{largestUnit}}' })).toString();
                })()
                """;

            engine.Evaluate(script).AsString().Should()
                .Be($"{two}[u-ca={calendar}]", "a.until(b) in {0} added back to a", largestUnit);
        }
    }

    /// <summary>
    /// The year the Persian table stops inside holds the twelve months and 365 days the calendar gives
    /// it, not the ten months and 289 days the table has room for.
    /// </summary>
    [Test]
    public void ThePartialYearAtTheEndOfThePersianTableIsAnsweredByTheReckoning()
    {
        var engine = new Engine();

        var fields = engine.Evaluate(
            """
            (() => {
                const d = Temporal.PlainDate.from('9999-12-31').withCalendar('persian');
                return [d.year, d.monthCode, d.day, d.monthsInYear, d.daysInMonth, d.daysInYear].join(' ');
            })()
            """).AsString();

        fields.Should().Be("9378 M10 13 12 30 365");
    }

    /// <summary>
    /// The year before it is one the table holds whole, and stays the table's: 9377 is a leap year of 366
    /// days there and a common year of 365 by the arithmetic reckoning, so this says which one answered.
    /// </summary>
    [Test]
    public void TheLastWholeYearThePersianTableHoldsIsStillReadFromTheTable()
    {
        var engine = new Engine();

        var fields = engine.Evaluate(
            """
            (() => {
                const d = Temporal.PlainDate.from('9998-06-01').withCalendar('persian');
                return [d.year, d.monthCode, d.day, d.daysInYear, d.inLeapYear].join(' ');
            })()
            """).AsString();

        fields.Should().Be("9377 M03 15 366 true");
    }

    /// <summary>
    /// The eleventh and twelfth months of that year, and the second half of its tenth, are dates the
    /// calendar holds even though the table has no room for them, so <c>reject</c> has nothing to reject.
    /// </summary>
    [TestCase(10, 20)]
    [TestCase(11, 1)]
    [TestCase(12, 29)]
    public void ADayThePersianTableHasNoRoomForIsStillADayOfTheCalendar(int month, int day)
    {
        var engine = new Engine();

        var round = engine.Evaluate(
            $$"""
            (() => {
                const d = Temporal.PlainDate.from(
                    { calendar: 'persian', year: 9378, month: {{month}}, day: {{day}} }, { overflow: 'reject' });
                return [d.year, d.month, d.day].join(' ');
            })()
            """).AsString();

        round.Should().Be($"9378 {month} {day}");
    }

    private static readonly int[] Signs = [1, -1];

    private static readonly string[] LargestUnits = ["year", "month", "week", "day"];

    private static string Show(in IsoDate date) => $"{date.Year:D4}-{date.Month:D2}-{date.Day:D2}";
}
