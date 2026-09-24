#nullable enable

namespace Jint.Tests.Runtime;

/// <summary>
/// <c>Intl.Locale</c> canonicalizes the value of every Unicode extension keyword, whichever key carries
/// it and whether it came from the tag or from the options bag.
/// https://tc39.es/ecma402/#sec-canonicalizeuvalue (9.2.2) is the operation, and its second step defers
/// to UTS #35 Annex C section 5
/// (https://unicode.org/reports/tr35/#Canonical_Unicode_Locale_Identifiers) for what "canonical" means:
/// the CLDR <c>common/bcp47</c> aliases, and the removal of a value of <c>true</c>.
/// </summary>
/// <remarks>
/// <para>
/// Three defects sat behind the test262 files that exposed this. <c>Intl.Locale</c> parses its tag with
/// a scanner of its own, which only ASCII-lowercased each value, so no <c>-true</c> was ever removed and
/// no bcp47 alias was ever applied on that path; the <c>firstDayOfWeek</c> option read a Number before a
/// String, so it truncated; and <c>getWeekInfo</c> answered Monday for a first-day identifier it did not
/// recognise instead of leaving the region's own value alone.
/// </para>
/// <para>
/// test262 covers the first two through <c>intl402/Locale/constructor-unicode-extension-uvalue-*.js</c>
/// and the <c>firstDayOfWeek</c> files. It does not cover <c>getWeekInfo</c>'s fallback for a present but
/// unrecognised identifier, which the first fix newly made reachable, so that one is pinned here.
/// </para>
/// </remarks>
public class IntlLocaleCanonicalizationTests
{
    private readonly Engine _engine = new();

    private string Evaluate(string expression) => _engine.Evaluate(expression).AsString();

    /// <summary>
    /// UTS #35 Annex C: "Any type or tfield value 'true' is removed." It is a property of the grammar,
    /// not of the seven keys <c>Intl.Locale</c> has an internal slot for, so it holds for a key the
    /// engine knows nothing about just as much as for <c>ca</c>.
    /// </summary>
    [Theory]
    [InlineData("en-u-ca-true", "en-u-ca")]
    [InlineData("en-u-co-true", "en-u-co")]
    [InlineData("en-u-fw-true", "en-u-fw")]
    [InlineData("en-u-hc-true", "en-u-hc")]
    [InlineData("en-u-kf-true", "en-u-kf")]
    [InlineData("en-u-kn-true", "en-u-kn")]
    [InlineData("en-u-nu-true", "en-u-nu")]
    [InlineData("en-u-aa-true", "en-u-aa")]
    [InlineData("en-u-kb-true", "en-u-kb")]
    [InlineData("en-u-zz-true", "en-u-zz")]
    [InlineData("EN-U-AA-TRUE", "en-u-aa")]
    public void ATypeValueOfTrueIsRemovedFromEveryKey(string tag, string expected)
    {
        Evaluate($"new Intl.Locale('{tag}').toString()").Should().Be(expected);
    }

