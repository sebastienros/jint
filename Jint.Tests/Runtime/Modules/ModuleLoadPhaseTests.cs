using Jint.Native;
using Jint.Native.Promise;
using Jint.Runtime.Modules;

using Module = Jint.Runtime.Modules.Module;

namespace Jint.Tests.Runtime.Modules;

/// <summary>
/// The specification's asynchronous load phase — LoadRequestedModules, InnerModuleLoading,
/// ContinueModuleLoading and the <c>[[LoadedModules]]</c> field they populate. These reach into the engine for
/// module status and promise state; the host-facing half of the same feature lives in
/// <c>Jint.Tests.PublicInterface.AsyncModuleLoaderTests</c>.
/// </summary>
public class ModuleLoadPhaseTests
{
    /// <summary>
    /// A loader that answers from a dictionary but counts, and can hold a specifier back until the test
    /// releases it — the two things a synchronous loader cannot show about the load phase.
    /// </summary>
    private sealed class CountingAsyncLoader : IAsyncModuleLoader
    {
        private readonly Dictionary<string, string> _sources;
        private readonly List<ModuleLoadCompletion> _held = new();

        public CountingAsyncLoader(Dictionary<string, string> sources, params string[] holdBack)
        {
            _sources = sources;
            HeldBack = new HashSet<string>(holdBack, StringComparer.Ordinal);
        }

        public HashSet<string> HeldBack { get; }
        public List<string> Asked { get; } = new();

        public ResolvedSpecifier Resolve(string referencingModuleLocation, ModuleRequest moduleRequest)
            => new(moduleRequest, moduleRequest.Specifier, Uri: null, SpecifierType.Bare);

        public Module LoadModule(Engine engine, ResolvedSpecifier resolved)
            => throw new InvalidOperationException("the synchronous path must not be taken");

        public void LoadModuleAsync(Engine engine, ResolvedSpecifier resolved, ModuleLoadCompletion completion)
        {
            var specifier = resolved.ModuleRequest.Specifier;
            Asked.Add(specifier);

            if (HeldBack.Contains(specifier))
            {
                _held.Add(completion);
                return;
            }

            Answer(completion, specifier);
        }

        public void Release(string specifier)
        {
            var completion = _held.Single(c => c.Resolved.ModuleRequest.Specifier == specifier);
            _held.Remove(completion);
            HeldBack.Remove(specifier);
            Answer(completion, specifier);
        }

        private void Answer(ModuleLoadCompletion completion, string specifier)
        {
            if (_sources.TryGetValue(specifier, out var code))
            {
                completion.SetSource(code);
            }
            else
            {
                completion.SetError($"Module not found: {specifier}");
            }
        }
    }

    [Fact]
    public void LoadRequestedModulesReturnsAPromiseThatSettlesWhenTheGraphHasLoaded()
    {
        var loader = new CountingAsyncLoader(new Dictionary<string, string>
        {
            ["root"] = "import { v } from 'dep'; export const value = v;",
            ["dep"] = "export const v = 3;",
        }, holdBack: "dep");

        var engine = new Engine(options => options.UseModules(loader));
        var module = ModuleFactory.BuildSourceTextModule(engine, Engine.PrepareModule("import { v } from 'dep'; export const value = v;", "root"));

        var loadPromise = (JsPromise) module.LoadRequestedModules();
        loadPromise.State.Should().Be(PromiseState.Pending, "the load phase cannot finish while 'dep' is held back");

        loader.Release("dep");
        engine.Advanced.ProcessTasks();

        loadPromise.State.Should().Be(PromiseState.Fulfilled);
        loadPromise.Value.Should().Be(JsValue.Undefined, "https://tc39.es/ecma262/#sec-InnerModuleLoading step 5.c resolves with undefined");
    }

    [Fact]
    public void ALoadFailureRejectsTheLoadPhasePromiseInsteadOfThrowing()
    {
        var loader = new CountingAsyncLoader(new Dictionary<string, string>
        {
            ["root"] = "import 'missing';",
        }, holdBack: "missing");

        var engine = new Engine(options => options.UseModules(loader));
        var module = ModuleFactory.BuildSourceTextModule(engine, Engine.PrepareModule("import 'missing';", "root"));

        var loadPromise = (JsPromise) module.LoadRequestedModules();
        loader.Release("missing");
        engine.Advanced.ProcessTasks();

        loadPromise.State.Should().Be(PromiseState.Rejected);
        loadPromise.Value.Get("message").AsString().Should().Contain("Module not found: missing");
    }

    [Fact]
    public void AModuleStartsNewAndBecomesUnlinkedOnlyWhenTheWholeGraphHasLoaded()
    {
        // https://tc39.es/ecma262/#sec-InnerModuleLoading step 5.b: every visited module leaves the `new`
        // state together, when the last pending request settles - not one by one as each arrives.
        var loader = new CountingAsyncLoader(new Dictionary<string, string>
        {
            ["root"] = "import 'first'; import 'second';",
            ["first"] = "export const a = 1;",
            ["second"] = "export const b = 2;",
        }, holdBack: "second");

        var engine = new Engine(options => options.UseModules(loader));
        var module = (CyclicModule) ModuleFactory.BuildSourceTextModule(engine, Engine.PrepareModule("import 'first'; import 'second';", "root"));

        module.Status.Should().Be(ModuleStatus.New);

        module.LoadRequestedModules();
        module.Status.Should().Be(ModuleStatus.New, "'second' has not arrived, so the graph is not loaded");

        loader.Release("second");
        engine.Advanced.ProcessTasks();

        module.Status.Should().Be(ModuleStatus.Unlinked);
    }

