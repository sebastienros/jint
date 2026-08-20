#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Promise;
using Jint.Runtime;

namespace Jint.WebApi.Streams;

/// <summary>
/// https://streams.spec.whatwg.org/#abstract-opdef-readablebytestreamtee — the algorithm behind
/// <c>ReadableStream.prototype.tee()</c> for a stream whose controller is a byte controller.
/// </summary>
/// <remarks>
/// <para>
/// It differs from the default tee in three ways, all of them consequences of the branches being byte
/// streams themselves. Both branches are byte streams, so each can be read BYOB. The chunk handed to the
/// second branch is a <b>copy</b>, not the same view — it has to be, because a byte stream transfers the
/// buffer of everything enqueued into it, so one branch's chunk cannot also be the other's. And the reader
/// over the original stream is <b>swapped</b> at run time: a branch pulled through a BYOB request reads
/// into that request's own buffer, which needs a BYOB reader, while a branch pulled ordinarily needs a
/// default one — so whichever kind is needed is acquired, after releasing the other.
/// </para>
/// <para>
/// The reader swap is why every reader-error forwarding closes over the reader it was registered for: a
/// rejection arriving from a reader that has since been replaced belongs to the old lock and is ignored.
/// </para>
/// </remarks>
internal sealed class ReadableByteStreamTee
{
    private readonly Engine _engine;
    private readonly Realm _realm;
    private readonly JsReadableStream _stream;
    private readonly PromiseCapability _cancelCapability;

    private JsReadableStreamReader _reader;
    private bool _reading;
    private bool _readAgainForBranch1;
    private bool _readAgainForBranch2;
    private bool _canceled1;
    private bool _canceled2;
    private JsValue _reason1 = JsValue.Undefined;
    private JsValue _reason2 = JsValue.Undefined;
    private JsReadableStream _branch1 = null!;
    private JsReadableStream _branch2 = null!;
    private JsReadableByteStreamController _controller1 = null!;
    private JsReadableByteStreamController _controller2 = null!;

    private ReadableByteStreamTee(JsReadableStream stream)
    {
        _engine = stream.Engine;
        _realm = stream.Realm;
        _stream = stream;
        _reader = ReadableStreamOperations.AcquireDefaultReader(stream);
        _cancelCapability = StreamPromises.NewPromise(_engine, _realm);
    }

    /// <summary>
    /// Tees <paramref name="stream"/> and returns its two branches, in order.
    /// </summary>
    internal static (JsReadableStream Branch1, JsReadableStream Branch2) Tee(JsReadableStream stream)
    {
        var tee = new ReadableByteStreamTee(stream);

        tee._branch1 = ReadableStreamOperations.CreateReadableByteStream(
            tee._engine, tee._realm, static () => JsValue.Undefined, tee.Pull1Algorithm, tee.Cancel1Algorithm);

        tee._branch2 = ReadableStreamOperations.CreateReadableByteStream(
            tee._engine, tee._realm, static () => JsValue.Undefined, tee.Pull2Algorithm, tee.Cancel2Algorithm);

        tee._controller1 = (JsReadableByteStreamController) tee._branch1.Controller;
        tee._controller2 = (JsReadableByteStreamController) tee._branch2.Controller;

        tee.ForwardReaderError(tee._reader);

        return (tee._branch1, tee._branch2);
    }

    /// <summary>
    /// An error in the original reaches both branches through the reader's closed promise, which is the only
    /// channel that reports an error arriving while no read is outstanding.
    /// </summary>
    private void ForwardReaderError(JsReadableStreamReader thisReader)
    {
        StreamPromises.UponRejection(_engine, thisReader.ClosedPromise, error =>
        {
            // A rejection from a reader that has since been swapped out belongs to a lock nobody holds any
            // more, and says nothing about the stream.
            if (thisReader != _reader)
            {
                return;
            }

            ReadableByteStreamControllerOperations.Error(_controller1, error);
            ReadableByteStreamControllerOperations.Error(_controller2, error);

            if (!_canceled1 || !_canceled2)
            {
                _cancelCapability.Resolve(JsValue.Undefined);
            }
        });
    }

    private void PullWithDefaultReader()
    {
        if (_reader is JsReadableStreamBYOBReader byobReader)
        {
            ReadableStreamOperations.BYOBReaderRelease(byobReader);
            _reader = ReadableStreamOperations.AcquireDefaultReader(_stream);
            ForwardReaderError(_reader);
        }

        ReadableStreamOperations.DefaultReaderRead((JsReadableStreamDefaultReader) _reader, new TeeReadRequest(this));
    }

