#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Promise;
using Jint.Runtime;
using Jint.WebApi.StructuredClone;

namespace Jint.WebApi.Streams;

/// <summary>
/// https://streams.spec.whatwg.org/#readable-stream-default-tee — the algorithm behind
/// <c>ReadableStream.prototype.tee()</c>, and the one <c>clone</c> reaches through
/// https://streams.spec.whatwg.org/#readablestream-tee.
/// </summary>
/// <remarks>
/// <para>
/// The two branches read from one reader over the original stream, which is locked for as long as they
/// exist. Each branch has its own queue and its own consumer, so one may run far ahead of the other.
/// </para>
/// <para>
/// <b>Whether the second branch gets the chunk itself or a structured clone of it is the algorithm's
/// <i>cloneForBranch2</i> parameter, and nothing else.</b> <c>tee()</c> passes <see langword="false"/> — the
/// chunks seen in each branch are then the same object, so two consumers of a mutable chunk can interfere
/// with each other, which is the standard's own caveat rather than an implementation shortcut. Every other
/// specification reaches the tee through the "tee a <c>ReadableStream</c>" wrapper, which passes
/// <see langword="true"/>; https://fetch.spec.whatwg.org/#concept-body-clone is the one that matters here,
/// and it is what stops a <c>Response</c> and its <c>clone()</c> sharing a buffer.
/// </para>
/// <para>
/// Cancelling one branch does not cancel the original: only when <i>both</i> have been cancelled is the
/// original cancelled, with an array of the two reasons as its composite reason.
/// </para>
/// </remarks>
internal sealed class ReadableStreamTee
{
    private readonly Engine _engine;
    private readonly Realm _realm;
    private readonly JsReadableStream _stream;
    private readonly JsReadableStreamDefaultReader _reader;
    private readonly PromiseCapability _cancelCapability;
    private readonly bool _cloneForBranch2;

    private bool _reading;
    private bool _readAgain;
    private bool _canceled1;
    private bool _canceled2;
    private JsValue _reason1 = JsValue.Undefined;
    private JsValue _reason2 = JsValue.Undefined;
    private JsReadableStream _branch1 = null!;
    private JsReadableStream _branch2 = null!;

    private ReadableStreamTee(JsReadableStream stream, bool cloneForBranch2)
    {
        _engine = stream.Engine;
        _realm = stream.Realm;
        _stream = stream;
        _reader = ReadableStreamOperations.AcquireDefaultReader(stream);
        _cancelCapability = StreamPromises.NewPromise(_engine, _realm);
        _cloneForBranch2 = cloneForBranch2;
    }

    /// <summary>
    /// Tees <paramref name="stream"/> and returns its two branches, in order.
    /// </summary>
    /// <param name="stream">The stream to tee, which is locked by the tee's own reader for good.</param>
    /// <param name="cloneForBranch2">
    /// The specification's <i>cloneForBranch2</i>: when set, every chunk the second branch is given is a
    /// StructuredClone of the chunk the first branch is given.
    /// </param>
    internal static (JsReadableStream Branch1, JsReadableStream Branch2) Tee(JsReadableStream stream, bool cloneForBranch2)
    {
        var tee = new ReadableStreamTee(stream, cloneForBranch2);

        tee._branch1 = ReadableStreamOperations.CreateReadableStream(
            tee._engine, tee._realm, static () => JsValue.Undefined, tee.PullAlgorithm, tee.Cancel1Algorithm);

        tee._branch2 = ReadableStreamOperations.CreateReadableStream(
            tee._engine, tee._realm, static () => JsValue.Undefined, tee.PullAlgorithm, tee.Cancel2Algorithm);

        // An error in the original reaches both branches through the reader's closed promise, which is the
        // only channel that reports an error arriving while no read is outstanding.
        StreamPromises.UponRejection(tee._engine, tee._reader.ClosedPromise, error =>
        {
            ReadableStreamDefaultControllerOperations.Error(tee._branch1.DefaultController, error);
            ReadableStreamDefaultControllerOperations.Error(tee._branch2.DefaultController, error);
            if (!tee._canceled1 || !tee._canceled2)
            {
                tee._cancelCapability.Resolve(JsValue.Undefined);
            }
        });

        return (tee._branch1, tee._branch2);
    }

