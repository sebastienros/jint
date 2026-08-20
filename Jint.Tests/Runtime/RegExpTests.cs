using System.Diagnostics;
using System.Text.RegularExpressions;
using Jint.Native;
using Jint.Runtime;

namespace Jint.Tests.Runtime;

public class RegExpTests
{
    [Fact]
    public void MatchGlobalUnicodeNoMatchesReturnsNull()
    {
        var engine = new Engine();
        var result = engine.Evaluate("'abc'.match(/\\d/gu) === null").AsBoolean();

        result.Should().BeTrue();
    }

    [Fact]
    public void MatchGlobalUnicodeCollectsAllMatches()
    {
        var engine = new Engine();
        var result = engine.Evaluate("JSON.stringify('a1b22c333'.match(/\\d+/gu))").AsString();

        result.Should().Be("[\"1\",\"22\",\"333\"]");
    }

    [Fact]
    public void MatchGlobalUnicodeEmptyMatchesAdvanceByCodePoint()
    {
        var engine = new Engine();
        // 2 astral code points (4 UTF-16 units): empty matches at positions 0, 2, 4
        var result = engine.Evaluate("'\\u{1F600}\\u{1F600}'.match(/(?:)/gu).length").AsNumber();

        result.Should().Be(3);
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

        result.Should().Be(expected);
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

        result.Should().Be("[[\"a\",\"a\",\"a\"],[\"ab\",\"ab\",\"ab\"]]");
    }

    [Theory]
    [InlineData("gy")]
    [InlineData("guy")]
    public void MatchStickyGlobalStopsAtFirstGap(string flags)
    {
        // matches after the gap exist but are not adjacent, so sticky matching must not include them
        var engine = new Engine();
        var result = engine.Evaluate($"JSON.stringify(['aabaa'.match(/a/{flags}), 'baa'.match(/a/{flags})])").AsString();

        result.Should().Be("[[\"a\",\"a\"],null]");
    }

    [Theory]
    [InlineData("gy")]
    [InlineData("guy")]
    public void MatchStickyGlobalAdvancesOverEmptyMatches(string flags)
    {
        var engine = new Engine();
        var result = engine.Evaluate($"JSON.stringify('aab'.match(/a*/{flags}))").AsString();

        result.Should().Be("[\"aa\",\"\",\"\"]");
    }

    [Fact]
    public void MatchStickyGlobalResetsLastIndex()
    {
        var engine = new Engine();
        var result = engine.Evaluate("var r = /a/gy; r.lastIndex = 2; JSON.stringify(['aaa'.match(r), r.lastIndex])").AsString();

        result.Should().Be("[[\"a\",\"a\",\"a\"],0]");
    }

    [Theory]
    [InlineData("")]
    [InlineData("u")]
    public void SplitCollectsSegmentsCapturesAndTail(string flags)
    {
        // without flags exercises the .NET fast path, with 'u' the generic exec loop
        var engine = new Engine();
        var result = engine.Evaluate($"JSON.stringify('a1b22c'.split(/(\\d+)/{flags}))").AsString();

        result.Should().Be("[\"a\",\"1\",\"b\",\"22\",\"c\"]");
    }

    [Theory]
    [InlineData("")]
    [InlineData("u")]
    public void SplitHonorsLimit(string flags)
    {
        var engine = new Engine();
        var result = engine.Evaluate($"JSON.stringify(['a,b,c'.split(/,/{flags}, 2), 'a1b2c'.split(/(\\d)/{flags}, 2)])").AsString();

        result.Should().Be("[[\"a\",\"b\"],[\"a\",\"1\"]]");
    }

    [Theory]
    [InlineData("")]
    [InlineData("u")]
    public void SplitKeepsEmptyLeadingAndTrailingSegments(string flags)
    {
        var engine = new Engine();
        var result = engine.Evaluate($"JSON.stringify([',a,'.split(/,/{flags}), ''.split(/x/{flags}), ''.split(/(?:)/{flags})])").AsString();

        result.Should().Be("[[\"\",\"a\",\"\"],[\"\"],[]]");
    }

    private const string TestRegex = "^(https?:\\/\\/)?([\\da-z\\.-]+)\\.([a-z\\.]{2,6})([\\/\\w\\.-]*)*\\/?$";
    private const string TestedValue = "https://archiverbx.blob.core.windows.net/static/C:/Users/USR/Documents/Projects/PROJ/static/images/full/1234567890.jpg";

