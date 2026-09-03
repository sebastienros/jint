#nullable enable

namespace Jint.Tests.Runtime;

/// <summary>
/// Adding months in <c>hebrew</c> counts them on the Metonic cycle rather than walking one calendar year
/// at a time, and a calendar whose years all hold the same number of months divides instead of walking
/// too. These pin that they do, and that they land where the walk landed.
/// </summary>
/// <remarks>
/// <para>
/// <b>How the bound is asserted.</b> Never with a clock — a wall-clock assertion on a shared machine is
/// the flake it would exist to catch. The walk consults the engine's constraints every 256 years stepped
/// (<see href="https://github.com/sebastienros/jint/issues/3511"/>) and a constraint check charges
/// <c>MaxStatements</c> one statement, so a statement budget <em>is</em> a budget of years walked. A
/// hundred thousand Hebrew months is some eight thousand years and would spend thirty-one of them;
/// counting months spends none, so it fits in a budget of two with the script's own statement to spare.
/// </para>
/// <para>
/// <b>Why the answers are written down.</b> Both reckonings were compared over 483,875 cases — every
/// 37th day from ISO 1500 to 2400 against twenty month steps, twenty-one dates from ISO −400 to +100000
/// against twenty-six steps out to three million months, and 528 <c>until</c> and <c>since</c>
/// differences, all in <c>hebrew</c> and <c>persian</c>, plus 126,455 field reads across five calendars —
/// and they agree on every one. The values below come from that comparison, so a change to any of them is
/// a regression rather than a matter of taste
/// (<see href="https://github.com/sebastienros/jint/issues/3520"/>).
/// </para>
/// <para>
/// <b>The four Persian values past ISO 9999 moved once.</b> Not because a step is taken differently — the
/// two spellings still agree on every one of those cases — but because the reckoning underneath them
/// became the 33-year cycle rather than the 2820-year one, which is what every other Temporal
/// implementation answers a proleptic <c>persian</c> date with
/// (<see href="https://github.com/sebastienros/jint/issues/3604"/>). Those years lie outside
/// <c>PersianCalendar</c>'s window, so nothing but integer arithmetic on a fixed epoch decides them; where
/// that window ends, and that it is Jint's to state rather than the runner's, is
/// <see cref="PersianCalendarBoundaryTests"/>.
/// </para>
/// </remarks>
public class HebrewMonthSteppingTests
{
    private static string Add(Engine engine, string calendar, string start, long months)
        => engine.Evaluate(
                $"Temporal.PlainDate.from('{start}').withCalendar('{calendar}').add({{ months: {months} }}).toString()")
            .AsString();

    /// <summary>
    /// The work bound. Walking three million months spends nine hundred and forty-seven statements on
    /// constraint checks alone, so a budget of two is what says the walk did not happen.
    /// </summary>
    [TestCase("hebrew", 100_000, "+010085-03-13")]
    [TestCase("hebrew", 300_000, "+026255-08-10")]
    [TestCase("hebrew", 3_000_000, "+244556-01-22")]
    [TestCase("persian", 3_000_000, "+251999-12-13")]
    public void ABulkMonthAdditionDoesNotWalkYearByYear(string calendar, int months, string expected)
    {
        var engine = new Engine(options => options.LimitStatements(2));

        Add(engine, calendar, "2000-01-01", months).Should().Be($"{expected}[u-ca={calendar}]");
    }

    /// <summary>
    /// A month difference is measured by walking one month at a time from an estimate, and every step of
    /// that walk used to be an <c>add</c> that walked the years between: eighteen thousand years of
    /// <c>hebrew</c> took 71 seconds. The estimate is still stepped off one month at a time — a few
    /// hundred steps, since the average month length it starts from is not exact — but each of those
    /// steps is now arithmetic, which is the whole of the difference.
    /// </summary>
    [Test]
    public void AMonthDifferenceDoesNotWalkYearByYear()
    {
        var engine = new Engine(options => options.LimitStatements(5));

        var difference = engine.Evaluate(
            "Temporal.PlainDate.from('2000-01-01').withCalendar('hebrew')"
            + ".until(Temporal.PlainDate.from('+020000-01-01').withCalendar('hebrew'), { largestUnit: 'month' })"
            + ".toString()").AsString();

        difference.Should().Be("P222628M28D");
    }

    /// <summary>
    /// Stepping there in one go and stepping there a month at a time have to agree, over a span that
    /// crosses Adar I in both directions and, for three of the starts, the end of the backing calendar's
    /// own table.
    /// </summary>
    /// <remarks>
    /// The Persian start at ISO 9999-06-01 is the one the two spellings disagreed on until the year the
    /// table stops inside stopped being read as a ten-month year; <see cref="CalendarBandEdgeTests"/>
    /// sweeps the window it sits in day by day.
    /// </remarks>
    [TestCase("hebrew", "2000-01-01")]
    [TestCase("hebrew", "1582-11-30")]
    [TestCase("hebrew", "2239-01-15")]
    [TestCase("hebrew", "+010000-06-06")]
    [TestCase("persian", "2000-01-01")]
    [TestCase("persian", "9000-06-01")]
    [TestCase("persian", "9999-06-01")]
    [TestCase("persian", "+010500-06-06")]
    public void SteppingByOneMonthAtATimeReachesTheSamePlace(string calendar, string start)
    {
        var engine = new Engine();

        for (var months = 1; months <= 40; months++)
        {
            var direct = Add(engine, calendar, start, months);

            var oneAtATime = engine.Evaluate(
                $$"""
                (() => {
                    let d = Temporal.PlainDate.from('{{start}}').withCalendar('{{calendar}}');
                    for (let i = 0; i < {{months}}; i++) { d = d.add({ months: 1 }); }
                    return d.toString();
                })()
                """).AsString();

            oneAtATime.Should().Be(direct, "stepping {0} months one at a time from {1}", months, start);
        }
    }

