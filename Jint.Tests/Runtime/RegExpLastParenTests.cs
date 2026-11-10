#nullable enable

using System.Text.RegularExpressions;

namespace Jint.Tests.Runtime;

/// <summary>
/// <c>RegExp.lastParen</c> (and its alias <c>RegExp.$+</c>) is the last capture of the last match, whether or not that
/// capture took part in it and however many captures there are: step 9 of UpdateLegacyRegExpStaticProperties in the
/// RegExp Legacy Features proposal (https://github.com/tc39/proposal-regexp-legacy-features), "if n > 0, set
/// [[RegExpLastParen]] to the last element of capturedValues", where a capture that took no part contributes the empty
/// string. It used to be the last of the first nine captures that took part. The expected strings are what Node prints
/// for the same script.
/// </summary>
public class RegExpLastParenTests
{
    private static string Run(string script) => new Engine().Evaluate(script).ToString();

    // a trailing optional group that took no part
    [TestCase("/(a)(b)?/.exec('xa'); JSON.stringify([RegExp.lastParen, RegExp['$+']])", "[\"\",\"\"]")]
    // a trailing optional group that took part
    [TestCase("/(a)(b)?/.exec('xab'); JSON.stringify([RegExp.lastParen, RegExp['$+']])", "[\"b\",\"b\"]")]
    // a last group that took part after one that did not
    [TestCase("/(a)(b)?(c)/.exec('xac'); JSON.stringify([RegExp.lastParen, RegExp['$+']])", "[\"c\",\"c\"]")]
    // an alternative that took no part
    [TestCase("/(a)|(b)/.exec('a'); JSON.stringify([RegExp.lastParen, RegExp['$+']])", "[\"\",\"\"]")]
    // the other alternative
    [TestCase("/(a)|(b)/.exec('b'); JSON.stringify([RegExp.lastParen, RegExp['$+']])", "[\"b\",\"b\"]")]
    // exactly nine groups
    [TestCase("/(a)(b)(c)(d)(e)(f)(g)(h)(i)/.exec('zabcdefghiw'); JSON.stringify([RegExp.lastParen, RegExp['$+']])", "[\"i\",\"i\"]")]
    // ten groups
    [TestCase("/(a)(b)(c)(d)(e)(f)(g)(h)(i)(j)/.exec('zabcdefghijw'); JSON.stringify([RegExp.lastParen, RegExp['$+']])", "[\"j\",\"j\"]")]
    // eleven groups
    [TestCase("/(a)(b)(c)(d)(e)(f)(g)(h)(i)(j)(k)/.exec('zabcdefghijkw'); JSON.stringify([RegExp.lastParen, RegExp['$+']])", "[\"k\",\"k\"]")]
    // twelve groups with the last two taking no part
    [TestCase("/(a)(b)(c)(d)(e)(f)(g)(h)(i)(j)(k)?(l)?/.exec('zabcdefghijw'); JSON.stringify([RegExp.lastParen, RegExp['$+']])", "[\"\",\"\"]")]
    // eleven groups, the eleventh taking no part
    [TestCase("/(a)(b)(c)(d)(e)(f)(g)(h)(i)(j)(k)?/.exec('zabcdefghijw'); JSON.stringify([RegExp.lastParen, RegExp['$+']])", "[\"\",\"\"]")]
    // twenty groups
    [TestCase("/(a)(b)(c)(d)(e)(f)(g)(h)(i)(j)(k)(l)(m)(n)(o)(p)(q)(r)(s)(t)/.exec('abcdefghijklmnopqrst'); JSON.stringify([RegExp.lastParen, RegExp['$+']])", "[\"t\",\"t\"]")]
    // no groups
    [TestCase("/b/.exec('abc'); JSON.stringify([RegExp.lastParen, RegExp['$+']])", "[\"\",\"\"]")]
    // no groups after a match that had some
    [TestCase("/(a)(b)/.exec('ab'); /c/.exec('c'); JSON.stringify([RegExp.lastParen, RegExp['$+']])", "[\"\",\"\"]")]
    // groups after a match that had none
    [TestCase("/c/.exec('c'); /(a)(b)/.exec('ab'); JSON.stringify([RegExp.lastParen, RegExp['$+']])", "[\"b\",\"b\"]")]
    // a group that matched the empty string
    [TestCase("/(a)(b*)/.exec('xa'); JSON.stringify([RegExp.lastParen, RegExp['$+']])", "[\"\",\"\"]")]
    // a read before the next match
    [TestCase("/(a)(b)/.exec('ab'); var r1 = RegExp.lastParen; /(c)(d)?/.exec('c'); JSON.stringify([r1, RegExp.lastParen])", "[\"b\",\"\"]")]
    // search
    [TestCase("'xaq'.search(/(a)(b)?(q)?/); JSON.stringify([RegExp.lastParen, RegExp['$+']])", "[\"q\",\"q\"]")]
    // search with a trailing group that took no part
    [TestCase("'xaq'.search(/(a)(q)(b)?/); JSON.stringify([RegExp.lastParen, RegExp['$+']])", "[\"\",\"\"]")]
    // a global replace with a function
    [TestCase("var seen = []; 'xa-xab'.replace(/(a)(b)?/g, function () { seen.push(RegExp.lastParen); return ''; }); JSON.stringify([seen, RegExp.lastParen])", "[[\"b\",\"b\"],\"b\"]")]
    // a match of a pattern with a named group
    [TestCase("/(?<n>a)(?<m>b)?/.exec('xa'); JSON.stringify([RegExp.lastParen, RegExp['$+']])", "[\"\",\"\"]")]
    // matchAll
    [TestCase("[...'a1b'.matchAll(/(\\d)(x)?/g)]; JSON.stringify([RegExp.lastParen, RegExp['$+']])", "[\"\",\"\"]")]
    // the alias is the same value
    [TestCase("/(a)(b)?/.exec('xa'); RegExp.lastParen === RegExp['$+']", "true")]
    public void LastParenIsTheLastCapture(string script, string expected)
    {
        Run(script).Should().Be(expected);
    }

    // The .NET lane of a Regex the host handed over applies the same rule to its own group numbering.
    [TestCase("([a-z])(b)?", "xa", "")]
    [TestCase("([a-z])(b)?", "xb", "b")]
    [TestCase("(a)(b)(c)(d)(e)(f)(g)(h)(i)(j)(k)", "abcdefghijk", "k")]
    [TestCase("(a)(b)(c)(d)(e)(f)(g)(h)(i)(j)(k)?", "abcdefghij", "")]
    [TestCase("a", "a", "")]
    public void AHostSuppliedRegexAppliesTheSameRule(string pattern, string subject, string expected)
    {
        var engine = new Engine();
        engine.SetValue("host", new Regex(pattern));
        engine.SetValue("subject", subject);
        engine.Evaluate("host.exec(subject); RegExp.lastParen").ToString().Should().Be(expected);
    }
}
