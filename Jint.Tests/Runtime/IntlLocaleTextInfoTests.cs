namespace Jint.Tests.Runtime;

/// <summary>
/// <c>Intl.Locale.prototype.getTextInfo</c> is https://tc39.es/ecma402/#sec-textdirectionoflocale: the direction
/// of the locale's script - its own script subtag, or the one Add Likely Subtags supplies - as the RTL field of
/// CLDR's script metadata records it, and <c>undefined</c> whenever that cannot be determined.
/// </summary>
/// <remarks>
/// The answer used to be the .NET culture's <c>TextInfo.IsRightToLeft</c>, which answered <c>"ltr"</c> for any tag
/// the machine's globalization data did not know, ignored a script subtag that disagreed with the language, and
/// could never answer <c>undefined</c>. test262's <c>intl402/Locale/prototype/getTextInfo/script-*.js</c> files
/// pin most of these rows too; these also pin the tags whose likely script is not the language's usual one.
/// </remarks>
public class IntlLocaleTextInfoTests
{
    private readonly Engine _engine = new();

    private string Direction(string tag) => _engine
        .Evaluate($"String(new Intl.Locale('{tag}').getTextInfo().direction)")
        .AsString();

    /// <summary>
    /// A script subtag decides on its own, whatever the language: Arabic written in Latin script is
    /// left-to-right, English written in Arabic script right-to-left, and a language nobody registered takes
    /// the direction of the script it names.
    /// </summary>
    [TestCase("en-Latn", "ltr")]
    [TestCase("ar-Latn", "ltr")]
    [TestCase("he-Latn", "ltr")]
    [TestCase("en-Arab", "rtl")]
    [TestCase("und-Hebr", "rtl")]
    [TestCase("abcdefgh-Latn", "ltr")]
    [TestCase("abcdefgh-Arab", "rtl")]
    [TestCase("ff-Adlm", "rtl")]
    [TestCase("sr-Cyrl", "ltr")]
    [TestCase("ja-Jpan", "ltr")]
    public void AScriptSubtagDecides(string tag, string expected)
    {
        Direction(tag).Should().Be(expected);
    }

    /// <summary>
    /// Without a script subtag the likely script decides, and the region can change which one that is:
    /// Punjabi is Gurmukhi in India but Arabic in Pakistan, Azerbaijani and Uzbek are Latin by default but Arabic
    /// in Iran and Afghanistan. <c>und</c> is likely <c>en-Latn-US</c>.
    /// </summary>
    [TestCase("en", "ltr")]
    [TestCase("und", "ltr")]
    [TestCase("ar", "rtl")]
    [TestCase("he", "rtl")]
    [TestCase("fa", "rtl")]
    [TestCase("dv", "rtl")]
    [TestCase("yi", "rtl")]
    [TestCase("pa", "ltr")]
    [TestCase("pa-PK", "rtl")]
    [TestCase("az", "ltr")]
    [TestCase("az-IR", "rtl")]
    [TestCase("uz-AF", "rtl")]
    [TestCase("ar-u-nu-latn", "rtl")]
    public void WithoutAScriptSubtagTheLikelyScriptDecides(string tag, string expected)
    {
        Direction(tag).Should().Be(expected);
    }

    /// <summary>
    /// A language with no likely subtags has no script to decide by: an unregistered one, a constructed one
    /// CLDR has no data for, and one from the private-use range CLDR excludes.
    /// </summary>
    [TestCase("abcdefgh")]
    [TestCase("tlh")]
    [TestCase("qfz")]
    public void NoLikelyScriptIsUndefined(string tag)
    {
        Direction(tag).Should().Be("undefined");
    }

    /// <summary>
    /// A script whose RTL field is <c>UNKNOWN</c> (Common, Inherited, Unknown and Braille), and one the script
    /// metadata does not list at all - unregistered, private use, reserved, unwritten, or a variant such as
    /// Nastaliq - is neither direction, whatever the language would have been written in.
    /// </summary>
    [TestCase("und-Zyyy")]
    [TestCase("en-Zinh")]
    [TestCase("ar-Zzzz")]
    [TestCase("en-Brai")]
    [TestCase("en-Aaaa")]
    [TestCase("ar-Qaaq")]
    [TestCase("en-True")]
    [TestCase("ar-Zxxx")]
    [TestCase("ur-Aran")]
    public void AScriptOfUnknownDirectionIsUndefined(string tag)
    {
        Direction(tag).Should().Be("undefined");
    }

    /// <summary>
    /// The result is still an ordinary object with one own, enumerable <c>direction</c> data property, even
    /// when that property holds <c>undefined</c>.
    /// </summary>
    [Test]
    public void AnUndefinedDirectionIsStillAnOwnProperty()
    {
        _engine.Evaluate("""
            var info = new Intl.Locale('tlh').getTextInfo();
            JSON.stringify([Object.getPrototypeOf(info) === Object.prototype, Reflect.ownKeys(info), 'direction' in info, info.direction === undefined])
            """).AsString().Should().Be("""[true,["direction"],true,true]""");
    }
}
