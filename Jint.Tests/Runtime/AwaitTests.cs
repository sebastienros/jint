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
        Engine engine = new();
        engine.SetValue("callable", Callable);
        var result = engine.Evaluate("callable().then(x=>x*2)");
        result = result.UnwrapIfPromise();
        Assert.Equal(2, result);

        static async Task<int> Callable()
        {
            await Task.Delay(10);
            Assert.True(true);
            return 1;
        }
    }

    [Fact]
    public void ShouldTaskCatchWhenCancelled()
    {
        Engine engine = new();
        CancellationTokenSource cancel = new();
        cancel.Cancel();
        engine.SetValue("token", cancel.Token);
        engine.SetValue("callable", Callable);
        engine.SetValue("assert", new Action<bool>(Assert.True));
        var result = engine.Evaluate("callable(token).then(_ => assert(false)).catch(_ => assert(true))");
        result = result.UnwrapIfPromise();
        static async Task Callable(CancellationToken token)
        {
            await Task.FromCanceled(token);
        }
    }

    [Fact]
    public void ShouldTaskCatchWhenThrowError()
    {
        Engine engine = new();
        engine.SetValue("callable", Callable);
        engine.SetValue("assert", new Action<bool>(Assert.True));
        var result = engine.Evaluate("callable().then(_ => assert(false)).catch(_ => assert(true))");

        static async Task Callable()
        {
            await Task.Delay(10);
            throw new Exception();
        }
    }

    [Fact]
    public void ShouldTaskAwaitCurrentStack()
    {
        //https://github.com/sebastienros/jint/issues/514#issuecomment-1507127509
        Engine engine = new();
        string log = "";
        engine.SetValue("myAsyncMethod", new Func<Task>(async () =>
        {
            await Task.Delay(1000);
            log += "1";
        }));
        engine.SetValue("myAsyncMethod2", new Action(() =>
        {
            log += "2";
        }));
        engine.Execute("async function hello() {await myAsyncMethod();myAsyncMethod2();} hello();");
        Assert.Equal("12", log);
    }

#if NETFRAMEWORK == false
    [Fact]
    public void ShouldValueTaskConvertedToPromiseInJS()
    {
        Engine engine = new();
        engine.SetValue("callable", Callable);
        var result = engine.Evaluate("callable().then(x=>x*2)");
        result = result.UnwrapIfPromise();
        Assert.Equal(2, result);

        static async ValueTask<int> Callable()
        {
            await Task.Delay(10);
            Assert.True(true);
            return 1;
        }
    }

    [Fact]
    public void ShouldValueTaskCatchWhenCancelled()
    {
        Engine engine = new();
        CancellationTokenSource cancel = new();
        cancel.Cancel();
        engine.SetValue("token", cancel.Token);
        engine.SetValue("callable", Callable);
        engine.SetValue("assert", new Action<bool>(Assert.True));
        var result = engine.Evaluate("callable(token).then(_ => assert(false)).catch(_ => assert(true))");
        result = result.UnwrapIfPromise();
        static async ValueTask Callable(CancellationToken token)
        {
            await ValueTask.FromCanceled(token);
        }
    }

    [Fact]
    public void ShouldValueTaskCatchWhenThrowError()
    {
        Engine engine = new();
        engine.SetValue("callable", Callable);
        engine.SetValue("assert", new Action<bool>(Assert.True));
        var result = engine.Evaluate("callable().then(_ => assert(false)).catch(_ => assert(true))");

        static async ValueTask Callable()
        {
            await Task.Delay(10);
            throw new Exception();
        }
    }

    [Fact]
    public void ShouldValueTaskAwaitCurrentStack()
    {
        //https://github.com/sebastienros/jint/issues/514#issuecomment-1507127509
        Engine engine = new();
        string log = "";
        engine.SetValue("myAsyncMethod", new Func<ValueTask>(async () =>
        {
            await Task.Delay(1000);
            log += "1";
        }));
        engine.SetValue("myAsyncMethod2", new Action(() =>
        {
            log += "2";
        }));
        engine.Execute("async function hello() {await myAsyncMethod();myAsyncMethod2();} hello();");
        Assert.Equal("12", log);
    }
#endif
}