    [Fact]
    public void CanNotBreakEngineWithLongRunningMatch()
    {
        var engine = new Engine(e => e.RegexTimeoutInterval(TimeSpan.FromSeconds(1)));

        Invoking(() =>
        {
            engine.Execute($"'{TestedValue}'.match(/{TestRegex}/)");
        }).Should().ThrowExactly<RegexMatchTimeoutException>();
    }

    [Fact]
    public void CanNotBreakEngineWithLongRunningRegExp()
    {
        var engine = new Engine(e => e.RegexTimeoutInterval(TimeSpan.FromSeconds(1)));

        Invoking(() =>
        {
            engine.Execute($"'{TestedValue}'.match(new RegExp(/{TestRegex}/))");
        }).Should().ThrowExactly<RegexMatchTimeoutException>();
    }

    [Fact]
    public void PreventsInfiniteLoop()
    {
        var engine = new Engine();
        var result = (JsArray) engine.Evaluate("'x'.match(/|/g);");
        result.Length.Should().Be((uint) 2);
        result[0].Should().Be("");
        result[1].Should().Be("");
    }

    [Fact]
    public void ToStringWithNonRegExpInstanceAndMissingProperties()
    {
        var engine = new Engine();
        var result = engine.Evaluate("/./['toString'].call({})").AsString();

        result.Should().Be("/undefined/undefined");
    }

    [Fact]
    public void ToStringWithNonRegExpInstanceAndValidProperties()
    {
        var engine = new Engine();
        var result = engine.Evaluate("/./['toString'].call({ source: 'a', flags: 'b' })").AsString();

        result.Should().Be("/a/b");
    }

    [Fact]
    public void MatchAllIteratorReturnsCorrectNumberOfElements()
    {
        var engine = new Engine();
        var result = engine.Evaluate("[...'one two three'.matchAll(/t/g)].length").AsInteger();

        result.Should().Be(2);
    }

    [Fact]
    public void ToStringWithRealRegExpInstance()
    {
        var engine = new Engine();
        var result = engine.Evaluate("/./['toString'].call(/test/g)").AsString();

        result.Should().Be("/test/g");
    }

    [Fact]
    public void ToStringPreserversOriginalPatternOfLiteral()
    {
        var engine = new Engine();
        var result = engine.Evaluate("/^x\\/\\\\r\\n\\u2028\\u2029\\0\0|[x/\\\\r\\n\\u2028\\u2029\\0\0]$/");

        var jsRegExp = result.Should().BeOfType<JsRegExp>().Which;
        jsRegExp.Source.Should().Be("^x\\/\\\\r\\n\\u2028\\u2029\\0\0|[x/\\\\r\\n\\u2028\\u2029\\0\0]$");
        jsRegExp.ToString().Should().Be("/^x\\/\\\\r\\n\\u2028\\u2029\\0\0|[x/\\\\r\\n\\u2028\\u2029\\0\0]$/");
    }

    [Fact]
    public void ToStringCorrectlyEscapesProblematicCharacters()
    {
        var engine = new Engine();
        var result = engine.Evaluate(@"new RegExp('^x/\\\r\n\u2028\u2029\\0\0|[x/\\\r\n\u2028\u2029\\0\0]$')");

        var jsRegExp = result.Should().BeOfType<JsRegExp>().Which;
        jsRegExp.Source.Should().Be("^x\\/\\r\\n\\u2028\\u2029\\0\0|[x/\\r\\n\\u2028\\u2029\\0\0]$");
        jsRegExp.ToString().Should().Be("/^x\\/\\r\\n\\u2028\\u2029\\0\0|[x/\\r\\n\\u2028\\u2029\\0\0]$/");
    }

    [Fact]
    public void ShouldNotThrowErrorOnIncompatibleRegex()
    {
        var engine = new Engine();
        engine.Evaluate(@"/[^]*?(:[rp][el]a[\w-]+)[^]*/").Should().NotBeNull();
        engine.Evaluate("/[^]a/").Should().NotBeNull();
        engine.Evaluate("new RegExp('[^]a')").Should().NotBeNull();

        engine.Evaluate("/[]/").Should().NotBeNull();
        engine.Evaluate("new RegExp('[]')").Should().NotBeNull();
    }

