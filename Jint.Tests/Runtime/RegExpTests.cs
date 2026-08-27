using System.Text.RegularExpressions;
using Jint.Native;
using Jint.Runtime;

namespace Jint.Tests.Runtime;

public class RegExpTests
{
    [Test]
    public void MatchGlobalUnicodeNoMatchesReturnsNull()
    {
        var engine = new Engine();
        var result = engine.Evaluate("'abc'.match(/\\d/gu) === null").AsBoolean();

        result.Should().BeTrue();
    }

    [Test]
    public void MatchGlobalUnicodeCollectsAllMatches()
    {
        var engine = new Engine();
        var result = engine.Evaluate("JSON.stringify('a1b22c333'.match(/\\d+/gu))").AsString();

        result.Should().Be("[\"1\",\"22\",\"333\"]");
    }

    [Test]
    public void MatchGlobalUnicodeEmptyMatchesAdvanceByCodePoint()
    {
        var engine = new Engine();
        // 2 astral code points (4 UTF-16 units): empty matches at positions 0, 2, 4
        var result = engine.Evaluate("'\\u{1F600}\\u{1F600}'.match(/(?:)/gu).length").AsNumber();

        result.Should().Be(3);
    }

    // A capturing group nested inside a quantified non-capturing group routes the pattern to
    // the custom (QuickJS-port) regex engine. Its greedy Char+/Range+ bulk-advance optimization
    // must not fire when the loop body has more than the single leading char/range atom,
    // otherwise trailing iterations get dropped (e.g. "abcabc" matched as "abca").
    [TestCase("(?:a(b)c)+", "abcabc", "[\"abcabc\",\"b\"]")]
    [TestCase("(?:a(b)c)+", "abc", "[\"abc\",\"b\"]")]
    [TestCase("(?:x(y)z)+", "xyzxyzxyz", "[\"xyzxyzxyz\",\"y\"]")]
    [TestCase("(?:a(b)c)+", "zzabcabc", "[\"abcabc\",\"b\"]")]
    [TestCase("(a(b)c)+", "abcabc", "[\"abcabc\",\"abc\",\"b\"]")]
    [TestCase("(?:(a)(b))+", "abab", "[\"abab\",\"a\",\"b\"]")]
    [TestCase("(?:([ab])(x))+", "axbx", "[\"axbx\",\"b\",\"x\"]")]
    // Range as the leading atom of a multi-atom body exercises the Range+ bulk-advance guard.
    [TestCase("(?:[a-c](x))+", "axbx", "[\"axbx\",\"x\"]")]
    // Single char/range body: the bulk-advance optimization SHOULD still apply and stay correct.
    [TestCase("(a)+", "aaa", "[\"aaa\",\"a\"]")]
    [TestCase("([a-z])+", "abc", "[\"abc\",\"c\"]")]
    public void MatchesNestedCaptureInsideQuantifiedGroup(string pattern, string input, string expected)
    {
        var engine = new Engine();
        var result = engine.Evaluate($"JSON.stringify(/{pattern}/.exec({JsonString(input)}))").AsString();

        result.Should().Be(expected);
    }

    private static string JsonString(string s) => System.Text.Json.JsonSerializer.Serialize(s);

    [TestCase("gy")]
    [TestCase("guy")]
    public void MatchStickyGlobalCollectsAllAdjacentMatches(string flags)
    {
        // without 'u' exercises the .NET sticky fast path, with 'u' the generic exec loop
        var engine = new Engine();
        var result = engine.Evaluate($"JSON.stringify(['aaa'.match(/a/{flags}), 'ababab'.match(/ab/{flags})])").AsString();

        result.Should().Be("[[\"a\",\"a\",\"a\"],[\"ab\",\"ab\",\"ab\"]]");
    }

    [TestCase("gy")]
    [TestCase("guy")]
    public void MatchStickyGlobalStopsAtFirstGap(string flags)
    {
        // matches after the gap exist but are not adjacent, so sticky matching must not include them
        var engine = new Engine();
        var result = engine.Evaluate($"JSON.stringify(['aabaa'.match(/a/{flags}), 'baa'.match(/a/{flags})])").AsString();

        result.Should().Be("[[\"a\",\"a\"],null]");
    }

    [TestCase("gy")]
    [TestCase("guy")]
    public void MatchStickyGlobalAdvancesOverEmptyMatches(string flags)
    {
        var engine = new Engine();
        var result = engine.Evaluate($"JSON.stringify('aab'.match(/a*/{flags}))").AsString();

        result.Should().Be("[\"aa\",\"\",\"\"]");
    }

    [Test]
    public void MatchStickyGlobalResetsLastIndex()
    {
        var engine = new Engine();
        var result = engine.Evaluate("var r = /a/gy; r.lastIndex = 2; JSON.stringify(['aaa'.match(r), r.lastIndex])").AsString();

        result.Should().Be("[[\"a\",\"a\",\"a\"],0]");
    }

    [TestCase("")]
    [TestCase("u")]
    public void SplitCollectsSegmentsCapturesAndTail(string flags)
    {
        // without flags exercises the .NET fast path, with 'u' the generic exec loop
        var engine = new Engine();
        var result = engine.Evaluate($"JSON.stringify('a1b22c'.split(/(\\d+)/{flags}))").AsString();

        result.Should().Be("[\"a\",\"1\",\"b\",\"22\",\"c\"]");
    }

    [TestCase("")]
    [TestCase("u")]
    public void SplitHonorsLimit(string flags)
    {
        var engine = new Engine();
        var result = engine.Evaluate($"JSON.stringify(['a,b,c'.split(/,/{flags}, 2), 'a1b2c'.split(/(\\d)/{flags}, 2)])").AsString();

        result.Should().Be("[[\"a\",\"b\"],[\"a\",\"1\"]]");
    }

    [TestCase("")]
    [TestCase("u")]
    public void SplitKeepsEmptyLeadingAndTrailingSegments(string flags)
    {
        var engine = new Engine();
        var result = engine.Evaluate($"JSON.stringify([',a,'.split(/,/{flags}), ''.split(/x/{flags}), ''.split(/(?:)/{flags})])").AsString();

        result.Should().Be("[[\"\",\"a\",\"\"],[\"\"],[]]");
    }

