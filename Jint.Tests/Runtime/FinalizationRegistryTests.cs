#nullable enable

using System.Runtime.CompilerServices;

namespace Jint.Tests.Runtime;

/// <summary>
/// The <c>FinalizationRegistry</c> cleanup callback is discovered by the CLR garbage collector but must be
/// <em>run</em> by the engine, on the engine's own thread and from the event loop —
/// https://tc39.es/ecma262/#sec-host-cleanup-finalization-registry. These tests pin that split,
/// and the parts of https://tc39.es/ecma262/#sec-cleanup-finalization-registry a host can observe.
/// </summary>
/// <remarks>
/// Non-parallel for the reason <see cref="GarbageCollectionTests"/> is: every test here forces a full
/// collection, which cannot be isolated from whatever else the runner is doing.
/// </remarks>
[NonParallelizable]
public class FinalizationRegistryTests
{
    /// <summary>
    /// How many collect / run-finalizers / pump rounds a test gives the runtime to notice that an
    /// unreachable target really is unreachable. A bound, not a wait: nothing here observes the clock.
    /// </summary>
    private const int CollectionRounds = 10;

    private sealed class Recorder
    {
        internal List<string> HeldValues { get; } = new();

        internal List<int> ThreadIds { get; } = new();

        internal Action<string> Callback => heldValue =>
        {
            HeldValues.Add(heldValue);
            ThreadIds.Add(Environment.CurrentManagedThreadId);
        };
    }

    private static Engine CreateEngine(Recorder recorder)
    {
        var engine = new Engine();
        engine.SetValue("record", recorder.Callback);
        engine.Execute("globalThis.registry = new FinalizationRegistry(record);");
        return engine;
    }

    /// <summary>
    /// Collects, runs finalizers and does <b>not</b> pump the event loop. Anything the cleanup callback
    /// would have done on the finalizer thread has happened by the time this returns.
    /// </summary>
    private static void CollectWithoutPumping()
    {
        for (var i = 0; i < CollectionRounds; i++)
        {
            GC.Collect(2, GCCollectionMode.Forced, blocking: true);
            GC.WaitForPendingFinalizers();
        }

        GC.Collect(2, GCCollectionMode.Forced, blocking: true);
    }

    /// <summary>
    /// Collects and pumps until <paramref name="recorder"/> has seen a callback, or the round budget runs
    /// out — at which point the caller's assertion is what reports the failure.
    /// </summary>
    private static void CollectAndPump(Engine engine, Recorder recorder)
    {
        for (var i = 0; i < CollectionRounds && recorder.HeldValues.Count == 0; i++)
        {
            GC.Collect(2, GCCollectionMode.Forced, blocking: true);
            GC.WaitForPendingFinalizers();
            engine.Tasks.ProcessTasks();
        }
    }

    // NoInlining so the object literal's argument slot cannot stay stack-rooted in the caller's frame
    // across the collections below.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void RegisterUnreachableTarget(Engine engine, string script) => engine.Execute(script);

    [Test]
    public void CleanupCallbackRunsOnTheEngineThreadFromTheEventLoop()
    {
        var recorder = new Recorder();
        var engine = CreateEngine(recorder);

        RegisterUnreachableTarget(engine, "registry.register({}, 'held');");

        // Every finalizer that was going to run has run by now. A callback invoked from ~Observer() would
        // already be recorded here — that is precisely the defect, and this assertion cannot flake, because
        // a target that was *not* collected produces no callback on any thread.
        CollectWithoutPumping();
        recorder.HeldValues.Should().BeEmpty("the cleanup callback must not run on the CLR finalizer thread");

        CollectAndPump(engine, recorder);

        // https://tc39.es/ecma262/#sec-cleanup-finalization-registry step 3.c: the callback is called with
        // the cell's [[HeldValue]] — Jint used to call it with no arguments at all.
        recorder.HeldValues.Should().Equal("held");
        recorder.ThreadIds.Should().Equal(Environment.CurrentManagedThreadId);
    }

