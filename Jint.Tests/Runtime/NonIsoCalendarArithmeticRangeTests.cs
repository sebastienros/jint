#nullable enable

namespace Jint.Tests.Runtime;

/// <summary>
/// Every non-ISO calendar answers its field accessors for every date in Temporal's range. These pin that
/// its arithmetic answers for the same dates — that a calendar never reports two verdicts about whether
/// the engine can reckon one date.
/// </summary>
public class NonIsoCalendarArithmeticRangeTests
{
    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(15);

    private static string Evaluate(string expression)
    {
        var result = "";

        DedicatedThread.Run(
            () =>
            {
                var engine = new Engine();
                result = engine.Evaluate(
                    $"(function () {{ try {{ return String({expression}); }} catch (e) {{ return e.constructor.name + ': ' + e.message; }} }})()").AsString();
            },
            joinTimeout: Budget,
            timeoutMessage: $"did not finish within {Budget}: {expression}",
            maxStackSize: DedicatedThread.DefaultStackSize);

        return result;
    }

    /// <summary>
    /// The four calendars a <c>System.Globalization.Calendar</c> backs over less than Temporal's range,
    /// sampled below the floor and above the ceiling of each.
    /// </summary>
    private static IEnumerable<TestCaseData> PastTheirBackingRangeCases()
    {
        yield return new TestCaseData("hebrew", "1500-06-15").SetName("hebrew below its 1583 floor");
        yield return new TestCaseData("hebrew", "2300-06-15").SetName("hebrew above its 2239 ceiling");
        yield return new TestCaseData("persian", "0500-06-15").SetName("persian below its 622 floor");
        yield return new TestCaseData("chinese", "1800-01-01").SetName("chinese below its 1901 floor");
        yield return new TestCaseData("chinese", "2200-01-01").SetName("chinese above its 2101 ceiling");
        yield return new TestCaseData("dangi", "0800-01-01").SetName("dangi below its 918 floor");
        yield return new TestCaseData("dangi", "2200-01-01").SetName("dangi above its 2051 ceiling");
    }

    /// <summary>
    /// The report from the issue, verbatim: the accessors reckon 1500-06-15 in the Hebrew calendar and
    /// the arithmetic refuses the same date.
    /// </summary>
    [Test]
    public void TheReportedHebrewDateAddsAMonthWhereItsYearIsAlreadyAnswered()
    {
        Evaluate("Temporal.PlainDate.from('1500-06-15').withCalendar('hebrew').year")
            .Should().Be("5260");

        Evaluate("Temporal.PlainDate.from('1500-06-15').withCalendar('hebrew').add({ months: 1 }).toString()")
            .Should().NotStartWith("RangeError");
    }

    /// <summary>
    /// Adding a month and taking it back is the identity, whichever side of a backing calendar's range
    /// the date sits on. An answer that is merely produced is not enough; it has to be the calendar's.
    /// </summary>
    [TestCaseSource(nameof(PastTheirBackingRangeCases))]
    public void AMonthAddedPastTheBackingRangeIsAMonthTakenBack(string calendar, string iso)
    {
        Evaluate(
            $@"(function () {{
                 var d = Temporal.PlainDate.from('{iso}').withCalendar('{calendar}');
                 return d.add({{ months: 1 }}).subtract({{ months: 1 }}).equals(d);
               }})()")
            .Should().Be("true", $"{calendar} at {iso}");
    }

    /// <summary>
    /// A year added past the range keeps the monthCode, which is what makes it calendar arithmetic and
    /// not ISO arithmetic wearing the calendar's name.
    /// </summary>
    [TestCaseSource(nameof(PastTheirBackingRangeCases))]
    public void AYearAddedPastTheBackingRangeKeepsTheMonthCode(string calendar, string iso)
    {
        Evaluate(
            $@"(function () {{
                 var d = Temporal.PlainDate.from('{iso}').withCalendar('{calendar}');
                 var n = d.add({{ years: 1 }});
                 return (n.year - d.year) + '|' + (n.monthCode === d.monthCode);
               }})()")
            .Should().Be("1|true", $"{calendar} at {iso}");
    }

    /// <summary>
    /// <c>until</c> and <c>add</c> are the same reckoning read in opposite directions. Past the backing
    /// range they have to stay each other's inverse, and the walk has to terminate while doing it.
    /// </summary>
    [TestCaseSource(nameof(PastTheirBackingRangeCases))]
    public void TheMonthDifferencePastTheBackingRangeIsTheMonthsThatWereAdded(string calendar, string iso)
    {
        Evaluate(
            $@"(function () {{
                 var d = Temporal.PlainDate.from('{iso}').withCalendar('{calendar}');
                 return d.until(d.add({{ months: 5 }}), {{ largestUnit: 'month' }}).toString();
               }})()")
            .Should().Be("P5M", $"{calendar} at {iso}");
    }

    /// <summary>
    /// The whole of the point: whichever calendar and whichever date, a date whose year the engine reports
    /// is a date whose arithmetic the engine performs. Nothing about a date says which of the two it will
    /// get, so the engine may not answer one and refuse the other.
    /// </summary>
    [TestCase("chinese")]
    [TestCase("dangi")]
    [TestCase("hebrew")]
    [TestCase("persian")]
    [TestCase("coptic")]
    [TestCase("ethiopic")]
    [TestCase("ethioaa")]
    [TestCase("indian")]
    [TestCase("islamic-umalqura")]
    [TestCase("islamic-civil")]
    [TestCase("islamic-tbla")]
    public void EveryCalendarThatReportsAYearAlsoAddsToIt(string calendar)
    {
        foreach (var iso in new[] { "-005000-06-15", "0500-06-15", "1500-06-15", "1990-03-05", "2300-06-15", "9000-06-15" })
        {
            Evaluate($"Temporal.PlainDate.from('{iso}').withCalendar('{calendar}').year")
                .Should().NotStartWith("RangeError", $"{calendar} year at {iso}");

            Evaluate($"Temporal.PlainDate.from('{iso}').withCalendar('{calendar}').add({{ months: 1 }}).toString()")
                .Should().NotStartWith("RangeError", $"{calendar} add at {iso}");

            Evaluate($"Temporal.PlainDate.from('{iso}').withCalendar('{calendar}').add({{ years: 3, months: 2 }}).toString()")
                .Should().NotStartWith("RangeError", $"{calendar} add years at {iso}");
        }
    }

    /// <summary>
    /// A month added past the end of a table lands on the date the conversions name for the fields it
    /// reports — which is the property that was broken: the arithmetic was reading a different reckoning
    /// from the accessors, or refusing to read one at all.
    /// </summary>
    [TestCaseSource(nameof(PastTheirBackingRangeCases))]
    public void TheDateAMonthAddedLandsOnIsTheDateItsOwnFieldsBuild(string calendar, string iso)
    {
        Evaluate(
            $@"(function () {{
                 var n = Temporal.PlainDate.from('{iso}').withCalendar('{calendar}').add({{ months: 1 }});
                 var built = Temporal.PlainDate.from({{ calendar: '{calendar}', year: n.year, monthCode: n.monthCode, day: n.day }});
                 return n.equals(built);
               }})()")
            .Should().Be("true", $"{calendar} at {iso}");
    }
}