    private const string TestRegex = "^(https?:\\/\\/)?([\\da-z\\.-]+)\\.([a-z\\.]{2,6})([\\/\\w\\.-]*)*\\/?$";
    private const string TestedValue = "https://archiverbx.blob.core.windows.net/static/C:/Users/USR/Documents/Projects/PROJ/static/images/full/1234567890.jpg";

    [Test]
    public void CanNotBreakEngineWithLongRunningMatch()
    {
        var engine = new Engine(e => e.Constraints.RegexTimeout = TimeSpan.FromSeconds(1));

        Invoking(() =>
        {
            engine.Execute($"'{TestedValue}'.match(/{TestRegex}/)");
        }).Should().ThrowExactly<RegexMatchTimeoutException>();
    }

    [Test]
    public void CanNotBreakEngineWithLongRunningRegExp()
    {
        var engine = new Engine(e => e.Constraints.RegexTimeout = TimeSpan.FromSeconds(1));

        Invoking(() =>
        {
            engine.Execute($"'{TestedValue}'.match(new RegExp(/{TestRegex}/))");
        }).Should().ThrowExactly<RegexMatchTimeoutException>();
    }

    [Test]
    public void PreventsInfiniteLoop()
    {
        var engine = new Engine();
        var result = (JsArray) engine.Evaluate("'x'.match(/|/g);");
        result.Length.Should().Be((uint) 2);
        result[0].Should().Be("");
        result[1].Should().Be("");
    }

    [Test]
    public void ToStringWithNonRegExpInstanceAndMissingProperties()
    {
        var engine = new Engine();
        var result = engine.Evaluate("/./['toString'].call({})").AsString();

        result.Should().Be("/undefined/undefined");
    }

    [Test]
    public void ToStringWithNonRegExpInstanceAndValidProperties()
    {
        var engine = new Engine();
        var result = engine.Evaluate("/./['toString'].call({ source: 'a', flags: 'b' })").AsString();

        result.Should().Be("/a/b");
    }

    [Test]
    public void MatchAllIteratorReturnsCorrectNumberOfElements()
    {
        var engine = new Engine();
        var result = engine.Evaluate("[...'one two three'.matchAll(/t/g)].length").AsInteger();

        result.Should().Be(2);
    }

    [Test]
    public void ToStringWithRealRegExpInstance()
    {
        var engine = new Engine();
        var result = engine.Evaluate("/./['toString'].call(/test/g)").AsString();

        result.Should().Be("/test/g");
    }

    [Test]
    public void ToStringPreserversOriginalPatternOfLiteral()
    {
        var engine = new Engine();
        var result = engine.Evaluate("/^x\\/\\\\r\\n\\u2028\\u2029\\0\0|[x/\\\\r\\n\\u2028\\u2029\\0\0]$/");

        var jsRegExp = result.Should().BeOfType<JsRegExp>().Which;
        jsRegExp.Source.Should().Be("^x\\/\\\\r\\n\\u2028\\u2029\\0\0|[x/\\\\r\\n\\u2028\\u2029\\0\0]$");
        jsRegExp.ToString().Should().Be("/^x\\/\\\\r\\n\\u2028\\u2029\\0\0|[x/\\\\r\\n\\u2028\\u2029\\0\0]$/");
    }

    [Test]
    public void ToStringCorrectlyEscapesProblematicCharacters()
    {
        var engine = new Engine();
        var result = engine.Evaluate(@"new RegExp('^x/\\\r\n\u2028\u2029\\0\0|[x/\\\r\n\u2028\u2029\\0\0]$')");

        var jsRegExp = result.Should().BeOfType<JsRegExp>().Which;
        jsRegExp.Source.Should().Be("^x\\/\\r\\n\\u2028\\u2029\\0\0|[x/\\r\\n\\u2028\\u2029\\0\0]$");
        jsRegExp.ToString().Should().Be("/^x\\/\\r\\n\\u2028\\u2029\\0\0|[x/\\r\\n\\u2028\\u2029\\0\0]$/");
    }

    [Test]
    public void ShouldNotThrowErrorOnIncompatibleRegex()
    {
        var engine = new Engine();
        engine.Evaluate(@"/[^]*?(:[rp][el]a[\w-]+)[^]*/").Should().NotBeNull();
        engine.Evaluate("/[^]a/").Should().NotBeNull();
        engine.Evaluate("new RegExp('[^]a')").Should().NotBeNull();

        engine.Evaluate("/[]/").Should().NotBeNull();
        engine.Evaluate("new RegExp('[]')").Should().NotBeNull();
    }

    [Test]
    public void ShouldNotThrowErrorOnRegExNumericNegation()
    {
        var engine = new Engine();
        ReferenceEquals(JsNumber.DoubleNaN, engine.Evaluate("-/[]/")).Should().BeTrue();
    }

    [Test]
    public void ShouldProduceCorrectSourceForSlashEscapes()
    {
        var engine = new Engine();
        var source = engine.Evaluate(@"/\/\//.source");
        source.Should().Be("\\/\\/");
    }

    [TestCase("", "/()/ug", new[] { "" }, new[] { 0 })]
    [TestCase("💩", "/()/ug", new[] { "", "" }, new[] { 0, 2 })]
    [TestCase("ᴜⁿᵢ𝒸ₒᵈₑ is a 💩", "/i?/ug",
        new[] { "", "", "", "", "", "", "", "", "i", "", "", "", "", "", "" },
        new[] { 0, 1, 2, 3, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 16 })]
    public void ShouldNotMatchEmptyStringsWithinSurrogatePairsInUnicodeMode(string input, string pattern, string[] expectedCaptures, int[] expectedIndices)
    {
        var engine = new Engine();
        var matches = engine.Evaluate($"[...'{input}'.matchAll({pattern})]").AsArray();
        matches.Length.Should().Be((uint) expectedCaptures.Length);
        matches.Select((m, i) => m.Get(0).AsString()).Should().Equal(expectedCaptures);
        matches.Select(m => m.Get("index").AsInteger()).Should().Equal(expectedIndices);
    }

