#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.WebApi.Streams;

/// <summary>
/// <c>ReadableStreamBYOBRequest.prototype</c> — the interface prototype object.
/// <para>
/// https://streams.spec.whatwg.org/#rs-byob-request-prototype
/// </para>
/// </summary>
[JsObject(UseShape = true)]
internal sealed partial class ReadableStreamBYOBRequestPrototype : Prototype
{
    [JsProperty(Name = "constructor", Flags = PropertyFlag.NonEnumerable)]
    private readonly ReadableStreamBYOBRequestConstructor _constructor;

    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)]
    private static readonly JsString RequestToStringTag = new("ReadableStreamBYOBRequest");

    internal ReadableStreamBYOBRequestPrototype(
        Engine engine,
        Realm realm,
        ReadableStreamBYOBRequestConstructor constructor,
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
    /// https://streams.spec.whatwg.org/#rs-byob-request-view — <c>null</c> once the request has been
    /// responded to or otherwise invalidated.
    /// </summary>
    [JsAccessor("view", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsValue ViewGet(JsValue thisObject)
    {
        var view = Brand(thisObject).View;
        return view is null ? Null : view;
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#rs-byob-request-respond — "I wrote this many bytes into
    /// <c>view</c>". The view is transferred by this call, so the underlying source must not write into it
    /// afterwards.
    /// </summary>
    [JsFunction(Name = "respond", Length = 1, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsValue Respond(JsValue thisObject, JsValue bytesWritten)
    {
        var request = Brand(thisObject);

        // The argument is converted at the WebIDL layer, before the method's own steps — so a bytesWritten
        // that is not a non-negative integer is reported even for an invalidated request.
        var written = StreamDictionaries.ToEnforcedUnsignedLongLong(_realm, bytesWritten, "The bytesWritten");

        if (request.Controller is not { } controller)
        {
            Throw.TypeError(_realm, "This BYOB request has been invalidated");
            return Undefined;
        }

        if (request.View!._viewedArrayBuffer.IsDetachedBuffer)
        {
            Throw.TypeError(_realm, "The BYOB request's view has a detached buffer");
        }

        ReadableByteStreamControllerOperations.Respond(controller, written);
        return Undefined;
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#rs-byob-request-respond-with-new-view — "I wrote into this other
    /// view of the same memory instead", which is how a source that had to hand its buffer to something else
    /// still avoids a copy.
    /// </summary>
    [JsFunction(Name = "respondWithNewView", Length = 1, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsValue RespondWithNewView(JsValue thisObject, JsValue viewArgument)
    {
        var request = Brand(thisObject);
        var view = StreamBufferOperations.ReadArrayBufferView(_realm, viewArgument, "The view");

        if (request.Controller is not { } controller)
        {
            Throw.TypeError(_realm, "This BYOB request has been invalidated");
            return Undefined;
        }

        if (view.Buffer.IsDetachedBuffer)
        {
            Throw.TypeError(_realm, "The view's buffer is detached");
        }

        ReadableByteStreamControllerOperations.RespondWithNewView(controller, in view);
        return Undefined;
    }

    private JsReadableStreamBYOBRequest Brand(JsValue thisObject)
    {
        if (thisObject is JsReadableStreamBYOBRequest request)
        {
            return request;
        }

        Throw.TypeError(_realm, "Illegal invocation: receiver is not a ReadableStreamBYOBRequest");
        return null!;
    }
}
#endif
