#nullable enable

using Jint.Native.Intl;

namespace Jint.Tests.Runtime;

/// <summary>
/// https://tc39.es/ecma402/#table-datetimeformat-components makes <c>"narrow"</c> a weekday format of its own, and
/// https://tc39.es/ecma402/#sec-formatdatetimepattern writes it as locale data. A weekday is the same day in every
/// calendar, so the Chinese and Dangi formatters write CLDR's narrow weekdays, the ones the Gregorian formatter writes
/// and ICU writes for all three. They used to write names derived from .NET's culture data, which differed from CLDR
/// and from one target framework to the next.
/// </summary>
/// <remarks>
/// Expected values are Node 26's (ICU 78.3, CLDR 48), which writes the same seven for <c>gregory</c>.
/// </remarks>
public class IntlLunisolarNarrowWeekdayTests
{
    /// <summary>2024-01-07 is a Sunday; the week runs to Saturday 2024-01-13.</summary>
    private static string[] Weekdays(Engine engine, string locale, string calendar, string extraOptions = "")
    {
        var result = new string[7];
        for (var i = 0; i < 7; i++)
        {
            result[i] = engine.Evaluate(
                    $"new Intl.DateTimeFormat('{locale}', {{ weekday: 'narrow', calendar: '{calendar}', timeZone: 'UTC'{extraOptions} }})"
                    + $".formatToParts(Date.UTC(2024, 0, {7 + i})).find(p => p.type === 'weekday').value")
                .AsString();
        }

        return result;
    }

    // vi: .NET's narrowest names are one letter, so six of the seven days read "T" (net10.0) or "H".."B" (net472).
    [TestCase("vi", "chinese", "CN,T2,T3,T4,T5,T6,T7")]
    [TestCase("vi", "dangi", "CN,T2,T3,T4,T5,T6,T7")]
    // ca: .NET's names all start with "d", so every day was "D".
    [TestCase("ca", "chinese", "dg.,dl.,dt.,dc.,dj.,dv.,ds.")]
    // he: CLDR's narrow names carry a geresh.
    [TestCase("he", "dangi", "\u05D0\u05F3,\u05D1\u05F3,\u05D2\u05F3,\u05D3\u05F3,\u05D4\u05F3,\u05D5\u05F3,\u05E9\u05F3")]
    // ccp: Thursday is a conjunct (GB9c), which .NET Framework's text elements cut after the virama.
    [TestCase("ccp", "chinese", "\U00011122\U00011127,\U00011125\U00011127,\U0001111F\U00011127,\U0001111D\U0001112A,\U0001111D\U00011133\U00011122\U00011128,\U00011125\U0001112A,\U00011125\U00011127")]
    // ff-Adlm: Monday is a letter and a combining mark, which .NET Framework's text elements split.
    [TestCase("ff-Adlm", "dangi", "\U0001E908,\U0001E900\U0001E944,\U0001E903,\U0001E914,\U0001E910,\U0001E903,\U0001E916")]
    // ml: Thursday is a conjunct too.
    [TestCase("ml", "chinese", "\u0D1E\u0D3E,\u0D24\u0D3F,\u0D1A\u0D4A,\u0D2C\u0D41,\u0D35\u0D4D\u0D2F\u0D3E,\u0D35\u0D46,\u0D36")]
    // bs: the stand-alone names are lower case; the derivation upper-cased them.
    [TestCase("bs", "chinese", "n,p,u,s,\u010D,p,s")]
    [TestCase("en", "chinese", "S,M,T,W,T,F,S")]
    public void ALunisolarNarrowWeekdayIsCldrs(string locale, string calendar, string expected)
    {
        var engine = new Engine();
        var names = Weekdays(engine, locale, calendar);

        names.Should().Equal(expected.Split(','));

        // the Gregorian formatter writes the same seven
        Weekdays(engine, locale, "gregory").Should().Equal(names);
    }

    /// <summary>The string lane writes the names the parts lane does.</summary>
    [Test]
    public void FormatWritesTheSameNameAsFormatToParts()
    {
        new Engine().Evaluate("new Intl.DateTimeFormat('vi', { weekday: 'narrow', calendar: 'chinese', timeZone: 'UTC' }).format(Date.UTC(2024, 0, 8))")
            .AsString().Should().Be("T2");
    }

    /// <summary>
    /// A weekday written beside another field takes CLDR's format context, and one written alone its stand-alone
    /// context; they differ for <c>bs</c>, as they do in the pattern of the Gregorian formatter.
    /// </summary>
    [Test]
    public void AWeekdayBesideAnotherFieldTakesTheFormatContext()
    {
        var engine = new Engine();

        Weekdays(engine, "bs", "chinese", ", day: 'numeric'").Should().Equal("N", "P", "U", "S", "\u010C", "P", "S");
        Weekdays(engine, "bs", "dangi", ", day: 'numeric'").Should().Equal("N", "P", "U", "S", "\u010C", "P", "S");
    }

    /// <summary>A host's narrow weekdays still take CLDR's place.</summary>
    [Test]
    public void AHostsNarrowWeekdaysAreWritten()
    {
        var engine = new Engine(options => options.Intl.CldrProvider = new NumberedNarrowWeekdays());

        Weekdays(engine, "vi", "chinese").Should().Equal("1", "2", "3", "4", "5", "6", "7");
    }

    /// <summary>
    /// A host that answers only for other styles inherits <see cref="DefaultCldrProvider"/>'s narrow answer, which is
    /// not one of its own, so CLDR's names are written, as for every other name a host does not change.
    /// </summary>
    [Test]
    public void AHostThatKeepsTheDefaultNarrowWeekdaysGetsCldrs()
    {
        var engine = new Engine(options => options.Intl.CldrProvider = new ShoutedLongWeekdays());

        Weekdays(engine, "vi", "chinese").Should().Equal("CN", "T2", "T3", "T4", "T5", "T6", "T7");
    }
}

/// <summary>A host with narrow weekday names of its own, and no other datum.</summary>
file sealed class NumberedNarrowWeekdays : DefaultCldrProvider
{
    public override string[]? GetWeekdayNames(string locale, string style)
        => string.Equals(style, "narrow", StringComparison.Ordinal)
            ? ["1", "2", "3", "4", "5", "6", "7"]
            : base.GetWeekdayNames(locale, style);
}

/// <summary>A host answering only for the long weekday names.</summary>
file sealed class ShoutedLongWeekdays : DefaultCldrProvider
{
    public override string[]? GetWeekdayNames(string locale, string style)
        => string.Equals(style, "long", StringComparison.Ordinal)
            ? ["CHU NHAT", "THU HAI", "THU BA", "THU TU", "THU NAM", "THU SAU", "THU BAY"]
            : base.GetWeekdayNames(locale, style);
}
