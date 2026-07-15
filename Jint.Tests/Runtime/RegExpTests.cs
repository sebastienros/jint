using System.Diagnostics;
using System.Text.RegularExpressions;
using Jint.Native;

namespace Jint.Tests.Runtime;

public class RegExpTests
{
    [Fact]
    public void MatchGlobalUnicodeNoMatchesReturnsNull()
    {
        var engine = new Engine();
        var result = engine.Evaluate("'abc'.match(/\\d/gu) === null").AsBoolean();

        Assert.True(result);
    }

    [Fact]
    public void MatchGlobalUnicodeCollectsAllMatches()
    {
        var engine = new Engine();
        var result = engine.Evaluate("JSON.stringify('a1b22c333'.match(/\\d+/gu))").AsString();

        Assert.Equal("[\"1\",\"22\",\"333\"]", result);
    }

    [Fact]
    public void MatchGlobalUnicodeEmptyMatchesAdvanceByCodePoint()
    {
        var engine = new Engine();
        // 2 astral code points (4 UTF-16 units): empty matches at positions 0, 2, 4
        var result = engine.Evaluate("'\\u{1F600}\\u{1F600}'.match(/(?:)/gu).length").AsNumber();

        Assert.Equal(3, result);
    }

    [Theory]
    // A capturing group nested inside a quantified non-capturing group routes the pattern to
    // the custom (QuickJS-port) regex engine. Its greedy Char+/Range+ bulk-advance optimization
    // must not fire when the loop body has more than the single leading char/range atom,
    // otherwise trailing iterations get dropped (e.g. "abcabc" matched as "abca").
    [InlineData("(?:a(b)c)+", "abcabc", "[\"abcabc\",\"b\"]")]
    [InlineData("(?:a(b)c)+", "abc", "[\"abc\",\"b\"]")]
    [InlineData("(?:x(y)z)+", "xyzxyzxyz", "[\"xyzxyzxyz\",\"y\"]")]
    [InlineData("(?:a(b)c)+", "zzabcabc", "[\"abcabc\",\"b\"]")]
    [InlineData("(a(b)c)+", "abcabc", "[\"abcabc\",\"abc\",\"b\"]")]
    [InlineData("(?:(a)(b))+", "abab", "[\"abab\",\"a\",\"b\"]")]
    [InlineData("(?:([ab])(x))+", "axbx", "[\"axbx\",\"b\",\"x\"]")]
    // Range as the leading atom of a multi-atom body exercises the Range+ bulk-advance guard.
    [InlineData("(?:[a-c](x))+", "axbx", "[\"axbx\",\"x\"]")]
    // Single char/range body: the bulk-advance optimization SHOULD still apply and stay correct.
    [InlineData("(a)+", "aaa", "[\"aaa\",\"a\"]")]
    [InlineData("([a-z])+", "abc", "[\"abc\",\"c\"]")]
    public void MatchesNestedCaptureInsideQuantifiedGroup(string pattern, string input, string expected)
    {
        var engine = new Engine();
        var result = engine.Evaluate($"JSON.stringify(/{pattern}/.exec({JsonString(input)}))").AsString();

        Assert.Equal(expected, result);
    }

    private static string JsonString(string s) => System.Text.Json.JsonSerializer.Serialize(s);

    [Theory]
    [InlineData("gy")]
    [InlineData("guy")]
    public void MatchStickyGlobalCollectsAllAdjacentMatches(string flags)
    {
        // without 'u' exercises the .NET sticky fast path, with 'u' the generic exec loop
        var engine = new Engine();
        var result = engine.Evaluate($"JSON.stringify(['aaa'.match(/a/{flags}), 'ababab'.match(/ab/{flags})])").AsString();

        Assert.Equal("[[\"a\",\"a\",\"a\"],[\"ab\",\"ab\",\"ab\"]]", result);
    }

    [Theory]
    [InlineData("gy")]
    [InlineData("guy")]
    public void MatchStickyGlobalStopsAtFirstGap(string flags)
    {
        // matches after the gap exist but are not adjacent, so sticky matching must not include them
        var engine = new Engine();
        var result = engine.Evaluate($"JSON.stringify(['aabaa'.match(/a/{flags}), 'baa'.match(/a/{flags})])").AsString();

        Assert.Equal("[[\"a\",\"a\"],null]", result);
    }

