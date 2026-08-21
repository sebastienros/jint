#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.WebApi.Streams;

/// <summary>
/// <c>ReadableByteStreamController.prototype</c> — the interface prototype object.
/// <para>
/// https://streams.spec.whatwg.org/#rbs-controller-prototype
/// </para>
/// </summary>
[JsObject(UseShape = true)]
internal sealed partial class ReadableByteStreamControllerPrototype : Prototype
{
    [JsProperty(Name = "constructor", Flags = PropertyFlag.NonEnumerable)]
    private readonly ReadableByteStreamControllerConstructor _constructor;

    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)]
    private static readonly JsString ControllerToStringTag = new("ReadableByteStreamController");

    internal ReadableByteStreamControllerPrototype(
        Engine engine,
        Realm realm,
        ReadableByteStreamControllerConstructor constructor,
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
    /// https://streams.spec.whatwg.org/#rbs-controller-byob-request — the buffer the stream is currently
    /// waiting to have filled, or <c>null</c> when there is no pending BYOB pull.
    /// </summary>
    [JsAccessor("byobRequest", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsValue ByobRequestGet(JsValue thisObject)
    {
        var byobRequest = ReadableByteStreamControllerOperations.GetByobRequest(Brand(thisObject));
        return byobRequest is null ? Null : byobRequest;
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#rbs-controller-desired-size — in bytes, since a byte stream's chunk
    /// size is its byte length.
    /// </summary>
    [JsAccessor("desiredSize", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsValue DesiredSizeGet(JsValue thisObject)
    {
        var desiredSize = ReadableByteStreamControllerOperations.GetDesiredSize(Brand(thisObject));
        return desiredSize is { } value ? JsNumber.Create(value) : Null;
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#rbs-controller-close
    /// </summary>
    [JsFunction(Name = "close", Length = 0)]
    private JsValue Close(JsValue thisObject)
    {
        var controller = Brand(thisObject);

        if (controller.CloseRequested)
        {
            Throw.TypeError(_realm, "The stream has already been closed");
        }

        if (controller.Stream.State != ReadableStreamState.Readable)
        {
            Throw.TypeError(_realm, "The stream is not in a state that permits close");
        }

        ReadableByteStreamControllerOperations.Close(controller);
        return Undefined;
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#rbs-controller-enqueue. The chunk is an <c>ArrayBufferView</c>, and
    /// an empty one — or one over a detached buffer, whose byte length is therefore also zero — is refused:
    /// a zero-length chunk would be indistinguishable from end-of-stream to a BYOB consumer.
    /// </summary>
    [JsFunction(Name = "enqueue", Length = 1)]
    private JsValue Enqueue(JsValue thisObject, JsValue chunk)
    {
        var controller = Brand(thisObject);
        var view = StreamBufferOperations.ReadArrayBufferView(_realm, chunk, "The chunk");

        if (view.ByteLength == 0)
        {
            Throw.TypeError(_realm, "The chunk has a byte length of 0");
        }

        if (view.Buffer.ArrayBufferByteLength == 0)
        {
            Throw.TypeError(_realm, "The chunk's buffer has a byte length of 0");
        }

        if (controller.CloseRequested)
        {
            Throw.TypeError(_realm, "The stream has already been closed");
        }

        if (controller.Stream.State != ReadableStreamState.Readable)
        {
            Throw.TypeError(_realm, "The stream is not in a state that permits enqueue");
        }

        ReadableByteStreamControllerOperations.Enqueue(controller, in view);
        return Undefined;
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#rbs-controller-error — silently does nothing for a stream that has
    /// already stopped being readable, exactly as the default controller's does.
    /// </summary>
    [JsFunction(Name = "error", Length = 0)]
    private JsValue Error(JsValue thisObject, JsValue error)
    {
        ReadableByteStreamControllerOperations.Error(Brand(thisObject), error);
        return Undefined;
    }

    private JsReadableByteStreamController Brand(JsValue thisObject)
    {
        if (thisObject is JsReadableByteStreamController controller)
        {
            return controller;
        }

        Throw.TypeError(_realm, "Illegal invocation: receiver is not a ReadableByteStreamController");
        return null!;
    }
}
#endif
