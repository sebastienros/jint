#if NET8_0_OR_GREATER
#nullable enable

using System.Threading;
using Jint;
using Jint.Runtime;
using Jint.Runtime.Modules;
using Jint.WebApi;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// The Workers surface seen from outside the assembly: what a host has to write, and what an engine that
/// asked for it has — which today is deliberately no script surface at all.
/// </summary>
/// <remarks>
/// <para>
/// This project has no <c>InternalsVisibleTo</c>, so everything reachable here is reachable by a third party.
/// That is the whole point for this feature: <see cref="WorkerProvider"/> is the extension point a host
/// implements, and a host cannot fabricate a <see cref="WorkerRequest"/> or a
/// <see cref="WorkerConnection"/> — both have internal constructors, because both are things the engine hands
/// over rather than things a host builds.
/// </para>
/// <para>
/// <see cref="TypeofWorkerAnswersOnlyWithBothTheFlagAndAProvider"/> replaced the foundation's
/// no-script-surface pin, deliberately: that one asserted <c>typeof Worker === 'undefined'</c> even with the
/// flag <i>and</i> a provider, which was true exactly while the constructor did not exist. Now that it does,
/// the same question has a three-way answer and the pin asserts it — the absent-with-flag-alone and
/// absent-with-provider-alone halves are the ones that survived unchanged.
/// </para>
/// </remarks>
public class WebApiWorkerTests
{
    /// <summary>
    /// A provider a third party could write: it derives from the public abstract class and overrides all
    /// three members using nothing this project cannot see.
    /// </summary>
    private sealed class RecordingWorkerProvider : WorkerProvider
    {
        public int Requests { get; private set; }

        public int Started { get; private set; }

        public int Ended { get; private set; }

        public WorkerEndReason? LastReason { get; private set; }

        public override Engine? CreateWorkerEngine(WorkerRequest request)
        {
            Requests++;

            // What a real provider reads before deciding, all of it public.
            _ = request.Parent;
            _ = request.Specifier;
            _ = request.ReferencingLocation;
            _ = request.Type;
            _ = request.Name;
            _ = request.Depth;
            _ = request.LiveWorkerCount;
            _ = request.TerminationToken;

            return null;
        }

        public override void OnWorkerStarted(WorkerConnection connection)
        {
            Started++;
            connection.HostState = new object();
        }

        public override void OnWorkerEnded(WorkerConnection connection, WorkerEndReason reason)
        {
            Ended++;
            LastReason = reason;
        }
    }

    [Fact]
    public void AHostWorkerProviderIsSubclassableOutsideTheAssembly()
    {
        var provider = new RecordingWorkerProvider();

        provider.Should().BeAssignableTo<WorkerProvider>();
        provider.Requests.Should().Be(0);
        provider.Started.Should().Be(0);
        provider.Ended.Should().Be(0);
        provider.LastReason.Should().BeNull();
    }

    [Fact]
    public void UseWorkersIsReachableAndSetsFlagAndProviderTogether()
    {
        var provider = new RecordingWorkerProvider();
        var options = new Options().UseWebApis().UseWorkers(provider);

        (options.WebApi.Features & WebApiFeatures.Workers).Should().Be(WebApiFeatures.Workers);
        options.WebApi.Workers.Provider.Should().BeSameAs(provider);
    }

    [Fact]
    public void TheWorkerOptionsGroupIsReachableAndCarriesTheDocumentedDefaults()
    {
        var options = new Options();

        options.WebApi.Workers.Provider.Should().BeNull("a worker needs a thread, and Jint never starts one");
        options.WebApi.Workers.MaxWorkers.Should().Be(16);
        options.WebApi.Workers.MaxQueuedMessages.Should().Be(16384);
    }

    [Fact]
    public void TheWorkersFlagIsNotPartOfTheDefaultFeatureSet()
    {
        (WebApiFeatures.Default & WebApiFeatures.Workers).Should().Be(WebApiFeatures.None);
    }

