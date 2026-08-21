#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;
using Jint.Native.Promise;
using Jint.Runtime;
using Jint.WebApi.Messaging;

namespace Jint.WebApi.Streams;

/// <summary>
/// The "cross-realm transform" abstract operations — the identity transform whose writable side is in one
/// realm and whose readable side is in another, which is the whole mechanism behind a transferred stream.
/// <para>
/// https://streams.spec.whatwg.org/#transferrable-streams
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// <b>Everything crosses as a <c>MessagePort</c> message, and deliberately so.</b> The standard's
/// <c>PackAndPostMessage</c> builds an ordinary object <c>{ type, value }</c> and runs the <i>message port
/// post message steps</i> on it, "to avoid having to duplicate" them — so a chunk is structured-serialized on
/// the sending engine, travels as an engine-neutral
/// <see cref="StructuredClone.SerializationRecord"/>, and is deserialized into the receiving realm, exactly as
/// any other message. A chunk that cannot be cloned therefore fails the way the standard says it does, at the
/// write, with the <c>DataCloneError</c> reported to the other side as well.
/// </para>
/// <para>
/// The protocol is four message types on one channel. Sender to receiver: <c>chunk</c> carrying the value,
/// <c>close</c>, and <c>error</c> carrying the reason. Receiver to sender: <c>pull</c>, which is the only
/// backpressure signal there is — the writable side starts with an unresolved backpressure promise and the
/// readable side's high water mark is 0, so the first chunk is not posted until the receiving script actually
/// reads, and one <c>pull</c> releases exactly one chunk.
/// </para>
/// <para>
/// <b>Both ends run on their own engine's own pump, and neither runs on the other's.</b> A cross-engine
/// transferred stream is two engines that have to be pumped: a chunk written on the sender is a task on the
/// receiver, so an engine nobody pumps receives nothing — the same contract a <c>MessagePort</c> carries, and
/// for the same reason, since a port is exactly what this is.
/// </para>
/// <para>
/// <b>One thing here is Jint's and not the standard's</b>, and it is the reason a transferred stream cannot
/// leave a pipe running against nothing. HTML's post message steps drop a message posted into a disentangled
/// channel without telling anybody, so <c>PackAndPostMessage</c> cannot fail that way and the standard's
/// writable side would go on draining its source into a channel whose far end has been ended — by a message
/// that was serialized and never delivered, by a <c>close()</c>, or by a <c>RestoreGlobalSnapshot</c> on the
/// receiving engine. The write and pull algorithms therefore consult
/// <see cref="JsMessagePort.IsChannelExhausted"/> first and end their stream when it answers yes. It can only
/// answer yes where the standard leaves the outcome undefined: every orderly shutdown sends <c>close</c> or
/// <c>error</c> <i>before</i> disentangling, and that message is still on the queue the predicate consults.
/// </para>
/// </remarks>
internal static class CrossRealmTransform
{
    /// <summary>The message's <c>type</c> key, https://streams.spec.whatwg.org/#abstract-opdef-packandpostmessage.</summary>
    private static readonly JsString _typeKey = new("type");

    /// <summary>The message's <c>value</c> key.</summary>
    private static readonly JsString _valueKey = new("value");

    private static readonly JsString _chunkType = new("chunk");

    private static readonly JsString _closeType = new("close");

    private static readonly JsString _errorType = new("error");

    private static readonly JsString _pullType = new("pull");

    /// <summary>The size algorithm both sides get: "an algorithm that returns 1".</summary>
    private static readonly Func<JsValue, double> _sizeOfOne = static _ => 1;

