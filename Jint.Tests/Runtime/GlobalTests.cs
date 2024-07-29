namespace Jint.Tests.Runtime;

public class GlobalTests
{
    [Fact]
    public void UnescapeAtEndOfString()
    {
        var e = new Engine();

        Assert.Equal("@", e.Evaluate("unescape('%40');").AsString());
        Assert.Equal("@_", e.Evaluate("unescape('%40_');").AsString());
        Assert.Equal("@@", e.Evaluate("unescape('%40%40');").AsString());
        Assert.Equal("@", e.Evaluate("unescape('%u0040');").AsString());
        Assert.Equal("@@", e.Evaluate("unescape('%u0040%u0040');").AsString());
    }
}