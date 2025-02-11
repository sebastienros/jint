using Jint.Native.Function;
using Jint.Native.Iterator;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.WeakMap;

internal sealed class WeakMapConstructor : Constructor
{
    private static readonly JsString _functionName = new JsString("WeakMap");

    internal WeakMapConstructor(
        Engine engine,
        Realm realm,
        FunctionPrototype prototype,
        ObjectPrototype objectPrototype)
        : base(engine, realm, _functionName)
    {
        _prototype = prototype;
        PrototypeObject = new WeakMapPrototype(engine, realm, this, objectPrototype);
        _length = new PropertyDescriptor(0, PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    public WeakMapPrototype PrototypeObject { get; }

    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        if (newTarget.IsUndefined())
        {
            ExceptionHelper.ThrowTypeError(_realm);
        }

        var map = OrdinaryCreateFromConstructor(
            newTarget,
            static intrinsics =>  intrinsics.WeakMap.PrototypeObject,
            static (Engine engine, Realm _, object? _) => new JsWeakMap(engine));
        if (arguments.Length > 0 && !arguments[0].IsNullOrUndefined())
        {
            var adder = map.Get("set");
            var iterator = arguments.At(0).GetIterator(_realm);

            IteratorProtocol.AddEntriesFromIterable(map, iterator, adder);
        }

        return map;
    }
}
