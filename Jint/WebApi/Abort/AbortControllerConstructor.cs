#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.WebApi.Abort;

/// <summary>
/// The <c>AbortController</c> interface object.
/// <para>
/// https://dom.spec.whatwg.org/#interface-abortcontroller
/// </para>
/// </summary>
/// <remarks>
/// <c>AbortController</c> does not inherit from anything, so its <c>[[Prototype]]</c> is
/// <c>%Function.prototype%</c>. It declares no static member, so it needs nothing from the source generator.
/// </remarks>
internal sealed class AbortControllerConstructor : Constructor
{
    private static readonly JsString _functionName = new("AbortController");

    private readonly AbortSignalConstructor _signalConstructor;

    internal AbortControllerConstructor(
        Engine engine,
        Realm realm,
        FunctionPrototype functionPrototype,
        ObjectPrototype objectPrototype,
        AbortSignalConstructor signalConstructor)
        : base(engine, realm, _functionName)
    {
        _prototype = functionPrototype;
        _signalConstructor = signalConstructor;
        PrototypeObject = new AbortControllerPrototype(engine, realm, this, objectPrototype);
        _length = new PropertyDescriptor(JsNumber.PositiveZero, PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    internal AbortControllerPrototype PrototypeObject { get; }

    /// <summary>
    /// https://dom.spec.whatwg.org/#dom-abortcontroller-abortcontroller
    /// </summary>
    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        // The signal is built first and handed to the creator, so that a subclass's prototype only affects the
        // controller: `class C extends AbortController {}` still gets a plain AbortSignal, which is what the
        // specification's "let signal be a new AbortSignal object" says.
        var signal = _signalConstructor.CreateSignal();

        return OrdinaryCreateFromConstructor(
            newTarget,
            static intrinsics => intrinsics.AbortController.PrototypeObject,
            static (Engine engine, Realm _, JsAbortSignal? state) => new JsAbortController(engine, state!),
            signal);
    }
}
#endif