    /// <summary>
    /// The bcp47 collation data aliases <c>yes</c> to <c>true</c> for <c>kb</c>, <c>kc</c>, <c>kh</c>,
    /// <c>kk</c> and <c>kn</c> and for no other key, so on those five the alias and the removal compose
    /// and everywhere else <c>yes</c> is an ordinary value that survives.
    /// </summary>
    [Theory]
    [InlineData("en-u-kb-yes", "en-u-kb")]
    [InlineData("en-u-kc-yes", "en-u-kc")]
    [InlineData("en-u-kh-yes", "en-u-kh")]
    [InlineData("en-u-kk-yes", "en-u-kk")]
    [InlineData("en-u-kn-yes", "en-u-kn")]
    [InlineData("en-u-aa-yes", "en-u-aa-yes")]
    [InlineData("en-u-ca-yes", "en-u-ca-yes")]
    [InlineData("en-u-fw-yes", "en-u-fw-yes")]
    [InlineData("en-u-kf-yes", "en-u-kf-yes")]
    public void TheYesAliasIsAppliedOnlyToTheKeysTheBcp47DataNames(string tag, string expected)
    {
        Evaluate($"new Intl.Locale('{tag}').toString()").Should().Be(expected);
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-Intl.Locale sets <c>[[Numeric]]</c> to true when the record's
    /// <c>[[kn]]</c> is <c>"true"</c> or the empty String — which, after the removal above, is what both
    /// <c>kn-true</c> and the <c>kn-yes</c> that aliases to it become.
    /// </summary>
    [Theory]
    [InlineData("en-u-kn", true)]
    [InlineData("en-u-kn-true", true)]
    [InlineData("en-u-kn-yes", true)]
    [InlineData("en-u-kn-false", false)]
    public void NumericReadsTheCanonicalizedKeywordValue(string tag, bool expected)
    {
        _engine.Evaluate($"new Intl.Locale('{tag}').numeric").AsBoolean().Should().Be(expected);
    }

    /// <summary>
    /// A keyword the tag writes without any value at all is already in canonical form, and that form is
    /// the empty string: present, not absent. https://tc39.es/ecma402/#sec-Intl.Locale.prototype.firstDayOfWeek
    /// returns <c>[[FirstDayOfWeek]]</c> unchanged, so the accessor has to tell the two apart.
    /// </summary>
    [Theory]
    [InlineData("en-u-fw", "")]
    [InlineData("en-u-fw-true", "")]
    [InlineData("EN-U-FW-TRUE", "")]
    [InlineData("en-u-fw-false", "false")]
    [InlineData("en-u-fw-yes", "yes")]
    [InlineData("EN-U-FW-LUNEDI", "lunedi")]
    [InlineData("en-u-fw-001", "001")]
    [InlineData("en-u-fw-11111111", "11111111")]
    [InlineData("en-u-fw-wed", "wed")]
    public void FirstDayOfWeekAnswersThePresentButEmptyValue(string tag, string expected)
    {
        Evaluate($"new Intl.Locale('{tag}').firstDayOfWeek").Should().Be(expected);
    }

    [Fact]
    public void FirstDayOfWeekIsUndefinedWhenTheTagCarriesNoSuchKeyword()
    {
        _engine.Evaluate("new Intl.Locale('en').firstDayOfWeek").IsUndefined().Should().BeTrue();
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-Intl.Locale step 22 reads the option with
    /// <c>GetOption(options, "firstDayOfWeek", string, empty, undefined)</c>, so ToString runs before
    /// anything else looks at the value, and only then does
    /// https://tc39.es/ecma402/#sec-weekdaytouvalue map the numeral strings <c>"0"</c> to <c>"7"</c>.
    /// Reading the Number first is what made <c>NaN</c> and <c>Infinity</c> collapse onto weekday names.
    /// </summary>
    [Theory]
    [InlineData("1", "en-u-fw-mon")]
    [InlineData("7", "en-u-fw-sun")]
    [InlineData("0", "en-u-fw-sun")]
    [InlineData("-0", "en-u-fw-sun")]
    [InlineData("100", "en-u-fw-100")]
    [InlineData("1000", "en-u-fw-1000")]
    [InlineData("1n", "en-u-fw-mon")]
    [InlineData("0n", "en-u-fw-sun")]
    [InlineData("1000n", "en-u-fw-1000")]
    [InlineData("NaN", "en-u-fw-nan")]
    [InlineData("Infinity", "en-u-fw-infinity")]
    [InlineData("true", "en-u-fw")]
    [InlineData("'TRUE'", "en-u-fw")]
    [InlineData("false", "en-u-fw-false")]
    [InlineData("null", "en-u-fw-null")]
    [InlineData("'yes'", "en-u-fw-yes")]
    [InlineData("'MONTAG'", "en-u-fw-montag")]
    public void TheFirstDayOfWeekOptionIsCoercedToStringBeforeItIsMapped(string option, string expected)
    {
        Evaluate($"new Intl.Locale('en', {{ firstDayOfWeek: {option} }}).toString()").Should().Be(expected);
        Evaluate($"new Intl.Locale('en-u-fw-WED', {{ firstDayOfWeek: {option} }}).toString()").Should().Be(expected);
    }

    /// <summary>
    /// The <c>type</c> Unicode locale nonterminal is <c>(3*8alphanum) *("-" (3*8alphanum))</c> over
    /// ASCII. A fractional or negative Number only fails it once ToString has run — truncating to an
    /// <c>int</c> first turned <c>0.5</c> into <c>"sun"</c> — and the two non-ASCII cases only fail it
    /// once the check stops asking <c>char.IsLetterOrDigit</c>, which answers for all of Unicode.
    /// </summary>
    [Theory]
    [InlineData("''")]
    [InlineData("'m'")]
    [InlineData("'mo'")]
    [InlineData("'longerThan8Chars'")]
    [InlineData("'abc?'")]
    [InlineData("'\\u00e4\\u00f6\\u00fc'")]
    [InlineData("'\\u6161bc'")]
    [InlineData("8")]
    [InlineData("10")]
    [InlineData("-1")]
    [InlineData("-1000")]
    [InlineData("0.5")]
    [InlineData("Number.MIN_VALUE")]
    [InlineData("-Infinity")]
    [InlineData("8n")]
    [InlineData("-1n")]
    public void TheFirstDayOfWeekOptionRejectsWhatTheTypeNonterminalDoesNotMatch(string option)
    {
        Evaluate($"(() => {{ try {{ new Intl.Locale('en', {{ firstDayOfWeek: {option} }}); return 'no throw'; }} catch (e) {{ return e.constructor.name; }} }})()")
            .Should().Be("RangeError");
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-Intl.Locale.prototype.getWeekInfo overrides <c>[[FirstDay]]</c> only
    /// when WeekdayUValueToNumber answers something, and it answers undefined for every string that is
    /// not one of the seven identifiers. So a tag carrying a first-day keyword the standard does not
    /// name leaves the region's own first day exactly where it was.
    /// </summary>
    [Theory]
    [InlineData("en-US-u-fw")]
    [InlineData("en-US-u-fw-true")]
    [InlineData("en-US-u-fw-lunedi")]
    [InlineData("en-US-u-fw-100")]
    [InlineData("en-US-u-fw-false")]
    public void AnUnrecognizedFirstDayIdentifierLeavesTheRegionsOwnFirstDay(string tag)
    {
        var regionDefault = _engine.Evaluate("new Intl.Locale('en-US').getWeekInfo().firstDay").AsNumber();

        _engine.Evaluate($"new Intl.Locale('{tag}').getWeekInfo().firstDay").AsNumber().Should().Be(regionDefault);
    }

    [Theory]
    [InlineData("en-US-u-fw-mon", 1d)]
    [InlineData("en-US-u-fw-sun", 7d)]
    [InlineData("en-US-u-fw-sat", 6d)]
    public void ARecognizedFirstDayIdentifierOverridesTheRegion(string tag, double expected)
    {
        _engine.Evaluate($"new Intl.Locale('{tag}').getWeekInfo().firstDay").AsNumber().Should().Be(expected);
    }

    /// <summary>
    /// Canonicalizing each keyword's value must not disturb the ones a value legitimately spans several
    /// subtags for, nor the aliases that map one value onto another rather than onto <c>true</c>.
    /// </summary>
    [Theory]
    [InlineData("en-u-ca-islamic-civil", "en-u-ca-islamic-civil")]
    [InlineData("en-u-ca-islamicc", "en-u-ca-islamic-civil")]
    [InlineData("en-u-ca-ethiopic-amete-alem", "en-u-ca-ethioaa")]
    [InlineData("en-u-ks-primary", "en-u-ks-level1")]
    [InlineData("en-u-nu-thai-ca-gregory", "en-u-ca-gregory-nu-thai")]
    [InlineData("en-u-ca-buddhist-nu-arab", "en-u-ca-buddhist-nu-arab")]
    public void AMultiSubtagValueAndItsAliasesSurviveTheCanonicalization(string tag, string expected)
    {
        Evaluate($"new Intl.Locale('{tag}').toString()").Should().Be(expected);
    }
}
