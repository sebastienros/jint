namespace Jint.Tests.Runtime;

/// <summary>
/// The display defaults GetDurationUnitOptions gives each <c>Intl.DurationFormat</c> unit
/// (https://tc39.es/ecma402/#sec-getdurationunitoptions), and the RangeError for a fractional unit asked to be
/// displayed on its own. Main keeps these in <c>IntlDurationFormatTests</c>, which 4.x does not have.
/// </summary>
public class IntlDurationFormatDisplayDefaultsTests
{
    private readonly Engine _engine = new();

    /// <summary>
    /// https://tc39.es/ecma402/#sec-getdurationunitoptions steps 2-3: digital's hours, minutes and seconds, a
    /// unit whose own style option is given, and the minutes and seconds after a numeric unit are displayed
    /// even when zero. Everything else defaults to "auto".
    /// </summary>
    [Theory]
    [InlineData("{ style: 'digital' }", "{ seconds: 1 }", "0:00:01")]
    [InlineData("{ style: 'digital' }", "{ minutes: 1 }", "0:01:00")]
    [InlineData("{ style: 'digital' }", "{ days: 1 }", "1 day, 0:00:00")]
    [InlineData("{ style: 'digital' }", "{ seconds: -1 }", "-0:00:01")]
    [InlineData("{ style: 'digital' }", "{ seconds: 3, milliseconds: 456 }", "0:00:03.456")]
    [InlineData("{ style: 'digital', hoursDisplay: 'auto' }", "{ seconds: 1 }", "00:01")]
    [InlineData("{ hours: 'numeric' }", "{ hours: 1 }", "1:00:00")]
    [InlineData("{ hours: 'numeric' }", "{ seconds: 1 }", "0:00:01")]
    [InlineData("{ hours: 'long' }", "{ minutes: 1 }", "0 hours, 1 min")]
    [InlineData("{ years: 'long' }", "{ days: 1 }", "0 years, 1 day")]
    [InlineData("{ minutes: 'numeric' }", "{ hours: 1 }", "1 hr, 0:00")]
    [InlineData("{}", "{ hours: 1 }", "1 hr")]
    public void UnitsAreDisplayedByDefaultWhereTheSpecSaysAlways(string options, string duration, string expected)
    {
        _engine.Evaluate($"new Intl.DurationFormat('en', {options}).format({duration})")
            .AsString().Should().Be(expected);
    }

    [Fact]
    public void ResolvedOptionsReportTheDisplayDefaults()
    {
        _engine.Evaluate(
            """
            var o = new Intl.DurationFormat('en', { style: 'digital' }).resolvedOptions();
            [o.daysDisplay, o.hoursDisplay, o.minutesDisplay, o.secondsDisplay, o.millisecondsDisplay].join()
            """).AsString().Should().Be("auto,always,always,always,auto");

        _engine.Evaluate(
            """
            var o = new Intl.DurationFormat('en', { years: 'long', seconds: 'numeric' }).resolvedOptions();
            [o.yearsDisplay, o.monthsDisplay, o.hoursDisplay, o.secondsDisplay, o.millisecondsDisplay].join()
            """).AsString().Should().Be("always,auto,auto,always,auto");
    }

    /// <summary>
    /// A numeric sub-second unit is "fractional" (GetDurationUnitOptions step 4), and ValidateDurationUnitStyle
    /// step 1 rejects a fractional unit that is asked to be displayed on its own.
    /// </summary>
    [Theory]
    [InlineData("{ milliseconds: 'numeric', millisecondsDisplay: 'always' }")]
    [InlineData("{ style: 'digital', millisecondsDisplay: 'always' }")]
    [InlineData("{ seconds: 'numeric', nanosecondsDisplay: 'always' }")]
    public void AFractionalUnitCannotBeDisplayedAlways(string options)
    {
        _engine.Evaluate(
            $$"""
            (function () {
                try { new Intl.DurationFormat('en', {{options}}); return 'no error'; }
                catch (e) { return e.name; }
            })()
            """).AsString().Should().Be("RangeError");
    }
}
