#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.WebApi.Streams;

/// <summary>
/// <c>WritableStream.prototype</c> — the interface prototype object.
/// <para>
/// https://streams.spec.whatwg.org/#ws-prototype
/// </para>
/// </summary>
[JsObject(UseShape = true)]
internal sealed partial class WritableStreamPrototype : Prototype
{
    [JsProperty(Name = "constructor", Flags = PropertyFlag.NonEnumerable)]
    private readonly WritableStreamConstructor _constructor;

    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)]
    private static readonly JsString WritableStreamToStringTag = new("WritableStream");

    internal WritableStreamPrototype(
        Engine engine,
        Realm realm,
        WritableStreamConstructor constructor,
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
    /// https://streams.spec.whatwg.org/#ws-locked
    /// </summary>
    [JsAccessor("locked", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsBoolean LockedGet(JsValue thisObject)
        => WritableStreamOperations.IsLocked(Brand(thisObject)) ? JsBoolean.True : JsBoolean.False;

    /// <summary>
    /// https://streams.spec.whatwg.org/#ws-abort
    /// </summary>
    [JsFunction(Name = "abort", Length = 0)]
    private JsPromise Abort(JsValue thisObject, JsValue reason)
    {
        try
        {
            var stream = Brand(thisObject);

            if (WritableStreamOperations.IsLocked(stream))
            {
                Throw.TypeError(_realm, "Cannot abort a writable stream that is locked to a writer");
            }

            return WritableStreamOperations.Abort(stream, reason);
        }
        catch (JavaScriptException e)
        {
            return StreamPromises.RejectedWith(_engine, _realm, e.Error);
        }
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#ws-close
    /// </summary>
    [JsFunction(Name = "close", Length = 0)]
    private JsPromise Close(JsValue thisObject)
    {
        try
        {
            var stream = Brand(thisObject);

            if (WritableStreamOperations.IsLocked(stream))
            {
                Throw.TypeError(_realm, "Cannot close a writable stream that is locked to a writer");
            }

            if (WritableStreamOperations.CloseQueuedOrInFlight(stream))
            {
                Throw.TypeError(_realm, "Cannot close an already-closing writable stream");
            }

            return WritableStreamOperations.Close(stream);
        }
        catch (JavaScriptException e)
        {
            return StreamPromises.RejectedWith(_engine, _realm, e.Error);
        }
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#ws-get-writer
    /// </summary>
    [JsFunction(Name = "getWriter", Length = 0)]
    private JsWritableStreamDefaultWriter GetWriter(JsValue thisObject)
        => WritableStreamOperations.AcquireDefaultWriter(Brand(thisObject));

    private JsWritableStream Brand(JsValue thisObject)
    {
        if (thisObject is JsWritableStream stream)
        {
            return stream;
        }

        Throw.TypeError(_realm, "Illegal invocation: receiver is not a WritableStream");
        return null!;
    }
}
#endif