    /// <summary>
    /// The <c>Worker</c> global needs the flag <b>and</b> a provider, and there is deliberately no way to get a
    /// constructor that can only throw: a worker needs a thread and a pump, and Jint never starts either.
    /// </summary>
    [Fact]
    public void TypeofWorkerAnswersOnlyWithBothTheFlagAndAProvider()
    {
        var provider = new RecordingWorkerProvider();

        var neither = new Engine(options => options.UseWebApis());
        neither.Evaluate("typeof Worker").AsString().Should().Be("undefined");

        var flagOnly = new Engine(options => options.UseWebApis(WebApiFeatures.Default | WebApiFeatures.Workers));
        flagOnly.Evaluate("typeof Worker").AsString().Should().Be("undefined", "with no provider there is no thread");

        var providerOnly = new Engine(options =>
        {
            options.UseWebApis();
            options.WebApi.Workers.Provider = provider;
        });
        providerOnly.Evaluate("typeof Worker").AsString().Should().Be("undefined", "the flag is still the grant");

        var both = new Engine(options => options.UseWebApis().UseWorkers(provider));
        both.Evaluate("typeof Worker").AsString().Should().Be("function");

        // The interface objects that would make `self instanceof WorkerGlobalScope` lie stay absent whatever
        // was enabled — the ruling this feature shares with the interface-globals decision.
        both.Evaluate("typeof WorkerGlobalScope").AsString().Should().Be("undefined");
        both.Evaluate("typeof DedicatedWorkerGlobalScope").AsString().Should().Be("undefined");

        provider.Requests.Should().Be(0, "building an engine consults no provider");
    }

    /// <summary>
    /// A provider that refuses everything is still reachable, and its refusal is what the script sees.
    /// </summary>
    [Fact]
    public void AProviderRefusalIsReachableFromScript()
    {
        var provider = new RecordingWorkerProvider();
        var engine = new Engine(options => options.UseWebApis().UseWorkers(provider));

        var exception = Assert.Throws<JavaScriptException>(
            () => engine.Execute("new Worker('./worker.js', { type: 'module' })"));

        exception.Error.Get("name").AsString().Should().Be("SecurityError");
        provider.Requests.Should().Be(1);
    }

    /// <summary>
    /// A worker gets strictly fewer capabilities than its creator, and the one that matters most is the
    /// network. Reachable through the real constructor now, rather than through a hand-built request.
    /// </summary>
    [Fact]
    public void AWorkerDoesNotInheritNetworkAccess()
    {
        var host = new PumpOnDemandWorkerHost(new Dictionary<string, string>
        {
            ["./worker.js"] = """
                report('fetch:' + typeof fetch);
                report('WebSocket:' + typeof WebSocket);
                report('EventSource:' + typeof EventSource);
                report('localStorage:' + typeof localStorage);
                report('caches:' + typeof caches);
                report('Worker:' + typeof Worker);
                report('postMessage:' + typeof postMessage);
                """,
        });

        var parent = new Engine(options =>
        {
            options.UseWebApis(WebApiFeatures.Default | WebApiFeatures.Fetch | WebApiFeatures.WebSocket | WebApiFeatures.Storage | WebApiFeatures.CacheApi);
            options.UseWorkers(host);
        });

        parent.Evaluate("typeof fetch").AsString().Should().Be("function", "the parent really was granted it");

        parent.Execute("var w = new Worker('./worker.js', { type: 'module' });");
        host.Drain(parent);

        host.Log.Should().Be(
            "fetch:undefined,WebSocket:undefined,EventSource:undefined,localStorage:undefined,caches:undefined,Worker:undefined,postMessage:function");
    }

