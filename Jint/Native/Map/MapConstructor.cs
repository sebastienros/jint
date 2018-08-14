using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Map
{
    public sealed class MapConstructor : FunctionInstance, IConstructor
    {
        private MapConstructor(Engine engine) :  base(engine, null, null, false)
        {
        }

        public MapPrototype PrototypeObject { get; private set; }

        public static MapConstructor CreateMapConstructor(Engine engine)
        {
            var obj = new MapConstructor(engine);
            obj.Extensible = true;

            // The value of the [[Prototype]] internal property of the Map constructor is the Function prototype object
            obj.Prototype = engine.Function.PrototypeObject;
            obj.PrototypeObject = MapPrototype.CreatePrototypeObject(engine, obj);

            obj.SetOwnProperty("length", new PropertyDescriptor(0, PropertyFlag.Configurable));

            // The initial value of Map.prototype is the Map prototype object
            obj.SetOwnProperty("prototype", new PropertyDescriptor(obj.PrototypeObject, PropertyFlag.AllForbidden));

            obj.SetOwnProperty("name", new PropertyDescriptor("Map", PropertyFlag.Configurable));

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
            var instance = new MapInstance(Engine)
            {
                Prototype = PrototypeObject,
                Extensible = true
            };

            return instance;
        }
    }
}