    /// <summary>
    /// https://streams.spec.whatwg.org/#abstract-opdef-setupcrossrealmtransformreadable — the receiving half:
    /// a readable stream whose chunks arrive over <paramref name="port"/>.
    /// </summary>
    internal static void SetUpReadable(JsReadableStream stream, JsMessagePort port)
    {
        var engine = stream.Engine;
        var realm = stream.Realm;

        var controller = new JsReadableStreamDefaultController(engine, realm)
        {
            _prototype = realm.Intrinsics.ReadableStreamDefaultController.PrototypeObject,
        };

        // "Add a handler for port's message event." See JsMessagePort.InternalMessageHandler for why this is a
        // delegate rather than a listener; there is no messageerror handler because Jint never fires one — a
        // record built by this engine's own serializer always deserializes, which JsMessagePort documents.
        port.InternalMessageHandler = data =>
        {
            if (!TryReadMessage(data, out var type, out var value))
            {
                // The standard asserts here and notes that "the input might come from an untrusted context".
                // In Jint it cannot: neither port of the pair a transfer creates is ever handed to script, so
                // the only writer of these messages is the sending half below. Refusing the message rather
                // than trusting it costs one test and keeps that reasoning from having to hold forever.
                return;
            }

            if (type.Equals(_chunkType))
            {
                ReadableStreamDefaultControllerOperations.Enqueue(controller, value);
                return;
            }

            if (type.Equals(_closeType))
            {
                ReadableStreamDefaultControllerOperations.Close(controller);
                Disentangle(port);
                return;
            }

            if (type.Equals(_errorType))
            {
                ReadableStreamDefaultControllerOperations.Error(controller, value);
                Disentangle(port);
            }
        };

        // "Enable port's port message queue" — which is what makes whatever the transfer brought with it, and
        // whatever the sender has posted since, reach the handler above.
        port.Start();

        ReadableStreamOperations.SetUpDefaultController(
            stream,
            controller,
            startAlgorithm: static () => JsValue.Undefined,
            pullAlgorithm: () =>
            {
                // Jint's addition; see the class remarks. A source that can never send again is a stream that
                // will never produce another chunk, so it is errored rather than left readable forever.
                if (port.IsChannelExhausted)
                {
                    Disentangle(port);
                    ReadableStreamDefaultControllerOperations.Error(controller, ChannelGoneError(realm));
                    return StreamPromises.ResolvedWithUndefined(engine, realm);
                }

                // "Perform ! PackAndPostMessage(port, "pull", undefined)": undefined always serializes, so the
                // standard's `!` holds and there is nothing to handle.
                PackAndPostMessage(port, _pullType, JsValue.Undefined);
                return StreamPromises.ResolvedWithUndefined(engine, realm);
            },
            cancelAlgorithm: reason =>
            {
                var failure = PackAndPostMessageHandlingError(port, _errorType, reason);
                Disentangle(port);

                return failure is null
                    ? StreamPromises.ResolvedWithUndefined(engine, realm)
                    : StreamPromises.RejectedWith(engine, realm, failure);
            },
            highWaterMark: 0,
            sizeAlgorithm: _sizeOfOne);
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#abstract-opdef-setupcrossrealmtransformwritable — the sending half: a
    /// writable stream that posts everything written to it over <paramref name="port"/>.
    /// </summary>
    internal static void SetUpWritable(JsWritableStream stream, JsMessagePort port)
    {
        var engine = stream.Engine;
        var realm = stream.Realm;

        var controller = new JsWritableStreamDefaultController(engine, realm)
        {
            _prototype = realm.Intrinsics.WritableStreamDefaultController.PrototypeObject,
        };

        // "Let backpressurePromise be a new promise" — pending, so the very first chunk waits for the
        // receiver's first read. Null stands for the standard's "undefined", i.e. nothing to wait on.
        PromiseCapability? backpressure = StreamPromises.NewPromise(engine, realm);

        void ReleaseBackpressure()
        {
            if (backpressure is not { } pending)
            {
                return;
            }

            backpressure = null;
            pending.Resolve(JsValue.Undefined);
        }

        port.InternalMessageHandler = data =>
        {
            if (!TryReadMessage(data, out var type, out var value))
            {
                return;
            }

            if (type.Equals(_pullType))
            {
                ReleaseBackpressure();
                return;
            }

            if (type.Equals(_errorType))
            {
                WritableStreamDefaultControllerOperations.ErrorIfNeeded(controller, value);

                // Released after the error too: a write already waiting must not stay parked on a channel
                // that has just reported failure.
                ReleaseBackpressure();
            }
        };

        port.Start();

        WritableStreamDefaultControllerOperations.SetUp(
            stream,
            controller,
            startAlgorithm: static () => JsValue.Undefined,
            writeAlgorithm: chunk =>
            {
                // Jint's addition; see the class remarks. Checked before the backpressure wait rather than
                // after it, because the pull that would end that wait is exactly what a channel with no far
                // end can no longer send.
                if (port.IsChannelExhausted)
                {
                    Disentangle(port);
                    WritableStreamDefaultControllerOperations.ErrorIfNeeded(controller, ChannelGoneError(realm));

                    // Resolved rather than rejected: the stream is erroring already, and the in-flight write
                    // request has to finish before WritableStreamFinishErroring may run.
                    return StreamPromises.ResolvedWithUndefined(engine, realm);
                }

                // Step 1, "if backpressurePromise is undefined, set it to a promise resolved with undefined",
                // as a local rather than as an assignment: the reaction below replaces it unconditionally, and
                // the only reader in between — ReleaseBackpressure — treats null and an already-resolved
                // promise identically.
                var waitOn = backpressure is { } pending
                    ? StreamPromises.PromiseOf(pending)
                    : StreamPromises.ResolvedWithUndefined(engine, realm);

                return StreamPromises.TransformPromiseWith(
                    engine,
                    realm,
                    waitOn,
                    _ =>
                    {
                        // A fresh one before the post, so the next chunk waits for the next pull.
                        backpressure = StreamPromises.NewPromise(engine, realm);

                        var failure = PackAndPostMessageHandlingError(port, _chunkType, chunk);
                        if (failure is null)
                        {
                            return JsValue.Undefined;
                        }

                        Disentangle(port);

                        // The reaction's abrupt completion is what rejects the derived promise, which is the
                        // write's own promise: an uncloneable chunk fails that write and errors the stream.
                        throw new JavaScriptException(failure);
                    },
                    onRejected: null);
            },
            closeAlgorithm: () =>
            {
                PackAndPostMessage(port, _closeType, JsValue.Undefined);
                Disentangle(port);
                return StreamPromises.ResolvedWithUndefined(engine, realm);
            },
            abortAlgorithm: reason =>
            {
                var failure = PackAndPostMessageHandlingError(port, _errorType, reason);
                Disentangle(port);

                return failure is null
                    ? StreamPromises.ResolvedWithUndefined(engine, realm)
                    : StreamPromises.RejectedWith(engine, realm, failure);
            },
            highWaterMark: 1,
            sizeAlgorithm: _sizeOfOne);
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#abstract-opdef-packandpostmessage — build <c>{ type, value }</c> and
    /// run the message port post message steps on it.
    /// </summary>
    /// <remarks>
    /// The object's prototype is null, which the standard asks for so that <c>%Object.prototype%</c> cannot
    /// interfere. It survives only as far as the serializer: structured cloning never carries a prototype, so
    /// what the far side reads is an ordinary object of its own realm — whose own <c>type</c> and
    /// <c>value</c> properties still outrank anything a script put on <c>Object.prototype</c> there.
    /// </remarks>
    private static void PackAndPostMessage(JsMessagePort port, JsString type, JsValue value)
    {
        var message = ObjectInstance.OrdinaryObjectCreate(port.Engine, proto: null);
        message.CreateDataProperty(_typeKey, type);
        message.CreateDataProperty(_valueKey, value);

        port.PostMessage(message, transferList: null);
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#abstract-opdef-packandpostmessagehandlingerror — the same, reporting a
    /// failure to the other side before handing it back.
    /// </summary>
    /// <returns>
    /// The value the post threw, or <see langword="null"/> when it did not — the completion record the
    /// standard returns, in the one shape a caller here ever inspects.
    /// </returns>
    private static JsValue? PackAndPostMessageHandlingError(JsMessagePort port, JsString type, JsValue value)
    {
        try
        {
            PackAndPostMessage(port, type, value);
            return null;
        }
        catch (JavaScriptException e)
        {
            CrossRealmTransformSendError(port, e.Error);
            return e.Error;
        }
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#abstract-opdef-crossrealmtransformsenderror — "As we are already in an
    /// errored state when this abstract operation is performed, we cannot handle further errors, so we just
    /// discard them."
    /// </summary>
    private static void CrossRealmTransformSendError(JsMessagePort port, JsValue error)
    {
        try
        {
            PackAndPostMessage(port, _errorType, error);
        }
        catch (JavaScriptException)
        {
            // Discarded, as the note says. An error value that cannot itself be cloned — a DOMException can
            // always be — leaves the far side to notice through the channel going quiet.
        }
    }

    /// <summary>
    /// "Disentangle port". HTML's disentangle is two-sided, and so is closing a side here: the peer's
    /// <c>postMessage</c> consults it and finds it has no target — see <c>MessagePortEndpoint</c>.
    /// </summary>
    private static void Disentangle(JsMessagePort port) => port.Close();

    /// <summary>
    /// Reads the two properties the message carries. The standard asserts their shape; this returns
    /// <see langword="false"/> instead, for the reason the readable side's handler gives.
    /// </summary>
    private static bool TryReadMessage(JsValue data, out JsString type, out JsValue value)
    {
        if (data is ObjectInstance envelope && envelope.Get(_typeKey) is JsString messageType)
        {
            type = messageType;
            value = envelope.Get(_valueKey);
            return true;
        }

        type = JsString.Empty;
        value = JsValue.Undefined;
        return false;
    }

    /// <summary>
    /// The reason a stream is given when its channel has been ended without a <c>close</c> or an <c>error</c>
    /// ever arriving. Jint's, not the standard's; see the class remarks.
    /// </summary>
    private static ObjectInstance ChannelGoneError(Realm realm) => realm.Intrinsics.TypeError.Construct(
        "The transferred stream's channel was closed before the stream finished");
}
#endif