    [Theory]
    [InlineData("gy")]
    [InlineData("guy")]
    public void MatchStickyGlobalAdvancesOverEmptyMatches(string flags)
    {
        var engine = new Engine();
        var result = engine.Evaluate($"JSON.stringify('aab'.match(/a*/{flags}))").AsString();

        Assert.Equal("[\"aa\",\"\",\"\"]", result);
    }

    [Fact]
    public void MatchStickyGlobalResetsLastIndex()
    {
        var engine = new Engine();
        var result = engine.Evaluate("var r = /a/gy; r.lastIndex = 2; JSON.stringify(['aaa'.match(r), r.lastIndex])").AsString();

        Assert.Equal("[[\"a\",\"a\",\"a\"],0]", result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("u")]
    public void SplitCollectsSegmentsCapturesAndTail(string flags)
    {
        // without flags exercises the .NET fast path, with 'u' the generic exec loop
        var engine = new Engine();
        var result = engine.Evaluate($"JSON.stringify('a1b22c'.split(/(\\d+)/{flags}))").AsString();

        Assert.Equal("[\"a\",\"1\",\"b\",\"22\",\"c\"]", result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("u")]
    public void SplitHonorsLimit(string flags)
    {
        var engine = new Engine();
        var result = engine.Evaluate($"JSON.stringify(['a,b,c'.split(/,/{flags}, 2), 'a1b2c'.split(/(\\d)/{flags}, 2)])").AsString();

        Assert.Equal("[[\"a\",\"b\"],[\"a\",\"1\"]]", result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("u")]
    public void SplitKeepsEmptyLeadingAndTrailingSegments(string flags)
    {
        var engine = new Engine();
        var result = engine.Evaluate($"JSON.stringify([',a,'.split(/,/{flags}), ''.split(/x/{flags}), ''.split(/(?:)/{flags})])").AsString();

        Assert.Equal("[[\"\",\"a\",\"\"],[\"\"],[]]", result);
    }

    private const string TestRegex = "^(https?:\\/\\/)?([\\da-z\\.-]+)\\.([a-z\\.]{2,6})([\\/\\w\\.-]*)*\\/?$";
    private const string TestedValue = "https://archiverbx.blob.core.windows.net/static/C:/Users/USR/Documents/Projects/PROJ/static/images/full/1234567890.jpg";

    [Fact]
    public void CanNotBreakEngineWithLongRunningMatch()
    {
        var engine = new Engine(e => e.RegexTimeoutInterval(TimeSpan.FromSeconds(1)));

        Assert.Throws<RegexMatchTimeoutException>(() =>
        {
            engine.Execute($"'{TestedValue}'.match(/{TestRegex}/)");
        });
    }

    [Fact]
    public void CanNotBreakEngineWithLongRunningRegExp()
    {
        var engine = new Engine(e => e.RegexTimeoutInterval(TimeSpan.FromSeconds(1)));

        Assert.Throws<RegexMatchTimeoutException>(() =>
        {
            engine.Execute($"'{TestedValue}'.match(new RegExp(/{TestRegex}/))");
        });
    }

    [Fact]
    public void PreventsInfiniteLoop()
    {
        var engine = new Engine();
        var result = (JsArray) engine.Evaluate("'x'.match(/|/g);");
        Assert.Equal((uint) 2, result.Length);
        Assert.Equal("", result[0]);
        Assert.Equal("", result[1]);
    }

    [Fact]
    public void ToStringWithNonRegExpInstanceAndMissingProperties()
    {
        var engine = new Engine();
        var result = engine.Evaluate("/./['toString'].call({})").AsString();

        Assert.Equal("/undefined/undefined", result);
    }

    [Fact]
    public void ToStringWithNonRegExpInstanceAndValidProperties()
    {
        var engine = new Engine();
        var result = engine.Evaluate("/./['toString'].call({ source: 'a', flags: 'b' })").AsString();

        Assert.Equal("/a/b", result);
    }

    [Fact]
    public void MatchAllIteratorReturnsCorrectNumberOfElements()
    {
        var engine = new Engine();
        var result = engine.Evaluate("[...'one two three'.matchAll(/t/g)].length").AsInteger();

        Assert.Equal(2, result);
    }

    [Fact]
    public void ToStringWithRealRegExpInstance()
    {
        var engine = new Engine();
        var result = engine.Evaluate("/./['toString'].call(/test/g)").AsString();

        Assert.Equal("/test/g", result);
    }

    [Fact]
    public void ToStringPreserversOriginalPatternOfLiteral()
    {
        var engine = new Engine();
        var result = engine.Evaluate("/^x\\/\\\\r\\n\\u2028\\u2029\\0\0|[x/\\\\r\\n\\u2028\\u2029\\0\0]$/");

        var jsRegExp = Assert.IsType<JsRegExp>(result);
        Assert.Equal("^x\\/\\\\r\\n\\u2028\\u2029\\0\0|[x/\\\\r\\n\\u2028\\u2029\\0\0]$", jsRegExp.Source);
        Assert.Equal("/^x\\/\\\\r\\n\\u2028\\u2029\\0\0|[x/\\\\r\\n\\u2028\\u2029\\0\0]$/", jsRegExp.ToString());
    }

    [Fact]
    public void ToStringCorrectlyEscapesProblematicCharacters()
    {
        var engine = new Engine();
        var result = engine.Evaluate(@"new RegExp('^x/\\\r\n\u2028\u2029\\0\0|[x/\\\r\n\u2028\u2029\\0\0]$')");

        var jsRegExp = Assert.IsType<JsRegExp>(result);
        Assert.Equal("^x\\/\\r\\n\\u2028\\u2029\\0\0|[x/\\r\\n\\u2028\\u2029\\0\0]$", jsRegExp.Source);
        Assert.Equal("/^x\\/\\r\\n\\u2028\\u2029\\0\0|[x/\\r\\n\\u2028\\u2029\\0\0]$/", jsRegExp.ToString());
    }

    [Fact]
    public void ShouldNotThrowErrorOnIncompatibleRegex()
    {
        var engine = new Engine();
        Assert.NotNull(engine.Evaluate(@"/[^]*?(:[rp][el]a[\w-]+)[^]*/"));
        Assert.NotNull(engine.Evaluate("/[^]a/"));
        Assert.NotNull(engine.Evaluate("new RegExp('[^]a')"));

        Assert.NotNull(engine.Evaluate("/[]/"));
        Assert.NotNull(engine.Evaluate("new RegExp('[]')"));
    }

    [Fact]
    public void ShouldNotThrowErrorOnRegExNumericNegation()
    {
        var engine = new Engine();
        Assert.True(ReferenceEquals(JsNumber.DoubleNaN, engine.Evaluate("-/[]/")));
    }

    [Fact]
    public void ShouldProduceCorrectSourceForSlashEscapes()
    {
        var engine = new Engine();
        var source = engine.Evaluate(@"/\/\//.source");
        Assert.Equal("\\/\\/", source);
    }

    [Theory]
    [InlineData("", "/()/ug", new[] { "" }, new[] { 0 })]
    [InlineData("💩", "/()/ug", new[] { "", "" }, new[] { 0, 2 })]
    [InlineData("ᴜⁿᵢ𝒸ₒᵈₑ is a 💩", "/i?/ug",
        new[] { "", "", "", "", "", "", "", "", "i", "", "", "", "", "", "" },
        new[] { 0, 1, 2, 3, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 16 })]
    public void ShouldNotMatchEmptyStringsWithinSurrogatePairsInUnicodeMode(string input, string pattern, string[] expectedCaptures, int[] expectedIndices)
    {
        var engine = new Engine();
        var matches = engine.Evaluate($"[...'{input}'.matchAll({pattern})]").AsArray();
        Assert.Equal((ulong) expectedCaptures.Length, matches.Length);
        Assert.Equal(expectedCaptures, matches.Select((m, i) => m.Get(0).AsString()));
        Assert.Equal(expectedIndices, matches.Select(m => m.Get("index").AsInteger()));
    }

    [Fact]
    public void ShouldAllowProblematicGroupNames()
    {
        var engine = new Engine();

        var match = engine.Evaluate("'abc'.match(/(?<$group>b)/)").AsArray();
        var groups = match.Get("groups").AsObject();
        Assert.Equal(["$group"], groups.GetOwnPropertyKeys().Select(k => k.AsString()));
        Assert.Equal("b", groups["$group"]);

        var result = engine.Evaluate("'abc'.replace(/(?<$group>b)/g, '-$<$group>-')").AsString();
        Assert.Equal("a-b-c", result);
    }

    [Fact]
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

    [Fact]
    public void ShouldAllowClassSetSyntaxCharacterOutsideClassSetForFlagV()
    {
        var engine = new Engine();
        var result = engine.Evaluate(@"new RegExp('/-', 'v').test('/-')");
        Assert.True(result.AsBoolean());
    }

    [Fact]
    public void ShouldAllowClassSetReservedDoublePunctuatorCharactersOutsideClassSetForFlagV()
    {
        var engine = new Engine();
        var result = engine.Evaluate(@"new RegExp('&&!!##%%,,::;;<<==>>@@``~~', 'v').test('&&!!##%%,,::;;<<==>>@@``~~')");
        Assert.True(result.AsBoolean());
    }

    [Fact]
    public void ShouldAllowEscapedClassSetReservedPunctuatorsForFlagV()
    {
        var engine = new Engine();
        var result = engine.Evaluate(@"new RegExp('[\\!\\#\\%\\&\\,\\-\\:\\;\\<\\=\\>\\@\\`\\~]{14}', 'v').test('!#%&,-:;<=>@`~')");
        Assert.True(result.AsBoolean());
    }

    [Fact]
    public void Issue506()
    {
        var engine = new Engine();
        var result = engine.Evaluate("/[^]?(:[rp][el]a[\\w-]+)[^]/.test(':reagent-')").AsBoolean();
        Assert.True(result);
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

    [Theory]
    // string-tagcloud.js parseJSON patterns (hot in benchmarks)
    [InlineData(@"""[^""\\\n\r]*""|true|false|null|-?\d+(?:\.\d*)?(:?[eE][+\-]?\d+)?", "g")]
    [InlineData(@"(?:^|:|,)(?:\s*\[)+", "g")]
    // string-unpack-code.js pattern
    [InlineData(@"\b\w+\b", "g")]
    // quantified non-capturing groups without captures/lookarounds whose body cannot match
    // the empty string are .NET-safe
    [InlineData(@"(?:ab)+", "")]
    // quantified capturing groups whose body cannot match the empty string are .NET-safe
    [InlineData(@"(a+)+", "")]
    [InlineData(@"((?:a|b)c)*", "")]
    [InlineData(@"(:?[eE][+\-]?\d+)?", "")]
    public void UsesDotNetRegexWhenPatternIsTranslatable(string pattern, string flags)
    {
        var engine = new Engine();
        var regExp = (JsRegExp) engine.Realm.Intrinsics.RegExp.Construct([pattern, flags]);

        Assert.True(regExp.UsesDotNetEngine);
    }

    [Theory]
    // capturing group inside a quantified group: .NET retains captures across iterations
    [InlineData(@"((a)|b)+", "")]
    [InlineData(@"(?:(a)|b)+", "")]
    // quantified capturing group whose body can match the empty string: .NET records empty captures
    [InlineData(@"(a*)*", "")]
    [InlineData(@"(a|)*", "")]
    [InlineData(@"(\b)*", "")]
    [InlineData(@"(a{0,2})+", "")]
    [InlineData(@"((?:a*))*", "")]
    // quantified non-capturing group whose body can match the empty string: .NET's empty-loop
    // protection stops at the first empty/zero-width alternative, ECMAScript backtracks into a
    // later consuming alternative (the match itself diverges)
    [InlineData(@"(?:|a)*", "")]
    [InlineData(@"(?:a*|b)*", "")]
    [InlineData(@"(?:a||b)*", "")]
    [InlineData(@"(?:^|a)+", "")]
    [InlineData(@"(?:a??)*", "")]
    // conservatively routed to custom as well (nullable non-capturing body, though .NET agrees here)
    [InlineData(@"(?:a*)+", "")]
    [InlineData(@"(?:a|)+b", "")]
    // quantified lookaround assertions
    [InlineData(@"(?=(a))+", "")]
    [InlineData(@"(?:(?=a).)+", "")]
    // forward backreference
    [InlineData(@"\1(a)", "")]
    // unicode modes and case-insensitive matching of non-ASCII content
    [InlineData("a", "u")]
    [InlineData("a", "v")]
    [InlineData("ä", "i")]
    public void UsesCustomEngineWhenDotNetSemanticsDiverge(string pattern, string flags)
    {
        var engine = new Engine();
        var regExp = (JsRegExp) engine.Realm.Intrinsics.RegExp.Construct([pattern, flags]);

        Assert.False(regExp.UsesDotNetEngine);
    }

    [Fact]
    public void QuantifiedGroupCapturesAreClearedOnEachIteration()
    {
        var engine = new Engine();

        // the last iteration matches 'b', so the inner capture must be undefined
        Assert.Equal("[\"ab\",\"b\",null]", engine.Evaluate("JSON.stringify(/((a)|b)+/.exec('ab'))").AsString());
        Assert.Equal("[\"ab\",null]", engine.Evaluate("JSON.stringify(/(?:(a)|b)+/.exec('ab'))").AsString());
    }

    [Fact]
    public void QuantifiedCapturingGroupRejectsEmptyIterations()
    {
        var engine = new Engine();

        // an iteration matching the empty string fails per RepeatMatcher, so the capture
        // never participates and must not report an empty string
        Assert.Equal("[\"\",null]", engine.Evaluate("JSON.stringify(/(a*)*/.exec('b'))").AsString());
        Assert.Equal("[\"a\",\"a\"]", engine.Evaluate("JSON.stringify(/(a*)*/.exec('ab'))").AsString());
        Assert.Equal("[\"\",null]", engine.Evaluate("JSON.stringify(/(a|)*/.exec('b'))").AsString());
        Assert.Equal("[\"\",null]", engine.Evaluate("JSON.stringify(/(\\b)*/.exec('a'))").AsString());
    }

    [Fact]
    public void QuantifiedNonCapturingGroupWithNullableBodyMatchesPerSpec()
    {
        // Regression: a non-capturing quantified group whose body can reach the empty string
        // before a consuming alternative must not be delegated to .NET Regex, whose empty-loop
        // protection truncates the match. ECMAScript's RepeatMatcher prunes the empty iteration
        // and backtracks into the later consuming alternative, matching fully.
        var engine = new Engine();

        Assert.Equal("aaabbb", engine.Evaluate("/(?:a*|b)*/.exec('aaabbb')[0]").AsString());
        Assert.Equal("aaa", engine.Evaluate("/(?:|a)*/.exec('aaa')[0]").AsString());
        Assert.Equal("aabb", engine.Evaluate("/(?:a||b)*/.exec('aabb')[0]").AsString());
        Assert.Equal("a", engine.Evaluate("/(?:^|a)+/m.exec('abc')[0]").AsString());
        Assert.Equal("aaa", engine.Evaluate("/(?:a??)*/.exec('aaa')[0]").AsString());
        Assert.Equal("aaa", engine.Evaluate("/(?:\\b|a)*/.exec('aaa')[0]").AsString());
        Assert.Equal("XX", engine.Evaluate("'aaa'.replace(/(?:|a)*/g, 'X')").AsString());
    }

    [Theory]
    // without 'u' these route to .NET Regex, with 'u' to the custom engine; results must agree
    [InlineData("")]
    [InlineData("u")]
    public void QuantifiedNonCapturingGroupBehavesTheSameOnBothEngines(string flags)
    {
        var engine = new Engine();

        Assert.Equal("[\":  [[\",\",  [\"]", engine.Evaluate($"JSON.stringify('a:  [[x,  [y'.match(/(?:^|:|,)(?:\\s*\\[)+/g{flags}))").AsString());
        Assert.Equal("[\"12.5e3\",\"7\",\"2E-3\"]", engine.Evaluate($"JSON.stringify('12.5e3 7 2E-3'.match(/-?\\d+(?:\\.\\d*)?(:?[eE][+\\-]?\\d+)?/g{flags}))").AsString());
        Assert.Equal("[\"aa\",\"a\"]", engine.Evaluate($"JSON.stringify('aabxa'.match(/(?:a|)+/g{flags}).filter(function (s) {{ return s !== ''; }}))").AsString());
    }

    [Fact]
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

        Assert.True(result);
    }

    // Regression tests for https://github.com/sebastienros/jint/issues/2454
    //
    // The TestRegex pattern triggers Jint's custom (QuickJS-port) regex engine via
    // RegExpConstructor.NeedCustomEngine — that engine has no built-in match timeout
    // (unlike .NET Regex which embeds MatchTimeout), so each prototype method must
    // honor the prepare-time RegexTimeout when calling the custom engine.

    [Theory]
    // RegExp.prototype[@@match] without /g → slow path (RegExpExec → CustomEngineBuiltinExec).
    [InlineData("'{0}'.match(/{1}/)")]
    // RegExp.prototype[@@match] with /g → custom-engine fast loop in Match().
    [InlineData("'{0}'.match(/{1}/g)")]
    // RegExp.prototype[@@replace] with /g → custom-engine fast loop in Replace().
    [InlineData("'{0}'.replace(/{1}/g, 'X')")]
    // RegExp.prototype.test() → custom-engine IsMatch fast path.
    [InlineData("/{1}/.test('{0}')")]
    // RegExp.prototype[@@search] → custom-engine Execute fast path.
    [InlineData("'{0}'.search(/{1}/)")]
    public void PreparedScriptHonorsRegexTimeoutForCustomEngine(string scriptTemplate)
    {
        var script = scriptTemplate.Replace("{0}", TestedValue).Replace("{1}", TestRegex);
        AssertPrepareTimeRegexTimeoutFires(script);
    }

    [Fact]
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

        // Engine is set to a long timeout so a regression that drops the prepare-time
        // timeout on the floor falls through to ~EngineRegexTimeoutSeconds rather than
        // the modest default — keeping a clear gap above the assertion threshold even
        // on slow Windows CI runners where cancellation lag can be several seconds.
        var engine = new Engine(o => o.RegexTimeoutInterval(TimeSpan.FromSeconds(EngineRegexTimeoutSeconds)));
        engine.Modules.Add("__main__", x => x.AddModule(preparedModule));

        var sw = Stopwatch.StartNew();
        Assert.Throws<RegexMatchTimeoutException>(() => engine.Modules.Import("__main__"));
        sw.Stop();

        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(MaxAcceptableTimeoutSeconds),
            $"Expected RegexMatchTimeoutException within {MaxAcceptableTimeoutSeconds}s, took {sw.Elapsed.TotalSeconds:F1}s");
    }

    private const int EngineRegexTimeoutSeconds = 30;
    private const int MaxAcceptableTimeoutSeconds = 15;
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

        // Engine is set to a long timeout so a regression that drops the prepare-time
        // timeout on the floor falls through to ~EngineRegexTimeoutSeconds rather than
        // the modest default — keeping a clear gap above the assertion threshold even
        // on slow Windows CI runners where cancellation lag can be several seconds.
        var engine = new Engine(o => o.RegexTimeoutInterval(TimeSpan.FromSeconds(EngineRegexTimeoutSeconds)));

        var sw = Stopwatch.StartNew();
        Assert.Throws<RegexMatchTimeoutException>(() => engine.Execute(preparedScript));
        sw.Stop();

        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(MaxAcceptableTimeoutSeconds),
            $"Expected RegexMatchTimeoutException within {MaxAcceptableTimeoutSeconds}s, took {sw.Elapsed.TotalSeconds:F1}s");
    }
}