    [Test]
    public void ShouldAllowProblematicGroupNames()
    {
        var engine = new Engine();

        var match = engine.Evaluate("'abc'.match(/(?<$group>b)/)").AsArray();
        var groups = match.Get("groups").AsObject();
        groups.GetOwnPropertyKeys().Select(k => k.AsString()).Should().Equal(["$group"]);
        groups["$group"].Should().Be("b");

        var result = engine.Evaluate("'abc'.replace(/(?<$group>b)/g, '-$<$group>-')").AsString();
        result.Should().Be("a-b-c");
    }

    [Test]
    public void ShouldSupportRegExpModifiersInLiteralsAndConstructor()
    {
        var engine = new Engine();

        var prepared = Engine.PrepareScript("""
            const literal = /(?m-i:^a$)/i;
            `${literal.test('A\n')},${literal.test('a\n')}`;
            """);

        engine.Evaluate(prepared).AsString().Should().Be("false,true");
        engine.Evaluate("""
            const regex = new RegExp("(?m-i:^a$)", "i");
            `${regex.test('A\n')},${regex.test('a\n')}`;
            """).AsString().Should().Be("false,true");
    }

    [Test]
    public void ShouldAllowClassSetSyntaxCharacterOutsideClassSetForFlagV()
    {
        var engine = new Engine();
        var result = engine.Evaluate(@"new RegExp('/-', 'v').test('/-')");
        result.AsBoolean().Should().BeTrue();
    }

    [Test]
    public void ShouldAllowClassSetReservedDoublePunctuatorCharactersOutsideClassSetForFlagV()
    {
        var engine = new Engine();
        var result = engine.Evaluate(@"new RegExp('&&!!##%%,,::;;<<==>>@@``~~', 'v').test('&&!!##%%,,::;;<<==>>@@``~~')");
        result.AsBoolean().Should().BeTrue();
    }

    [Test]
    public void ShouldAllowEscapedClassSetReservedPunctuatorsForFlagV()
    {
        var engine = new Engine();
        var result = engine.Evaluate(@"new RegExp('[\\!\\#\\%\\&\\,\\-\\:\\;\\<\\=\\>\\@\\`\\~]{14}', 'v').test('!#%&,-:;<=>@`~')");
        result.AsBoolean().Should().BeTrue();
    }

    [Test]
    public void Issue506()
    {
        var engine = new Engine();
        var result = engine.Evaluate("/[^]?(:[rp][el]a[\\w-]+)[^]/.test(':reagent-')").AsBoolean();
        result.Should().BeTrue();
    }

    // Engine routing tests for RegExpConstructor.NeedCustomEngine: .NET Regex is preferred
    // for performance whenever it can reproduce ECMAScript semantics, the custom
    // (QuickJS-port) engine is the fallback. A quantified group needs the custom engine when
    // its body contains a capturing group or lookaround assertion, or when its body can match
    // the empty string. The empty-body case diverges two ways: for a capturing body .NET
    // retains captures from earlier iterations and records empty-iteration captures, whereas
    // ECMAScript clears captures per iteration and rejects empty iterations; for a
    // non-capturing body .NET's empty-subexpression loop protection stops the repetition at
    // the first empty/zero-width alternative, whereas ECMAScript's RepeatMatcher prunes the
    // empty iteration and backtracks into a later consuming alternative (so the match itself
    // diverges). Nullable non-capturing bodies are routed conservatively — some, like a single
    // greedy-nullable alternative, would in fact agree with .NET
    // (https://tc39.es/ecma262/#sec-runtime-semantics-repeatmatcher-abstract-operation).

