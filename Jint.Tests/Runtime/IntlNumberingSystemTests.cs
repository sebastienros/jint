#nullable enable

using Jint.Native.Intl;

namespace Jint.Tests.Runtime;

/// <summary>
/// The four formatters that carry a <c>[[NumberingSystem]]</c> answer one locale with one numbering
/// system. https://tc39.es/ecma402/#sec-resolvelocale (9.2.7) step 13 starts <c>nu</c> at
/// <c>keyLocaleData[0]</c> — the locale's own default, which
/// <see cref="ICldrProvider.GetDefaultNumberingSystem"/> answers for — and lets the locale's
/// <c>-u-nu-</c> extension and then the <c>numberingSystem</c> option overwrite it.
/// </summary>
/// <remarks>
/// The shipped <see cref="DefaultCldrProvider"/> has no per-locale numbering data and answers null for
/// every locale, so these tests install the answers ICU gives instead: that is the configuration in which
/// the four constructors could disagree, and the one an embedder that installs a CLDR-backed provider is
/// in.
/// </remarks>
public class IntlNumberingSystemTests
{
    private static readonly string[] Formatters = ["NumberFormat", "DateTimeFormat", "RelativeTimeFormat", "DurationFormat"];

    private static Engine WithCldrDefaults() => new(options => options.Intl.CldrProvider = new CldrLocaleDefaults());

    /// <summary>
    /// The defect: two of the four asked the provider and two returned <c>"latn"</c> unconditionally, so
    /// one engine gave one locale two answers to the same question.
    /// </summary>
    [Test]
    public void EveryFormatterResolvesTheLocalesOwnNumberingSystem()
    {
        var engine = WithCldrDefaults();

        foreach (var formatter in Formatters)
        {
            engine.Evaluate($"new Intl.{formatter}('ar-EG').resolvedOptions().numberingSystem")
                .AsString().Should().Be("arab", $"Intl.{formatter} reads the locale's default");
            engine.Evaluate($"new Intl.{formatter}('bn-BD').resolvedOptions().numberingSystem")
                .AsString().Should().Be("beng", $"Intl.{formatter} reads the locale's default");

            // …and a locale whose default is Latin is untouched
            engine.Evaluate($"new Intl.{formatter}('en-US').resolvedOptions().numberingSystem")
                .AsString().Should().Be("latn", $"Intl.{formatter} leaves a Latin locale alone");
        }
    }

    /// <summary>
    /// A resolved numbering system that nothing formats in is the defect #3420 fixed, wearing a new coat
    /// of paint — so the digits are asserted, not only <c>resolvedOptions()</c>.
    /// </summary>
    [Test]
    public void TheLocaleDefaultIsWrittenAndNotOnlyReported()
    {
        var engine = WithCldrDefaults();

        // the digits, not the separators: which separator ar-EG groups with is .NET's answer and differs
        // between .NET Framework and modern .NET, while the ten digits do not
        ShouldWriteArabicIndicDigits(engine, "new Intl.NumberFormat('ar-EG').format(12)", "١٢");
        ShouldWriteArabicIndicDigits(engine, "new Intl.NumberFormat('ar-EG').format(1234.5)", "٢٣٤");
        ShouldWriteArabicIndicDigits(engine, "new Intl.DateTimeFormat('ar-EG', { year: 'numeric', timeZone: 'UTC' }).format(0)", "١٩٧٠");
        ShouldWriteArabicIndicDigits(engine, "new Intl.RelativeTimeFormat('ar-EG').format(3, 'day')", "٣");
        ShouldWriteArabicIndicDigits(engine, "new Intl.DurationFormat('ar-EG').format({ hours: 12, minutes: 30 })", "١٢");
        ShouldWriteArabicIndicDigits(engine, "new Intl.DurationFormat('ar-EG', { style: 'digital' }).format({ hours: 12, minutes: 30 })", "١٢:٣٠");
    }

    /// <summary>
    /// Step 13.e: a value the locale's own <c>-u-nu-</c> extension asked for overwrites the default.
    /// </summary>
    [Test]
    public void TheUnicodeExtensionWinsOverTheLocaleDefault()
    {
        var engine = WithCldrDefaults();

        foreach (var formatter in Formatters)
        {
            engine.Evaluate($"new Intl.{formatter}('ar-EG-u-nu-latn').resolvedOptions().numberingSystem")
                .AsString().Should().Be("latn", $"Intl.{formatter} lets -u-nu- win");

            // an extension naming some other non-Latin system wins just as much
            engine.Evaluate($"new Intl.{formatter}('ar-EG-u-nu-beng').resolvedOptions().numberingSystem")
                .AsString().Should().Be("beng", $"Intl.{formatter} lets -u-nu- win");
        }

        engine.Evaluate("new Intl.NumberFormat('ar-EG-u-nu-latn').format(1234.5)")
            .AsString().Should().MatchRegex("[0-9]");
        engine.Evaluate("new Intl.DurationFormat('ar-EG-u-nu-latn').format({ hours: 12 })")
            .AsString().Should().MatchRegex("[0-9]");
    }

