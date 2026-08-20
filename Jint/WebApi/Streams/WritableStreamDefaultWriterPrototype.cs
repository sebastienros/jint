#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.WebApi.Streams;

/// <summary>
/// <c>WritableStreamDefaultWriter.prototype</c> — the interface prototype object.
/// <para>
/// https://streams.spec.whatwg.org/#default-writer-prototype
/// </para>
/// </summary>
/// <remarks>
/// <c>desiredSize</c> is the odd one out: it is an <c>unrestricted double?</c> rather than a promise, so a
/// released writer makes it <b>throw</b> where every other member of this prototype answers with a rejected
/// promise.
/// </remarks>
[JsObject(UseShape = true)]
internal sealed partial class WritableStreamDefaultWriterPrototype : Prototype
{
    [JsProperty(Name = "constructor", Flags = PropertyFlag.NonEnumerable)]
    private readonly WritableStreamDefaultWriterConstructor _constructor;

    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)]
    private static readonly JsString WriterToStringTag = new("WritableStreamDefaultWriter");

    internal WritableStreamDefaultWriterPrototype(
        Engine engine,
        Realm realm,
        WritableStreamDefaultWriterConstructor constructor,
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
    /// https://streams.spec.whatwg.org/#default-writer-closed
    /// </summary>
    [JsAccessor("closed", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsPromise ClosedGet(JsValue thisObject)
    {
        if (thisObject is not JsWritableStreamDefaultWriter writer)
        {
            return RejectedTypeError();
        }

        return writer.ClosedPromise;
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#default-writer-desired-size
    /// </summary>
    [JsAccessor("desiredSize", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsValue DesiredSizeGet(JsValue thisObject)
    {
        var writer = Brand(thisObject);

        if (writer.Stream is null)
        {
            Throw.TypeError(_realm, "Cannot read the desired size of a released writer");
        }

        var desiredSize = WritableStreamOperations.DefaultWriterGetDesiredSize(writer);
        return desiredSize is { } value ? JsNumber.Create(value) : Null;
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#default-writer-ready — fulfils when the destination stops applying
    /// backpressure, and is replaced by a fresh pending promise each time backpressure returns.
    /// </summary>
    [JsAccessor("ready", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsPromise ReadyGet(JsValue thisObject)
    {
        if (thisObject is not JsWritableStreamDefaultWriter writer)
        {
            return RejectedTypeError();
        }

        return writer.ReadyPromise;
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#default-writer-abort
    /// </summary>
    [JsFunction(Name = "abort", Length = 0)]
    private JsPromise Abort(JsValue thisObject, JsValue reason)
    {
        try
        {
            var writer = Brand(thisObject);

            if (writer.Stream is null)
            {
                Throw.TypeError(_realm, "Cannot abort a stream using a released writer");
            }

            return WritableStreamOperations.DefaultWriterAbort(writer, reason);
        }
        catch (JavaScriptException e)
        {
            return StreamPromises.RejectedWith(_engine, _realm, e.Error);
        }
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#default-writer-close
    /// </summary>
    [JsFunction(Name = "close", Length = 0)]
    private JsPromise Close(JsValue thisObject)
    {
        try
        {
            var writer = Brand(thisObject);
            var stream = writer.Stream;

            if (stream is null)
            {
                Throw.TypeError(_realm, "Cannot close a stream using a released writer");
                return null!;
            }

            if (WritableStreamOperations.CloseQueuedOrInFlight(stream))
            {
                Throw.TypeError(_realm, "Cannot close an already-closing writable stream");
            }

            return WritableStreamOperations.DefaultWriterClose(writer);
        }
        catch (JavaScriptException e)
        {
            return StreamPromises.RejectedWith(_engine, _realm, e.Error);
        }
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#default-writer-release-lock — the lock may be released while writes
    /// are still outstanding: it prevents interleaved producers, it does not span the writes themselves.
    /// </summary>
    [JsFunction(Name = "releaseLock", Length = 0)]
    private JsValue ReleaseLock(JsValue thisObject)
    {
        var writer = Brand(thisObject);

        if (writer.Stream is null)
        {
            return Undefined;
        }

        WritableStreamOperations.DefaultWriterRelease(writer);
        return Undefined;
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#default-writer-write
    /// </summary>
    [JsFunction(Name = "write", Length = 0)]
    private JsPromise Write(JsValue thisObject, JsValue chunk)
    {
        try
        {
            var writer = Brand(thisObject);

            if (writer.Stream is null)
            {
                Throw.TypeError(_realm, "Cannot write to a stream using a released writer");
            }

            return WritableStreamOperations.DefaultWriterWrite(writer, chunk);
        }
        catch (JavaScriptException e)
        {
            return StreamPromises.RejectedWith(_engine, _realm, e.Error);
        }
    }

    private JsPromise RejectedTypeError()
        => StreamPromises.RejectedWith(
            _engine,
            _realm,
            _realm.Intrinsics.TypeError.Construct("Illegal invocation: receiver is not a WritableStreamDefaultWriter"));

    private JsWritableStreamDefaultWriter Brand(JsValue thisObject)
    {
        if (thisObject is JsWritableStreamDefaultWriter writer)
        {
            return writer;
        }

        Throw.TypeError(_realm, "Illegal invocation: receiver is not a WritableStreamDefaultWriter");
        return null!;
    }
}
#endif
