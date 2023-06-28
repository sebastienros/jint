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

    [Fact]
    public void ShouldTaskConvertedToPromiseInJS()
    {
        var engine = new Engine();
        engine.SetValue("callable", Callable);
        var result = engine.Evaluate("callable().then(x=>x*2)");
        result = result.UnwrapIfPromise();
        Assert.Equal(2, result);

        async Task<int> Callable()
        {
            await Task.Delay(10);
            Assert.True(true);
            return 1;
        }
    }

    [Fact]
    public void ShouldTaskCatchWhenCancelled()
    {
        var engine = new Engine();
        var cancel = new CancellationTokenSource();
        cancel.Cancel();
        engine.SetValue("token", cancel.Token);
        engine.SetValue("callable", Callable);
        engine.SetValue("assert", new Action<bool>(Assert.True));
        var result = engine.Evaluate("callable(token).then(_ => assert(false)).catch(_ => assert(true))");

        async Task Callable(CancellationToken token)
        {
            await Task.Delay(10);
            token.ThrowIfCancellationRequested();
        }
    }

    [Fact]
    public void ShouldTaskCatchWhenThrowError()
    {
        var engine = new Engine();
        engine.SetValue("callable", Callable);
        engine.SetValue("assert", new Action<bool>(Assert.True));
        var result = engine.Evaluate("callable().then(_ => assert(false)).catch(_ => assert(true))");

        async Task Callable(CancellationToken token)
        {
            await Task.Delay(10);
            throw new Exception();
        }
    }
}
