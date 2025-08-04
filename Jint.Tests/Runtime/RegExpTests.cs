using System.Text.RegularExpressions;
using Acornima;
using Acornima.Ast;
using Jint.Native;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime.Descriptors;

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
    public void Issue506()
    {
        var engine = new Engine();
        var result = engine.Evaluate("/[^]?(:[rp][el]a[\\w-]+)[^]/.test(':reagent-')").AsBoolean();
        Assert.True(result);
    }

    [Fact]
    public void RegExpLiteralInGetterShouldWork()
    {
        // Reproduces issue where using RegExp literal notation in getters parsed via Parser().ParseExpression()
        // resulted in NullReferenceException due to uncompiled regex
        var engine = new Engine();
        var obj = new JsObject(engine);
        
        // This should not throw NullReferenceException
        var getterFunction = (IFunction)new Parser().ParseExpression(
            "function() { return 'test'.match(/[a-z]/)?.[0]; }"
        );
        var getter = new ScriptFunction(engine, getterFunction, true);

        obj.FastSetProperty("name", new GetSetPropertyDescriptor(getter, null, true, false));
        engine.SetValue("customObj", obj);

        engine.Execute(string.Empty);
        var result = engine.GetValue("customObj").AsObject();
        var name = result.Get("name").ToString();

        Assert.Equal("t", name);
    }

    [Fact]
    public void RegExpLiteralInGetterWorksWithConstructorToo()
    {
        // Ensure the alternative RegExp constructor approach continues to work
        var engine = new Engine();
        var obj = new JsObject(engine);
        
        var getterFunction = (IFunction)new Parser().ParseExpression(
            "function() { return 'test'.match(new RegExp('[a-z]'))?.[0]; }"
        );
        var getter = new ScriptFunction(engine, getterFunction, true);

        obj.FastSetProperty("name", new GetSetPropertyDescriptor(getter, null, true, false));
        engine.SetValue("customObj", obj);

        engine.Execute(string.Empty);
        var result = engine.GetValue("customObj").AsObject();
        var name = result.Get("name").ToString();

        Assert.Equal("t", name);
    }
}
