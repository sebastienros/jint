#if NET8_0_OR_GREATER
#nullable enable

using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Jint.Constraints;
using Jint.Runtime;
using Jint.WebApi;

namespace Jint.Tests.Runtime.WebApi;

/// <summary>
/// The Workers foundation: the options a provider is handed, and the connection object a host holds.
/// </summary>
/// <remarks>
/// <para>
/// There is no script surface yet — <c>typeof Worker</c> is <c>undefined</c> on every engine, which
/// <c>Jint.Tests.PublicInterface.WebApiWorkerTests</c> pins from outside the assembly. What exists is the
/// host-facing contract: <see cref="WorkerRequest.CreateDefaultOptions"/>, whose whole job is that a worker
/// inherits its creator's <i>restrictions</i> and none of its <i>grants</i>, and
/// <see cref="WorkerConnection"/>, which is thread-safe from the day it exists because the machinery that
/// will end it from two threads lands later.
/// </para>
/// <para>
/// The request is built directly through its internal constructor, which is what this project's
/// <c>InternalsVisibleTo</c> is for: it is the same object the constructor will hand a provider, without a
/// constructor to run it through yet.
/// </para>
/// </remarks>
public class WorkerTests
{
    private static WorkerRequest Request(Engine parent, CancellationToken terminationToken = default)
    {
        return new WorkerRequest(
            parent,
            specifier: "./worker.js",
            referencingLocation: null,
            WorkerType.Module,
            name: "",
            depth: 0,
            liveWorkerCount: 0,
            terminationToken);
    }

    // ---------------------------------------------------------------------------------------------------
    // The termination token
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public void TheDefaultOptionsRegisterTheTerminationTokenAsAConstraint()
    {
        using var cts = new CancellationTokenSource();
        var parent = new Engine();

        var worker = new Engine(Request(parent, cts.Token).CreateDefaultOptions());

        var constraint = worker.Constraints.Find<CancellationConstraint>();
        constraint.Should().NotBeNull("terminate() has to stop a running worker, not merely close its ports");
        constraint!.Token.Should().Be(cts.Token);

        cts.Cancel();

        // Bounded rather than `while (true)`: a worker whose token was never registered must make this test
        // fail, not hang.
        Assert.Throws<ExecutionCanceledException>(() => worker.Execute("for (var i = 0; i < 1000000; i++) { }"));
    }

    // ---------------------------------------------------------------------------------------------------
    // Constraints: factories replay, instances never copy
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public void TheDefaultOptionsReplayTheParentsConstraintFactories()
    {
        var parentOptions = new Options().MaxStatements(100);
        var parent = new Engine(parentOptions);

        var worker = new Engine(Request(parent).CreateDefaultOptions());

        Assert.Throws<StatementsCountOverflowException>(
            () => worker.Execute("for (var i = 0; i < 1000; i++) { var j = i; }"));

        // Its own instance, so what the worker spent is not what the parent has left.
        parent.Execute("for (var i = 0; i < 10; i++) { var j = i; }");

        var parentConstraint = parent.Constraints.Find<MaxStatementsConstraint>();
        var workerConstraint = worker.Constraints.Find<MaxStatementsConstraint>();
        parentConstraint.Should().NotBeNull();
        workerConstraint.Should().NotBeNull();
        workerConstraint.Should().NotBeSameAs(parentConstraint);
    }

    [Fact]
    public void TheDefaultOptionsDoNotCopyConstraintInstances()
    {
        var instance = new CountingConstraint();
        var parentOptions = new Options();
        parentOptions.Constraint(instance);
        var parent = new Engine(parentOptions);

        var workerOptions = Request(parent).CreateDefaultOptions();
        workerOptions.Constraints.Constraints.Should().BeEmpty(
            "a Constraint carries per-execution state and is documented single-engine-only");

        var worker = new Engine(workerOptions);

        parent.Execute("var a = 1;");
        var afterParent = instance.Checks;
        afterParent.Should().BeGreaterThan(0, "the parent's own engine does observe its instance");

        worker.Execute("for (var i = 0; i < 100; i++) { var j = i; }");
        instance.Checks.Should().Be(afterParent, "the worker engine never saw the parent's constraint instance");
    }

    // ---------------------------------------------------------------------------------------------------
    // Security posture: restrictions travel
    // ---------------------------------------------------------------------------------------------------

    public static TheoryData<string> CopiedSettings =>
    [
        "Constraints.MaxRecursionDepth",
        "Constraints.MaxExecutionStackCount",
        "Constraints.StackOverflowGuard",
        "Constraints.RegexTimeout",
        "Constraints.PromiseTimeout",
        "Constraints.MaxArraySize",
        "Constraints.MaxAtomicsPauseIterations",
        "Host.StringCompilationAllowed",
        "AgentCanSuspend",
        "Json.MaxParseDepth",
    ];

