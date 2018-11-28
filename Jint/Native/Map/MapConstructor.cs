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
        private MapConstructor(Engine engine, string name) : base(engine, name, null, null, false)
        {
        }

        public MapPrototype PrototypeObject { get; private set; }

        public static MapConstructor CreateMapConstructor(Engine engine)
        {
            MapConstructor CreateMapConstructorTemplate(string name)
            {
                var mapConstructor = new MapConstructor(engine, name);
                mapConstructor.Extensible = true;

                // The value of the [[Prototype]] internal property of the Map constructor is the Function prototype object
                mapConstructor.Prototype = engine.Function.PrototypeObject;
                mapConstructor.PrototypeObject = MapPrototype.CreatePrototypeObject(engine, mapConstructor);

                mapConstructor.SetOwnProperty("length", new PropertyDescriptor(0, PropertyFlag.Configurable));
                return mapConstructor;
            }

            var obj = CreateMapConstructorTemplate("Map");

            // The initial value of Map.prototype is the Map prototype object
            obj.SetOwnProperty("prototype", new PropertyDescriptor(obj.PrototypeObject, PropertyFlag.AllForbidden));

            obj.SetOwnProperty(GlobalSymbolRegistry.Species._value,
                new GetSetPropertyDescriptor(
                    get: new ClrFunctionInstance(engine, "get [Symbol.species]", Species, 0, PropertyFlag.Configurable),
                    set: Undefined,
                    PropertyFlag.Configurable));

            return obj;
        }

        public void Configure()
        {
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