#nullable enable

using Jint.Runtime;

namespace Jint.Tests.Runtime;

/// <summary>
/// Adding months in <c>chinese</c> or <c>dangi</c> counts lunations rather than walking one calendar year
/// at a time. These pin that it does, that it lands where the walk landed, and that it no longer lands
/// where the walk was wrong.
/// </summary>
/// <remarks>
/// <para>
/// <b>How the bound is asserted.</b> Never with a clock — a wall-clock assertion on a shared machine is the
/// flake it would exist to catch. The walk consults the engine's constraints every 256 years stepped
/// (<see href="https://github.com/sebastienros/jint/issues/3511"/>) and a constraint check charges
/// <c>MaxStatements</c> one statement, so a statement budget <em>is</em> a budget of years walked. A hundred
/// thousand months is some eight thousand lunisolar years and would spend thirty-one of them; counting
/// lunations spends none, so it fits in a budget of two with the script's own statement to spare.
/// </para>
/// <para>
/// <b>Why the answers are written down.</b> The two reckonings were compared over 356,326 cases — every
/// 37th day from ISO 1500 to 2400 plus a dozen dates further out, twenty month steps from −37 to +1200, and
/// 444 month differences, in both calendars — and they agree on every one. The values below come from that
/// comparison, so a change to any of them is a regression rather than a matter of taste.
/// </para>
/// </remarks>
public class LunisolarMonthSteppingTests
{
    private static string Add(Engine engine, string calendar, string start, long months)
        => engine.Evaluate(
                $"Temporal.PlainDate.from('{start}').withCalendar('{calendar}').add({{ months: {months} }}).toString()")
            .AsString();

    /// <summary>
    /// The work bound. Walking a hundred thousand months spends thirty-one statements on constraint checks
    /// alone, so a budget of two is what says the walk did not happen.
    /// </summary>
    [TestCase("chinese", "+010085-03-12[u-ca=chinese]")]
    [TestCase("dangi", "+010085-03-12[u-ca=dangi]")]
    public void ABulkMonthAdditionDoesNotWalkYearByYear(string calendar, string expected)
    {
        var engine = new Engine(options => options.LimitStatements(2));

        Add(engine, calendar, "2000-01-01", 100_000).Should().Be(expected);
    }

    /// <summary>
    /// Three million months is about as far as the calendar's own range reaches, and it is still a date the
    /// engine owes the script.
    /// </summary>
    [Test]
    public void TheLargestAdditionTheRangePermitsIsAnswered()
    {
        var engine = new Engine(options => options.LimitStatements(2));

        Add(engine, "chinese", "2000-01-01", 3_000_000).Should().Be("+244615-11-01[u-ca=chinese]");
    }

    /// <summary>
    /// Stepping there in one go and stepping there a month at a time have to agree, over a span that
    /// crosses leap months in both directions.
    /// </summary>
    [TestCase("chinese", "2000-01-01")]
    [TestCase("chinese", "2033-11-22")]
    [TestCase("chinese", "1800-01-01")]
    [TestCase("chinese", "2200-06-06")]
    [TestCase("dangi", "1000-01-01")]
    [TestCase("dangi", "2000-02-05")]
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
    /// Out and back on the first day of a month, where no day has to be clamped, returns where it started.
    /// </summary>
    [TestCase("chinese", 1)]
    [TestCase("chinese", 13)]
    [TestCase("chinese", 1237)]
    [TestCase("chinese", 100_000)]
    [TestCase("dangi", 1237)]
    [TestCase("dangi", 100_000)]
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

        roundTrip.Should().Be($"2000-02-05[u-ca={calendar}]");
    }

    /// <summary>
    /// A month is a lunation, so a span split in two has to reach where the whole span reaches. The walk
    /// did not: it answered <c>+042426-12-30</c> for the whole and raised <c>RangeError</c> for the halves,
    /// because wherever the reckoning cannot name a lunisolar new year inside a Gregorian year it counted a
    /// twelve-month year that does not exist.
    /// </summary>
    [TestCase("chinese")]
    [TestCase("dangi")]
    public void AMonthSpanSplitInTwoReachesWhereTheWholeSpanReaches(string calendar)
    {
        var engine = new Engine();

        var whole = Add(engine, calendar, "2000-02-05", 500_000);

        var halves = engine.Evaluate(
            $"Temporal.PlainDate.from('2000-02-05').withCalendar('{calendar}')"
            + ".add({ months: 250000 }).add({ months: 250000 }).toString()").AsString();

        halves.Should().Be(whole);
        whole.Should().Be($"+042426-01-10[u-ca={calendar}]");
    }

    /// <summary>
    /// Far enough out that the calendar's new year has drifted clean out of the Gregorian year that names
    /// it, one month back used to move thirteen and a year forward used to be refused outright.
    /// </summary>
    [Test]
    public void AMonthStepFarOutMovesOneMonth()
    {
        var engine = new Engine();

        Add(engine, "chinese", "+100000-01-01", -1).Should().Be("+099999-12-03[u-ca=chinese]");
        Add(engine, "chinese", "+100000-01-01", 13).Should().Be("+100001-01-19[u-ca=chinese]");
    }

    /// <summary>
    /// A step no representable date could survive still reports the range failure rather than an answer.
    /// </summary>
    [Test]
    public void AStepPastTheCalendarsRangeIsARangeError()
    {
        var engine = new Engine();

        var act = () => engine.Evaluate(
            "Temporal.PlainDate.from('2000-01-01').withCalendar('chinese').add({ months: 4000000 })");

        act.Should().Throw<JavaScriptException>();
    }
}