    /// <summary>
    /// Step 13.f: the <c>numberingSystem</c> option overwrites both.
    /// </summary>
    [Test]
    public void TheNumberingSystemOptionWinsOverTheLocaleDefault()
    {
        var engine = WithCldrDefaults();

        foreach (var formatter in Formatters)
        {
            engine.Evaluate($"new Intl.{formatter}('ar-EG', {{ numberingSystem: 'latn' }}).resolvedOptions().numberingSystem")
                .AsString().Should().Be("latn", $"Intl.{formatter} lets the option win");
            engine.Evaluate($"new Intl.{formatter}('ar-EG-u-nu-beng', {{ numberingSystem: 'latn' }}).resolvedOptions().numberingSystem")
                .AsString().Should().Be("latn", $"Intl.{formatter} lets the option win over the extension too");
        }

        engine.Evaluate("new Intl.NumberFormat('ar-EG', { numberingSystem: 'latn' }).format(1234.5)")
            .AsString().Should().MatchRegex("[0-9]");
        engine.Evaluate("new Intl.DurationFormat('ar-EG', { numberingSystem: 'latn' }).format({ hours: 12 })")
            .AsString().Should().MatchRegex("[0-9]");
    }

    /// <summary>
    /// Steps 13.e.iii.1 and 13.f.iv both ask whether <c>keyLocaleData</c> contains the requested value
    /// before adopting it, and neither clears <c>value</c> when it does not — so a well-formed request for
    /// a system nothing can write leaves the locale's default in place, rather than dropping to Latin.
    /// </summary>
    [Test]
    public void AWellFormedButUnusableRequestFallsBackToTheLocaleDefault()
    {
        var engine = WithCldrDefaults();

        foreach (var formatter in Formatters)
        {
            engine.Evaluate($"new Intl.{formatter}('ar-EG', {{ numberingSystem: 'abcdef' }}).resolvedOptions().numberingSystem")
                .AsString().Should().Be("arab", $"Intl.{formatter} keeps the locale default");
            engine.Evaluate($"new Intl.{formatter}('ar-EG-u-nu-abcdef').resolvedOptions().numberingSystem")
                .AsString().Should().Be("arab", $"Intl.{formatter} keeps the locale default");
        }
    }

    /// <summary>
    /// The locale's default is a default, not a request, so it does not become a <c>-u-nu-</c> subtag of
    /// the reported locale — while one the caller did ask for stays exactly where they put it.
    /// </summary>
    [Test]
    public void TheLocaleDefaultDoesNotAppearInTheResolvedLocale()
    {
        var engine = WithCldrDefaults();

        foreach (var formatter in Formatters)
        {
            engine.Evaluate($"new Intl.{formatter}('ar-EG').resolvedOptions().locale")
                .AsString().Should().Be("ar-EG", $"Intl.{formatter} reports the locale it was given");
            engine.Evaluate($"new Intl.{formatter}('ar-EG-u-nu-beng').resolvedOptions().locale")
                .AsString().Should().Be("ar-EG-u-nu-beng", $"Intl.{formatter} keeps a requested extension");
        }
    }

    /// <summary>
    /// A system the provider names but cannot supply digits for is not in <c>keyLocaleData</c> at all, so
    /// no formatter adopts it — a numbering system nothing could write in is exactly what must not be
    /// reported.
    /// </summary>
    [Test]
    public void ADefaultTheProviderCannotWriteIsNotAdopted()
    {
        var engine = new Engine(options => options.Intl.CldrProvider = new NamesASystemItCannotWrite());

        foreach (var formatter in Formatters)
        {
            engine.Evaluate($"new Intl.{formatter}('ar-EG').resolvedOptions().numberingSystem")
                .AsString().Should().Be("latn", $"Intl.{formatter} refuses a system with no digits");
        }
    }

    /// <summary>
    /// The shipped provider has no per-locale numbering data and says so by answering null, so an
    /// unconfigured engine writes exactly what it wrote before.
    /// </summary>
    [Test]
    public void TheShippedProviderStillWritesLatinDigitsEverywhere()
    {
        var engine = new Engine();

        DefaultCldrProvider.Instance.GetDefaultNumberingSystem("ar-EG").Should().BeNull();

        foreach (var formatter in Formatters)
        {
            engine.Evaluate($"new Intl.{formatter}('ar-EG').resolvedOptions().numberingSystem")
                .AsString().Should().Be("latn");
        }

        engine.Evaluate("new Intl.NumberFormat('ar-EG').format(1234.5)").AsString().Should().MatchRegex("[0-9]");
        engine.Evaluate("new Intl.DurationFormat('ar-EG').format({ hours: 12 })").AsString().Should().MatchRegex("[0-9]");
    }

