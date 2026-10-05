#nullable enable

using System;
using System.Runtime.CompilerServices;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// <c>engine.Advanced.DiscardInterpreterCaches()</c> from the embedder's side: the engine-pool shape — capture
/// once, evaluate a cached prepared script with per-request host globals, restore — with the discard added on
/// return, which is the only way a pooled engine stops keeping the last request's services alive through a
/// warmed call site.
/// </summary>
[CollectionDefinition(nameof(HostInterpreterCacheDiscardTests), DisableParallelization = true)]
[Collection(nameof(HostInterpreterCacheDiscardTests))]
public class HostInterpreterCacheDiscardTests
{
    private sealed class RequestServices
    {
        public string Name => "services";
    }

    [Fact]
    public void APooledEngineReleasesTheLastRequestsServicesAfterADiscard()
    {
        var engine = new Engine();
        var snapshot = engine.Advanced.CaptureGlobalSnapshot();
        var script = Engine.PrepareScript("describe(ctx.Name)");

        RunRequest(engine, snapshot, script);
        var services = RunRequest(engine, snapshot, script);

        engine.Advanced.DiscardInterpreterCaches();
        GC.Collect(2, GCCollectionMode.Forced, blocking: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(2, GCCollectionMode.Forced, blocking: true);

        services.IsAlive.Should().BeFalse("nothing in a restored, discarded engine may reach a finished request's services");

        // the engine stays fully usable and computes the same thing
        RunRequest(engine, snapshot, script);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference RunRequest(Engine engine, GlobalSnapshot snapshot, Prepared<Script> script)
    {
        var services = new RequestServices();
        engine.SetValue("ctx", services);
        engine.SetValue("describe", new Func<string, string>(value => value + ":" + services.Name));
        engine.Evaluate(script).AsString().Should().Be("services:services");
        engine.Advanced.RestoreGlobalSnapshot(snapshot);
        return new WeakReference(services);
    }

    [Fact]
    public void DiscardingWhileAnEvaluationIsInProgressIsRefused()
    {
        var engine = new Engine();
        engine.SetValue("discard", new Action(() => engine.Advanced.DiscardInterpreterCaches()));

        Assert.Throws<InvalidOperationException>(() => engine.Execute("discard()"));
    }
}
