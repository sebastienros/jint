#nullable enable

namespace Jint.Tests.Runtime;

/// <summary>
/// <c>Intl.NumberFormat</c>'s two lanes are one algorithm.
/// https://tc39.es/ecma402/#sec-formatnumeric defines <c>format</c> as the concatenation of exactly the
/// parts https://tc39.es/ecma402/#sec-formatnumerictoparts returns, both of them
/// https://tc39.es/ecma402/#sec-partitionnumberpattern, so the digits, the separators and the pattern text
/// are the same characters read two ways — including when <c>[[NumberingSystem]]</c> is not Latin.
/// </summary>
/// <remarks>
/// <see cref="PartsConcatenateToFormat"/> asserts the join outright, in every numbering system and in
/// <c>latn</c> alike. The other two grids compare each numbering system against <c>latn</c> instead, which
/// pins the narrower claim that <b>asking for a numbering system introduces no disagreement</b> and skips a
/// combination whose Latin lanes already differ rather than silently blessing it. One such combination
/// exists today and it belongs to the range lane: <c>formatRange</c> of a currency writes the symbol once
/// where its parts lane repeats it per endpoint.
/// </remarks>
public class IntlNumberFormatPartsTests
{
    private readonly Engine _engine = new();

    private static readonly string[] NumberingSystems =
    [
        "arab", "arabext", "beng", "deva", "thai", "hanidec", "fullwide",
        // ten digits outside the BMP, so a surrogate pair has to survive the rewrite
        "mathbold"
    ];

    private static readonly string[] Locales = ["en", "en-US", "de-DE", "fr-FR", "pt-PT", "ar-EG", "ja-JP"];

    private static readonly string[] OptionSets =
    [
        "{}",
        "{ minimumFractionDigits: 2 }",
        "{ useGrouping: false }",
        "{ signDisplay: 'always' }",
        "{ style: 'percent' }",
        "{ style: 'currency', currency: 'USD' }",
        "{ style: 'currency', currency: 'EUR', currencyDisplay: 'name' }",
        "{ style: 'unit', unit: 'meter' }",
        "{ style: 'unit', unit: 'kilometer-per-hour', unitDisplay: 'long' }",
        "{ notation: 'scientific' }",
        "{ notation: 'engineering' }",
        "{ notation: 'compact' }",
        "{ notation: 'compact', compactDisplay: 'long' }"
    ];

    private const string Values =
        "[0, 1, -1, 0.5, 1234.5, -1234.5, 1234567.891, 12345678901234, NaN, Infinity, -Infinity]";

    /// <summary>
    /// The defect: <c>format</c> transliterated and <c>formatToParts</c> did not, so
    /// <c>new Intl.NumberFormat('en', { numberingSystem: 'arab' })</c> wrote <c>"١,٢٣٤٫٥"</c> from one lane
    /// and <c>"1,234.5"</c> from the other. The grid asserts the join outright, so it also stands over the
    /// four disagreements that had nothing to do with digits — a unit pattern's prefix, a currency's sign,
    /// scientific notation of zero, and a non-finite value's style.
    /// </summary>
    [Test]
    public void PartsConcatenateToFormat()
    {
        AssertNoMismatch(
            $$"""
            (function () {
                var bad = [];
                var locales = {{JsArrayOf(Locales)}};
                var systems = {{JsArrayOf(NumberingSystems)}};
                var optionSets = [{{string.Join(", ", OptionSets)}}];
                var values = {{Values}};
                function join(parts) { return parts.map(function (p) { return p.value; }).join(''); }
                function check(nf, system, locale, o, value) {
                    var joined = join(nf.formatToParts(value));
                    var formatted = nf.format(value);
                    if (joined !== formatted) {
                        bad.push(locale + '/' + system + '/' + o + '/' + value
                            + ': format=' + formatted + ' parts=' + joined);
                    }
                }
                for (var l = 0; l < locales.length; l++) {
                    for (var o = 0; o < optionSets.length; o++) {
                        var latin = new Intl.NumberFormat(locales[l], optionSets[o]);
                        for (var v = 0; v < values.length; v++) {
                            check(latin, 'latn', locales[l], o, values[v]);
                        }
                        for (var s = 0; s < systems.length; s++) {
                            var options = Object.assign({ numberingSystem: systems[s] }, optionSets[o]);
                            var nf = new Intl.NumberFormat(locales[l], options);
                            for (var v = 0; v < values.length; v++) {
                                check(nf, systems[s], locales[l], o, values[v]);
                            }
                        }
                    }
                }
                return JSON.stringify(bad);
            })()
            """);
    }

