using System.Collections.Concurrent;
using Jint.Native;
using Jint.Runtime;
using Jint.Runtime.Modules;


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
    /// Builds the engine every outcome-asserting test here uses: modules over <paramref name="loader"/>, with
    /// a promise budget generous enough that only a genuine hang can reach it. A loaded CI runner can starve
    /// the thread pool long enough that even a 5 ms loader hop misses the engine's default 10-second
    /// <c>PromiseTimeout</c> — seen as one-leg CI failures reading "Timeout of 00:00:10 reached". These tests
    /// assert outcomes, never durations, so the budget is not part of what they prove; the tests that DO
    /// assert timeout semantics configure their own short budget explicitly and never come through here.
    /// </summary>
    private static Engine CreateEngine(IModuleLoader loader) => new(options =>
    {
        options.UseModules(loader);
        options.Constraints.PromiseTimeout = TestBudgets.WedgeCeiling;
    });
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

        /// <summary>
        /// Specifiers <see cref="Resolve"/> refuses, the way <see cref="DefaultModuleLoader"/> refuses a path
        /// outside its root — with a <see cref="ModuleResolutionException"/>, not a JavaScriptException.
        /// </summary>
        public HashSet<string> RefuseToResolve { get; } = new(StringComparer.Ordinal);

        public ResolvedSpecifier Resolve(string? referencingModuleLocation, ModuleRequest moduleRequest)
        {
            if (RefuseToResolve.Contains(moduleRequest.Specifier))
            {
                throw new ModuleResolutionException("Specifier is not allowed", moduleRequest.Specifier, referencingModuleLocation, filePath: null);
            }

            return new(moduleRequest, moduleRequest.Specifier, Uri: null, SpecifierType.Bare);
        }

        public ModuleRecord LoadModule(Engine engine, ResolvedSpecifier resolved)
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

    private sealed class ResultFailureLoader(Exception failure, bool settle) : IAsyncModuleLoader
    {
        public ResolvedSpecifier Resolve(string? referencingModuleLocation, ModuleRequest moduleRequest)
            => new(moduleRequest, moduleRequest.Specifier, Uri: null, SpecifierType.Bare);

        public ModuleRecord LoadModule(Engine engine, ResolvedSpecifier resolved)
            => throw new InvalidOperationException("The asynchronous path is required.");

        public void LoadModuleAsync(Engine engine, ResolvedSpecifier resolved, ModuleLoadCompletion completion)
        {
            if (settle)
            {
                completion.SetError(failure);
                return;
            }

            throw failure;
        }
    }

    /// <summary>
    /// Fails a load with a task that is already faulted when it is handed back: the warm-cache shape of an
    /// <see cref="AsyncModuleLoader"/> answer, applied to an answer that is a failure. Because the task is
    /// already completed, <see cref="AsyncModuleLoader"/> settles it there and then rather than through a
    /// continuation, so the settle lands inside the inline window and on the engine's own thread.
    /// </summary>
    /// <remarks>
    /// Deliberately not an <c>async</c> method: <see cref="Task.FromException{TResult}"/> is what makes
    /// "already completed" a property of the code rather than of how quickly a continuation happens to run.
    /// </remarks>
    private sealed class WarmFaultedLoader(Exception failure) : AsyncModuleLoader
    {
        public override ResolvedSpecifier Resolve(string? referencingModuleLocation, ModuleRequest moduleRequest)
            => new(moduleRequest, moduleRequest.Specifier, Uri: null, SpecifierType.Bare);

        protected override Task<string> LoadModuleContentsAsync(
            Engine engine,
            ResolvedSpecifier resolved,
            CancellationToken cancellationToken)
            => Task.FromException<string>(failure);
    }

    [Fact]
    public async Task CanImportAModuleWhoseSourceArrivesAsynchronously()
    {
        var loader = new DelayedModuleLoader(new Dictionary<string, string>
        {
            ["./main.js"] = "import { value } from './dep.js'; export const result = value * 2;",
            ["./dep.js"] = "export const value = 21;",
        }, TimeSpan.FromMilliseconds(20));

        var engine = CreateEngine(loader);

        var ns = await engine.Modules.ImportAsync("./main.js");

        ns.Get("result").AsNumber().Should().Be(42);
        loader.Loads.Should().Be(2);
    }

    [Fact]
    public void DirectResultLimitFromLoaderRemainsFatal()
    {
        var failure = CreateResultLimitFailure();
        var engine = new Engine(options => options.UseModules(new ResultFailureLoader(failure, settle: false)));

        Invoking(() => engine.Modules.Import("module"))
            .Should().ThrowExactly<ResultLimitExceededException>();
        engine.Evaluate("1 + 1").AsNumber().Should().Be(2);
    }

    [Fact]
    public void SettledResultLimitFromLoaderRemainsFatal()
    {
        var failure = CreateResultLimitFailure();
        var engine = new Engine(options => options.UseModules(new ResultFailureLoader(failure, settle: true)));

        Invoking(() => engine.Modules.Import("module"))
            .Should().ThrowExactly<ResultLimitExceededException>();
        engine.Evaluate("1 + 1").AsNumber().Should().Be(2);
    }

    // The two tests below are a deliberate pair: they pin the two sides of ModuleLoadCompletion's inline
    // settle window, and neither is redundant.
    //
    // A settle that happens while the engine is still inside IAsyncModuleLoader.LoadModuleAsync, on the very
    // thread the engine called it from, finishes the load on the importing stack; every other settle is
    // queued and only runs when the host gives the engine a turn. See AGENTS.md, "Asynchronous module
    // loading", and ModuleLoadCompletion.Settle. For a failure the engine must propagate rather than flatten
    // into a rejection - a limit that became a rejection would no longer bound anything - that difference
    // decides *where* the exception erupts: out of the call that started the import, or out of the pump, with
    // the importing promise left unsettled. A host cannot predict which side a given load lands on, so both
    // are behaviour, and both are pinned here.
    //
    // Their predecessor, FaultedResultLimitFromLoaderRemainsFatal, asserted the exception type alone and let
    // an `await Task.Yield(); throw;` loader pick the window for it. That is a coin flip - instrumenting the
    // engine showed it taking the inline branch on about one run in forty on net472 - and on a loaded runner
    // it fails in a third way entirely, because ImportAsync's PromiseTimeout answers first with a
    // PromiseRejectedException (issue #3218). Each test here therefore selects its branch structurally and
    // then asserts the branch, not only the outcome: only an inline settle can throw out of StartImport,
    // which takes no event-loop turn, and only a queued one can let StartImport return normally and erupt
    // from ProcessTasks instead. Nothing in either test waits on wall-clock time or on the thread pool.
    //
    // If a change ever makes the two paths agree, both tests must be updated to say so. Deleting one as a
    // duplicate throws away the asymmetry the pair exists to record.

    [Fact]
    public void AResultLimitSettledInsideTheLoadCallStaysFatalOnTheImportingStack()
    {
        var failure = CreateResultLimitFailure();
        var engine = CreateEngine(new WarmFaultedLoader(failure));

        // StartImport never pumps the event loop, so an exception coming out of it can only have been thrown
        // on this stack - which is the inline branch itself, not merely its outcome.
        Invoking(() => engine.Modules.StartImport("module"))
            .Should().ThrowExactly<ResultLimitExceededException>();

        Invoking(() => engine.Tasks.ProcessTasks())
            .Should().NotThrow("the load was finished inline and left nothing queued behind it");
        engine.Evaluate("1 + 1").AsNumber().Should().Be(2);
    }

    [Fact]
    public void AResultLimitSettledAfterTheLoadCallReturnedStaysFatalOnThePumpInstead()
    {
        var failure = CreateResultLimitFailure();
        var loader = new DeferredModuleLoader();
        var engine = CreateEngine(loader);

        // The loader kept the completion and settled nothing, so LoadModuleAsync has returned and the window
        // is shut by the time the test settles it. Everything happens on this one thread, in this order:
        // what selects the branch is the window, not which thread or which moment won a race.
        var import = engine.Modules.StartImport("module");
        loader.Pending.Should().ContainSingle();

        Invoking(() => loader.Fail("module", failure))
            .Should().NotThrow("a settle after the load call returned is queued, never run on the settling stack");
        import.IsCompleted.Should().BeFalse("nothing has run the queued completion yet");

        Invoking(() => engine.Tasks.ProcessTasks())
            .Should().ThrowExactly<ResultLimitExceededException>("a limit that became a rejection would no longer bound anything");

        // Propagating drops the waiters, so on this side of the window the erupting exception is the whole
        // outcome: the import stays pending, and a host that only polled IsCompleted would poll forever.
        import.IsCompleted.Should().BeFalse();
        engine.Evaluate("1 + 1").AsNumber().Should().Be(2);
    }

    private static ResultLimitExceededException CreateResultLimitFailure()
    {
        using var engine = new Engine();
        return Invoking(() => engine.ConvertResult(
                engine.Evaluate("[1, 2]"),
                new ResultLimits(maxPropertyCount: 1)))
            .Should().ThrowExactly<ResultLimitExceededException>().Which;
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

        var engine = CreateEngine(loader);

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

        var engine = CreateEngine(loader);

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

        var engine = CreateEngine(loader);

        var ex = await Invoking(() => engine.Modules.ImportAsync("./main.js")).Should().ThrowAsync<PromiseRejectedException>();
        ex.Which.RejectedValue.Get("message").AsString().Should().Be("Could not load module.");
        JintException.TryGetClrException(ex.Which, out var clrException).Should().BeTrue();
        clrException.Should().BeOfType<FileNotFoundException>().Which.Message.Should().Contain("404 ./missing.js");
    }

    [Fact]
    public void ADynamicImportKeepsTheEngineRunningWhileTheLoadIsInFlight()
    {
        // The point of the whole exercise: evaluation returns, the script goes on, and nothing is blocked
        // waiting for the module. The promise settles on a later turn of the event loop.
        var loader = new DeferredModuleLoader();
        var engine = CreateEngine(loader);

        engine.Execute("globalThis.log = []; import('./late.js').then(ns => log.push('loaded:' + ns.value), e => log.push('failed:' + e.message)); log.push('after-import');");

        engine.Evaluate("log.join(',')").AsString().Should().Be("after-import");
        loader.Pending.Should().ContainSingle();

        loader.Deliver("./late.js", "export const value = 7;");
        engine.Tasks.ProcessTasks();

        engine.Evaluate("log.join(',')").AsString().Should().Be("after-import,loaded:7");
    }

    [Fact]
    public void ADynamicImportRejectsWhenTheHostFailsTheLoad()
    {
        var loader = new DeferredModuleLoader();
        var engine = CreateEngine(loader);

        engine.Execute("globalThis.outcome = 'pending'; import('./gone.js').then(() => { outcome = 'resolved'; }, e => { outcome = e.message; });");

        engine.Evaluate("outcome").AsString().Should().Be("pending");

        loader.Fail("./gone.js", new HttpRequestExceptionStandIn("connection refused"));
        engine.Tasks.ProcessTasks();

        engine.Evaluate("outcome").AsString().Should().Be("Could not load module.");
    }

    [Fact]
    public Task AHostCanFinishALoadFromAnotherThread() => DedicatedThread.RunAsync(() =>
    {
        var loader = new DeferredModuleLoader();
        var engine = CreateEngine(loader);

        engine.Execute("globalThis.value = 0; import('./worker.js').then(ns => { value = ns.value; });");

        var completion = loader.Take("./worker.js");
        var thread = new Thread(() => completion.SetSource("export const value = 99;"));
        thread.Start();
        thread.Join();

        // Nothing of the engine ran on that thread: the completion was queued, and only a turn taken here
        // applies it.
        engine.Evaluate("value").AsNumber().Should().Be(0);

        engine.Tasks.ProcessTasks();
        engine.Evaluate("value").AsNumber().Should().Be(99);
    });

    [Fact]
    public void TheLoaderIsAskedOncePerReferrerAndSpecifier()
    {
        // https://tc39.es/ecma262/#sec-HostLoadImportedModule: "each time this operation is called with a
        // specific referrer, specifier, ... it must perform FinishLoadingImportedModule with the same
        // module". Jint keeps that promise by answering a recorded pair from [[LoadedModules]] instead of
        // asking again. The record is written when the load finishes, so a pair can still be dispatched
        // twice while its first load is in flight - see the two-phase case below, which the spec both
        // produces and permits.
        var loader = new DeferredModuleLoader();
        var engine = CreateEngine(loader);

        engine.Execute("globalThis.results = []; import('./twice.js').then(ns => results.push(ns.value));");
        loader.Deliver("./twice.js", "export const value = 'first';");
        engine.Tasks.ProcessTasks();

        engine.Execute("import('./twice.js').then(ns => results.push(ns.value));");
        engine.Tasks.ProcessTasks();

        engine.Evaluate("results.join(',')").AsString().Should().Be("first,first");
        loader.AskedFor("./twice.js").Should().Be(1);
    }

    [Fact]
    public void OneSpecifierImportedAtTwoPhasesIsStillOneRecordAndOneFetch()
    {
        // https://tc39.es/ecma262/#sec-InnerModuleLoading dispatches HostLoadImportedModule for every entry
        // of [[RequestedModules]] before any of them has been recorded, and `import defer x` and `import x`
        // of one specifier are two entries. Against an asynchronous loader both are therefore dispatched,
        // and the second is resolved a second time - which HostLoadImportedModule permits, requiring only
        // that the answer be the same. What must hold is the outcome: the two denote one module record, so
        // there is one fetch and one evaluation, not two.
        var loader = new DeferredModuleLoader();
        var engine = CreateEngine(loader);

        var import = engine.Modules.StartImport("./main.js");
        loader.Deliver("./main.js", "import defer * as d from './dep.js'; import { v } from './dep.js'; export const a = v;");
        engine.Tasks.ProcessTasks();

        loader.Deliver("./dep.js", "globalThis.depEvaluations = (globalThis.depEvaluations ?? 0) + 1; export const v = 3;");
        engine.Tasks.ProcessTasks();

        import.GetResult().Get("a").AsNumber().Should().Be(3);
        loader.AskedFor("./dep.js").Should().Be(1, "the two phases denote one module record, so the second request coalesces onto the first load");
        engine.Evaluate("globalThis.depEvaluations").AsNumber().Should().Be(1);
    }

    [Fact]
    public void SettlingALoadTwiceIsIgnored()
    {
        var loader = new DeferredModuleLoader();
        var engine = CreateEngine(loader);

        engine.Execute("globalThis.value = 0; import('./once.js').then(ns => { value = ns.value; });");

        var completion = loader.Take("./once.js");
        completion.IsCompleted.Should().BeFalse();
        completion.SetSource("export const value = 1;");
        completion.IsCompleted.Should().BeTrue();
        completion.SetSource("export const value = 2;");
        completion.SetError("too late");

        engine.Tasks.ProcessTasks();
        engine.Evaluate("value").AsNumber().Should().Be(1);
    }

    [Fact]
    public void AnAsyncLoaderCanAnswerWithAModuleItBuiltItself()
    {
        var loader = new DeferredModuleLoader();
        var engine = CreateEngine(loader);

        var import = engine.Modules.StartImport("./built.js");

        var completion = loader.Take("./built.js");
        completion.Engine.Should().BeSameAs(engine);
        completion.Resolved.ModuleRequest.Specifier.Should().Be("./built.js");
        completion.SetModule(ModuleFactory.BuildSourceTextModule(engine, Engine.PrepareModule("export const value = 'built';", "./built.js")));

        engine.Tasks.ProcessTasks();

        import.GetResult().Get("value").AsString().Should().Be("built");
    }

    [Fact]
    public Task StartImportIsDrivenByTheHostsOwnPump() => DedicatedThread.RunAsync(() =>
    {
        // The game-loop shape: start the import, then hand the engine a turn per frame and watch the operation.
        // Nothing here blocks, and no engine work happens on any thread but this one.
        var loader = new DelayedModuleLoader(new Dictionary<string, string>
        {
            ["./frame.js"] = "import { dep } from './dep.js'; export const value = dep;",
            ["./dep.js"] = "export const dep = 'ready';",
        }, TimeSpan.FromMilliseconds(20));

        var engine = CreateEngine(loader);

        var import = engine.Modules.StartImport("./frame.js");
        import.IsCompleted.Should().BeFalse();

        var frames = 0;
        while (!import.IsCompleted && frames < 2000)
        {
            frames++;
            engine.Tasks.ProcessTasks();
            Thread.Sleep(1);
        }

        import.IsCompleted.Should().BeTrue();
        import.IsFaulted.Should().BeFalse();
        import.GetResult().Get("value").AsString().Should().Be("ready");
        frames.Should().BeGreaterThan(1, "the load should not have completed on the turn that started it");
    });

    [Fact]
    public void AFailedPumpedImportReportsTheErrorRatherThanThrowingOutOfThePump()
    {
        var loader = new DeferredModuleLoader();
        var engine = CreateEngine(loader);

        var import = engine.Modules.StartImport("./nope.js");
        loader.Fail("./nope.js", new InvalidOperationException("asset bundle missing"));

        engine.Tasks.ProcessTasks();

        import.IsCompleted.Should().BeTrue();
        import.IsFaulted.Should().BeTrue();
        import.Error!.Get("message").AsString().Should().Be("Could not load module.");
        Invoking(() => import.GetResult()).Should().Throw<PromiseRejectedException>();
    }

    [Fact]
    public void AskingAPumpedImportForItsResultTooEarlyIsAnActionableError()
    {
        var loader = new DeferredModuleLoader();
        var engine = CreateEngine(loader);

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

        var engine = CreateEngine(loader);

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

        var engine = CreateEngine(loader);
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
        var engine = CreateEngine(loader);
        engine.Modules.Add("lib", "export const = broken;");

        var import = engine.Modules.StartImport("./root.js");
        loader.Deliver("./root.js", "import './mid.js';");
        engine.Tasks.ProcessTasks();
        loader.Deliver("./mid.js", "import 'lib';");

        Invoking(() => engine.Tasks.ProcessTasks()).Should().NotThrow();

        import.IsCompleted.Should().BeTrue("the parse failure must settle the import rather than strand it");
        import.IsFaulted.Should().BeTrue();
        import.Error!.Get("name").AsString().Should().Be("SyntaxError");
    }

    [Fact]
    public void ASyntaxErrorInAsynchronouslyDeliveredSourceRejectsTheImport()
    {
        var loader = new DeferredModuleLoader();
        var engine = CreateEngine(loader);

        var import = engine.Modules.StartImport("./broken.js");
        loader.Deliver("./broken.js", "export const = ;");

        Invoking(() => engine.Tasks.ProcessTasks()).Should().NotThrow();

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
        var engine = CreateEngine(loader);

        var import = engine.Modules.StartImport("./unreachable.js");
        engine.Tasks.ProcessTasks();

        import.IsCompleted.Should().BeTrue();
        import.IsFaulted.Should().BeTrue();
        import.Error!.Get("message").AsString().Should().Be("Could not load module.");
        var exception = Invoking(() => import.GetResult()).Should().Throw<PromiseRejectedException>().Which;
        JintException.TryGetClrException(exception, out var clrException).Should().BeTrue();
        clrException.Should().BeOfType<InvalidOperationException>().Which.Message.Should().Be("transport exploded");
    }

    private sealed class ThrowingLoader : IAsyncModuleLoader
    {
        public ResolvedSpecifier Resolve(string? referencingModuleLocation, ModuleRequest moduleRequest)
            => new(moduleRequest, moduleRequest.Specifier, Uri: null, SpecifierType.Bare);

        public ModuleRecord LoadModule(Engine engine, ResolvedSpecifier resolved)
            => throw new InvalidOperationException("The engine must not take the synchronous path for an IAsyncModuleLoader.");

        public void LoadModuleAsync(Engine engine, ResolvedSpecifier resolved, ModuleLoadCompletion completion)
            => throw new InvalidOperationException("transport exploded");
    }

    [Fact]
    public void TheEnginesCancellationTokenReachesTheLoadersFetch()
    {
        // A host that registered options.ObserveCancellation(token) means it for the loader's I/O too:
        // AsyncModuleLoader hands LoadModuleContentsAsync the same token the interpreter's cancellation
        // constraint observes, so one token stops both the script and the fetches it started.
        using var cts = new CancellationTokenSource();
        var loader = new TokenCapturingLoader();
        var engine = new Engine(options => options.UseModules(loader).ObserveCancellation(cts.Token));

        engine.Modules.StartImport("./fetch.js");

        loader.CapturedToken.Should().Be(cts.Token);
    }

    [Fact]
    public Task ACanceledFetchIsAnOrdinarySanitizedLoadFailure() => DedicatedThread.RunAsync(() =>
    {
        // This token belongs to the transport, not the engine operation. It is therefore an ordinary load
        // failure and crosses the same disclosure boundary as every other loader exception.
        var loader = new TokenCapturingLoader();
        var engine = CreateEngine(loader);

        var import = engine.Modules.StartImport("./doomed.js");
        loader.CancelPendingFetch();

        // The cancellation reaches the completion through task continuations on another thread, so the
        // settle is not necessarily enqueued by the time Cancel returns - pump until it lands.
        var frames = 0;
        while (!import.IsCompleted && frames < 2000)
        {
            frames++;
            engine.Tasks.ProcessTasks();
            Thread.Sleep(1);
        }

        import.IsFaulted.Should().BeTrue();
        import.Error!.Get("message").AsString().Should().Be("Could not load module.");
    });

    /// <summary>
    /// The same cancellation as <see cref="ACanceledFetchIsAnOrdinarySanitizedLoadFailure"/>, reaching a
    /// caller that is awaiting rather than pumping. The rejection <em>value</em> is what is asserted, not
    /// merely the exception type: a <see cref="PromiseRejectedException"/> is also what a promise budget
    /// that ran out throws, so a type-only assertion would go on passing on a runner where the settle never
    /// arrived — testing nothing, and never failing to say so. Asserting the sanitized message costs that
    /// test its vacuity, which is why the engine comes through <see cref="CreateEngine"/>: the budget it
    /// sets is what keeps the tightened assertion honest instead of flaky.
    /// </summary>
    [Fact]
    public async Task ACanceledFetchSettlesWhileImportAsyncOwnsTheEngine()
    {
        var loader = new TokenCapturingLoader();
        var engine = CreateEngine(loader);

        var import = engine.Modules.ImportAsync("./doomed.js");
        loader.CancelPendingFetch();

        var rejection = await Invoking(() => import).Should().ThrowAsync<PromiseRejectedException>();
        rejection.Which.RejectedValue.Get("message").AsString().Should().Be("Could not load module.");
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
        var engine = CreateEngine(loader);

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
        var engine = CreateEngine(loader);

        var import = engine.Modules.StartImport("./main.js");
        loader.Deliver("./main.js", "import data from './config.json' with { type: 'json' }; export const message = data.message;");
        engine.Tasks.ProcessTasks();
        loader.Deliver("./config.json", """{ "message": "hello" }""");
        engine.Tasks.ProcessTasks();

        import.IsCompleted.Should().BeTrue();
        import.GetResult().Get("message").AsString().Should().Be("hello");
    }

    [Fact]
    public void BytesDeliveredForABytesImportStayBytes()
    {
        // SetSource(byte[]) hands raw content over; with { type: 'bytes' } must receive it untouched rather
        // than round-tripped through a string decode.
        var loader = new DeferredModuleLoader();
        var engine = CreateEngine(loader);

        engine.Execute("globalThis.result = null; import('./blob.bin', { with: { type: 'bytes' } }).then(ns => { result = ns.default.length + ':' + ns.default[0] + ',' + ns.default[1]; });");

        loader.Take("./blob.bin").SetSource(new byte[] { 200, 7 });
        engine.Tasks.ProcessTasks();

        engine.Evaluate("result").AsString().Should().Be("2:200,7");
    }

    [Fact]
    public Task TheSynchronousImportIsWokenByASettleFromABackgroundThread() => DedicatedThread.RunAsync(() =>
    {
        // The blocking Import drains the event loop while the load is in flight; a settle arriving from
        // another thread - the shape every real fetch has - only enqueues, and the drain on this thread has
        // to notice it. Every other sync-Import test settles inline on the engine thread, which never
        // exercises that wait.
        var engine = CreateEngine(new BackgroundThreadLoader());

        var ns = engine.Modules.Import("./bg.js");

        ns.Get("value").AsString().Should().Be("from-background");
    });

    private sealed class BackgroundThreadLoader : IAsyncModuleLoader
    {
        public ResolvedSpecifier Resolve(string? referencingModuleLocation, ModuleRequest moduleRequest)
            => new(moduleRequest, moduleRequest.Specifier, Uri: null, SpecifierType.Bare);

        public ModuleRecord LoadModule(Engine engine, ResolvedSpecifier resolved)
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

        var engine = CreateEngine(loader);

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
            options.UseModules(loader);
            // Small so a regression fails the test in half a second rather than the default ten.
            options.Constraints.PromiseTimeout = TimeSpan.FromMilliseconds(500);
        });

        engine.SetValue("hostImport", new Func<string>(() => engine.Modules.Import("./root.js").Get("value").AsString()));
        engine.Execute("globalThis.result = null; Promise.resolve().then(() => { result = hostImport(); });");
        engine.Tasks.ProcessTasks();

        engine.Evaluate("result").AsString().Should().Be("root+dep");
    }

    [Fact]
    public void AnInlineFailureStillReachesTheBlockingImportAsAnError()
    {
        var loader = new InlineFailingLoader();
        var engine = CreateEngine(loader);

        Invoking(() => engine.Modules.Import("./missing.js"))
            .Should().Throw<JavaScriptException>().WithMessage("*not in the bundle*");
    }

    [Fact]
    public Task AGraphMixingWarmAndTrulyAsynchronousAnswersStillLoadsThroughTheBlockingImport() => DedicatedThread.RunAsync(() =>
    {
        // The root settles inline on the import's own stack; its dependency arrives from the thread pool
        // later. The blocking Import has to switch from the synchronous continuation to draining the event
        // loop mid-graph.
        var loader = new WarmModuleLoader(new Dictionary<string, string>
        {
            ["./root.js"] = "import { dep } from './slow.js'; export const value = 'root+' + dep;",
            ["./slow.js"] = "export const dep = 'slow';",
        }, coldSpecifiers: ["./slow.js"]);

        var engine = CreateEngine(loader);

        engine.Modules.Import("./root.js").Get("value").AsString().Should().Be("root+slow");
    });

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

        public ModuleRecord LoadModule(Engine engine, ResolvedSpecifier resolved)
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

        var engine = CreateEngine(loader);

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

        var engine = CreateEngine(loader);

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
    /// <see cref="ModuleRecord.Location"/> from <see cref="Uri.LocalPath"/>, so the referrer location a nested
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
        var engine = CreateEngine(new SynchronousLoader());

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
            "./imports-missing.js" => "import './missing.js';",
            _ => throw new FileNotFoundException(resolved.ModuleRequest.Specifier),
        };
    }

    [Fact]
    public void LinkingBeforeTheGraphHasLoadedIsRefusedWithAnActionableError()
    {
        // A host driving a module by hand has to run the load phase first. With a synchronous loader Link()
        // still does it implicitly; with an asynchronous one it cannot, and says so.
        var loader = new DeferredModuleLoader();
        var engine = CreateEngine(loader);

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
        var engine = CreateEngine(loader);

        var module = ModuleFactory.BuildSourceTextModule(engine, Engine.PrepareModule("import { v } from './dep.js'; export const value = v;", "./root.js"));

        module.LoadRequestedModules();
        loader.Deliver("./dep.js", "export const v = 11;");
        engine.Tasks.ProcessTasks();

        module.Link();
        module.Evaluate();
        engine.Tasks.ProcessTasks();

        ModuleRecord.GetModuleNamespace(module).Get("value").AsNumber().Should().Be(11);
    }

    [Fact]
    public void AShadowRealmModuleDeliveredAsynchronouslyEvaluatesInTheShadowRealm()
    {
        // ShadowRealm.importValue enters the shadow realm's execution context only around the synchronous
        // start of the load. The settle arrives on a later event-loop turn, so the module record has to be
        // built against the realm captured when the load started — otherwise the sandboxed module's top-level
        // code runs against the principal realm's globals, which is precisely what a ShadowRealm exists to
        // prevent.
        var loader = new DeferredModuleLoader();
        var engine = CreateEngine(loader);

        engine.Execute("""
            globalThis.imported = null;
            globalThis.sandbox = new ShadowRealm();
            sandbox.importValue('./sandboxed.js', 'value').then(v => { imported = v; });
            """);

        loader.Deliver("./sandboxed.js", "globalThis.leak = 'from-module'; export const value = 'ok';");
        engine.Tasks.ProcessTasks();

        engine.Evaluate("imported").AsString().Should().Be("ok");
        engine.Evaluate("typeof globalThis.leak").AsString().Should().Be("undefined", "the module's top-level code must not have run against the principal realm's globals");
        engine.Evaluate("sandbox.evaluate(\"typeof globalThis.leak\")").AsString().Should().Be("string", "the module's top-level code belongs to the shadow realm");
    }

    [Fact]
    public void AResolutionFailureInsideAnAsyncGraphRejectsEveryWaitingImportInsteadOfStrandingThem()
    {
        // ModuleLoader.Resolve is allowed to refuse a specifier with ModuleResolutionException — a sibling of
        // JavaScriptException the load pipeline's JavaScriptException-only handling did not see. Thrown while
        // walking a graph inside a queued load-completion job, it has no caller to erupt to: it must become
        // the load's failure, for every importer waiting on it, or their promises hang forever.
        var loader = new DeferredModuleLoader();
        loader.RefuseToResolve.Add("./forbidden.js");
        var engine = CreateEngine(loader);

        var first = engine.Modules.StartImport("./a.js");
        loader.Deliver("./a.js", "import './shared.js';");
        engine.Tasks.ProcessTasks();

        var second = engine.Modules.StartImport("./b.js");
        loader.Deliver("./b.js", "import './shared.js';");
        engine.Tasks.ProcessTasks();

        // Both graphs now wait on one in-flight load of './shared.js', whose own import is refused by Resolve.
        loader.Deliver("./shared.js", "import './forbidden.js';");
        Invoking(() => engine.Tasks.ProcessTasks()).Should().NotThrow();

        first.IsCompleted.Should().BeTrue("the resolution failure must settle the import rather than strand it");
        first.IsFaulted.Should().BeTrue();
        first.Error!.Get("message").AsString().Should().Be("Could not load module.");

        second.IsCompleted.Should().BeTrue("every waiter attached to the shared load must be finished, not only the first");
        second.IsFaulted.Should().BeTrue();
    }

    [Fact]
    public void AConstraintExceptionFromTheLoaderPropagatesAndLeavesNoPoisonedLoadBehind()
    {
        // Constraint exceptions exist to bound execution; flattened into a rejection they no longer bound
        // anything, and a host's catch (ExecutionCanceledException) shutdown handling never runs. The failed
        // attempt must also not stay registered as in flight, or the next import of the same specifier would
        // attach to a completion that can never settle.
        var loader = new ConstraintThrowingLoader();
        var engine = CreateEngine(loader);

        Invoking(() => engine.Modules.Import("./guarded.js")).Should().Throw<ExecutionCanceledException>();

        engine.Modules.Import("./guarded.js").Get("value").AsString().Should().Be("second-attempt");
        loader.Asked.Should().Be(2, "the cancelled attempt must not have been recorded as an in-flight load");
    }

    private sealed class ConstraintThrowingLoader : IAsyncModuleLoader
    {
        public int Asked;

        public ResolvedSpecifier Resolve(string? referencingModuleLocation, ModuleRequest moduleRequest)
            => new(moduleRequest, moduleRequest.Specifier, Uri: null, SpecifierType.Bare);

        public ModuleRecord LoadModule(Engine engine, ResolvedSpecifier resolved)
            => throw new InvalidOperationException("The engine must not take the synchronous path for an IAsyncModuleLoader.");

        public void LoadModuleAsync(Engine engine, ResolvedSpecifier resolved, ModuleLoadCompletion completion)
        {
            if (++Asked == 1)
            {
                // The shape of a loader observing the engine's cancellation before starting its fetch.
                throw new ExecutionCanceledException();
            }

            completion.SetSource("export const value = 'second-attempt';");
        }
    }

    [Fact]
    public Task AThrowingModuleSourceHookRejectsTheImportInsteadOfStrandingIt() => DedicatedThread.RunAsync(() =>
    {
        // GetModuleSource is a host hook that runs inside the queued build of an asynchronously delivered
        // module. A failure there has no caller to erupt to; escaping would leave every waiter permanently
        // pending behind a half-finished load.
        var loader = new ThrowingModuleSourceLoader();
        var engine = CreateEngine(loader);

        var import = engine.Modules.StartImport("./hooked.js");

        var frames = 0;
        while (!import.IsCompleted && frames < 2000)
        {
            frames++;
            Invoking(() => engine.Tasks.ProcessTasks()).Should().NotThrow();
            Thread.Sleep(1);
        }

        import.IsCompleted.Should().BeTrue("the hook failure must settle the import rather than strand it");
        import.IsFaulted.Should().BeTrue();
        import.Error!.Get("message").AsString().Should().Be("Could not load module.");
    });

    private sealed class ThrowingModuleSourceLoader : AsyncModuleLoader
    {
        public override ResolvedSpecifier Resolve(string? referencingModuleLocation, ModuleRequest moduleRequest)
            => new(moduleRequest, moduleRequest.Specifier, Uri: null, SpecifierType.Bare);

        protected override async Task<string> LoadModuleContentsAsync(Engine engine, ResolvedSpecifier resolved, CancellationToken cancellationToken)
        {
            // The delay matters: the settle must arrive on a queued event-loop turn, where no caller is left.
            await Task.Delay(10, cancellationToken).ConfigureAwait(false);
            return "export const value = 1;";
        }

        protected override Jint.Native.Object.ObjectInstance? GetModuleSource(Engine engine, ResolvedSpecifier resolved)
            => throw new InvalidOperationException("source hook failed");
    }

    [Fact]
    public void ABuilderRegisteredWhileAFetchOfTheSameKeyIsInFlightDoesNotForkTheModule()
    {
        // Modules.Add of a key whose fetch is already airborne must not produce two live module records —
        // one from the builder, one from the fetch — each with its own top-level state. Every importer of
        // the key gets the same record: the fetch that was started first.
        var loader = new DeferredModuleLoader();
        var engine = CreateEngine(loader);

        var first = engine.Modules.StartImport("lib");
        engine.Modules.Add("lib", "export const who = 'builder';");
        var second = engine.Modules.StartImport("lib");

        loader.Deliver("lib", "export const who = 'fetched';");
        engine.Tasks.ProcessTasks();

        first.GetResult().Get("who").AsString().Should().Be("fetched");
        second.GetResult().Should().BeSameAs(first.GetResult(), "one key must denote one module record");
        loader.AskedFor("lib").Should().Be(1);
    }

    [Fact]
    public void ASyntaxErrorReachesTheBlockingImportWithItsOriginalLocation()
    {
        // The failure travels from the queued build to the blocking importer as a promise rejection, which
        // can carry only the error value — but an error UI reads JavaScriptException.Location, and the parse
        // error knows its file, line and column. The original exception must survive the crossing.
        var engine = new Engine(options =>
        {
            options.UseModules(new BackgroundDictionaryLoader(new Dictionary<string, string>
            {
                ["./broken.js"] = "export const ok = 1;\nexport const = broken;",
            }));
            options.Modules.ExposeDetailedLoadErrors = true;
        });

        var ex = Invoking(() => engine.Modules.Import("./broken.js"))
            .Should().Throw<JavaScriptException>().Which;

        ex.Location.Start.Line.Should().Be(2, "the parse error's location must not be reduced to (0, 0)");
        ex.Location.SourceFile.Should().Be("./broken.js");
    }

    [Fact]
    public Task AResolutionFailureReachesTheBlockingImportThroughTheDisclosurePolicy() => DedicatedThread.RunAsync(() =>
    {
        // The refusal happens inside the queued build of './mid.js', two loads deep. Once it crosses that
        // asynchronous module boundary, a blocking importer must see the same sanitized error as every other
        // importer; the original remains available only through the host diagnostic contract.
        var loader = new BackgroundDictionaryLoader(new Dictionary<string, string>
        {
            ["./root.js"] = "import './mid.js';",
            ["./mid.js"] = "import './forbidden.js';",
        });
        loader.RefuseToResolve.Add("./forbidden.js");

        var engine = CreateEngine(loader);

        var exception = Invoking(() => engine.Modules.Import("./root.js"))
            .Should().Throw<JavaScriptException>().Which;

        exception.Message.Should().Be("Could not load module.");
        JintException.TryGetClrException(exception, out var original).Should().BeTrue();
        original.Should().BeOfType<ModuleResolutionException>()
            .Which.Specifier.Should().Be("./forbidden.js");
    });

    /// <summary>
    /// Serves from a dictionary, always from a background thread — the shape of a real fetch, and the only
    /// shape whose settles run as queued event-loop jobs. <see cref="RefuseToResolve"/> makes
    /// <see cref="Resolve"/> refuse a specifier with <see cref="ModuleResolutionException"/>.
    /// </summary>
    private sealed class BackgroundDictionaryLoader : IAsyncModuleLoader
    {
        private readonly IReadOnlyDictionary<string, string> _sources;

        public BackgroundDictionaryLoader(IReadOnlyDictionary<string, string> sources)
        {
            _sources = sources;
        }

        public HashSet<string> RefuseToResolve { get; } = new(StringComparer.Ordinal);

        public ResolvedSpecifier Resolve(string? referencingModuleLocation, ModuleRequest moduleRequest)
        {
            if (RefuseToResolve.Contains(moduleRequest.Specifier))
            {
                throw new ModuleResolutionException("Specifier is not allowed", moduleRequest.Specifier, referencingModuleLocation, filePath: null);
            }

            return new(moduleRequest, moduleRequest.Specifier, Uri: null, SpecifierType.Bare);
        }

        public ModuleRecord LoadModule(Engine engine, ResolvedSpecifier resolved)
            => throw new InvalidOperationException("The engine must not take the synchronous path for an IAsyncModuleLoader.");

        public void LoadModuleAsync(Engine engine, ResolvedSpecifier resolved, ModuleLoadCompletion completion)
        {
            var thread = new Thread(() =>
            {
                Thread.Sleep(10);
                if (_sources.TryGetValue(resolved.ModuleRequest.Specifier, out var code))
                {
                    completion.SetSource(code);
                }
                else
                {
                    completion.SetError($"404 {resolved.ModuleRequest.Specifier}");
                }
            });
            thread.IsBackground = true;
            thread.Start();
        }
    }

    [Fact]
    public void AFailedBlockingImportRaisesNoUnhandledRejectionEvent()
    {
        // The load-phase promise is engine-internal plumbing the host never sees; its rejection is delivered
        // to the blocking caller as an exception. Unhandled-rejection telemetry must not alarm about it.
        var engine = CreateEngine(new SynchronousLoader());

        var events = new List<PromiseRejectionTrackerEventArgs>();
        engine.Tasks.PromiseRejectionTracker += (_, args) => events.Add(args);

        Invoking(() => engine.Modules.Import("./imports-missing.js")).Should().Throw<JavaScriptException>();

        events.Should().BeEmpty("the failure was delivered as an exception, and no host-observable promise was involved");
    }

    [Fact]
    public void StartImportDeliversAResolutionFailureThroughTheOperation()
    {
        // The single most common failure — the loader refusing to resolve — must arrive through the
        // operation's IsFaulted/Error channel like every other failure, not erupt from the start call: host
        // code is written to the documented poll-then-GetResult pattern, and where resolution happens (the
        // synchronous start or an asynchronous settle) is not the host's business.
        var loader = new DeferredModuleLoader();
        loader.RefuseToResolve.Add("./forbidden.js");
        var engine = CreateEngine(loader);

        ModuleImportOperation import = null!;
        Invoking(() => import = engine.Modules.StartImport("./forbidden.js")).Should().NotThrow();
        engine.Tasks.ProcessTasks();

        import.IsCompleted.Should().BeTrue();
        import.IsFaulted.Should().BeTrue();
        import.Error!.Get("message").AsString().Should().Be("Could not load module.");
    }

    [Fact]
    public void ABlockingImportThatCannotProgressInsideAJobFailsFastWithTheActualReason()
    {
        // Inside an event-loop job the re-entrancy guard makes queued work unrunnable, so a blocking import
        // of a load that needs even one turn is structurally unable to finish. It used to sit out the whole
        // PromiseTimeout and then blame the loader for being slow; the host should instead learn immediately
        // what is wrong and what to do.
        var loader = new DeferredModuleLoader();
        var engine = CreateEngine(loader);

        engine.SetValue("hostImport", new Action(() => engine.Modules.Import("./never.js")));

        // The reaction job runs during Execute's own end-of-script drain, and the import inside it fails
        // there and then — not ten seconds later with a TimeoutException blaming the loader.
        Invoking(() => engine.Execute("Promise.resolve().then(() => hostImport());"))
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*event-loop job*StartImport*");
    }

    [Fact]
    public void TheSynchronousEntryOfAnAsyncLoaderExplainsTheMisuseInsteadOfClaimingAMissingModule()
    {
        // AsyncModuleLoader.LoadModuleContents throws NotSupportedException with guidance on how to reach the
        // loader correctly; the generic could-not-load wrapping used to replace it with what reads as a
        // missing file.
        var loader = new DelayedModuleLoader(new Dictionary<string, string>(), TimeSpan.Zero);
        var engine = CreateEngine(loader);

        Invoking(() => ((IModuleLoader) loader).LoadModule(engine, loader.Resolve(null, new ModuleRequest("./x.js", []))))
            .Should().Throw<NotSupportedException>()
            .WithMessage("*ImportAsync*");
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
