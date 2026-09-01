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

    /// <summary>The stack the walk under test runs on: the smallest a thread request is honoured with.</summary>
    /// <remarks>
    /// 256 KB is the floor Windows rounds a request up to, so asking for less buys nothing there and the
    /// depth cannot be pinned from this end. How much stack this actually yields is a platform question and
    /// deliberately not assumed to be 256 KB — see <see cref="ChainLengths"/>.
    /// </remarks>
    private const int StackUnderTest = 256 * 1024;

    /// <summary>
    /// The chain lengths tried, in order, until one meets the probe.
    /// </summary>
    /// <remarks>
    /// The suite used to name a single length and an argued margin — five times the depth measured to trip
    /// on a 256 KB Windows thread. That reasoning was sound about frame sizes and wrong about stacks: the
    /// <em>frames</em> agree across platforms to within a percent (this branch trips at 157 modules on a
    /// 256 KB Windows thread, where main measured 156), but the requested stack size does not. On the Linux
    /// runner the same 1,000-module chain on the same 256 KB request linked and evaluated <em>successfully</em>,
    /// so the test asserted an exception that legitimately never happened and the margin was a margin over
    /// the wrong quantity. Growing the chain until the walk meets the probe measures the machine instead of
    /// predicting it, which is the only form of this test that is portable: whatever stack the platform
    /// really handed over, doubling reaches the end of it. The cap exists so a regression is reported rather
    /// than pursued forever — on a genuinely 8 MB stack the last entry is still several times too deep to
    /// fit.
    /// </remarks>
    private static readonly int[] ChainLengths = [2_000, 4_000, 8_000, 16_000, 32_000];

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
    /// The slice a chain is walked in, chosen so that a phase driven slice by slice stays shallow whatever
    /// stack the preparing thread turns out to have.
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

    /// <summary>What the walk under test did, once a chain long enough to reach the probe was found.</summary>
    private sealed record Probed(int Length, Exception Failure, Exception? Again, double Afterwards);

    /// <summary>
    /// Grows the chain until importing it meets the probe, with every phase before the one under test
    /// already driven, and hands back what that import raised.
    /// </summary>
    /// <remarks>
    /// The preparation runs on an ordinary thread and only the walk under test runs on
    /// <see cref="StackUnderTest"/>. Two reasons, and the second is why it is not merely tidier: a prepared
    /// graph is not what this test is about, and where a platform's probe reserve is itself larger than the
    /// small stack — a 64 KB page size makes the runtime's reserve 768 KB — preparing on that stack would
    /// meet the probe during setup and report a failure about the wrong thing.
    /// </remarks>
    private static Probed DriveUntilProbed(string phase, Action<Jint.Runtime.Modules.Module> prepare)
    {
        foreach (var length in ChainLengths)
        {
            var modules = ImportChain(length);
            Engine engine = null!;

            DedicatedThread.Run(
                () =>
                {
                    engine = ChainEngine(modules);
                    InSlices(engine, modules, prepare);
                },
                joinTimeout: WedgeCeiling,
                timeoutMessage: $"preparing a {length}-module chain for {phase} did not complete within {WedgeCeiling}");

            Exception? failure = null;
            Exception? again = null;
            var afterwards = 0d;

            DedicatedThread.Run(
                () =>
                {
                    failure = Record.Exception(() => engine.Modules.Import("0.js"));
                    if (failure is null)
                    {
                        return;
                    }

                    // Asked on the same thread as the first import, because "the same way again" is only
                    // meaningful where the stack is the same too.
                    again = Record.Exception(() => engine.Modules.Import("0.js"));

                    // The other half of the promise the guard makes for script recursion: an engine that is
                    // still usable. Nothing about a native stack overflow leaves one.
                    afterwards = engine.Evaluate("1 + 1").AsNumber();
                },
                joinTimeout: WedgeCeiling,
                timeoutMessage: $"importing a {length}-module chain did not complete within {WedgeCeiling}",
                maxStackSize: StackUnderTest);

            if (failure is not null)
            {
                return new Probed(length, failure, again, afterwards);
            }
        }

        Assert.Fail(
            $"a chain of {ChainLengths[^1]} modules {phase} inside a {StackUnderTest / 1024} KB thread request without " +
            "ever meeting the stack probe, so nothing here exercised it. Either the request bought a far larger stack " +
            "than any platform is known to give it, or the probe is no longer armed.");

        throw new InvalidOperationException("unreachable");
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
    /// walk left with the whole chain to descend is linking's.
    /// </para>
    /// </remarks>
    [Fact]
    public void ALinkTooDeepForTheStackThrowsRatherThanEndingTheProcess()
    {
        var probed = DriveUntilProbed("linking", static m => m.LoadRequestedModules());

        probed.Failure.Should().BeOfType<JavaScriptException>()
            .Which.Message.Should().StartWith("Maximum call stack size exceeded while linking module '");

        probed.Afterwards.Should().Be(2);
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
    /// it leaves them in <c>evaluating</c> forever. Measured on this branch against a build that throws
    /// instead: the first import fails as it does here and the second one <b>succeeds</b>, handing the host
    /// a namespace for a graph not one body of which ever ran. That is the failure this second assertion
    /// exists for — a half-evaluated graph that reports itself as fine is worse than the crash the probe
    /// replaced.
    /// <para>
    /// The evaluation half is selected rather than hoped for: linking the chain in slices first means the
    /// only walk left with the whole chain to descend is evaluation's.
    /// </para>
    /// </remarks>
    [Fact]
    public void AnEvaluationTooDeepForTheStackLeavesTheGraphErrored()
    {
        var probed = DriveUntilProbed("evaluation", static m => m.Link());

        probed.Failure.Should().BeOfType<JavaScriptException>()
            .Which.Message.Should().StartWith("Maximum call stack size exceeded while evaluating module '");

        probed.Again.Should().BeOfType<JavaScriptException>()
            .Which.Message.Should().Be(probed.Failure.Message);
    }
}
