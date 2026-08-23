#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Iterator;
using Jint.Native.Object;
using Jint.Native.Promise;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.WebApi.Streams;

/// <summary>
/// <c>ReadableStreamBYOBReader.prototype</c> — the interface prototype object, carrying both the interface's
/// own members and the <c>ReadableStreamGenericReader</c> mixin's.
/// <para>
/// https://streams.spec.whatwg.org/#byob-reader-prototype and
/// https://streams.spec.whatwg.org/#generic-reader-prototype
/// </para>
/// </summary>
/// <remarks>
/// The mixin's two members are duplicated here rather than shared with
/// <see cref="ReadableStreamDefaultReaderPrototype"/>, because that is what a WebIDL <c>includes</c>
/// statement produces: each interface prototype object gets its own function objects, and
/// <c>ReadableStreamBYOBReader.prototype.cancel !== ReadableStreamDefaultReader.prototype.cancel</c>.
/// </remarks>
[JsObject(UseShape = true)]
internal sealed partial class ReadableStreamBYOBReaderPrototype : Prototype
{
    [JsProperty(Name = "constructor", Flags = PropertyFlag.NonEnumerable)]
    private readonly ReadableStreamBYOBReaderConstructor _constructor;

    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)]
    private static readonly JsString ReaderToStringTag = new("ReadableStreamBYOBReader");

    internal ReadableStreamBYOBReaderPrototype(
        Engine engine,
        Realm realm,
        ReadableStreamBYOBReaderConstructor constructor,
        ObjectInstance objectPrototype) : base(engine, realm)
    {
        _prototype = objectPrototype;
        _constructor = constructor;
    }

    protected override void Initialize()
    {
        CreateProperties_Generated();
        CreateSymbols_Generated();
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#generic-reader-closed
    /// </summary>
    [JsAccessor("closed", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsPromise ClosedGet(JsValue thisObject)
    {
        if (thisObject is not JsReadableStreamBYOBReader reader)
        {
            // The attribute's type is a promise type, so a failed brand check answers with a rejected
            // promise rather than throwing — https://webidl.spec.whatwg.org/#dfn-attribute-getter.
            return RejectedTypeError("Illegal invocation: receiver is not a ReadableStreamBYOBReader");
        }

        return reader.ClosedPromise;
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#generic-reader-cancel
    /// </summary>
    [JsFunction(Name = "cancel", Length = 0, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsPromise Cancel(JsValue thisObject, JsValue reason)
    {
        try
        {
            var reader = Brand(thisObject);

            if (reader.Stream is null)
            {
                Throw.TypeError(_realm, "Cannot cancel a stream using a released reader");
            }

            return ReadableStreamOperations.ReaderGenericCancel(reader, reason);
        }
        catch (JavaScriptException e)
        {
            return StreamPromises.RejectedWith(_engine, _realm, e.Error);
        }
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#byob-reader-read — the read that fills the caller's own buffer.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The view is <b>transferred</b>, so <paramref name="viewArgument"/> is detached and unusable the
    /// moment this returns; the promise fulfils with a new view of the same type onto the same memory. That
    /// is the whole point of a BYOB read — the buffer travels down to the source and back rather than being
    /// copied — and it is why a read loop has to keep using the view it was <i>given</i> rather than the one
    /// it passed in.
    /// </para>
    /// <para>
    /// Every failure here is a rejection rather than a throw, including the brand check and the argument
    /// conversions, because the operation's return type is a promise type —
    /// https://webidl.spec.whatwg.org/#js-operations.
    /// </para>
    /// </remarks>
    [JsFunction(Name = "read", Length = 1, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsPromise Read(JsValue thisObject, JsValue viewArgument, JsValue options)
    {
        JsReadableStreamBYOBReader reader;
        StreamBufferOperations.ArrayBufferViewInfo view;
        int min;

        try
        {
            reader = Brand(thisObject);
            view = StreamBufferOperations.ReadArrayBufferView(_realm, viewArgument, "The view");
            var minimum = StreamDictionaries.ReadMinimumFill(_realm, options);

            if (view.ByteLength == 0)
            {
                Throw.TypeError(_realm, "The view has a byte length of 0");
            }

            if (view.Buffer.ArrayBufferByteLength == 0)
            {
                // Which is also how a detached buffer is caught: it has no byte length left.
                Throw.TypeError(_realm, "The view's buffer has a byte length of 0");
            }

            if (minimum == 0)
            {
                Throw.TypeError(_realm, "The read options' min must be greater than 0");
            }

            if (minimum > (ulong) view.ArrayLength)
            {
                Throw.RangeError(_realm, "The read options' min is larger than the view");
            }

            if (reader.Stream is null)
            {
                Throw.TypeError(_realm, "Cannot read from a released reader");
            }

            min = (int) minimum;
        }
        catch (JavaScriptException e)
        {
            return StreamPromises.RejectedWith(_engine, _realm, e.Error);
        }

        var capability = StreamPromises.NewPromise(_engine, _realm);
        ReadableStreamOperations.BYOBReaderRead(reader, in view, min, new ReaderReadIntoRequest(_engine, capability));
        return StreamPromises.PromiseOf(capability);
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#byob-reader-release-lock
    /// </summary>
    [JsFunction(Name = "releaseLock", Length = 0, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsValue ReleaseLock(JsValue thisObject)
    {
        var reader = Brand(thisObject);

        if (reader.Stream is null)
        {
            return Undefined;
        }

        ReadableStreamOperations.BYOBReaderRelease(reader);
        return Undefined;
    }

    private JsPromise RejectedTypeError(string message)
        => StreamPromises.RejectedWith(_engine, _realm, _realm.Intrinsics.TypeError.Construct(message));

    private JsReadableStreamBYOBReader Brand(JsValue thisObject)
    {
        if (thisObject is JsReadableStreamBYOBReader reader)
        {
            return reader;
        }

        Throw.TypeError(_realm, "Illegal invocation: receiver is not a ReadableStreamBYOBReader");
        return null!;
    }

    /// <summary>
    /// The read-into request behind <c>read()</c>. Its close steps carry the chunk, so a read of a closed
    /// stream fulfils with <c>{ value: emptyViewOntoTheSameMemory, done: true }</c> — and a read of a
    /// <i>cancelled</i> one with <c>{ value: undefined, done: true }</c>, the memory having been discarded.
    /// </summary>
    private sealed class ReaderReadIntoRequest : ReadIntoRequest
    {
        private readonly Engine _engine;
        private readonly PromiseCapability _capability;

        internal ReaderReadIntoRequest(Engine engine, PromiseCapability capability)
        {
            _engine = engine;
            _capability = capability;
        }

        internal override void ChunkSteps(JsValue chunk)
            => _capability.Resolve(IteratorResult.CreateValueIteratorPosition(_engine, chunk, JsBoolean.False));

        internal override void CloseSteps(JsValue chunk)
            => _capability.Resolve(IteratorResult.CreateValueIteratorPosition(_engine, chunk, JsBoolean.True));

        internal override void ErrorSteps(JsValue error) => _capability.Reject(error);
    }
}
#endif
