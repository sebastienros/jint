#if NET8_0_OR_GREATER
using Jint.Runtime;
using Jint.WebApi.Messaging;
using Jint.WebApi.StructuredClone;

namespace Jint.WebApi.Streams;

/// <summary>
/// The <i>transfer steps</i> and <i>transfer-receiving steps</i> of the three transferable stream interfaces.
/// <para>
/// https://streams.spec.whatwg.org/#rs-transfer, https://streams.spec.whatwg.org/#ws-transfer and
/// https://streams.spec.whatwg.org/#ts-transfer
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// <b>A transferred stream is a pipe plus a channel.</b> Transferring a <c>ReadableStream</c> creates an
/// entangled <c>MessagePort</c> pair in the sending realm, sets a cross-realm transform <i>writable</i> up on
/// one port, pipes the original stream into it, and puts the other port in the data holder. The
/// transfer-receiving steps build a cross-realm transform <i>readable</i> over the port that arrived. A
/// <c>WritableStream</c> is the mirror image, and a <c>TransformStream</c> is both: its readable and its
/// writable side are each transferred, so it costs two channels.
/// </para>
/// <para>
/// <b>The port rides the transport that already exists.</b> The standard's data holder holds
/// <c>StructuredSerializeWithTransfer(port2, « port2 »)</c> — a whole nested serialization whose result is one
/// port data holder and nothing else. Jint's <see cref="SerializedMessagePort"/> <i>is</i> that data holder,
/// so the stream's record carries one directly and the nesting collapses to a field. Nothing else changes:
/// the side travels exactly as a script-transferred port's does, the receiving engine binds it to a
/// <c>MessagePort</c> of its own realm, and a side that is never delivered is ended by the same stranding.
/// </para>
/// <para>
/// <b>What the original stream is afterwards</b> is what the standard says: <c>[[Detached]]</c>, and locked,
/// because the pipe holds a reader (or a writer) on it. A transferred <c>TransformStream</c> leaves both its
/// sides detached and locked. The pipe runs entirely on the sending engine's own event loop; the receiving
/// engine only ever sees messages.
/// </para>
/// </remarks>
internal static class TransferableStreams
{
    /// <summary>
    /// https://streams.spec.whatwg.org/#rs-transfer — the <c>ReadableStream</c> transfer steps, returning the
    /// channel side the data holder carries.
    /// </summary>
    internal static MessagePortEndpoint TransferReadable(JsReadableStream value)
    {
        var engine = value.Engine;
        var realm = value.Realm;

        // StructuredSerializeWithTransfer step 5.2, which for a nested serialization is asked here rather than
        // by the outer CompleteTransfers: transferring a TransformStream twice must be refused on the second
        // attempt even though the TransformStream object itself is a different one each time.
        if (value.Detached)
        {
            StructuredSerializer.ThrowDataCloneError(realm, "A ReadableStream that has already been transferred could not be cloned");
        }

        // Step 1.
        if (ReadableStreamOperations.IsLocked(value))
        {
            StructuredSerializer.ThrowDataCloneError(realm, "A locked ReadableStream could not be cloned");
        }

        // Steps 2-4.
        var (port1, port2) = MessagePortBridge.CreatePair(engine, realm, engine, realm);

        // Steps 5-6: a WritableStream in the current realm, which is what the original is piped into.
        var writable = new JsWritableStream(engine, realm)
        {
            _prototype = realm.Intrinsics.WritableStream.PrototypeObject,
        };

        CrossRealmTransform.SetUpWritable(writable, port1);

        // Steps 7-8. Nothing observes the pipe's promise — the stream is gone from this realm's point of view
        // — so it is marked handled rather than left to the rejection tracker.
        var promise = ReadableStreamPipe.PipeTo(
            value, writable, preventClose: false, preventAbort: false, preventCancel: false, signal: null);
        StreamPromises.MarkHandled(promise);

        // StructuredSerializeWithTransfer step 5's last act, done here so it covers the nested case too.
        value.Detached = true;

        // Step 9, reduced to the side the port data holder would have held; see the class remarks.
        return port2.DetachForTransfer();
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#ws-transfer — the <c>WritableStream</c> transfer steps, the mirror
    /// image of <see cref="TransferReadable"/>: a readable is created here and piped into the original.
    /// </summary>
    internal static MessagePortEndpoint TransferWritable(JsWritableStream value)
    {
        var engine = value.Engine;
        var realm = value.Realm;

        if (value.Detached)
        {
            StructuredSerializer.ThrowDataCloneError(realm, "A WritableStream that has already been transferred could not be cloned");
        }

        // Step 1.
        if (WritableStreamOperations.IsLocked(value))
        {
            StructuredSerializer.ThrowDataCloneError(realm, "A locked WritableStream could not be cloned");
        }

        var (port1, port2) = MessagePortBridge.CreatePair(engine, realm, engine, realm);

        var readable = new JsReadableStream(engine, realm)
        {
            _prototype = realm.Intrinsics.ReadableStream.PrototypeObject,
        };

        CrossRealmTransform.SetUpReadable(readable, port1);

        var promise = ReadableStreamPipe.PipeTo(
            readable, value, preventClose: false, preventAbort: false, preventCancel: false, signal: null);
        StreamPromises.MarkHandled(promise);

        value.Detached = true;

        return port2.DetachForTransfer();
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#ts-transfer, steps 3 and 4: both sides have to be unlocked before
    /// either is transferred, so a transform stream whose readable is locked leaves its writable alone.
    /// </summary>
    internal static void CheckTransformTransferable(JsTransformStream value)
    {
        var realm = value.Realm;

        if (value.Detached)
        {
            StructuredSerializer.ThrowDataCloneError(realm, "A TransformStream that has already been transferred could not be cloned");
        }

        if (ReadableStreamOperations.IsLocked(value.Readable))
        {
            StructuredSerializer.ThrowDataCloneError(realm, "A TransformStream whose readable side is locked could not be cloned");
        }

        if (WritableStreamOperations.IsLocked(value.Writable))
        {
            StructuredSerializer.ThrowDataCloneError(realm, "A TransformStream whose writable side is locked could not be cloned");
        }
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#rs-transfer — the <c>ReadableStream</c> transfer-receiving steps.
    /// </summary>
    /// <remarks>
    /// <paramref name="endpoint"/> is the channel side the data holder carried, or <see langword="null"/> for
    /// a record read a second time — which <see cref="SerializationRecord"/> forbids. Such a stream gets a
    /// port entangled with nothing, which is inert, exactly as a twice-read <c>MessagePort</c> holder does.
    /// </remarks>
    internal static JsReadableStream ReceiveReadable(Engine engine, Realm realm, MessagePortEndpoint? endpoint)
    {
        var stream = new JsReadableStream(engine, realm)
        {
            _prototype = realm.Intrinsics.ReadableStream.PrototypeObject,
        };

        CrossRealmTransform.SetUpReadable(stream, new JsMessagePort(engine, realm, endpoint));
        return stream;
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#ws-transfer — the <c>WritableStream</c> transfer-receiving steps.
    /// </summary>
    /// <inheritdoc cref="ReceiveReadable" path="/remarks"/>
    internal static JsWritableStream ReceiveWritable(Engine engine, Realm realm, MessagePortEndpoint? endpoint)
    {
        var stream = new JsWritableStream(engine, realm)
        {
            _prototype = realm.Intrinsics.WritableStream.PrototypeObject,
        };

        CrossRealmTransform.SetUpWritable(stream, new JsMessagePort(engine, realm, endpoint));
        return stream;
    }
}
#endif
