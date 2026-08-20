#if NET8_0_OR_GREATER
using System.Threading;
using Jint.Native;

namespace Jint.WebApi.Abort;

/// <summary>
/// The link between a host <see cref="CancellationToken"/> and the <c>AbortSignal</c> handed to script by
/// <see cref="Engine.AdvancedOperations.CreateAbortSignal"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>The token's registration never aborts the signal itself.</b> Cancelling a
/// <see cref="CancellationTokenSource"/> runs its registrations synchronously on the cancelling thread, and
/// aborting an <c>AbortSignal</c> runs abort algorithms and then dispatches a JavaScript <c>abort</c> event —
/// which is script, and script runs on the engine's thread and nowhere else. So the registration does exactly
/// one thing: enqueue a generation-stamped event-loop job. The abort happens when that job runs, i.e. on the
/// next pump, on the pumping thread. It is the same contract <c>AbortSignal.timeout()</c> has, and it is why
/// an engine nobody pumps never observes the cancellation.
/// </para>
/// <para>
/// The generation is read when the bridge is built — on the engine thread — and carried by the job, so a
/// cancellation arriving after a <see cref="Engine.AdvancedOperations.RestoreGlobalSnapshot"/> is discarded at
/// dequeue rather than aborting a signal that belongs to a cycle the engine has ended.
/// </para>
/// </remarks>
internal sealed class HostAbortSignalBridge
{
    private readonly Engine _engine;
    private readonly AbortSignalConstructor _constructor;
    private readonly JsAbortSignal _signal;
    private readonly int _generation;

    /// <summary>Allocated once, in the constructor, because the cancelling thread must not race to create it.</summary>
    private readonly Action _job;

    private readonly CancellationTokenRegistration _registration;

    internal HostAbortSignalBridge(Engine engine, AbortSignalConstructor constructor, JsAbortSignal signal, CancellationToken cancellationToken)
    {
        _engine = engine;
        _constructor = constructor;
        _signal = signal;
        _generation = engine.EventLoopGeneration;
        _job = Abort;

        // UnsafeRegister rather than Register: the callback touches nothing that an ExecutionContext carries —
        // no AsyncLocal, no culture, no impersonation — and flowing the host's context into a callback whose
        // whole body is an enqueue would only cost an allocation per bridge.
        //
        // A token cancelled between the caller's already-cancelled check and this line runs the callback right
        // here, on the engine thread. That is still only an enqueue, so the abort lands on the pump like every
        // other one rather than reentering the engine from inside a constructor.
        _registration = cancellationToken.UnsafeRegister(
            static state => ((HostAbortSignalBridge) state!).OnCancellationRequested(),
            this);
    }

    /// <summary>
    /// Runs on whichever thread cancelled the token — a thread-pool thread, a UI thread, anything. The only
    /// engine member it may touch is the event loop's queue, which is the one part of an <see cref="Engine"/>
    /// that is thread-safe by design.
    /// </summary>
    private void OnCancellationRequested() => _engine.AddToEventLoop(_job, _generation);

    /// <summary>
    /// The engine-thread half: build the reason in this realm and abort. Reached only through the event loop,
    /// so it is as safe a point to run script from as a timer callback is.
    /// </summary>
    private void Abort()
    {
        Detach();
        _engine._webApi?.RemoveHostAbortBridge(this);

        // Aborting is one-shot and the signal has no controller, so this is the only route to it — but a
        // second cancellation of a linked token could still enqueue twice, and SignalAbort is idempotent.
        _signal.SignalAbort(_constructor.DefaultedReason(JsValue.Undefined));
    }

    /// <summary>
    /// Releases the token registration, so a long-lived host token stops retaining this engine. Called when the
    /// abort lands, when the engine's evaluation cycle ends, and when the engine is disposed. It deliberately
    /// does not touch the engine's bridge list, so the bulk paths can call it while walking that list.
    /// </summary>
    /// <remarks>
    /// <see cref="CancellationTokenRegistration.Unregister"/> rather than <c>Dispose</c>: <c>Dispose</c> blocks
    /// until a callback that is already running on another thread has finished, and there is no reason for the
    /// engine's thread to wait on a callback whose entire body is an enqueue. <c>Unregister</c> reports
    /// <see langword="false"/> in that case and returns immediately; the job it enqueued is then dropped by the
    /// generation fence, or aborts a signal nobody is listening to any more.
    /// </remarks>
    internal void Detach() => _registration.Unregister();
}
#endif
