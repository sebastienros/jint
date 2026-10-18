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

    // The FIRST-strings prefilter searches OrdinalIgnoreCase, which agrees with Canonicalize only on ASCII and not on
    // k/s under u/v (KELVIN SIGN, LONG S): those must stay findable through the VM.
    [TestCase("/agggtaaa|tttaccct/i.exec('xxAgGgTaAaxx').index === 2", true)]
    [TestCase("'xCgggtaaaxTTTACCCgx'.match(/[cgt]gggtaaa|tttaccc[acg]/ig).join() === 'Cgggtaaa,TTTACCCg'", true)]
    [TestCase("'GGGTAAAc'.match(/[cgt]gggtaaa|tttaccc[acg]/ig) === null", true)]
    [TestCase("/kk/iu.test('\u212A\u212A')", true)]
    [TestCase("/ss/iu.test('\u017Fs')", true)]
    [TestCase("/kk/i.test('\u212A\u212A')", false)]
    [TestCase("/(?:ab|cd)\\b/i.exec('xx CD yy').index === 3", true)]
    public void StringPrefiltersFindEveryMatch(string script, bool expected)
    {
        Evaluate(script).AsBoolean().Should().Be(expected);
    }

    [Test]
    public void AUnicodeGreedyRunGivesBackWholeCodePoints()
    {
        Evaluate("/a.*a/u.exec('xa\\u{1F600}a\\u{1F600}b')[0]").AsString().Should().Be("a\U0001F600a");
        Evaluate("/q[^z]*\\u{1F600}/u.exec('q1\\u{1F600}2\\u{1F600}3')[0]").AsString().Should().Be("q1\U0001F6002\U0001F600");
        Evaluate("/.*\\uDE00/u.exec('a\\u{1F600}b')").IsNull().Should().BeTrue();
    }

    [Test]
    public void AReplacementWithNoMatchReturnsTheSubject()
    {
        Evaluate("var s = 'abc'.repeat(10); s.replace(/zz/g, 'x') === s").AsBoolean().Should().BeTrue();
    }

    // A nullable repeat now runs on the atomic-sequence plan: its run is taken whole only where nothing after it
    // could have used a shorter one.
    [TestCase("JSON.stringify('a , b,c ,d, e'.split(/\\s*,\\s*/))", """["a","b","c","d","e"]""")]
    [TestCase("'x  ,  y'.match(/\\s*,\\s*/)[0]", "  ,  ")]
    [TestCase("String('x y, z'.match(/\\s*,\\s*/).index)", "3")]
    [TestCase("String((function () { var re = /\\s*,/g; re.lastIndex = 2; return re.exec('a , b ,c').index; })())", "2")]
    [TestCase("String(/\\s*,\\s*/y.exec('a ,b'))", "null")]
    [TestCase("(function () { var re = /\\s*,\\s*/y; re.lastIndex = 1; return re.exec('a ,b')[0]; })()", " ,")]
    [TestCase("/[a-c]*d/.exec('xxabcabdz')[0]", "abcabd")]
    [TestCase("/x?y/.exec('aaxyz')[0]", "xy")]
    [TestCase("String(/^\\s*$/.test(' x '))", "false")]
    public void NullableRepeatsMatchAsTheyDidOnTheVm(string script, string expected)
    {
        Evaluate(script).AsString().Should().Be(expected);
    }

    // Under legacy i a class has a folded membership bitmap, and a literal no other character canonicalizes with is
    // planned as a plain literal; Canonicalize's ASCII and BMP rules must survive both.
    [TestCase("/[a-z]/i.test('K')", true)]
    [TestCase("/[a-z]/i.test('\\u212A')", false)]
    [TestCase("/[\\u212A]/i.test('k')", false)]
    [TestCase("/[\\u017F]/i.test('S')", false)]
    [TestCase("/[s]/i.test('\\u017F')", false)]
    [TestCase("/[\\u03c3]/i.test('\\u03c2')", true)]
    [TestCase("/[\\u0131]/i.test('I')", false)]
    [TestCase("/[i]/i.test('\\u0130')", false)]
    [TestCase("/[\\u00df]/i.test('\\u1e9e')", false)]
    [TestCase("/[a-z]/iu.test('\\u212A')", true)]
    [TestCase("/[a-z]+@/i.exec('xx-AbC@d')[0] === 'AbC@'", true)]
    [TestCase("/[^a-z]+/i.exec('abc-12Z')[0] === '-12'", true)]
    [TestCase("/[\\u0400-\\u04ff]+@/i.exec('\\u0416\\u0436@')[0].length === 3", true)]
    [TestCase("/[a-z]+@[a-z0-9.]+\\.[a-z]{2,}/i.test('sample-123-text')", false)]
    [TestCase("/[a-z]+@[a-z0-9.]+\\.[a-z]{2,}/i.exec('mail Bob@Example.COM now')[0] === 'Bob@Example.COM'", true)]
    public void CaseInsensitiveClassesKeepCanonicalize(string script, bool expected)
    {
        Evaluate(script).AsBoolean().Should().Be(expected);
    }

    // The FIRST-character prefilter now looks through groups, alternations and inner sequences.
    [TestCase("/(?:abc|def|ghi)+\\d*/.exec('xx-defabc12-')[0] === 'defabc12'", true)]
    [TestCase("/(https?):\\/\\/([^\\/\\s]+)/i.exec('see HTTPS://host/x')[2] === 'host'", true)]
    [TestCase("/(?:(?=b)|a)c/.exec('xbac')[0] === 'ac'", true)]
    [TestCase("/(?:x?)+y/.exec('aay')[0] === 'y'", true)]
    [TestCase("/(?<=a)b/.exec('cbab').index === 3", true)]
    [TestCase("/(a)\\1|z/.exec('qaa')[0] === 'aa'", true)]
    public void FirstCharactersSeeThroughGroups(string script, bool expected)
    {
        Evaluate(script).AsBoolean().Should().Be(expected);
    }

    // A /./ run is measured by a range search for its four line terminators, falling back after a few false hits.
    [TestCase("/a.*a/.exec('a\\tb\\ta\\u2028a')[0] === 'a\\tb\\ta'", true)]
    [TestCase("/a.*a/.exec('a\\u4e2d\\u4e2d\\u4e2d\\u4e2d\\u4e2d\\u4e2d\\u4e2d\\u4e2d\\u4e2d\\u4e2da\\rza')[0].length === 12", true)]
    [TestCase("/^.*$/m.exec('x\\t\\t\\t\\t\\t\\t\\t\\t\\t\\ty\\u2029z')[0].length === 12", true)]
    [TestCase("/.*/u.exec('\\u{1F600}\\t\\u{1F600}\\nz')[0].length === 5", true)]
    [TestCase("/a.*b/s.exec('a\\nb')[0] === 'a\\nb'", true)]
    public void DotRunsStopAtEveryLineTerminator(string script, bool expected)
    {
        Evaluate(script).AsBoolean().Should().Be(expected);
    }

    // Split takes the ranges of a pattern without groups a batch at a time.
    [TestCase("JSON.stringify('a;b;c'.split(/;/, 2))", """["a","b"]""")]
    [TestCase("String(('x;'.repeat(100)).split(/;/).length)", "101")]
    [TestCase("JSON.stringify('abc'.split(/x*/))", """["a","b","c"]""")]
    [TestCase("JSON.stringify('a1b22c'.split(/\\d*/))", """["a","b","c"]""")]
    [TestCase("JSON.stringify('\\u{1F600}a'.split(/(?:)/u))", "[\"\U0001F600\",\"a\"]")]
    [TestCase("JSON.stringify('ab'.split(/$/))", """["ab"]""")]
    [TestCase("JSON.stringify(';a;'.split(/;/))", """["","a",""]""")]
    public void SplitWithoutGroupsTakesMatchesInBatches(string script, string expected)
    {
        Evaluate(script).AsString().Should().Be(expected);
    }

    [Test]
    public void SplitWithoutGroupsStillTimesOut()
    {
        var engine = new Engine(o => o.Constraints.RegexTimeout = TimeSpan.FromMilliseconds(50));
        var act = () => engine.Evaluate("('a'.repeat(30) + '!b,').repeat(20).split(/^(?:a|a)*b/)");
        act.Should().Throw<System.Text.RegularExpressions.RegexMatchTimeoutException>();
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

    // Review of #4300: one large input used to grow the shared matcher's frame plan for every later search, on every
    // thread and engine, and keep the storage for the thread's life; exhausting it threw an internal exception type.
    [Test]
    public void ExhaustingBacktrackingStorageThrowsARangeErrorAndDoesNotGrowTheSharedMatcher()
    {
        var engine = new Engine();
        var error = engine.Evaluate("""
            try { /"(?:[^"\\]|\\.)*"/.exec('"' + 'a\\"b'.repeat(250000) + '"'); 'no error' }
            catch (e) { e.name + ': ' + e.message }
            """).AsString();
        error.Should().Be("RangeError: Maximum regular expression backtracking depth exceeded");

        // The next search of the same pattern runs from the small, shared plan.
        engine.Evaluate("""/"(?:[^"\\]|\\.)*"/.exec('x"ab"y')[0]""").AsString().Should().Be("\"ab\"");
        var matcher = Jint.Runtime.RegExp.JintRegExpEngine.Compile("\"(?:[^\"\\\\]|\\\\.)*\"", Jint.Runtime.RegExp.RegExpFlags.None);
        var groups = new Jint.Runtime.RegExp.GregsEx.RegexCapture[matcher.CaptureCount];
        var big = "\"" + string.Concat(Enumerable.Repeat("a\\\"b", 20_000)) + "\"";
        matcher.TryMatch(big, 0, Jint.Runtime.RegExp.JintRegExpEngine.NoDeadline, groups).Should().BeTrue();
        groups[0].Length.Should().Be(big.Length);
        matcher.PlannedFrames.Should().BeLessThan(20_000, "growth beyond the retained ceiling stays with the search");
    }

    [Test]
    public void ARangeErrorFromTheMatcherIsCatchableInScript()
    {
        Evaluate("""
            var n = 0;
            for (var i = 0; i < 3; i++) {
                try { /"(?:[^"\\]|\\.)*"/.test('"' + 'ab'.repeat(600000) + '"'); } catch (e) { if (e instanceof RangeError) n++; }
            }
            n
            """).AsNumber().Should().Be(3);
    }

    // Review of #4300: RegExp.prototype[@@search] goes through RegExpBuiltinExec, which updates the legacy statics.
    [TestCase("'zzq'.match(/(z)/); 'xaby'.search(/(a)(b)/); RegExp.$1 + RegExp.$2 + '|' + RegExp.lastMatch", "ab|ab")]
    [TestCase("'zzq'.match(/(z)/); 'xaby'.search(/b/); JSON.stringify(RegExp.$1) + '|' + RegExp.lastMatch + '|' + RegExp.leftContext", "\"\"|b|xa")]
    [TestCase("'zzq'.match(/(z)/); 'xaby'.search(/nope/); RegExp.$1", "z")]
    public void SearchUpdatesTheLegacyStatics(string script, string expected)
    {
        Evaluate(script).AsString().Should().Be(expected);
    }

    // Review of #4300: compiling a script-supplied pattern runs under the regex timeout, as a compile-work budget.
    [Test]
    public void CompilingRunsUnderTheRegexTimeout()
    {
        var tight = new Engine(o => o.Constraints.RegexTimeout = TimeSpan.FromMilliseconds(1));
        var act = () => tight.Evaluate("new RegExp('[\\u0100-\\uffff]'.repeat(1000) + 'tight', 'i')");
        act.Should().Throw<System.Text.RegularExpressions.RegexMatchTimeoutException>();

        // An ordinary pattern is far below the budget's floor, whatever the timeout.
        tight.Evaluate("new RegExp('[a-z]+@[a-z0-9.]+\\\\.[a-z]{2,}', 'i').test('a@b.cd')").AsBoolean().Should().BeTrue();
    }

    [Test]
    public void TheEmptyJsRegExpConstructorBuildsAUsableMatcher()
    {
        new JsRegExp(new Engine()).Matcher.Should().NotBeNull();
    }
}
