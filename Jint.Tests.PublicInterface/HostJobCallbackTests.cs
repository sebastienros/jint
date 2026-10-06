#nullable enable

using Jint.Native;
using Jint.Runtime;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// <see cref="Options.HostOptions.JobCallbacks"/> from the outside: a host that keeps its own notion of the
/// current logical flow — here an <see cref="AsyncLocal{T}"/>, as an embedder would — can carry it from where
/// script registers a callback to where the engine runs it, so a host function called after an <c>await</c>
/// still knows which flow it belongs to.
/// </summary>
/// <remarks>
/// The members are <c>protected internal</c> in Jint and therefore <c>protected</c> from here, which is the
/// spelling a third party has to be able to write.
/// </remarks>
public class HostJobCallbackTests
{
    private sealed class FlowHooks : JobCallbackHooks
    {
        internal static readonly AsyncLocal<string?> Flow = new();

        internal readonly List<Engine> CapturingEngines = new();

        protected override object? Capture(Engine engine)
        {
            CapturingEngines.Add(engine);
            return Flow.Value;
        }

        protected override object? Enter(Engine engine, object hostDefined)
        {
            var previous = Flow.Value;
            Flow.Value = (string) hostDefined;
            return previous;
        }

        protected override void Exit(Engine engine, object? token) => Flow.Value = (string?) token;
    }

    private static Engine CreateEngine(FlowHooks? hooks, List<string> log)
    {
        var engine = new Engine(options => options.Host.JobCallbacks = hooks);

        engine.SetValue("scope", new Func<string, JsValue, JsValue>((name, callback) =>
        {
            var previous = FlowHooks.Flow.Value;
            FlowHooks.Flow.Value = name;
            try
            {
                return callback.Call();
            }
            finally
            {
                FlowHooks.Flow.Value = previous;
            }
        }));

        engine.SetValue("hostOperation", new Func<int, int>(item =>
        {
            log.Add($"{item} in {FlowHooks.Flow.Value ?? "none"}");
            return item;
        }));

        return engine;
    }

    [Test]
    public async Task EachBranchOfAConcurrentFanOutKeepsItsFlowAcrossAwait()
    {
        // sebastienros/jint#4200: three branches, whose continuations all resume in one microtask drain.
        var hooks = new FlowHooks();
        var log = new List<string>();
        var engine = CreateEngine(hooks, log);

        await engine.EvaluateAsync("""
            (async () => {
                await Promise.all([1, 2, 3].map(async (item) => {
                    await scope(`Item ${item}`, async () => {
                        await hostOperation(item);
                        await hostOperation(item * 10);
                        await new Promise(resolve => resolve()).then(() => hostOperation(item * 100));
                    });
                }));
            })()
            """);

        log.Should().Equal(
            "1 in Item 1", "2 in Item 2", "3 in Item 3",
            "10 in Item 1", "20 in Item 2", "30 in Item 3",
            "100 in Item 1", "200 in Item 2", "300 in Item 3");
        FlowHooks.Flow.Value.Should().BeNull();
    }

    [Test]
    public void OneInstanceServesEveryEngineBuiltFromTheOptionsAndIsToldWhichOne()
    {
        var hooks = new FlowHooks();
        var options = new Options { Host = { JobCallbacks = hooks } };
        var first = new Engine(options);
        var second = new Engine(options);

        first.Execute("Promise.resolve().then(() => {})");
        second.Execute("Promise.resolve().then(() => {})");

        hooks.CapturingEngines.Should().Equal(first, second);
    }

    [Test]
    public void WithoutHooksTheFlowIsLostAtTheFirstAwait()
    {
        // The baseline the hooks exist to change: scope() has restored the host's state by the time the
        // continuation runs, so a host function called after the await sees nothing.
        var log = new List<string>();
        var plain = CreateEngine(hooks: null, log);

        plain.Execute("scope('lost', async () => { hostOperation(1); await null; hostOperation(2); })");

        log.Should().Equal("1 in lost", "2 in none");
    }
}