    // string-tagcloud.js parseJSON patterns (hot in benchmarks)
    [TestCase(@"""[^""\\\n\r]*""|true|false|null|-?\d+(?:\.\d*)?(:?[eE][+\-]?\d+)?", "g")]
    [TestCase(@"(?:^|:|,)(?:\s*\[)+", "g")]
    // string-unpack-code.js pattern
    [TestCase(@"\b\w+\b", "g")]
    // quantified non-capturing groups without captures/lookarounds whose body cannot match
    // the empty string are .NET-safe
    [TestCase(@"(?:ab)+", "")]
    // quantified capturing groups whose body cannot match the empty string are .NET-safe
    [TestCase(@"(a+)+", "")]
    [TestCase(@"((?:a|b)c)*", "")]
    [TestCase(@"(:?[eE][+\-]?\d+)?", "")]
    public void UsesDotNetRegexWhenPatternIsTranslatable(string pattern, string flags)
    {
        var engine = new Engine();
        var regExp = (JsRegExp) engine.Realm.Intrinsics.RegExp.Construct([pattern, flags]);

        regExp.UsesDotNetEngine.Should().BeTrue();
    }

    // capturing group inside a quantified group: .NET retains captures across iterations
    [TestCase(@"((a)|b)+", "")]
    [TestCase(@"(?:(a)|b)+", "")]
    // quantified capturing group whose body can match the empty string: .NET records empty captures
    [TestCase(@"(a*)*", "")]
    [TestCase(@"(a|)*", "")]
    [TestCase(@"(\b)*", "")]
    [TestCase(@"(a{0,2})+", "")]
    [TestCase(@"((?:a*))*", "")]
    // quantified non-capturing group whose body can match the empty string: .NET's empty-loop
    // protection stops at the first empty/zero-width alternative, ECMAScript backtracks into a
    // later consuming alternative (the match itself diverges)
    [TestCase(@"(?:|a)*", "")]
    [TestCase(@"(?:a*|b)*", "")]
    [TestCase(@"(?:a||b)*", "")]
    [TestCase(@"(?:^|a)+", "")]
    [TestCase(@"(?:a??)*", "")]
    // conservatively routed to custom as well (nullable non-capturing body, though .NET agrees here)
    [TestCase(@"(?:a*)+", "")]
    [TestCase(@"(?:a|)+b", "")]
    // quantified lookaround assertions
    [TestCase(@"(?=(a))+", "")]
    [TestCase(@"(?:(?=a).)+", "")]
    // forward backreference
    [TestCase(@"\1(a)", "")]
    // unicode modes and case-insensitive matching of non-ASCII content
    [TestCase("a", "u")]
    [TestCase("a", "v")]
    [TestCase("ä", "i")]
    public void UsesCustomEngineWhenDotNetSemanticsDiverge(string pattern, string flags)
    {
        var engine = new Engine();
        var regExp = (JsRegExp) engine.Realm.Intrinsics.RegExp.Construct([pattern, flags]);

        regExp.UsesDotNetEngine.Should().BeFalse();
    }

    [Test]
    public void QuantifiedGroupCapturesAreClearedOnEachIteration()
    {
        var engine = new Engine();

        // the last iteration matches 'b', so the inner capture must be undefined
        engine.Evaluate("JSON.stringify(/((a)|b)+/.exec('ab'))").AsString().Should().Be("[\"ab\",\"b\",null]");
        engine.Evaluate("JSON.stringify(/(?:(a)|b)+/.exec('ab'))").AsString().Should().Be("[\"ab\",null]");
    }

    [Test]
    public void QuantifiedCapturingGroupRejectsEmptyIterations()
    {
        var engine = new Engine();

        // an iteration matching the empty string fails per RepeatMatcher, so the capture
        // never participates and must not report an empty string
        engine.Evaluate("JSON.stringify(/(a*)*/.exec('b'))").AsString().Should().Be("[\"\",null]");
        engine.Evaluate("JSON.stringify(/(a*)*/.exec('ab'))").AsString().Should().Be("[\"a\",\"a\"]");
        engine.Evaluate("JSON.stringify(/(a|)*/.exec('b'))").AsString().Should().Be("[\"\",null]");
        engine.Evaluate("JSON.stringify(/(\\b)*/.exec('a'))").AsString().Should().Be("[\"\",null]");
    }

    [Test]
    public void QuantifiedNonCapturingGroupWithNullableBodyMatchesPerSpec()
    {
        // Regression: a non-capturing quantified group whose body can reach the empty string
        // before a consuming alternative must not be delegated to .NET Regex, whose empty-loop
        // protection truncates the match. ECMAScript's RepeatMatcher prunes the empty iteration
        // and backtracks into the later consuming alternative, matching fully.
        var engine = new Engine();

        engine.Evaluate("/(?:a*|b)*/.exec('aaabbb')[0]").AsString().Should().Be("aaabbb");
        engine.Evaluate("/(?:|a)*/.exec('aaa')[0]").AsString().Should().Be("aaa");
        engine.Evaluate("/(?:a||b)*/.exec('aabb')[0]").AsString().Should().Be("aabb");
        engine.Evaluate("/(?:^|a)+/m.exec('abc')[0]").AsString().Should().Be("a");
        engine.Evaluate("/(?:a??)*/.exec('aaa')[0]").AsString().Should().Be("aaa");
        engine.Evaluate("/(?:\\b|a)*/.exec('aaa')[0]").AsString().Should().Be("aaa");
        engine.Evaluate("'aaa'.replace(/(?:|a)*/g, 'X')").AsString().Should().Be("XX");
    }

    // without 'u' these route to .NET Regex, with 'u' to the custom engine; results must agree
    [TestCase("")]
    [TestCase("u")]
    public void QuantifiedNonCapturingGroupBehavesTheSameOnBothEngines(string flags)
    {
        var engine = new Engine();

        engine.Evaluate($"JSON.stringify('a:  [[x,  [y'.match(/(?:^|:|,)(?:\\s*\\[)+/g{flags}))").AsString().Should().Be("[\":  [[\",\",  [\"]");
        engine.Evaluate($"JSON.stringify('12.5e3 7 2E-3'.match(/-?\\d+(?:\\.\\d*)?(:?[eE][+\\-]?\\d+)?/g{flags}))").AsString().Should().Be("[\"12.5e3\",\"7\",\"2E-3\"]");
        engine.Evaluate($"JSON.stringify('aabxa'.match(/(?:a|)+/g{flags}).filter(function (s) {{ return s !== ''; }}))").AsString().Should().Be("[\"aa\",\"a\"]");
    }

    [Test]
    public void TagCloudJsonValidationPipelineWorks()
    {
        // the json2.js-style parseJSON validation from the string-tagcloud benchmark;
        // both hot patterns route to .NET Regex and must produce spec-identical results
        var engine = new Engine();
        var result = engine.Evaluate("""
            var text = '[{"tag":"x","popularity":123},{"tag":"y","popularity":-1.5e+3,"ok":true,"nil":null}]';
            /^[\],:{}\s]*$/.test(text.replace(/\\./g, '@').
                replace(/"[^"\\\n\r]*"|true|false|null|-?\d+(?:\.\d*)?(:?[eE][+\-]?\d+)?/g, ']').
                replace(/(?:^|:|,)(?:\s*\[)+/g, ''))
            """).AsBoolean();

        result.Should().BeTrue();
    }

    // Regression tests for https://github.com/sebastienros/jint/issues/2454
    //
    // The TestRegex pattern triggers Jint's custom (QuickJS-port) regex engine via
    // RegExpConstructor.NeedCustomEngine — that engine has no built-in match timeout
    // (unlike .NET Regex which embeds MatchTimeout), so each prototype method must
    // honor the prepare-time RegexTimeout when calling the custom engine.

    // RegExp.prototype[@@match] without /g → slow path (RegExpExec → CustomEngineBuiltinExec).
    [TestCase("'{0}'.match(/{1}/)")]
    // RegExp.prototype[@@match] with /g → custom-engine fast loop in Match().
    [TestCase("'{0}'.match(/{1}/g)")]
    // RegExp.prototype[@@replace] with /g → custom-engine fast loop in Replace().
    [TestCase("'{0}'.replace(/{1}/g, 'X')")]
    // RegExp.prototype.test() → custom-engine IsMatch fast path.
    [TestCase("/{1}/.test('{0}')")]
    // RegExp.prototype[@@search] → custom-engine Execute fast path.
    [TestCase("'{0}'.search(/{1}/)")]
    public void PreparedScriptHonorsRegexTimeoutForCustomEngine(string scriptTemplate)
    {
        var script = scriptTemplate.Replace("{0}", TestedValue).Replace("{1}", TestRegex);
        AssertPrepareTimeRegexTimeoutFires(script);
    }

    /// <summary>
    /// The dynamic sibling of <see cref="PreparedScriptHonorsRegexTimeoutForCustomEngine"/>: a pattern the
    /// custom engine has to take over, built by the script rather than written as a literal, runs under the
    /// same budget its literal form does. Before sebastienros/jint#3431 the two halves of one prepared
    /// script disagreed — the literal ran under the prepared script's timeout while <c>new RegExp(...)</c>
    /// was compiled under the parser package's default and matched under whatever the engine's constraint
    /// happened to be.
    /// </summary>
    [Test]
    public void PreparedScriptHonorsRegexTimeoutForRuntimeBuiltCustomEngineRegex()
    {
        // The pattern is spelled as a JavaScript string literal here rather than a regex literal, so its
        // backslashes have to survive one more level of escaping.
        var patternAsJsString = TestRegex.Replace("\\", "\\\\");

        AssertPrepareTimeRegexTimeoutFires($"'{TestedValue}'.match(new RegExp('{patternAsJsString}'))");
    }

    [Test]
    public void PreparedModuleHonorsRegexTimeoutForCustomEngine()
    {
        var preparationOptions = ModulePreparationOptions.Default with
        {
            ParsingOptions = ModulePreparationOptions.Default.ParsingOptions with
            {
                RegexTimeout = PrepareTimeRegexTimeout,
            },
        };

        var preparedModule = Engine.PrepareModule(
            $"export default '{TestedValue}'.match(/{TestRegex}/)",
            options: preparationOptions);

        // Engine is set to a long timeout so a regression that drops the prepare-time timeout on the floor
        // falls through to EngineRegexTimeout rather than to the modest default — see
        // ShouldHaveRunUnderThePrepareTimeBudget for why that is the wrong answer this test discriminates
        // against, and why it no longer needs a clock to do it.
        var engine = new Engine(o => o.Constraints.RegexTimeout = EngineRegexTimeout);
        engine.Modules.Add("__main__", x => x.AddModule(preparedModule));

        var timedOut = Invoking(() => engine.Modules.Import("__main__"))
            .Should().ThrowExactly<RegexMatchTimeoutException>().Which;

        ShouldHaveRunUnderThePrepareTimeBudget(timedOut);
    }

    /// <summary>
    /// The budget the match that failed was actually running under — <b>not</b> a restatement of the
    /// configuration.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>RegExpPrototype.ExecuteWithTimeout</c> resolves the effective timeout into one local
    /// (<c>GetCustomEngineTimeout</c>: the prepared script's, falling back to the engine's), hands that local
    /// to the interpreter as its deadline, and constructs the <see cref="RegexMatchTimeoutException"/> from
    /// the very same local. So <see cref="RegexMatchTimeoutException.MatchTimeout"/> is a witness of which of
    /// the two budgets fired, thrown by the matcher that fired it — a regression that drops the prepare-time
    /// timeout reports <see cref="EngineRegexTimeout"/> here and fails, and there is no way to report
    /// <see cref="PrepareTimeRegexTimeout"/> while having actually waited out the other one.
    /// </para>
    /// <para>
    /// This replaces a wall-clock bound (#3379). That bound asked the same question — did this fire on one
    /// second or on thirty? — through a proxy, and the proxy does not survive a loaded runner: .NET and
    /// Jint's interpreter both check a regex deadline every <i>N</i> steps rather than continuously, so the
    /// overshoot scales with the machine and a one-second budget was measured reporting at 16.7 s against a
    /// 15 s bound on a contended Windows leg. Expressing the bound as a ratio of the thirty seconds would not
    /// have helped either, because the wrong answer's overshoot scales by the same factor — both candidates
    /// move together, so no fixed fraction separates them. The exception's own field does, exactly, and
    /// without measuring anything.
    /// </para>
    /// <para>
    /// It is also strictly narrower than the type check it accompanies:
    /// <c>RegExpInterpreter</c> raises the same exception type for a backtracking stack overflow, which
    /// carries no match timeout at all and used to satisfy <c>ThrowExactly</c>.
    /// </para>
    /// </remarks>
    private static void ShouldHaveRunUnderThePrepareTimeBudget(RegexMatchTimeoutException timedOut)
        => timedOut.MatchTimeout.Should().Be(
            PrepareTimeRegexTimeout,
            "the match must have run under the prepared script's own regex timeout rather than falling through to the engine's {0}",
            EngineRegexTimeout);

    /// <summary>
    /// The wrong answer. Deliberately far from <see cref="PrepareTimeRegexTimeout"/> so that a regression is
    /// unmistakable in the witness above, and finite so that a regression which dropped <em>both</em> budgets
    /// is reported rather than hanging the run.
    /// </summary>
    private static readonly TimeSpan EngineRegexTimeout = TimeSpan.FromSeconds(30);

    private static readonly TimeSpan PrepareTimeRegexTimeout = TimeSpan.FromSeconds(1);

    private static void AssertPrepareTimeRegexTimeoutFires(string script)
    {
        var preparationOptions = ScriptPreparationOptions.Default with
        {
            ParsingOptions = ScriptPreparationOptions.Default.ParsingOptions with
            {
                RegexTimeout = PrepareTimeRegexTimeout,
            },
        };

        var preparedScript = Engine.PrepareScript(script, options: preparationOptions);

        // Engine is set to a long timeout so a regression that drops the prepare-time timeout on the floor
        // falls through to EngineRegexTimeout rather than to the modest default — see
        // ShouldHaveRunUnderThePrepareTimeBudget.
        var engine = new Engine(o => o.Constraints.RegexTimeout = EngineRegexTimeout);

        var timedOut = Invoking(() => engine.Execute(preparedScript))
            .Should().ThrowExactly<RegexMatchTimeoutException>().Which;

        ShouldHaveRunUnderThePrepareTimeBudget(timedOut);
    }

    // ---- host-supplied .NET Regex (RegExpConstructor.Construct(Regex, ...)) ----

    [Test]
    public void HostSuppliedRegexSupportsPrototypeMethods()
    {
        // A Regex handed over by the host carries no Acornima parse result, so every lane keyed on it
        // has to fall back to the .NET engine rather than dereferencing an absent custom engine.
        var engine = new Engine();
        engine.SetValue("re", new Regex("[a-z]+"));

        engine.Evaluate("re.test('abc')").AsBoolean().Should().BeTrue();
        engine.Evaluate("re.test('123')").AsBoolean().Should().BeFalse();
        engine.Evaluate("JSON.stringify(re.exec('12ab34'))").AsString().Should().Be("""["ab"]""");
        engine.Evaluate("'12ab34'.search(re)").AsNumber().Should().Be(2);
        engine.Evaluate("JSON.stringify('12ab34'.match(re))").AsString().Should().Be("""["ab"]""");
        engine.Evaluate("'12ab34'.replace(re, 'X')").AsString().Should().Be("12X34");

        // split is the one operation out of reach: @@split rebuilds a sticky splitter from `source`,
        // which for a host Regex is deliberately an invalid JS pattern ("?[native regex]").
        Invoking(() => engine.Evaluate("'1a2b3'.split(re)")).Should().Throw<JavaScriptException>();
    }

    [Test]
    public void HostSuppliedRegexReportsCaptureGroups()
    {
        var engine = new Engine();
        engine.SetValue("re", new Regex("([a-z])(b)"));
        engine.SetValue("named", new Regex("(?<first>[a-z])b"));

        engine.Evaluate("JSON.stringify(Array.from(re.exec('xab')))").AsString().Should().Be("""["ab","a","b"]""");
        engine.Evaluate("'xab'.replace(re, '[$1|$2]')").AsString().Should().Be("x[a|b]");
        engine.Evaluate("named.exec('xab').groups.first").AsString().Should().Be("a");
    }

    // ---- RegExp.prototype.test lastIndex handling (https://tc39.es/ecma262/#sec-regexpbuiltinexec) ----

    [TestCase("g")]
    [TestCase("y")]
    [TestCase("gu")]
    public void TestOnEmptySubjectWithLastIndexPastTheEnd(string flags)
    {
        // Step 15.a: lastIndex > length returns null after resetting lastIndex; the empty subject is
        // not special-cased.
        var engine = new Engine();
        var result = engine.Evaluate($"var re = /a/{flags}; re.lastIndex = 5; JSON.stringify([re.test(''), re.lastIndex])");

        result.AsString().Should().Be("[false,0]");
    }

    [TestCase("g")]
    [TestCase("y")]
    public void TestMatchesZeroLengthAtEndOfSubject(string flags)
    {
        // lastIndex == length is a legal start position: only lastIndex > length fails outright.
        var engine = new Engine();
        var result = engine.Evaluate($"var re = /a*/{flags}; re.lastIndex = 3; JSON.stringify([re.test('abc'), re.lastIndex])");

        result.AsString().Should().Be("[true,3]");
    }

    [TestCase("g")]
    [TestCase("y")]
    public void TestResetsLastIndexWhenItIsPastTheEnd(string flags)
    {
        // Step 15.a.i: a global/sticky regexp resets lastIndex before returning null, so the next
        // test() starts over instead of latching false.
        var engine = new Engine();
        var result = engine.Evaluate($"var re = /a/{flags}; re.lastIndex = 10; JSON.stringify([re.test('abc'), re.lastIndex, re.test('abc'), re.lastIndex])");

        result.AsString().Should().Be("[false,0,true,1]");
    }

    [TestCase("")]
    [TestCase("i")]
    [TestCase("u")]
    public void TestLeavesLastIndexAloneOnNonGlobalNonStickyRegExp(string flags)
    {
        // Step 10 sets the *local* lastIndex to 0 for a non-global, non-sticky regexp; the property
        // itself is never written, so a non-writable lastIndex must not raise a TypeError.
        var engine = new Engine();
        var result = engine.Evaluate($$"""
            var re = /a/{{flags}};
            Object.defineProperty(re, 'lastIndex', { value: 5, writable: false });
            JSON.stringify([re.test('abc'), re.lastIndex])
            """);

        result.AsString().Should().Be("[true,5]");
    }
    // ---- RegExp.prototype[Symbol.replace] lastIndex handling
    // (https://tc39.es/ecma262/#sec-regexp.prototype-@@replace) ----

    [TestCase("")]
    [TestCase("i")]
    [TestCase("u")]
    [TestCase("iu")]
    public void ReplaceLeavesLastIndexAloneOnNonGlobalRegExp(string flags)
    {
        // Only step 9 writes lastIndex, and only when the regexp is global. A non-global one is left
        // exactly as the script set it - including a -0 that a write of +0 would quietly normalize.
        var engine = new Engine();
        var result = engine.Evaluate($$"""
            var re = /a/{{flags}};
            re.lastIndex = -0;
            re[Symbol.replace]('a', 'b');
            var kept = /a/{{flags}};
            kept.lastIndex = 7;
            kept[Symbol.replace]('a', 'b');
            JSON.stringify([Object.is(re.lastIndex, -0), kept.lastIndex])
            """);

        result.AsString().Should().Be("[true,7]");
    }

    [Test]
    public void ReplaceLeavesLastIndexWhereAReplacerFunctionPutIt()
    {
        // The exec loop of steps 11-12 runs to exhaustion before the first replacer call, so by then
        // lastIndex is back to +0 and nothing may overwrite what the replacer assigns afterwards.
        var engine = new Engine();
        var result = engine.Evaluate("""
            var re = /x/g;
            re.lastIndex = 3;
            var seen = [];
            '0x2x4x6x8'.replace(re, function () { seen.push(re.lastIndex++); return 'y'; });
            JSON.stringify([seen, re.lastIndex])
            """);

        result.AsString().Should().Be("[[0,1,2,3],4]");
    }

    [TestCase("", true)]
    [TestCase("y", true)]
    [TestCase("g", false)]
    [TestCase("gy", false)]
    public void ReplaceCoercesLastIndexOnNonGlobalRegExp(string flags, bool expectedCoercion)
    {
        // RegExpBuiltinExec step 2 coerces lastIndex whatever the flags say, and only a global regexp
        // has had that value replaced by the +0 of step 9 before the coercion could run.
        var engine = new Engine();
        var result = engine.Evaluate($$"""
            var re = new RegExp('a', '{{flags}}');
            var called = false;
            re.lastIndex = { valueOf: function () { called = true; return 0; } };
            re[Symbol.replace]('', '');
            called
            """);

        result.AsBoolean().Should().Be(expectedCoercion);
    }

    [Test]
    public void ReplaceHonoursARecompileFromTheLastIndexCoercion()
    {
        // Step 2 runs before step 3 reads the flags and step 8 reads the matcher, so a compile() from
        // inside the coercion decides which pattern the very same exec goes on to match with.
        var engine = new Engine();
        var result = engine.Evaluate("""
            var re = new RegExp('a', '');
            re.lastIndex = { valueOf: function () { re.compile('b'); return 0; } };
            var replaced = re[Symbol.replace]('b', 'pass');

            var toGlobal = new RegExp('a', '');
            toGlobal.lastIndex = { valueOf: function () { toGlobal.compile('a', 'g'); return 0; } };
            toGlobal[Symbol.replace]('a', '');

            var fromSticky = new RegExp('a', 'y');
            fromSticky.lastIndex = { valueOf: function () { fromSticky.compile('a', ''); fromSticky.lastIndex = 9000; return 0; } };
            fromSticky[Symbol.replace]('a', '');

            JSON.stringify([replaced, toGlobal.lastIndex, fromSticky.lastIndex])
            """);

        result.AsString().Should().Be("""["pass",1,9000]""");
    }

    [Test]
    public void ReplaceStartsAStickyRegExpAtItsLastIndex()
    {
        // A sticky match is anchored at lastIndex and writes the end position back. The non-ASCII
        // pattern is what routes this through the custom engine, whose fast path used to scan from
        // the start of the subject regardless.
        var engine = new Engine();
        var result = engine.Evaluate("""
            var re = /µ/iy;
            re.lastIndex = 1;
            JSON.stringify([re[Symbol.replace]('Xµ', 'Z'), re.lastIndex])
            """);

        result.AsString().Should().Be("""["XZ",2]""");
    }

    // ---- RegExp.prototype.exec replaced by an accessor (https://tc39.es/ecma262/#sec-regexpexec) ----

    [Test]
    public void ReplacingExecWithAnAccessorDoesNotInvokeItOnThePrototype()
    {
        // Deciding whether exec is still the built-in must not perform an ordinary [[Get]]: that calls
        // a script-installed getter an extra time, with RegExp.prototype as the receiver rather than
        // the regexp being matched.
        var engine = new Engine();
        var result = engine.Evaluate("""
            var builtinExec = RegExp.prototype.exec;
            var receivers = [];
            Object.defineProperty(RegExp.prototype, 'exec', {
                configurable: true,
                get: function () { receivers.push(this); return builtinExec; }
            });
            var re = /a+/g;
            var matched = 'aabbaa'.match(re);
            Object.defineProperty(RegExp.prototype, 'exec', {
                value: builtinExec, writable: true, enumerable: false, configurable: true
            });
            JSON.stringify([matched, receivers.length, receivers.every(function (r) { return r === re; })])
            """);

        result.AsString().Should().Be("""[["aa","aa"],3,true]""");
    }

    // ---- Case-insensitive matching across the ASCII/non-ASCII boundary
    // (https://tc39.es/ecma262/#sec-runtime-semantics-canonicalize-ch) ----

    // Canonicalize is toUpperCase in non-Unicode mode, so U+00B5 MICRO SIGN and U+039C GREEK CAPITAL
    // MU share a canonical value and match either way round.
    [TestCase(@"/\xB5/i", 0x039C, true)]
    [TestCase(@"/Μ/i", 0x00B5, true)]
    // The Latin-1 pair whose uppercase stays non-ASCII matches too.
    [TestCase(@"/\xFF/i", 0x0178, true)]
    // ... but step 9 keeps a character at or above U+0080 whose uppercase is ASCII to itself, so these
    // pairs stay distinct however alike .NET's IgnoreCase considers them.
    [TestCase(@"/\x6B/i", 0x212A, false)]
    [TestCase("/k/i", 0x212A, false)]
    [TestCase("/K/i", 0x212A, false)]
    [TestCase("/[a-z]/i", 0x212A, false)]
    [TestCase("/[j-l]/i", 0x212A, false)]
    // ... and the word-character set contains them, so it carries the same hazard.
    [TestCase(@"/\w/i", 0x212A, false)]
    [TestCase(@"/\x73/i", 0x017F, false)]
    [TestCase(@"/\xDF/i", 0x1E9E, false)]
    [TestCase(@"/\xE5/i", 0x212B, false)]
    public void CaseInsensitiveMatchingUsesCanonicalizeNotDotNetCaseFolding(string pattern, int subject, bool expected)
    {
        var engine = new Engine();
        var result = engine.Evaluate($"{pattern}.test(String.fromCharCode({subject}))");

        result.AsBoolean().Should().Be(expected);
    }

    // In Unicode mode Canonicalize is Simple Case Folding, and the two non-ASCII characters that fold
    // into ASCII must be found even though the first-character scan searches for ASCII code units.
    [TestCase("/s/iu", 0x017F, 1)]
    [TestCase("/S/iu", 0x017F, 1)]
    [TestCase("/k/iu", 0x212A, 1)]
    [TestCase("/K/iu", 0x212A, 1)]
    [TestCase("/s+/iu", 0x017F, 2)]
    public void UnicodeCaseFoldingFindsTheNonAsciiFoldingPartners(string pattern, int subject, int repeat)
    {
        var engine = new Engine();
        var result = engine.Evaluate(
            $"{pattern}.exec(String.fromCharCode({subject}).repeat({repeat}))[0].length");

        result.AsNumber().Should().Be(repeat);
    }

    [Test]
    public void UnicodeCaseFoldingFindsAFoldingPartnerInsideALiteralPrefix()
    {
        // The multi-code-unit scan path searches for the whole literal with OrdinalIgnoreCase, which
        // knows nothing about the folding partners either.
        var engine = new Engine();
        var result = engine.Evaluate("""
            JSON.stringify([/as/iu.exec('xxaſ')[0], /sa/iu.exec('xxſa')[0]])
            """);

        result.AsString().Should().Be("[\"aſ\",\"ſa\"]");
    }
    // The word-boundary assertions classify their neighbours with .NET's own word-character set,
    // which under IgnoreCase takes in U+0130 LATIN CAPITAL LETTER I WITH DOT ABOVE. The spec's
    // WordCharacters gains nothing in non-Unicode mode, so U+0130 is a boundary on both sides.
    [TestCase(@"/a\b/i", "aİ", true)]
    [TestCase(@"/a\B/i", "aİ", false)]
    [TestCase(@"/\bk/i", "İk", true)]
    [TestCase(@"/\Bk/i", "İk", false)]
    public void WordBoundariesTreatTheDottedCapitalIAsANonWordCharacter(string pattern, string subject, bool expected)
    {
        var engine = new Engine();
        var result = engine.Evaluate($"{pattern}.test({ToJsLiteral(subject)})");

        result.AsBoolean().Should().Be(expected);
    }

    // \W is the one construct the .NET adaptation expands into a positive class spanning the whole
    // BMP while excluding the ASCII word characters. Closing that class under IgnoreCase pulls the
    // partners of its non-ASCII members back in, so /\W/i matched 'k' on .NET 7+ and 'I' on .NET
    // Framework - a divergence a pure-ASCII subject already shows.
    [TestCase(@"/\W/i", "k", false)]
    [TestCase(@"/\W/i", "K", false)]
    [TestCase(@"/\W/i", "i", false)]
    [TestCase(@"/\W/i", "I", false)]
    [TestCase(@"/[\W]/i", "k", false)]
    [TestCase(@"/\W/i", "-", true)]
    public void NegatedWordClassDoesNotTakeInItsMembersCasePartners(string pattern, string subject, bool expected)
    {
        var engine = new Engine();
        var result = engine.Evaluate($"{pattern}.test({ToJsLiteral(subject)})");

        result.AsBoolean().Should().Be(expected);
    }

    [Test]
    public void OneRegExpAlternatesBetweenEnginesAcrossSubjects()
    {
        // The engine is chosen per subject, so the same instance has to answer correctly whichever
        // subject it sees next - including after a compile() drops what it had determined.
        var engine = new Engine();
        var result = engine.Evaluate("""
            var kelvin = String.fromCharCode(0x212A);
            var re = /[a-z]/i;
            var answers = [re.test('Q'), re.test(kelvin), re.test('Q'), re.test(kelvin), re.test('4')];
            re.compile('[0-9]', 'i');
            answers.push(re.test(kelvin), re.test('4'), re.test('Q'));
            JSON.stringify(answers)
            """);

        result.AsString().Should().Be("[true,false,true,false,false,false,true,false]");
    }

    [Test]
    public void GlobalMatchOverASubjectCarryingATriggerStaysCorrectForEveryMatch()
    {
        // The per-subject probe is memoized by string instance so a match loop scans the subject once;
        // the memo must not let the first answer stand for a different subject.
        var engine = new Engine();
        var result = engine.Evaluate("""
            var kelvin = String.fromCharCode(0x212A);
            var re = /[a-z]+/gi;
            var withTrigger = 'ab' + kelvin + 'cd';
            var first = withTrigger.match(re);
            var second = 'abcd'.match(re);
            var third = withTrigger.match(re);
            JSON.stringify([first, second, third])
            """);

        result.AsString().Should().Be("""[["ab","cd"],["abcd"],["ab","cd"]]""");
    }

    // Routing pin. A case-insensitive pattern that merely mentions 'k', covers it with a range, or
    // uses \w keeps the .NET engine and is diverted per subject; only the verdict that holds for
    // every subject takes a pattern away from it. Losing this would put the most common
    // case-insensitive patterns in real scripts on the bytecode interpreter for every match.
    [TestCase("[a-z0-9]+", "i", false)]
    [TestCase(@"\w+", "i", false)]
    [TestCase("k", "i", false)]
    [TestCase("check", "i", false)]
    [TestCase(@"\bfoo\b", "i", false)]
    [TestCase(@"\x6B", "i", false)]
    // ... while non-ASCII pattern content and \W do, on every subject.
    [TestCase(@"\xB5", "i", true)]
    [TestCase(@"Μ", "i", true)]
    [TestCase(@"\W", "i", true)]
    [TestCase("[a-z]", "u", true)]
    public void CaseInsensitivePatternsKeepTheDotNetEngineUnlessTheyDivergeOnEverySubject(
        string pattern, string flags, bool needsCustomEngine)
    {
        Jint.Native.RegExp.RegExpConstructor.NeedCustomEngine(pattern, flags).Should().Be(needsCustomEngine);
    }

    private static string ToJsLiteral(string value)
    {
        var builder = new System.Text.StringBuilder(value.Length * 6 + 2);
        builder.Append('\'');
        foreach (var c in value)
        {
            builder.Append("\\u").Append(((int) c).ToString("x4", System.Globalization.CultureInfo.InvariantCulture));
        }

        return builder.Append('\'').ToString();
    }
}
