#if NET8_0_OR_GREATER
#nullable enable

namespace Jint.Tests.Runtime.NodeCompat;

/// <summary>
/// <c>process.nextTick</c> against the timers, which is the half of Node's ordering claim that needs the
/// opt-in web-API timers to observe at all — hence its own file, gated like every other test that touches
/// them.
/// </summary>
public class ProcessTimerOrderingTests
{
    [Test]
    public void NextTickRunsBeforeATimerThatIsAlreadyDue()
    {
        var engine = new Engine(options => options.UseNodeProcess().UseWebApis(WebApiFeatures.Timers));

        engine.Execute("""
            var log = [];
            setTimeout(() => log.push('timeout'), 0);
            process.nextTick(() => log.push('tick'));
            """);

        // "It runs before any additional I/O events (including timers) fire in subsequent ticks of the event
        // loop" — https://nodejs.org/api/process.html#processnexttickcallback-args. Here that falls out of the
        // engine's own rule: a due timer is promoted onto the job queue only once the queue has run dry.
        engine.Evaluate("log.join(',')").AsString().Should().Be("tick,timeout");
    }

    [Test]
    public void ATimerCallbackCanQueueANextTickThatRunsBeforeTheNextTimer()
    {
        var engine = new Engine(options => options.UseNodeProcess().UseWebApis(WebApiFeatures.Timers));

        engine.Execute("""
            var log = [];
            setTimeout(() => { log.push('t1'); process.nextTick(() => log.push('tick')); }, 0);
            setTimeout(() => log.push('t2'), 0);
            """);

        // Everything the first timer queues runs before the second timer is even looked at, exactly as it
        // does for a promise reaction.
        engine.Evaluate("log.join(',')").AsString().Should().Be("t1,tick,t2");
    }
}
#endif