    /// <summary>
    /// The range lanes are the same operation twice, plus the literal between them.
    /// https://tc39.es/ecma402/#sec-formatnumericrange is the concatenation of
    /// https://tc39.es/ecma402/#sec-formatnumericrangetoparts, so a range that is not collapsed has to join
    /// back to the string.
    /// </summary>
    [Test]
    public void RangePartsConcatenateToFormatRange()
    {
        AssertNoMismatch(
            $$"""
            (function () {
                var bad = [];
                var locales = {{JsArrayOf(Locales)}};
                var systems = {{JsArrayOf(NumberingSystems)}};
                var optionSets = [{}, { minimumFractionDigits: 2 }, { style: 'percent' },
                                  { style: 'unit', unit: 'meter' }, { notation: 'compact' },
                                  { style: 'currency', currency: 'USD' }];
                var pairs = [[1, 5], [3, 3], [-5, -1], [0, 1000000], [1234.5, 2345.6]];
                function join(parts) { return parts.map(function (p) { return p.value; }).join(''); }
                for (var l = 0; l < locales.length; l++) {
                    for (var o = 0; o < optionSets.length; o++) {
                        var latin = new Intl.NumberFormat(locales[l], optionSets[o]);
                        for (var s = 0; s < systems.length; s++) {
                            var options = Object.assign({ numberingSystem: systems[s] }, optionSets[o]);
                            var nf = new Intl.NumberFormat(locales[l], options);
                            for (var p = 0; p < pairs.length; p++) {
                                var a = pairs[p][0], b = pairs[p][1];
                                if (join(latin.formatRangeToParts(a, b)) !== latin.formatRange(a, b)) {
                                    continue;
                                }
                                var joined = join(nf.formatRangeToParts(a, b));
                                var formatted = nf.formatRange(a, b);
                                if (joined !== formatted) {
                                    bad.push(locales[l] + '/' + systems[s] + '/' + o + '/' + pairs[p]
                                        + ': formatRange=' + formatted + ' parts=' + joined);
                                }
                            }
                        }
                    }
                }
                return JSON.stringify(bad);
            })()
            """);
    }

    /// <summary>
    /// <c>Intl.RelativeTimeFormat.prototype.formatToParts</c> copies <c>NumberFormat</c>'s parts
    /// (https://tc39.es/ecma402/#sec-partitionrelativetimepattern step 8.b), so it inherited whichever digits
    /// that lane wrote — and its own <c>format</c> transliterated the assembled string, pattern text and all.
    /// </summary>
    [Test]
    public void RelativeTimePartsConcatenateToFormat()
    {
        AssertNoMismatch(
            $$"""
            (function () {
                var bad = [];
                var locales = {{JsArrayOf(Locales)}};
                var systems = {{JsArrayOf(NumberingSystems)}};
                var styles = ['long', 'short', 'narrow'];
                var numerics = ['always', 'auto'];
                var units = ['second', 'minute', 'hour', 'day', 'week', 'month', 'quarter', 'year'];
                var values = [0, 1, -1, 3, -3, 1234.5];
                function join(parts) { return parts.map(function (p) { return p.value; }).join(''); }
                for (var l = 0; l < locales.length; l++) {
                    for (var st = 0; st < styles.length; st++) {
                        for (var n = 0; n < numerics.length; n++) {
                            var latin = new Intl.RelativeTimeFormat(locales[l],
                                { style: styles[st], numeric: numerics[n] });
                            for (var s = 0; s < systems.length; s++) {
                                var rtf = new Intl.RelativeTimeFormat(locales[l],
                                    { numberingSystem: systems[s], style: styles[st], numeric: numerics[n] });
                                for (var u = 0; u < units.length; u++) {
                                    for (var v = 0; v < values.length; v++) {
                                        if (join(latin.formatToParts(values[v], units[u]))
                                            !== latin.format(values[v], units[u])) {
                                            continue;
                                        }
                                        var joined = join(rtf.formatToParts(values[v], units[u]));
                                        var formatted = rtf.format(values[v], units[u]);
                                        if (joined !== formatted) {
                                            bad.push(locales[l] + '/' + systems[s] + '/' + styles[st] + '/'
                                                + numerics[n] + '/' + units[u] + '/' + values[v]
                                                + ': format=' + formatted + ' parts=' + joined);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                return JSON.stringify(bad);
            })()
            """);
    }