    /// <summary>
    /// Out and back on the first day of a month, where no day has to be clamped, returns where it
    /// started.
    /// </summary>
    [TestCase("hebrew", 1)]
    [TestCase("hebrew", 13)]
    [TestCase("hebrew", 1237)]
    [TestCase("hebrew", 100_000)]
    [TestCase("persian", 1237)]
    [TestCase("persian", 100_000)]
    public void SteppingOutAndBackReturnsWhereItStarted(string calendar, int months)
    {
        var engine = new Engine();

        var roundTrip = engine.Evaluate(
            $$"""
            Temporal.PlainDate.from({ year: 2000, monthCode: 'M01', day: 1, calendar: '{{calendar}}' })
                .add({ months: {{months}} })
                .subtract({ months: {{months}} })
                .toString()
            """).AsString();

        var start = engine.Evaluate(
            $"Temporal.PlainDate.from({{ year: 2000, monthCode: 'M01', day: 1, calendar: '{calendar}' }}).toString()")
            .AsString();

        roundTrip.Should().Be(start);
    }

    /// <summary>
    /// A month index is a month index however it is reached, so a span split in two has to reach where
    /// the whole span reaches.
    /// </summary>
    [TestCase("hebrew", "+042426-02-07")]
    [TestCase("persian", "+043666-10-04")]
    public void AMonthSpanSplitInTwoReachesWhereTheWholeSpanReaches(string calendar, string expected)
    {
        var engine = new Engine();

        var whole = Add(engine, calendar, "2000-02-05", 500_000);

        var halves = engine.Evaluate(
            $"Temporal.PlainDate.from('2000-02-05').withCalendar('{calendar}')"
            + ".add({ months: 250000 }).add({ months: 250000 }).toString()").AsString();

        halves.Should().Be(whole);
        whole.Should().Be($"{expected}[u-ca={calendar}]");
    }

    /// <summary>
    /// The two directions have to be each other's inverse: adding back what a difference measured has to
    /// land on the date it was measured to, including across the end of the backing calendar's table.
    /// </summary>
    [TestCase("hebrew", "1600-02-29", "2300-11-05")]
    [TestCase("hebrew", "1500-01-01", "+012000-01-01")]
    [TestCase("hebrew", "2239-09-29", "2240-10-15")]
    [TestCase("persian", "1600-02-29", "2300-11-05")]
    [TestCase("persian", "9000-01-01", "+010500-06-06")]
    public void AddingWhatUntilMeasuredReachesTheDateItMeasuredTo(string calendar, string one, string two)
    {
        var engine = new Engine();

        var reached = engine.Evaluate(
            $$"""
            (() => {
                const a = Temporal.PlainDate.from('{{one}}').withCalendar('{{calendar}}');
                const b = Temporal.PlainDate.from('{{two}}').withCalendar('{{calendar}}');
                return a.add(a.until(b, { largestUnit: 'month' })).equals(b);
            })()
            """).AsBoolean();

        reached.Should().BeTrue();
    }

    /// <summary>
    /// One step across the end of a backing calendar's table, which is where the year range the walk used
    /// to discover by catching <c>ArgumentOutOfRangeException</c> is now compared against. The Hebrew
    /// table runs to ISO 2239-09-29 and starts at 1583-01-01; the Persian one stops in the middle of
    /// 9378, which is why the band leaves that year out and the reckoning answers for it.
    /// </summary>
    [TestCase("hebrew", "1583-01-01", -13, "1581-12-12")]
    [TestCase("hebrew", "1583-01-01", -1, "1582-12-02")]
    [TestCase("hebrew", "1583-01-01", 1, "1583-01-30")]
    [TestCase("hebrew", "2239-09-29", 1, "2239-10-28")]
    [TestCase("hebrew", "2239-09-29", 13, "2240-10-15")]
    [TestCase("hebrew", "+100000-01-01", -1, "+099999-12-02")]
    [TestCase("hebrew", "+100000-01-01", 13, "+100001-01-20")]
    [TestCase("persian", "9999-12-31", -1, "9999-12-01")]
    [TestCase("persian", "9999-12-31", 1, "+010000-02-02")]
    [TestCase("persian", "9999-12-31", 13, "+010001-02-01")]
    public void AStepAcrossTheEndOfTheTableLandsWhereItAlwaysDid(string calendar, string start, int months, string expected)
    {
        var engine = new Engine();

        Add(engine, calendar, start, months).Should().Be($"{expected}[u-ca={calendar}]");
    }
}
