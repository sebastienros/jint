#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.WebApi.Streams;

/// <summary>
/// <c>ReadableStreamDefaultController.prototype</c> — the interface prototype object.
/// <para>
/// https://streams.spec.whatwg.org/#rs-default-controller-prototype
/// </para>
/// </summary>
[JsObject(UseShape = true)]
internal sealed partial class ReadableStreamDefaultControllerPrototype : Prototype
{
    [JsProperty(Name = "constructor", Flags = PropertyFlag.NonEnumerable)]
    private readonly ReadableStreamDefaultControllerConstructor _constructor;

    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)]
    private static readonly JsString ControllerToStringTag = new("ReadableStreamDefaultController");

    internal ReadableStreamDefaultControllerPrototype(
        Engine engine,
        Realm realm,
        ReadableStreamDefaultControllerConstructor constructor,
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
    /// https://streams.spec.whatwg.org/#rs-default-controller-desired-size — how much more the queue wants,
    /// which is negative when it is over-full and <c>null</c> once the stream has errored.
    /// </summary>
    [JsAccessor("desiredSize", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsValue DesiredSizeGet(JsValue thisObject)
    {
        var desiredSize = ReadableStreamDefaultControllerOperations.GetDesiredSize(Brand(thisObject));
        return desiredSize is { } value ? JsNumber.Create(value) : Null;
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#rs-default-controller-close
    /// </summary>
    [JsFunction(Name = "close", Length = 0, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsValue Close(JsValue thisObject)
    {
        var controller = Brand(thisObject);

        if (!ReadableStreamDefaultControllerOperations.CanCloseOrEnqueue(controller))
        {
            Throw.TypeError(_realm, "The stream is not in a state that permits close");
        }

        ReadableStreamDefaultControllerOperations.Close(controller);
        return Undefined;
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#rs-default-controller-enqueue
    /// </summary>
    [JsFunction(Name = "enqueue", Length = 0, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsValue Enqueue(JsValue thisObject, JsValue chunk)
    {
        var controller = Brand(thisObject);

        if (!ReadableStreamDefaultControllerOperations.CanCloseOrEnqueue(controller))
        {
            Throw.TypeError(_realm, "The stream is not in a state that permits enqueue");
        }

        ReadableStreamDefaultControllerOperations.Enqueue(controller, chunk);
        return Undefined;
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#rs-default-controller-error — silently does nothing for a stream
    /// that has already stopped being readable, so a source that errors twice is not itself an error.
    /// </summary>
    [JsFunction(Name = "error", Length = 0, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsValue Error(JsValue thisObject, JsValue error)
    {
        ReadableStreamDefaultControllerOperations.Error(Brand(thisObject), error);
        return Undefined;
    }

    private JsReadableStreamDefaultController Brand(JsValue thisObject)
    {
        if (thisObject is JsReadableStreamDefaultController controller)
        {
            return controller;
        }

        Throw.TypeError(_realm, "Illegal invocation: receiver is not a ReadableStreamDefaultController");
        return null!;
    }
}
#endif
