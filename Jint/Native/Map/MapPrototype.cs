using Jint.Collections;
using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Descriptors.Specialized;
using Jint.Runtime.Interop;

namespace Jint.Native.Map
{
    /// <summary>
    /// https://www.ecma-international.org/ecma-262/6.0/#sec-map-objects
    /// </summary>
    public sealed class MapPrototype : ObjectInstance
    {
        private MapConstructor _mapConstructor;

        private MapPrototype(Engine engine) : base(engine)
        {
        }

        public static MapPrototype CreatePrototypeObject(Engine engine, MapConstructor mapConstructor)
        {
            var obj = new MapPrototype(engine)
            {
                Extensible = true,
                Prototype = engine.Object.PrototypeObject,
                _mapConstructor = mapConstructor
            };

            return obj;
        }

        protected override void Initialize()
        {
            _properties = new StringDictionarySlim<PropertyDescriptor>(15)
            {
                ["length"] = new PropertyDescriptor(0, PropertyFlag.Configurable),
                ["constructor"] = new PropertyDescriptor(_mapConstructor, PropertyFlag.NonEnumerable),
                [GlobalSymbolRegistry.Iterator._value] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "iterator", Iterator, 1, PropertyFlag.Configurable), true, false, true),
                [GlobalSymbolRegistry.ToStringTag._value] = new PropertyDescriptor("Map", false, false, true),
                ["clear"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "clear", Clear, 0, PropertyFlag.Configurable), true, false, true),
                ["delete"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "delete", Delete, 1, PropertyFlag.Configurable), true, false, true),
                ["entries"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "entries", Iterator, 0, PropertyFlag.Configurable), true, false, true),
                ["forEach"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "forEach", ForEach, 1, PropertyFlag.Configurable), true, false, true),
                ["get"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "get", Get, 1, PropertyFlag.Configurable), true, false, true),
                ["has"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "has", Has, 1, PropertyFlag.Configurable), true, false, true),
                ["keys"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "keys", Keys, 0, PropertyFlag.Configurable), true, false, true),
                ["set"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "set", Set, 2, PropertyFlag.Configurable), true, false, true),
                ["values"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "values", Values, 0, PropertyFlag.Configurable), true, false, true),
                ["size"] = new GetSetPropertyDescriptor(get: new ClrFunctionInstance(Engine, "get size", Size, 0, PropertyFlag.Configurable), set: null, PropertyFlag.Configurable)
            };
        }

        private JsValue Size(JsValue thisObj, JsValue[] arguments)
        {
            if (!(thisObj is MapInstance))
            {
                ExceptionHelper.ThrowTypeError(_engine);
            }
            return JsNumber.Create(0);
        }

        private JsValue Get(JsValue thisObj, JsValue[] arguments)
        {
            return ((MapInstance) thisObj).Get(arguments.At(0));
        }

        private JsValue Clear(JsValue thisObj, JsValue[] arguments)
        {
            var map = thisObj as MapInstance
                      ?? ExceptionHelper.ThrowTypeError<MapInstance>(_engine, "object must be a Map");

            map.Clear();
            return Undefined;
        }

        private JsValue Delete(JsValue thisObj, JsValue[] arguments)
        {
            var map = thisObj as MapInstance
                      ?? ExceptionHelper.ThrowTypeError<MapInstance>(_engine, "object must be a Map");

            return map.Delete(arguments[0])
                ? JsBoolean.True
                : JsBoolean.False;
        }

        private JsValue Set(JsValue thisObj, JsValue[] arguments)
        {
            ((MapInstance) thisObj).Set(arguments[0], arguments[1]);
            return thisObj;
        }

        private JsValue Has(JsValue thisObj, JsValue[] arguments)
        {
            return ((MapInstance) thisObj).Has(arguments[0])
                ? JsBoolean.True
                : JsBoolean.False;
        }

        private JsValue ForEach(JsValue thisObj, JsValue[] arguments)
        {
            var callbackfn = arguments.At(0);
            var thisArg = arguments.At(1);

            var map = (MapInstance) thisObj;

            var callable = GetCallable(callbackfn);

            map.ForEach(callable, thisArg);

            return Undefined;
        }

        private ObjectInstance Iterator(JsValue thisObj, JsValue[] arguments)
        {
            return ((MapInstance) thisObj).Iterator();
        }

        private ObjectInstance Keys(JsValue thisObj, JsValue[] arguments)
        {
            return ((MapInstance) thisObj).Keys();
        }

        private ObjectInstance Values(JsValue thisObj, JsValue[] arguments)
        {
            return ((MapInstance) thisObj).Values();
        }
    }
}