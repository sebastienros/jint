namespace Jint.Tests.Runtime;

public class UnicodeTests
{
    private readonly Engine _engine;

    public UnicodeTests()
    {
        _engine = new Engine()
            .SetValue("log", new Action<object>(Console.WriteLine))
            .SetValue("assert", new Action<bool>(static value => value.Should().BeTrue()))
            .SetValue("equal", new Action<object, object>(static (expected, actual) =>
                    actual.Should().BeEquivalentTo(expected, static options => options.WithStrictOrdering())));
    }

    private void RunTest(string source)
    {
        _engine.Execute(source);
    }

    [Fact]
    public void EscapeUnicodeUnits()
    {
        var value = _engine.Evaluate(@"var a = '\uD83D\uDE80'; return a;").AsString();
        value.Should().Be("🚀");
    }

    [Fact]
    public void EscapeUnicodeEscaped()
    {
        var value = _engine.Evaluate(@"var a = '\u{1F680}'; return a;").AsString();
        value.Should().Be("🚀");
    }

    [Fact]
    public void UnicodeIdentifiers()
    {
        var value = _engine.Evaluate(@"const hello = 123; return hell\u{6F}").AsNumber();
        value.Should().Be(123);
    }

    [Fact]
    public void RegexDontParseUnicodeEscapesWithoutFlag()
    {
        var value = _engine.Evaluate(@"return /^\u{3}$/.test('uuu')").AsBoolean();
        value.Should().BeTrue();
    }

    [Fact]
    public void RegexParseUnicodeEscapesWithFlag()
    {
        var value = _engine.Evaluate(@"return /^\u{3}$/u.test('uuu')").AsBoolean();
        value.Should().BeFalse();
    }

    [Fact]
    public void RegexParseUnicodeDoesntChangeSource()
    {
        var value = _engine.Evaluate(@"return /a\u0041/.source").AsString();
        value.Should().Be("a\\u0041");
    }

}