    /// <summary>
    /// The worker's global object is a <c>DedicatedWorkerGlobalScope</c>, and the prototype chain is what
    /// says so — which is what the canonical worker feature-detect asks and what every worker library in the
    /// wild is written against.
    /// </summary>
    /// <remarks>
    /// The chain stops one link short of HTML's, at <c>%Object.prototype%</c> where the standard has
    /// <c>EventTarget.prototype</c>: Jint's global object is not an <c>EventTarget</c>, so claiming the
    /// inheritance would make <c>self instanceof EventTarget</c> true while <c>addEventListener</c> failed its
    /// brand check. That is asserted here rather than merely documented, because a host reading the chain is
    /// entitled to know where it ends.
    /// </remarks>
    [Fact]
    public void TheWorkerGlobalIsADedicatedWorkerGlobalScope()
    {
        var host = new PumpOnDemandWorkerHost(new Dictionary<string, string>
        {
            ["./worker.js"] = """
                report('sniff:' + ('DedicatedWorkerGlobalScope' in self && self instanceof DedicatedWorkerGlobalScope));
                report('base:' + (self instanceof WorkerGlobalScope));
                report('proto:' + (Object.getPrototypeOf(self) === DedicatedWorkerGlobalScope.prototype));
                report('chain:' + (Object.getPrototypeOf(DedicatedWorkerGlobalScope.prototype) === WorkerGlobalScope.prototype));
                report('root:' + (Object.getPrototypeOf(WorkerGlobalScope.prototype) === Object.prototype));
                report('inherit:' + (Object.getPrototypeOf(DedicatedWorkerGlobalScope) === WorkerGlobalScope));
                report('shim:' + [DedicatedWorkerGlobalScope, WorkerGlobalScope].some(
                    c => Object.prototype.hasOwnProperty.call(c, Symbol.hasInstance)));
                report('tag:' + Object.prototype.toString.call(self));
                report('new:' + (function () {
                    try { new DedicatedWorkerGlobalScope(); return 'no throw'; } catch (e) { return e.constructor.name; }
                })());
                """,
        });

        var parent = new Engine(options => options.UseWebApis().UseWorkers(host));

        // [Exposed=Worker] and [Exposed=DedicatedWorker]: the engine that creates workers is not one.
        parent.Evaluate("typeof WorkerGlobalScope").AsString().Should().Be("undefined");
        parent.Evaluate("typeof DedicatedWorkerGlobalScope").AsString().Should().Be("undefined");
        parent.Evaluate("Object.getPrototypeOf(globalThis) === Object.prototype").AsBoolean().Should().BeTrue();

        parent.Execute("var w = new Worker('./worker.js', { type: 'module' });");
        host.Drain(parent);

        host.Log.Should().Be(
            "sniff:true,base:true,proto:true,chain:true,root:true,inherit:true,shim:false,"
            + "tag:[object DedicatedWorkerGlobalScope],new:TypeError");
    }

    /// <summary>
    /// One provider serving a pool of engines reaches per-request policy through
    /// <c>request.Parent.Advanced.HostDefined</c>, which is per engine and which the engine never reads.
    /// </summary>
    [Fact]
    public void TheProviderCanReachPerRequestStateThroughHostDefined()
    {
        var seen = new List<string>();
        var host = new PumpOnDemandWorkerHost(new Dictionary<string, string> { ["./worker.js"] = "" })
        {
            Inspect = request => seen.Add((string) request.Parent.Advanced.HostDefined!),
        };

        var shared = new Options().UseWebApis().UseWorkers(host);

        var tenantA = new Engine(shared);
        tenantA.Advanced.HostDefined = "tenant-a";
        var tenantB = new Engine(shared);
        tenantB.Advanced.HostDefined = "tenant-b";

        tenantA.Execute("new Worker('./worker.js', { type: 'module' });");
        tenantB.Execute("new Worker('./worker.js', { type: 'module' });");

        seen.Should().Equal("tenant-a", "tenant-b");
    }

    /// <summary>
    /// <c>OnWorkerStarted</c> is where a host starts pumping, and a thread-per-worker provider starts that
    /// thread from inside it — so the engine wiring has to have let go of the worker engine before the call.
    /// </summary>
    /// <remarks>
    /// The callback waits for that thread's first pump before returning, which is what makes the pin
    /// deterministic: the engine is <i>provably</i> still inside <c>OnWorkerStarted</c> while another thread
    /// enters it. Holding the construction's ownership across the callback makes that pump the engine's own
    /// concurrent-use exception. Pumping from the callback's own thread would prove nothing — same-thread
    /// re-entry is always allowed.
    /// </remarks>
    [Fact]
    public void OnWorkerStartedSeesAPumpableEngine()
    {
        Exception? pumpFailure = null;
        var host = new PumpOnDemandWorkerHost(new Dictionary<string, string> { ["./worker.js"] = "report('ran');" })
        {
            OnStarted = connection =>
            {
                using var pumped = new ManualResetEventSlim(false);
                var pump = new Thread(() =>
                {
                    try
                    {
                        connection.Worker.Advanced.ProcessTasks();
                    }
                    catch (Exception ex)
                    {
                        pumpFailure = ex;
                    }
                    finally
                    {
                        pumped.Set();
                    }
                })
                {
                    IsBackground = true,
                    Name = "worker pump",
                };

                pump.Start();
                pumped.Wait(TimeSpan.FromSeconds(10)).Should().BeTrue();
                pump.Join(TimeSpan.FromSeconds(10)).Should().BeTrue();
            },
        };

        var parent = new Engine(options => options.UseWebApis().UseWorkers(host));
        parent.Execute("new Worker('./worker.js', { type: 'module' });");

        pumpFailure.Should().BeNull("the worker engine is the host's from the moment this callback runs");
        host.Log.Should().Be("ran", "the worker's module runs on the first pump the host gives it");
    }

