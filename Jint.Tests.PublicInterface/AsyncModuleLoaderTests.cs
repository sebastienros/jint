using System.Collections.Concurrent;
using Jint.Native;
using Jint.Runtime;
using Jint.Runtime.Modules;

using Module = Jint.Runtime.Modules.Module;

#nullable enable

namespace Jint.Tests.PublicInterface;

/// <summary>
/// The asynchronous module loading surface: a host that fetches module source over I/O — HTTP, a dev server,
/// an asset pipeline — cannot answer <see cref="IModuleLoader.LoadModule"/> without blocking the thread that
/// asked, which is not viable on a game loop or a UI thread. Such a host implements
/// <see cref="IAsyncModuleLoader"/> instead and finishes each load through a
/// <see cref="ModuleLoadCompletion"/>, whenever and on whatever thread the content arrives.
/// </summary>
public class AsyncModuleLoaderTests
{
    /// <summary>
    /// A loader that hands every request to the test and finishes nothing by itself, so a test can prove the
    /// engine really does carry on without the answer.
    /// </summary>
    private sealed class DeferredModuleLoader : IAsyncModuleLoader
    {
        private readonly List<ModuleLoadCompletion> _pending = new();
        private readonly ConcurrentDictionary<string, int> _asked = new(StringComparer.Ordinal);

        public IReadOnlyList<ModuleLoadCompletion> Pending => _pending;

        /// <summary>How many times the loader was asked for each specifier.</summary>
        public int AskedFor(string specifier) => _asked.TryGetValue(specifier, out var count) ? count : 0;

        public ResolvedSpecifier Resolve(string? referencingModuleLocation, ModuleRequest moduleRequest)
            => new(moduleRequest, moduleRequest.Specifier, Uri: null, SpecifierType.Bare);

        public Module LoadModule(Engine engine, ResolvedSpecifier resolved)
            => throw new InvalidOperationException("The engine must not take the synchronous path for an IAsyncModuleLoader.");

        public void LoadModuleAsync(Engine engine, ResolvedSpecifier resolved, ModuleLoadCompletion completion)
        {
            _asked.AddOrUpdate(resolved.ModuleRequest.Specifier, 1, static (_, count) => count + 1);
            _pending.Add(completion);
        }

        /// <summary>Answers the outstanding request for <paramref name="specifier"/> with source text.</summary>
        public void Deliver(string specifier, string code)
        {
            Take(specifier).SetSource(code);
        }

        public void Fail(string specifier, Exception exception)
        {
            Take(specifier).SetError(exception);
        }

        public ModuleLoadCompletion Take(string specifier)
        {
            var completion = _pending.FirstOrDefault(c => c.Resolved.ModuleRequest.Specifier == specifier);
            completion.Should().NotBeNull($"the loader should have been asked for '{specifier}'");
            _pending.Remove(completion!);
            return completion!;
        }
    }

    /// <summary>
    /// The shape a real integration takes: source arrives from somewhere with latency, and the loader answers
    /// out of a <see cref="Task"/> rather than inline.
    /// </summary>
    private sealed class DelayedModuleLoader : AsyncModuleLoader
    {
        private readonly IReadOnlyDictionary<string, string> _sources;
        private readonly TimeSpan _latency;

        public DelayedModuleLoader(IReadOnlyDictionary<string, string> sources, TimeSpan latency)
        {
            _sources = sources;
            _latency = latency;
        }

        public int Loads;

        public override ResolvedSpecifier Resolve(string? referencingModuleLocation, ModuleRequest moduleRequest)
            => new(moduleRequest, moduleRequest.Specifier, Uri: null, SpecifierType.Bare);

        protected override async Task<string> LoadModuleContentsAsync(Engine engine, ResolvedSpecifier resolved, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref Loads);
            await Task.Delay(_latency, cancellationToken).ConfigureAwait(false);

            if (!_sources.TryGetValue(resolved.ModuleRequest.Specifier, out var code))
            {
                throw new FileNotFoundException($"404 {resolved.ModuleRequest.Specifier}");
            }

