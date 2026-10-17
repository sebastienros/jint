#nullable enable

using Jint.Native;

namespace Jint.Tests.Runtime;

/// <summary>
/// Pins the defects that moving every pattern onto the GregsEx ECMAScript matcher surfaced, in the matcher and in the
/// RegExp built-ins around it. Each case is one that answered wrongly, threw or ran pathologically slowly at some point
/// of the migration; see docs/design/regexp-gregsex-migration.md.
/// </summary>
public class RegExpEngineRegressionTests
{
    private static JsValue Evaluate(string source) => new Engine().Evaluate(source);

    // A lone surrogate written as an escape next to a raw one is two code points under u/v, never a pair: the literal
    // plans used to assemble them into one UTF-16 string and match U+1F438.
    [TestCase("new RegExp('\\\\uD83D\\uDC38', 'u').exec('\\u{1F438}')")]
    [TestCase("new RegExp('x\\\\uD83D\\uDC38', 'u').exec('x\\u{1F438}')")]
    [TestCase("new RegExp('\\\\uD83D\\uDC38|zz', 'u').exec('\\u{1F438}')")]
    // ... and a prefilter hit on the trail half of a pair is not a place a u/v match may start.
    [TestCase("/[\\uDC38]/u.exec('\\uD83D\\uDC38')")]
    [TestCase("/\\uDC38/u.exec('\\uD83D\\uDC38')")]
    public void LoneSurrogatesStayLoneUnderUnicodeModes(string script)
    {
        Evaluate(script).IsNull().Should().BeTrue();
    }

    [Test]
    public void QuantifierBoundsBeyondInt32AreAdmitted()
    {
        // Upstream GregsEx refuses these with ProgramLimitExceeded; ECMAScript admits any DecimalDigits.
        Evaluate("new RegExp('b{' + Number.MAX_SAFE_INTEGER + '}', 'u').test('')").AsBoolean().Should().BeFalse();
        Evaluate("new RegExp('b{1,' + Number.MAX_SAFE_INTEGER + '}').exec('bbb')[0]").AsString().Should().Be("bbb");
    }

    [Test]
    public void AnUnsatisfiableFixedLengthSequenceFailsInsteadOfThrowing()
    {
        // The atomic-sequence plan summed its step minimums in an int, wrapped negative and sliced past the subject.
        Evaluate("''.match(/.{2147483647}11/)").IsNull().Should().BeTrue();
        Evaluate("/x{2147483647,2147483648}x/.test('1')").AsBoolean().Should().BeFalse();
    }

    [Test]
    public void AReplacerSeesUndefinedForAGroupThatDidNotParticipate()
    {
        // RegExp.prototype[@@replace] step 14.k: an undefined capture is passed as undefined, not as "".
        Evaluate("var seen = []; 'x'.replace(/x(.)?/g, function (m, g) { seen.push(g === undefined); }); seen.join()")
            .AsString().Should().Be("true");
        Evaluate("var seen = []; 'x'.replace(/x(.)?/, function (m, g) { seen.push(g === undefined); }); seen.join()")
            .AsString().Should().Be("true");
        Evaluate("'ab'.replace(/(a)(c)?/, '[$1|$2]')").AsString().Should().Be("[a|]b");
    }

    [Test]
    public void AGreedyRunGivesCharactersBackOneAtATime()
    {
        // The counted greedy choice must offer every shorter length, longest first, exactly as one choice per
        // character did.
        Evaluate("/a.*a/.exec('xabcabcx')[0]").AsString().Should().Be("abca");
        Evaluate("/^(\\w*)(\\w)$/.exec('abc').slice(1).join()").AsString().Should().Be("ab,c");
        Evaluate("/(\\d{2,4})(\\d)/.exec('123456').slice(1).join()").AsString().Should().Be("1234,5");
        Evaluate("/.*\\uD83D\\uDE00/u.exec('a\\u{1F600}b\\u{1F600}c')[0]").AsString().Should().Be("a\U0001F600b\U0001F600");
        Evaluate("/x[\\s\\S]*?y/.exec('x1y2y')[0]").AsString().Should().Be("x1y");
    }

    [Test]
    public void ALongGreedyRunNeedsNoFrameGrowthToSucceed()
    {
        // A greedy .* over a subject far longer than the initial frame reservation.
        Evaluate("var s = 'a' + 'b'.repeat(200000) + 'a'; /a.*a/.exec(s)[0].length").AsNumber().Should().Be(200002);
        Evaluate("var s = 'b'.repeat(200000); s.split(/b*/).length").AsNumber().Should().Be(2);
    }

    [TestCase("/ZZQ/i.test('xxzZqxx')", true)]
    [TestCase("/[k]/i.test('\\u212A')", false)] // legacy Canonicalize keeps KELVIN SIGN apart from 'k'
    [TestCase("/[k]/iu.test('\\u212A')", true)] // Unicode case folding does not
    [TestCase("/\\u212A/iu.test('k')", true)]
    [TestCase("/s/i.test('\\u017F')", false)]
    [TestCase("/s/iu.test('\\u017F')", true)]
    [TestCase("/\\s*,\\s*x/.test('a ,  x')", true)]
    [TestCase("/(?=\\d)\\w+/.exec('ab12')[0] === '12'", true)]
    public void PrefiltersAdmitEveryCharacterThatCanStartAMatch(string script, bool expected)
    {
        Evaluate(script).AsBoolean().Should().Be(expected);
    }

    [Test]
    public void AReplacementWithNoMatchReturnsTheSubject()
    {
        Evaluate("var s = 'abc'.repeat(10); s.replace(/zz/g, 'x') === s").AsBoolean().Should().BeTrue();
    }

    [Test]
    public void SplitFollowsTheSpecificationsMatcherLoop()
    {
        Evaluate("JSON.stringify('a,b,,c'.split(/,/))").AsString().Should().Be("""["a","b","","c"]""");
        Evaluate("JSON.stringify('abc'.split(/(?:)/))").AsString().Should().Be("""["a","b","c"]""");
        Evaluate("JSON.stringify('\\u{1F600}x'.split(/(?:)/u))").AsString().Should().Be("[\"\U0001F600\",\"x\"]");
        Evaluate("JSON.stringify('A<B>bold</B>and<CODE>coded</CODE>'.split(/<(\\/)?([^<>]+)>/))")
            .AsString().Should().Be("""["A",null,"B","bold","/","B","and",null,"CODE","coded","/","CODE",""]""");
        Evaluate("JSON.stringify('test'.split(/(?:)/, 2))").AsString().Should().Be("""["t","e"]""");
    }
}
