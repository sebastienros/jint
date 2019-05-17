using Jint.Collections;
using Jint.Native.Function;
using Jint.Native.Iterator;
using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Descriptors.Specialized;
using Jint.Runtime.Interop;

namespace Jint.Native.Map
{
    public sealed class MapConstructor : FunctionInstance, IConstructor
    {
        private static readonly JsString _functionName = new JsString("Map");

        private MapConstructor(Engine engine)
            : base(engine, _functionName, strict: false)
        {
        }

        public MapPrototype PrototypeObject { get; private set; }

        public static MapConstructor CreateMapConstructor(Engine engine)
        {
            var obj = new MapConstructor(engine)
            {
                Extensible = true,
                Prototype = engine.Function.PrototypeObject
            };

            // The value of the [[Prototype]] internal property of the Map constructor is the Function prototype object
            obj.PrototypeObject = MapPrototype.CreatePrototypeObject(engine, obj);

            obj._length = new PropertyDescriptor(0, PropertyFlag.Configurable);

            // The initial value of Map.prototype is the Map prototype object
            obj._prototype = new PropertyDescriptor(obj.PrototypeObject, PropertyFlag.AllForbidden);

            return obj;
        }

        protected override void Initialize()
        {
            _properties = new StringDictionarySlim<PropertyDescriptor>(2)
            {
                [GlobalSymbolRegistry.Species._value] = new GetSetPropertyDescriptor(get: new ClrFunctionInstance(_engine, "get [Symbol.species]", Species, 0, PropertyFlag.Configurable), set: Undefined, PropertyFlag.Configurable)
            };

        }

        private static JsValue Species(JsValue thisObject, JsValue[] arguments)
        {
            return thisObject;
        }

        public override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
            if (thisObject.IsUndefined())
            {
                ExceptionHelper.ThrowTypeError(_engine, "Constructor Map requires 'new'");
            }

            return Construct(arguments);
        }

        public ObjectInstance Construct(JsValue[] arguments)
        {
            var instance = new MapInstance(Engine)
            {
                Prototype = PrototypeObject,
                Extensible = true
            };

            if (arguments.Length > 0 && !arguments[0].IsNullOrUndefined())
            {
                var iterator = arguments.At(0).GetIterator(_engine);
                var mapProtocol = new MapProtocol(_engine, instance, iterator);
                mapProtocol.Execute();
            }

            return instance;
        }

        private sealed class MapProtocol : IteratorProtocol
        {
            private readonly MapInstance _instance;
            private readonly ICallable _setter;

            public MapProtocol(
                Engine engine,
                MapInstance instance,
                IIterator iterator) : base(engine, iterator, 2)
            {
                _instance = instance;
                var setterProperty = instance.GetProperty("set");

                if (setterProperty is null
                    || !setterProperty.TryGetValue(instance, out var setterValue)
                    || (_setter = setterValue as ICallable) is null)
                {
                    ExceptionHelper.ThrowTypeError(_engine, "set must be callable");
                }
            }

            protected override void ProcessItem(JsValue[] args, JsValue currentValue)
            {
                if (!(currentValue is ObjectInstance oi))
                {
                    ExceptionHelper.ThrowTypeError(_engine, "iterator's value must be an object");
                    return;
                }

                oi.TryGetValue("0", out var key);
                oi.TryGetValue("1", out var value);

                args[0] = key;
                args[1] = value;
                _setter.Call(_instance, args);
            }
        }
    }
}