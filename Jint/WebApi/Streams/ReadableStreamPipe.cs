#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Promise;
using Jint.Runtime;
using Jint.WebApi.Abort;

namespace Jint.WebApi.Streams;

/// <summary>
/// https://streams.spec.whatwg.org/#readable-stream-pipe-to — the piping algorithm behind
/// <c>pipeTo()</c> and <c>pipeThrough()</c>.
/// </summary>
/// <remarks>
/// <para>
/// The standard writes this one operation in prose rather than in numbered steps, because it wants to leave
/// implementations room in how they schedule the reads and writes. What it does <i>not</i> leave room in are
/// the constraints, and they are what this class is written against:
/// </para>
/// <list type="bullet">
/// <item><description><b>Public API must not be used.</b> The reader, the writer and both streams are
/// manipulated through the abstract operations, never through the prototype methods a script can
/// replace.</description></item>
/// <item><description><b>Backpressure must be enforced.</b> A read happens only after the writer's
/// <c>ready</c> promise fulfils, so the destination's high water mark throttles the source.</description></item>
/// <item><description><b>Reads and writes must not be serialized against each other.</b> The pipe does not
/// wait for a write to complete before reading again — doing so would make the destination's queue useless.
/// Only shutdown waits for outstanding writes.</description></item>
/// <item><description><b>Shutdown must stop activity</b>, and the error and close conditions are checked in
/// the order the standard lists them.</description></item>
/// </list>
/// <para>
/// The whole of it runs on the engine's thread as ordinary promise reactions; an <c>AbortSignal</c> is
/// observed through the same internal abort-algorithm seam the rest of the web APIs use, so aborting the
/// pipe does not depend on the <c>abort</c> event reaching any script.
/// </para>
/// </remarks>
internal sealed class ReadableStreamPipe
{
    private readonly Engine _engine;
    private readonly Realm _realm;
    private readonly JsReadableStream _source;
    private readonly JsWritableStream _destination;
    private readonly bool _preventClose;
    private readonly bool _preventAbort;
    private readonly bool _preventCancel;
    private readonly JsAbortSignal? _signal;
    private readonly JsReadableStreamDefaultReader _reader;
    private readonly JsWritableStreamDefaultWriter _writer;
    private readonly PromiseCapability _capability;

    private bool _shuttingDown;
    private JsPromise _currentWrite;
    private Action? _abortAlgorithm;

    private ReadableStreamPipe(
        JsReadableStream source,
        JsWritableStream destination,
        bool preventClose,
        bool preventAbort,
        bool preventCancel,
        JsAbortSignal? signal)
    {
        _engine = source.Engine;
        _realm = source.Realm;
        _source = source;
        _destination = destination;
        _preventClose = preventClose;
        _preventAbort = preventAbort;
        _preventCancel = preventCancel;
        _signal = signal;

        _reader = ReadableStreamOperations.AcquireDefaultReader(source);
        _writer = WritableStreamOperations.AcquireDefaultWriter(destination);
        source.Disturbed = true;

        _capability = StreamPromises.NewPromise(_engine, _realm);
        _currentWrite = StreamPromises.ResolvedWithUndefined(_engine, _realm);
    }

    /// <summary>
    /// Starts piping and returns the promise that settles when the pipe finishes.
    /// </summary>
    internal static JsPromise PipeTo(
        JsReadableStream source,
        JsWritableStream destination,
        bool preventClose,
        bool preventAbort,
        bool preventCancel,
        JsAbortSignal? signal)
    {
        var pipe = new ReadableStreamPipe(source, destination, preventClose, preventAbort, preventCancel, signal);
        pipe.Start();
        return StreamPromises.PromiseOf(pipe._capability);
    }

    /// <summary>
    /// "Pipe <paramref name="readable"/> through <paramref name="transform"/>" —
    /// https://streams.spec.whatwg.org/#readablestream-pipe-through, the abstract operation another
    /// standard's algorithm reaches for when it composes two streams the engine owns.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is deliberately <i>not</i> <c>ReadableStream.prototype.pipeThrough</c>. That method exists to
    /// convert a <c>ReadableWritablePair</c> a script supplied — reading <c>readable</c> and
    /// <c>writable</c> off an arbitrary object, and <c>signal</c>/<c>preventClose</c>/… off a
    /// <c>StreamPipeOptions</c> — and every one of those reads is a property access a script can intercept.
    /// A composition inside the engine has neither an untrusted pair nor options to read: both streams came
    /// from a transform this code just built, so the operation is what it reduces to once those conversions
    /// are done. It is the same choice the piping algorithm itself is written against — "public API must not
    /// be used" — carried one level out.
    /// </para>
    /// <para>
    /// The operation's two assertions (neither side is locked) hold by construction for every caller here,
    /// since both streams are fresh. Its every option is defaulted: no signal, and nothing prevented.
    /// </para>
    /// <para>
    /// The pipe's own promise is not handed back — the readable side is — so it is marked handled and a
    /// failure surfaces through that stream instead of as an unhandled rejection.
    /// </para>
    /// </remarks>
    internal static JsReadableStream PipeThrough(JsReadableStream readable, JsTransformStream transform)
    {
        var promise = PipeTo(
            readable,
            transform.Writable,
            preventClose: false,
            preventAbort: false,
            preventCancel: false,
            signal: null);

        StreamPromises.MarkHandled(promise);

        return transform.Readable;
    }