    /// <summary>
    /// One case per copied setting, so that dropping a single line of
    /// <c>Options.CopySecurityPosture</c> fails exactly the case that names it.
    /// </summary>
    [Theory]
    [MemberData(nameof(CopiedSettings))]
    public void TheDefaultOptionsCopyTheParentsSecurityPosture(string setting)
    {
        var parentOptions = Hardened();
        var parent = new Engine(parentOptions);

        var workerOptions = Request(parent).CreateDefaultOptions();

        var expected = Read(parentOptions, setting);
        expected.Should().NotBe(
            Read(new Options(), setting),
            "the hardened parent must differ from a fresh Options, or the case could pass by default");

        Read(workerOptions, setting).Should().Be(expected, "a worker inherits {0} from its creator", setting);
    }

    /// <summary>
    /// Every setting <c>CopySecurityPosture</c> copies, set to something no fresh <see cref="Options"/> has.
    /// </summary>
    private static Options Hardened()
    {
        var options = new Options();
        options.Constraints.MaxRecursionDepth = 7;
        options.Constraints.MaxExecutionStackCount = 123;
        options.Constraints.StackOverflowGuard = true;
        options.Constraints.RegexTimeout = TimeSpan.FromMilliseconds(250);
        options.Constraints.PromiseTimeout = TimeSpan.FromMilliseconds(500);
        options.Constraints.MaxArraySize = 4096;
        options.Constraints.MaxAtomicsPauseIterations = 17;
        options.Host.StringCompilationAllowed = false;
        options.AgentCanSuspend = false;
        options.Json.MaxParseDepth = 3;
        return options;
    }

    private static object Read(Options options, string setting) => setting switch
    {
        "Constraints.MaxRecursionDepth" => options.Constraints.MaxRecursionDepth,
        "Constraints.MaxExecutionStackCount" => options.Constraints.MaxExecutionStackCount,
        "Constraints.StackOverflowGuard" => options.Constraints.StackOverflowGuard,
        "Constraints.RegexTimeout" => options.Constraints.RegexTimeout,
        "Constraints.PromiseTimeout" => options.Constraints.PromiseTimeout,
        "Constraints.MaxArraySize" => options.Constraints.MaxArraySize,
        "Constraints.MaxAtomicsPauseIterations" => options.Constraints.MaxAtomicsPauseIterations,
        "Host.StringCompilationAllowed" => options.Host.StringCompilationAllowed,
        "AgentCanSuspend" => options.AgentCanSuspend,
        "Json.MaxParseDepth" => options.Json.MaxParseDepth,
        _ => throw new ArgumentOutOfRangeException(nameof(setting), setting, "unknown setting"),
    };

    // ---------------------------------------------------------------------------------------------------
    // Features: grants never travel by implication
    // ---------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(WebApiFeatures.Fetch)]
    [InlineData(WebApiFeatures.EventSource)]
    [InlineData(WebApiFeatures.WebSocket)]
    [InlineData(WebApiFeatures.Storage)]
    [InlineData(WebApiFeatures.CacheApi)]
    [InlineData(WebApiFeatures.FetchEvents)]
    [InlineData(WebApiFeatures.Workers)]
    public void TheDefaultOptionsSubtractNetworkStorageRoutingAndWorkers(WebApiFeatures granted)
    {
        var parent = new Engine(options => options.UseWebApis(WebApiFeatures.Default | granted));
        (parent.Advanced.WebApiFeatures & granted).Should().Be(granted, "the parent really was granted it");

        var features = Request(parent).CreateDefaultOptions().WebApi.Features;

        (features & granted).Should().Be(WebApiFeatures.None, "a worker gets strictly fewer capabilities than its creator");
    }

    [Fact]
    public void TheDefaultOptionsForceMessagingAndGlobalEvents()
    {
        var parent = new Engine(options => options.UseWebApis(WebApiFeatures.Console));

        var features = Request(parent).CreateDefaultOptions().WebApi.Features;

        (features & WebApiFeatures.Messaging).Should().Be(WebApiFeatures.Messaging, "both halves of the connection are ports");
        (features & WebApiFeatures.GlobalEvents).Should().Be(WebApiFeatures.GlobalEvents, "the worker global is built out of them");
        (features & WebApiFeatures.Console).Should().Be(WebApiFeatures.Console, "what the parent had, and is not a grant, travels");
    }

