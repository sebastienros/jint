namespace Jint.Tests.Runtime;

/// <summary>
/// <c>Intl.DurationFormat</c> resolves a numbering system and reports it from <c>resolvedOptions()</c>.
/// https://tc39.es/ecma402/#sec-partitiondurationformatpattern step 4.h.iii.1, and the three numeric-style
/// operations it delegates to, put that system into every <c>NumberFormat</c> the partition constructs — so
/// the digits are the system's, in both lanes and in every style.
/// </summary>
public class IntlDurationFormatTests
{
    private readonly Engine _engine = new();

    [Test]
    public void TheResolvedNumberingSystemIsTheOneFormatWritesIn()
    {
        _engine.Evaluate("new Intl.DurationFormat('en', { numberingSystem: 'arab' }).resolvedOptions().numberingSystem")
            .AsString().Should().Be("arab");
        _engine.Evaluate("new Intl.DurationFormat('en', { numberingSystem: 'arab' }).format({ hours: 12, minutes: 30 })")
            .AsString().Should().Be("١٢ hr, ٣٠ min");
    }

    /// <summary>
    /// The digits come from the system and the unit names do not: a unit name is not a number, and
    /// PartitionDurationFormatPattern only ever hands a value to a NumberFormat.
    /// </summary>
    [Test]
    public void OnlyTheNumbersAreTransliterated()
    {
        var parts = _engine.Evaluate(
            """
            JSON.stringify(new Intl.DurationFormat('en', { numberingSystem: 'arab' })
                .formatToParts({ hours: 12, minutes: 30 }))
            """).AsString();

        parts.Should().Be(
            """
            [{"type":"integer","value":"١٢","unit":"hour"},{"type":"literal","value":" ","unit":"hour"},{"type":"unit","value":"hr","unit":"hour"},{"type":"literal","value":", "},{"type":"integer","value":"٣٠","unit":"minute"},{"type":"literal","value":" ","unit":"minute"},{"type":"unit","value":"min","unit":"minute"}]
            """);
    }

    /// <summary>
    /// The numeric styles construct their own NumberFormats — FormatNumericHours, FormatNumericMinutes and
    /// FormatNumericSeconds — and each is handed the same <c>[[NumberingSystem]]</c>. The fractional second
    /// separator is the system's too.
    /// </summary>
    [Test]
    public void TheNumericStylesWriteTheSameDigits()
    {
        _engine.Evaluate("new Intl.DurationFormat('en', { numberingSystem: 'arab', style: 'digital' }).format({ hours: 1, minutes: 2, seconds: 3 })")
            .AsString().Should().Be("١:٠٢:٠٣");

        _engine.Evaluate(
            """
            new Intl.DurationFormat('en', { numberingSystem: 'arab', style: 'digital', fractionalDigits: 3 })
                .format({ hours: 1, minutes: 2, seconds: 3, milliseconds: 456 })
            """).AsString().Should().Be("١:٠٢:٠٣٫٤٥٦");
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-Intl.DurationFormat.prototype.format steps 4-7 define <c>format</c> as
    /// the concatenation of the very partition <c>formatToParts</c> returns, so the two can never disagree.
    /// Jint writes them down separately — one transliterates the assembled string, the other transliterates
    /// by part type — which is exactly why this is checked rather than assumed.
    /// </summary>
    [TestCase("{ hours: 12, minutes: 30 }", "short")]
    [TestCase("{ years: 1234, months: 2 }", "long")]
    [TestCase("{ hours: 1, minutes: 2, seconds: 3 }", "digital")]
    [TestCase("{ days: 3, hours: 4 }", "narrow")]
    [TestCase("{ hours: -1, minutes: -30 }", "digital")]
    [TestCase("{ hours: -1, minutes: -30 }", "long")]
    [TestCase("{ seconds: 3, milliseconds: 456 }", "digital")]
    [TestCase("{ weeks: 2, days: 1, hours: 5, minutes: 6, seconds: 7 }", "short")]
    public void PartsConcatenateToFormat(string duration, string style)
    {
        foreach (var numberingSystem in new[] { "latn", "arab", "beng", "deva" })
        {
            var script = $$"""
                (function () {
                    var df = new Intl.DurationFormat('en', { numberingSystem: '{{numberingSystem}}', style: '{{style}}' });
                    var d = {{duration}};
                    return df.formatToParts(d).map(function (p) { return p.value; }).join('') === df.format(d);
                })()
                """;

            _engine.Evaluate(script).AsBoolean().Should().BeTrue($"{numberingSystem}/{style}/{duration}");
        }
    }

    /// <summary>The Latin default writes what it always wrote, and pays nothing to find that out.</summary>
    [Test]
    public void TheLatinDefaultIsUnchanged()
    {
        _engine.Evaluate("new Intl.DurationFormat('en').format({ hours: 12, minutes: 30 })")
            .AsString().Should().Be("12 hr, 30 min");
        _engine.Evaluate("new Intl.DurationFormat('en', { numberingSystem: 'latn' }).format({ hours: 12, minutes: 30 })")
            .AsString().Should().Be("12 hr, 30 min");
        _engine.Evaluate("new Intl.DurationFormat('en', { style: 'digital' }).format({ hours: 1, minutes: 2, seconds: 3 })")
            .AsString().Should().Be("1:02:03");
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-getdurationunitoptions steps 2-3: digital's hours, minutes and seconds, a
    /// unit whose own style option is given, and the minutes and seconds after a numeric unit are displayed
    /// even when zero. Everything else defaults to "auto".
    /// </summary>
    [TestCase("{ style: 'digital' }", "{ seconds: 1 }", "0:00:01")]
    [TestCase("{ style: 'digital' }", "{ minutes: 1 }", "0:01:00")]
    [TestCase("{ style: 'digital' }", "{ days: 1 }", "1 day, 0:00:00")]
    [TestCase("{ style: 'digital' }", "{ seconds: -1 }", "-0:00:01")]
    [TestCase("{ style: 'digital' }", "{ seconds: 3, milliseconds: 456 }", "0:00:03.456")]
    [TestCase("{ style: 'digital', hoursDisplay: 'auto' }", "{ seconds: 1 }", "00:01")]
    [TestCase("{ hours: 'numeric' }", "{ hours: 1 }", "1:00:00")]
    [TestCase("{ hours: 'numeric' }", "{ seconds: 1 }", "0:00:01")]
    [TestCase("{ hours: 'long' }", "{ minutes: 1 }", "0 hours, 1 min")]
    [TestCase("{ years: 'long' }", "{ days: 1 }", "0 years, 1 day")]
    [TestCase("{ minutes: 'numeric' }", "{ hours: 1 }", "1 hr, 0:00")]
    [TestCase("{}", "{ hours: 1 }", "1 hr")]
    public void UnitsAreDisplayedByDefaultWhereTheSpecSaysAlways(string options, string duration, string expected)
    {
        _engine.Evaluate($"new Intl.DurationFormat('en', {options}).format({duration})")
            .AsString().Should().Be(expected);
    }

    [Test]
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
    [TestCase("{ milliseconds: 'numeric', millisecondsDisplay: 'always' }")]
    [TestCase("{ style: 'digital', millisecondsDisplay: 'always' }")]
    [TestCase("{ seconds: 'numeric', nanosecondsDisplay: 'always' }")]
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
