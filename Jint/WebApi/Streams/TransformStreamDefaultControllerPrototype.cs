#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.WebApi.Streams;

/// <summary>
/// <c>TransformStreamDefaultController.prototype</c> — the interface prototype object.
/// <para>
/// https://streams.spec.whatwg.org/#ts-default-controller-prototype
/// </para>
/// </summary>
[JsObject(UseShape = true)]
internal sealed partial class TransformStreamDefaultControllerPrototype : Prototype
{
    [JsProperty(Name = "constructor", Flags = PropertyFlag.NonEnumerable)]
    private readonly TransformStreamDefaultControllerConstructor _constructor;

    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)]
    private static readonly JsString ControllerToStringTag = new("TransformStreamDefaultController");

    internal TransformStreamDefaultControllerPrototype(
        Engine engine,
        Realm realm,
        TransformStreamDefaultControllerConstructor constructor,
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
    /// https://streams.spec.whatwg.org/#ts-default-controller-desired-size — the desired size of the
    /// <b>readable</b> side, which is what tells a transformer how much its consumer still wants.
    /// </summary>
    [JsAccessor("desiredSize", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsValue DesiredSizeGet(JsValue thisObject)
    {
        var readableController = Brand(thisObject).Stream.Readable.DefaultController;
        var desiredSize = ReadableStreamDefaultControllerOperations.GetDesiredSize(readableController);
        return desiredSize is { } value ? JsNumber.Create(value) : Null;
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#ts-default-controller-enqueue
    /// </summary>
    [JsFunction(Name = "enqueue", Length = 0, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsValue Enqueue(JsValue thisObject, JsValue chunk)
    {
        TransformStreamOperations.ControllerEnqueue(Brand(thisObject), chunk);
        return Undefined;
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#ts-default-controller-error — errors both sides, discarding whatever
    /// was queued for transformation.
    /// </summary>
    [JsFunction(Name = "error", Length = 0, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsValue Error(JsValue thisObject, JsValue reason)
    {
        TransformStreamOperations.ControllerError(Brand(thisObject), reason);
        return Undefined;
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#ts-default-controller-terminate — closes the readable side and
    /// errors the writable side, for a transformer that only wants part of what is written to it.
    /// </summary>
    [JsFunction(Name = "terminate", Length = 0, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsValue Terminate(JsValue thisObject)
    {
        TransformStreamOperations.ControllerTerminate(Brand(thisObject));
        return Undefined;
    }

    private JsTransformStreamDefaultController Brand(JsValue thisObject)
    {
        if (thisObject is JsTransformStreamDefaultController controller)
        {
            return controller;
        }

        Throw.TypeError(_realm, "Illegal invocation: receiver is not a TransformStreamDefaultController");
        return null!;
    }
}
#endif