    /// <summary>The reported issue, verbatim: the parts lane wrote Latin digits the string lane never did.</summary>
    [Test]
    public void TheResolvedNumberingSystemReachesTheParts()
    {
        _engine.Evaluate("new Intl.NumberFormat('en', { numberingSystem: 'arab' }).format(1234.5)")
            .AsString().Should().Be("١,٢٣٤٫٥");
        _engine.Evaluate(
            "new Intl.NumberFormat('en', { numberingSystem: 'arab' }).formatToParts(1234.5).map(function (p) { return p.value; }).join('')")
            .AsString().Should().Be("١,٢٣٤٫٥");

        _engine.Evaluate("new Intl.RelativeTimeFormat('en', { numberingSystem: 'arab' }).format(3, 'day')")
            .AsString().Should().Be("in ٣ days");
        _engine.Evaluate(
            "new Intl.RelativeTimeFormat('en', { numberingSystem: 'arab' }).formatToParts(3, 'day').map(function (p) { return p.value; }).join('')")
            .AsString().Should().Be("in ٣ days");
    }

    /// <summary>
    /// A currency symbol, a unit name and a literal are pattern text, and pattern text is not a number.
    /// The full stop of a <c>style: "short"</c> abbreviation is the case that made
    /// <c>Intl.RelativeTimeFormat</c> write <c>"in ٣ sec٫"</c>, where <c>٫</c> is U+066B, the Arabic decimal
    /// separator that only a decimal point should ever have become.
    /// </summary>
    [Test]
    public void OnlyTheNumberIsTransliterated()
    {
        _engine.Evaluate("new Intl.RelativeTimeFormat('en', { style: 'short', numberingSystem: 'arab' }).format(3, 'second')")
            .AsString().Should().Be("in ٣ sec.");
        _engine.Evaluate("new Intl.RelativeTimeFormat('en', { style: 'short', numberingSystem: 'arab' }).format(3, 'minute')")
            .AsString().Should().Be("in ٣ min.");

        _engine.Evaluate(
            """
            JSON.stringify(new Intl.NumberFormat('en', { numberingSystem: 'arab', style: 'unit', unit: 'meter' }).formatToParts(1.5))
            """)
            .AsString().Should().Be(
                """
                [{"type":"integer","value":"١"},{"type":"decimal","value":"٫"},{"type":"fraction","value":"٥"},{"type":"literal","value":" "},{"type":"unit","value":"m"}]
                """);
    }

    /// <summary>
    /// A full stop a locale does not use as its decimal separator is pattern text like any other.
    /// German writes <c>1,2 Mio.</c>, and the comma is the number while the full stop is the abbreviation's.
    /// </summary>
    [Test]
    public void ALocaleWhoseSeparatorIsACommaKeepsItsFullStops()
    {
        // the space between the two is .NET's, and is a non-breaking one on some platforms
        var compact = _engine.Evaluate(
            "new Intl.NumberFormat('de-DE', { numberingSystem: 'arab', notation: 'compact' }).format(1234567.891)").AsString();
        compact.Should().StartWith("١٫٢").And.EndWith("Mio.");
        _engine.Evaluate("new Intl.NumberFormat('de-DE', { numberingSystem: 'arab' }).format(1234.5)")
            .AsString().Should().Be("١.٢٣٤٫٥");
    }

