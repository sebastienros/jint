#nullable enable

namespace Jint.Tests.Runtime;

/// <summary>
/// Jint reckons the eleven non-ISO calendars with <c>System.Globalization.Calendar</c> classes and with
/// fixed-epoch arithmetic, and several of those classes cover far less than Temporal's own date range —
/// <c>ChineseLunisolarCalendar</c> spans ISO 1901-02-19 to 2101-01-28. These pin what happens at the end
/// of one: a bounded walk that terminates, and an answer reckoned in the calendar rather than the
/// calendar's own boundary date handed back as though it were one.
/// </summary>
/// <remarks>
/// The failure these replace is the worst one an engine has. <c>CalendarDateUntil</c> walks a month at a
/// time towards its target and stops when a step passes it; a conversion that answered with the
/// calendar's boundary date made every further step land on that same date, so no step ever passed and
/// the loop had no other exit. It is a CLR loop inside one interpreter step, so it crosses no statement
/// boundary and no execution constraint can interrupt it: <c>LimitStatements</c> never counts,
/// <c>LimitExecutionTime</c> never gets a check, and a <c>CancellationToken</c> is never observed. That
/// is why every case here runs on a dedicated thread with a join timeout — a regression has to fail the
/// run rather than wedge it. See <see href="https://github.com/sebastienros/jint/issues/3428"/>.
/// <para>
/// What ends the walk is no longer a refusal. <see href="https://github.com/sebastienros/jint/issues/3483"/>
/// pointed out that the field accessors reckon the same dates the arithmetic was refusing, so a step past
/// the end of a table is now placed by the same reckoning the accessors read it with. Termination
/// survives because that reckoning is strictly monotone in (year, ordinal month) and declines outright
/// past Temporal's own range; the no-progress guard stays as the structural guarantee for one that
/// saturates instead.
/// </para>
/// </remarks>
public class NonIsoCalendarRangeTests
{
    /// <summary>
    /// Generous next to the milliseconds every case below actually takes, and unreachable for a walk
    /// that does not terminate at all — which is the only distinction this has to make.
    /// </summary>
    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(15);

    private static readonly string[] TheElevenNonIsoCalendars =
    [
        "chinese", "dangi", "hebrew", "persian",
        "coptic", "ethiopic", "ethioaa", "indian",
        "islamic-umalqura", "islamic-civil", "islamic-tbla",
    ];

    /// <summary>
    /// Runs <paramref name="body"/> on a dedicated thread, failing the test if it has not finished inside
    /// <see cref="Budget"/>. Nothing in this fixture may evaluate on the runner's own thread: the defect
    /// under test is a non-terminating one, so a regression evaluated inline wedges the whole run instead
    /// of reporting it.
    /// </summary>
    private static void Guarded(string what, Action body) => DedicatedThread.Run(
        body,
        joinTimeout: Budget,
        timeoutMessage: $"did not finish within {Budget}: {what}",
        maxStackSize: DedicatedThread.DefaultStackSize);

    /// <summary>
    /// Evaluates <paramref name="expression"/>, answering with its string value or with the constructor
    /// name of the JavaScript error it raised.
    /// </summary>
    private static string Evaluate(string expression)
    {
        var result = "";

        Guarded(expression, () =>
        {
            var engine = new Engine();
            result = engine.Evaluate(
                $"(function () {{ try {{ return String({expression}); }} catch (e) {{ return e.constructor.name; }} }})()").AsString();
        });

        return result;
    }

    /// <summary>
    /// The report from the issue, verbatim. Before the fix this never returned.
    /// </summary>
    [Test]
    public void TheReportedMonthDifferencePastTheChineseRangeTerminates()
    {
        Evaluate(
            "Temporal.PlainDate.from('1910-01-01').withCalendar('chinese')" +
            ".until(Temporal.PlainDate.from('1900-01-03').withCalendar('chinese'), { largestUnit: 'year' })")
            .Should().Be("-P9Y12M17D");
    }

