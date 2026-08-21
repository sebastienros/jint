#if NET8_0_OR_GREATER
using System.Threading;
using Jint.Native;
using Jint.Native.Error;
using Jint.Native.Object;
using Jint.Native.Promise;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;
using Jint.Runtime.Modules;
using Jint.WebApi.Messaging;

namespace Jint.WebApi.Workers;

/// <summary>
/// The engine-side half of one worker: the two entangled ports neither side ever sees, the start job, the
/// message paths in both directions, and the two ways a connection ends.
/// <para>
/// https://html.spec.whatwg.org/multipage/workers.html#dedicated-workers-and-the-worker-interface
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// <b>Neither port is handed to script</b>, which is HTML's own shape: <c>Worker</c> and
/// <c>DedicatedWorkerGlobalScope</c> both include <c>MessageEventTarget</c> and there is no <c>worker.port</c>
/// — that belongs to <c>SharedWorker</c>. So the <c>Worker</c> object <i>is</i> the parent half's façade and
/// the worker's global scope <i>is</i> the worker half's, and each hidden port carries a
/// <see cref="JsMessagePort.RetargetTo"/> pointing at the listener list its façade owns.
/// </para>
/// <para>
/// <b>Which thread runs what.</b> This object is created on the parent's thread, with the worker engine
/// quiescent and owned for the duration. After that, everything that touches the worker engine's state is an
/// event-loop job carrying the worker's own generation, and everything that ends the connection touches only
/// endpoints, a <see cref="CancellationTokenSource"/> and interlocked bookkeeping — which is what makes
/// <see cref="WorkerConnection.End"/> callable from any thread.
/// </para>
/// </remarks>
internal sealed class WorkerLink
{
    private readonly Engine _parent;
    private readonly Engine _worker;
    private readonly WorkerRegistry _registry;

    /// <summary>The parent's hidden port: its side is the parent's inbox, and the worker posts into it.</summary>
    private readonly JsMessagePort _outerPort;

    /// <summary>The worker's hidden port: its side is the worker's inbox, and the parent posts into it.</summary>
    private readonly JsMessagePort _innerPort;

    /// <summary>
    /// Cancelled when the connection ends, <b>except</b> for a worker's own <c>close()</c> — see
    /// <see cref="OnEnded"/>. Deliberately never disposed: a host pump loop is documented as waiting on
    /// <see cref="WorkerConnection.TerminationToken"/>, and a disposed source turns that wait into an
    /// <see cref="ObjectDisposedException"/> on the host's thread. It holds no timer and no registration of
    /// the engine's own, so it is ordinary garbage once the host lets go of the connection.
    /// </summary>
    private readonly CancellationTokenSource _termination;

    /// <summary>
    /// The evaluation cycle the worker engine was in when the connection was made. Every job this class queues
    /// on the worker's loop carries it, so a <c>RestoreGlobalSnapshot</c> on the worker discards them rather
    /// than running the ended cycle's work against the restored globals.
    /// </summary>
    private readonly int _workerGeneration;

    private readonly string _specifier;
    private readonly string? _referencingLocation;

    /// <summary>The job <c>close()</c> queues, built once so a script calling it in a loop allocates nothing.</summary>
    private readonly Action _closeJob;

    internal WorkerLink(
        Engine parent,
        Engine worker,
        WorkerRegistry registry,
        JsWorker workerObject,
        string name,
        string specifier,
        string? referencingLocation,
        CancellationTokenSource termination)
    {
        _parent = parent;
        _worker = worker;
        _registry = registry;
        _termination = termination;
        _specifier = specifier;
        _referencingLocation = referencingLocation;
        _workerGeneration = worker.EventLoopGeneration;
        _closeJob = EndAsClosedByWorker;

        WorkerObject = workerObject;

        // Step 6: entangle. Both ports are built here, on the parent's thread, with the worker engine owned —
        // which is what constructing a port on it amounts to, since it materializes that realm's MessagePort
        // intrinsics.
        var (outer, inner) = MessagePortBridge.CreatePair(parent, parent._mainRealm, worker, worker._mainRealm);
        _outerPort = outer;
        _innerPort = inner;

        // The retarget hook: a message that arrives on the parent's hidden port fires at the Worker object,
        // and one that arrives on the worker's fires at the worker's global scope.
        _outerPort.RetargetTo = workerObject;
        _innerPort.RetargetTo = worker._webApi!.GlobalEventTarget;

        Connection = new WorkerConnection(parent, worker, name, OnEnded, termination.Token);
    }

