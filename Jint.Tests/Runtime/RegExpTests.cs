using System.Diagnostics;
using System.Text.RegularExpressions;
using Jint.Native;

namespace Jint.Tests.Runtime;

public class RegExpTests
{
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
    public void Issue506()
    {
        var engine = new Engine();
        var result = engine.Evaluate("/[^]?(:[rp][el]a[\\w-]+)[^]/.test(':reagent-')").AsBoolean();
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