    /// <summary>
    /// Several workers cooperating on one host loop — the game-frame shape — each make progress, and neither
    /// starves the other.
    /// </summary>
    [Fact]
    public void TwoWorkersPumpedFromOneLoopBothMakeProgress()
    {
        var host = new PumpOnDemandWorkerHost(new Dictionary<string, string>
        {
            ["./a.js"] = "addEventListener('message', e => postMessage('a:' + e.data));",
            ["./b.js"] = "addEventListener('message', e => postMessage('b:' + e.data));",
        });

        var parent = new Engine(options => options.UseWebApis().UseWorkers(host));
        parent.Execute("""
            var got = [];
            var a = new Worker('./a.js', { type: 'module' });
            var b = new Worker('./b.js', { type: 'module' });
            a.onmessage = e => got.push(e.data);
            b.onmessage = e => got.push(e.data);
            a.postMessage(1);
            b.postMessage(2);
            """);

        host.Connections.Should().HaveCount(2);
        host.Drain(parent);

        var got = parent.Evaluate("got.slice().sort().join(',')").AsString();
        got.Should().Be("a:1,b:2");
    }

    /// <summary>
    /// The shape the feature exists for: the host gives each worker a thread of its own, and a full
    /// <c>postMessage</c> round trip crosses it.
    /// </summary>
    /// <remarks>
    /// Event-driven throughout, with a ten-second ceiling on every wait so that a loaded machine makes this
    /// slower rather than redder. The worker's thread parks on its own reset event, which the parent's pump
    /// wakes — the engine's own wait primitive is a separate change and this deliberately does not depend on
    /// it.
    /// </remarks>
    [Fact]
    public void AHostProviderThreadPerWorkerRoundTrips()
    {
        using var host = new ThreadPerWorkerHost();

        var parent = new Engine(options => options.UseWebApis().UseWorkers(host));
        parent.Execute("""
            var got = null;
            var w = new Worker('./worker.js', { type: 'module' });
            w.onmessage = e => { got = e.data; };
            w.postMessage('ping');
            """);

        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (DateTime.UtcNow < deadline && parent.Evaluate("got").IsNull())
        {
            parent.Advanced.ProcessTasks();
            host.Answered.Wait(TimeSpan.FromMilliseconds(50));
        }

        parent.Evaluate("got").AsString().Should().Be("pong:ping");

        parent.Execute("w.terminate();");
        host.WaitForPumpToLeave();
    }

    /// <summary>
    /// A startup failure reaches the host through the connection alone — no sink, no listener, nothing wired.
    /// </summary>
    /// <remarks>
    /// The two channels carry different things and this pin is about the one a host gets for free: the reason
    /// says <i>what</i> happened rather than blaming a <c>terminate()</c> nobody called, and the failure is a
    /// CLR exception rather than a worker-realm value that could not have crossed the thread anyway. The
    /// script-facing half — a plain <c>Event</c> named <c>error</c> at the <c>Worker</c> object — is asserted
    /// beside it, because a host that does wire a listener gets both.
    /// </remarks>
    [Fact]
    public void AHostSeesAStartupFailureWithoutWiringAnySink()
    {
        var host = new PumpOnDemandWorkerHost(new Dictionary<string, string>());

        var parent = new Engine(options => options.UseWebApis().UseWorkers(host));
        parent.Execute("""
            var seen = [];
            var w = new Worker('./missing.js', { type: 'module' });
            w.onerror = e => seen.push(e.type + '|' + (e instanceof ErrorEvent));
            """);

        var connection = host.Connections.Should().ContainSingle().Subject;
        connection.IsEnded.Should().BeFalse("nothing has pumped the worker yet");

        host.Drain(parent);

        connection.IsEnded.Should().BeTrue();
        connection.EndReason.Should().Be(WorkerEndReason.StartupFailed);
        connection.IsFaulted.Should().BeTrue();
        connection.Error.Should().BeOfType<ModuleResolutionException>();
        connection.TerminationToken.IsCancellationRequested.Should().BeTrue();

        parent.Evaluate("seen.join(',')").AsString().Should().Be(
            "error|false",
            "the standard's step for a script that failed to fetch names no interface");
    }

