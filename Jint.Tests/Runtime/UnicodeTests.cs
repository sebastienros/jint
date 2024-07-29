namespace Jint.Tests.Runtime;

public class UnicodeTests
{
    private readonly Engine _engine;

    public UnicodeTests()
    {
        _engine = new Engine()
            .SetValue("log", new Action<object>(Console.WriteLine))
            .SetValue("assert", new Action<bool>(Assert.True))
            .SetValue("equal", new Action<object, object>(Assert.Equal));
    }

    private void RunTest(string source)
    {
        _engine.Execute(source);
    }

    [Fact]
    public void EscapeUnicodeUnits()
    {
        var value = _engine.Evaluate(@"var a = '\uD83D\uDE80'; return a;").AsString();
        Assert.Equal("🚀", value);
    }

    [Fact]
    public void EscapeUnicodeEscaped()
    {
        var value = _engine.Evaluate(@"var a = '\u{1F680}'; return a;").AsString();
        Assert.Equal("🚀", value);
    }

    [Fact]
    public void UnicodeIdentifiers()
    {
        var value = _engine.Evaluate(@"const hello = 123; return hell\u{6F}").AsNumber();
        Assert.Equal(123, value);
    }

    [Fact]
    public void RegexDontParseUnicodeEscapesWithoutFlag()
    {
        var value = _engine.Evaluate(@"return /^\u{3}$/.test('uuu')").AsBoolean();
        Assert.True(value);
    }

    [Fact]
    public void RegexParseUnicodeEscapesWithFlag()
    {
        var value = _engine.Evaluate(@"return /^\u{3}$/u.test('uuu')").AsBoolean();
        Assert.False(value);
    }

    [Fact]
    public void RegexParseUnicodeDoesntChangeSource()
    {
        var value = _engine.Evaluate(@"return /a\u0041/.source").AsString();
        Assert.Equal("a\\u0041", value);
    }

}