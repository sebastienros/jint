namespace Jint.Tests.Runtime;

public class DestructuringTests
{
    [Fact]
    public void WithStrings()
    {
        const string Script = @"
            return function([a, b, c]) {
              return a === ""a"" && b === ""b"" && c === void undefined;
            }(""ab"");";

        var engine = new Engine();
        Assert.True(engine.Evaluate(Script).AsBoolean());
    }
}