    /// <summary>The host's view of this link.</summary>
    internal WorkerConnection Connection { get; }

    /// <summary>The <c>Worker</c> object the parent's script holds.</summary>
    internal JsWorker WorkerObject { get; }

    /// <summary>The engine that runs the worker — what <c>close()</c> and the start job are queued on.</summary>
    internal Engine Worker => _worker;

    /// <summary>
    /// Step 7: the parent half is enabled now, so a message the worker posts during its own evaluation is
    /// delivered on the parent's next pump. <b>A documented divergence</b> — HTML enables both queues only
    /// after the worker's initial script has run — and the reason the <i>inner</i> queue stays disabled until
    /// <see cref="OnModuleEvaluated"/>, which is what makes
    /// <c>const w = new Worker(u); w.postMessage(1)</c> buffer in order rather than being lost.
    /// </summary>
    internal void EnableParentHalf() => _outerPort.Start();

    /// <summary>
    /// Step 9: the start job, queued on the <b>worker's</b> loop with the worker's own generation. Nothing of
    /// the worker's script has run when <c>new Worker(...)</c> returns; the first pump the host gives it is
    /// what runs this.
    /// </summary>
    internal void QueueStartJob() => _worker.AddToEventLoop(RunStartJob, _workerGeneration);

    /// <summary>
    /// <c>worker.postMessage(message, transfer)</c> — the parent half. Runs on the parent's thread.
    /// </summary>
    internal void PostFromParent(Realm realm, JsValue message, List<JsValue>? transferList)
    {
        EnforceQueueBound(_parent, realm, _outerPort, "postMessage' on 'Worker");
        _outerPort.PostMessage(message, transferList);
    }

    /// <summary>
    /// The worker global's <c>postMessage(message, transfer)</c>. Runs on the worker's thread.
    /// </summary>
    internal void PostFromWorker(Realm realm, JsValue message, List<JsValue>? transferList)
    {
        EnforceQueueBound(_worker, realm, _innerPort, "postMessage' on 'DedicatedWorkerGlobalScope");
        _innerPort.PostMessage(message, transferList);
    }

    /// <summary>
    /// <c>worker.terminate()</c>, https://html.spec.whatwg.org/multipage/workers.html#dom-worker-terminate —
    /// and <see cref="WorkerConnection.End"/>, which is the same thing from the host's side. Idempotent, and
    /// safe from any thread.
    /// </summary>
    internal void Terminate() => Connection.TryEnd(WorkerEndReason.Terminated, error: null);

    /// <summary>
    /// The worker global's <c>close()</c>,
    /// https://html.spec.whatwg.org/multipage/workers.html#dom-dedicatedworkerglobalscope-close — <i>close a
    /// worker</i>, which is emphatically not <i>terminate a worker</i>.
    /// </summary>
    /// <remarks>
    /// Two halves, and the split is the whole point. The worker's own side is disentangled <b>now</b>, so it
    /// receives nothing further and posts nothing further. The connection itself ends on a job queued on the
    /// worker's own loop, so the turn that called <c>close()</c> runs to completion first — which is what
    /// keeps <c>close(); flushMetrics();</c> working, where terminate's cancellation would have killed it
    /// within 64 statements.
    /// </remarks>
    internal void CloseFromWorker()
    {
        if (Connection.IsEnded)
        {
            return;
        }

        // Always the endpoint, never JsMessagePort.Close(): the port object's fields are engine-thread-only,
        // and doing it uniformly here keeps this method's shape identical to the end sequence's.
        _innerPort.Endpoint?.Close();

        _worker.AddToEventLoop(_closeJob, _workerGeneration);
    }

