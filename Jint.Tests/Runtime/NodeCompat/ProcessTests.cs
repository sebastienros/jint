#nullable enable

using Jint.NodeCompat;
using Jint.Native;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Descriptors.Specialized;

namespace Jint.Tests.Runtime.NodeCompat;

/// <summary>
/// The opt-in Node-style <c>process</c> global — https://nodejs.org/api/process.html — and the boundaries
/// drawn around it: what it exposes of the host, what it refuses to, and how it is installed.
/// </summary>
/// <remarks>
/// Nothing here waits on a clock. The <c>nextTick</c> tests are about ordering, which the engine's own job
/// queue decides, and <c>hrtime.bigint()</c> is asserted structurally — a monotonic reading, never a duration
/// a test could race.
/// </remarks>
public class ProcessTests
{
    private static Engine ProcessEngine(Action<NodeProcessOptions>? configure = null)
        => new(options => options.UseNodeProcess(configure));

    [Test]
    public void ADefaultEngineHasNoProcess()
    {
        var engine = new Engine();

        engine.Evaluate("typeof process").AsString().Should().Be("undefined");
        engine.Evaluate("'process' in globalThis").AsBoolean().Should().BeFalse();
    }

    [Test]
    public void InstallsAnUnmaterializedLazyDescriptor()
    {
        var engine = ProcessEngine();

        var descriptor = engine.Realm.GlobalObject.GetOwnProperty("process");

        descriptor.Should().BeOfType<LazyPropertyDescriptor<EngineAndState<NodeProcessConfiguration>>>();
        // Still flagged CustomJsValue means the factory has not run: enabling the shim costs one descriptor
        // and nothing else until a script mentions the name.
        (descriptor._flags & PropertyFlag.CustomJsValue).Should().NotBe(PropertyFlag.None);
        descriptor._value.Should().BeNull();

        // An ordinary data property, as `console` is, rather than the non-enumerable interface object a
        // WebIDL global gets — `process` is neither.
        descriptor.Writable.Should().BeTrue();
        descriptor.Enumerable.Should().BeTrue();
        descriptor.Configurable.Should().BeTrue();
    }

    [Test]
    public void DoesNotReachIntoAShadowRealm()
    {
        var engine = ProcessEngine();

        engine.Evaluate("new ShadowRealm().evaluate('typeof process')").AsString().Should().Be("undefined");
        engine.Evaluate("typeof process").AsString().Should().Be("object");
    }

    [Test]
    public void SurvivesAGlobalSnapshotRestoreAndForgetsWhatTheScriptWroteIntoEnv()
    {
        var engine = ProcessEngine(p => p.EnvironmentOverrides = new Dictionary<string, string> { ["A"] = "1" });

        var snapshot = engine.Advanced.CaptureGlobalSnapshot();

        engine.Execute("process.env.SCRIBBLE = 'x';");
        engine.Realm.GlobalObject.GetOwnProperty("process")._value.Should().NotBeNull();

        engine.Advanced.RestoreGlobalSnapshot(snapshot);

        // Back to the unmaterialized state it was captured in, so the next read rebuilds `process` — and with
        // it a fresh `env`, rather than serving the previous cycle's scribbles to the next request.
        var descriptor = engine.Realm.GlobalObject.GetOwnProperty("process");
        descriptor.Should().BeOfType<LazyPropertyDescriptor<EngineAndState<NodeProcessConfiguration>>>();
        descriptor._value.Should().BeNull();

        engine.Evaluate("typeof process.env.SCRIBBLE").AsString().Should().Be("undefined");
    }

    [Test]
    public void EnvIsShapedSoARepeatedReadStaysMonomorphic()
    {
        var engine = ProcessEngine(p =>
        {
            p.EnvironmentVariableAllowlist = ["NODE_ENV", "TZ"];
            p.EnvironmentOverrides = new Dictionary<string, string> { ["NODE_ENV"] = "production", ["TZ"] = "UTC" };
        });

        var env = engine.Evaluate("process.env").AsObject();

        engine.Advanced.HasSharedShape(env).Should().BeTrue();
    }

    [Test]
    public void EnvExposesNothingByDefault()
    {
        var engine = ProcessEngine();

        engine.Evaluate("typeof process.env").AsString().Should().Be("object");
        engine.Evaluate("Object.keys(process.env).length").AsNumber().Should().Be(0);
    }

    [Test]
    public void EnvKeepsTheHostsOrderAndDropsDuplicates()
    {
        var engine = ProcessEngine(p =>
        {
            p.EnvironmentVariableAllowlist = ["B", "A", "B"];
            p.EnvironmentOverrides = new Dictionary<string, string> { ["A"] = "a", ["B"] = "b" };
        });

        engine.Evaluate("Object.keys(process.env).join(',')").AsString().Should().Be("B,A");
    }

    [Test]
    public void EnvIsMaterializedOnceAndDoesNotFollowTheRealEnvironment()
    {
        const string Name = "JINT_TEST_PROCESS_ENV_LIVE";
        Environment.SetEnvironmentVariable(Name, "first");
        try
        {
            var engine = ProcessEngine(p => p.EnvironmentVariableAllowlist = [Name]);

            engine.Evaluate($"process.env.{Name}").AsString().Should().Be("first");

            Environment.SetEnvironmentVariable(Name, "second");

            // No live reads: the object was built when `process` was first touched, and a variable that
            // changes underneath it does not change here.
            engine.Evaluate($"process.env.{Name}").AsString().Should().Be("first");
        }
        finally
        {
            Environment.SetEnvironmentVariable(Name, null);
        }
    }

