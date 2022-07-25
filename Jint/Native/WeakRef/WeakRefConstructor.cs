using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.WeakRef;

/// <summary>
/// https://tc39.es/ecma262/#sec-weak-ref-constructor
/// </summary>
internal sealed class WeakRefConstructor : FunctionInstance, IConstructor
{
    private static readonly JsString _functionName = new("WeakRef");

    internal WeakRefConstructor(
        Engine engine,
        Realm realm,
        FunctionPrototype functionPrototype,
        ObjectPrototype objectPrototype)
        : base(engine, realm, _functionName)
    {
        _prototype = functionPrototype;
        PrototypeObject = new WeakRefPrototype(engine, realm, this, objectPrototype);
        _length = new PropertyDescriptor(1, PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    private WeakRefPrototype PrototypeObject { get; }

    protected internal override JsValue Call(JsValue thisObject, JsValue[] arguments)
    {
        ExceptionHelper.ThrowTypeError(_realm, "Constructor WeakRef requires 'new'");
        return null;
    }

    ObjectInstance IConstructor.Construct(JsValue[] arguments, JsValue newTarget)
    {
        if (newTarget.IsUndefined())
        {
            ExceptionHelper.ThrowTypeError(_realm);
        }

        var target = arguments.At(0);

        if (target is not ObjectInstance)
        {
            ExceptionHelper.ThrowTypeError(_realm, "WeakRef: target must be an object");
        }

        var weakRef = OrdinaryCreateFromConstructor(
            newTarget,
            static intrinsics => intrinsics.WeakRef.PrototypeObject,
            static (Engine engine, Realm _, object? t) => new WeakRefInstance(engine, (ObjectInstance) t!),
            target);

        _engine.AddToKeptObjects(target);

        return weakRef;
    }
}
