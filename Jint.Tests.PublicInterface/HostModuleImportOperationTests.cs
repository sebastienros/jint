#nullable enable

using System;
using System.Collections.Generic;
using Jint.Runtime;
using Jint.Runtime.Modules;

using Module = Jint.Runtime.Modules.Module;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// <c>Engine.Modules.StartImport</c> hands back a <see cref="ModuleImportOperation"/> and runs nothing on its
/// own: the contract is that the host gives the engine turns and polls
/// <see cref="ModuleImportOperation.IsCompleted"/>. A polling API is only usable if every way the import can
/// end reaches that flag — including the ways that are not the import finishing.
/// </summary>
public class HostModuleImportOperationTests
{
    /// <summary>
    /// A loader that parks every request until the test answers it, so a test can decide exactly when — and
    /// whether — a load finishes.
    /// </summary>
    private sealed class HandOffModuleLoader : IAsyncModuleLoader
    {
        private readonly Dictionary<string, string> _sources;
        private readonly List<ModuleLoadCompletion> _pending = new();

        public HandOffModuleLoader(Dictionary<string, string> sources) => _sources = sources;

        public int Fetches { get; private set; }

        public ResolvedSpecifier Resolve(string? referencingModuleLocation, ModuleRequest moduleRequest)
            => new(moduleRequest, moduleRequest.Specifier, Uri: null, SpecifierType.Bare);

        public Module LoadModule(Engine engine, ResolvedSpecifier resolved)
            => throw new InvalidOperationException("The engine must not take the synchronous path for an IAsyncModuleLoader.");

        public void LoadModuleAsync(Engine engine, ResolvedSpecifier resolved, ModuleLoadCompletion completion)
        {
            Fetches++;
            _pending.Add(completion);
        }

        /// <summary>
        /// Answers the most recent outstanding request for <paramref name="specifier"/>. Most recent rather
        /// than only: a request the engine has fenced off stays in this list, because the loader is the
        /// host's own object and nothing tells it to forget one.
        /// </summary>
        public void Deliver(string specifier)
        {
            var index = _pending.FindLastIndex(c => c.Resolved.ModuleRequest.Specifier == specifier);
            index.Should().BeGreaterThanOrEqualTo(0, $"the loader should have been asked for '{specifier}'");

            var completion = _pending[index];
            _pending.RemoveAt(index);
            completion.SetSource(_sources[completion.Resolved.Key]);
        }
    }

    private static Engine CreateEngine(HandOffModuleLoader loader) => new(options => options.EnableModules(loader));

    private static HandOffModuleLoader Loader() => new(new Dictionary<string, string> { ["m"] = "export const v = 1;" });

    [Fact]
    public void AnImportStillInFlightAtAGlobalRestoreFaultsItsOperation()
    {
        var engine = CreateEngine(Loader());
        var snapshot = engine.Advanced.CaptureGlobalSnapshot();

        var operation = engine.Modules.StartImport("m");
        operation.IsCompleted.Should().BeFalse();

        engine.Advanced.RestoreGlobalSnapshot(snapshot);

        operation.IsCompleted.Should().BeTrue("the restore fenced the load off, so no turn of the event loop can ever complete it");
        operation.IsFaulted.Should().BeTrue();
        operation.Error.Should().NotBeNull();
        Invoking(() => operation.GetResult()).Should().Throw<PromiseRejectedException>();
    }

    [Fact]
    public void AnImportDeliveredBeforeAnyRestoreCompletesNormally()
    {
        var loader = Loader();
        var engine = CreateEngine(loader);

        var operation = engine.Modules.StartImport("m");
        operation.IsCompleted.Should().BeFalse();

        loader.Deliver("m");
        engine.Advanced.ProcessTasks();

        operation.IsCompleted.Should().BeTrue();
        operation.IsFaulted.Should().BeFalse();
        operation.GetResult().Get("v").AsNumber().Should().Be(1);
    }

    [Fact]
    public void AnImportThatFinishedKeepsItsResultAcrossALaterRestore()
    {
        var loader = Loader();
        var engine = CreateEngine(loader);
        var snapshot = engine.Advanced.CaptureGlobalSnapshot();

        var operation = engine.Modules.StartImport("m");
        loader.Deliver("m");
        engine.Advanced.ProcessTasks();
        operation.IsCompleted.Should().BeTrue();

        engine.Advanced.RestoreGlobalSnapshot(snapshot);

        operation.IsFaulted.Should().BeFalse("a settled operation is a result the host already owns");
        operation.GetResult().Get("v").AsNumber().Should().Be(1);
    }

    [Fact]
    public void TheHostCanStartTheAbandonedImportAgainOnTheRestoredEngine()
    {
        var loader = Loader();
        var engine = CreateEngine(loader);
        var snapshot = engine.Advanced.CaptureGlobalSnapshot();

        var abandoned = engine.Modules.StartImport("m");
        engine.Advanced.RestoreGlobalSnapshot(snapshot);
        abandoned.IsFaulted.Should().BeTrue();

        var retried = engine.Modules.StartImport("m");
        loader.Deliver("m");
        engine.Advanced.ProcessTasks();

        retried.GetResult().Get("v").AsNumber().Should().Be(1);
        loader.Fetches.Should().Be(2, "the abandoned load's registration must not be what the retry attaches to");
    }
}
