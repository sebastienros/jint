#nullable enable

namespace Jint.Tests.Runtime;

/// <summary>
/// Pins the two Temporal defects the test262 bump to 2e58f87d exposed, on the entry points test262 itself
/// does not reach: <c>PlainMonthDay.prototype.with</c> reads its year through the same lane as
/// <c>PlainMonthDay.from</c>, and every Temporal method taking a duration string shares one parser.
/// </summary>
public class TemporalTest262BumpRegressionTests
{
    // An iso8601 PlainMonthDay uses the year only to apply overflow, so any integral year is accepted and
    // only its leap-year status matters (https://tc39.es/proposal-temporal/#sec-temporal.plainmonthday.from,
    // https://tc39.es/proposal-temporal/#sec-temporal.plainmonthday.prototype.with). Years past int.MaxValue
    // used to saturate, which made every one of them a common year.
    [TestCase("1000000000000004", 29)]
    [TestCase("1000000000000005", 28)]
    [TestCase("1000000000000000", 29)]
    [TestCase("1000000000000100", 28)]
    [TestCase("-1000000000000004", 29)]
    [TestCase("-1000000000000100", 28)]
    [TestCase("2 ** 60", 29)]
    public void PlainMonthDayFromUsesTheExactYearForLeapStatus(string year, int expectedDay)
    {
        var engine = new Engine();
        engine.Evaluate($"Temporal.PlainMonthDay.from({{ year: {year}, monthCode: 'M02', day: 29 }}).day")
            .AsNumber().Should().Be(expectedDay);
    }

    [TestCase("1000000000000004", 29)]
    [TestCase("1000000000000005", 28)]
    [TestCase("-1000000000000004", 29)]
    [TestCase("-1000000000000100", 28)]
    public void PlainMonthDayWithUsesTheExactYearForLeapStatus(string year, int expectedDay)
    {
        var engine = new Engine();
        engine.Evaluate($"Temporal.PlainMonthDay.from('03-01').with({{ year: {year}, monthCode: 'M02', day: 29 }}).day")
            .AsNumber().Should().Be(expectedDay);
    }

    [Test]
    public void PlainMonthDayWithRejectsFebruary29InAHugeCommonYear()
    {
        var engine = new Engine();
        engine.Evaluate("""
            try {
              Temporal.PlainMonthDay.from('03-01').with({ year: 1000000000000005, monthCode: 'M02', day: 29 }, { overflow: 'reject' });
              'no exception';
            } catch (e) { e.constructor.name; }
            """).AsString().Should().Be("RangeError");
    }

    // A time designator must be followed by at least one time component
    // (https://tc39.es/proposal-temporal/#sec-temporal-totemporalduration, which parses with the
    // TemporalDurationString grammar: DurationTimeDesignator TimeDuration, never a bare "T").
    [TestCase("Temporal.Duration.from('P1DT')")]
    [TestCase("Temporal.Duration.from('P1YT')")]
    [TestCase("Temporal.Duration.from('-P1Y1M1W1DT')")]
    [TestCase("Temporal.PlainDate.from('2020-01-01').add('P1DT')")]
    [TestCase("Temporal.Duration.compare('P1DT', 'P1D')")]
    public void ADanglingTimeDesignatorIsRejected(string expression)
    {
        var engine = new Engine();
        engine.Evaluate($"try {{ {expression}; 'no exception'; }} catch (e) {{ e.constructor.name; }}")
            .AsString().Should().Be("RangeError");
    }

    [TestCase("P1DT1H", "P1DT1H")]
    [TestCase("PT1M", "PT1M")]
    [TestCase("P1D", "P1D")]
    public void ATimeDesignatorWithAComponentStillParses(string input, string expected)
    {
        var engine = new Engine();
        engine.Evaluate($"Temporal.Duration.from('{input}').toString()").AsString().Should().Be(expected);
    }
}
