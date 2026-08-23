#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Iterator;
using Jint.Native.Object;
using Jint.Native.Promise;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.WebApi.Streams;

/// <summary>
/// <c>ReadableStreamDefaultReader.prototype</c> — the interface prototype object, carrying both the
/// interface's own members and the <c>ReadableStreamGenericReader</c> mixin's.
/// <para>
/// https://streams.spec.whatwg.org/#default-reader-prototype and
/// https://streams.spec.whatwg.org/#generic-reader-prototype
/// </para>
/// </summary>
[JsObject(UseShape = true)]
internal sealed partial class ReadableStreamDefaultReaderPrototype : Prototype
{
    [JsProperty(Name = "constructor", Flags = PropertyFlag.NonEnumerable)]
    private readonly ReadableStreamDefaultReaderConstructor _constructor;

    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)]
    private static readonly JsString ReaderToStringTag = new("ReadableStreamDefaultReader");

    internal ReadableStreamDefaultReaderPrototype(
        Engine engine,
        Realm realm,
        ReadableStreamDefaultReaderConstructor constructor,
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
    /// https://streams.spec.whatwg.org/#generic-reader-closed. The attribute's type is a promise type, so a
    /// failed brand check answers with a rejected promise rather than throwing —
    /// https://webidl.spec.whatwg.org/#dfn-attribute-getter.
    /// </summary>
    [JsAccessor("closed", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsPromise ClosedGet(JsValue thisObject)
    {
        if (thisObject is not JsReadableStreamDefaultReader reader)
        {
            return RejectedTypeError("Illegal invocation: receiver is not a ReadableStreamDefaultReader");
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
    /// https://streams.spec.whatwg.org/#default-reader-read
    /// </summary>
    [JsFunction(Name = "read", Length = 0, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsPromise Read(JsValue thisObject)
    {
        JsReadableStreamDefaultReader reader;
        try
        {
            reader = Brand(thisObject);

            if (reader.Stream is null)
            {
                Throw.TypeError(_realm, "Cannot read from a released reader");
            }
        }
        catch (JavaScriptException e)
        {
            return StreamPromises.RejectedWith(_engine, _realm, e.Error);
        }

        var capability = StreamPromises.NewPromise(_engine, _realm);
        ReadableStreamOperations.DefaultReaderRead(reader, new ReaderReadRequest(_engine, capability));
        return StreamPromises.PromiseOf(capability);
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#default-reader-release-lock
    /// </summary>
    /// <remarks>
    /// Releasing while reads are outstanding rejects each of them with a <c>TypeError</c>; the chunks they
    /// were waiting for stay in the stream's queue and can be read by a reader acquired later.
    /// </remarks>
    [JsFunction(Name = "releaseLock", Length = 0, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsValue ReleaseLock(JsValue thisObject)
    {
        var reader = Brand(thisObject);

        if (reader.Stream is null)
        {
            return Undefined;
        }

        ReadableStreamOperations.DefaultReaderRelease(reader);
        return Undefined;
    }

    private JsPromise RejectedTypeError(string message)
        => StreamPromises.RejectedWith(_engine, _realm, _realm.Intrinsics.TypeError.Construct(message));

    private JsReadableStreamDefaultReader Brand(JsValue thisObject)
    {
        if (thisObject is JsReadableStreamDefaultReader reader)
        {
            return reader;
        }

        Throw.TypeError(_realm, "Illegal invocation: receiver is not a ReadableStreamDefaultReader");
        return null!;
    }

    /// <summary>
    /// The read request behind <c>read()</c>: a chunk becomes
    /// <c>{ value: chunk, done: false }</c>, a close becomes <c>{ value: undefined, done: true }</c>, and an
    /// error becomes a rejection.
    /// </summary>
    private sealed class ReaderReadRequest : ReadRequest
    {
        private readonly Engine _engine;
        private readonly PromiseCapability _capability;

        internal ReaderReadRequest(Engine engine, PromiseCapability capability)
        {
            _engine = engine;
            _capability = capability;
        }

        internal override void ChunkSteps(JsValue chunk)
            => _capability.Resolve(IteratorResult.CreateValueIteratorPosition(_engine, chunk, JsBoolean.False));

        internal override void CloseSteps()
            => _capability.Resolve(IteratorResult.CreateValueIteratorPosition(_engine, JsValue.Undefined, JsBoolean.True));

        internal override void ErrorSteps(JsValue error) => _capability.Reject(error);
    }
}
#endif
