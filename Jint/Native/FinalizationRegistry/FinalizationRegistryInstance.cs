using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Native.FinalizationRegistry;

internal sealed record Cell(JsValue WeakRefTarget, JsValue HeldValue, JsValue? UnregisterToken);

/// <summary>
/// https://tc39.es/ecma262/#sec-properties-of-finalization-registry-instances
/// </summary>
/// <remarks>
/// <para>
/// The specification's <c>[[Cells]]</c> list is spread over three structures here, because the CLR's garbage
/// collector — not the engine — is what discovers that a cell's <c>[[WeakRefTarget]]</c> has become empty.
/// A cell's <see cref="FinalizationCell"/> record holds the parts the engine needs later
/// (<c>[[HeldValue]]</c>, the cycle it was registered in, and whether it has left <c>[[Cells]]</c>); an
/// <see cref="Observer"/> is the finalizable sentinel whose lifetime is tied to the target's through
/// <see cref="_cells"/>, so its finalizer running <em>is</em> the observation that the target was collected;
/// and <see cref="_byToken"/> indexes the cells by <c>[[UnregisterToken]]</c>.
/// </para>
/// <para>
/// Which structure holds which object is load-bearing, not incidental. The sentinel must be reachable
/// <b>only</b> from the target-keyed table, or a live unregister token would keep it alive after its target
/// died and the collection would never be observed at all — which is exactly what the earlier strongly-keyed
/// token dictionary did. The token table therefore holds the record, never the sentinel, and both tables are
/// <see cref="ConditionalWeakTable{TKey,TValue}"/>s so that neither target nor token is kept alive by being
/// registered: "registering an object with itself as its unregister token would not keep the object alive
/// forever" (the note under https://tc39.es/ecma262/#sec-finalization-registry.prototype.register). The
/// record is held strongly, which is the other half of that note — <c>cell.[[HeldValue]]</c> is live while
/// <c>[[Cells]]</c> contains the cell, and it stays live across the target's collection because the sentinel
/// references it and the CLR keeps everything a finalizable object references alive until its finalizer has
/// run.
/// </para>
/// <para>
/// The sentinel's reference back to the registry is weak, for two reasons. A strong one would make every
/// live registered target keep the registry — and through it the whole <see cref="Engine"/> — alive. And a
/// registry that is itself unreachable must not have its callback called: without the weak reference,
/// dropping the registry while its targets are still live finalizes the sentinels and reports collections
/// that never happened.
/// </para>
/// </remarks>
internal sealed class FinalizationRegistryInstance : ObjectInstance
{
    private readonly Realm _realm;

    /// <summary>The specification's <c>[[CleanupCallback]]</c>.</summary>
    private readonly JobCallback _callable;

    /// <summary>
    /// The finalization sentinels, keyed by the cell's <c>[[WeakRefTarget]]</c>: a target may be registered
    /// more than once, so the value is a list. Ephemeron semantics are the whole point — the sentinels for a
    /// target become unreachable exactly when the target does.
    /// </summary>
    private readonly ConditionalWeakTable<JsValue, List<Observer>> _cells = new();

    /// <summary>
    /// The cells indexed by their <c>[[UnregisterToken]]</c>, for <see cref="Remove"/>. Deliberately holds
    /// <see cref="FinalizationCell"/> records rather than <see cref="Observer"/> sentinels; see the remarks
    /// on the class.
    /// </summary>
    private readonly ConditionalWeakTable<JsValue, List<FinalizationCell>> _byToken = new();

    /// <summary>
    /// The cells whose <c>[[WeakRefTarget]]</c> has been observed to be empty and whose callback has not run
    /// yet. Written from the CLR finalizer thread and drained on the engine's thread, which is why it is a
    /// concurrent queue: it is the whole of the hand-off between the two.
    /// </summary>
    private readonly ConcurrentQueue<FinalizationCell> _dirtyCells = new();

    /// <summary>
    /// The job <see cref="CleanupFinalizationRegistry"/> is enqueued as, allocated once per registry rather
    /// than once per collected target — a finalizer thread should allocate as little as it can.
    /// </summary>
    private readonly Action _cleanupJob;

    /// <summary>
    /// The one weak self-reference every <see cref="Observer"/> of this registry shares, and the reason it is
    /// a field here rather than one per sentinel: <see cref="WeakReference{T}"/> is <b>itself finalizable</b>
    /// — its finalizer frees the underlying handle — finalization order between two unreachable objects is
    /// unspecified, and a sentinel's private handle would become unreachable at exactly the moment the
    /// sentinel does. Such a sentinel finds a possibly already-finalized handle in its own finalizer and
    /// reads every collection as "the registry has gone away", which presents as a FinalizationRegistry that
    /// silently never fires rather than as a crash. Rooting the handle on the registry keeps it alive for
    /// precisely as long as the answer it gives can be anything but "gone".
    /// </summary>
    private readonly WeakReference<FinalizationRegistryInstance> _self;

