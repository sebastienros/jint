#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Jint.Runtime;
using Jint.Runtime.Modules;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// <c>Engine.Modules.Import</c> is the host's spelling of a dynamic <c>import()</c>, and the spec's
/// <see href="https://tc39.es/ecma262/#sec-ContinueDynamicImport">ContinueDynamicImport</see> hands the
/// module record to <c>Evaluate()</c> unconditionally. So every state <c>Evaluate()</c> accepts —
/// <see href="https://tc39.es/ecma262/#sec-moduleevaluation">its step 2</see> lists linked, evaluating-async
/// and evaluated — has to reach it, not just the two an entry-point import happens to produce.
///
/// <para>
/// The two states a host runs into are the ones some <em>other</em> operation left the module in: a
/// <c>import defer</c> elsewhere in the graph leaves it linked-but-unevaluated, and an import still
/// suspended on a top-level <c>await</c> leaves it evaluating-async. Handing back a namespace over either
/// gives the host uninitialised bindings.
/// </para>
/// </summary>
public class HostModuleImportStateTests
{
    private sealed class DictionaryModuleLoader : ModuleLoader
    {
        private readonly Dictionary<string, string> _sources;

        public DictionaryModuleLoader(Dictionary<string, string> sources) => _sources = sources;

        public override ResolvedSpecifier Resolve(string? referencingModuleLocation, ModuleRequest moduleRequest)
            => new(moduleRequest, moduleRequest.Specifier, Uri: null, SpecifierType.Bare);

        protected override string LoadModuleContents(Engine engine, ResolvedSpecifier resolved) => _sources[resolved.Key];
    }

    [Test]
    public void ImportingAModuleADeferredImportLeftLinkedEvaluatesIt()
    {
        var engine = new Engine(options => options.UseModules(new DictionaryModuleLoader(new Dictionary<string, string>
        {
            ["main"] = "import defer * as d from 'dep'; export const touch = () => d.v;",
            ["dep"] = "globalThis.depEvaluations = (globalThis.depEvaluations ?? 0) + 1; export const v = 5;",
        })));

        engine.Modules.Import("main");
        engine.Evaluate("globalThis.depEvaluations ?? 0").AsNumber().Should().Be(0, "a deferred import must not evaluate its target");

        var dep = engine.Modules.Import("dep");

        dep.Get("v").AsNumber().Should().Be(5);
        engine.Evaluate("globalThis.depEvaluations").AsNumber().Should().Be(1, "the import that needs the value is what evaluates it");
    }

    [Test]
    public void ImportingAModuleStillSuspendedOnTopLevelAwaitWaitsForIt()
    {
        var gate = new TaskCompletionSource<int>();

        var engine = new Engine(options =>
        {
            options.ExperimentalFeatures = ExperimentalFeature.TaskInterop;
            options.Constraints.PromiseTimeout = TimeSpan.FromSeconds(30);
            options.UseModules(new DictionaryModuleLoader(new Dictionary<string, string>
            {
                ["m"] = "const x = await gate(); export const v = x;",
            }));
        });
        engine.SetValue("gate", new Func<Task<int>>(() => gate.Task));

        // The host loop StartImport documents: start, then give the engine turns. One turn takes the module
        // through link and into its body, where it suspends on the top-level await.
        var operation = engine.Modules.StartImport("m");
        engine.Tasks.ProcessTasks();
        operation.IsCompleted.Should().BeFalse();

        // Settled a good while later, so that nothing is queued when the blocking import starts and the
        // continuations it runs on the way out cannot finish the module for it. Waiting the top-level await
        // out has to be the import's own doing.
        _ = Task.Run(async () =>
        {
            await Task.Delay(300).ConfigureAwait(false);
            gate.SetResult(7);
        });

        var ns = engine.Modules.Import("m");

        ns.Get("v").AsNumber().Should().Be(7);
        operation.IsCompleted.Should().BeTrue();
    }
}