    /// <summary>
    /// The pull algorithm both branches share: one read at a time over the original, with a request that
    /// arrives while a read is outstanding remembered rather than started.
    /// </summary>
    private JsPromise PullAlgorithm()
    {
        if (_reading)
        {
            _readAgain = true;
            return StreamPromises.ResolvedWithUndefined(_engine, _realm);
        }

        _reading = true;
        ReadableStreamOperations.DefaultReaderRead(_reader, new TeeReadRequest(this));
        return StreamPromises.ResolvedWithUndefined(_engine, _realm);
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#readable-stream-default-tee, the cancel algorithm of the first
    /// branch. The original is only cancelled once both branches have been.
    /// </summary>
    private JsPromise Cancel1Algorithm(JsValue reason)
    {
        _canceled1 = true;
        _reason1 = reason;

        if (_canceled2)
        {
            ResolveCancelPromiseWithComposite();
        }

        return StreamPromises.PromiseOf(_cancelCapability);
    }

    /// <summary>The cancel algorithm of the second branch.</summary>
    private JsPromise Cancel2Algorithm(JsValue reason)
    {
        _canceled2 = true;
        _reason2 = reason;

        if (_canceled1)
        {
            ResolveCancelPromiseWithComposite();
        }

        return StreamPromises.PromiseOf(_cancelCapability);
    }

    private void ResolveCancelPromiseWithComposite()
    {
        var compositeReason = new JsArray(_engine, [_reason1, _reason2]);
        var cancelResult = ReadableStreamOperations.Cancel(_stream, compositeReason);

        // Resolving with a promise makes both branches' cancel() promises adopt the underlying source's
        // outcome, so a source whose cancel() rejects reports that to whichever branch was cancelled last.
        _cancelCapability.Resolve(cancelResult);
    }

    /// <summary>
    /// The read request the shared pull uses. Its chunk steps are deliberately deferred by one microtask:
    /// an error in the original is only observable through the reader's closed promise, which takes a
    /// microtask to react to, and a synchronously available chunk must not overtake it.
    /// </summary>
    private sealed class TeeReadRequest : ReadRequest
    {
        private readonly ReadableStreamTee _tee;

        internal TeeReadRequest(ReadableStreamTee tee)
        {
            _tee = tee;
        }

        internal override void ChunkSteps(JsValue chunk)
        {
            _tee._engine.AddToEventLoop(() =>
            {
                _tee._readAgain = false;

                // Step 3: "let chunk1 and chunk2 be chunk" — the two branches receive the very same object
                // unless cloneForBranch2 asks for the second to get a StructuredClone of it.
                var chunk2 = chunk;

                if (!_tee._canceled2 && _tee._cloneForBranch2)
                {
                    try
                    {
                        chunk2 = StructuredCloner.Clone(_tee._engine, _tee._realm, chunk, transferList: null);
                    }
                    catch (JavaScriptException e)
                    {
                        // Step 3.2: a chunk the serializer refuses — a function, a Symbol, a detached buffer —
                        // errors BOTH branches with the DataCloneError and cancels the original with it. The
                        // first branch is errored too even though its own chunk was fine, and `reading` is
                        // deliberately left set: this read is the tee's last.
                        var error = e.Error;
                        ReadableStreamDefaultControllerOperations.Error(_tee._branch1.DefaultController, error);
                        ReadableStreamDefaultControllerOperations.Error(_tee._branch2.DefaultController, error);
                        _tee._cancelCapability.Resolve(ReadableStreamOperations.Cancel(_tee._stream, error));
                        return;
                    }
                }

                if (!_tee._canceled1)
                {
                    ReadableStreamDefaultControllerOperations.Enqueue(_tee._branch1.DefaultController, chunk);
                }

                if (!_tee._canceled2)
                {
                    ReadableStreamDefaultControllerOperations.Enqueue(_tee._branch2.DefaultController, chunk2);
                }

                _tee._reading = false;
                if (_tee._readAgain)
                {
                    _tee.PullAlgorithm();
                }
            });
        }

        internal override void CloseSteps()
        {
            _tee._reading = false;

            if (!_tee._canceled1)
            {
                ReadableStreamDefaultControllerOperations.Close(_tee._branch1.DefaultController);
            }

            if (!_tee._canceled2)
            {
                ReadableStreamDefaultControllerOperations.Close(_tee._branch2.DefaultController);
            }

            if (!_tee._canceled1 || !_tee._canceled2)
            {
                _tee._cancelCapability.Resolve(JsValue.Undefined);
            }
        }

        internal override void ErrorSteps(JsValue error)
        {
            // Nothing else: the branches are errored through the reader's closed promise instead, which is
            // the channel that also covers an error arriving with no read outstanding.
            _tee._reading = false;
        }
    }
}
#endif