    /// <summary>
    /// The separator between two range endpoints is the one the string lane writes: the locale's tight form
    /// for a plain number, its spaced form for a currency. test262's
    /// <c>NumberFormat/prototype/formatRange/en-US.js</c> pins <c>"987,654,321–987,654,322"</c> against
    /// <c>formatRangeToParts/en-US.js</c>'s <c>{ type: "literal", value: " – " }</c> for a currency.
    /// </summary>
    [Test]
    public void TheRangeSeparatorIsTheOneFormatRangeWrites()
    {
        _engine.Evaluate("new Intl.NumberFormat('en-US').formatRange(1, 5)").AsString().Should().Be("1–5");
        _engine.Evaluate(
            "new Intl.NumberFormat('en-US').formatRangeToParts(1, 5).map(function (p) { return p.value; }).join('')")
            .AsString().Should().Be("1–5");

        _engine.Evaluate(
            "new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD', maximumFractionDigits: 0 }).formatRange(3, 5)")
            .AsString().Should().Be("$3 – $5");
        _engine.Evaluate(
            """
            new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD', maximumFractionDigits: 0 })
                .formatRangeToParts(3, 5).map(function (p) { return p.value; }).join('')
            """)
            .AsString().Should().Be("$3 – $5");
    }

    /// <summary>An engine that asks for no numbering system writes exactly what it always wrote.</summary>
    [Test]
    public void TheLatinDefaultIsUnchanged()
    {
        _engine.Evaluate("new Intl.NumberFormat('en').formatToParts(1234.5).map(function (p) { return p.value; }).join('')")
            .AsString().Should().Be("1,234.5");
        _engine.Evaluate("new Intl.NumberFormat('en').format(1234.5)").AsString().Should().Be("1,234.5");
        _engine.Evaluate("new Intl.RelativeTimeFormat('en', { style: 'short' }).format(3, 'second')")
            .AsString().Should().Be("in 3 sec.");
        _engine.Evaluate("new Intl.RelativeTimeFormat('en', { numeric: 'auto' }).format(-1, 'day')")
            .AsString().Should().Be("yesterday");
    }

    /// <summary>
    /// A non-finite value chooses only the number's own text.
    /// https://tc39.es/ecma402/#sec-partitionnumberpattern's NaN and infinity branches set nothing but
    /// <c>formattedString</c>, so the pattern https://tc39.es/ecma402/#sec-getnumberformatpattern selects is
    /// still walked and the currency symbol, the unit and the percent sign are still written around it.
    /// </summary>
    [Test]
    public void ANonFiniteValueKeepsTheStyleAroundIt()
    {
        const string Usd = "new Intl.NumberFormat('en', { style: 'currency', currency: 'USD' })";
        Format(Usd, "NaN").Should().Be("$NaN");
        Format(Usd, "Infinity").Should().Be("$∞");
        Format(Usd, "-Infinity").Should().Be("-$∞");

        const string Percent = "new Intl.NumberFormat('en', { style: 'percent' })";
        Format(Percent, "NaN").Should().Be("NaN%");
        Format(Percent, "-Infinity").Should().Be("-∞%");

        const string Meter = "new Intl.NumberFormat('en', { style: 'unit', unit: 'meter' })";
        Format(Meter, "NaN").Should().Be("NaN m");
        Format(Meter, "Infinity").Should().Be("∞ m");

        // de-DE writes the number, a non-breaking space and the symbol, and NaN takes the same pattern
        // every non-negative value takes
        Format("new Intl.NumberFormat('de-DE', { style: 'currency', currency: 'EUR' })", "NaN")
            .Should().Be("NaN €");
    }