    /// <summary>
    /// "Create a proxy for <paramref name="stream"/>" —
    /// https://streams.spec.whatwg.org/#readablestream-create-a-proxy: pipe it through an identity transform
    /// and hand back the readable end.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The standard states the observable consequence itself: the result "pulls its data from
    /// <i>stream</i>, while <i>stream</i> itself becomes immediately locked and disturbed". Both halves come
    /// from the pipe rather than from anything written here — it acquires a reader, which locks, and sets
    /// <c>disturbed</c> before it has read anything. <b>The proxied stream keeps its identity</b>, which is
    /// the difference from <see cref="ReadableStreamOperations.Tee"/>: a tee replaces the stream its caller
    /// held with a branch, and a proxy does not.
    /// </para>
    /// <para>
    /// The result is a default stream even when the source is a byte stream, because the identity transform's
    /// readable side is one — so BYOB reading does not survive a proxy, exactly as it does not in the
    /// algorithm this implements.
    /// </para>
    /// </remarks>
    internal static JsReadableStream CreateProxy(JsReadableStream stream)
        => PipeThrough(stream, TransformStreamOperations.CreateIdentity(stream.Engine, stream.Realm));

    private void Start()
    {
        if (_signal is { } signal)
        {
            _abortAlgorithm = Abort;

            // An already-aborted signal shuts the pipe down before it reads anything at all — and, as the
            // reference algorithm does, without ever attaching the error and close propagation below.
            if (signal.Aborted)
            {
                Abort();
                return;
            }

            signal.AddAbortAlgorithm(_abortAlgorithm);
        }

        // "Errors must be propagated forward": an errored source aborts the destination.
        IsOrBecomesErrored(
            () => _source.State == ReadableStreamState.Errored,
            () => _source.StoredError,
            _reader.ClosedPromise,
            storedError =>
            {
                if (!_preventAbort)
                {
                    ShutdownWithAction(() => WritableStreamOperations.Abort(_destination, storedError), isError: true, storedError);
                }
                else
                {
                    Shutdown(isError: true, storedError);
                }
            });

        // "Errors must be propagated backward": an errored destination cancels the source.
        IsOrBecomesErrored(
            () => _destination.State == WritableStreamState.Errored,
            () => _destination.StoredError,
            _writer.ClosedPromise,
            storedError =>
            {
                if (!_preventCancel)
                {
                    ShutdownWithAction(() => ReadableStreamOperations.Cancel(_source, storedError), isError: true, storedError);
                }
                else
                {
                    Shutdown(isError: true, storedError);
                }
            });

        // "Closing must be propagated forward": a closed source closes the destination.
        IsOrBecomesClosed(
            () => _source.State == ReadableStreamState.Closed,
            _reader.ClosedPromise,
            () =>
            {
                if (!_preventClose)
                {
                    ShutdownWithAction(
                        () => WritableStreamOperations.DefaultWriterCloseWithErrorPropagation(_writer),
                        isError: false,
                        JsValue.Undefined);
                }
                else
                {
                    Shutdown(isError: false, JsValue.Undefined);
                }
            });

        // "Closing must be propagated backward": a destination that is already closing or closed cancels the
        // source, because nothing read from it could ever be written.
        if (WritableStreamOperations.CloseQueuedOrInFlight(_destination) || _destination.State == WritableStreamState.Closed)
        {
            var destinationClosed = _realm.Intrinsics.TypeError.Construct(
                "The destination writable stream closed before all data could be piped to it");

            if (!_preventCancel)
            {
                ShutdownWithAction(() => ReadableStreamOperations.Cancel(_source, destinationClosed), isError: true, destinationClosed);
            }
            else
            {
                Shutdown(isError: true, destinationClosed);
            }
        }

        // The loop's own promise is never handed out: the pipe reports through _capability instead.
        StreamPromises.MarkHandled(PipeLoop());
    }