            return code;
        }
    }

    [Fact]
    public async Task CanImportAModuleWhoseSourceArrivesAsynchronously()
    {
        var loader = new DelayedModuleLoader(new Dictionary<string, string>
        {
            ["./main.js"] = "import { value } from './dep.js'; export const result = value * 2;",
            ["./dep.js"] = "export const value = 21;",
        }, TimeSpan.FromMilliseconds(20));

        var engine = new Engine(options => options.EnableModules(loader));

        var ns = await engine.Modules.ImportAsync("./main.js");

        ns.Get("result").AsNumber().Should().Be(42);
        loader.Loads.Should().Be(2);
    }

    [Fact]
    public async Task StaticImportsInsideAnAsynchronouslyLoadedModuleResolveTheSameWay()
    {
        // The load phase is recursive: a module fetched asynchronously has its own static imports fetched the
        // same way, however deep the chain, before anything is linked.
        var loader = new DelayedModuleLoader(new Dictionary<string, string>
        {
            ["./a.js"] = "import { b } from './b.js'; export const a = b + 'a';",
            ["./b.js"] = "import { c } from './c.js'; export const b = c + 'b';",
            ["./c.js"] = "import { d } from './d.js'; export const c = d + 'c';",
            ["./d.js"] = "export const d = 'd';",
        }, TimeSpan.FromMilliseconds(5));

        var engine = new Engine(options => options.EnableModules(loader));

        var ns = await engine.Modules.ImportAsync("./a.js");

        ns.Get("a").AsString().Should().Be("dcba");
        loader.Loads.Should().Be(4);
    }

    [Fact]
    public async Task ADiamondDependencyIsFetchedOnce()
    {
        var loader = new DelayedModuleLoader(new Dictionary<string, string>
        {
            ["./main.js"] = "import { l } from './left.js'; import { r } from './right.js'; export const both = l + r;",
            ["./left.js"] = "import { shared } from './shared.js'; export const l = shared;",
            ["./right.js"] = "import { shared } from './shared.js'; export const r = shared;",
            ["./shared.js"] = "export const shared = 1;",
        }, TimeSpan.FromMilliseconds(20));

        var engine = new Engine(options => options.EnableModules(loader));

        var ns = await engine.Modules.ImportAsync("./main.js");

        ns.Get("both").AsNumber().Should().Be(2);
        // Both branches want './shared.js' while the first fetch of it is still in flight; the second must
        // attach to that load rather than start another one.
        loader.Loads.Should().Be(4);
    }

    [Fact]
    public async Task ALoaderFailureBecomesARejectedPromise()
    {
        var loader = new DelayedModuleLoader(new Dictionary<string, string>
        {
            ["./main.js"] = "import './missing.js';",
        }, TimeSpan.FromMilliseconds(5));

        var engine = new Engine(options => options.EnableModules(loader));

        var ex = await Invoking(() => engine.Modules.ImportAsync("./main.js")).Should().ThrowAsync<PromiseRejectedException>();
        ex.Which.RejectedValue.Get("message").AsString().Should().Contain("404 ./missing.js");
    }

    [Fact]
    public void ADynamicImportKeepsTheEngineRunningWhileTheLoadIsInFlight()
    {
        // The point of the whole exercise: evaluation returns, the script goes on, and nothing is blocked
        // waiting for the module. The promise settles on a later turn of the event loop.
        var loader = new DeferredModuleLoader();
        var engine = new Engine(options => options.EnableModules(loader));

        engine.Execute("globalThis.log = []; import('./late.js').then(ns => log.push('loaded:' + ns.value), e => log.push('failed:' + e.message)); log.push('after-import');");

        engine.Evaluate("log.join(',')").AsString().Should().Be("after-import");
        loader.Pending.Should().ContainSingle();

        loader.Deliver("./late.js", "export const value = 7;");
        engine.Advanced.ProcessTasks();

        engine.Evaluate("log.join(',')").AsString().Should().Be("after-import,loaded:7");
    }

    [Fact]
    public void ADynamicImportRejectsWhenTheHostFailsTheLoad()
    {
        var loader = new DeferredModuleLoader();
        var engine = new Engine(options => options.EnableModules(loader));

        engine.Execute("globalThis.outcome = 'pending'; import('./gone.js').then(() => { outcome = 'resolved'; }, e => { outcome = e.message; });");

        engine.Evaluate("outcome").AsString().Should().Be("pending");

        loader.Fail("./gone.js", new HttpRequestExceptionStandIn("connection refused"));
        engine.Advanced.ProcessTasks();

        engine.Evaluate("outcome").AsString().Should().Be("connection refused");
    }

    [Fact]
    public void AHostCanFinishALoadFromAnotherThread()
    {
        var loader = new DeferredModuleLoader();
        var engine = new Engine(options => options.EnableModules(loader));

        engine.Execute("globalThis.value = 0; import('./worker.js').then(ns => { value = ns.value; });");

        var completion = loader.Take("./worker.js");
        var thread = new Thread(() => completion.SetSource("export const value = 99;"));
        thread.Start();
        thread.Join();

        // Nothing of the engine ran on that thread: the completion was queued, and only a turn taken here
        // applies it.
        engine.Evaluate("value").AsNumber().Should().Be(0);

        engine.Advanced.ProcessTasks();
        engine.Evaluate("value").AsNumber().Should().Be(99);
    }

    [Fact]
    public void TheLoaderIsAskedOncePerReferrerAndSpecifier()
    {
        // https://tc39.es/ecma262/#sec-HostLoadImportedModule: "each time this operation is called with a
        // specific referrer, specifier, ... it must perform FinishLoadingImportedModule with the same
        // module". Jint keeps that promise by never asking twice.
        var loader = new DeferredModuleLoader();
        var engine = new Engine(options => options.EnableModules(loader));

        engine.Execute("globalThis.results = []; import('./twice.js').then(ns => results.push(ns.value));");
        loader.Deliver("./twice.js", "export const value = 'first';");
        engine.Advanced.ProcessTasks();

        engine.Execute("import('./twice.js').then(ns => results.push(ns.value));");
        engine.Advanced.ProcessTasks();

        engine.Evaluate("results.join(',')").AsString().Should().Be("first,first");
        loader.AskedFor("./twice.js").Should().Be(1);
    }

    [Fact]
    public void SettlingALoadTwiceIsIgnored()
    {
        var loader = new DeferredModuleLoader();
        var engine = new Engine(options => options.EnableModules(loader));

        engine.Execute("globalThis.value = 0; import('./once.js').then(ns => { value = ns.value; });");

        var completion = loader.Take("./once.js");
        completion.IsCompleted.Should().BeFalse();
        completion.SetSource("export const value = 1;");
        completion.IsCompleted.Should().BeTrue();
        completion.SetSource("export const value = 2;");
        completion.SetError("too late");

        engine.Advanced.ProcessTasks();
        engine.Evaluate("value").AsNumber().Should().Be(1);
    }

    [Fact]
    public void AnAsyncLoaderCanAnswerWithAModuleItBuiltItself()
    {
        var loader = new DeferredModuleLoader();
        var engine = new Engine(options => options.EnableModules(loader));

        var import = engine.Modules.StartImport("./built.js");

        var completion = loader.Take("./built.js");
        completion.Engine.Should().BeSameAs(engine);
        completion.Resolved.ModuleRequest.Specifier.Should().Be("./built.js");
        completion.SetModule(ModuleFactory.BuildSourceTextModule(engine, Engine.PrepareModule("export const value = 'built';", "./built.js")));

        engine.Advanced.ProcessTasks();

        import.GetResult().Get("value").AsString().Should().Be("built");
    }

    [Fact]
    public void StartImportIsDrivenByTheHostsOwnPump()
    {
        // The game-loop shape: start the import, then hand the engine a turn per frame and watch the operation.
        // Nothing here blocks, and no engine work happens on any thread but this one.
        var loader = new DelayedModuleLoader(new Dictionary<string, string>
        {
            ["./frame.js"] = "import { dep } from './dep.js'; export const value = dep;",
            ["./dep.js"] = "export const dep = 'ready';",
        }, TimeSpan.FromMilliseconds(20));

        var engine = new Engine(options => options.EnableModules(loader));

        var import = engine.Modules.StartImport("./frame.js");
        import.IsCompleted.Should().BeFalse();

        var frames = 0;
        while (!import.IsCompleted && frames < 2000)
        {
            frames++;
            engine.Advanced.ProcessTasks();
            Thread.Sleep(1);
        }

        import.IsCompleted.Should().BeTrue();
        import.IsFaulted.Should().BeFalse();
        import.GetResult().Get("value").AsString().Should().Be("ready");
        frames.Should().BeGreaterThan(1, "the load should not have completed on the turn that started it");
    }

    [Fact]
    public void AFailedPumpedImportReportsTheErrorRatherThanThrowingOutOfThePump()
    {
        var loader = new DeferredModuleLoader();
        var engine = new Engine(options => options.EnableModules(loader));

        var import = engine.Modules.StartImport("./nope.js");
        loader.Fail("./nope.js", new InvalidOperationException("asset bundle missing"));

        engine.Advanced.ProcessTasks();

        import.IsCompleted.Should().BeTrue();
        import.IsFaulted.Should().BeTrue();
        import.Error!.Get("message").AsString().Should().Be("asset bundle missing");
        Invoking(() => import.GetResult()).Should().Throw<PromiseRejectedException>();
    }

    [Fact]
    public void AskingAPumpedImportForItsResultTooEarlyIsAnActionableError()
    {
        var loader = new DeferredModuleLoader();
        var engine = new Engine(options => options.EnableModules(loader));

        var import = engine.Modules.StartImport("./slow.js");

        Invoking(() => import.GetResult()).Should().Throw<InvalidOperationException>()
            .WithMessage("*ProcessTasks*ImportAsync*");
    }

    [Fact]
    public async Task AsynchronouslyLoadedModulesSupportTopLevelAwait()
    {
        var loader = new DelayedModuleLoader(new Dictionary<string, string>
        {
            ["./tla.js"] = "const v = await Promise.resolve(5); export const value = v * 2;",
        }, TimeSpan.FromMilliseconds(10));

        var engine = new Engine(options => options.EnableModules(loader));

        var ns = await engine.Modules.ImportAsync("./tla.js");

        ns.Get("value").AsNumber().Should().Be(10);
    }

    [Fact]
    public async Task AModuleRegisteredWithAddIsNeverHandedToTheLoader()
    {
        var loader = new DelayedModuleLoader(new Dictionary<string, string>
        {
            ["./main.js"] = "import { answer } from 'lib'; export const value = answer;",
        }, TimeSpan.FromMilliseconds(5));

        var engine = new Engine(options => options.EnableModules(loader));
        engine.Modules.Add("lib", "export const answer = 42;");

        var ns = await engine.Modules.ImportAsync("./main.js");

        ns.Get("value").AsNumber().Should().Be(42);
        loader.Loads.Should().Be(1, "'lib' is registered with the engine, so only './main.js' is fetched");
    }

    [Fact]
    public void ASyntaxErrorInARegisteredModuleReachedThroughAnAsyncGraphRejectsInsteadOfStrandingTheLoad()
    {
        // The builder branch of LoadImportedModule can run from inside a load-completion job - an
        // asynchronously fetched module statically importing a module registered with Add - where no caller is
        // left to catch a parse error. The error must become the load's failure: escaping instead would erupt
        // out of ProcessTasks and leave the import pending forever.
        var loader = new DeferredModuleLoader();
        var engine = new Engine(options => options.EnableModules(loader));
        engine.Modules.Add("lib", "export const = broken;");

        var import = engine.Modules.StartImport("./root.js");
        loader.Deliver("./root.js", "import './mid.js';");
        engine.Advanced.ProcessTasks();
        loader.Deliver("./mid.js", "import 'lib';");

        Invoking(() => engine.Advanced.ProcessTasks()).Should().NotThrow();

        import.IsCompleted.Should().BeTrue("the parse failure must settle the import rather than strand it");
        import.IsFaulted.Should().BeTrue();
        import.Error!.Get("name").AsString().Should().Be("SyntaxError");
    }

    [Fact]
    public void ASyntaxErrorInAsynchronouslyDeliveredSourceRejectsTheImport()
    {
        var loader = new DeferredModuleLoader();
        var engine = new Engine(options => options.EnableModules(loader));

        var import = engine.Modules.StartImport("./broken.js");
        loader.Deliver("./broken.js", "export const = ;");

        Invoking(() => engine.Advanced.ProcessTasks()).Should().NotThrow();

        import.IsCompleted.Should().BeTrue();
        import.IsFaulted.Should().BeTrue();
        import.Error!.Get("name").AsString().Should().Be("SyntaxError");
    }

    [Fact]
    public void ALoaderThrowingInsteadOfSettlingRejectsTheImport()
    {
        // IAsyncModuleLoader.LoadModuleAsync is supposed to settle the completion, but a loader that throws
        // on the way out - a broken transport constructor, say - must produce the same rejection SetError
        // would have, not an exception on whatever thread was evaluating.
        var loader = new ThrowingLoader();
        var engine = new Engine(options => options.EnableModules(loader));

        var import = engine.Modules.StartImport("./unreachable.js");
        engine.Advanced.ProcessTasks();

        import.IsCompleted.Should().BeTrue();
        import.IsFaulted.Should().BeTrue();
        import.Error!.Get("message").AsString().Should().Be("transport exploded");
    }

    private sealed class ThrowingLoader : IAsyncModuleLoader
    {
        public ResolvedSpecifier Resolve(string? referencingModuleLocation, ModuleRequest moduleRequest)
            => new(moduleRequest, moduleRequest.Specifier, Uri: null, SpecifierType.Bare);

        public Module LoadModule(Engine engine, ResolvedSpecifier resolved)
            => throw new InvalidOperationException("The engine must not take the synchronous path for an IAsyncModuleLoader.");

        public void LoadModuleAsync(Engine engine, ResolvedSpecifier resolved, ModuleLoadCompletion completion)
            => throw new InvalidOperationException("transport exploded");
    }

    [Fact]
    public void TheEnginesCancellationTokenReachesTheLoadersFetch()
    {
        // A host that registered options.CancellationToken(token) means it for the loader's I/O too:
        // AsyncModuleLoader hands LoadModuleContentsAsync the same token the interpreter's cancellation
        // constraint observes, so one token stops both the script and the fetches it started.
        using var cts = new CancellationTokenSource();
        var loader = new TokenCapturingLoader();
        var engine = new Engine(options => options.EnableModules(loader).CancellationToken(cts.Token));

        engine.Modules.StartImport("./fetch.js");

        loader.CapturedToken.Should().Be(cts.Token);
    }

    [Fact]
    public void ACanceledFetchRejectsTheImportAsCanceled()
    {
        // Task.IsCanceled is not Task.IsFaulted - there is no exception object to take a message from - so
        // the base class has to say what happened itself.
        var loader = new TokenCapturingLoader();
        var engine = new Engine(options => options.EnableModules(loader));

        var import = engine.Modules.StartImport("./doomed.js");
        loader.CancelPendingFetch();

        // The cancellation reaches the completion through task continuations on another thread, so the
        // settle is not necessarily enqueued by the time Cancel returns - pump until it lands.
        var frames = 0;
        while (!import.IsCompleted && frames < 2000)
        {
            frames++;
            engine.Advanced.ProcessTasks();
            Thread.Sleep(1);
        }

        import.IsCompleted.Should().BeTrue();
        import.IsFaulted.Should().BeTrue();
        import.Error!.Get("message").AsString().Should().Be("Loading module './doomed.js' was canceled.");
    }

    /// <summary>
    /// Records the cancellation token the engine hands the fetch, and never finishes on its own: the fetch
    /// task only settles when the test cancels it.
    /// </summary>
    private sealed class TokenCapturingLoader : AsyncModuleLoader
    {
        private readonly CancellationTokenSource _fetch = new();

        public CancellationToken? CapturedToken { get; private set; }

        public void CancelPendingFetch() => _fetch.Cancel();

        public override ResolvedSpecifier Resolve(string? referencingModuleLocation, ModuleRequest moduleRequest)
            => new(moduleRequest, moduleRequest.Specifier, Uri: null, SpecifierType.Bare);

        protected override async Task<string> LoadModuleContentsAsync(Engine engine, ResolvedSpecifier resolved, CancellationToken cancellationToken)
        {
            CapturedToken = cancellationToken;
            await Task.Delay(System.Threading.Timeout.InfiniteTimeSpan, _fetch.Token).ConfigureAwait(false);
            return string.Empty;
        }
    }

    [Fact]
    public async Task ImportAsyncHonoursItsCancellationToken()
    {
        // The loader never answers; the await must still be escapable. This is the caller abandoning the
        // wait, not the engine failing the load - the load stays pending, there is just nobody waiting.
        using var cts = new CancellationTokenSource();
        var loader = new DeferredModuleLoader();
        var engine = new Engine(options => options.EnableModules(loader));

        var importTask = engine.Modules.ImportAsync("./never.js", cts.Token);
        cts.Cancel();

        await Invoking(() => importTask).Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public void AJsonModuleImportedFromAnAsynchronouslyLoadedModuleIsBuiltAsJson()
    {
        // Import attributes travel with the request through the asynchronous path, so the source the host
        // delivers is built into the module kind the attribute asks for - the same dispatch the synchronous
        // loader performs.
        var loader = new DeferredModuleLoader();
        var engine = new Engine(options => options.EnableModules(loader));

        var import = engine.Modules.StartImport("./main.js");
        loader.Deliver("./main.js", "import data from './config.json' with { type: 'json' }; export const message = data.message;");
        engine.Advanced.ProcessTasks();
        loader.Deliver("./config.json", """{ "message": "hello" }""");
        engine.Advanced.ProcessTasks();

        import.IsCompleted.Should().BeTrue();
        import.GetResult().Get("message").AsString().Should().Be("hello");
    }

    [Fact]
    public void BytesDeliveredForABytesImportStayBytes()
    {
        // SetSource(byte[]) hands raw content over; with { type: 'bytes' } must receive it untouched rather
        // than round-tripped through a string decode.
        var loader = new DeferredModuleLoader();
        var engine = new Engine(options => options.EnableModules(loader));

        engine.Execute("globalThis.result = null; import('./blob.bin', { with: { type: 'bytes' } }).then(ns => { result = ns.default.length + ':' + ns.default[0] + ',' + ns.default[1]; });");

        loader.Take("./blob.bin").SetSource(new byte[] { 200, 7 });
        engine.Advanced.ProcessTasks();

        engine.Evaluate("result").AsString().Should().Be("2:200,7");
    }

    [Fact]
    public void TheSynchronousImportIsWokenByASettleFromABackgroundThread()
    {
        // The blocking Import drains the event loop while the load is in flight; a settle arriving from
        // another thread - the shape every real fetch has - only enqueues, and the drain on this thread has
        // to notice it. Every other sync-Import test settles inline on the engine thread, which never
        // exercises that wait.
        var engine = new Engine(options => options.EnableModules(new BackgroundThreadLoader()));

        var ns = engine.Modules.Import("./bg.js");

        ns.Get("value").AsString().Should().Be("from-background");
    }

    private sealed class BackgroundThreadLoader : IAsyncModuleLoader
    {
        public ResolvedSpecifier Resolve(string? referencingModuleLocation, ModuleRequest moduleRequest)
            => new(moduleRequest, moduleRequest.Specifier, Uri: null, SpecifierType.Bare);

        public Module LoadModule(Engine engine, ResolvedSpecifier resolved)
            => throw new InvalidOperationException("The engine must not take the synchronous path for an IAsyncModuleLoader.");

        public void LoadModuleAsync(Engine engine, ResolvedSpecifier resolved, ModuleLoadCompletion completion)
        {
            var thread = new Thread(() =>
            {
                Thread.Sleep(30);
                completion.SetSource("export const value = 'from-background';");
            });
            thread.IsBackground = true;
            thread.Start();
        }
    }

    [Fact]
    public void AWarmAnswerServesTheBlockingImportSynchronously()
    {
        // A loader whose answer is already at hand settles the completion before LoadModuleAsync returns,
        // and the engine finishes the load on that very stack - the blocking Import over such answers never
        // touches the event loop, exactly as if the loader were a synchronous IModuleLoader.
        var loader = new WarmModuleLoader(new Dictionary<string, string>
        {
            ["./root.js"] = "import { dep } from './dep.js'; export const value = 'root+' + dep;",
            ["./dep.js"] = "export const dep = 'dep';",
        });

        var engine = new Engine(options => options.EnableModules(loader));

        engine.Modules.Import("./root.js").Get("value").AsString().Should().Be("root+dep");
    }

    [Fact]
    public void AWarmAnswerServesTheBlockingImportEvenWhereDrainingIsImpossible()
    {
        // The strongest form of "continues synchronously": inside an event-loop job the re-entrancy guard
        // makes draining impossible, so an import needing even one event-loop turn sits out the whole
        // PromiseTimeout and fails. An inline-settling loader completes the graph on the calling stack and
        // never notices.
        var loader = new WarmModuleLoader(new Dictionary<string, string>
        {
            ["./root.js"] = "import { dep } from './dep.js'; export const value = 'root+' + dep;",
            ["./dep.js"] = "export const dep = 'dep';",
        });

        var engine = new Engine(options =>
        {
            options.EnableModules(loader);
            // Small so a regression fails the test in half a second rather than the default ten.
            options.Constraints.PromiseTimeout = TimeSpan.FromMilliseconds(500);
        });

        engine.SetValue("hostImport", new Func<string>(() => engine.Modules.Import("./root.js").Get("value").AsString()));
        engine.Execute("globalThis.result = null; Promise.resolve().then(() => { result = hostImport(); });");
        engine.Advanced.ProcessTasks();

        engine.Evaluate("result").AsString().Should().Be("root+dep");
    }

    [Fact]
    public void AnInlineFailureStillReachesTheBlockingImportAsAnError()
    {
        var loader = new InlineFailingLoader();
        var engine = new Engine(options => options.EnableModules(loader));

        Invoking(() => engine.Modules.Import("./missing.js"))
            .Should().Throw<JavaScriptException>().WithMessage("*not in the bundle*");
    }

    [Fact]
    public void AGraphMixingWarmAndTrulyAsynchronousAnswersStillLoadsThroughTheBlockingImport()
    {
        // The root settles inline on the import's own stack; its dependency arrives from the thread pool
        // later. The blocking Import has to switch from the synchronous continuation to draining the event
        // loop mid-graph.
        var loader = new WarmModuleLoader(new Dictionary<string, string>
        {
            ["./root.js"] = "import { dep } from './slow.js'; export const value = 'root+' + dep;",
            ["./slow.js"] = "export const dep = 'slow';",
        }, coldSpecifiers: ["./slow.js"]);

        var engine = new Engine(options => options.EnableModules(loader));

        engine.Modules.Import("./root.js").Get("value").AsString().Should().Be("root+slow");
    }

    /// <summary>
    /// Answers from an in-memory dictionary with an already-completed task — the cache-hit shape — except
    /// for <paramref name="coldSpecifiers"/>, which take the thread-pool round trip a real fetch takes.
    /// </summary>
    private sealed class WarmModuleLoader : AsyncModuleLoader
    {
        private readonly IReadOnlyDictionary<string, string> _sources;
        private readonly HashSet<string> _coldSpecifiers;

        public WarmModuleLoader(IReadOnlyDictionary<string, string> sources, IEnumerable<string>? coldSpecifiers = null)
        {
            _sources = sources;
            _coldSpecifiers = new HashSet<string>(coldSpecifiers ?? [], StringComparer.Ordinal);
        }

        public override ResolvedSpecifier Resolve(string? referencingModuleLocation, ModuleRequest moduleRequest)
            => new(moduleRequest, moduleRequest.Specifier, Uri: null, SpecifierType.Bare);

        protected override async Task<string> LoadModuleContentsAsync(Engine engine, ResolvedSpecifier resolved, CancellationToken cancellationToken)
        {
            var specifier = resolved.ModuleRequest.Specifier;
            if (_coldSpecifiers.Contains(specifier))
            {
                await Task.Delay(20, cancellationToken).ConfigureAwait(false);
            }

            return _sources.TryGetValue(specifier, out var code)
                ? code
                : throw new FileNotFoundException($"404 {specifier}");
        }
    }

    private sealed class InlineFailingLoader : IAsyncModuleLoader
    {
        public ResolvedSpecifier Resolve(string? referencingModuleLocation, ModuleRequest moduleRequest)
            => new(moduleRequest, moduleRequest.Specifier, Uri: null, SpecifierType.Bare);

        public Module LoadModule(Engine engine, ResolvedSpecifier resolved)
            => throw new InvalidOperationException("The engine must not take the synchronous path for an IAsyncModuleLoader.");

        public void LoadModuleAsync(Engine engine, ResolvedSpecifier resolved, ModuleLoadCompletion completion)
            => completion.SetError($"'{resolved.ModuleRequest.Specifier}' is not in the bundle.");
    }

    [Fact]
    public async Task ResolutionStillSeesTheReferringModulesLocation()
    {
        // The shape a dev server needs: specifiers inside a fetched module are relative to where that module
        // came from, so Resolve has to be given the referrer's location on the asynchronous path too.
        var loader = new UriModuleLoader(new Dictionary<string, string>
        {
            ["http://localhost:5173/app/main.js"] = "import { v } from './lib/util.js'; export const value = v;",
            ["http://localhost:5173/app/lib/util.js"] = "import { base } from '../base.js'; export const v = base + '/util';",
            ["http://localhost:5173/app/base.js"] = "export const base = 'base';",
        });

        var engine = new Engine(options => options.EnableModules(loader));

        var ns = await engine.Modules.ImportAsync("http://localhost:5173/app/main.js");

        ns.Get("value").AsString().Should().Be("base/util");
    }

    [Fact]
    public async Task ADynamicImportInsideAnAsynchronouslyLoadedModuleResolvesRelativeToIt()
    {
        var loader = new UriModuleLoader(new Dictionary<string, string>
        {
            ["http://localhost:5173/app/main.js"] = "export const load = () => import('./sibling.js');",
            ["http://localhost:5173/app/sibling.js"] = "export const who = 'sibling';",
        });

        var engine = new Engine(options => options.EnableModules(loader));

        var ns = await engine.Modules.ImportAsync("http://localhost:5173/app/main.js");

        // Call the exported function and await the promise its dynamic import() settles into. The referrer of
        // that import is the module the function came from, so './sibling.js' resolves against its location.
        engine.SetValue("load", ns.Get("load"));
        var result = await engine.EvaluateAsync("load().then(m => m.who)");

        result.AsString().Should().Be("sibling");
    }

    /// <summary>
    /// Resolves specifiers as URIs against the referring module's location, the way a dev server or CDN loader
    /// does, and fetches with latency.
    /// </summary>
    /// <remarks>
    /// Note the rebase against <see cref="_baseUri"/>: <see cref="ModuleFactory"/> takes a module's
    /// <see cref="Module.Location"/> from <see cref="Uri.LocalPath"/>, so the referrer location a nested
    /// specifier is resolved against arrives as a path rather than an absolute URI.
    /// <see cref="DefaultModuleLoader"/> does the same thing for the same reason.
    /// </remarks>
    private sealed class UriModuleLoader : AsyncModuleLoader
    {
        private readonly IReadOnlyDictionary<string, string> _sources;
        private readonly Uri _baseUri = new("http://localhost:5173/");

        public UriModuleLoader(IReadOnlyDictionary<string, string> sources)
        {
            _sources = sources;
        }

        public override ResolvedSpecifier Resolve(string? referencingModuleLocation, ModuleRequest moduleRequest)
        {
            var baseUri = _baseUri;
            if (referencingModuleLocation is not null
                && (Uri.TryCreate(referencingModuleLocation, UriKind.Absolute, out var referrerUri)
                    || Uri.TryCreate(_baseUri, referencingModuleLocation, out referrerUri)))
            {
                baseUri = referrerUri;
            }

            var uri = new Uri(baseUri, moduleRequest.Specifier);
            return new ResolvedSpecifier(moduleRequest, uri.AbsoluteUri, uri, SpecifierType.RelativeOrAbsolute);
        }

        protected override async Task<string> LoadModuleContentsAsync(Engine engine, ResolvedSpecifier resolved, CancellationToken cancellationToken)
        {
            await Task.Delay(5, cancellationToken).ConfigureAwait(false);

            if (!_sources.TryGetValue(resolved.Key, out var code))
            {
                throw new FileNotFoundException($"404 {resolved.Key}");
            }

            return code;
        }
    }

    [Fact]
    public void TheSynchronousLoaderInterfaceIsUnaffected()
    {
        // The whole point of making async opt-in: an IModuleLoader that only loads synchronously behaves
        // exactly as it did, including answering the blocking Modules.Import.
        var engine = new Engine(options => options.EnableModules(new SynchronousLoader()));

        engine.Modules.Import("./main.js").Get("value").AsNumber().Should().Be(3);
    }

    private sealed class SynchronousLoader : ModuleLoader
    {
        public override ResolvedSpecifier Resolve(string? referencingModuleLocation, ModuleRequest moduleRequest)
            => new(moduleRequest, moduleRequest.Specifier, Uri: null, SpecifierType.Bare);

        protected override string LoadModuleContents(Engine engine, ResolvedSpecifier resolved) => resolved.ModuleRequest.Specifier switch
        {
            "./main.js" => "import { one } from './dep.js'; export const value = one + 2;",
            "./dep.js" => "export const one = 1;",
            _ => throw new FileNotFoundException(resolved.ModuleRequest.Specifier),
        };
    }

    [Fact]
    public void LinkingBeforeTheGraphHasLoadedIsRefusedWithAnActionableError()
    {
        // A host driving a module by hand has to run the load phase first. With a synchronous loader Link()
        // still does it implicitly; with an asynchronous one it cannot, and says so.
        var loader = new DeferredModuleLoader();
        var engine = new Engine(options => options.EnableModules(loader));

        var module = ModuleFactory.BuildSourceTextModule(engine, Engine.PrepareModule("import './dep.js';", "./hand-driven.js"));

        Invoking(module.Link).Should().Throw<InvalidOperationException>()
            .WithMessage("*still loading*LoadRequestedModules*ImportAsync*");
    }

    [Fact]
    public void AHostCanDriveTheLoadPhaseItselfBeforeLinking()
    {
        // LoadRequestedModules is the specification's load phase as a primitive: run it to completion, and the
        // ordinary synchronous Link/Evaluate pipeline works on an asynchronously fetched graph.
        var loader = new DeferredModuleLoader();
        var engine = new Engine(options => options.EnableModules(loader));

        var module = ModuleFactory.BuildSourceTextModule(engine, Engine.PrepareModule("import { v } from './dep.js'; export const value = v;", "./root.js"));

        module.LoadRequestedModules();
        loader.Deliver("./dep.js", "export const v = 11;");
        engine.Advanced.ProcessTasks();

        module.Link();
        module.Evaluate();
        engine.Advanced.ProcessTasks();

        Module.GetModuleNamespace(module).Get("value").AsNumber().Should().Be(11);
    }

    /// <summary>
    /// Stands in for the kind of exception a real fetch fails with, without adding an
    /// <c>System.Net.Http</c> reference to this project.
    /// </summary>
    private sealed class HttpRequestExceptionStandIn : Exception
    {
        public HttpRequestExceptionStandIn(string message) : base(message)
        {
        }
    }
}
