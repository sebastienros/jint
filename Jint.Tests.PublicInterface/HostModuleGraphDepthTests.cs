#nullable enable

using System;
using System.Collections.Generic;
using Jint;
using Jint.Runtime;
using Jint.Runtime.Modules;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// What an embedder gets when a module graph is deeper than the thread importing it can recurse over.
/// Tests live here because the public-interface project has no <c>InternalsVisibleTo</c> grant, so every
/// green assertion proves an embedder can reach the surface.
/// </summary>
/// <remarks>
/// <para>
/// <c>InnerModuleLinking</c> and <c>InnerModuleEvaluation</c> descend once per module, the way the spec
/// writes them, so how deep a graph an engine can link and evaluate is decided by the calling thread's
/// stack rather than by anything the host configured. Exceeding it was a native stack overflow: no
/// exception, nothing in a <c>catch</c>, no log, and the process gone. With
/// <c>options.Constraints.StackOverflowGuard</c> on, both algorithms probe for headroom before descending
/// and fail the way script recursion already does.
/// </para>
/// <para>
/// Each test drives its own phase deliberately rather than importing a cold graph, because the load phase
/// recurses per module too and would meet the reserve first. That third recursion is
/// <see href="https://github.com/sebastienros/jint/issues/3401">#3401</see>'s remaining half and is not
/// probed here: the load phase reports failure by rejecting its own promise, so a throw out of it would be
/// a different contract rather than the same one in another place.
/// </para>
/// </remarks>
public sealed class HostModuleGraphDepthTests
{
    /// <summary>Simple in-memory module loader returning fixed source per specifier.</summary>
    private sealed class DictLoader : ModuleLoader
    {
        private readonly Dictionary<string, string> _modules;

        public DictLoader(Dictionary<string, string> modules) => _modules = modules;

        public override ResolvedSpecifier Resolve(string? referencingModuleLocation, ModuleRequest moduleRequest)
        {
            var key = moduleRequest.Specifier;
            if (key.StartsWith("./", StringComparison.Ordinal))
            {
                key = key.Substring(2);
            }

            return new ResolvedSpecifier(moduleRequest, key, new Uri($"file:///base/{key}"), SpecifierType.RelativeOrAbsolute);
        }

        protected override string LoadModuleContents(Engine engine, ResolvedSpecifier resolved)
        {
            if (_modules.TryGetValue(resolved.Key!, out var source))
            {
                return source;
            }

            throw new ModuleResolutionException($"Module not found: {resolved.Key}", resolved.ModuleRequest.Specifier, null, null);
        }
    }

    /// <summary>
    /// A wedge ceiling, never an assertion: every test here asserts an outcome rather than a duration, and
    /// this exists only so a graph walk that stopped terminating is reported instead of hanging the run.
    /// </summary>
    private static readonly TimeSpan WedgeCeiling = TimeSpan.FromMinutes(2);

    private const int LongChainLength = 1_000;

    /// <summary>A chain of <paramref name="count"/> modules, each importing the next.</summary>
    private static Dictionary<string, string> ImportChain(int count)
    {
        var modules = new Dictionary<string, string>(count, StringComparer.Ordinal);
        for (var i = 0; i < count - 1; i++)
        {
            modules[$"{i}.js"] = $"import './{i + 1}.js';";
        }

        modules[$"{count - 1}.js"] = "export default 1;";
        return modules;
    }

    /// <summary>
    /// An engine serving <paramref name="modules"/> with the guard on, which is what arms the probe: the
    /// module pipeline follows <c>Constraints.StackOverflowGuard</c> rather than introducing a switch of
    /// its own, and on this branch that flag is opt-in.
    /// </summary>
    private static Engine ChainEngine(Dictionary<string, string> modules)
    {
        return new Engine(o =>
        {
            o.EnableModules(new DictLoader(modules));
            o.Constraints.StackOverflowGuard = true;
        });
    }

    /// <summary>
    /// A stack the chain below cannot possibly fit in, so that what this suite measures is Jint's probe and
    /// not the machine it runs on.
    /// </summary>
    /// <remarks>
    /// 256 KB is the floor a thread request is rounded up to on Windows, and at that size a chain reaches
    /// roughly two hundred modules before a recursive phase runs out. <see cref="LongChainLength"/> is five
    /// times that, which is the point: the per-module cost differs by runtime, architecture and operating
    /// system by tens of percent, and it was exactly a margin thinner than that which let
    /// <see href="https://github.com/sebastienros/jint/issues/3308">#3308</see> pass on four legs and end
    /// the process on the fifth. Nothing decided by codegen can move a factor of five.
    /// </remarks>
    private const int StackTooSmallForTheChain = 256 * 1024;

    /// <summary>
    /// The slice a chain is walked in, chosen so that a phase driven slice by slice stays comfortably inside
    /// <see cref="StackTooSmallForTheChain"/> — about half of what that stack holds.
    /// </summary>
    private const int SliceLength = 100;