    /// <summary>
    /// The abort algorithm registered on the signal: abort the destination and cancel the source, then shut
    /// down with the signal's reason.
    /// </summary>
    private void Abort()
    {
        var error = _signal!.Reason;
        var actions = new List<Func<JsPromise>>(2);

        if (!_preventAbort)
        {
            actions.Add(() => _destination.State == WritableStreamState.Writable
                ? WritableStreamOperations.Abort(_destination, error)
                : StreamPromises.ResolvedWithUndefined(_engine, _realm));
        }

        if (!_preventCancel)
        {
            actions.Add(() => _source.State == ReadableStreamState.Readable
                ? ReadableStreamOperations.Cancel(_source, error)
                : StreamPromises.ResolvedWithUndefined(_engine, _realm));
        }

        ShutdownWithAction(() => WaitForAll(actions), isError: true, error);
    }

    /// <summary>
    /// "Getting a promise to wait for all of the actions": the two abort actions are started together and
    /// the shutdown waits for both. The first rejection is the one reported.
    /// </summary>
    private JsPromise WaitForAll(List<Func<JsPromise>> actions)
    {
        var capability = StreamPromises.NewPromise(_engine, _realm);
        var remaining = actions.Count;

        if (remaining == 0)
        {
            capability.Resolve(JsValue.Undefined);
            return StreamPromises.PromiseOf(capability);
        }

        foreach (var action in actions)
        {
            StreamPromises.UponPromise(
                _engine,
                action(),
                _ =>
                {
                    remaining--;
                    if (remaining == 0)
                    {
                        capability.Resolve(JsValue.Undefined);
                    }
                },
                capability.Reject);
        }

        return StreamPromises.PromiseOf(capability);
    }

    /// <summary>
    /// Reads and writes until the source closes or the pipe shuts down. One step at a time, but a write is
    /// never waited on before the next read starts.
    /// </summary>
    private JsPromise PipeLoop()
    {
        var capability = StreamPromises.NewPromise(_engine, _realm);
        Next(capability, done: false);
        return StreamPromises.PromiseOf(capability);
    }

    private void Next(PromiseCapability loopCapability, bool done)
    {
        if (done)
        {
            loopCapability.Resolve(JsValue.Undefined);
            return;
        }

        StreamPromises.UponPromise(
            _engine,
            PipeStep(),
            value => Next(loopCapability, TypeConverter.ToBoolean(value)),
            loopCapability.Reject);
    }

    /// <summary>
    /// One read, and the write it feeds. Fulfils with <see langword="true"/> once there is nothing left to
    /// pipe — because the source closed, or because the pipe is shutting down.
    /// </summary>
    private JsPromise PipeStep()
    {
        if (_shuttingDown)
        {
            return StreamPromises.ResolvedWith(_engine, _realm, JsBoolean.True);
        }

        // The read waits for the destination to want more: this is where backpressure is enforced.
        return StreamPromises.TransformPromiseWith(
            _engine,
            _realm,
            _writer.ReadyPromise,
            _ =>
            {
                var readCapability = StreamPromises.NewPromise(_engine, _realm);
                ReadableStreamOperations.DefaultReaderRead(_reader, new PipeReadRequest(this, readCapability));
                return StreamPromises.PromiseOf(readCapability);
            },
            onRejected: null);
    }

    /// <summary>
    /// Waits for every write that has been started to settle — including one started while waiting for an
    /// earlier one, which is why the loop re-checks that the current write is still the one it waited on.
    /// </summary>
    private JsPromise WaitForWritesToFinish()
    {
        var oldCurrentWrite = _currentWrite;

        return StreamPromises.TransformPromiseWith(
            _engine,
            _realm,
            _currentWrite,
            _ => ReferenceEquals(oldCurrentWrite, _currentWrite) ? JsValue.Undefined : WaitForWritesToFinish(),
            onRejected: null);
    }

    private void IsOrBecomesErrored(Func<bool> isErrored, Func<JsValue> storedError, JsPromise promise, Action<JsValue> action)
    {
        if (isErrored())
        {
            action(storedError());
            return;
        }

        StreamPromises.UponRejection(_engine, promise, action);
    }

