using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.WeakRef;

/// <summary>
/// https://tc39.es/ecma262/#sec-weak-ref-constructor
/// </summary>
internal sealed class WeakRefConstructor : Constructor
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

    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        if (newTarget.IsUndefined())
        {
            ExceptionHelper.ThrowTypeError(_realm);
        }

        var target = arguments.At(0);

        if (!target.CanBeHeldWeakly(_engine.GlobalSymbolRegistry))
        {
            ExceptionHelper.ThrowTypeError(_realm, "WeakRef: target must be an object or symbol");
        }

        var weakRef = OrdinaryCreateFromConstructor(
            newTarget,
            static intrinsics => intrinsics.WeakRef.PrototypeObject,
            static (engine, _, target) => new JsWeakRef(engine, target!),
            target);

        _engine.AddToKeptObjects(target);

        return weakRef;
    }
}
