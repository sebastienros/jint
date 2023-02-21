namespace Jint.Tests.Runtime;

public class AsyncTests
{
    [Fact]
    public void AwaitPropagationAgainstPrimitiveValue()
    {
        var engine = new Engine();
        var result = engine.Evaluate("(async ()=>await '1')()");
        result = result.UnwrapIfPromise();
        Assert.Equal("1", result);
    }
}