    [Fact]
    public void ShouldNotThrowErrorOnRegExNumericNegation()
    {
        var engine = new Engine();
        ReferenceEquals(JsNumber.DoubleNaN, engine.Evaluate("-/[]/")).Should().BeTrue();
    }

    [Fact]
    public void ShouldProduceCorrectSourceForSlashEscapes()
    {
        var engine = new Engine();
        var source = engine.Evaluate(@"/\/\//.source");
        source.Should().Be("\\/\\/");
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
        matches.Length.Should().Be((uint) expectedCaptures.Length);
        matches.Select((m, i) => m.Get(0).AsString()).Should().Equal(expectedCaptures);
        matches.Select(m => m.Get("index").AsInteger()).Should().Equal(expectedIndices);
    }

    [Fact]
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
        result.AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void ShouldAllowClassSetReservedDoublePunctuatorCharactersOutsideClassSetForFlagV()
    {
        var engine = new Engine();
        var result = engine.Evaluate(@"new RegExp('&&!!##%%,,::;;<<==>>@@``~~', 'v').test('&&!!##%%,,::;;<<==>>@@``~~')");
        result.AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void ShouldAllowEscapedClassSetReservedPunctuatorsForFlagV()
    {
        var engine = new Engine();
        var result = engine.Evaluate(@"new RegExp('[\\!\\#\\%\\&\\,\\-\\:\\;\\<\\=\\>\\@\\`\\~]{14}', 'v').test('!#%&,-:;<=>@`~')");
        result.AsBoolean().Should().BeTrue();
    }

    [Fact]
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

        regExp.UsesDotNetEngine.Should().BeTrue();
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

        regExp.UsesDotNetEngine.Should().BeFalse();
    }

    [Fact]
    public void QuantifiedGroupCapturesAreClearedOnEachIteration()
    {
        var engine = new Engine();

        // the last iteration matches 'b', so the inner capture must be undefined
        engine.Evaluate("JSON.stringify(/((a)|b)+/.exec('ab'))").AsString().Should().Be("[\"ab\",\"b\",null]");
        engine.Evaluate("JSON.stringify(/(?:(a)|b)+/.exec('ab'))").AsString().Should().Be("[\"ab\",null]");
    }

    [Fact]
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

    [Fact]
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

    [Theory]
    // without 'u' these route to .NET Regex, with 'u' to the custom engine; results must agree
    [InlineData("")]
    [InlineData("u")]
    public void QuantifiedNonCapturingGroupBehavesTheSameOnBothEngines(string flags)
    {
        var engine = new Engine();

        engine.Evaluate($"JSON.stringify('a:  [[x,  [y'.match(/(?:^|:|,)(?:\\s*\\[)+/g{flags}))").AsString().Should().Be("[\":  [[\",\",  [\"]");
        engine.Evaluate($"JSON.stringify('12.5e3 7 2E-3'.match(/-?\\d+(?:\\.\\d*)?(:?[eE][+\\-]?\\d+)?/g{flags}))").AsString().Should().Be("[\"12.5e3\",\"7\",\"2E-3\"]");
        engine.Evaluate($"JSON.stringify('aabxa'.match(/(?:a|)+/g{flags}).filter(function (s) {{ return s !== ''; }}))").AsString().Should().Be("[\"aa\",\"a\"]");
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

        result.Should().BeTrue();
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
        Invoking(() => engine.Modules.Import("__main__")).Should().ThrowExactly<RegexMatchTimeoutException>();
        sw.Stop();

        sw.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(MaxAcceptableTimeoutSeconds), $"Expected RegexMatchTimeoutException within {MaxAcceptableTimeoutSeconds}s, took {sw.Elapsed.TotalSeconds:F1}s");
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
        Invoking(() => engine.Execute(preparedScript)).Should().ThrowExactly<RegexMatchTimeoutException>();
        sw.Stop();

