using Jint.Native.Function;
using Jint.Native.Iterator;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Descriptors.Specialized;

namespace Jint.Native.Map
{
    public sealed class MapConstructor : FunctionInstance, IConstructor
    {
        private MapConstructor(Engine engine, string name) :  base(engine, name, null, null, false)
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

            // TODO fix
            obj.SetOwnProperty(JsSymbol.species._value, new GetSetPropertyDescriptor(
                get: CreateMapConstructorTemplate("get [Symbol.species]"),
                set: Undefined,
                PropertyFlag.None));

            return obj;
        }

        public void Configure()
        {
        }

        public override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
            return Construct(arguments);
        }

        public ObjectInstance Construct(JsValue[] arguments)
        {
            IIterable iterable = null;
            if (arguments.Length > 0)
            {
                if (arguments.At(0) is IIterable it)
                {
                    iterable = it;
                }
            }

            var instance = new MapInstance(Engine, iterable)
            {
                Prototype = PrototypeObject,
                Extensible = true
            };

            return instance;
        }
    }
}