    [Test]
    public void NextTickRunsAfterTheCurrentScriptAndForwardsItsArguments()
    {
        var engine = ProcessEngine();

        engine.Execute("""
            var log = [];
            process.nextTick((a, b) => log.push('tick:' + a + b), 1, 2);
            log.push('script');
            """);

        // Execute drains the event loop once the script has finished, so the callback has already run.
        engine.Evaluate("log.join(',')").AsString().Should().Be("script,tick:12");
    }

    [Test]
    public void NextTickSharesOneQueueWithPromiseReactions()
    {
        var engine = ProcessEngine();

        engine.Execute("""
            var log = [];
            Promise.resolve().then(() => log.push('promise'));
            process.nextTick(() => log.push('tick'));
            """);

        // The documented divergence from Node, where the next-tick queue is drained ahead of the promise
        // microtask queue and this would answer "tick,promise". Jint has one job queue, so the two run in
        // registration order.
        engine.Evaluate("log.join(',')").AsString().Should().Be("promise,tick");
    }

    [Test]
    public void NextTickRequiresACallableAndDoesNotQueueAnythingWithoutOne()
    {
        var engine = ProcessEngine();

        engine.Execute("""
            var log = [];
            var caught = null;
            try { process.nextTick('not a function'); } catch (e) { caught = e; }
            """);

        engine.Evaluate("caught instanceof TypeError").AsBoolean().Should().BeTrue();
        engine.Evaluate("log.length").AsNumber().Should().Be(0);
    }

    [Test]
    public void HrtimeBigIntIsAMonotonicBigInt()
    {
        var engine = ProcessEngine();

        engine.Evaluate("typeof process.hrtime.bigint()").AsString().Should().Be("bigint");

        // Monotonic, never a duration a test could race: two readings of a monotonic clock cannot go
        // backwards, and on a coarse platform timer they may well be equal.
        engine.Evaluate("(() => { const a = process.hrtime.bigint(); const b = process.hrtime.bigint(); return b >= a; })()")
            .AsBoolean().Should().BeTrue();

        // The legacy tuple form is deliberately absent rather than approximated.
        engine.Evaluate("typeof process.hrtime").AsString().Should().Be("object");
    }

    [Test]
    public void TheProcessObjectIsIdenticalOnEveryRead()
    {
        var engine = ProcessEngine();

        engine.Evaluate("process === process && process.env === process.env").AsBoolean().Should().BeTrue();
    }

    [Test]
    public void ExposesTheMembersItDocumentsAndNoOthers()
    {
        var engine = ProcessEngine();

        engine.Evaluate("Object.keys(process).join(',')").AsString()
            .Should().Be("platform,version,versions,argv,env,cwd,nextTick,hrtime");

        // Nothing that could act on the host process exists at all — an absent function lets a script's own
        // feature detection take its other branch, where a throwing one would not.
        foreach (var name in new[] { "exit", "abort", "kill", "chdir", "on", "emit", "binding", "dlopen" })
        {
            engine.Evaluate($"typeof process.{name}").AsString().Should().Be("undefined");
        }
    }

    [Test]
    public void FunctionsCarryTheirNodeNamesAndArity()
    {
        var engine = ProcessEngine();

        engine.Evaluate("process.nextTick.name").AsString().Should().Be("nextTick");
        engine.Evaluate("process.nextTick.length").AsNumber().Should().Be(1);
        engine.Evaluate("process.cwd.name").AsString().Should().Be("cwd");
        engine.Evaluate("process.cwd.length").AsNumber().Should().Be(0);
        engine.Evaluate("process.hrtime.bigint.name").AsString().Should().Be("bigint");
    }

    [Test]
    public void ArgvIsAnEmptyArray()
    {
        var engine = ProcessEngine();

        engine.Evaluate("Array.isArray(process.argv)").AsBoolean().Should().BeTrue();
        engine.Evaluate("process.argv.length").AsNumber().Should().Be(0);
        engine.Evaluate("process.argv.slice(2).length").AsNumber().Should().Be(0);
    }

    [Test]
    public void LeavesAProcessGlobalTheHostAlreadyOwns()
    {
        var marker = new JsString("host's own");
        var engine = new Engine(options => options
            .Configure(e => e.SetValue("process", marker))
            .UseNodeProcess());

        engine.Evaluate("process").Should().BeSameAs(marker);
    }

    [Test]
    public void LeavesAHostLazyGlobalUnmaterialized()
    {
        var built = 0;
        var engine = new Engine(options => options
            .AddLazyGlobal("process", _ => { built++; return new JsString("host's own"); })
            .UseNodeProcess());

        // The non-clobbering check probes rather than reads, so looking does not force the host's factory.
        built.Should().Be(0);
        engine.Evaluate("process").AsString().Should().Be("host's own");
        built.Should().Be(1);
    }

    [Test]
    public void AHostGlobalRegisteredAfterwardsWinsToo()
    {
        var marker = new JsString("host's own");
        var engine = new Engine(options => options
            .UseNodeProcess()
            .Configure(e => e.SetValue("process", marker)));

        engine.Evaluate("process").Should().BeSameAs(marker);
    }
}