    /// <summary>
    /// The parts lane reports the pattern the string lane writes, one part per pattern part:
    /// https://tc39.es/ecma402/#sec-partitionnotationsubpattern makes the number itself exactly one
    /// <c>nan</c> or <c>infinity</c> part, and everything else around it belongs to the style.
    /// </summary>
    [Test]
    public void ANonFiniteValuesPartsCarryTheStyleToo()
    {
        const string Usd = "new Intl.NumberFormat('en', { style: 'currency', currency: 'USD' })";
        Parts(Usd, "NaN").Should().Be("""[{"type":"currency","value":"$"},{"type":"nan","value":"NaN"}]""");
        Parts(Usd, "-Infinity").Should().Be(
            """[{"type":"minusSign","value":"-"},{"type":"currency","value":"$"},{"type":"infinity","value":"∞"}]""");

        Parts("new Intl.NumberFormat('en', { style: 'percent' })", "NaN")
            .Should().Be("""[{"type":"nan","value":"NaN"},{"type":"percentSign","value":"%"}]""");

        Parts("new Intl.NumberFormat('en', { style: 'unit', unit: 'meter' })", "NaN")
            .Should().Be(
                """[{"type":"nan","value":"NaN"},{"type":"literal","value":" "},{"type":"unit","value":"m"}]""");
    }