    [Test]
    public void CleanupCallbackRunsForACellRegisteredWithALiveUnregisterToken()
    {
        var recorder = new Recorder();
        var engine = CreateEngine(recorder);

        // The token stays reachable for the whole test. It used to be the registry's own strong index that
        // kept the cell's finalization sentinel alive, so a token-registered cell was never cleaned up at
        // all; the token must hold the cell, never the sentinel.
        RegisterUnreachableTarget(engine, "globalThis.token = {}; registry.register({}, 'held', token);");

        CollectAndPump(engine, recorder);

        recorder.HeldValues.Should().Equal("held");
    }

    [Test]
    public void UnregisterBeforeTheCleanupJobRunsSuppressesTheCallback()
    {
        var recorder = new Recorder();
        var engine = CreateEngine(recorder);

        RegisterUnreachableTarget(engine, "globalThis.token = {}; registry.register({}, 'held', token);");

        // The collection is observed, so the cleanup job is queued — but not run.
        CollectWithoutPumping();

        // https://tc39.es/ecma262/#sec-finalization-registry.prototype.unregister: the cell is still in
        // [[Cells]] (nothing has removed it yet), so it is removed now and `removed` is true.
        engine.Evaluate("registry.unregister(token)").AsBoolean().Should().BeTrue();

        engine.Tasks.ProcessTasks();
        recorder.HeldValues.Should().BeEmpty("an unregistered cell has left [[Cells]] and step 3 no longer sees it");
    }

    [Test]
    public void UnregisterAfterTheCleanupCallbackHasRunReportsNothingRemoved()
    {
        var recorder = new Recorder();
        var engine = CreateEngine(recorder);

        RegisterUnreachableTarget(engine, "globalThis.token = {}; registry.register({}, 'held', token);");

        CollectAndPump(engine, recorder);
        recorder.HeldValues.Should().Equal("held");

        // The cell left [[Cells]] when its callback ran (step 3.b), so there is nothing left to remove.
        engine.Evaluate("registry.unregister(token)").AsBoolean().Should().BeFalse();
    }

    [Test]
    public void CollectionObservedAfterARestoreIsDroppedRatherThanRunAgainstRestoredGlobals()
    {
        var recorder = new Recorder();
        var engine = CreateEngine(recorder);

        // A CLR-side reference, so the registry survives the restore that takes its global binding away and
        // the sentinel below still finds it. Without this the test would pass for the wrong reason.
        var registry = engine.Evaluate("registry");

        var snapshot = engine.Advanced.CaptureGlobalSnapshot();
        RegisterUnreachableTarget(engine, "registry.register({}, 'stale');");

        // Ends the cycle the cell was registered in. The cell is not queued yet — the collection has not
        // been observed — so this is the generation *stamp* being tested, not the queue flush.
        engine.Advanced.RestoreGlobalSnapshot(snapshot);

        for (var i = 0; i < CollectionRounds; i++)
        {
            GC.Collect(2, GCCollectionMode.Forced, blocking: true);
            GC.WaitForPendingFinalizers();
            engine.Tasks.ProcessTasks();
        }

        recorder.HeldValues.Should().BeEmpty("a cell registered before the restore belongs to a cycle the engine has ended");
        GC.KeepAlive(registry);
    }

    [Test]
    public void OneCleanupJobNeverDeliversACellFromAnEndedCycleAlongsideACurrentOne()
    {
        var recorder = new Recorder();
        var engine = CreateEngine(recorder);
        var registry = engine.Evaluate("registry");

        var snapshot = engine.Advanced.CaptureGlobalSnapshot();
        RegisterUnreachableTarget(engine, "registry.register({}, 'stale');");
        engine.Advanced.RestoreGlobalSnapshot(snapshot);

        // The same registry, now used by the cycle that follows the restore: its cell belongs to that cycle,
        // and the job the *stale* cell queues is the one the event loop drops. The surviving job therefore
        // drains both cells, which is the whole reason the drain re-checks each cell's own generation rather
        // than trusting the job's stamp.
        engine.SetValue("registry", registry);
        RegisterUnreachableTarget(engine, "registry.register({}, 'current');");

        for (var i = 0; i < CollectionRounds && recorder.HeldValues.Count == 0; i++)
        {
            GC.Collect(2, GCCollectionMode.Forced, blocking: true);
            GC.WaitForPendingFinalizers();
            engine.Tasks.ProcessTasks();
        }

        recorder.HeldValues.Should().Equal("current");
        GC.KeepAlive(registry);
    }
}