    /// <summary>
    /// The end sequence, run exactly once, on whichever thread ended the connection, and outside every lock
    /// this feature holds.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The endpoint treatment is §6.1/§6.2 of the design, which is the specification split: <i>terminate a
    /// worker</i>'s fourth step empties the queue of the port entangled with the worker's, and <i>close a
    /// worker</i> has no counterpart to it at all. So a terminate closes both sides and discards what the
    /// parent had already posted, while a <c>close()</c> leaves the parent's side draining — deliverable, but
    /// joinable no longer.
    /// </para>
    /// <para>
    /// The token is cancelled for every reason but <see cref="WorkerEndReason.ClosedByWorker"/>: cancelling it
    /// there would abort the very turn the worker asked to be allowed to finish.
    /// </para>
    /// </remarks>
    private void OnEnded(WorkerEndReason reason)
    {
        var closedByWorker = reason == WorkerEndReason.ClosedByWorker;

        _innerPort.Endpoint?.Close();

        if (closedByWorker)
        {
            _outerPort.Endpoint?.BeginDrainThenClose();
        }
        else
        {
            _outerPort.Endpoint?.Close();
        }

        if (!closedByWorker)
        {
            _termination.Cancel();
        }

        _registry.Remove(this);

        // Last, and outside everything: it is host code, and the host is documented as being allowed to do
        // nothing here but signal its own pump.
        _registry.Provider.OnWorkerEnded(Connection, reason);
    }

    private void EndAsClosedByWorker() => Connection.TryEnd(WorkerEndReason.ClosedByWorker, error: null);

    /// <summary>
    /// The start job's body: begin the import, and settle the connection from what it answers.
    /// </summary>
    /// <remarks>
    /// <c>StartImport</c> rather than <c>Import</c> or <c>ImportAsync</c>, and the operation's promise rather
    /// than its <c>IsCompleted</c>: polling would need a pump the connection may no longer get, and every
    /// getter on the operation is admission-guarded, so a poll from anywhere but the worker's own thread is an
    /// exception rather than an answer. It is legal from inside a job — a blocking import is what
    /// <c>ThrowIfBlockedInsideJob</c> refuses, and this one never blocks.
    /// </remarks>
    private void RunStartJob()
    {
        if (Connection.IsEnded)
        {
            return;
        }

        ModuleImportOperation operation;
        try
        {
            operation = _worker.Modules.StartImport(_specifier, _referencingLocation);
        }
        catch (Exception ex) when (!Throw.MustPropagateHostException(ex))
        {
            // StartImport turns almost everything into a rejection itself; this is the backstop for what it
            // does not, so that a failure to even begin still reaches the host as StartupFailed rather than
            // erupting from whatever was pumping.
            FailStartup(ex);
            return;
        }

        if (operation.Promise is not JsPromise promise)
        {
            // Unreachable: StartImport always hands back a promise capability's promise.
            FailStartup(new WorkerStartupException($"The worker's module '{_specifier}' could not be started."));
            return;
        }

        var onFulfilled = new ClrFunction(_worker, "", (_, _) =>
        {
            OnModuleEvaluated();
            return JsValue.Undefined;
        }, 1, PropertyFlag.Configurable);

        var onRejected = new ClrFunction(_worker, "", (_, arguments) =>
        {
            OnModuleFailed(arguments.At(0));
            return JsValue.Undefined;
        }, 1, PropertyFlag.Configurable);

        // Reactions rather than a poll — and this also marks the rejection handled, so a worker whose module
        // fails to load does not additionally read as an unhandled promise rejection.
        PromiseOperations.PerformPromiseThen(_worker, promise, onFulfilled, onRejected, resultCapability: null!);
    }

    /// <summary>
    /// The module evaluated: enable the worker's own queue, which delivers everything the parent posted while
    /// it was loading, in order.
    /// </summary>
    private void OnModuleEvaluated()
    {
        if (Connection.IsEnded)
        {
            return;
        }

        _innerPort.Start();
    }