    /// <summary>
    /// <c>currencySign: "accounting"</c> takes the negative pattern for -∞ as it does for any other negative
    /// value, and the two lanes agree on which one that is.
    /// </summary>
    [Test]
    public void AccountingWrapsANonFiniteValueTheWayItWrapsAFiniteOne()
    {
        const string Accounting =
            "new Intl.NumberFormat('en', { style: 'currency', currency: 'USD', currencySign: 'accounting' })";
        Format(Accounting, "-5").Should().Be("($5.00)");
        Format(Accounting, "-Infinity").Should().Be("($∞)");
        Join(Accounting, "-Infinity").Should().Be("($∞)");
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-partitionnotationsubpattern gives a non-finite value one part and no
    /// notation sub-pattern, so no exponent is ever written for it — which is what test262's
    /// <c>engineering-scientific-en-US.js</c> asserts.
    /// </summary>
    [Test]
    public void NotationAddsNoExponentToANonFiniteValue()
    {
        const string Scientific = "new Intl.NumberFormat('en', { notation: 'scientific' })";
        Format(Scientific, "NaN").Should().Be("NaN");
        Format(Scientific, "Infinity").Should().Be("∞");
        Format(Scientific, "-Infinity").Should().Be("-∞");
        Join(Scientific, "-Infinity").Should().Be("-∞");
    }

    /// <summary>
    /// Zero is a finite value like any other, and
    /// https://tc39.es/ecma402/#sec-partitionnotationsubpattern writes <c>"0"</c> for an exponent of zero
    /// rather than dropping the exponent. The parts lane already did; the string lane fell back to plain
    /// decimal formatting.
    /// </summary>
    [Test]
    public void ScientificNotationOfZeroStillHasAnExponent()
    {
        const string Scientific = "new Intl.NumberFormat('en', { notation: 'scientific' })";
        Format(Scientific, "0").Should().Be("0E0");
        Format("new Intl.NumberFormat('en', { notation: 'engineering' })", "0").Should().Be("0E0");
        Format(Scientific, "-0").Should().Be("-0E0");
        Join(Scientific, "-0").Should().Be("-0E0");
        Format(Scientific, "5").Should().Be("5E0");
    }

    /// <summary>
    /// ECMA-402 calls the sign "the ILND String representing the minus sign", so it is the locale's own
    /// datum in both lanes: <c>ar</c> prefixes U+061C ARABIC LETTER MARK, which the string lane wrote for a
    /// plain number and not for a currency.
    /// </summary>
    /// <remarks>
    /// The sign itself is read back out of the formatter rather than written here, because it is locale data
    /// and the two frameworks do not carry the same: .NET gives <c>ar</c> the directionality mark, .NET
    /// Framework a plain hyphen.
    /// </remarks>
    [Test]
    public void TheCurrencySignIsTheOneTheLocaleWrites()
    {
        var minusSign = PartValue("new Intl.NumberFormat('ar-EG')", "-5", "minusSign");
        var plusSign = PartValue("new Intl.NumberFormat('ar-EG', { signDisplay: 'always' })", "5", "plusSign");

        const string Egp = "new Intl.NumberFormat('ar-EG', { style: 'currency', currency: 'EGP' })";
        Format(Egp, "-5").Should().StartWith(minusSign).And.Be(Join(Egp, "-5"));

        const string Always =
            "new Intl.NumberFormat('ar-EG', { style: 'currency', currency: 'EGP', signDisplay: 'always' })";
        Format(Always, "5").Should().StartWith(plusSign).And.Be(Join(Always, "5"));

        // ar has no accounting parentheses, so accounting falls through to the same negative pattern
        const string Accounting =
            "new Intl.NumberFormat('ar-EG', { style: 'currency', currency: 'EGP', currencySign: 'accounting' })";
        Format(Accounting, "-5").Should().StartWith(minusSign).And.Be(Join(Accounting, "-5"));
    }

    /// <summary>
    /// A CLDR unit pattern can put text on both sides of the number, and
    /// https://tc39.es/ecma402/#sec-partitionnumberpattern's <c>unitPrefix</c> branch appends a <c>unit</c>
    /// part for the leading one. test262's <c>formatToParts/unit-ja-JP.js</c> builds its expectation from
    /// that part, and <c>format/unit-ja-JP.js</c> reads the separator back out of the same list.
    /// </summary>
    [Test]
    public void AUnitPatternsPrefixIsAPartOfItsOwn()
    {
        const string Japanese =
            "new Intl.NumberFormat('ja-JP', { style: 'unit', unit: 'kilometer-per-hour', unitDisplay: 'long' })";
        Format(Japanese, "1").Should().Be("時速 1 キロメートル");
        Parts(Japanese, "1").Should().Be(
            """
            [{"type":"unit","value":"時速"},{"type":"literal","value":" "},{"type":"integer","value":"1"},{"type":"literal","value":" "},{"type":"unit","value":"キロメートル"}]
            """);

        // the sign stands inside the pattern, after its prefix
        Parts(Japanese, "-987").Should().Be(
            """
            [{"type":"unit","value":"時速"},{"type":"literal","value":" "},{"type":"minusSign","value":"-"},{"type":"integer","value":"987"},{"type":"literal","value":" "},{"type":"unit","value":"キロメートル"}]
            """);

        // ko-KR separates the prefix from the number but not the number from the suffix
        Parts("new Intl.NumberFormat('ko-KR', { style: 'unit', unit: 'kilometer-per-hour', unitDisplay: 'long' })", "987")
            .Should().Be(
                """
                [{"type":"unit","value":"시속"},{"type":"literal","value":" "},{"type":"integer","value":"987"},{"type":"unit","value":"킬로미터"}]
                """);

        // a pattern with no prefix keeps reporting none
        Parts("new Intl.NumberFormat('de-DE', { style: 'unit', unit: 'kilometer-per-hour', unitDisplay: 'long' })", "987")
            .Should().Be(
                """
                [{"type":"integer","value":"987"},{"type":"literal","value":" "},{"type":"unit","value":"Kilometer pro Stunde"}]
                """);
    }

    private string Format(string formatter, string value)
        => _engine.Evaluate($"{formatter}.format({value})").AsString();

    private string Join(string formatter, string value)
        => _engine.Evaluate(
            $"{formatter}.formatToParts({value}).map(function (p) {{ return p.value; }}).join('')").AsString();

    private string Parts(string formatter, string value)
        => _engine.Evaluate($"JSON.stringify({formatter}.formatToParts({value}))").AsString();

    private string PartValue(string formatter, string value, string type)
        => _engine.Evaluate(
            $"{formatter}.formatToParts({value}).filter(function (p) {{ return p.type === '{type}'; }})[0].value")
            .AsString();

    private void AssertNoMismatch(string script)
    {
        var mismatches = _engine.Evaluate(script).AsString();
        mismatches.Should().Be("[]", "every part list joins back to the string the same formatter writes");
    }

    private static string JsArrayOf(string[] values) => "['" + string.Join("', '", values) + "']";
}
