#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.WebApi.Abort;

/// <summary>
/// <c>AbortController.prototype</c> — the interface prototype object.
/// <para>
/// https://dom.spec.whatwg.org/#interface-abortcontroller
/// </para>
/// </summary>
[JsObject(UseShape = true)]
internal sealed partial class AbortControllerPrototype : Prototype
{
    [JsProperty(Name = "constructor", Flags = PropertyFlag.NonEnumerable)]
    private readonly AbortControllerConstructor _constructor;

    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)]
    private static readonly JsString AbortControllerToStringTag = new("AbortController");

    internal AbortControllerPrototype(
        Engine engine,
        Realm realm,
        AbortControllerConstructor constructor,
        ObjectPrototype objectPrototype) : base(engine, realm)
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
    /// https://dom.spec.whatwg.org/#dom-abortcontroller-signal
    /// </summary>
    [JsAccessor("signal", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsAbortSignal SignalGet(JsValue thisObject) => Brand(thisObject).Signal;

    /// <summary>
    /// https://dom.spec.whatwg.org/#dom-abortcontroller-abort — "signal abort on this with reason if it is
    /// given". Calling it a second time does nothing, because signal abort returns early for a signal that has
    /// already aborted.
    /// </summary>
    [JsFunction(Name = "abort", Length = 0)]
    private JsValue Abort(JsValue thisObject, JsValue reason)
    {
        var controller = Brand(thisObject);
        controller.Signal.SignalAbort(_realm.Intrinsics.AbortSignal.DefaultedReason(reason));
        return Undefined;
    }

    private JsAbortController Brand(JsValue thisObject)
    {
        if (thisObject is JsAbortController controller)
        {
            return controller;
        }

        Throw.TypeError(_realm, "Illegal invocation: receiver is not an AbortController");
        return null!;
    }
}
#endif