    private void IsOrBecomesClosed(Func<bool> isClosed, JsPromise promise, Action action)
    {
        if (isClosed())
        {
            action();
            return;
        }

        StreamPromises.UponFulfillment(_engine, promise, _ => action());
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#rs-pipeTo-shutdown-with-action
    /// </summary>
    private void ShutdownWithAction(Func<JsPromise> action, bool isError, JsValue error)
    {
        if (_shuttingDown)
        {
            return;
        }

        _shuttingDown = true;

        if (_destination.State == WritableStreamState.Writable && !WritableStreamOperations.CloseQueuedOrInFlight(_destination))
        {
            StreamPromises.UponFulfillment(_engine, WaitForWritesToFinish(), _ => DoTheRest());
        }
        else
        {
            DoTheRest();
        }

        void DoTheRest()
        {
            StreamPromises.UponPromise(
                _engine,
                action(),
                _ => Finalize(isError, error),
                newError => Finalize(isError: true, newError));
        }
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#rs-pipeTo-shutdown
    /// </summary>
    private void Shutdown(bool isError, JsValue error)
    {
        if (_shuttingDown)
        {
            return;
        }

        _shuttingDown = true;

        if (_destination.State == WritableStreamState.Writable && !WritableStreamOperations.CloseQueuedOrInFlight(_destination))
        {
            StreamPromises.UponFulfillment(_engine, WaitForWritesToFinish(), _ => Finalize(isError, error));
        }
        else
        {
            Finalize(isError, error);
        }
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#rs-pipeTo-finalize — both locks are released, the signal stops being
    /// observed, and the pipe's promise settles.
    /// </summary>
    private void Finalize(bool isError, JsValue error)
    {
        WritableStreamOperations.DefaultWriterRelease(_writer);
        ReadableStreamOperations.DefaultReaderRelease(_reader);

        if (_signal is { } signal && _abortAlgorithm is { } abortAlgorithm)
        {
            signal.RemoveAbortAlgorithm(abortAlgorithm);
        }

        if (isError)
        {
            _capability.Reject(error);
        }
        else
        {
            _capability.Resolve(JsValue.Undefined);
        }
    }

    /// <summary>
    /// The read request one pipe step makes: a chunk starts a write and lets the loop go round again, a
    /// close ends the loop, and an error ends it abruptly — though the error itself is reported through the
    /// propagation rules above, not through here.
    /// </summary>
    private sealed class PipeReadRequest : ReadRequest
    {
        private readonly ReadableStreamPipe _pipe;
        private readonly PromiseCapability _readCapability;

        internal PipeReadRequest(ReadableStreamPipe pipe, PromiseCapability readCapability)
        {
            _pipe = pipe;
            _readCapability = readCapability;
        }

        /// <remarks>
        /// <para>
        /// <b>Deferred by one microtask, and this is the pipe's only deferral point.</b> A read request's
        /// chunk steps run synchronously inside <c>ReadableStreamFulfillReadRequest</c>, which is reached
        /// from the producer's own <c>enqueue()</c> — so starting the write from here would run the
        /// destination's <c>write</c> algorithm on the producer's stack. What licenses the pipe to schedule
        /// reads and writes as it likes is precisely that "the exact manner in which this happens is not
        /// observable to author code", and a synchronous re-entry into author code is the one thing that
        /// makes it observable.
        /// </para>
        /// <para>
        /// The <i>whole</i> body waits, not just the write, so the write still starts before the loop is let
        /// round again — deferring only the write would let the next step consult the writer's <c>ready</c>
        /// promise before this chunk had been charged to the destination's queue, which is the backpressure
        /// signal the standard says must throttle the reads. And it waits exactly one microtask, because
        /// "reads or writes should not be delayed for reasons other than these backpressure signals".
        /// </para>
        /// <para>
        /// <c>_currentWrite</c> is therefore assigned a microtask later than the chunk arrived, which the
        /// shutdown path already tolerates: <see cref="WaitForWritesToFinish"/> exists to notice a write that
        /// started while it was waiting, and any shutdown reaching it is itself a promise reaction queued
        /// after this microtask. What cannot be tolerated is writing through a writer the pipe has already
        /// released, hence the guard.
        /// </para>
        /// </remarks>
        internal override void ChunkSteps(JsValue chunk)
        {
            var pipe = _pipe;

            pipe._engine.AddToEventLoop(() =>
            {
                if (pipe._writer.Stream is null)
                {
                    // The pipe finalized while this microtask was queued — an already-errored destination
                    // shuts it down and releases both locks — and a released writer has no stream to write
                    // to. Nothing is owed to the chunk: the destination it was read for is gone, and the
                    // loop is told it is done (which is what the shutting-down check at the top of PipeStep
                    // would have answered anyway).
                    _readCapability.Resolve(JsBoolean.True);
                    return;
                }

                // The write is started and remembered, but deliberately not waited on: the destination's
                // queue is what absorbs the difference in speed between the two sides. A failed write is
                // reported through the destination's closed promise, so the rejection is swallowed here.
                pipe._currentWrite = StreamPromises.TransformPromiseWith(
                    pipe._engine,
                    pipe._realm,
                    WritableStreamOperations.DefaultWriterWrite(pipe._writer, chunk),
                    onFulfilled: null,
                    onRejected: static _ => JsValue.Undefined);

                _readCapability.Resolve(JsBoolean.False);
            });
        }

        internal override void CloseSteps() => _readCapability.Resolve(JsBoolean.True);

        internal override void ErrorSteps(JsValue error) => _readCapability.Reject(error);
    }
}
#endif