    public FinalizationRegistryInstance(Engine engine, Realm realm, ICallable cleanupCallback) : base(engine)
    {
        _realm = realm;
        _callable = engine._host.MakeJobCallBack(cleanupCallback);
        _cleanupJob = CleanupFinalizationRegistry;
        _self = new WeakReference<FinalizationRegistryInstance>(this);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-cleanup-finalization-registry
    /// <para>
    /// Runs as an event-loop job, never on the thread that discovered the collection: see
    /// <see cref="OnTargetCollected"/>.
    /// </para>
    /// </summary>
    private void CleanupFinalizationRegistry()
    {
        // 2. Let callback be finalizationRegistry.[[CleanupCallback]].
        var callback = _callable.Callback;
        var generation = _engine.EventLoopGeneration;

        // 3. While finalizationRegistry.[[Cells]] contains a Record cell such that cell.[[WeakRefTarget]] is
        //    empty, an implementation may perform the following steps:
        //      a. Choose any such cell.
        while (_dirtyCells.TryDequeue(out var cell))
        {
            // b. Remove cell from finalizationRegistry.[[Cells]]. Before the callback runs, so that an
            // unregister() from inside it correctly reports the cell as already gone, and so that a cell can
            // never be delivered twice however many jobs are queued for this registry.
            if (cell.Removed)
            {
                // Unregistered between the collection being observed and this job running: the cell left
                // [[Cells]] then, so step 3 no longer sees it at all.
                continue;
            }

            cell.Removed = true;

            // Work registered before a RestoreGlobalSnapshot belongs to a cycle the engine has ended, and a
            // cleanup callback from it would run against the restored globals — the same fence the event
            // loop applies to whole jobs, applied per cell because one job may drain cells that were
            // registered in different cycles.
            if (cell.Generation != generation)
            {
                continue;
            }

            // c. Perform ? HostCallJobCallback(callback, undefined, « cell.[[HeldValue]] »).
            //
            // The "?" is what ends the loop on an abrupt completion: the throw propagates out of the job and
            // out of whatever is pumping the event loop, which is Jint's usual "perform any host-defined
            // steps for reporting the error" and the same thing a timer callback's exception does. Nothing
            // is stranded by it — every dirty cell was enqueued with a job of its own, so the cells behind
            // this one are delivered by those.
            callback.Call(JsValue.Undefined, cell.HeldValue);
        }
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-finalization-registry.prototype.register steps 7-8: build the cell and
    /// append it to <c>[[Cells]]</c>.
    /// </summary>
    public void AddCell(Cell cell)
    {
        // The cycle the cell is registered in, read here on the engine's thread. The collection may be
        // observed arbitrarily later, on the finalizer thread, and stamping the job with the generation
        // captured now is what lets the drain tell work this engine is still waiting for from work whose
        // cycle a RestoreGlobalSnapshot has since ended. Per cell rather than per registry: a host that kept
        // a reference to the registry across a restore may register new cells on it afterwards, and those
        // belong to the new cycle.
        var finalizationCell = new FinalizationCell(cell.HeldValue, _engine.EventLoopGeneration);

        var observers = _cells.GetOrCreateValue(cell.WeakRefTarget);
        observers.Add(new Observer(_self, finalizationCell));

        // "If CanBeHeldWeakly(unregisterToken) is false, then ... Set unregisterToken to empty" — the
        // prototype passes null for empty, and a cell with an empty token is reachable through the target
        // alone.
        if (cell.UnregisterToken is not null)
        {
            if (!_byToken.TryGetValue(cell.UnregisterToken, out var list))
            {
                list = new List<FinalizationCell>();
                _byToken.Add(cell.UnregisterToken, list);
            }

            list.Add(finalizationCell);
        }
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-finalization-registry.prototype.unregister steps 4-6.
    /// </summary>
    public JsValue Remove(JsValue unregisterToken)
    {
        if (!_byToken.TryGetValue(unregisterToken, out var cells))
        {
            return JsBoolean.False;
        }

        // 5. For each Record cell of finalizationRegistry.[[Cells]], do
        //      a. If cell.[[UnregisterToken]] is not empty and SameValue(cell.[[UnregisterToken]],
        //         unregisterToken) is true, then
        //          i. Remove cell from finalizationRegistry.[[Cells]].
        //          ii. Set removed to true.
        //
        // A cell whose callback has already run left [[Cells]] then and must not be counted now, which is
        // what the flag distinguishes.
        var removed = false;
        for (var i = 0; i < cells.Count; i++)
        {
            var cell = cells[i];
            if (cell.Removed)
            {
                continue;
            }

            cell.Removed = true;
            removed = true;
        }

        _byToken.Remove(unregisterToken);
        return removed ? JsBoolean.True : JsBoolean.False;
    }

    /// <summary>
    /// Called from the CLR finalizer thread when a cell's <c>[[WeakRefTarget]]</c> has been collected. Runs
    /// on a thread that is not the engine's, so it does exactly two things: hand the cell over through a
    /// concurrent queue, and enqueue the cleanup job. Everything the specification's algorithm does happens
    /// later, on the engine's thread, in <see cref="CleanupFinalizationRegistry"/>.
    /// </summary>
    /// <remarks>
    /// One job per collected cell rather than one coalesced job for the registry. Coalescing would have to
    /// pick a single generation for cells that may not share one, and a coalesced job dropped by the restore
    /// fence would strand every cell behind it; a job per cell carries each cell's own generation and can
    /// strand nothing. The extra jobs are cheap — the drain is shared, so all but the first find the queue
    /// empty.
    /// <para>
    /// The stamp on the job is the belt and the drain's per-cell check is the braces, the same way
    /// <c>TimerEntry.Generation</c> is: a surviving job may reach a cell of any generation, so the check that
    /// decides whether a callback runs has to be the one on the cell.
    /// </para>
    /// </remarks>
    private void OnTargetCollected(FinalizationCell cell)
    {
        _dirtyCells.Enqueue(cell);
        _engine._host.HostEnqueueFinalizationRegistryCleanupJob(_cleanupJob, cell.Generation);
    }

    /// <summary>
    /// One entry of the registry's <c>[[Cells]]</c>, minus the target — which is the key it is stored under
    /// and is deliberately not referenced from here, so that nothing about a cell keeps its target alive.
    /// </summary>
    internal sealed class FinalizationCell
    {
        /// <summary>
        /// Whether the cell has left <c>[[Cells]]</c>, by <c>unregister</c> or by its callback having run.
        /// Only the engine's thread ever sets it; the finalizer thread reads it to skip a cell that is
        /// already gone, which is what makes it volatile.
        /// </summary>
        internal volatile bool Removed;

        internal FinalizationCell(JsValue heldValue, int generation)
        {
            HeldValue = heldValue;
            Generation = generation;
        }

        /// <summary>The specification's <c>[[HeldValue]]</c>, the sole argument the cleanup callback receives.</summary>
        internal JsValue HeldValue { get; }

        /// <summary>The evaluation cycle the cell was registered in; see <see cref="AddCell"/>.</summary>
        internal int Generation { get; }
    }

    /// <summary>
    /// The finalizable sentinel that stands for one cell's <c>[[WeakRefTarget]]</c>. It is stored as the
    /// value of a <see cref="ConditionalWeakTable{TKey,TValue}"/> entry keyed by that target, so it becomes
    /// unreachable exactly when the target does and its finalizer running is the engine's only way of
    /// learning that the target was collected.
    /// </summary>
    private sealed class Observer
    {
        /// <summary>The registry's own <see cref="_self"/> handle, never one this sentinel owns; see its remarks.</summary>
        private readonly WeakReference<FinalizationRegistryInstance> _registry;

        private readonly FinalizationCell _cell;

        internal Observer(WeakReference<FinalizationRegistryInstance> registry, FinalizationCell cell)
        {
            _registry = registry;
            _cell = cell;
        }

#pragma warning disable MA0055
        ~Observer()
#pragma warning restore MA0055
        {
            try
            {
                if (_cell.Removed)
                {
                    return;
                }

                // A registry nothing can reach any more must not have its callback called: script cannot
                // observe a collection through a registry it has already dropped, and the sentinels of its
                // still-live targets are being finalized here only because the registry went away, not
                // because those targets did.
                if (!_registry.TryGetTarget(out var registry))
                {
                    return;
                }

                registry.OnTargetCollected(_cell);
            }
            catch
            {
                // Nothing may escape a finalizer — an exception on the finalizer thread terminates the host
                // process. The body above only pushes onto a concurrent queue and signals the event loop, so
                // the only way here is an allocation failure, and losing one cleanup callback is by far the
                // better outcome. Note that the callback itself is emphatically not called from here: it
                // runs on the engine's thread, from the job the push above queued.
            }
        }
    }
}