    [Fact]
    public void TheLoaderIsAskedOncePerReferrerAndSpecifierAcrossAnEntireGraph()
    {
        // https://tc39.es/ecma262/#sec-HostLoadImportedModule's consistency requirement, and the reason
        // [[LoadedModules]] exists: 'shared' is wanted by three referrers and fetched once.
        var loader = new CountingAsyncLoader(new Dictionary<string, string>
        {
            ["root"] = "import 'a'; import 'b'; import { s } from 'shared'; export const v = s;",
            ["a"] = "import { s } from 'shared'; export const av = s;",
            ["b"] = "import { s } from 'shared'; export const bv = s;",
            ["shared"] = "export const s = 'once';",
        });

        var engine = new Engine(options => options.UseModules(loader));

        var ns = engine.Modules.Import("root");

        ns.Get("v").AsString().Should().Be("once");
        loader.Asked.Count(s => string.Equals(s, "shared", StringComparison.Ordinal)).Should().Be(1);
    }

    [Fact]
    public void ReimportingAModuleDoesNotAskTheLoaderAgain()
    {
        var loader = new CountingAsyncLoader(new Dictionary<string, string>
        {
            ["lib"] = "export const value = 5;",
        });

        var engine = new Engine(options => options.UseModules(loader));

        engine.Modules.Import("lib").Get("value").AsNumber().Should().Be(5);
        engine.Modules.Import("lib").Get("value").AsNumber().Should().Be(5);

        loader.Asked.Should().Equal("lib");
    }

    [Fact]
    public void ACycleIsLoadedWithoutRecursingForever()
    {
        // InnerModuleLoading guards on state.[[Visited]], so a module already being visited is not descended
        // into again. Without it a cycle would recurse until the stack ran out.
        var loader = new CountingAsyncLoader(new Dictionary<string, string>
        {
            ["A"] = "import { b } from 'B'; export const a = 'a';",
            ["B"] = "import { a } from 'A'; export const b = 'b';",
        });

        var engine = new Engine(options => options.UseModules(loader));

        engine.Modules.Import("A").Get("a").AsString().Should().Be("a");
        loader.Asked.Should().BeEquivalentTo(["A", "B"]);
    }

    [Fact]
    public void ASynchronousLoaderStillFinishesTheLoadPhaseInline()
    {
        // The cost of the load phase for the existing synchronous loaders: none, in the sense that matters -
        // the promise is already settled by the time LoadRequestedModules returns, so nothing needs a turn of
        // the event loop and Import stays a straight-line call.
        var engine = new Engine();
        engine.Modules.Add("dep", "export const v = 1;");
        engine.Modules.Add("root", "import { v } from 'dep'; export const value = v;");

        var module = (CyclicModule) engine.Modules.Load(null, new ModuleRequest("root", []));
        var loadPromise = (JsPromise) module.LoadRequestedModules();

        loadPromise.State.Should().Be(PromiseState.Fulfilled);
        module.Status.Should().Be(ModuleStatus.Unlinked);
    }

    [Fact]
    public void LoadingIsFinishedBeforeLinkingBegins()
    {
        // The ordering the load phase makes structural: a graph containing both a linking error and an
        // unresolvable specifier reports the loading failure, because nothing links until everything loaded.
        var loader = new CountingAsyncLoader(new Dictionary<string, string>
        {
            ["main"] = "import './has-linking-error'; import './does-not-exist';",
            ["./has-linking-error"] = "import { nonExistent } from './has-linking-error';",
        });

        var engine = new Engine(options => options.UseModules(loader));

        var ex = Invoking(() => engine.Modules.Import("main")).Should().Throw<Exception>().Which;

        ex.Message.Should().Contain("./does-not-exist");
        ex.Message.Should().NotContain("Ambiguous");
    }

    [Fact]
    public void APendingLoadIsForgottenWhenTheEngineEndsItsEvaluationCycle()
    {
        // A load in flight belongs to the cycle that started it. RestoreGlobalSnapshot ends that cycle, so the
        // completion is fenced off at dequeue - and the pending entry must go too, or the next cycle would
        // attach to a load that can never finish.
        var loader = new CountingAsyncLoader(new Dictionary<string, string>
        {
            ["late"] = "export const value = 'second-cycle';",
        }, holdBack: "late");

        var engine = new Engine(options => options.UseModules(loader));
        var snapshot = engine.Advanced.CaptureGlobalSnapshot();

        var abandoned = engine.Modules.StartImport("late");
        engine.Advanced.RestoreGlobalSnapshot(snapshot);

        // The abandoned load's completion is now stale; delivering it does nothing. The operation reports the
        // abandonment rather than the delivery, which is what makes the fence observable to a host that only
        // has the operation to poll - the promise behind it stays pending forever.
        loader.Release("late");
        engine.Advanced.ProcessTasks();
        abandoned.IsFaulted.Should().BeTrue("a promise registered before a restore never settles into the engine afterwards");
        abandoned.Namespace.Should().BeNull();

        // A fresh import asks again rather than waiting on the abandoned load.
        var reimported = engine.Modules.StartImport("late");
        engine.Advanced.ProcessTasks();

        reimported.IsCompleted.Should().BeTrue();
        reimported.GetResult().Get("value").AsString().Should().Be("second-cycle");
        loader.Asked.Should().Equal("late", "late");
    }
}