    /// <summary>
    /// The module never ran. §6.3's order: the inner endpoint first — so the records the parent posted are
    /// discarded and the sides they carried are stranded rather than left waiting for a deserialization that
    /// can never happen — then the outer one, then the host is told.
    /// </summary>
    /// <remarks>
    /// The parent-side <c>error</c> event HTML fires at the <c>Worker</c> object lands in its own change
    /// (wave 3); what this wave gives a host is the connection surface, which needs no sink wired at all:
    /// <see cref="WorkerConnection.IsFaulted"/>, <see cref="WorkerConnection.Error"/> and an
    /// <see cref="WorkerEndReason.StartupFailed"/> that says what happened rather than blaming a
    /// <c>terminate()</c> nobody called.
    /// </remarks>
    private void OnModuleFailed(JsValue error) => FailStartup(ToClrFailure(error));

    private void FailStartup(Exception error) => Connection.TryEnd(WorkerEndReason.StartupFailed, error);

    /// <summary>
    /// Reduces the worker realm's rejection value to a CLR exception, because
    /// <see cref="WorkerConnection.Error"/> is read by a host on some other thread and a <c>JsValue</c>
    /// belongs to the engine that made it.
    /// </summary>
    /// <remarks>
    /// The CLR exception behind the failure is preferred whenever there is one — a
    /// <c>ModuleResolutionException</c>, or whatever a host loader threw, which the module machinery records
    /// on the error value it built. Failing that, the message and location are copied out as <i>strings</i>,
    /// through own data properties only: reading them with <c>Get</c> would run a script getter while the
    /// connection is being torn down.
    /// </remarks>
    private Exception ToClrFailure(JsValue error)
    {
        if (error is ErrorInstance instance && instance.ClrException is { } clrException)
        {
            return clrException;
        }

        return new WorkerStartupException($"The worker's module '{_specifier}' failed to start: {Describe(error)}");
    }

    /// <summary>
    /// A one-line description of a rejection value, built without running any script.
    /// </summary>
    private static string Describe(JsValue error)
    {
        if (error is not ObjectInstance instance)
        {
            return Throw.SafeToDisplayString(error);
        }

        var name = ReadOwnString(instance, "name") ?? "Error";
        var message = ReadOwnString(instance, "message");
        var text = message is null ? name : $"{name}: {message}";

        return ReadOwnString(instance, "stack") is { } stack ? stack : text;
    }

    private static string? ReadOwnString(ObjectInstance instance, string property)
    {
        var descriptor = instance.GetOwnProperty(JsString.Create(property));
        return descriptor.IsDataDescriptor() && descriptor.Value is JsString value ? value.ToString() : null;
    }

    /// <summary>
    /// <c>Options.WebApi.Workers.MaxQueuedMessages</c>, per direction: the sender is refused before it
    /// serializes anything, so a receiver nobody pumps cannot grow the sender's live set without bound.
    /// </summary>
    /// <remarks>
    /// Before the serialization rather than after it, unlike the specification's own step order for a
    /// disentangled port: this is a resource refusal rather than a delivery outcome, and there is nothing to
    /// be gained by detaching a transfer list for a message that is not going to be taken.
    /// </remarks>
    private void EnforceQueueBound(Engine engine, Realm realm, JsMessagePort source, string operation)
    {
        if (source.Endpoint?.Peer is not { } target)
        {
            return;
        }

        var queued = target.QueuedMessageCount;
        if (queued < _registry.MaxQueuedMessages)
        {
            return;
        }

        WorkerErrors.ThrowQuotaExceededError(
            engine,
            realm,
            $"Failed to execute '{operation}': the connection already has {queued} messages waiting to be delivered, which is its Options.WebApi.Workers.MaxQueuedMessages limit.",
            quota: Math.Max(0, _registry.MaxQueuedMessages),
            requested: (double) queued + 1);
    }
}

/// <summary>
/// The failure a worker's startup is reported as when there is no CLR exception behind it — a module whose
/// evaluation threw a JavaScript error, which cannot cross to the host as a value.
/// </summary>
/// <remarks>
/// Internal deliberately: a host reads it through <see cref="WorkerConnection.Error"/>, which is typed
/// <see cref="Exception"/>, and what it carries is a message. Making the type public would promise a shape
/// that is a summary by construction — the real failure is a value in another engine's realm, and the whole
/// reason this exists is that such a value may not travel.
/// </remarks>
internal sealed class WorkerStartupException : Exception
{
    internal WorkerStartupException(string message) : base(message)
    {
    }
}
#endif
