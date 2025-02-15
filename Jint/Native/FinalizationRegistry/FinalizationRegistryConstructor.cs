using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.FinalizationRegistry;

/// <summary>
/// https://tc39.es/ecma262/#sec-finalization-registry-constructor
/// </summary>
internal sealed class FinalizationRegistryConstructor : Constructor
{
    private static readonly JsString _functionName = new("FinalizationRegistry");

    public FinalizationRegistryConstructor(
        Engine engine,
        Realm realm,
        FunctionConstructor functionConstructor,
        ObjectPrototype objectPrototype) : base(engine, realm, _functionName)
    {
        PrototypeObject = new FinalizationRegistryPrototype(engine, realm, this, objectPrototype);
        _prototype = functionConstructor.PrototypeObject;
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
        _length = new PropertyDescriptor(JsNumber.PositiveOne, PropertyFlag.Configurable);
    }

    public FinalizationRegistryPrototype PrototypeObject { get; }

    protected internal override JsValue Call(JsValue thisObject, JsCallArguments arguments)
    {
        return Construct(arguments, thisObject);
    }

    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        if (newTarget.IsUndefined())
        {
            ExceptionHelper.ThrowTypeError(_realm);
        }

        var cleanupCallback = arguments.At(0);
        if (cleanupCallback is not ICallable callable)
        {
            ExceptionHelper.ThrowTypeError(_realm, "cleanup must be callable");
            return null;
        }

        var finalizationRegistry = OrdinaryCreateFromConstructor(
            newTarget,
            static intrinsics => intrinsics.FinalizationRegistry.PrototypeObject,
            (engine, realm, state) => new FinalizationRegistryInstance(engine, realm, state!),
            callable
        );

        return finalizationRegistry;
    }
}