    /// <summary>
    /// A numbering system rewrites the digits of a date's fields, and nothing else the pattern wrote.
    /// </summary>
    /// <remarks>
    /// https://tc39.es/ecma402/#sec-formatdatetimepattern splits the pattern with PartitionPattern and
    /// copies every <c>literal</c> through untouched; <c>[[NumberingSystem]]</c> reaches only a field's
    /// value, through the FormatNumeric calls in the numeric and 2-digit branches. Those values are
    /// integers, so a date carries no decimal separator to rewrite — and <c>de-DE</c>, whose pattern
    /// separates the fields with a full stop, is where rewriting one anyway shows up.
    /// </remarks>
    [Test]
    public void ADatePatternsPunctuationIsNotADecimalSeparator()
    {
        var engine = new Engine();
        const string TwentySeventhOfAugust = "new Date(Date.UTC(2026, 7, 27))";
        // 27.08.2026 in Arabic-Indic digits, around the full stops de-DE's own pattern writes. Spelled
        // out because a bidirectional literal is unreadable in a diff and easy to reorder by accident.
        const string Expected = "٢٧.٠٨.٢٠٢٦";

        var components = "new Intl.DateTimeFormat('de-DE', { numberingSystem: 'arab', "
            + "year: 'numeric', month: '2-digit', day: '2-digit', timeZone: 'UTC' })";

        engine.Evaluate($"{components}.format({TwentySeventhOfAugust})")
            .AsString().Should().Be(Expected, "the pattern's full stops are literals, not decimal separators");
        engine.Evaluate($"{components}.formatToParts({TwentySeventhOfAugust}).map(p => p.value).join('')")
            .AsString().Should().Be(Expected, "https://tc39.es/ecma402/#sec-formatdatetime is the concatenation of the parts");

        var styled = "new Intl.DateTimeFormat('de-DE', { numberingSystem: 'arab', dateStyle: 'short', timeZone: 'UTC' })";
        engine.Evaluate($"{styled}.format({TwentySeventhOfAugust})")
            .AsString().Should().Be(Expected, "a dateStyle pattern's full stops are literals too");
    }

    /// <summary>
    /// The one separator a date formatter writes itself — the one before a fractional second — is the
    /// numbering system's, and both lanes write it.
    /// </summary>
    /// <remarks>
    /// No CLDR pattern supplies this separator: <c>fractionalSecondDigits</c> is a component option, and
    /// the component lane assembles its own pattern around it. That makes the character
    /// implementation-, locale- and numbering-system-dependent in the sense
    /// https://tc39.es/ecma402/#sec-formatdatetimepattern allows, and <c>arab</c> writes U+066B for it —
    /// which is a different question from the pattern text above, and is pinned here so that answering
    /// the first one does not silently change this one.
    /// </remarks>
    [Test]
    public void TheSeparatorBeforeAFractionalSecondIsTheNumberingSystems()
    {
        var engine = new Engine();
        const string Instant = "new Date(Date.UTC(2026, 7, 27, 1, 2, 3, 456))";
        // 02:03 and 456 in Arabic-Indic digits, around U+066B ARABIC DECIMAL SEPARATOR
        const string Expected = "٠٢:٠٣٫٤٥٦";

        var formatter = "new Intl.DateTimeFormat('en-US', { numberingSystem: 'arab', "
            + "minute: '2-digit', second: '2-digit', fractionalSecondDigits: 3, timeZone: 'UTC' })";

        engine.Evaluate($"{formatter}.format({Instant})").AsString().Should().Be(Expected);
        engine.Evaluate($"{formatter}.formatToParts({Instant}).map(p => p.value).join('')")
            .AsString().Should().Be(Expected);
    }

    private static void ShouldWriteArabicIndicDigits(Engine engine, string expression, string expectedDigits)
    {
        var text = engine.Evaluate(expression).AsString();
        text.Should().Contain(expectedDigits, $"{expression} writes in the system it resolved");
        text.Should().NotMatchRegex("[0-9]", $"{expression} writes no Latin digit");
    }
}

/// <summary>
/// The answers ICU gives for two locales whose CLDR default is not Latin, and nothing else: everything
/// the rest of the interface knows still comes from the inherited implementation.
/// </summary>
file sealed class CldrLocaleDefaults : DefaultCldrProvider
{
    public override string? GetDefaultNumberingSystem(string locale)
    {
        var separator = locale.IndexOf('-');
        var language = separator < 0 ? locale : locale.Substring(0, separator);

        if (string.Equals(language, "ar", StringComparison.OrdinalIgnoreCase))
        {
            return "arab";
        }

        if (string.Equals(language, "bn", StringComparison.OrdinalIgnoreCase))
        {
            return "beng";
        }

        return base.GetDefaultNumberingSystem(locale);
    }
}

/// <summary>A provider naming a numbering system it has no digits for.</summary>
file sealed class NamesASystemItCannotWrite : DefaultCldrProvider
{
    public override string? GetDefaultNumberingSystem(string locale) => "abcdef";
}