        sw.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(MaxAcceptableTimeoutSeconds), $"Expected RegexMatchTimeoutException within {MaxAcceptableTimeoutSeconds}s, took {sw.Elapsed.TotalSeconds:F1}s");
    }

    // ---- host-supplied .NET Regex (RegExpConstructor.Construct(Regex, ...)) ----

    [Fact]
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

    [Fact]
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

    [Theory]
    [InlineData("g")]
    [InlineData("y")]
    [InlineData("gu")]
    public void TestOnEmptySubjectWithLastIndexPastTheEnd(string flags)
    {
        // Step 15.a: lastIndex > length returns null after resetting lastIndex; the empty subject is
        // not special-cased.
        var engine = new Engine();
        var result = engine.Evaluate($"var re = /a/{flags}; re.lastIndex = 5; JSON.stringify([re.test(''), re.lastIndex])");

        result.AsString().Should().Be("[false,0]");
    }

    [Theory]
    [InlineData("g")]
    [InlineData("y")]
    public void TestMatchesZeroLengthAtEndOfSubject(string flags)
    {
        // lastIndex == length is a legal start position: only lastIndex > length fails outright.
        var engine = new Engine();
        var result = engine.Evaluate($"var re = /a*/{flags}; re.lastIndex = 3; JSON.stringify([re.test('abc'), re.lastIndex])");

        result.AsString().Should().Be("[true,3]");
    }

    [Theory]
    [InlineData("g")]
    [InlineData("y")]
    public void TestResetsLastIndexWhenItIsPastTheEnd(string flags)
    {
        // Step 15.a.i: a global/sticky regexp resets lastIndex before returning null, so the next
        // test() starts over instead of latching false.
        var engine = new Engine();
        var result = engine.Evaluate($"var re = /a/{flags}; re.lastIndex = 10; JSON.stringify([re.test('abc'), re.lastIndex, re.test('abc'), re.lastIndex])");

        result.AsString().Should().Be("[false,0,true,1]");
    }

    [Theory]
    [InlineData("")]
    [InlineData("i")]
    [InlineData("u")]
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

    [Theory]
    [InlineData("")]
    [InlineData("i")]
    [InlineData("u")]
    [InlineData("iu")]
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

    [Fact]
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

    [Theory]
    [InlineData("", true)]
    [InlineData("y", true)]
    [InlineData("g", false)]
    [InlineData("gy", false)]
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

    [Fact]
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

    [Fact]
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

    [Fact]
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

    [Theory]
    // Canonicalize is toUpperCase in non-Unicode mode, so U+00B5 MICRO SIGN and U+039C GREEK CAPITAL
    // MU share a canonical value and match either way round.
    [InlineData(@"/\xB5/i", 0x039C, true)]
    [InlineData(@"/Μ/i", 0x00B5, true)]
    // The Latin-1 pair whose uppercase stays non-ASCII matches too.
    [InlineData(@"/\xFF/i", 0x0178, true)]
    // ... but step 9 keeps a character at or above U+0080 whose uppercase is ASCII to itself, so these
    // pairs stay distinct however alike .NET's IgnoreCase considers them.
    [InlineData(@"/\x6B/i", 0x212A, false)]
    [InlineData("/k/i", 0x212A, false)]
    [InlineData("/K/i", 0x212A, false)]
    [InlineData("/[a-z]/i", 0x212A, false)]
    [InlineData("/[j-l]/i", 0x212A, false)]
    // ... and the word-character set contains them, so it carries the same hazard.
    [InlineData(@"/\w/i", 0x212A, false)]
    [InlineData(@"/\x73/i", 0x017F, false)]
    [InlineData(@"/\xDF/i", 0x1E9E, false)]
    [InlineData(@"/\xE5/i", 0x212B, false)]
    public void CaseInsensitiveMatchingUsesCanonicalizeNotDotNetCaseFolding(string pattern, int subject, bool expected)
    {
        var engine = new Engine();
        var result = engine.Evaluate($"{pattern}.test(String.fromCharCode({subject}))");

        result.AsBoolean().Should().Be(expected);
    }

    [Theory]
    // In Unicode mode Canonicalize is Simple Case Folding, and the two non-ASCII characters that fold
    // into ASCII must be found even though the first-character scan searches for ASCII code units.
    [InlineData("/s/iu", 0x017F, 1)]
    [InlineData("/S/iu", 0x017F, 1)]
    [InlineData("/k/iu", 0x212A, 1)]
    [InlineData("/K/iu", 0x212A, 1)]
    [InlineData("/s+/iu", 0x017F, 2)]
    public void UnicodeCaseFoldingFindsTheNonAsciiFoldingPartners(string pattern, int subject, int repeat)
    {
        var engine = new Engine();
        var result = engine.Evaluate(
            $"{pattern}.exec(String.fromCharCode({subject}).repeat({repeat}))[0].length");

        result.AsNumber().Should().Be(repeat);
    }

    [Fact]
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
}