    [Fact]
    public void TheDefaultOptionsDoNotCopyTheProvider()
    {
        var provider = new TestWorkerProvider();
        var parent = new Engine(options => options.UseWebApis().UseWorkers(provider));

        var workerOptions = Request(parent).CreateDefaultOptions();

        workerOptions.WebApi.Workers.Provider.Should().BeNull("nesting is off by default");
        (workerOptions.WebApi.Features & WebApiFeatures.Workers).Should().Be(WebApiFeatures.None);
    }

    [Fact]
    public void TheDefaultOptionsInstallTheNullSink()
    {
        var parent = new Engine();

        var workerOptions = Request(parent).CreateDefaultOptions();

        workerOptions.WebApi.Diagnostics.Sink.Should().BeSameAs(
            DiagnosticsSink.Null,
            "it is what flips a worker's callbacks to report-and-continue, and what the error relay reads");
    }

    [Fact]
    public void EveryCallToCreateDefaultOptionsReturnsAFreshInstance()
    {
        var parentOptions = new Options();
        var parent = new Engine(parentOptions);
        var request = Request(parent);

        var first = request.CreateDefaultOptions();
        var second = request.CreateDefaultOptions();

        first.Should().NotBeSameAs(second);
        first.Should().NotBeSameAs(parentOptions);

        first.Constraints.MaxRecursionDepth = 42;
        second.Constraints.MaxRecursionDepth.Should().Be(-1);
        parentOptions.Constraints.MaxRecursionDepth.Should().Be(-1);
    }

    // ---------------------------------------------------------------------------------------------------
    // UseWorkers
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public void UseWorkersSetsFlagAndProviderTogether()
    {
        var provider = new TestWorkerProvider();
        var options = new Options().UseWebApis(WebApiFeatures.Console).UseWorkers(provider);

        (options.WebApi.Features & WebApiFeatures.Workers).Should().Be(WebApiFeatures.Workers);
        options.WebApi.Workers.Provider.Should().BeSameAs(provider);
        (options.WebApi.Features & WebApiFeatures.Console).Should().Be(WebApiFeatures.Console, "it adds rather than replaces");
    }

    // ---------------------------------------------------------------------------------------------------
    // The connection
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public void WorkerConnectionEndIsIdempotentUnderConcurrentCallers()
    {
        using var cts = new CancellationTokenSource();
        var callbacks = 0;
        var reasons = new ConcurrentBag<WorkerEndReason>();
        var connection = new WorkerConnection(
            new Engine(),
            new Engine(),
            "w",
            reason =>
            {
                Interlocked.Increment(ref callbacks);
                reasons.Add(reason);
            },
            cts.Token);

        Parallel.For(0, 64, _ => connection.End());

        Volatile.Read(ref callbacks).Should().Be(1, "the end sequence runs exactly once however many threads race for it");
        reasons.Should().ContainSingle().Which.Should().Be(WorkerEndReason.Terminated);
        connection.IsEnded.Should().BeTrue();
        connection.EndReason.Should().Be(WorkerEndReason.Terminated);
        connection.IsFaulted.Should().BeFalse();
        connection.Error.Should().BeNull();
    }

    [Fact]
    public void AConnectionThatEndedFaultedCarriesTheCLRErrorAndStaysEnded()
    {
        var failure = new InvalidOperationException("the specifier did not resolve");
        var connection = new WorkerConnection(new Engine(), new Engine(), "w", onEnded: null, default);

        connection.IsEnded.Should().BeFalse();
        connection.EndReason.Should().BeNull();
        connection.Error.Should().BeNull();

        connection.TryEnd(WorkerEndReason.StartupFailed, failure).Should().BeTrue();

        connection.IsEnded.Should().BeTrue();
        connection.EndReason.Should().Be(WorkerEndReason.StartupFailed);
        connection.IsFaulted.Should().BeTrue();
        connection.Error.Should().BeSameAs(failure);

        // A later end — the host's own, or a restore's — neither runs again nor rewrites what happened.
        connection.End();
        connection.EndReason.Should().Be(WorkerEndReason.StartupFailed);
        connection.Error.Should().BeSameAs(failure);
    }

    [Fact]
    public void HostStateIsCarriedAndTheEngineNeverReadsIt()
    {
        var connection = new WorkerConnection(new Engine(), new Engine(), "w", onEnded: null, default);

        connection.HostState.Should().BeNull();

        var state = new object();
        connection.HostState = state;

        connection.HostState.Should().BeSameAs(state);

        connection.End();
        connection.HostState.Should().BeSameAs(state, "ending signals the host, it does not tidy up after it");
    }

    private sealed class CountingConstraint : Constraint
    {
        public int Checks { get; private set; }

        public override void Check() => Checks++;

        public override void Reset()
        {
        }
    }

    private sealed class TestWorkerProvider : WorkerProvider
    {
        public override Engine? CreateWorkerEngine(WorkerRequest request) => null;
    }
}
#endif
