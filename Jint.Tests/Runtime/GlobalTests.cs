namespace Jint.Tests.Runtime;

public class GlobalTests
{
    [Fact]
    public void UnescapeAtEndOfString()
    {
        var e = new Engine();

        e.Evaluate("unescape('%40');").AsString().Should().Be("@");
        e.Evaluate("unescape('%40_');").AsString().Should().Be("@_");
        e.Evaluate("unescape('%40%40');").AsString().Should().Be("@@");
        e.Evaluate("unescape('%u0040');").AsString().Should().Be("@");
        e.Evaluate("unescape('%u0040%u0040');").AsString().Should().Be("@@");
    }
}