    /// <summary>
    /// A difference is a sequence of additions, so every direction of walk and every largest unit that
    /// walks has to terminate — not only the one the report happened to name — and the difference has to
    /// be the one that takes the one date to the other, which is what says the walk stopped in the right
    /// place rather than merely stopping.
    /// </summary>
    [TestCase("chinese", "1910-01-01", "1900-01-03", TestName = "chinese below its 1901 floor")]
    [TestCase("chinese", "2050-01-01", "2300-01-03", TestName = "chinese above its 2101 ceiling")]
    [TestCase("dangi", "2050-01-01", "2300-01-03", TestName = "dangi above its 2051 ceiling")]
    [TestCase("hebrew", "1990-01-01", "1000-01-03", TestName = "hebrew below its 1583 floor")]
    [TestCase("hebrew", "2050-01-01", "2300-01-03", TestName = "hebrew above its 2239 ceiling")]
    [TestCase("persian", "0700-01-01", "0300-01-03", TestName = "persian below its 622 floor")]
    public void ADifferencePastACalendarRangeTerminatesAndTakesTheOneDateToTheOther(string calendar, string one, string two)
    {
        foreach (var largestUnit in new[] { "year", "month" })
        {
            foreach (var op in new[] { "until", "since" })
            {
                // Both differences are reckoned from the receiver, so each is undone on the receiver:
                // "a.until(b)" added to a is b, and "a.since(b)" subtracted from a is b.
                var replay = string.Equals(op, "until", StringComparison.Ordinal) ? "a.add(d).equals(b)" : "a.subtract(d).equals(b)";

                Evaluate(
                    $@"(function () {{
                         var a = Temporal.PlainDate.from('{one}').withCalendar('{calendar}');
                         var b = Temporal.PlainDate.from('{two}').withCalendar('{calendar}');
                         var d = a.{op}(b, {{ largestUnit: '{largestUnit}' }});
                         return {replay};
                       }})()")
                    .Should().Be("true", $"{calendar}.{op} by {largestUnit}");
            }
        }
    }

    /// <summary>
    /// The same walk backs <c>until</c> and <c>since</c> on every type that carries a calendar, and
    /// <c>Duration</c>'s <c>round</c>/<c>total</c> reach it through <c>relativeTo</c>. Each of these was
    /// its own way to lose the thread, so each is named rather than left to the one the report used.
    /// </summary>
    [Test]
    public void EveryDifferenceSurfaceThatWalksMonthsIsBounded()
    {
        const string Chinese = "withCalendar('chinese')";

        Evaluate($"Temporal.PlainDateTime.from('1910-01-01T00:00').{Chinese}.until(Temporal.PlainDateTime.from('1900-01-03T00:00').{Chinese}, {{ largestUnit: 'year' }})")
            .Should().StartWith("-P");

        Evaluate($"Temporal.ZonedDateTime.from('1910-01-01T00:00[UTC]').{Chinese}.since(Temporal.ZonedDateTime.from('1900-01-03T00:00[UTC]').{Chinese}, {{ largestUnit: 'year' }})")
            .Should().StartWith("P");

        Evaluate($"Temporal.PlainDate.from('1910-01-01').{Chinese}.toPlainYearMonth().until(Temporal.PlainDate.from('1900-01-03').{Chinese}.toPlainYearMonth(), {{ largestUnit: 'year' }})")
            .Should().StartWith("-P");

        Evaluate($"new Temporal.Duration(0, 0, 0, 200000).total({{ unit: 'year', relativeTo: Temporal.PlainDate.from('1990-01-01').{Chinese} }})")
            .Should().NotBe("RangeError");

        // round reaches the walk through a ZonedDateTime relativeTo. It does not reach it through a
        // PlainDate one, because that arm reckons in "iso8601" whatever calendar the relativeTo carries
        // -- a separate defect with its own issue.
        Evaluate($"new Temporal.Duration(200).round({{ largestUnit: 'year', relativeTo: Temporal.ZonedDateTime.from('1990-01-01T00:00[UTC]').{Chinese} }})")
            .Should().StartWith("P");
    }

    /// <summary>
    /// The other half of the same defect, and the one that answered rather than hanging: the boundary
    /// date handed back was the calendar's <em>maximum</em> whichever end had been overrun, so
    /// subtracting a century from a 1950 Chinese date moved it a century and a half forward, to
    /// 2101-01-28, and reported success. It is now the date a century back, placed by the same
    /// conversion that reads that date's fields.
    /// </summary>
    [TestCase("chinese", "1950-01-01", "subtract({ years: 100 })", "1849-12-26")]
    [TestCase("chinese", "2050-01-01", "add({ years: 100 })", "2150-01-06")]
    [TestCase("chinese", "2050-01-01", "add({ months: 1200 })", "2147-01-09")]
    [TestCase("dangi", "2050-01-01", "add({ years: 100 })", "2150-01-06")]
    [TestCase("hebrew", "2050-01-01", "add({ years: 500 })", "2549-12-29")]
    [TestCase("persian", "0700-01-01", "subtract({ years: 200 })", "0500-01-01")]
    public void ArithmeticPastACalendarRangeAnswersInTheCalendarInsteadOfWithItsBoundary(string calendar, string from, string call, string expected)
    {
        Evaluate($"Temporal.PlainDate.from('{from}').withCalendar('{calendar}').{call}")
            .Should().Be($"{expected}[u-ca={calendar}]");
    }

    /// <summary>
    /// Temporal's own range still ends somewhere, and past it the refusal is all that is left. It is only
    /// an improvement on a hang if a script can act on it, which means it has to be a JavaScript error
    /// and not a CLR exception on its way out of <c>Engine.Evaluate</c>.
    /// </summary>
    [Test]
    public void TheRefusalPastTemporalsOwnRangeIsAJavaScriptErrorAScriptCanCatch()
    {
        var caught = "";

        Guarded(nameof(TheRefusalPastTemporalsOwnRangeIsAJavaScriptErrorAScriptCanCatch), () => caught = new Engine().Evaluate(
            @"(function () {
                try {
                    Temporal.PlainDate.from('2000-01-01').withCalendar('chinese').add({ years: 300000 });
                    return 'no error';
                } catch (e) {
                    return (e instanceof RangeError) + '|' + (e.message.length > 0);
                }
            })()").AsString());

        caught.Should().Be("true|true");
    }

    /// <summary>
    /// The end of a table is not a new ceiling on ordinary arithmetic: every calendar still measures and
    /// still adds inside the range it supports, and the two still agree with each other.
    /// </summary>
    [Test]
    public void ArithmeticInsideEveryCalendarRangeIsUnchanged()
    {
        foreach (var calendar in TheElevenNonIsoCalendars)
        {
            Evaluate($"Temporal.PlainDate.from('1990-03-05').withCalendar('{calendar}').until(Temporal.PlainDate.from('2020-07-11').withCalendar('{calendar}'), {{ largestUnit: 'year' }})")
                .Should().NotBe("RangeError", calendar);

            Evaluate($"Temporal.PlainDate.from('1990-03-05').withCalendar('{calendar}').add({{ years: 5, months: 3, days: 2 }})")
                .Should().NotBe("RangeError", calendar);

            // add and until are the same reckoning read in opposite directions, so a date plus the
            // difference to another date is that other date.
            Evaluate(
                $@"(function () {{
                     var a = Temporal.PlainDate.from('1990-03-05').withCalendar('{calendar}');
                     var b = Temporal.PlainDate.from('2020-07-11').withCalendar('{calendar}');
                     return a.add(a.until(b, {{ largestUnit: 'year' }})).equals(b);
                   }})()")
                .Should().Be("true", calendar);
        }
    }

    /// <summary>
    /// Every one of the eleven measures across the whole of Temporal's range. Six never had a
    /// <c>System.Globalization.Calendar</c> to run out of; the other five reckon past the end of theirs.
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
    public void EveryNonIsoCalendarMeasuresAcrossTheWholeTemporalRange(string calendar)
    {
        Evaluate($"Temporal.PlainDate.from('1950-01-01').withCalendar('{calendar}').until(Temporal.PlainDate.from('-100000-01-03').withCalendar('{calendar}'), {{ largestUnit: 'year' }})")
            .Should().StartWith("-P", calendar);
    }
}