    private void PullWithBYOBReader(JsTypedArray view, bool forBranch2)
    {
        if (_reader is JsReadableStreamDefaultReader defaultReader)
        {
            ReadableStreamOperations.DefaultReaderRelease(defaultReader);
            _reader = ReadableStreamOperations.AcquireBYOBReader(_stream);
            ForwardReaderError(_reader);
        }

        var viewInfo = StreamBufferOperations.ReadArrayBufferView(_realm, view, "The view");

        ReadableStreamOperations.BYOBReaderRead(
            (JsReadableStreamBYOBReader) _reader, in viewInfo, min: 1, new TeeReadIntoRequest(this, forBranch2));
    }

    private JsPromise Pull1Algorithm() => PullAlgorithm(forBranch2: false);

    private JsPromise Pull2Algorithm() => PullAlgorithm(forBranch2: true);

    /// <summary>
    /// A branch pulls with whichever kind of read its own consumer asked for: a pending BYOB request on the
    /// branch means its consumer supplied a buffer, and the original is read straight into it.
    /// </summary>
    private JsPromise PullAlgorithm(bool forBranch2)
    {
        if (_reading)
        {
            if (forBranch2)
            {
                _readAgainForBranch2 = true;
            }
            else
            {
                _readAgainForBranch1 = true;
            }

            return StreamPromises.ResolvedWithUndefined(_engine, _realm);
        }

        _reading = true;

        var controller = forBranch2 ? _controller2 : _controller1;
        var byobRequest = ReadableByteStreamControllerOperations.GetByobRequest(controller);

        if (byobRequest is null)
        {
            PullWithDefaultReader();
        }
        else
        {
            PullWithBYOBReader(byobRequest.View!, forBranch2);
        }

        return StreamPromises.ResolvedWithUndefined(_engine, _realm);
    }

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
        _cancelCapability.Resolve(ReadableStreamOperations.Cancel(_stream, compositeReason));
    }

    /// <summary>
    /// The one read whose result both branches share, once whichever branch asked for it has been served.
    /// A failure to copy the chunk for the second branch errors both and cancels the original.
    /// </summary>
    private bool TryCloneChunk(JsTypedArray chunk, JsReadableByteStreamController first, JsReadableByteStreamController second, out JsTypedArray clone)
    {
        try
        {
            var view = StreamBufferOperations.ReadArrayBufferView(_realm, chunk, "The chunk");
            var buffer = StreamBufferOperations.CloneArrayBufferRegion(_realm, view.Buffer, view.ByteOffset, view.ByteLength);
            clone = StreamBufferOperations.ConstructUint8Array(_realm, buffer, 0, view.ByteLength);
            return true;
        }
        catch (JavaScriptException e)
        {
            ReadableByteStreamControllerOperations.Error(first, e.Error);
            ReadableByteStreamControllerOperations.Error(second, e.Error);
            _cancelCapability.Resolve(ReadableStreamOperations.Cancel(_stream, e.Error));
            clone = null!;
            return false;
        }
    }

    private void EnqueueToBranch(JsReadableByteStreamController controller, JsTypedArray chunk)
    {
        var view = StreamBufferOperations.ReadArrayBufferView(_realm, chunk, "The chunk");
        ReadableByteStreamControllerOperations.Enqueue(controller, in view);
    }

    private void RespondToBranch(JsReadableByteStreamController controller, JsTypedArray chunk)
    {
        var view = StreamBufferOperations.ReadArrayBufferView(_realm, chunk, "The chunk");
        ReadableByteStreamControllerOperations.RespondWithNewView(controller, in view);
    }

    /// <summary>Continues whichever branch asked for more while the shared read was outstanding.</summary>
    private void ReadAgainIfRequested()
    {
        _reading = false;

        if (_readAgainForBranch1)
        {
            PullAlgorithm(forBranch2: false);
        }
        else if (_readAgainForBranch2)
        {
            PullAlgorithm(forBranch2: true);
        }
    }

    /// <summary>
    /// The read request used when a branch pulls ordinarily. Its chunk steps are deferred by one microtask
    /// for the same reason the default tee's are: an error in the original is only observable through the
    /// reader's closed promise, and a synchronously available chunk must not overtake it.
    /// </summary>
    private sealed class TeeReadRequest : ReadRequest
    {
        private readonly ReadableByteStreamTee _tee;

        internal TeeReadRequest(ReadableByteStreamTee tee)
        {
            _tee = tee;
        }

        internal override void ChunkSteps(JsValue chunk)
        {
            _tee._engine.AddToEventLoop(() =>
            {
                _tee._readAgainForBranch1 = false;
                _tee._readAgainForBranch2 = false;

                var chunk1 = (JsTypedArray) chunk;
                var chunk2 = chunk1;

                if (!_tee._canceled1 && !_tee._canceled2)
                {
                    if (!_tee.TryCloneChunk(chunk1, _tee._controller1, _tee._controller2, out chunk2))
                    {
                        return;
                    }
                }

                if (!_tee._canceled1)
                {
                    _tee.EnqueueToBranch(_tee._controller1, chunk1);
                }

                if (!_tee._canceled2)
                {
                    _tee.EnqueueToBranch(_tee._controller2, chunk2);
                }

                _tee.ReadAgainIfRequested();
            });
        }

        internal override void CloseSteps()
        {
            _tee._reading = false;

            if (!_tee._canceled1)
            {
                ReadableByteStreamControllerOperations.Close(_tee._controller1);
            }

            if (!_tee._canceled2)
            {
                ReadableByteStreamControllerOperations.Close(_tee._controller2);
            }

            // A branch left holding a BYOB request has to be told that nothing more is coming, which for a
            // closed byte stream is a zero-byte response.
            if (_tee._controller1.PendingPullIntos.Count > 0)
            {
                ReadableByteStreamControllerOperations.Respond(_tee._controller1, 0);
            }

            if (_tee._controller2.PendingPullIntos.Count > 0)
            {
                ReadableByteStreamControllerOperations.Respond(_tee._controller2, 0);
            }

            if (!_tee._canceled1 || !_tee._canceled2)
            {
                _tee._cancelCapability.Resolve(JsValue.Undefined);
            }
        }

        internal override void ErrorSteps(JsValue error)
        {
            // Nothing else: the branches are errored through the reader's closed promise instead.
            _tee._reading = false;
        }
    }

    /// <summary>
    /// The read-into request used when a branch pulls through its own BYOB request: the original is read
    /// directly into that branch's consumer's buffer, and the other branch gets a copy.
    /// </summary>
    private sealed class TeeReadIntoRequest : ReadIntoRequest
    {
        private readonly ReadableByteStreamTee _tee;
        private readonly bool _forBranch2;

        internal TeeReadIntoRequest(ReadableByteStreamTee tee, bool forBranch2)
        {
            _tee = tee;
            _forBranch2 = forBranch2;
        }

        private JsReadableByteStreamController ByobBranch => _forBranch2 ? _tee._controller2 : _tee._controller1;

        private JsReadableByteStreamController OtherBranch => _forBranch2 ? _tee._controller1 : _tee._controller2;

        private bool ByobCanceled => _forBranch2 ? _tee._canceled2 : _tee._canceled1;

        private bool OtherCanceled => _forBranch2 ? _tee._canceled1 : _tee._canceled2;

        internal override void ChunkSteps(JsValue chunk)
        {
            _tee._engine.AddToEventLoop(() =>
            {
                _tee._readAgainForBranch1 = false;
                _tee._readAgainForBranch2 = false;

                var view = (JsTypedArray) chunk;

                if (!OtherCanceled)
                {
                    if (!_tee.TryCloneChunk(view, ByobBranch, OtherBranch, out var clonedChunk))
                    {
                        return;
                    }

                    if (!ByobCanceled)
                    {
                        _tee.RespondToBranch(ByobBranch, view);
                    }

                    _tee.EnqueueToBranch(OtherBranch, clonedChunk);
                }
                else if (!ByobCanceled)
                {
                    _tee.RespondToBranch(ByobBranch, view);
                }

                _tee.ReadAgainIfRequested();
            });
        }

        internal override void CloseSteps(JsValue chunk)
        {
            _tee._reading = false;

            if (!ByobCanceled)
            {
                ReadableByteStreamControllerOperations.Close(ByobBranch);
            }

            if (!OtherCanceled)
            {
                ReadableByteStreamControllerOperations.Close(OtherBranch);
            }

            // The chunk is the (empty) view the closed original handed back, and it is the branch's own
            // consumer's buffer: giving it back is what lets that read fulfil with { done: true }.
            if (!chunk.IsUndefined())
            {
                if (!ByobCanceled)
                {
                    _tee.RespondToBranch(ByobBranch, (JsTypedArray) chunk);
                }

                if (!OtherCanceled && OtherBranch.PendingPullIntos.Count > 0)
                {
                    ReadableByteStreamControllerOperations.Respond(OtherBranch, 0);
                }
            }

            if (!ByobCanceled || !OtherCanceled)
            {
                _tee._cancelCapability.Resolve(JsValue.Undefined);
            }
        }

        internal override void ErrorSteps(JsValue error)
        {
            _tee._reading = false;
        }
    }
}
#endif
