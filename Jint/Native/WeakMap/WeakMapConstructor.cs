using Jint.Native.Function;
using Jint.Native.Iterator;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.WeakMap
{
    public sealed class WeakMapConstructor : FunctionInstance, IConstructor
    {
        private static readonly JsString _functionName = new JsString("WeakMap");

        private WeakMapConstructor(Engine engine)
            : base(engine, _functionName)
        {
        }

        public WeakMapPrototype PrototypeObject { get; private set; }

        public static WeakMapConstructor CreateWeakMapConstructor(Engine engine)
        {
            var obj = new WeakMapConstructor(engine)
            {
                _prototype = engine.Function.PrototypeObject
            };

            // The value of the [[Prototype]] internal property of the WeakMap constructor is the WeakMap prototype object
            obj.PrototypeObject = WeakMapPrototype.CreatePrototypeObject(engine, obj);

            obj._length = new PropertyDescriptor(0, PropertyFlag.Configurable);

            // The initial value of WeakMap.prototype is the WeakMap prototype object
            obj._prototypeDescriptor = new PropertyDescriptor(obj.PrototypeObject, PropertyFlag.AllForbidden);

            return obj;
        }

        public override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
            ExceptionHelper.ThrowTypeError(_engine, "Constructor WeakMap requires 'new'");
            return null;
        }

        public ObjectInstance Construct(JsValue[] arguments, JsValue newTarget)
        {
            if (newTarget.IsUndefined())
            {
                ExceptionHelper.ThrowTypeError(_engine);
            }

            var map = OrdinaryCreateFromConstructor(newTarget, PrototypeObject, static (engine, _) => new WeakMapInstance(engine));
            if (arguments.Length > 0 && !arguments[0].IsNullOrUndefined())
            {
                var adder = map.Get("set");
                var iterator = arguments.At(0).GetIterator(_engine);

                IteratorProtocol.AddEntriesFromIterable(map, iterator, adder);
            }

            return map;
        }
    }
}