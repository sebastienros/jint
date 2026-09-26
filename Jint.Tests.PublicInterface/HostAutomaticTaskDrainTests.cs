#nullable enable

namespace Jint.Tests.PublicInterface;

public class HostAutomaticTaskDrainTests
{
    [Test]
    public void ExecuteDrainsPostedWorkAndItsAsyncReactionsBeforeReturning()
    {
        using var engine = new Engine();
        var order = new List<string>();
        engine.SetValue("record", new Action<string>(order.Add));
        engine.Tasks.Post(() =>
        {
            order.Add("post");
            engine.Execute("(async () => { await 0; record('async'); })();");
        });

        engine.Execute("record('script');");

        order.Should().Equal("script", "post", "async");
    }

    [Test]
    public void AManualPromiseStillSettlesInlineOnTheOwningThread()
    {
        using var engine = new Engine();
        var settled = false;
        var (promise, resolve, _) = engine.Tasks.RegisterPromise();
        engine.SetValue("promise", promise);
        engine.SetValue("settled", new Action(() => settled = true));
        engine.Execute("promise.then(settled);");

        resolve(42);

        settled.Should().BeTrue();
    }

#if NET8_0_OR_GREATER
    private sealed class FrozenClock : TimeProvider
    {
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => 0;
        public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch;
    }

    [Test]
    public void ExecuteStillPromotesDueTimersAfterPromiseReactions()
    {
        using var engine = new Engine(options => options.UseWebApis(webApi =>
            webApi.Timers.TimeProvider = new FrozenClock()));
        var order = new List<string>();
        engine.SetValue("record", new Action<string>(order.Add));

        engine.Execute("""
            setTimeout(() => record('timer'), 0);
            Promise.resolve().then(() => record('reaction'));
            record('script');
            """);

        order.Should().Equal("script", "reaction", "timer");
    }
#endif
}
