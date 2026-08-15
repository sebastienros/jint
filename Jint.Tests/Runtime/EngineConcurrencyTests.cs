namespace Jint.Tests.Runtime;

public class EngineConcurrencyTests
{
    [Fact]
    public void BackgroundManualPromiseCompletionOnlyEnqueuesWork()
    {
        var engine = new Engine();
        var promise = engine.Advanced.RegisterPromise();
        engine.SetValue("hostPromise", promise.Promise);
        engine.Execute("globalThis.result = 0; hostPromise.then(value => { result = value; });");

        var thread = new Thread(() => promise.Resolve(42));
        thread.Start();
        thread.Join();

        engine.GetValue("result").AsNumber().Should().Be(0);
        engine.Advanced.ProcessTasks();
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
