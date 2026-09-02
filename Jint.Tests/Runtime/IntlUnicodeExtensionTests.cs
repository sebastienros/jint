#nullable enable

using Jint.Native.Intl;

namespace Jint.Tests.Runtime;

/// <summary>
/// A locale's <c>-u-</c> sequence carries as many keys as it likes, and every formatter that reads one
/// reads all of them. https://unicode.org/reports/tr35/#unicode_locale_extensions is the grammar: a key
/// is two characters, a value is the 3-to-8-character subtags that follow it, and both end at the next
/// key or the next singleton.
/// </summary>
/// <remarks>
/// The defect (#3573) was a hand-rolled scanner per constructor, each of which consumed the next key into
/// the current key's value — so <c>en-US-u-ca-buddhist-nu-arab</c> read <c>ca</c> back as "buddhist-nu",
/// resolved to no calendar at all, and came back as bare <c>en-US</c> with <c>gregory</c> and
/// <c>latn</c>. Either key alone worked, which is why it survived. Every expectation below is what V8
/// (Node 24, full ICU) answers for the same tag.
/// </remarks>
public class IntlUnicodeExtensionTests
{
    private static Engine Engine() => new();

    private static string Resolved(Engine engine, string expression) => engine.Evaluate(expression).AsString();

    /// <summary>
    /// The table from the issue: two keys in either order, and neither of them lost.
    /// </summary>
    [TestCase("en-US-u-ca-buddhist", "en-US-u-ca-buddhist", "buddhist", "latn", "h12")]
    [TestCase("en-US-u-nu-arab", "en-US-u-nu-arab", "gregory", "arab", "h12")]
    [TestCase("en-US-u-ca-buddhist-nu-arab", "en-US-u-ca-buddhist-nu-arab", "buddhist", "arab", "h12")]
    [TestCase("en-US-u-nu-arab-ca-buddhist", "en-US-u-ca-buddhist-nu-arab", "buddhist", "arab", "h12")]
    [TestCase("en-US-u-ca-buddhist-hc-h23", "en-US-u-ca-buddhist-hc-h23", "buddhist", "latn", "h23")]
    [TestCase("en-US-u-hc-h23-ca-buddhist", "en-US-u-ca-buddhist-hc-h23", "buddhist", "latn", "h23")]
    [TestCase("en-US-u-hc-h23-nu-arab-ca-buddhist", "en-US-u-ca-buddhist-hc-h23-nu-arab", "buddhist", "arab", "h23")]
    public void DateTimeFormatReadsEveryKeyOfTheExtension(
        string requested, string locale, string calendar, string numberingSystem, string hourCycle)
    {
        var engine = Engine();
        var resolved = $"new Intl.DateTimeFormat('{requested}', {{ hour: 'numeric' }}).resolvedOptions()";

        Resolved(engine, $"{resolved}.locale").Should().Be(locale);
        Resolved(engine, $"{resolved}.calendar").Should().Be(calendar);
        Resolved(engine, $"{resolved}.numberingSystem").Should().Be(numberingSystem);
        Resolved(engine, $"{resolved}.hourCycle").Should().Be(hourCycle);
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-resolvelocale step 12 builds the resolved locale from the relevant
    /// extension keys only, so a NumberFormat keeps <c>nu</c> and drops the <c>ca</c> beside it.
    /// </summary>
    [TestCase("en-US-u-ca-buddhist", "en-US", "latn")]
    [TestCase("en-US-u-nu-arab", "en-US-u-nu-arab", "arab")]
    [TestCase("en-US-u-ca-buddhist-nu-arab", "en-US-u-nu-arab", "arab")]
    [TestCase("en-US-u-nu-arab-ca-buddhist", "en-US-u-nu-arab", "arab")]
    [TestCase("en-US-u-hc-h23-nu-arab-ca-buddhist", "en-US-u-nu-arab", "arab")]
    public void NumberFormatReadsTheNumberingSystemBesideAnotherKey(string requested, string locale, string numberingSystem)
    {
        var engine = Engine();
        var resolved = $"new Intl.NumberFormat('{requested}').resolvedOptions()";

        Resolved(engine, $"{resolved}.locale").Should().Be(locale);
        Resolved(engine, $"{resolved}.numberingSystem").Should().Be(numberingSystem);
    }

    /// <summary>
    /// The two formatters the issue does not measure share the same relevant key, and answer the same.
    /// </summary>
    [TestCase("RelativeTimeFormat")]
    [TestCase("DurationFormat")]
    public void TheOtherNumberingSystemCarriersReadItToo(string formatter)
    {
        var engine = Engine();

        Resolved(engine, $"new Intl.{formatter}('en-US-u-ca-buddhist-nu-arab').resolvedOptions().numberingSystem")
            .Should().Be("arab");
        Resolved(engine, $"new Intl.{formatter}('en-US-u-ca-buddhist-nu-arab').resolvedOptions().locale")
            .Should().Be("en-US-u-nu-arab");
    }

    /// <summary>
    /// Collator's three keys, read from one sequence.
    /// </summary>
    [Test]
    public void CollatorReadsAllThreeOfItsKeys()
    {
        var engine = Engine();
        var resolved = "new Intl.Collator('de-DE-u-co-phonebk-kf-upper-kn').resolvedOptions()";

        Resolved(engine, $"{resolved}.collation").Should().Be("phonebk");
        Resolved(engine, $"{resolved}.caseFirst").Should().Be("upper");
        engine.Evaluate($"{resolved}.numeric").AsBoolean().Should().BeTrue();

        // …and the same three when kn carries an explicit value that another key follows
        var withValue = "new Intl.Collator('de-DE-u-kn-false-co-phonebk').resolvedOptions()";
        Resolved(engine, $"{withValue}.collation").Should().Be("phonebk");
        engine.Evaluate($"{withValue}.numeric").AsBoolean().Should().BeFalse();
    }

    /// <summary>
    /// A value is allowed more than one subtag, and a sequence is allowed attributes ahead of its keys.
    /// </summary>
    [Test]
    public void AMultiSubtagValueAndALeadingAttributeAreBothRead()
    {
        var engine = Engine();

        var multiPart = "new Intl.DateTimeFormat('en-US-u-ca-islamic-civil-nu-arab', { hour: 'numeric' }).resolvedOptions()";
        Resolved(engine, $"{multiPart}.calendar").Should().Be("islamic-civil");
        Resolved(engine, $"{multiPart}.numberingSystem").Should().Be("arab");
        Resolved(engine, $"{multiPart}.locale").Should().Be("en-US-u-ca-islamic-civil-nu-arab");

        var withAttribute = "new Intl.DateTimeFormat('en-US-u-foo-ca-buddhist-nu-arab', { hour: 'numeric' }).resolvedOptions()";
        Resolved(engine, $"{withAttribute}.calendar").Should().Be("buddhist");
        Resolved(engine, $"{withAttribute}.numberingSystem").Should().Be("arab");
    }

    /// <summary>
    /// The sequence ends at the next singleton, so a private-use or transformed section after it is not
    /// part of any key's value — and does not stop the keys before it from being read.
    /// </summary>
    [Test]
    public void ASequenceEndsAtTheNextSingleton()
    {
        var engine = Engine();

        Resolved(engine, "new Intl.DateTimeFormat('en-US-u-nu-arab-x-private', { hour: 'numeric' }).resolvedOptions().numberingSystem")
            .Should().Be("arab");
        Resolved(engine, "new Intl.NumberFormat('en-US-u-nu-arab-t-en-latn').resolvedOptions().numberingSystem")
            .Should().Be("arab");
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-resolvelocale step 13: an option supersedes the tag's own keyword, and
    /// the keyword then leaves the resolved locale. Reading two keys correctly must not disturb that, so
    /// the single-key cases are pinned beside the two-key one.
    /// </summary>
    [Test]
    public void AnOptionStillSupersedesTheExtension()
    {
        var engine = Engine();

        // one key: the option wins and the extension goes
        var overridden = "new Intl.DateTimeFormat('en-US-u-ca-buddhist', { calendar: 'hebrew', hour: 'numeric' }).resolvedOptions()";
        Resolved(engine, $"{overridden}.calendar").Should().Be("hebrew");
        Resolved(engine, $"{overridden}.locale").Should().Be("en-US");

        // one key, agreeing: the extension stays
        var agreeing = "new Intl.DateTimeFormat('en-US-u-ca-buddhist', { calendar: 'buddhist', hour: 'numeric' }).resolvedOptions()";
        Resolved(engine, $"{agreeing}.calendar").Should().Be("buddhist");
        Resolved(engine, $"{agreeing}.locale").Should().Be("en-US-u-ca-buddhist");

        // two keys: only the superseded one goes
        var mixed = "new Intl.DateTimeFormat('en-US-u-ca-buddhist-nu-arab', { calendar: 'hebrew', hour: 'numeric' }).resolvedOptions()";
        Resolved(engine, $"{mixed}.calendar").Should().Be("hebrew");
        Resolved(engine, $"{mixed}.numberingSystem").Should().Be("arab");
        Resolved(engine, $"{mixed}.locale").Should().Be("en-US-u-nu-arab");

        // and the same for NumberFormat's own key
        var numeric = "new Intl.NumberFormat('en-US-u-nu-arab', { numberingSystem: 'thai' }).resolvedOptions()";
        Resolved(engine, $"{numeric}.numberingSystem").Should().Be("thai");
        Resolved(engine, $"{numeric}.locale").Should().Be("en-US");
    }

    /// <summary>
    /// Intl.Locale and Intl.getCanonicalLocales always read these tags; the point is that the formatters
    /// now agree with them rather than answering for a locale the tag never asked for.
    /// </summary>
    [Test]
    public void TheFormattersAgreeWithLocaleAndGetCanonicalLocales()
    {
        var engine = Engine();
        const string Tag = "en-US-u-ca-buddhist-nu-arab";

        Resolved(engine, $"Intl.getCanonicalLocales(['{Tag}'])[0]").Should().Be(Tag);
        Resolved(engine, $"new Intl.Locale('{Tag}').calendar").Should().Be("buddhist");
        Resolved(engine, $"new Intl.Locale('{Tag}').numberingSystem").Should().Be("arab");

        var formatter = $"new Intl.DateTimeFormat('{Tag}', {{ hour: 'numeric' }}).resolvedOptions()";
        Resolved(engine, $"{formatter}.calendar").Should().Be(Resolved(engine, $"new Intl.Locale('{Tag}').calendar"));
        Resolved(engine, $"{formatter}.numberingSystem").Should().Be(Resolved(engine, $"new Intl.Locale('{Tag}').numberingSystem"));
    }

    /// <summary>
    /// The reader itself, since it is the one definition of the rule the eight scanners each had.
    /// </summary>
    [TestCase("en-US-u-ca-buddhist-nu-arab", "ca", "buddhist")]
    [TestCase("en-US-u-ca-buddhist-nu-arab", "nu", "arab")]
    [TestCase("en-US-u-nu-arab-ca-buddhist", "ca", "buddhist")]
    [TestCase("en-US-u-ca-islamic-civil-nu-arab", "ca", "islamic-civil")]
    [TestCase("en-US-u-foo-bar-ca-buddhist", "ca", "buddhist")]
    [TestCase("en-US-u-nu-arab-x-ca-gregory", "ca", null)]
    [TestCase("en-US-x-u-ca-gregory", "ca", null)]
    [TestCase("en-US-u-kn-ca-buddhist", "kn", "")]
    [TestCase("en-US-u-kn-ca-buddhist", "ca", "buddhist")]
    [TestCase("en-US-u-CA-BUDDHIST", "ca", "buddhist")]
    [TestCase("en-US", "ca", null)]
    public void GetKeywordValueReadsTheKeyAndOnlyTheKey(string locale, string key, string? expected)
    {
        UnicodeExtension.GetKeywordValue(locale, key).Should().Be(expected);
    }

    [TestCase("en-US-u-ca-buddhist-nu-arab", "en-US")]
    [TestCase("en-US-u-nu-arab-x-private", "en-US-x-private")]
    [TestCase("en-US-t-en-latn-u-nu-arab", "en-US-t-en-latn")]
    [TestCase("en-US", "en-US")]
    public void RemoveSequenceKeepsEveryOtherExtension(string locale, string expected)
    {
        UnicodeExtension.RemoveSequence(locale).Should().Be(expected);
    }

    [TestCase("en-US", "nu", "arab", "en-US-u-nu-arab")]
    [TestCase("en-US-u-ca-buddhist", "nu", "arab", "en-US-u-ca-buddhist-nu-arab")]
    [TestCase("en-US-u-nu-latn-ca-buddhist", "nu", "arab", "en-US-u-ca-buddhist-nu-arab")]
    [TestCase("en-US-u-ca-buddhist-nu-arab", "nu", null, "en-US-u-ca-buddhist")]
    [TestCase("en-US-u-nu-arab", "nu", null, "en-US")]
    [TestCase("en-US-x-private", "nu", "arab", "en-US-u-nu-arab-x-private")]
    [TestCase("en-US-u-nu-arab-x-private", "nu", null, "en-US-x-private")]
    [TestCase("en-US-u-foo-nu-arab", "nu", null, "en-US-u-foo")]
    public void WithKeywordRewritesOneKeyAndKeepsTheRestInOrder(string locale, string key, string? value, string expected)
    {
        UnicodeExtension.WithKeyword(locale, key, value).Should().Be(expected);
    }
}
