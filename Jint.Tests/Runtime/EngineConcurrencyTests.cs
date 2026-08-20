namespace Jint.Tests.Runtime;

public class EngineConcurrencyTests
{
    [Fact]
    public async Task ConcurrentManualPromiseCompletionOnlyEnqueuesWork()
    {
        var engine = new Engine();
        var promise = engine.Advanced.RegisterPromise();
        engine.SetValue("hostPromise", promise.Promise);
        var continued = false;
        engine.SetValue("markContinued", new Action(() => Volatile.Write(ref continued, true)));
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        engine.SetValue("block", new Action(() =>
        {
            entered.Set();
            release.Wait();
        }));
        engine.Execute("hostPromise.then(markContinued);");

        var running = Task.Run(() => engine.Execute("block()"));
        entered.Wait(TimeSpan.FromSeconds(10)).Should().BeTrue();
        promise.Resolve(42);
        Volatile.Read(ref continued).Should().BeFalse();

        release.Set();
        await running;
        Volatile.Read(ref continued).Should().BeTrue();
    }

    [Fact]
    public void ManualPromiseCompletionDrainsInlineAfterExclusiveThreadHandoff()
    {
        var engine = new Engine();
        var promise = engine.Advanced.RegisterPromise();
        engine.SetValue("hostPromise", promise.Promise);
        engine.Execute("globalThis.result = 0; hostPromise.then(value => { result = value; });");

        var thread = new Thread(() => promise.Resolve(42));
        thread.Start();
        thread.Join();

        engine.GetValue("result").AsNumber().Should().Be(42);
    }

    [Fact]
    public void SameThreadManualPromiseCompletionDrainsInline()
    {
        var engine = new Engine();
        var promise = engine.Advanced.RegisterPromise();
        engine.SetValue("hostPromise", promise.Promise);
        engine.Execute("globalThis.result = 0; hostPromise.then(value => { result = value; });");

        promise.Resolve(42);

        engine.GetValue("result").AsNumber().Should().Be(42);
    }
}