    /// <summary>
    /// A provider that chains its own <see cref="DiagnosticsSink"/> onto the worker's options still sees every
    /// worker error, whatever the parent's script does about it — the sink's contract is that a script may not
    /// switch it off, and the parent-side relay deliberately sits beside it rather than on it.
    /// </summary>
    /// <remarks>
    /// Both directions are asserted from one run: the worker's own sink hears the failure as an
    /// <c>UncaughtCallbackError</c> even though the parent cancelled the propagation, and the parent's sink
    /// stays silent because cancelling is exactly what HTML's <i>notHandled</i> gate is for.
    /// </remarks>
    [Fact]
    public void ADiagnosticsSinkChainedByTheProviderStillSeesWorkerErrors()
    {
        var workerSink = new CollectingSink();
        var parentSink = new CollectingSink();

        var host = new PumpOnDemandWorkerHost(new Dictionary<string, string>
        {
            ["./worker.js"] = "addEventListener('message', () => { throw new TypeError('boom'); });",
        })
        {
            Tune = options => options.WebApi.Diagnostics.Sink = workerSink,
        };

        var parent = new Engine(options =>
        {
            options.UseWebApis().UseWorkers(host);
            options.WebApi.Diagnostics.Sink = parentSink;
        });

        parent.Execute("""
            var seen = [];
            var w = new Worker('./worker.js', { type: 'module' });
            w.onerror = e => { seen.push(e.message + '|' + (e.error === null)); e.preventDefault(); };
            w.postMessage('go');
            """);

        host.Drain(parent);

        parent.Evaluate("seen.join(',')").AsString().Should().Be("boom|true", "error is null for every worker error");

        workerSink.Kinds.Should().Equal(DiagnosticEventKind.UncaughtCallbackError);
        workerSink.Messages.Should().ContainSingle().Which.Should().Contain("boom");

        parentSink.Kinds.Should().BeEmpty("the Worker object's listener cancelled, which is HTML's gate");
    }

    /// <summary>
    /// The same failure with nothing cancelling it reaches the parent's sink as its own kind, so a host that
    /// wires one sink for a parent and its workers can still tell the two reports apart.
    /// </summary>
    [Fact]
    public void AnUnhandledWorkerErrorReachesTheParentsSinkAsItsOwnKind()
    {
        var sink = new CollectingSink();

        var host = new PumpOnDemandWorkerHost(new Dictionary<string, string>
        {
            ["./worker.js"] = "addEventListener('message', () => { throw new TypeError('boom'); });",
        })
        {
            Tune = options => options.WebApi.Diagnostics.Sink = sink,
        };

        var parent = new Engine(options =>
        {
            options.UseWebApis().UseWorkers(host);
            options.WebApi.Diagnostics.Sink = sink;
        });

        parent.Execute("var w = new Worker('./worker.js', { type: 'module' }); w.postMessage('go');");
        host.Drain(parent);

        sink.Kinds.Should().Equal(DiagnosticEventKind.UncaughtCallbackError, DiagnosticEventKind.WorkerError);
        sink.Messages.Should().AllSatisfy(message => message.Should().Contain("boom"));
    }

    /// <summary>
    /// A sink that keeps kinds and messages as CLR values, which is what the remarks on
    /// <see cref="DiagnosticsSink"/> say to do with a report rather than stashing its <c>JsValue</c>s.
    /// </summary>
    private sealed class CollectingSink : DiagnosticsSink
    {
        public List<DiagnosticEventKind> Kinds { get; } = new();

        public List<string> Messages { get; } = new();

        public override void Report(DiagnosticEvent report)
        {
            Kinds.Add(report.Kind);
            Messages.Add(report.Exception?.Message ?? report.Value.ToString());
        }
    }

    /// <summary>
    /// A provider the test drives itself: it builds the worker engine over an in-memory module map, starts no
    /// thread, and lets the test decide when each worker gets a turn.
    /// </summary>
    private sealed class PumpOnDemandWorkerHost : WorkerProvider
    {
        private readonly Dictionary<string, string> _modules;
        private readonly List<string> _log = new();

