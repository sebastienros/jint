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
}
