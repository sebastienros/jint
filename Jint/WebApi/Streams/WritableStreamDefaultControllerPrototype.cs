#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.WebApi.Abort;

namespace Jint.WebApi.Streams;

/// <summary>
/// <c>WritableStreamDefaultController.prototype</c> — the interface prototype object.
/// <para>
/// https://streams.spec.whatwg.org/#ws-default-controller-prototype
/// </para>
/// </summary>
[JsObject(UseShape = true)]
internal sealed partial class WritableStreamDefaultControllerPrototype : Prototype
{
    [JsProperty(Name = "constructor", Flags = PropertyFlag.NonEnumerable)]
    private readonly WritableStreamDefaultControllerConstructor _constructor;

    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)]
    private static readonly JsString ControllerToStringTag = new("WritableStreamDefaultController");

    internal WritableStreamDefaultControllerPrototype(
        Engine engine,
        Realm realm,
        WritableStreamDefaultControllerConstructor constructor,
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
    /// https://streams.spec.whatwg.org/#ws-default-controller-signal — an <c>AbortSignal</c> a sink can
    /// watch to abandon a write or a close the moment the stream is aborted, rather than discovering it only
    /// when its own promise is no longer listened to.
    /// </summary>
    [JsAccessor("signal", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsAbortSignal SignalGet(JsValue thisObject) => Brand(thisObject).Signal;

    /// <summary>
    /// https://streams.spec.whatwg.org/#ws-default-controller-error — silently does nothing unless the
    /// stream is still writable.
    /// </summary>
    [JsFunction(Name = "error", Length = 0, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsValue Error(JsValue thisObject, JsValue error)
    {
        var controller = Brand(thisObject);

        if (controller.Stream.State != WritableStreamState.Writable)
        {
            return Undefined;
        }

        WritableStreamDefaultControllerOperations.Error(controller, error);
        return Undefined;
    }

    private JsWritableStreamDefaultController Brand(JsValue thisObject)
    {
        if (thisObject is JsWritableStreamDefaultController controller)
        {
            return controller;
        }

        Throw.TypeError(_realm, "Illegal invocation: receiver is not a WritableStreamDefaultController");
        return null!;
    }
}
#endif