        public PumpOnDemandWorkerHost(Dictionary<string, string> modules) => _modules = modules;

        public Action<WorkerRequest>? Inspect { get; set; }

        public Action<WorkerConnection>? OnStarted { get; set; }

        /// <summary>Runs on the worker's options, after <c>CreateDefaultOptions</c> and before the engine.</summary>
        public Action<Options>? Tune { get; set; }

        public List<WorkerConnection> Connections { get; } = new();

        public string Log => string.Join(",", _log);

        public override Engine? CreateWorkerEngine(WorkerRequest request)
        {
            Inspect?.Invoke(request);

            var options = request.CreateDefaultOptions();
            options.Modules.ModuleLoader = new MapModuleLoader(_modules);
            Tune?.Invoke(options);

            var engine = new Engine(options);
            engine.SetValue("report", new Action<string>(_log.Add));
            return engine;
        }

        public override void OnWorkerStarted(WorkerConnection connection)
        {
            Connections.Add(connection);
            OnStarted?.Invoke(connection);
        }

        public void Drain(Engine parent, int rounds = 50)
        {
            for (var i = 0; i < rounds; i++)
            {
                foreach (var connection in Connections)
                {
                    connection.Worker.Advanced.ProcessTasks();
                }

                parent.Advanced.ProcessTasks();
            }
        }
    }

    /// <summary>
    /// The thread-per-worker shape, written the way <see cref="WorkerProvider"/> documents it: the connection
    /// is registered before the pump starts, the loop watches <see cref="WorkerConnection.IsEnded"/>, and the
    /// engine is disposed on the thread that was pumping it.
    /// </summary>
    private sealed class ThreadPerWorkerHost : WorkerProvider, IDisposable
    {
        private readonly ManualResetEventSlim _left = new(false);
        private Thread? _thread;

        public ManualResetEventSlim Answered { get; } = new(false);

        public override Engine? CreateWorkerEngine(WorkerRequest request)
        {
            var options = request.CreateDefaultOptions();
            options.Modules.ModuleLoader = new MapModuleLoader(new Dictionary<string, string>
            {
                ["./worker.js"] = "addEventListener('message', e => { postMessage('pong:' + e.data); answered(); });",
            });

            var engine = new Engine(options);
            engine.SetValue("answered", new Action(() => Answered.Set()));
            return engine;
        }

        public override void OnWorkerStarted(WorkerConnection connection)
        {
            _thread = new Thread(() =>
            {
                try
                {
                    while (!connection.IsEnded)
                    {
                        connection.Worker.Advanced.ProcessTasks();
                        connection.TerminationToken.WaitHandle.WaitOne(TimeSpan.FromMilliseconds(5));
                    }
                }
                catch (ExecutionCanceledException)
                {
                    // terminate()'s cooperative half, observed on this thread. Leaving is what it asks for.
                }
                finally
                {
                    // On the pumping thread, after the loop — never from OnWorkerEnded.
                    connection.Worker.Dispose();
                    _left.Set();
                }
            })
            {
                IsBackground = true,
                Name = "worker pump",
            };

            _thread.Start();
        }

        public void WaitForPumpToLeave()
            => _left.Wait(TimeSpan.FromSeconds(10)).Should().BeTrue("the loop observes IsEnded and leaves");

        public void Dispose()
        {
            _left.Dispose();
            Answered.Dispose();
        }
    }

    /// <summary>
    /// A module loader over a dictionary, built out of the public module API alone.
    /// </summary>
    private sealed class MapModuleLoader : IModuleLoader
    {
        private readonly Dictionary<string, string> _sources;

        public MapModuleLoader(Dictionary<string, string> sources) => _sources = sources;

        public ResolvedSpecifier Resolve(string? referencingModuleLocation, ModuleRequest moduleRequest)
        {
            if (!_sources.ContainsKey(moduleRequest.Specifier))
            {
                throw new ModuleResolutionException("Module not found", moduleRequest.Specifier, referencingModuleLocation, filePath: null);
            }

            return new ResolvedSpecifier(moduleRequest, moduleRequest.Specifier, Uri: null, SpecifierType.RelativeOrAbsolute);
        }

        public Jint.Runtime.Modules.Module LoadModule(Engine engine, ResolvedSpecifier resolved)
            => ModuleFactory.BuildSourceTextModule(engine, resolved, _sources[resolved.Key]);
    }
}
#endif
