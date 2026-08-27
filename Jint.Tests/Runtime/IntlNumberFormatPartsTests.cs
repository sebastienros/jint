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
/// The grids below compare each numbering system against <c>latn</c> rather than asserting the join outright:
/// what is being pinned is that <b>asking for a numbering system introduces no disagreement</b>, so a
/// combination whose Latin lanes already differ is skipped instead of being silently blessed. Four such
/// combinations exist today, all of them about locale data rather than digits — a <c>ja-JP</c> long unit
/// name, an <c>ar-EG</c> negative currency's directionality mark, <c>notation: "scientific"</c> of exactly
/// zero, and a non-finite value under <c>style: "currency"</c> or <c>"unit"</c>.
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
    /// and <c>"1,234.5"</c> from the other.
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
                for (var l = 0; l < locales.length; l++) {
                    for (var o = 0; o < optionSets.length; o++) {
                        var latin = new Intl.NumberFormat(locales[l], optionSets[o]);
                        for (var s = 0; s < systems.length; s++) {
                            var options = Object.assign({ numberingSystem: systems[s] }, optionSets[o]);
                            var nf = new Intl.NumberFormat(locales[l], options);
                            for (var v = 0; v < values.length; v++) {
                                if (join(latin.formatToParts(values[v])) !== latin.format(values[v])) {
                                    continue;
                                }
                                var joined = join(nf.formatToParts(values[v]));
                                var formatted = nf.format(values[v]);
                                if (joined !== formatted) {
                                    bad.push(locales[l] + '/' + systems[s] + '/' + o + '/' + values[v]
                                        + ': format=' + formatted + ' parts=' + joined);
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

    private void AssertNoMismatch(string script)
    {
        var mismatches = _engine.Evaluate(script).AsString();
        mismatches.Should().Be("[]", "every part list joins back to the string the same formatter writes");
    }

    private static string JsArrayOf(string[] values) => "['" + string.Join("', '", values) + "']";
}
