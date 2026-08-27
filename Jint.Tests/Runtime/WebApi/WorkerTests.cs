#if NET8_0_OR_GREATER
#nullable enable

using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Jint.Constraints;
using Jint.Native;
using Jint.Runtime;
using Jint.WebApi;

namespace Jint.Tests.Runtime.WebApi;

/// <summary>
/// The Workers foundation: the options a provider is handed, and the connection object a host holds.
/// </summary>
/// <remarks>
/// <para>
/// This file is the host-facing contract alone: <see cref="WorkerRequest.CreateDefaultOptions"/>, whose whole
/// job is that a worker inherits its creator's <i>restrictions</i> and none of its <i>grants</i>, and
/// <see cref="WorkerConnection"/>, which is thread-safe because two threads really do end it. The script
/// surface — the constructor, the two façades, the message paths and the two ways a connection ends — is
/// <c>WorkerMechanismTests</c>.
/// </para>
/// <para>
/// The request is built directly through its internal constructor, which is what this project's
/// <c>InternalsVisibleTo</c> is for: it is the same object the constructor hands a provider, without having to
/// run a constructor to get one.
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

    [Test]
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

    [Test]
    public void TheDefaultOptionsReplayTheParentsConstraintFactories()
    {
        var parentOptions = new Options().LimitStatements(100);
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

    [Test]
    public void TheDefaultOptionsDoNotCopyConstraintInstances()
    {
        var instance = new CountingConstraint();
        var parentOptions = new Options();
        parentOptions.AddConstraint(instance);
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

    public static TestCases<string> CopiedSettings =>
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
        "Parsing.MaxSourceLength",
        "Parsing.MaxNodeCount",
        "Modules.MaxModuleCount",
        "Modules.MaxTotalModuleSourceBytes",
        "Modules.MaxModuleGraphDepth",
        "Modules.MaxModuleResolutionHops",
        "ResultLimits",
    ];

    /// <summary>
    /// One case per copied setting, so that dropping a single line of
    /// <c>Options.CopySecurityPosture</c> fails exactly the case that names it.
    /// </summary>
    [TestCaseSource(nameof(CopiedSettings))]
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
        // "Hardened" describes the scenario, but what each case needs is only a value a fresh Options does
        // not have — and the security stack flipped two defaults (#3057 turned the stack-overflow guard on,
        // #3058 turned agent suspension off), so for those two the non-default value is the PERMISSIVE one.
        // The copy is value fidelity in both directions: a worker matches its parent's posture exactly.
        var options = new Options();
        options.Constraints.MaxRecursionDepth = 7;
        options.Constraints.MaxExecutionStackCount = 123;
        options.Constraints.StackOverflowGuard = false;
        options.Constraints.RegexTimeout = TimeSpan.FromMilliseconds(250);
        options.Constraints.PromiseTimeout = TimeSpan.FromMilliseconds(500);
        options.Constraints.MaxArraySize = 4096;
        options.Constraints.MaxAtomicsPauseIterations = 17;
        options.Host.StringCompilationAllowed = false;
        options.AgentCanSuspend = true;
        options.Json.MaxParseDepth = 3;
        options.Parsing.MaxSourceLength = 64_000;
        options.Parsing.MaxNodeCount = 9_000;
        options.Modules.MaxModuleCount = 11;
        options.Modules.MaxTotalModuleSourceBytes = 256_000;
        options.Modules.MaxModuleGraphDepth = 5;
        options.Modules.MaxModuleResolutionHops = 13;
        options.ResultLimits = new ResultLimits { MaxDepth = 6 };
        return options;
    }

    // object? because the two nullable parser bounds box to null on a fresh Options.
    private static object? Read(Options options, string setting) => setting switch
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
        "Parsing.MaxSourceLength" => options.Parsing.MaxSourceLength,
        "Parsing.MaxNodeCount" => options.Parsing.MaxNodeCount,
        "Modules.MaxModuleCount" => options.Modules.MaxModuleCount,
        "Modules.MaxTotalModuleSourceBytes" => options.Modules.MaxTotalModuleSourceBytes,
        "Modules.MaxModuleGraphDepth" => options.Modules.MaxModuleGraphDepth,
        "Modules.MaxModuleResolutionHops" => options.Modules.MaxModuleResolutionHops,

        // Class-typed and shared by reference, so identity is the copy the case asserts.
        "ResultLimits" => options.ResultLimits,
        _ => throw new ArgumentOutOfRangeException(nameof(setting), setting, "unknown setting"),
    };

    /// <summary>
    /// The whole hardened profile travels without the marker travelling. An untrusted-code parent's
    /// <c>Engine.Options</c> is the profile's expanded clone, so the posture copy picks up every value the
    /// expansion set and the factory replay carries its budgets — while the <c>UntrustedCodeLimits</c>
    /// marker itself deliberately stays behind: a marked options object re-expands at engine construction,
    /// and that expansion clears the constraint registrations, including the termination token a worker's
    /// <c>terminate()</c> depends on. See <c>Options.CopySecurityPosture</c>'s remarks.
    /// </summary>
    [Test]
    public void AnUntrustedCodeProfileParentHardensItsWorkerWithoutTheMarker()
    {
        var limits = new UntrustedCodeLimits
        {
            TimeoutInterval = TimeSpan.FromMilliseconds(750),
            MaxStatements = 12_345,
            MemoryLimit = 8_000_000,
            MaxRecursionDepth = 21,
            MaxArraySize = 2048,
            RegexTimeout = TimeSpan.FromMilliseconds(333),
            PromiseTimeout = TimeSpan.FromMilliseconds(444),
            MaxOperationDuration = TimeSpan.FromSeconds(5),
            MaxSourceLength = 55_555,
            MaxNodeCount = 4_321,
        };

        var parent = new Engine(new Options().ForUntrustedCode(limits));

        var workerOptions = Request(parent).CreateDefaultOptions();

        // The expansion's values, inherited through the ordinary posture copy.
        workerOptions.Host.StringCompilationAllowed.Should().BeFalse();
        workerOptions.AgentCanSuspend.Should().BeFalse();
        workerOptions.Constraints.MaxRecursionDepth.Should().Be(21);
        workerOptions.Constraints.MaxArraySize.Should().Be(2048u);
        workerOptions.Constraints.RegexTimeout.Should().Be(TimeSpan.FromMilliseconds(333));
        workerOptions.Constraints.PromiseTimeout.Should().Be(TimeSpan.FromMilliseconds(444));
        workerOptions.Parsing.MaxSourceLength.Should().Be(55_555);
        workerOptions.Parsing.MaxNodeCount.Should().Be(4_321);
        workerOptions.Modules.MaxModuleCount.Should().Be(100);
        workerOptions.ResultLimits.Should().BeSameAs(parent.Options.ResultLimits);

        // Its budget constraints arrive through the factory replay.
        workerOptions.Constraints.ConstraintFactories.Should().NotBeEmpty();

        // And the marker stays behind, so the worker's own construction cannot re-expand over them.
        workerOptions.UntrustedCodeLimits.Should().BeNull();
    }

    // ---------------------------------------------------------------------------------------------------
    // The clock: a budget travels, the yardstick does not
    // ---------------------------------------------------------------------------------------------------

    /// <summary>
    /// A callee long enough to reach the interpreter's amortized constraint check (every 64 statements), so a
    /// case that expects a timeout to be consulted is not merely hoping it was.
    /// </summary>
    private const string TimedWork = """
        function work() {
            var total = 0;
            for (var i = 0; i < 200; i++) {
                total += i;
            }
            return total;
        }
        """;

    /// <summary>
    /// The defect <see href="https://github.com/sebastienros/jint/issues/3481">#3481</see> is about, from the
    /// side that shows it: a worker's replayed <c>LimitExecutionTime</c> used to read the clock of the
    /// <see cref="Options"/> instance the parent configured it on, because the factory closed over that group.
    /// The worker's own clock — the one its inherited <c>PromiseTimeout</c> is measured against — was ignored.
    /// </summary>
    /// <remarks>
    /// Deterministic rather than a race: the parent's clock never moves, so before the fix nothing this test
    /// does can make the worker time out, and the assertion below fails by not throwing.
    /// </remarks>
    [Test]
    public void AWorkersExecutionTimeoutIsMeasuredAgainstTheWorkersOwnClock()
    {
        var parentClock = new ManualClock();
        var parentOptions = new Options().LimitExecutionTime(TimeSpan.FromMilliseconds(50));
        parentOptions.Constraints.TimeProvider = parentClock;
        var parent = new Engine(parentOptions);

        var workerOptions = Request(parent).CreateDefaultOptions();
        workerOptions.Constraints.TimeProvider = new AdvancingClock(TimeSpan.FromMilliseconds(50));
        var worker = new Engine(workerOptions);
        worker.Execute(TimedWork);

        Invoking(() => worker.GetValue("work").Call())
            .Should().Throw<TimeoutException>(
                "the worker's execution timeout runs on the worker's clock, not on the one its parent was configured with");
    }

    /// <summary>
    /// The mirror, and the half that makes the first case a boundary rather than one lucky direction: the
    /// parent's clock racing past the interval must not end anything on the worker.
    /// </summary>
    [Test]
    public void AParentsClockDoesNotBoundItsWorker()
    {
        var parentOptions = new Options().LimitExecutionTime(TimeSpan.FromMilliseconds(50));
        parentOptions.Constraints.TimeProvider = new AdvancingClock(TimeSpan.FromMilliseconds(50));
        var parent = new Engine(parentOptions);

        // The parent's own entries do time out; that is what its clock is for.
        parent.Execute(TimedWork);
        Invoking(() => parent.GetValue("work").Call()).Should().Throw<TimeoutException>();

        var workerOptions = Request(parent).CreateDefaultOptions();
        workerOptions.Constraints.TimeProvider = new ManualClock();
        var worker = new Engine(workerOptions);
        worker.Execute(TimedWork);

        Invoking(() => worker.GetValue("work").Call())
            .Should().NotThrow("nothing the parent's clock does may end a worker's execution");
    }

    /// <summary>
    /// The other budget on the same clock. <c>PromiseTimeout</c> travels as a value and its drain has always
    /// read the worker's own <c>Options.Constraints.TimeProvider</c>; what changed is that the execution
    /// timeout beside it now reads the same one, so a host steering a worker's clock steers both.
    /// </summary>
    [Test]
    public void AWorkersInheritedPromiseTimeoutIsMeasuredAgainstTheSameClock()
    {
        var timeout = TimeSpan.FromMilliseconds(200);
        var parentOptions = new Options();
        parentOptions.Constraints.PromiseTimeout = timeout;
        parentOptions.Constraints.TimeProvider = new ManualClock();
        var parent = new Engine(parentOptions);

        var workerOptions = Request(parent).CreateDefaultOptions();
        workerOptions.Constraints.PromiseTimeout.Should().Be(timeout, "the budget is a value setting and travels");
        workerOptions.Constraints.TimeProvider = new AdvancingClock(timeout);
        var worker = new Engine(workerOptions);

        // A promise nothing will ever settle. The bound has to end the drain, and it is the worker's clock
        // that decides the bound has elapsed - the parent's is frozen.
        Invoking(() => worker.Evaluate("new Promise(function () {})").UnwrapIfPromise())
            .Should().Throw<PromiseRejectedException>().WithMessage("*Timeout of 00:00:00.2000000*");
    }

    /// <summary>
    /// The classification itself. A clock is host wiring and stays behind — the decision
    /// <see href="https://github.com/sebastienros/jint/issues/3481">#3481</see> asked for — and it is named
    /// rather than merely omitted, which is the whole purpose of the two lists.
    /// </summary>
    [Test]
    public void TheDefaultOptionsDoNotInheritTheParentsClock()
    {
        var parentOptions = new Options();
        parentOptions.Constraints.TimeProvider = new ManualClock();
        var parent = new Engine(parentOptions);

        var workerOptions = Request(parent).CreateDefaultOptions();

        workerOptions.Constraints.TimeProvider.Should().BeSameAs(
            TimeProvider.System,
            "a worker is a separate agent on a separate thread; a host's clock is handed to one engine");

        Options.SecurityPostureNotInherited.Should().Contain(
            "Constraints.TimeProvider",
            "a setting that stays behind is a decision on the record, not an omission");
    }

    /// <summary>A clock the test moves itself, reporting ticks in <see cref="TimeSpan"/> units.</summary>
    private sealed class ManualClock : TimeProvider
    {
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => 0;
    }

    /// <summary>
    /// A clock that moves on by <paramref name="step"/> every time it is read, so an entry that arms its
    /// deadline from one reading provably outlives it by the next.
    /// </summary>
    private sealed class AdvancingClock(TimeSpan step) : TimeProvider
    {
        private long _timestamp;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp()
        {
            var now = _timestamp;
            _timestamp += step.Ticks;
            return now;
        }
    }

    // ---------------------------------------------------------------------------------------------------
    // Features: grants never travel by implication
    // ---------------------------------------------------------------------------------------------------

    [TestCase(WebApiFeatures.Fetch)]
    [TestCase(WebApiFeatures.EventSource)]
    [TestCase(WebApiFeatures.WebSocket)]
    [TestCase(WebApiFeatures.Storage)]
    [TestCase(WebApiFeatures.CacheApi)]
    [TestCase(WebApiFeatures.FetchEvents)]
    [TestCase(WebApiFeatures.Workers)]
    public void TheDefaultOptionsSubtractNetworkStorageRoutingAndWorkers(WebApiFeatures granted)
    {
        var parent = new Engine(options => options.UseWebApis(WebApiFeatures.Default | granted));
        (parent.WebApi.Features & granted).Should().Be(granted, "the parent really was granted it");

        var features = Request(parent).CreateDefaultOptions().WebApi.Features;

        (features & granted).Should().Be(WebApiFeatures.None, "a worker gets strictly fewer capabilities than its creator");
    }

    [Test]
    public void TheDefaultOptionsForceMessagingAndGlobalEvents()
    {
        var parent = new Engine(options => options.UseWebApis(WebApiFeatures.Console));

        var features = Request(parent).CreateDefaultOptions().WebApi.Features;

        (features & WebApiFeatures.Messaging).Should().Be(WebApiFeatures.Messaging, "both halves of the connection are ports");
        (features & WebApiFeatures.GlobalEvents).Should().Be(WebApiFeatures.GlobalEvents, "the worker global is built out of them");
        (features & WebApiFeatures.Console).Should().Be(WebApiFeatures.Console, "what the parent had, and is not a grant, travels");
    }

    [Test]
    public void TheDefaultOptionsDoNotCopyTheProvider()
    {
        var provider = new TestWorkerProvider();
        var parent = new Engine(options => options.UseWebApis().UseWorkers(provider));

        var workerOptions = Request(parent).CreateDefaultOptions();

        workerOptions.WebApi.Workers.Provider.Should().BeNull("nesting is off by default");
        (workerOptions.WebApi.Features & WebApiFeatures.Workers).Should().Be(WebApiFeatures.None);
    }

    [Test]
    public void TheDefaultOptionsInstallTheNullSink()
    {
        var parent = new Engine();

        var workerOptions = Request(parent).CreateDefaultOptions();

        workerOptions.WebApi.Diagnostics.Sink.Should().BeSameAs(
            DiagnosticsSink.Null,
            "it is what flips a worker's callbacks to report-and-continue, and what the error relay reads");
    }

    [Test]
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

    [Test]
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

    /// <summary>
    /// <c>End()</c> is idempotent under <i>concurrent</i> callers, not merely repeated ones — which is the whole
    /// reason <c>TryEnd</c> takes a lock rather than testing and setting a <c>volatile bool</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The rendezvous is the point. An earlier form of this pin used <c>Parallel.For(0, 64, …)</c>, and it
    /// passed against a <c>TryEnd</c> with the lock removed — five times out of five: the partitioner ramps its
    /// workers up, so by the time a second one starts the first has long since set the flag and there is no
    /// race left to lose. A <see cref="Barrier"/> releases every thread into the same instant instead, and the
    /// threads are reused across rounds so that many attempts cost a handful of threads rather than thousands.
    /// </para>
    /// <para>
    /// The rounds are what make the pin honest rather than lucky: one interleaving proves nothing, and the
    /// unlocked version loses this within a few rounds. There is no wall-clock assertion anywhere — a slow
    /// machine makes this test slower, never redder — and the joins carry a ceiling only so that a deadlocked
    /// <c>End()</c> fails the test instead of hanging the run.
    /// </para>
    /// </remarks>
    [Test]
    public void WorkerConnectionEndIsIdempotentUnderConcurrentCallers()
    {
        const int Rounds = 64;
        var racers = Math.Max(4, Environment.ProcessorCount);

        using var cts = new CancellationTokenSource();

        var callbacks = new int[Rounds];
        var reasons = new ConcurrentBag<WorkerEndReason>[Rounds];
        var connections = new WorkerConnection[Rounds];
        for (var round = 0; round < Rounds; round++)
        {
            var index = round;
            reasons[index] = [];
            connections[index] = new WorkerConnection(
                new Engine(),
                new Engine(),
                "w",
                (reason, _) =>
                {
                    Interlocked.Increment(ref callbacks[index]);
                    reasons[index].Add(reason);
                },
                cts.Token);
        }

        using var barrier = new Barrier(racers);
        var threads = new Thread[racers];
        for (var i = 0; i < racers; i++)
        {
            threads[i] = new Thread(() =>
            {
                for (var round = 0; round < Rounds; round++)
                {
                    barrier.SignalAndWait();
                    connections[round].End();
                }
            })
            {
                IsBackground = true,
                Name = "WorkerConnection.End racer",
            };

            threads[i].Start();
        }

        foreach (var thread in threads)
        {
            // A wedge ceiling: what is claimed is that End() never blocks its caller, and a call that blocks
            // blocks for ever on a Barrier nobody else will reach — so the budget separates "finished" from
            // "wedged" and never "fast" from "slow", however many racers the runner has cores for.
            thread.Join(TestBudgets.WedgeCeiling).Should().BeTrue("End() never blocks its caller");
        }

        for (var round = 0; round < Rounds; round++)
        {
            var connection = connections[round];

            Volatile.Read(ref callbacks[round]).Should().Be(
                1,
                "round {0}: the end sequence runs exactly once however many threads race for it",
                round);
            reasons[round].Should().ContainSingle().Which.Should().Be(WorkerEndReason.Terminated);
            connection.IsEnded.Should().BeTrue();
            connection.EndReason.Should().Be(WorkerEndReason.Terminated);
            connection.IsFaulted.Should().BeFalse();
            connection.Error.Should().BeNull();
        }
    }

    [Test]
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

    [Test]
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
