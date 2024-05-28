namespace Jint.Tests.Runtime;

public class StringTests
{
    public StringTests()
    {
        _engine = new Engine()
            .SetValue("log", new Action<object>(Console.WriteLine))
            .SetValue("assert", new Action<bool>(Assert.True))
            .SetValue("equal", new Action<object, object>(Assert.Equal));
    }

    private readonly Engine _engine;

    [Fact]
    public void StringConcatenationAndReferences()
    {
        const string script = @"
var foo = 'foo';
foo += 'foo';
var bar = foo;
bar += 'bar';
";
        var value = _engine.Execute(script);
        var foo = _engine.Evaluate("foo").AsString();
        var bar = _engine.Evaluate("bar").AsString();
        Assert.Equal("foofoo", foo);
        Assert.Equal("foofoobar", bar);
    }

    [Fact]
    public void TrimLeftRightShouldBeSameAsTrimStartEnd()
    {
        _engine.Execute(@"
                assert(''.trimLeft === ''.trimStart);
                assert(''.trimRight === ''.trimEnd);
");
    }

    [Fact]
    public void HasProperIteratorPrototypeChain()
    {
        const string Script = @"
        // Iterator instance
        var iterator = ''[Symbol.iterator]();
        // %StringIteratorPrototype%
        var proto1 = Object.getPrototypeOf(iterator);
        // %IteratorPrototype%
        var proto2 = Object.getPrototypeOf(proto1);";

        var engine = new Engine();
        engine.Execute(Script);
        Assert.True(engine.Evaluate("proto2.hasOwnProperty(Symbol.iterator)").AsBoolean());
        Assert.True(engine.Evaluate("!proto1.hasOwnProperty(Symbol.iterator)").AsBoolean());
        Assert.True(engine.Evaluate("!iterator.hasOwnProperty(Symbol.iterator)").AsBoolean());
        Assert.True(engine.Evaluate("iterator[Symbol.iterator]() === iterator").AsBoolean());
    }

    [Fact]
    public void IndexOf()
    {
        var engine = new Engine();
        Assert.Equal(0, engine.Evaluate("''.indexOf('', 0)"));
        Assert.Equal(0, engine.Evaluate("''.indexOf('', 1)"));
    }

    [Fact]
    public void TemplateLiteralsWithArrays()
    {
        var engine = new Engine();
        engine.Execute("var a = [1,2,'three',true];");
        Assert.Equal("test 1,2,three,true", engine.Evaluate("'test ' + a"));
        Assert.Equal("test 1,2,three,true", engine.Evaluate("`test ${a}`"));
    }

    [Fact]
    public void TemplateLiteralAsObjectKey()
    {
        var engine=new Engine();
        var result = engine.Evaluate("({ [`key`]: 'value' })").AsObject();
        Assert.True(result.HasOwnProperty("key"));
        Assert.Equal("value", result["key"]);
    }

    [Fact]
    public void ShouldCompareWithLocale()
    {
        var engine = new Engine();
        Assert.Equal(1, engine.Evaluate("'王五'.localeCompare('张三')").AsInteger());
        Assert.Equal(-1, engine.Evaluate("'王五'.localeCompare('张三', 'zh-CN')").AsInteger());
    }

    public static TheoryData<string, string> GetLithuaniaTestsData()
    {
        return new StringTetsLithuaniaData().TestData();
    }

    /// <summary>
    /// Lithuanian case is special and Test262 suite tests cover only correct parsing by character. See:
    /// https://github.com/tc39/test262/blob/main/test/intl402/String/prototype/toLocaleUpperCase/special_casing_Lithuanian.js
    /// Added logic in the engine needs to parse full strings and not only spare characters. This is what these tests cover.
    /// </summary>
    [Theory]
    [MemberData(nameof(GetLithuaniaTestsData))]
    public void LithuanianToLocaleUpperCase(string parseStr, string result)
    {
        var value = _engine.Evaluate($"('{parseStr}').toLocaleUpperCase('lt')").AsString();
        Assert.Equal(result, value);
    }
}