    /// <summary>
    /// Drives <paramref name="phase"/> over <paramref name="modules"/> bottom-up in slices, so the whole
    /// chain reaches that phase's end state without the phase ever having recursed more than one slice deep.
    /// </summary>
    /// <remarks>
    /// Each slice is driven from a module of its own whose single import is the slice's first module. The
    /// records underneath are the engine's, keyed by resolved specifier and so shared by every slice, which
    /// is what makes each slice after the first stop at a dependency the previous slice already advanced.
    /// </remarks>
    private static void InSlices(Engine engine, Dictionary<string, string> modules, Action<Jint.Runtime.Modules.Module> phase)
    {
        for (var start = modules.Count - SliceLength; start >= 0; start -= SliceLength)
        {
            var location = $"slice{start}.js";
            var request = new ModuleRequest(location, []);
            var resolved = new ResolvedSpecifier(request, location, new Uri($"file:///base/{location}"), SpecifierType.RelativeOrAbsolute);
            phase(ModuleFactory.BuildSourceTextModule(engine, resolved, $"import './{start}.js';"));
        }
    }

    /// <summary>
    /// A module graph deeper than the calling thread can link fails with a <c>RangeError</c> the host
    /// catches, naming the module it gave up on, and leaves an engine that still runs script.
    /// </summary>
    /// <remarks>
    /// The property being pinned is that there <em>is</em> an outcome. Linking descends once per module
    /// (<see href="https://github.com/sebastienros/jint/issues/3401">#3401</see>), so before the probe this
    /// same call ended the process: a native stack overflow, which .NET does not deliver to any
    /// <c>catch</c>, no exception, no log. A host whose whole reason for implementing
    /// <see cref="IModuleLoader"/> is to serve a graph it did not author could be killed by one.
    /// <para>
    /// The linking half is selected rather than hoped for: loading the chain in slices first means the only
    /// walk left with a thousand levels to descend is linking's.
    /// </para>
    /// </remarks>
    [Fact]
    public void ALinkTooDeepForTheStackThrowsRatherThanEndingTheProcess()
    {
        var modules = ImportChain(LongChainLength);
        Exception? failure = null;
        var afterwards = 0d;

        DedicatedThread.Run(
            () =>
            {
                var engine = ChainEngine(modules);
                InSlices(engine, modules, static m => m.LoadRequestedModules());

                failure = Record.Exception(() => engine.Modules.Import("0.js"));

                // The other half of the promise the guard makes for script recursion: an engine that is
                // still usable. Nothing about a native stack overflow leaves one.
                afterwards = engine.Evaluate("1 + 1").AsNumber();
            },
            joinTimeout: WedgeCeiling,
            timeoutMessage: $"importing a pre-loaded {LongChainLength}-module chain did not complete within {WedgeCeiling}",
            maxStackSize: StackTooSmallForTheChain);

        failure.Should().BeOfType<JavaScriptException>()
            .Which.Message.Should().StartWith("Maximum call stack size exceeded while linking module '");

        afterwards.Should().Be(2);
    }

    /// <summary>
    /// An evaluation that runs out of stack leaves the graph <em>errored</em> — every module it had reached
    /// marked evaluated with that error — rather than stranded mid-evaluation. Importing the same root again
    /// therefore fails the same way instead of waiting for a promise nothing will ever settle.
    /// </summary>
    /// <remarks>
    /// This is what makes the evaluation half of the probe hand back a throw <em>completion</em> where the
    /// linking half throws. <c>Evaluate</c> step 9.a is the only thing that marks the modules still on the
    /// Tarjan stack, and only an abrupt completion returned to it reaches that step; an exception thrown past
    /// it leaves them in <c>evaluating</c> forever. Measured against a build that throws instead: the first
    /// import fails as it does here and the second one <b>succeeds</b>, handing the host a namespace for a
    /// graph not one body of which ever ran. That is the failure this second assertion exists for — a
    /// half-evaluated graph that reports itself as fine is worse than the crash the probe replaced.
    /// <para>
    /// The evaluation half is selected rather than hoped for: linking the chain in slices first means the
    /// only walk left with a thousand levels to descend is evaluation's.
    /// </para>
    /// </remarks>
    [Fact]
    public void AnEvaluationTooDeepForTheStackLeavesTheGraphErrored()
    {
        var modules = ImportChain(LongChainLength);
        Exception? first = null;
        Exception? second = null;

        DedicatedThread.Run(
            () =>
            {
                var engine = ChainEngine(modules);
                InSlices(engine, modules, static m => m.Link());

                first = Record.Exception(() => engine.Modules.Import("0.js"));
                second = Record.Exception(() => engine.Modules.Import("0.js"));
            },
            joinTimeout: WedgeCeiling,
            timeoutMessage: $"importing a pre-linked {LongChainLength}-module chain did not complete within {WedgeCeiling}",
            maxStackSize: StackTooSmallForTheChain);

        first.Should().BeOfType<JavaScriptException>()
            .Which.Message.Should().StartWith("Maximum call stack size exceeded while evaluating module '");

        second.Should().BeOfType<JavaScriptException>()
            .Which.Message.Should().Be(first!.Message);
    }
}
